namespace Masterly.BusinessRules.UnitTests;

/// <summary>
/// Covers <c>IBusinessRule.Evaluate(BusinessRuleContext)</c>: the default interface member, the explicit members on
/// every in-library node, decorator, adapter and composite, and the metadata every construction site now populates.
/// </summary>
public class ContextAwareEvaluateTests
{
    private const string AmountKey = "amount";
    private const string Category = "Payments";
    private const string Tag = "customer-facing";

    private sealed class ContextDescribeRule : BaseBusinessRule
    {
        private readonly bool _broken;

        public ContextDescribeRule(bool broken = true) => _broken = broken;

        public int Evaluations { get; private set; }

        public override string Code => "CTX";
        public override string Message => "Context rule";
        public override string Name => "ContextRule";
        public override string Category => ContextAwareEvaluateTests.Category;
        public override IReadOnlyList<string> Tags => new[] { Tag };

        public override bool IsBroken()
        {
            Evaluations++;
            return _broken;
        }

        protected override RuleResultDetails Describe(BusinessRuleContext? context)
        {
            if (context == null)
            {
                return RuleResultDetails.Detail($"no context").Expected("context");
            }

            decimal amount = context.Get<decimal>(AmountKey);
            return RuleResultDetails.Detail($"Amount is {amount}").Actual(amount);
        }
    }

    /// <summary>A rule compiled against the interface without <c>Evaluate(context)</c>, as a third-party 2.x rule would be.</summary>
    private sealed class ThirdPartyRule : IBusinessRule
    {
        public string Code => "THIRD";
        public string Message => "Third party";
        public bool IsBroken() => true;
        public bool IsBroken(BusinessRuleContext context) => true;
        public void Check() => throw new BusinessRuleValidationException(new[] { Evaluate() });
        public void Check(BusinessRuleContext context) => Check();
        public BusinessRuleResult Evaluate() => new BusinessRuleResult(Code, Message, RuleSeverity.Warning);
    }

    private sealed class MetadataAsyncRule : BaseAsyncBusinessRule
    {
        public override string Code => "ASYNC";
        public override string Message => "Async rule";
        public override string Name => "AsyncName";
        public override string Category => ContextAwareEvaluateTests.Category;
        public override IReadOnlyList<string> Tags => new[] { Tag };

        public override Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private static BusinessRuleContext Context(decimal amount) => RuleTestHelper.CreateContext((AmountKey, amount));

    // ---------------------------------------------------------------------
    // Default interface member
    // ---------------------------------------------------------------------

    [Fact]
    public void Evaluate_WithContext_ThirdPartyRule_FallsBackToParameterlessEvaluate()
    {
        IBusinessRule rule = new ThirdPartyRule();

        BusinessRuleResult result = rule.Evaluate(Context(1m));

        Assert.Equal("THIRD", result.Code);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal("THIRD", result.Name);
        Assert.Null(result.Detail);
    }

    [Fact]
    public void Checker_ThirdPartyRule_UsesParameterlessEvaluateThroughTheDefaultMember()
    {
        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(Context(1m), new ThirdPartyRule());

        Assert.Equal("THIRD", Assert.Single(result.BrokenRules).Code);
    }

    // ---------------------------------------------------------------------
    // Structural nodes: factories carrying the node's metadata
    // ---------------------------------------------------------------------

    [Fact]
    public void AndBusinessRule_EvaluateWithContext_IsFactoryWithNodeMetadata()
    {
        AndBusinessRule node = new AndBusinessRule(new ContextDescribeRule(), new ContextDescribeRule());

        BusinessRuleResult result = node.Evaluate(Context(1m));

        Assert.Equal(node.Code, result.Code);
        Assert.Equal(nameof(AndBusinessRule), result.Name);
        Assert.Equal(string.Empty, result.Category);
        Assert.Empty(result.Tags);
        Assert.Empty(result.Details);
        Assert.Null(result.Detail);
    }

    [Fact]
    public void OrBusinessRule_EvaluateWithContext_IsFactoryWithNodeMetadata()
    {
        OrBusinessRule node = new OrBusinessRule(new ContextDescribeRule(), new ContextDescribeRule());

        BusinessRuleResult result = node.Evaluate(Context(1m));

        Assert.Equal(node.Code, result.Code);
        Assert.Equal(nameof(OrBusinessRule), result.Name);
        Assert.Empty(result.Details);
    }

