namespace Masterly.BusinessRules.UnitTests;

public class FeatureGatedAsyncRuleTests
{
    private const string Code = "SANCTIONS.CHECK";
    private const string Message = "Customer is on a sanctions list";
    private const string Name = "SanctionsCheck";
    private const string Description = "Checks the customer against the sanctions API";
    private const string Category = "Compliance";
    private const string Flag = "sanctions-check";
    private const string CustomerIdKey = "customerId";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------
    // Construction and metadata
    // ---------------------------------------------------------------------

    [Fact]
    public void GatedBy_WithAsyncProvider_ReturnsFeatureGatedAsyncBusinessRule()
    {
        IAsyncBusinessRule inner = CreateRule(broken: true);
        IAsyncRuleFeatureFlagProvider provider = new AsyncOnlyProvider(RuleRollout.Enforced());

        FeatureGatedAsyncBusinessRule gated = inner.GatedBy(Flag, provider);

        Assert.Same(inner, gated.InnerRule);
        Assert.Equal(Flag, gated.Flag);
        Assert.Same(provider, gated.AsyncProvider);
        Assert.Null(gated.Provider);
        Assert.False(gated.HasStableKey);
        Assert.IsAssignableFrom<IDecoratedAsyncBusinessRule>(gated);
    }

    [Fact]
    public void GatedBy_WithSyncOnlyProvider_StoresItAsSyncProvider()
    {
        IRuleFeatureFlagProvider provider = new SyncOnlyProvider(RuleRollout.Enforced());

        FeatureGatedAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, provider);

