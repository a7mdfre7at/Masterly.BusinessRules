namespace Masterly.BusinessRules.UnitTests;

public class ExplainTests
{
    private const string IdCode = "KYC.ID";
    private const string AddressCode = "KYC.ADDRESS";
    private const string SanctionsCode = "KYC.SANCTIONS";
    private const string GroupCode = "KYC.INCOMPLETE";
    private const string GroupMessage = "All KYC checks must pass";
    private const string FlagKey = "flag";

    [Fact]
    public void Explain_LeafRule_ReturnsSingleNode()
    {
        IBusinessRule rule = RuleTestHelper.CreateBrokenRule(IdCode, "Id must be verified");

        RuleExplanation explanation = rule.Explain();

        Assert.Same(rule, explanation.Rule);
        Assert.Equal(RuleOutcome.Broken, explanation.Outcome);
        Assert.True(explanation.IsBroken);
        Assert.Equal(string.Empty, explanation.Operator);
        Assert.Equal(string.Empty, explanation.Reason);
        Assert.Empty(explanation.Children);
        Assert.Equal(IdCode, explanation.Code);
        Assert.Equal("Id must be verified", explanation.Message);
    }

    [Fact]
    public void Explain_PassingLeafRule_ReportsPassed()
    {
        RuleExplanation explanation = RuleTestHelper.CreatePassingRule(IdCode).Explain();

        Assert.Equal(RuleOutcome.Passed, explanation.Outcome);
        Assert.False(explanation.IsBroken);
        Assert.Empty(explanation.BrokenLeaves());
    }

    [Fact]
    public void Explain_ThirdPartyRule_IsSingleLeafEvaluatedOnce()
    {
        ThirdPartyRule rule = new ThirdPartyRule();

        RuleExplanation explanation = rule.Explain();

        Assert.Equal(1, rule.Evaluations);
        Assert.Empty(explanation.Children);
        Assert.Equal(RuleOutcome.Broken, explanation.Outcome);
    }

    [Fact]
    public void Explain_EvaluatesEveryRuleExactlyOnce()
    {
        int idEvaluations = 0;
        int addressEvaluations = 0;
        int sanctionsEvaluations = 0;
        IBusinessRule tree = RuleGroup.All(GroupCode, GroupMessage,
            CreateCountingRule(IdCode, broken: false, () => idEvaluations++),
            CreateCountingRule(AddressCode, broken: true, () => addressEvaluations++).And(CreateCountingRule(SanctionsCode, broken: true, () => sanctionsEvaluations++)));

        RuleExplanation explanation = tree.Explain();

        Assert.True(explanation.IsBroken);
        Assert.Equal(1, idEvaluations);
        Assert.Equal(1, addressEvaluations);
        Assert.Equal(1, sanctionsEvaluations);
    }

    [Fact]
    public void Explain_AllGroupWithShortCircuit_MarksRemainingMembersNotEvaluated()
    {
        IBusinessRule kyc = RuleGroup.All(GroupCode, GroupMessage,
            RuleTestHelper.CreatePassingRule(IdCode),
            RuleTestHelper.CreateBrokenRule(AddressCode, "Address must be verified"),
            RuleTestHelper.CreateBrokenRule(SanctionsCode)).WithShortCircuit();

        RuleExplanation why = kyc.Explain();

        Assert.Equal(RuleOutcome.Broken, why.Outcome);
        Assert.Equal("ALL", why.Operator);
        Assert.Equal(3, why.Children.Count);
        Assert.Equal(RuleOutcome.Passed, why.Children[0].Outcome);
        Assert.Equal(RuleOutcome.Broken, why.Children[1].Outcome);
        Assert.Equal(RuleOutcome.NotEvaluated, why.Children[2].Outcome);
        Assert.Equal("short-circuit", why.Children[2].Reason);
        Assert.Equal(TimeSpan.Zero, why.Children[2].Duration);
    }

    [Fact]
    public void Explain_AtLeastGroup_RendersThresholdInOperator()
    {
        IBusinessRule tier = RuleGroup.AtLeast(2, "TIER.INSUFFICIENT", "Need two of three signals",
            RuleTestHelper.CreatePassingRule("S1"), RuleTestHelper.CreateBrokenRule("S2"), RuleTestHelper.CreateBrokenRule("S3"));

        RuleExplanation why = tier.Explain();

        Assert.Equal("AT LEAST 2", why.Operator);
        Assert.True(why.IsBroken);
    }