    [Fact]
    public void NotBusinessRule_EvaluateWithContext_IsFactoryWithNodeMetadata()
    {
        NotBusinessRule node = new NotBusinessRule(new ContextDescribeRule(broken: false));

        BusinessRuleResult result = node.Evaluate(Context(1m));

        Assert.Equal(node.Code, result.Code);
        Assert.Equal(nameof(NotBusinessRule), result.Name);
    }

    [Fact]
    public void GroupBusinessRule_EvaluateWithContext_IsFactoryWithNodeMetadata()
    {
        GroupBusinessRule group = RuleGroup.All("G", "Group", new ContextDescribeRule());

        BusinessRuleResult result = group.Evaluate(Context(1m));

        Assert.Equal("G", result.Code);
        Assert.Equal(nameof(GroupBusinessRule), result.Name);
        Assert.Empty(result.Details);
    }

    [Fact]
    public void CompositeBusinessRule_EvaluateWithContext_IsFactoryWithNodeMetadata()
    {
        CompositeBusinessRule composite = new CompositeBusinessRule(new[] { new ContextDescribeRule() });

        BusinessRuleResult result = composite.Evaluate(Context(1m));

        Assert.Equal("CompositeRule", result.Code);
        Assert.Equal("CompositeBusinessRule", result.Name);
        Assert.Empty(result.Details);
    }

    [Fact]
    public void AndBusinessRule_Check_MemberResultsCarryLeafMetadataAndContextDetails()
    {
        AndBusinessRule node = new AndBusinessRule(new ContextDescribeRule(), new ContextDescribeRule());

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => node.Check(Context(42m)));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(nameof(AndBusinessRule), result.Name);
        Assert.Equal(2, result.Details.Count);
        Assert.All(result.Details, member =>
        {
            Assert.Equal("ContextRule", member.Name);
            Assert.Equal(Category, member.Category);
            Assert.Equal("Amount is 42", member.Detail);
            Assert.Equal(42m, member.Actual);
        });
    }

    [Fact]
    public void GroupBusinessRule_Check_MemberResultsCarryContextDetails()
    {
        GroupBusinessRule group = RuleGroup.All("G", "Group", new ContextDescribeRule(), RuleTestHelper.CreatePassingRule());

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => group.Check(Context(5m)));