        Assert.Null(gated.AsyncProvider);
        Assert.Same(provider, gated.Provider);
    }

    [Fact]
    public void GatedBy_WithInMemoryProvider_UsesItAsBothProviders()
    {
        InMemoryRuleFeatureFlagProvider provider = RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced);

        FeatureGatedAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, provider, stableKey: ctx => ctx.Get<string>(CustomerIdKey));

        Assert.Same(provider, gated.AsyncProvider);
        Assert.Same(provider, gated.Provider);
        Assert.True(gated.HasStableKey);
    }

    [Fact]
    public void GatedBy_WithoutProvider_LeavesBothProvidersNull()
    {
        FeatureGatedAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, stableKey: ctx => ctx.Get<string>(CustomerIdKey));

        Assert.Null(gated.AsyncProvider);
        Assert.Null(gated.Provider);
        Assert.True(gated.HasStableKey);
    }

    [Fact]
    public void GatedBy_ForwardsEveryMetadataMember()
    {
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

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
        IAsyncBusinessRule rule = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => rule.GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced)));
        Assert.Throws<ArgumentNullException>(() => rule.GatedBy(Flag));
    }

    [Fact]
    public void GatedBy_NullProviders_ThrowArgumentNullException()
    {
        IAsyncRuleFeatureFlagProvider asyncProvider = null!; // intentional: exercising the null guard
        IRuleFeatureFlagProvider syncProvider = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => CreateRule(broken: true).GatedBy(Flag, asyncProvider));
        Assert.Throws<ArgumentNullException>(() => CreateRule(broken: true).GatedBy(Flag, syncProvider));
    }

    [Fact]
    public void Constructor_NullFlag_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new FeatureGatedAsyncBusinessRule(CreateRule(broken: true), null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_BlankFlag_ThrowsArgumentException(string flag)
    {
        Assert.Throws<ArgumentException>(() => new FeatureGatedAsyncBusinessRule(CreateRule(broken: true), flag));
    }

    [Fact]
    public void Constructor_NullInnerRule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new FeatureGatedAsyncBusinessRule(null!, Flag));
    }

    // ---------------------------------------------------------------------
    // Modes
    // ---------------------------------------------------------------------

    [Fact]
    public async Task IsBrokenAsync_FlagOff_PassesWithoutEvaluatingInner()
    {
        int evaluations = 0;
        IAsyncBusinessRule gated = CreateCountingRule(() => evaluations++, broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Off));

        bool broken = await gated.IsBrokenAsync(new BusinessRuleContext(), TestToken);

        Assert.False(broken);
        Assert.Equal(0, evaluations);
    }

    [Fact]
    public async Task IsBrokenAsync_UnknownFlag_IsOff()
    {
        int evaluations = 0;
        IAsyncBusinessRule gated = CreateCountingRule(() => evaluations++, broken: true).GatedBy("other-flag", RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        Assert.False(await gated.IsBrokenAsync(new BusinessRuleContext(), TestToken));
        Assert.Equal(0, evaluations);
    }

    [Fact]
    public async Task IsBrokenAsync_ForwardsContextToInnerRule()
    {
        IAsyncBusinessRule contextRule = AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WhenAsync(ctx => Task.FromResult(ctx.Get<bool>("flag"))).Build();
        IAsyncBusinessRule gated = contextRule.GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        Assert.True(await gated.IsBrokenAsync(RuleTestHelper.CreateContext(("flag", true)), TestToken));
        Assert.False(await gated.IsBrokenAsync(RuleTestHelper.CreateContext(("flag", false)), TestToken));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IsBrokenAsync_Enforced_ForwardsVerdict(bool broken)
    {
        IAsyncBusinessRule gated = CreateRule(broken).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        Assert.Equal(broken, await gated.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_Shadow_EvaluatesInnerButPasses()
    {
        int evaluations = 0;
        IAsyncBusinessRule gated = CreateCountingRule(() => evaluations++, broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));

        bool broken = await gated.IsBrokenAsync(new BusinessRuleContext(), TestToken);

        Assert.False(broken);
        Assert.Equal(1, evaluations);
    }

    [Fact]
    public async Task EvaluateAsync_Enforced_ReturnsInnerResult()
    {
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        BusinessRuleResult? result = await gated.EvaluateAsync(new BusinessRuleContext(), TestToken);

        Assert.NotNull(result);
        Assert.Equal(Code, result!.Code);
        Assert.Equal(Message, result.Message);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
    }

    [Fact]
    public async Task EvaluateAsync_Shadow_ReturnsNull()
    {
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));

        Assert.Null(await gated.EvaluateAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task EvaluateAsync_Off_ReturnsNull()
    {
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Off));

        Assert.Null(await gated.EvaluateAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task CheckAsync_Enforced_ThrowsWithInnerResult()
    {
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(() => gated.CheckAsync(new BusinessRuleContext(), TestToken));

        Assert.Equal(Code, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckAsync_Shadow_DoesNotThrow()
    {
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));

        await gated.CheckAsync(new BusinessRuleContext(), TestToken);
    }

    [Fact]
    public async Task EvaluateAsync_OnComposedInnerRule_KeepsMemberDetails()
    {
        IAsyncBusinessRule group = AsyncRuleGroup.All("GROUP", "group", RuleTestHelper.CreateBrokenAsyncRule("M1"), RuleTestHelper.CreatePassingAsyncRule("M2"));
        IAsyncBusinessRule gated = group.GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        BusinessRuleResult? result = await gated.EvaluateAsync(new BusinessRuleContext(), TestToken);

        Assert.NotNull(result);
        Assert.Equal("M1", Assert.Single(result!.Details).Code);
    }

    [Fact]
    public async Task IsBrokenAsync_NullContext_ThrowsArgumentNullException()
    {
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        await Assert.ThrowsAsync<ArgumentNullException>(() => gated.IsBrokenAsync(null!, TestToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => gated.EvaluateAsync(null!, TestToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => gated.CheckAsync(null!, TestToken));
    }

    // ---------------------------------------------------------------------
    // Provider resolution
    // ---------------------------------------------------------------------

    [Fact]
    public async Task IsBrokenAsync_NoProviderAnywhere_ThrowsRuleConfigurationException()
    {
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag);

        RuleConfigurationException exception = await Assert.ThrowsAsync<RuleConfigurationException>(() => gated.IsBrokenAsync(new BusinessRuleContext(), TestToken));

        Assert.Equal(Code, exception.RuleCode);
        Assert.Contains(Flag, exception.Message);
    }

    [Fact]
    public async Task IsBrokenAsync_AsyncProviderThrows_Propagates()
    {
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, new ThrowingAsyncProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(() => gated.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_SyncProviderThrows_Propagates()
    {
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, new ThrowingSyncProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(() => gated.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_AsyncProviderIsPreferredOverSyncProvider()
    {
        RecordingAsyncProvider asyncProvider = new RecordingAsyncProvider(RuleRollout.Enforced());
        RecordingSyncProvider syncProvider = new RecordingSyncProvider(RuleRollout.Off);
        IAsyncBusinessRule gated = new FeatureGatedAsyncBusinessRule(CreateRule(broken: true), Flag, asyncProvider, syncProvider);

        bool broken = await gated.IsBrokenAsync(new BusinessRuleContext(), TestToken);

        Assert.True(broken);
        Assert.Single(asyncProvider.Calls);
        Assert.Empty(syncProvider.Calls);
    }

    [Fact]
    public async Task IsBrokenAsync_SyncProviderIsUsedWhenNoAsyncProvider()
    {
        RecordingSyncProvider syncProvider = new RecordingSyncProvider(RuleRollout.Enforced());
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, syncProvider);

        Assert.True(await gated.IsBrokenAsync(new BusinessRuleContext(), TestToken));
        Assert.Single(syncProvider.Calls);
    }

    [Fact]
    public async Task IsBrokenAsync_AsyncContextKeyOverridesEverything()
    {
        RecordingAsyncProvider injected = new RecordingAsyncProvider(RuleRollout.Off);
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, injected);
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(RuleFeatureFlags.AsyncContextKey, new AsyncOnlyProvider(RuleRollout.Enforced()));
        context.Set(RuleFeatureFlags.ContextKey, new SyncOnlyProvider(RuleRollout.Off));

        Assert.True(await gated.IsBrokenAsync(context, TestToken));
        Assert.Empty(injected.Calls);
    }

    [Fact]
    public async Task IsBrokenAsync_SyncContextKeyOverridesInjectedProviders()
    {
        RecordingAsyncProvider injected = new RecordingAsyncProvider(RuleRollout.Off);
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, injected);
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(RuleFeatureFlags.ContextKey, new SyncOnlyProvider(RuleRollout.Enforced()));

        Assert.True(await gated.IsBrokenAsync(context, TestToken));
        Assert.Empty(injected.Calls);
    }

    [Fact]
    public async Task IsBrokenAsync_PassesFlagContextAndTokenToAsyncProvider()
    {
        RecordingAsyncProvider provider = new RecordingAsyncProvider(RuleRollout.Enforced());
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, provider);
        BusinessRuleContext context = new BusinessRuleContext();
        using CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(TestToken);

        await gated.IsBrokenAsync(context, source.Token);

        (string flag, BusinessRuleContext seen, CancellationToken token) = Assert.Single(provider.Calls);
        Assert.Equal(Flag, flag);
        Assert.Same(context, seen);
        Assert.Equal(source.Token, token);
    }

    [Fact]
    public async Task IsBrokenAsync_ProviderIsConsultedOnEveryEvaluation_SoRampingIsLive()
    {
        int evaluations = 0;
        InMemoryRuleFeatureFlagProvider provider = RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Off);
        IAsyncBusinessRule gated = CreateCountingRule(() => evaluations++, broken: true).GatedBy(Flag, provider);
        BusinessRuleContext context = new BusinessRuleContext();

        bool whileOff = await gated.IsBrokenAsync(context, TestToken);
        provider.Set(Flag, RuleRolloutMode.Shadow);
        bool whileShadow = await gated.IsBrokenAsync(context, TestToken);
        provider.Set(Flag, RuleRolloutMode.Enforced);
        bool whileEnforced = await gated.IsBrokenAsync(context, TestToken);

        Assert.False(whileOff);
        Assert.False(whileShadow);
        Assert.True(whileEnforced);
        Assert.Equal(2, evaluations);
    }

    [Fact]
    public async Task IsBrokenAsync_CancelledToken_PropagatesFromProvider()
    {
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gated.IsBrokenAsync(new BusinessRuleContext(), source.Token));
    }

    [Fact]
    public async Task IsBrokenAsync_CancelledToken_PropagatesFromInnerRule()
    {
        IAsyncBusinessRule cancelAware = AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message)
            .WhenAsync((ctx, ct) => { ct.ThrowIfCancellationRequested(); return Task.FromResult(true); }).Build();
        IAsyncBusinessRule gated = cancelAware.GatedBy(Flag, new AsyncOnlyProvider(RuleRollout.Enforced()));
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gated.IsBrokenAsync(new BusinessRuleContext(), source.Token));
    }

    // ---------------------------------------------------------------------
    // Percentage rollout
    // ---------------------------------------------------------------------

    [Fact]
    public async Task IsBrokenAsync_PartialRollout_InsideCohort_Evaluates()
    {
        string insideKey = KeyWithBucket(Flag, bucket => bucket < 10);
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced, percent: 10), ctx => ctx.Get<string>(CustomerIdKey));

        Assert.True(await gated.IsBrokenAsync(RuleTestHelper.CreateContext((CustomerIdKey, insideKey)), TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_PartialRollout_OutsideCohort_PassesWithoutEvaluating()
    {
        int evaluations = 0;
        string outsideKey = KeyWithBucket(Flag, bucket => bucket >= 10);
        IAsyncBusinessRule gated = CreateCountingRule(() => evaluations++, broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced, percent: 10), ctx => ctx.Get<string>(CustomerIdKey));

        Assert.False(await gated.IsBrokenAsync(RuleTestHelper.CreateContext((CustomerIdKey, outsideKey)), TestToken));
        Assert.Equal(0, evaluations);
    }

    [Fact]
    public async Task IsBrokenAsync_PartialRollout_IsStableForTheSameKey()
    {
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced, percent: 50), ctx => ctx.Get<string>(CustomerIdKey));
        BusinessRuleContext context = RuleTestHelper.CreateContext((CustomerIdKey, "customer-7"));

        bool first = await gated.IsBrokenAsync(context, TestToken);
        bool second = await gated.IsBrokenAsync(context, TestToken);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task IsBrokenAsync_ZeroPercent_NeverCallsStableKeyAndPasses()
    {
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced, percent: 0), ctx => throw new InvalidOperationException("stable key must not be called"));

        Assert.False(await gated.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_PartialRollout_WithoutStableKey_ThrowsRuleConfigurationException()
    {
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow, percent: 10));

        RuleConfigurationException exception = await Assert.ThrowsAsync<RuleConfigurationException>(() => gated.IsBrokenAsync(new BusinessRuleContext(), TestToken));

        Assert.Equal(Code, exception.RuleCode);
    }

    [Fact]
    public async Task IsBrokenAsync_PartialRollout_StableKeyReturnsNull_ThrowsRuleConfigurationException()
    {
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced, percent: 10), ctx => null!);

        await Assert.ThrowsAsync<RuleConfigurationException>(() => gated.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_FullRollout_NeverCallsStableKey()
    {
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced), ctx => throw new InvalidOperationException("stable key must not be called"));

        Assert.True(await gated.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    // ---------------------------------------------------------------------
    // Explain
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExplainAsync_FlagOff_ReportsSkippedGateWithReason()
    {
        IAsyncBusinessRule inner = CreateRule(broken: true);
        IAsyncBusinessRule gated = inner.GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Off));

        RuleExplanation explanation = await gated.ExplainAsync(new BusinessRuleContext(), TestToken);

        Assert.Same(gated, explanation.AsyncRule);
        Assert.Equal("GATED", explanation.Operator);
        Assert.Equal(RuleOutcome.Skipped, explanation.Outcome);
        Assert.Equal($"flag '{Flag}' is off", explanation.Reason);
        RuleExplanation child = Assert.Single(explanation.Children);
        Assert.Same(inner, child.AsyncRule);
        Assert.Equal(RuleOutcome.NotEvaluated, child.Outcome);
    }

    [Fact]
    public async Task ExplainAsync_Enforced_ReportsGateNodeAboveInnerRule()
    {
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        RuleExplanation explanation = await gated.ExplainAsync(new BusinessRuleContext(), TestToken);

        Assert.Equal("GATED", explanation.Operator);
        Assert.Equal(RuleOutcome.Broken, explanation.Outcome);
        Assert.Equal(RuleOutcome.Broken, Assert.Single(explanation.Children).Outcome);
    }

    [Fact]
    public async Task ExplainAsync_ShadowViolation_ReportsPassedShadowNodeWithBrokenChild()
    {
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));

        RuleExplanation explanation = await gated.ExplainAsync(new BusinessRuleContext(), TestToken);

        Assert.Equal("SHADOW", explanation.Operator);
        Assert.Equal(RuleOutcome.Passed, explanation.Outcome);
        Assert.False(explanation.IsBroken);
        Assert.Equal("shadow violation", explanation.Reason);
        Assert.Equal(RuleOutcome.Broken, Assert.Single(explanation.Children).Outcome);
    }

    [Fact]
    public async Task ExplainAsync_OutsideCohort_ReportsSkippedGateWithBucketReason()
    {
        string outsideKey = KeyWithBucket(Flag, bucket => bucket >= 10);
        IAsyncBusinessRule gated = CreateRule(broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced, percent: 10), ctx => ctx.Get<string>(CustomerIdKey));

        RuleExplanation explanation = await gated.ExplainAsync(RuleTestHelper.CreateContext((CustomerIdKey, outsideKey)), TestToken);

        Assert.Equal(RuleOutcome.Skipped, explanation.Outcome);
        Assert.Equal($"flag '{Flag}' excludes bucket {RuleRolloutBucket.Compute(Flag, outsideKey)} (10%)", explanation.Reason);
    }

    [Fact]
    public async Task ExplainAsync_EvaluatesInnerRuleExactlyOnce()
    {
        int evaluations = 0;
        IAsyncBusinessRule gated = CreateCountingRule(() => evaluations++, broken: true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        await gated.ExplainAsync(new BusinessRuleContext(), TestToken);

        Assert.Equal(1, evaluations);
    }

    [Fact]
    public async Task ExplainAsync_ShadowPass_ReportsPassedShadowNodeWithoutReason()
    {
        IAsyncBusinessRule gated = CreateRule(broken: false).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));

        RuleExplanation explanation = await gated.ExplainAsync(new BusinessRuleContext(), TestToken);

        Assert.Equal("SHADOW", explanation.Operator);
        Assert.Equal(RuleOutcome.Passed, explanation.Outcome);
        Assert.Equal(string.Empty, explanation.Reason);
    }

    // ---------------------------------------------------------------------
    // Interaction with CachedAsyncBusinessRule
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CacheInsideGate_GateIsDecidedOnEveryCallAndCacheServesTheInnerRule()
    {
        int evaluations = 0;
        InMemoryRuleFeatureFlagProvider provider = RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Off);
        IAsyncBusinessRule gated = CreateCountingRule(() => evaluations++, broken: true).Cached(TimeSpan.FromMinutes(5)).GatedBy(Flag, provider);
        BusinessRuleContext context = new BusinessRuleContext();

        bool whileOff = await gated.IsBrokenAsync(context, TestToken);
        provider.Set(Flag, RuleRolloutMode.Enforced);
        bool firstEnforced = await gated.IsBrokenAsync(context, TestToken);
        bool secondEnforced = await gated.IsBrokenAsync(context, TestToken);

        Assert.False(whileOff);
        Assert.True(firstEnforced);
        Assert.True(secondEnforced);
        Assert.Equal(1, evaluations);
    }

    [Fact]
    public async Task CacheOutsideGate_FreezesTheRolloutDecision()
    {
        InMemoryRuleFeatureFlagProvider provider = RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Off);
        IAsyncBusinessRule cachedGate = CreateRule(broken: true).GatedBy(Flag, provider).Cached(TimeSpan.FromMinutes(5));
        BusinessRuleContext context = new BusinessRuleContext();

        bool whileOff = await cachedGate.IsBrokenAsync(context, TestToken);
        provider.Set(Flag, RuleRolloutMode.Enforced);
        bool afterFlip = await cachedGate.IsBrokenAsync(context, TestToken);

        Assert.False(whileOff);
        Assert.False(afterFlip);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static IAsyncBusinessRule CreateRule(bool broken)
    {
        return CreateCountingRule(() => { }, broken);
    }

    private static IAsyncBusinessRule CreateCountingRule(Action onEvaluate, bool broken)
    {
        return AsyncBusinessRuleBuilder.Create(Code)
            .WithMessage(Message)
            .WithSeverity(RuleSeverity.Warning)
            .WithName(Name)
            .WithDescription(Description)
            .WithCategory(Category)
            .WithTags("a", "b")
            .WhenAsync(ctx =>
            {
                onEvaluate();
                return Task.FromResult(broken);
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

    private sealed class ThrowingAsyncProvider : IAsyncRuleFeatureFlagProvider
    {
        public Task<RuleRollout> GetRolloutAsync(string flag, BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("provider unavailable");
        }
    }

    private sealed class ThrowingSyncProvider : IRuleFeatureFlagProvider
    {
        public RuleRollout GetRollout(string flag, BusinessRuleContext context)
        {
            throw new InvalidOperationException("provider unavailable");
        }
    }

    private sealed class AsyncOnlyProvider : IAsyncRuleFeatureFlagProvider
    {
        private readonly RuleRollout _rollout;

        public AsyncOnlyProvider(RuleRollout rollout)
        {
            _rollout = rollout;
        }

        public Task<RuleRollout> GetRolloutAsync(string flag, BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_rollout);
        }
    }

    private sealed class SyncOnlyProvider : IRuleFeatureFlagProvider
    {
        private readonly RuleRollout _rollout;

        public SyncOnlyProvider(RuleRollout rollout)
        {
            _rollout = rollout;
        }

        public RuleRollout GetRollout(string flag, BusinessRuleContext context)
        {
            return _rollout;
        }
    }

    private sealed class RecordingAsyncProvider : IAsyncRuleFeatureFlagProvider
    {
        private readonly RuleRollout _rollout;

        public RecordingAsyncProvider(RuleRollout rollout)
        {
            _rollout = rollout;
        }

        public List<(string Flag, BusinessRuleContext Context, CancellationToken Token)> Calls { get; } = new List<(string, BusinessRuleContext, CancellationToken)>();

        public Task<RuleRollout> GetRolloutAsync(string flag, BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            Calls.Add((flag, context, cancellationToken));
            return Task.FromResult(_rollout);
        }
    }

    private sealed class RecordingSyncProvider : IRuleFeatureFlagProvider
    {
        private readonly RuleRollout _rollout;

        public RecordingSyncProvider(RuleRollout rollout)
        {
            _rollout = rollout;
        }

        public List<string> Calls { get; } = new List<string>();

        public RuleRollout GetRollout(string flag, BusinessRuleContext context)
        {
            Calls.Add(flag);
            return _rollout;
        }
    }
}
