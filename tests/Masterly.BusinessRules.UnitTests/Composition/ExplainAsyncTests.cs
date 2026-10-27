namespace Masterly.BusinessRules.UnitTests;

public class ExplainAsyncTests
{
    private const string IdCode = "KYC.ID";
    private const string AddressCode = "KYC.ADDRESS";
    private const string SanctionsCode = "KYC.SANCTIONS";
    private const string GroupCode = "KYC.INCOMPLETE";
    private const string GroupMessage = "All KYC checks must pass";
    private const string FlagKey = "flag";

    [Fact]
    public async Task ExplainAsync_LeafRule_ReturnsSingleNode()
    {
        IAsyncBusinessRule rule = RuleTestHelper.CreateBrokenAsyncRule(IdCode, "Id must be verified");

        RuleExplanation explanation = await rule.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.Same(rule, explanation.AsyncRule);
        Assert.Null(explanation.Rule);
        Assert.Equal(RuleOutcome.Broken, explanation.Outcome);
        Assert.True(explanation.IsBroken);
        Assert.Equal(string.Empty, explanation.Operator);
        Assert.Empty(explanation.Children);
        Assert.Equal("Id must be verified", explanation.Message);
    }

    [Fact]
    public async Task ExplainAsync_PassingLeafRule_ReportsPassed()
    {
        RuleExplanation explanation = await RuleTestHelper.CreatePassingAsyncRule(IdCode).ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(RuleOutcome.Passed, explanation.Outcome);
        Assert.False(explanation.IsBroken);
    }

    [Fact]
    public async Task ExplainAsync_ThirdPartyRule_IsSingleLeafEvaluatedOnce()
    {
        ThirdPartyAsyncRule rule = new ThirdPartyAsyncRule();

        RuleExplanation explanation = await rule.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(1, rule.Evaluations);
        Assert.Empty(explanation.Children);
        Assert.Equal(RuleOutcome.Broken, explanation.Outcome);
    }

    [Fact]
    public async Task ExplainAsync_EvaluatesEveryRuleExactlyOnce()
    {
        int idEvaluations = 0;
        int addressEvaluations = 0;
        int sanctionsEvaluations = 0;
        IAsyncBusinessRule tree = AsyncRuleGroup.All(GroupCode, GroupMessage,
            CreateCountingRule(IdCode, broken: false, () => idEvaluations++),
            CreateCountingRule(AddressCode, broken: true, () => addressEvaluations++).And(CreateCountingRule(SanctionsCode, broken: true, () => sanctionsEvaluations++)));

        RuleExplanation explanation = await tree.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(explanation.IsBroken);
        Assert.Equal(1, idEvaluations);
        Assert.Equal(1, addressEvaluations);
        Assert.Equal(1, sanctionsEvaluations);
    }

    [Fact]
    public async Task ExplainAsync_AllGroupWithShortCircuit_MarksRemainingMembersNotEvaluated()
    {
        IAsyncBusinessRule kyc = AsyncRuleGroup.All(GroupCode, GroupMessage,
            RuleTestHelper.CreatePassingAsyncRule(IdCode),
            RuleTestHelper.CreateBrokenAsyncRule(AddressCode, "Address must be verified"),
            RuleTestHelper.CreateBrokenAsyncRule(SanctionsCode)).WithShortCircuit();

        RuleExplanation why = await kyc.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(RuleOutcome.Broken, why.Outcome);
        Assert.Equal("ALL", why.Operator);
        Assert.Equal(RuleOutcome.Passed, why.Children[0].Outcome);
        Assert.Equal(RuleOutcome.Broken, why.Children[1].Outcome);
        Assert.Equal(RuleOutcome.NotEvaluated, why.Children[2].Outcome);
        Assert.Equal("short-circuit", why.Children[2].Reason);
    }