    [Fact]
    public void Explain_AndWhenFirstPasses_SecondIsNotEvaluated()
    {
        IBusinessRule composed = RuleTestHelper.CreatePassingRule(IdCode).And(RuleTestHelper.CreateBrokenRule(AddressCode));

        RuleExplanation why = composed.Explain();

        Assert.Equal("AND", why.Operator);
        Assert.Equal(RuleOutcome.Passed, why.Outcome);
        Assert.Equal(RuleOutcome.Passed, why.Children[0].Outcome);
        Assert.Equal(RuleOutcome.NotEvaluated, why.Children[1].Outcome);
    }

    [Fact]
    public void Explain_OrWhenFirstBroken_SecondIsNotEvaluated()
    {
        IBusinessRule composed = RuleTestHelper.CreateBrokenRule(IdCode).Or(RuleTestHelper.CreateBrokenRule(AddressCode));

        RuleExplanation why = composed.Explain();

        Assert.Equal("OR", why.Operator);
        Assert.True(why.IsBroken);
        Assert.Equal(RuleOutcome.NotEvaluated, why.Children[1].Outcome);
    }

    [Fact]
    public void Explain_Not_ReportsNotOperatorAndChild()
    {
        IBusinessRule composed = RuleTestHelper.CreatePassingRule(IdCode).Not();

        RuleExplanation why = composed.Explain();

        Assert.Equal("NOT", why.Operator);
        Assert.True(why.IsBroken);
        RuleExplanation child = Assert.Single(why.Children);
        Assert.Equal(RuleOutcome.Passed, child.Outcome);
        Assert.Same(why, Assert.Single(why.BrokenLeaves()));
    }

    [Fact]
    public void Explain_Composite_ReportsAllOperatorWithEveryChild()
    {
        CompositeBusinessRule composite = new CompositeBusinessRule(new[] { RuleTestHelper.CreateBrokenRule(IdCode), RuleTestHelper.CreateBrokenRule(AddressCode) });

        RuleExplanation why = composite.Explain();

        Assert.Equal("ALL", why.Operator);
        Assert.Equal(2, why.Children.Count);
        Assert.All(why.Children, child => Assert.Equal(RuleOutcome.Broken, child.Outcome));
    }

    [Fact]
    public void Explain_ConditionalWithFalsePrecondition_ReportsSkipped()
    {
        int evaluations = 0;
        IBusinessRule conditional = CreateCountingRule(IdCode, broken: true, () => evaluations++).When(() => false);

        RuleExplanation why = conditional.Explain();

        Assert.Equal(RuleOutcome.Skipped, why.Outcome);
        Assert.False(why.IsBroken);
        Assert.Equal("WHEN", why.Operator);
        Assert.Equal("precondition not met", why.Reason);
        RuleExplanation child = Assert.Single(why.Children);
        Assert.Equal(RuleOutcome.NotEvaluated, child.Outcome);
        Assert.Equal(0, evaluations);
    }

    [Fact]
    public void Explain_ConditionalWithTruePrecondition_ReportsInnerOutcome()
    {
        IBusinessRule conditional = RuleTestHelper.CreateBrokenRule(IdCode).When(() => true);

        RuleExplanation why = conditional.Explain();

        Assert.Equal(RuleOutcome.Broken, why.Outcome);
        Assert.Equal("WHEN", why.Operator);
        Assert.Equal(RuleOutcome.Broken, Assert.Single(why.Children).Outcome);
    }

    [Fact]
    public void Explain_CachedRule_SecondExplainReportsCacheHit()
    {
        int evaluations = 0;
        IBusinessRule cached = CreateCountingRule(IdCode, broken: true, () => evaluations++).Cached(TimeSpan.FromMinutes(5));

        RuleExplanation first = cached.Explain();
        RuleExplanation second = cached.Explain();

        Assert.Equal(RuleOutcome.Broken, first.Outcome);
        Assert.Equal("CACHED", first.Operator);
        Assert.Equal(RuleOutcome.Broken, Assert.Single(first.Children).Outcome);
        Assert.Equal(RuleOutcome.CacheHit, second.Outcome);
        Assert.True(second.IsBroken);
        Assert.Equal("cache hit", second.Reason);
        Assert.Equal(RuleOutcome.NotEvaluated, Assert.Single(second.Children).Outcome);
        Assert.Equal(1, evaluations);
    }

    [Fact]
    public void Explain_CachedPassingRule_CacheHitIsNotBroken()
    {
        IBusinessRule cached = RuleTestHelper.CreatePassingRule(IdCode).Cached(TimeSpan.FromMinutes(5));
        cached.Explain();

        RuleExplanation second = cached.Explain();

        Assert.Equal(RuleOutcome.CacheHit, second.Outcome);
        Assert.False(second.IsBroken);
    }

