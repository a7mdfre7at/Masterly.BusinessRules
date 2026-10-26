namespace Masterly.BusinessRules.UnitTests;

public class FeatureGatedRuleTests
{
    private const string Code = "CREDIT.SCORE";
    private const string Message = "Credit score is too low";
    private const string Name = "CreditScore";
    private const string Description = "Checks the new credit score model";
    private const string Category = "Credit";
    private const string Flag = "credit-score-v2";
    private const string CustomerIdKey = "customerId";

    // ---------------------------------------------------------------------
    // Construction and metadata
    // ---------------------------------------------------------------------

    [Fact]
    public void GatedBy_ReturnsFeatureGatedBusinessRule()
    {
        IBusinessRule inner = CreateRule(broken: true);
        InMemoryRuleFeatureFlagProvider provider = RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced);

        FeatureGatedBusinessRule gated = inner.GatedBy(Flag, provider);

        Assert.Same(inner, gated.InnerRule);
        Assert.Equal(Flag, gated.Flag);
        Assert.Same(provider, gated.Provider);
        Assert.False(gated.HasStableKey);
        Assert.IsAssignableFrom<IDecoratedBusinessRule>(gated);
    }

    [Fact]
    public void GatedBy_WithoutProvider_LeavesProviderNull()
    {
        FeatureGatedBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, stableKey: ctx => ctx.Get<string>(CustomerIdKey));

        Assert.Null(gated.Provider);
        Assert.True(gated.HasStableKey);
    }

    [Fact]
    public void GatedBy_ForwardsEveryMetadataMember()
    {
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        Assert.Equal(Code, gated.Code);
        Assert.Equal(Message, gated.Message);
        Assert.Equal(RuleSeverity.Warning, gated.Severity);
        Assert.Equal(Name, gated.Name);
        Assert.Equal(Description, gated.Description);
        Assert.Equal(Category, gated.Category);
        Assert.Equal(new[] { "a", "b" }, gated.Tags);
    }

    [Fact]
    public void GatedBy_NullRule_ThrowsArgumentNullException()
    {
        IBusinessRule rule = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => rule.GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced)));
        Assert.Throws<ArgumentNullException>(() => rule.GatedBy(Flag));
    }

    [Fact]
    public void GatedBy_NullProvider_ThrowsArgumentNullException()
    {
        IRuleFeatureFlagProvider provider = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => CreateRule(broken: true).GatedBy(Flag, provider));
    }

    [Fact]
    public void Constructor_NullFlag_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new FeatureGatedBusinessRule(CreateRule(broken: true), null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_BlankFlag_ThrowsArgumentException(string flag)
    {
        Assert.Throws<ArgumentException>(() => new FeatureGatedBusinessRule(CreateRule(broken: true), flag));
    }

    [Fact]
    public void Constructor_NullInnerRule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new FeatureGatedBusinessRule(null!, Flag));
    }

    // ---------------------------------------------------------------------
    // Modes
    // ---------------------------------------------------------------------

    [Fact]
    public void IsBroken_WithContext_FlagOff_PassesWithoutEvaluatingInner()
    {
        int evaluations = 0;
        IBusinessRule gated = CreateCountingRule(() => evaluations++, broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Off));

        bool broken = gated.IsBroken(new BusinessRuleContext());

        Assert.False(broken);
        Assert.Equal(0, evaluations);
    }

    [Fact]
    public void IsBroken_WithContext_UnknownFlag_IsOff()
    {
        int evaluations = 0;
        IBusinessRule gated = CreateCountingRule(() => evaluations++, broken: true).GatedBy("other-flag", RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        Assert.False(gated.IsBroken(new BusinessRuleContext()));
        Assert.Equal(0, evaluations);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsBroken_WithContext_Enforced_ForwardsVerdict(bool broken)
    {
        IBusinessRule gated = CreateRule(broken).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        Assert.Equal(broken, gated.IsBroken(new BusinessRuleContext()));
    }

    [Fact]
    public void IsBroken_WithContext_Shadow_EvaluatesInnerButPasses()
    {
        int evaluations = 0;
        IBusinessRule gated = CreateCountingRule(() => evaluations++, broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));

        bool broken = gated.IsBroken(new BusinessRuleContext());

        Assert.False(broken);
        Assert.Equal(1, evaluations);
    }

    [Fact]
    public void IsBroken_WithContext_ForwardsContextToInnerRule()
    {
        IBusinessRule contextRule = BusinessRuleBuilder.Create(Code).WithMessage(Message).When(ctx => ctx.Get<bool>("flag")).Build();
        IBusinessRule gated = contextRule.GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        Assert.True(gated.IsBroken(RuleTestHelper.CreateContext(("flag", true))));
        Assert.False(gated.IsBroken(RuleTestHelper.CreateContext(("flag", false))));
    }

    [Fact]
    public void IsBroken_NullContext_ThrowsArgumentNullException()
    {
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        Assert.Throws<ArgumentNullException>(() => gated.IsBroken(null!));
        Assert.Throws<ArgumentNullException>(() => gated.Check(null!));
    }

    [Fact]
    public void Check_WithContext_Enforced_ThrowsWithInnerResult()
    {
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => gated.Check(new BusinessRuleContext()));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(Code, result.Code);
        Assert.Equal(Message, result.Message);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
    }

    [Fact]
    public void Check_WithContext_Shadow_DoesNotThrow()
    {
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));

        gated.Check(new BusinessRuleContext());
    }

    [Fact]
    public void Check_WithContext_Off_DoesNotThrow()
    {
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Off));

        gated.Check(new BusinessRuleContext());
    }

    [Fact]
    public void Check_OnComposedInnerRule_KeepsMemberDetails()
    {
        IBusinessRule group = RuleGroup.All("GROUP", "group", RuleTestHelper.CreateBrokenRule("M1"), RuleTestHelper.CreatePassingRule("M2"));
        IBusinessRule gated = group.GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => gated.Check(new BusinessRuleContext()));

        Assert.Equal("M1", Assert.Single(Assert.Single(exception.BrokenRules).Details).Code);
    }

    // ---------------------------------------------------------------------
    // Un-gated parameterless path
    // ---------------------------------------------------------------------

    [Fact]
    public void IsBroken_Parameterless_EvaluatesInnerUngated()
    {
        RecordingProvider provider = new RecordingProvider(RuleRollout.Off);
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, provider);

        bool broken = gated.IsBroken();

        Assert.True(broken);
        Assert.Empty(provider.Calls);
    }

    [Fact]
    public void Check_Parameterless_EvaluatesInnerUngated()
    {
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Off));

        Assert.Throws<BusinessRuleValidationException>(() => gated.Check());
    }

    [Fact]
    public void IsBroken_Parameterless_WithoutProvider_DoesNotThrow()
    {
        IBusinessRule gated = CreateRule(broken: false).GatedBy(Flag);

        Assert.False(gated.IsBroken());
    }

    [Fact]
    public void And_WithoutContext_NeverConsultsProvider()
    {
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag);
        IBusinessRule composed = gated.And(RuleTestHelper.CreateBrokenRule("OTHER"));

        Assert.True(composed.IsBroken());
    }

    // ---------------------------------------------------------------------
    // Provider resolution
    // ---------------------------------------------------------------------

    [Fact]
    public void IsBroken_WithContext_NoProviderAnywhere_ThrowsRuleConfigurationException()
    {
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag);

        RuleConfigurationException exception = Assert.Throws<RuleConfigurationException>(() => gated.IsBroken(new BusinessRuleContext()));

        Assert.Equal(Code, exception.RuleCode);
        Assert.Contains(Flag, exception.Message);
        Assert.Contains(Code, exception.Message);
    }

    [Fact]
    public void IsBroken_WithContext_ProviderFromContextKey_IsUsed()
    {
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag);
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(RuleFeatureFlags.ContextKey, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        Assert.True(gated.IsBroken(context));
    }

    [Fact]
    public void IsBroken_WithContext_ContextKeyOverridesInjectedProvider()
    {
        RecordingProvider injected = new RecordingProvider(RuleRollout.Off);
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, injected);
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(RuleFeatureFlags.ContextKey, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        Assert.True(gated.IsBroken(context));
        Assert.Empty(injected.Calls);
    }

    [Fact]
    public void IsBroken_WithContext_PassesFlagAndContextToProvider()
    {
        RecordingProvider provider = new RecordingProvider(RuleRollout.Enforced());
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, provider);
        BusinessRuleContext context = new BusinessRuleContext();

        gated.IsBroken(context);

        (string flag, BusinessRuleContext seen) = Assert.Single(provider.Calls);
        Assert.Equal(Flag, flag);
        Assert.Same(context, seen);
    }

    [Fact]
    public void IsBroken_WithContext_ProviderIsConsultedOnEveryEvaluation_SoRampingIsLive()
    {
        int evaluations = 0;
        InMemoryRuleFeatureFlagProvider provider = RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Off);
        IBusinessRule gated = CreateCountingRule(() => evaluations++, broken: true).GatedBy(Flag, provider);
        BusinessRuleContext context = new BusinessRuleContext();

        bool whileOff = gated.IsBroken(context);
        provider.Set(Flag, RuleRolloutMode.Shadow);
        bool whileShadow = gated.IsBroken(context);
        provider.Set(Flag, RuleRolloutMode.Enforced);
        bool whileEnforced = gated.IsBroken(context);

        Assert.False(whileOff);
        Assert.False(whileShadow);
        Assert.True(whileEnforced);
        Assert.Equal(2, evaluations);
    }

    [Fact]
    public void IsBroken_WithContext_ProviderThrows_Propagates()
    {
        ThrowingProvider provider = new ThrowingProvider();
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, provider);

        Assert.Throws<InvalidOperationException>(() => gated.IsBroken(new BusinessRuleContext()));
    }

    // ---------------------------------------------------------------------
    // Percentage rollout
    // ---------------------------------------------------------------------

    [Fact]
    public void IsBroken_PartialRollout_InsideCohort_Evaluates()
    {
        string insideKey = KeyWithBucket(Flag, bucket => bucket < 10);
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced, percent: 10), ctx => ctx.Get<string>(CustomerIdKey));

        Assert.True(gated.IsBroken(RuleTestHelper.CreateContext((CustomerIdKey, insideKey))));
    }

    [Fact]
    public void IsBroken_PartialRollout_OutsideCohort_PassesWithoutEvaluating()
    {
        int evaluations = 0;
        string outsideKey = KeyWithBucket(Flag, bucket => bucket >= 10);
        IBusinessRule gated = CreateCountingRule(() => evaluations++, broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced, percent: 10), ctx => ctx.Get<string>(CustomerIdKey));

        Assert.False(gated.IsBroken(RuleTestHelper.CreateContext((CustomerIdKey, outsideKey))));
        Assert.Equal(0, evaluations);
    }

    [Fact]
    public void IsBroken_PartialRollout_IsStableForTheSameKey()
    {
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced, percent: 50), ctx => ctx.Get<string>(CustomerIdKey));
        BusinessRuleContext context = RuleTestHelper.CreateContext((CustomerIdKey, "customer-7"));

        bool first = gated.IsBroken(context);
        bool second = gated.IsBroken(context);

        Assert.Equal(first, second);
    }

    [Fact]
    public void IsBroken_PartialRollout_WithoutStableKey_ThrowsRuleConfigurationException()
    {
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced, percent: 10));

        RuleConfigurationException exception = Assert.Throws<RuleConfigurationException>(() => gated.IsBroken(new BusinessRuleContext()));

        Assert.Equal(Code, exception.RuleCode);
        Assert.Contains("10%", exception.Message);
    }

    [Fact]
    public void IsBroken_PartialRollout_StableKeyReturnsNull_ThrowsRuleConfigurationException()
    {
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced, percent: 10), ctx => null!);

        Assert.Throws<RuleConfigurationException>(() => gated.IsBroken(new BusinessRuleContext()));
    }

    [Fact]
    public void IsBroken_PartialRollout_MissingContextKey_ThrowsContextKeyNotFoundException()
    {
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced, percent: 10), ctx => ctx.Get<string>(CustomerIdKey));

        Assert.Throws<ContextKeyNotFoundException>(() => gated.IsBroken(new BusinessRuleContext()));
    }

    [Fact]
    public void IsBroken_FullRollout_NeverCallsStableKey()
    {
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced), ctx => throw new InvalidOperationException("stable key must not be called"));

        Assert.True(gated.IsBroken(new BusinessRuleContext()));
    }

    [Fact]
    public void IsBroken_ZeroPercent_NeverCallsStableKeyAndPasses()
    {
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced, percent: 0), ctx => throw new InvalidOperationException("stable key must not be called"));

        Assert.False(gated.IsBroken(new BusinessRuleContext()));
    }

    // ---------------------------------------------------------------------
    // Evaluate factories
    // ---------------------------------------------------------------------

    [Fact]
    public void Evaluate_IsAPureFactoryForwardingTheInnerResult()
    {
        int evaluations = 0;
        IBusinessRule gated = CreateCountingRule(() => evaluations++, broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Off));

        BusinessRuleResult result = gated.Evaluate();
        BusinessRuleResult contextResult = gated.Evaluate(new BusinessRuleContext());

        Assert.Equal(0, evaluations);
        Assert.Equal(Code, result.Code);
        Assert.Equal(Code, contextResult.Code);
        Assert.Equal(Message, result.Message);
    }

    // ---------------------------------------------------------------------
    // Explain
    // ---------------------------------------------------------------------

    [Fact]
    public void Explain_FlagOff_ReportsSkippedGateWithReason()
    {
        IBusinessRule inner = CreateRule(broken: true);
        IBusinessRule gated = inner.GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Off));

        RuleExplanation explanation = gated.Explain(new BusinessRuleContext());

        Assert.Same(gated, explanation.Rule);
        Assert.Equal("GATED", explanation.Operator);
        Assert.Equal(RuleOutcome.Skipped, explanation.Outcome);
        Assert.False(explanation.IsBroken);
        Assert.Equal($"flag '{Flag}' is off", explanation.Reason);
        RuleExplanation child = Assert.Single(explanation.Children);
        Assert.Same(inner, child.Rule);
        Assert.Equal(RuleOutcome.NotEvaluated, child.Outcome);
    }

    [Fact]
    public void Explain_OutsideCohort_ReportsSkippedGateWithBucketReason()
    {
        string outsideKey = KeyWithBucket(Flag, bucket => bucket >= 10);
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced, percent: 10), ctx => ctx.Get<string>(CustomerIdKey));

        RuleExplanation explanation = gated.Explain(RuleTestHelper.CreateContext((CustomerIdKey, outsideKey)));

        Assert.Equal(RuleOutcome.Skipped, explanation.Outcome);
        Assert.Equal($"flag '{Flag}' excludes bucket {RuleRolloutBucket.Compute(Flag, outsideKey)} (10%)", explanation.Reason);
    }

    [Fact]
    public void Explain_Enforced_ReportsGateNodeAboveInnerRule()
    {
        IBusinessRule inner = CreateRule(broken: true);
        IBusinessRule gated = inner.GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        RuleExplanation explanation = gated.Explain(new BusinessRuleContext());

        Assert.Equal("GATED", explanation.Operator);
        Assert.Equal(RuleOutcome.Broken, explanation.Outcome);
        Assert.True(explanation.IsBroken);
        Assert.Equal(string.Empty, explanation.Reason);
        RuleExplanation child = Assert.Single(explanation.Children);
        Assert.Same(inner, child.Rule);
        Assert.Equal(RuleOutcome.Broken, child.Outcome);
    }

    [Fact]
    public void Explain_ShadowViolation_ReportsPassedShadowNodeWithBrokenChild()
    {
        IBusinessRule inner = CreateRule(broken: true);
        IBusinessRule gated = inner.GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));

        RuleExplanation explanation = gated.Explain(new BusinessRuleContext());

        Assert.Equal("SHADOW", explanation.Operator);
        Assert.Equal(RuleOutcome.Passed, explanation.Outcome);
        Assert.False(explanation.IsBroken);
        Assert.Equal("shadow violation", explanation.Reason);
        RuleExplanation child = Assert.Single(explanation.Children);
        Assert.Equal(RuleOutcome.Broken, child.Outcome);
        Assert.Empty(explanation.BrokenLeaves());
    }

    [Fact]
    public void Explain_ShadowPass_ReportsPassedShadowNodeWithoutReason()
    {
        IBusinessRule gated = CreateRule(broken: false).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));

        RuleExplanation explanation = gated.Explain(new BusinessRuleContext());

        Assert.Equal("SHADOW", explanation.Operator);
        Assert.Equal(RuleOutcome.Passed, explanation.Outcome);
        Assert.Equal(string.Empty, explanation.Reason);
    }

    [Fact]
    public void Explain_WithoutContext_ReportsUngatedEvaluation()
    {
        IBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Off));

        RuleExplanation explanation = gated.Explain();

        Assert.Equal("GATED", explanation.Operator);
        Assert.Equal(RuleOutcome.Broken, explanation.Outcome);
        Assert.Equal("no context: evaluated un-gated", explanation.Reason);
    }

    [Fact]
    public void Explain_EvaluatesInnerRuleExactlyOnce()
    {
        int evaluations = 0;
        IBusinessRule gated = CreateCountingRule(() => evaluations++, broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        gated.Explain(new BusinessRuleContext());

        Assert.Equal(1, evaluations);
    }

    // ---------------------------------------------------------------------
    // Interaction with CachedBusinessRule
    // ---------------------------------------------------------------------

    [Fact]
    public void CacheInsideGate_GateIsDecidedOnEveryCallAndCacheServesTheInnerRule()
    {
        int evaluations = 0;
        InMemoryRuleFeatureFlagProvider provider = RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Off);
        IBusinessRule gated = CreateCountingRule(() => evaluations++, broken: true).Cached(TimeSpan.FromMinutes(5)).GatedBy(Flag, provider);
        BusinessRuleContext context = new BusinessRuleContext();

        bool whileOff = gated.IsBroken(context);
        provider.Set(Flag, RuleRolloutMode.Enforced);
        bool firstEnforced = gated.IsBroken(context);
        bool secondEnforced = gated.IsBroken(context);

        Assert.False(whileOff);
        Assert.True(firstEnforced);
        Assert.True(secondEnforced);
        Assert.Equal(1, evaluations);
    }

    [Fact]
    public void CacheOutsideGate_FreezesTheRolloutDecision()
    {
        InMemoryRuleFeatureFlagProvider provider = RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Off);
        IBusinessRule cachedGate = CreateRule(broken: true).GatedBy(Flag, provider).Cached(TimeSpan.FromMinutes(5));
        BusinessRuleContext context = new BusinessRuleContext();

        bool whileOff = cachedGate.IsBroken(context);
        provider.Set(Flag, RuleRolloutMode.Enforced);
        bool afterFlip = cachedGate.IsBroken(context);

        Assert.False(whileOff);
        Assert.False(afterFlip);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static IBusinessRule CreateRule(bool broken)
    {
        return CreateCountingRule(() => { }, broken);
    }

    private static IBusinessRule CreateCountingRule(Action onEvaluate, bool broken)
    {
        return BusinessRuleBuilder.Create(Code)
            .WithMessage(Message)
            .WithSeverity(RuleSeverity.Warning)
            .WithName(Name)
            .WithDescription(Description)
            .WithCategory(Category)
            .WithTags("a", "b")
            .When(() =>
            {
                onEvaluate();
                return broken;
            })
            .Build();
    }

    private static string KeyWithBucket(string flag, Func<int, bool> accept)
    {
        for (int index = 0; index < 1000; index++)
        {
            string key = $"customer-{index}";
            if (accept(RuleRolloutBucket.Compute(flag, key)))
            {
                return key;
            }
        }

        throw new InvalidOperationException("No stable key with the requested bucket was found.");
    }

    private sealed class RecordingProvider : IRuleFeatureFlagProvider
    {
        private readonly RuleRollout _rollout;

        public RecordingProvider(RuleRollout rollout)
        {
            _rollout = rollout;
        }

        public List<(string Flag, BusinessRuleContext Context)> Calls { get; } = new List<(string, BusinessRuleContext)>();

        public RuleRollout GetRollout(string flag, BusinessRuleContext context)
        {
            Calls.Add((flag, context));
            return _rollout;
        }
    }

    private sealed class ThrowingProvider : IRuleFeatureFlagProvider
    {
        public RuleRollout GetRollout(string flag, BusinessRuleContext context)
        {
            throw new InvalidOperationException("provider unavailable");
        }
    }
}