    [Fact]
    public async Task ExplainAsync_ParallelGroup_KeepsChildrenInDeclarationOrder()
    {
        IAsyncBusinessRule any = AsyncRuleGroup.Any(GroupCode, GroupMessage,
            RuleTestHelper.CreateBrokenAsyncRule("A"),
            RuleTestHelper.CreatePassingAsyncRule("B"),
            RuleTestHelper.CreateBrokenAsyncRule("C")).WithParallelEvaluation();

        RuleExplanation why = await any.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.False(why.IsBroken);
        Assert.Equal("ANY", why.Operator);
        Assert.Equal(new[] { "A", "B", "C" }, why.Children.Select(child => child.Code));
        Assert.Equal(new[] { RuleOutcome.Broken, RuleOutcome.Passed, RuleOutcome.Broken }, why.Children.Select(child => child.Outcome));
    }

    [Fact]
    public async Task ExplainAsync_AndWhenFirstPasses_SecondIsNotEvaluated()
    {
        IAsyncBusinessRule composed = RuleTestHelper.CreatePassingAsyncRule(IdCode).And(RuleTestHelper.CreateBrokenAsyncRule(AddressCode));

        RuleExplanation why = await composed.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal("AND", why.Operator);
        Assert.Equal(RuleOutcome.NotEvaluated, why.Children[1].Outcome);
    }

    [Fact]
    public async Task ExplainAsync_Not_ReportsNotOperatorAndChild()
    {
        IAsyncBusinessRule composed = RuleTestHelper.CreatePassingAsyncRule(IdCode).Not();

        RuleExplanation why = await composed.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal("NOT", why.Operator);
        Assert.True(why.IsBroken);
        Assert.Equal(RuleOutcome.Passed, Assert.Single(why.Children).Outcome);
    }

    [Fact]
    public async Task ExplainAsync_CompositeWithRules_ReportsAllOperatorWithEveryChild()
    {
        CompositeAsyncBusinessRule composite = new CompositeAsyncBusinessRule(new[] { RuleTestHelper.CreateBrokenAsyncRule(IdCode), RuleTestHelper.CreateBrokenAsyncRule(AddressCode) });

        RuleExplanation why = await composite.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal("ALL", why.Operator);
        Assert.Equal(2, why.Children.Count);
    }