    [Fact]
    public void Explain_ThrowingRule_WithoutCapture_Propagates()
    {
        IBusinessRule throwing = CreateThrowingRule(IdCode);

        Assert.Throws<InvalidOperationException>(() => throwing.Explain());
    }

    [Fact]
    public void Explain_ThrowingRule_WithCapture_ReportsFaulted()
    {
        IBusinessRule throwing = CreateThrowingRule(IdCode);

        RuleExplanation why = throwing.Explain(null, captureExceptions: true);

        Assert.Equal(RuleOutcome.Faulted, why.Outcome);
        Assert.True(why.IsBroken);
        Assert.IsType<InvalidOperationException>(why.Fault);
        Assert.Contains("InvalidOperationException", why.Reason);
        Assert.Contains("boom", why.Reason);
    }

    [Fact]
    public void Explain_FaultedMemberInAllGroup_WithCapture_TreatsMemberAsBroken()
    {
        IBusinessRule group = RuleGroup.All(GroupCode, GroupMessage, RuleTestHelper.CreatePassingRule(IdCode), CreateThrowingRule(AddressCode));

        RuleExplanation why = group.Explain(null, captureExceptions: true);

        Assert.Equal(RuleOutcome.Broken, why.Outcome);
        Assert.Equal(RuleOutcome.Faulted, why.Children[1].Outcome);
        Assert.Equal(AddressCode, Assert.Single(why.BrokenLeaves()).Code);
    }

    [Fact]
    public void Explain_WithContext_ForwardsContextThroughComposedNodes()
    {
        IBusinessRule composed = CreateContextReadingRule(IdCode).And(CreateContextReadingRule(AddressCode));

        RuleExplanation broken = composed.Explain(RuleTestHelper.CreateContext((FlagKey, true)));
        RuleExplanation passed = composed.Explain(RuleTestHelper.CreateContext((FlagKey, false)));

        Assert.True(broken.IsBroken);
        Assert.False(passed.IsBroken);
    }

    [Fact]
    public void Explain_WithoutContext_UsesParameterlessOverload()
    {
        OverloadRecordingRule recording = new OverloadRecordingRule();

        recording.Explain();

        Assert.False(recording.LastCallHadContext);
    }

    [Fact]
    public void Explain_WithContext_UsesContextOverload()
    {
        OverloadRecordingRule recording = new OverloadRecordingRule();

        recording.Explain(RuleTestHelper.CreateContext());

        Assert.True(recording.LastCallHadContext);
    }

    [Fact]
    public void Explain_NullRule_ThrowsArgumentNullException()
    {
        IBusinessRule rule = null!;

        Assert.Throws<ArgumentNullException>(() => rule.Explain());
    }

    [Fact]
    public void Explain_AsyncToSyncAdapter_ExpandsIntoAsyncSubtree()
    {
        IAsyncBusinessRule asyncRule = RuleTestHelper.CreateBrokenAsyncRule(IdCode).And(RuleTestHelper.CreateBrokenAsyncRule(AddressCode));
        AsyncToSyncRuleAdapter adapter = new AsyncToSyncRuleAdapter(asyncRule);

        RuleExplanation why = adapter.Explain();

        Assert.Equal("ADAPTER", why.Operator);
        Assert.Same(adapter, why.Rule);
        RuleExplanation child = Assert.Single(why.Children);
        Assert.Same(asyncRule, child.AsyncRule);
        Assert.Equal("AND", child.Operator);
        Assert.Equal(2, child.Children.Count);
    }