        BusinessRuleResult member = Assert.Single(Assert.Single(exception.BrokenRules).Details);
        Assert.Equal("Amount is 5", member.Detail);
        Assert.True(member.HasTag(Tag));
    }

    [Fact]
    public void CompositeBusinessRule_EvaluateAllWithContext_RoutesThroughContextOverload()
    {
        CompositeBusinessRule composite = new CompositeBusinessRule(new[] { new ContextDescribeRule() });

        IReadOnlyCollection<BusinessRuleResult> results = composite.EvaluateAll(Context(3m));

        Assert.Equal("Amount is 3", Assert.Single(results).Detail);
    }

    [Fact]
    public void CompositeBusinessRule_EvaluateAllWithoutContext_CallsDescribeWithNull()
    {
        CompositeBusinessRule composite = new CompositeBusinessRule(new[] { new ContextDescribeRule() });

        IReadOnlyCollection<BusinessRuleResult> results = composite.EvaluateAll();

        Assert.Equal("no context", Assert.Single(results).Detail);
    }

    // ---------------------------------------------------------------------
    // Decorators
    // ---------------------------------------------------------------------

    [Fact]
    public void CachedBusinessRule_EvaluateWithContext_ForwardsToInner()
    {
        CachedBusinessRule cached = new CachedBusinessRule(new ContextDescribeRule(), TimeSpan.FromMinutes(5));

        BusinessRuleResult result = cached.Evaluate(Context(8m));

        Assert.Equal("Amount is 8", result.Detail);
        Assert.Equal("ContextRule", result.Name);
    }

    [Fact]
    public void CachedBusinessRule_Evaluate_ForwardsToInnerWithNullContext()
    {
        CachedBusinessRule cached = new CachedBusinessRule(new ContextDescribeRule(), TimeSpan.FromMinutes(5));

        BusinessRuleResult result = cached.Evaluate();

        Assert.Equal("no context", result.Detail);
    }

    [Fact]
    public void CachedBusinessRule_CacheHit_ReportsInnerFactoryWithCurrentContext()
    {
        ContextDescribeRule inner = new ContextDescribeRule();
        CachedBusinessRule cached = new CachedBusinessRule(inner, TimeSpan.FromMinutes(5));
        Assert.True(cached.IsBroken(Context(1m)));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => cached.Check(Context(2m)));

        Assert.Equal(1, inner.Evaluations);
        Assert.Equal("Amount is 2", Assert.Single(exception.BrokenRules).Detail);
    }

    [Fact]
    public void CachedBusinessRule_CacheHitWithoutContext_ReportsInnerFactory()
    {
        ContextDescribeRule inner = new ContextDescribeRule();
        CachedBusinessRule cached = new CachedBusinessRule(inner, TimeSpan.FromMinutes(5));
        Assert.True(cached.IsBroken());

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => cached.Check());

        Assert.Equal(1, inner.Evaluations);
        Assert.Equal("no context", Assert.Single(exception.BrokenRules).Detail);
    }

    [Fact]
    public void ConditionalBusinessRule_EvaluateWithContext_ForwardsToInner()
    {
        ConditionalBusinessRule conditional = new ConditionalBusinessRule(new ContextDescribeRule(), () => true);

        BusinessRuleResult result = conditional.Evaluate(Context(4m));

        Assert.Equal("Amount is 4", result.Detail);
        Assert.Equal(Category, result.Category);
    }

    [Fact]
    public void ConditionalBusinessRule_CheckWithContext_CarriesContextDetails()
    {
        ConditionalBusinessRule conditional = new ConditionalBusinessRule(new ContextDescribeRule(), () => true);

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => conditional.Check(Context(6m)));

        Assert.Equal("Amount is 6", Assert.Single(exception.BrokenRules).Detail);
    }

    [Fact]
    public void SeverityOverrideBusinessRule_EvaluateWithContext_KeepsDetailsAndOverridesSeverity()
    {
        SeverityOverrideBusinessRule softened = new SeverityOverrideBusinessRule(new ContextDescribeRule(), RuleSeverity.Warning);

        BusinessRuleResult result = softened.Evaluate(Context(9m));

        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal("Amount is 9", result.Detail);
        Assert.Equal("ContextRule", result.Name);
        Assert.True(result.HasTag(Tag));
    }

    [Fact]
    public void SeverityOverrideBusinessRule_Evaluate_KeepsDetailsAndOverridesSeverity()
    {
        SeverityOverrideBusinessRule softened = new SeverityOverrideBusinessRule(new ContextDescribeRule(), RuleSeverity.Info);

        BusinessRuleResult result = softened.Evaluate();

        Assert.Equal(RuleSeverity.Info, result.Severity);
        Assert.Equal("no context", result.Detail);
    }

    [Fact]
    public void SeverityOverrideBusinessRule_Check_KeepsMetadataDetailsAndMemberDetails()
    {
        GroupBusinessRule group = RuleGroup.All("G", "Group", new ContextDescribeRule());
        IBusinessRule softened = group.WithSeverity(RuleSeverity.Warning);

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => softened.Check(Context(7m)));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(nameof(GroupBusinessRule), result.Name);
        Assert.Equal("Amount is 7", Assert.Single(result.Details).Detail);
    }

    // ---------------------------------------------------------------------
    // Adapter
    // ---------------------------------------------------------------------

    [Fact]
    public void AsyncToSyncRuleAdapter_EvaluateWithContext_CarriesForwardedMetadata()
    {
        AsyncToSyncRuleAdapter adapter = new AsyncToSyncRuleAdapter(new MetadataAsyncRule());

        BusinessRuleResult result = adapter.Evaluate(Context(1m));

        Assert.Equal("ASYNC", result.Code);
        Assert.Equal("AsyncName", result.Name);
        Assert.Equal(Category, result.Category);
        Assert.True(result.HasTag(Tag));
        Assert.Null(result.Detail);
    }

    [Fact]
    public void AsyncToSyncRuleAdapter_Evaluate_CarriesForwardedMetadata()
    {
        AsyncToSyncRuleAdapter adapter = new AsyncToSyncRuleAdapter(new MetadataAsyncRule());

        BusinessRuleResult result = adapter.Evaluate();

        Assert.Equal("AsyncName", result.Name);
    }

    // ---------------------------------------------------------------------
    // Interface-typed callers reach the explicit members
    // ---------------------------------------------------------------------

    [Fact]
    public void Evaluate_WithContext_ThroughInterface_ReachesDecoratorImplementation()
    {
        IBusinessRule rule = new ContextDescribeRule().Cached(TimeSpan.FromMinutes(1)).WithSeverity(RuleSeverity.Warning);

        BusinessRuleResult result = rule.Evaluate(Context(11m));

        Assert.Equal("Amount is 11", result.Detail);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
    }
}