    [Fact]
    public async Task ExplainAsync_CompositeWithDelegate_IsLeaf()
    {
        int evaluations = 0;
        CompositeAsyncBusinessRule composite = new CompositeAsyncBusinessRule(
            (ctx, ct) =>
            {
                evaluations++;
                return Task.FromResult(true);
            },
            "Delegate broken",
            "DELEGATE");

        RuleExplanation why = await composite.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, why.Operator);
        Assert.Empty(why.Children);
        Assert.Equal(RuleOutcome.Broken, why.Outcome);
        Assert.Equal("DELEGATE", why.Code);
        Assert.Equal(1, evaluations);
    }

    [Fact]
    public async Task ExplainAsync_ConditionalWithFalsePrecondition_ReportsSkipped()
    {
        int evaluations = 0;
        IAsyncBusinessRule conditional = CreateCountingRule(IdCode, broken: true, () => evaluations++).When(ctx => false);

        RuleExplanation why = await conditional.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(RuleOutcome.Skipped, why.Outcome);
        Assert.False(why.IsBroken);
        Assert.Equal("WHEN", why.Operator);
        Assert.Equal("precondition not met", why.Reason);
        Assert.Equal(RuleOutcome.NotEvaluated, Assert.Single(why.Children).Outcome);
        Assert.Equal(0, evaluations);
    }

    [Fact]
    public async Task ExplainAsync_CachedRule_SecondExplainReportsCacheHit()
    {
        int evaluations = 0;
        IAsyncBusinessRule cached = CreateCountingRule(IdCode, broken: true, () => evaluations++).Cached(TimeSpan.FromMinutes(5));
        BusinessRuleContext context = RuleTestHelper.CreateContext();

        RuleExplanation first = await cached.ExplainAsync(context, TestContext.Current.CancellationToken);
        RuleExplanation second = await cached.ExplainAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(RuleOutcome.Broken, first.Outcome);
        Assert.Equal("CACHED", first.Operator);
        Assert.Equal(RuleOutcome.CacheHit, second.Outcome);
        Assert.True(second.IsBroken);
        Assert.Equal("cache hit", second.Reason);
        Assert.Equal(RuleOutcome.NotEvaluated, Assert.Single(second.Children).Outcome);
        Assert.Equal(1, evaluations);
    }

    [Fact]
    public async Task ExplainAsync_ThrowingRule_WithoutCapture_Propagates()
    {
        IAsyncBusinessRule throwing = CreateThrowingRule(IdCode);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => throwing.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExplainAsync_ThrowingRule_WithCapture_ReportsFaulted()
    {
        IAsyncBusinessRule throwing = CreateThrowingRule(IdCode);

        RuleExplanation why = await throwing.ExplainAsync(RuleTestHelper.CreateContext(), captureExceptions: true, TestContext.Current.CancellationToken);

        Assert.Equal(RuleOutcome.Faulted, why.Outcome);
        Assert.True(why.IsBroken);
        Assert.IsType<InvalidOperationException>(why.Fault);
        Assert.Contains("boom", why.Reason);
    }

    [Fact]
    public async Task ExplainAsync_FaultedMemberInAllGroup_WithCapture_TreatsMemberAsBroken()
    {
        IAsyncBusinessRule group = AsyncRuleGroup.All(GroupCode, GroupMessage, RuleTestHelper.CreatePassingAsyncRule(IdCode), CreateThrowingRule(AddressCode));

        RuleExplanation why = await group.ExplainAsync(RuleTestHelper.CreateContext(), captureExceptions: true, TestContext.Current.CancellationToken);

        Assert.Equal(RuleOutcome.Broken, why.Outcome);
        Assert.Equal(RuleOutcome.Faulted, why.Children[1].Outcome);
    }

    [Fact]
    public async Task ExplainAsync_WithCapture_CancellationStillPropagates()
    {
        IAsyncBusinessRule cancellable = AsyncBusinessRuleBuilder.Create(IdCode).WithMessage(IdCode)
            .WhenAsync((ctx, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(true);
            })
            .Build();
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cancellable.ExplainAsync(RuleTestHelper.CreateContext(), captureExceptions: true, cancellation.Token));
    }

    [Fact]
    public async Task ExplainAsync_ForwardsContextThroughComposedNodes()
    {
        IAsyncBusinessRule composed = CreateContextReadingRule(IdCode).And(CreateContextReadingRule(AddressCode));

        RuleExplanation broken = await composed.ExplainAsync(RuleTestHelper.CreateContext((FlagKey, true)), TestContext.Current.CancellationToken);
        RuleExplanation passed = await composed.ExplainAsync(RuleTestHelper.CreateContext((FlagKey, false)), TestContext.Current.CancellationToken);

        Assert.True(broken.IsBroken);
        Assert.False(passed.IsBroken);
    }

    [Fact]
    public async Task ExplainAsync_NullRule_ThrowsArgumentNullException()
    {
        IAsyncBusinessRule rule = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(() => rule.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExplainAsync_NullContext_ThrowsArgumentNullException()
    {
        IAsyncBusinessRule rule = RuleTestHelper.CreatePassingAsyncRule(IdCode);

        await Assert.ThrowsAsync<ArgumentNullException>(() => rule.ExplainAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExplainAsync_SyncToAsyncAdapter_ExpandsIntoSyncSubtree()
    {
        IBusinessRule syncRule = RuleTestHelper.CreateBrokenRule(IdCode).Or(RuleTestHelper.CreateBrokenRule(AddressCode));
        IAsyncBusinessRule adapter = syncRule.ToAsync();

        RuleExplanation why = await adapter.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal("ADAPTER", why.Operator);
        Assert.Same(adapter, why.AsyncRule);
        RuleExplanation child = Assert.Single(why.Children);
        Assert.Same(syncRule, child.Rule);
        Assert.Equal("OR", child.Operator);
    }

    [Fact]
    public async Task ExplainAsync_BrokenLeaves_ReturnsDeepestBrokenNodes()
    {
        IAsyncBusinessRule tree = AsyncRuleGroup.All(GroupCode, GroupMessage,
            RuleTestHelper.CreatePassingAsyncRule(IdCode),
            AsyncRuleGroup.Any("PAYMENT", "payment", RuleTestHelper.CreateBrokenAsyncRule("CARD"), RuleTestHelper.CreateBrokenAsyncRule("BANK")),
            RuleTestHelper.CreateBrokenAsyncRule(SanctionsCode));

        RuleExplanation why = await tree.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "CARD", "BANK", SanctionsCode }, why.BrokenLeaves().Select(leaf => leaf.Code));
    }

    [Fact]
    public async Task ExplainAsync_ToText_RendersSketchShape()
    {
        IAsyncBusinessRule kyc = AsyncRuleGroup.All(GroupCode, GroupMessage,
            RuleTestHelper.CreatePassingAsyncRule(IdCode),
            RuleTestHelper.CreateBrokenAsyncRule(AddressCode, "Address must be verified"),
            RuleTestHelper.CreateBrokenAsyncRule(SanctionsCode)).WithShortCircuit();

        RuleExplanation why = await kyc.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);
        string[] lines = why.ToText().Split(Environment.NewLine);

        Assert.Equal(4, lines.Length);
        Assert.StartsWith("[BROKEN] ALL  KYC.INCOMPLETE", lines[0]);
        Assert.StartsWith(" ├─ [PASSED]  KYC.ID", lines[1]);
        Assert.StartsWith(" ├─ [BROKEN]  KYC.ADDRESS  \"Address must be verified\"", lines[2]);
        Assert.Equal(" └─ [NOT EVALUATED]  KYC.SANCTIONS  reason: short-circuit", lines[3]);
    }

    [Fact]
    public async Task ExplainAsync_ConditionalWithTruePrecondition_ReportsInnerOutcome()
    {
        IAsyncBusinessRule conditional = RuleTestHelper.CreateBrokenAsyncRule(IdCode).When(ctx => true);

        RuleExplanation why = await conditional.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(RuleOutcome.Broken, why.Outcome);
        Assert.Equal("WHEN", why.Operator);
        Assert.Equal(RuleOutcome.Broken, Assert.Single(why.Children).Outcome);
    }

    [Fact]
    public async Task ExplainAsync_OrWhenFirstBroken_SecondIsNotEvaluated()
    {
        int secondEvaluations = 0;
        IAsyncBusinessRule composed = RuleTestHelper.CreateBrokenAsyncRule(IdCode).Or(CreateCountingRule(AddressCode, broken: true, () => secondEvaluations++));

        RuleExplanation why = await composed.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal("OR", why.Operator);
        Assert.True(why.IsBroken);
        Assert.Equal(RuleOutcome.NotEvaluated, why.Children[1].Outcome);
        Assert.Equal(0, secondEvaluations);
    }

    [Fact]
    public async Task ExplainAsync_AtLeastGroup_RendersThresholdInOperator()
    {
        IAsyncBusinessRule tier = AsyncRuleGroup.AtLeast(2, "TIER.INSUFFICIENT", "Need two of three signals",
            RuleTestHelper.CreatePassingAsyncRule("S1"), RuleTestHelper.CreateBrokenAsyncRule("S2"), RuleTestHelper.CreateBrokenAsyncRule("S3"));

        RuleExplanation why = await tier.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal("AT LEAST 2", why.Operator);
        Assert.True(why.IsBroken);
    }

    [Fact]
    public async Task ExplainAsync_CachedPassingRule_CacheHitIsNotBroken()
    {
        IAsyncBusinessRule cached = RuleTestHelper.CreatePassingAsyncRule(IdCode).Cached(TimeSpan.FromMinutes(5));
        await cached.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        RuleExplanation second = await cached.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(RuleOutcome.CacheHit, second.Outcome);
        Assert.False(second.IsBroken);
    }

    [Fact]
    public async Task ExplainAsync_CheckAfterExplain_ProducesSameVerdictWithoutExtraEvaluationOfExplain()
    {
        int evaluations = 0;
        IAsyncBusinessRule group = AsyncRuleGroup.All(GroupCode, GroupMessage, CreateCountingRule(IdCode, broken: true, () => evaluations++));

        RuleExplanation why = await group.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<BusinessRuleValidationException>(() => group.CheckAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken));

        Assert.True(why.IsBroken);
        Assert.Equal(2, evaluations);
    }

    [Fact]
    public async Task ExplainAsync_Durations_AreNonNegativeAndZeroForNotEvaluated()
    {
        IAsyncBusinessRule group = AsyncRuleGroup.All(GroupCode, GroupMessage, RuleTestHelper.CreateBrokenAsyncRule(IdCode), RuleTestHelper.CreatePassingAsyncRule(AddressCode)).WithShortCircuit();

        RuleExplanation why = await group.ExplainAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(why.Duration >= TimeSpan.Zero);
        Assert.True(why.Children[0].Duration >= TimeSpan.Zero);
        Assert.Equal(TimeSpan.Zero, why.Children[1].Duration);
    }

    [Fact]
    public async Task ExplainAsync_ToText_RendersEveryOutcomeAndOperatorLabel()
    {
        IAsyncBusinessRule cached = RuleTestHelper.CreatePassingAsyncRule("CACHED.RULE").Cached(TimeSpan.FromMinutes(5));
        await cached.IsBrokenAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);
        IAsyncBusinessRule tree = AsyncRuleGroup.Exactly(1, "TOP", "Exactly one",
            RuleTestHelper.CreateBrokenAsyncRule("SKIPPED.RULE").When(ctx => false),
            cached,
            CreateThrowingRule("FAULTED.RULE"),
            RuleTestHelper.CreatePassingAsyncRule("INVERTED").Not());

        RuleExplanation why = await tree.ExplainAsync(RuleTestHelper.CreateContext(), captureExceptions: true, TestContext.Current.CancellationToken);
        string[] lines = why.ToText().Split(Environment.NewLine);

        Assert.Contains("] EXACTLY 1  TOP", lines[0]);
        Assert.Contains(lines, line => line.Contains("[SKIPPED] WHEN  SKIPPED.RULE", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("[CACHE HIT] CACHED  CACHED.RULE", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("[FAULTED]  FAULTED.RULE", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("[BROKEN] NOT  !INVERTED", StringComparison.Ordinal));
    }

    private static IAsyncBusinessRule CreateCountingRule(string code, bool broken, Action onEvaluated)
    {
        return AsyncBusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .WhenAsync((ctx, ct) =>
            {
                onEvaluated();
                return Task.FromResult(broken);
            })
            .Build();
    }

    private static IAsyncBusinessRule CreateContextReadingRule(string code)
    {
        return AsyncBusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .WhenAsync((ctx, ct) => Task.FromResult(ctx.Get<bool>(FlagKey)))
            .Build();
    }

    private static IAsyncBusinessRule CreateThrowingRule(string code)
    {
        return AsyncBusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .WhenAsync((ctx, ct) => Task.FromException<bool>(new InvalidOperationException("boom")))
            .Build();
    }

    private sealed class ThirdPartyAsyncRule : IAsyncBusinessRule
    {
        public int Evaluations { get; private set; }

        public string Code => "THIRD.PARTY.ASYNC";

        public string Message => "Third-party async rule";

        public Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            Evaluations++;
            return Task.FromResult(true);
        }

        public async Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            return await IsBrokenAsync(context, cancellationToken) ? new BusinessRuleResult(Code, Message, RuleSeverity.Error) : null;
        }

        public async Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            BusinessRuleResult? result = await EvaluateAsync(context, cancellationToken);
            if (result != null)
            {
                throw new BusinessRuleValidationException(new[] { result });
            }
        }
    }
}