    [Fact]
    public void Explain_ToText_RendersEveryOutcomeAndOperatorLabel()
    {
        IBusinessRule cached = RuleTestHelper.CreatePassingRule("CACHED.RULE").Cached(TimeSpan.FromMinutes(5));
        cached.IsBroken();
        IBusinessRule tree = RuleGroup.Exactly(1, "TOP", "Exactly one",
            RuleTestHelper.CreateBrokenRule("SKIPPED.RULE").When(() => false),
            cached,
            BusinessRuleBuilder.Create("FAULTED.RULE").WithMessage("faulted").When(() => throw new InvalidOperationException("boom")).Build(),
            RuleTestHelper.CreatePassingRule("INVERTED").Not());

        RuleExplanation why = tree.Explain(captureExceptions: true);
        string[] lines = why.ToText().Split(Environment.NewLine);

        Assert.Contains("] EXACTLY 1  TOP", lines[0]);
        Assert.Contains(lines, line => line.Contains("[SKIPPED] WHEN  SKIPPED.RULE", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("[CACHE HIT] CACHED  CACHED.RULE", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("[FAULTED]  FAULTED.RULE", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("[BROKEN] NOT  !INVERTED", StringComparison.Ordinal));
    }

    [Fact]
    public void Explain_Durations_AreNonNegativeAndZeroForNotEvaluated()
    {
        IBusinessRule group = RuleGroup.All(GroupCode, GroupMessage, RuleTestHelper.CreateBrokenRule(IdCode), RuleTestHelper.CreatePassingRule(AddressCode)).WithShortCircuit();

        RuleExplanation why = group.Explain();

        Assert.True(why.Duration >= TimeSpan.Zero);
        Assert.True(why.Children[0].Duration >= TimeSpan.Zero);
        Assert.Equal(TimeSpan.Zero, why.Children[1].Duration);
    }

    [Fact]
    public void Explain_BrokenLeaves_ReturnsDeepestBrokenNodes()
    {
        IBusinessRule tree = RuleGroup.All(GroupCode, GroupMessage,
            RuleTestHelper.CreatePassingRule(IdCode),
            RuleGroup.Any("PAYMENT", "payment", RuleTestHelper.CreateBrokenRule("CARD"), RuleTestHelper.CreateBrokenRule("BANK")),
            RuleTestHelper.CreateBrokenRule(SanctionsCode));

        RuleExplanation why = tree.Explain();

        Assert.Equal(new[] { "CARD", "BANK", SanctionsCode }, why.BrokenLeaves().Select(leaf => leaf.Code));
    }

    [Fact]
    public void Explain_ToText_RendersSketchShape()
    {
        IBusinessRule kyc = RuleGroup.All(GroupCode, GroupMessage,
            RuleTestHelper.CreatePassingRule(IdCode),
            RuleTestHelper.CreateBrokenRule(AddressCode, "Address must be verified"),
            RuleTestHelper.CreateBrokenRule(SanctionsCode)).WithShortCircuit();

        string[] lines = kyc.Explain().ToText().Split(Environment.NewLine);

        Assert.Equal(4, lines.Length);
        Assert.StartsWith("[BROKEN] ALL  KYC.INCOMPLETE  \"All KYC checks must pass\"  (", lines[0]);
        Assert.EndsWith(" ms)", lines[0]);
        Assert.StartsWith(" ├─ [PASSED]  KYC.ID  (", lines[1]);
        Assert.StartsWith(" ├─ [BROKEN]  KYC.ADDRESS  \"Address must be verified\"  (", lines[2]);
        Assert.Equal(" └─ [NOT EVALUATED]  KYC.SANCTIONS  reason: short-circuit", lines[3]);
    }

    [Fact]
    public void Explain_CheckAfterExplain_ProducesSameVerdictWithoutExtraEvaluationOfExplain()
    {
        int evaluations = 0;
        IBusinessRule group = RuleGroup.All(GroupCode, GroupMessage, CreateCountingRule(IdCode, broken: true, () => evaluations++));

        RuleExplanation why = group.Explain();
        Assert.Throws<BusinessRuleValidationException>(() => group.Check());

        Assert.True(why.IsBroken);
        Assert.Equal(2, evaluations);
    }

    private static IBusinessRule CreateCountingRule(string code, bool broken, Action onEvaluated)
    {
        return BusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .When(() =>
            {
                onEvaluated();
                return broken;
            })
            .Build();
    }

    private static IBusinessRule CreateContextReadingRule(string code)
    {
        return BusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .When(ctx => ctx.Get<bool>(FlagKey))
            .Build();
    }

    private static IBusinessRule CreateThrowingRule(string code)
    {
        return BusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .When(() => throw new InvalidOperationException("boom"))
            .Build();
    }

    private sealed class ThirdPartyRule : IBusinessRule
    {
        public int Evaluations { get; private set; }

        public string Code => "THIRD.PARTY";

        public string Message => "Third-party rule";

        public bool IsBroken()
        {
            Evaluations++;
            return true;
        }

        public bool IsBroken(BusinessRuleContext context) => IsBroken();

        public void Check()
        {
            if (IsBroken())
            {
                throw new BusinessRuleValidationException(new[] { Evaluate() });
            }
        }

        public void Check(BusinessRuleContext context) => Check();

        public BusinessRuleResult Evaluate() => new BusinessRuleResult(Code, Message, RuleSeverity.Error);
    }

    private sealed class OverloadRecordingRule : BaseBusinessRule
    {
        public bool? LastCallHadContext { get; private set; }

        public override string Code => "OVERLOAD";

        public override string Message => "Overload recording rule";

        public override bool IsBroken()
        {
            LastCallHadContext = false;
            return false;
        }

        public override bool IsBroken(BusinessRuleContext context)
        {
            LastCallHadContext = true;
            return false;
        }
    }
}
