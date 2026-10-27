using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

namespace Masterly.BusinessRules.FeatureManagement.UnitTests;

/// <summary>
/// Sync side of the adapter: <see cref="IRuleFeatureFlagProvider.GetRollout"/>. Mirrored by
/// <see cref="FeatureManagementRuleFeatureFlagProviderAsyncTests"/>.
/// </summary>
public class FeatureManagementRuleFeatureFlagProviderTests
{
    private const string Flag = "credit-score-v2";
    private const string Code = "CREDIT.SCORE";
    private const string CustomerIdKey = "customerId";
    private const string CustomerId = "cust-42";

    // ---------------------------------------------------------------------
    // Construction
    // ---------------------------------------------------------------------

    [Fact]
    public void Constructor_NullVariantFeatureManager_ThrowsArgumentNullException()
    {
        IVariantFeatureManager manager = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => new FeatureManagementRuleFeatureFlagProvider(manager));
    }

    [Fact]
    public void Constructor_NullFeatureManager_ThrowsArgumentNullException()
    {
        IFeatureManager manager = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => new FeatureManagementRuleFeatureFlagProvider(manager));
    }

    [Fact]
    public void Constructor_NullOptions_UsesDefaults()
    {
        FeatureManagementRuleFeatureFlagProvider provider = new((IVariantFeatureManager)new FakeFeatureManager());

        Assert.NotNull(provider.Options);
        Assert.Equal(RuleRollout.Enforced(), provider.Options.EnabledRollout);
        Assert.Equal(UnrecognizedVariantBehavior.Throw, provider.Options.OnUnrecognizedVariant);
    }

    [Fact]
    public void Constructor_WithOptions_ExposesSameInstance()
    {
        FeatureManagementRolloutOptions options = new();

        FeatureManagementRuleFeatureFlagProvider provider = new((IFeatureManager)new FakeFeatureManager(), options);

        Assert.Same(options, provider.Options);
    }

    [Fact]
    public void SupportsVariants_ReflectsManagerType()
    {
        FakeFeatureManager manager = new();

        Assert.True(new FeatureManagementRuleFeatureFlagProvider((IVariantFeatureManager)manager).SupportsVariants);
        Assert.False(new FeatureManagementRuleFeatureFlagProvider((IFeatureManager)manager).SupportsVariants);
    }

    [Fact]
    public void Provider_ImplementsBothProviderInterfaces()
    {
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager());

        Assert.IsAssignableFrom<IRuleFeatureFlagProvider>(provider);
        Assert.IsAssignableFrom<IAsyncRuleFeatureFlagProvider>(provider);
    }

    // ---------------------------------------------------------------------
    // Guards
    // ---------------------------------------------------------------------

    [Fact]
    public void GetRollout_NullFlag_ThrowsArgumentNullException()
    {
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager());

        Assert.Throws<ArgumentNullException>(() => provider.GetRollout(null!, new BusinessRuleContext()));
    }

    [Fact]
    public void GetRollout_NullContext_IsTolerated()
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag);
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(manager);

        RuleRollout rollout = provider.GetRollout(Flag, null!);

        Assert.Equal(RuleRollout.Enforced(), rollout);
    }

    // ---------------------------------------------------------------------
    // Enabled state (no variants)
    // ---------------------------------------------------------------------

    [Fact]
    public void GetRollout_UnknownFlag_IsOff()
    {
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager());

        RuleRollout rollout = provider.GetRollout(Flag, new BusinessRuleContext());

        Assert.Equal(RuleRollout.Off, rollout);
        Assert.True(rollout.IsOff);
    }

    [Fact]
    public void GetRollout_DisabledFlag_IsOff()
    {
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Disable(Flag));

        Assert.Equal(RuleRollout.Off, provider.GetRollout(Flag, new BusinessRuleContext()));
    }

    [Fact]
    public void GetRollout_EnabledFlagWithoutVariant_IsEnforced()
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag);
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(manager);

        RuleRollout rollout = provider.GetRollout(Flag, new BusinessRuleContext());

        Assert.Equal(RuleRollout.Enforced(), rollout);
        Assert.Equal(new[] { Flag }, manager.GetVariantCalls);
        Assert.Equal(new[] { Flag }, manager.IsEnabledCalls);
    }

    [Fact]
    public void GetRollout_EnabledFlag_UsesConfiguredEnabledRollout()
    {
        FeatureManagementRolloutOptions options = new() { EnabledRollout = RuleRollout.Shadow() };
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag), options);

        Assert.Equal(RuleRollout.Shadow(), provider.GetRollout(Flag, new BusinessRuleContext()));
    }

    [Fact]
    public void GetRollout_DisabledFlag_IgnoresEnabledRollout()
    {
        FeatureManagementRolloutOptions options = new() { EnabledRollout = RuleRollout.Shadow() };
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Disable(Flag), options);

        Assert.Equal(RuleRollout.Off, provider.GetRollout(Flag, new BusinessRuleContext()));
    }

    [Fact]
    public void GetRollout_PlainFeatureManager_IgnoresVariants()
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag).WithVariant(Flag, "Shadow");
        FeatureManagementRuleFeatureFlagProvider provider = new((IFeatureManager)manager);

        RuleRollout rollout = provider.GetRollout(Flag, new BusinessRuleContext());

        Assert.Equal(RuleRollout.Enforced(), rollout);
        Assert.Empty(manager.GetVariantCalls);
        Assert.Equal(new[] { Flag }, manager.IsEnabledCalls);
    }

    [Fact]
    public void GetRollout_PlainFeatureManagerDisabled_IsOff()
    {
        FeatureManagementRuleFeatureFlagProvider provider = new((IFeatureManager)new FakeFeatureManager().Disable(Flag));

        Assert.Equal(RuleRollout.Off, provider.GetRollout(Flag, new BusinessRuleContext()));
    }

    // ---------------------------------------------------------------------
    // Variant mapping
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("Shadow", RuleRolloutMode.Shadow, 100)]
    [InlineData("Enforced-10", RuleRolloutMode.Enforced, 10)]
    [InlineData("off", RuleRolloutMode.Off, 0)]
    public void GetRollout_VariantNamedByConvention_MapsToRollout(string variantName, RuleRolloutMode mode, int percent)
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag).WithVariant(Flag, variantName);
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(manager);

        RuleRollout rollout = provider.GetRollout(Flag, new BusinessRuleContext());

        Assert.Equal(new RuleRollout(mode, percent), rollout);
        Assert.Empty(manager.IsEnabledCalls);
    }

    [Fact]
    public void GetRollout_VariantWithConfiguration_ReadsModeAndPercent()
    {
        Variant variant = Variants.WithConfiguration("rollout", (RolloutVariantConvention.ModeKey, "Shadow"), (RolloutVariantConvention.PercentKey, "10"));
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag).WithVariant(Flag, variant));

        Assert.Equal(RuleRollout.Shadow(10), provider.GetRollout(Flag, new BusinessRuleContext()));
    }

    [Fact]
    public void GetRollout_VariantWithScalarConfiguration_ParsesValue()
    {
        Variant variant = Variants.WithScalarConfiguration("rollout", "Enforced-25");
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag).WithVariant(Flag, variant));

        Assert.Equal(RuleRollout.Enforced(25), provider.GetRollout(Flag, new BusinessRuleContext()));
    }

    [Fact]
    public void GetRollout_ConfigurationWinsOverName()
    {
        Variant variant = Variants.WithConfiguration("Enforced", (RolloutVariantConvention.ModeKey, "Shadow"));
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag).WithVariant(Flag, variant));

        Assert.Equal(RuleRollout.Shadow(), provider.GetRollout(Flag, new BusinessRuleContext()));
    }

    [Fact]
    public void GetRollout_ExplicitMappingWinsOverConfigurationAndName()
    {
        Variant variant = Variants.WithConfiguration("Enforced", (RolloutVariantConvention.ModeKey, "Shadow"));
        FeatureManagementRolloutOptions options = new FeatureManagementRolloutOptions().MapVariant("enforced", RuleRollout.Enforced(50));
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag).WithVariant(Flag, variant), options);

        Assert.Equal(RuleRollout.Enforced(50), provider.GetRollout(Flag, new BusinessRuleContext()));
    }

    [Fact]
    public void GetRollout_ExplicitMapping_MapsArbitraryName()
    {
        FeatureManagementRolloutOptions options = new FeatureManagementRolloutOptions().MapVariant("dark", RuleRollout.Shadow());
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag).WithVariant(Flag, "dark"), options);

        Assert.Equal(RuleRollout.Shadow(), provider.GetRollout(Flag, new BusinessRuleContext()));
    }

    [Fact]
    public void GetRollout_VariantAssignedWhileDisabled_VariantDecides()
    {
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Disable(Flag).WithVariant(Flag, "Shadow"));

        Assert.Equal(RuleRollout.Shadow(), provider.GetRollout(Flag, new BusinessRuleContext()));
    }

    // ---------------------------------------------------------------------
    // Unrecognised variants
    // ---------------------------------------------------------------------

    [Fact]
    public void GetRollout_UnrecognizedVariant_DefaultThrows()
    {
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag).WithVariant(Flag, "Big"));

        RolloutVariantFormatException exception = Assert.Throws<RolloutVariantFormatException>(() => provider.GetRollout(Flag, new BusinessRuleContext()));

        Assert.Equal(Flag, exception.Flag);
        Assert.Equal("Big", exception.Variant);
    }

    [Fact]
    public void GetRollout_UnnamedVariant_ThrowsWithNullVariant()
    {
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag).WithVariant(Flag, new Variant()));

        RolloutVariantFormatException exception = Assert.Throws<RolloutVariantFormatException>(() => provider.GetRollout(Flag, new BusinessRuleContext()));

        Assert.Null(exception.Variant);
    }

    [Fact]
    public void GetRollout_UnrecognizedVariant_OffBehavior_IsOffWithoutEnabledCheck()
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag).WithVariant(Flag, "Big");
        FeatureManagementRolloutOptions options = new() { OnUnrecognizedVariant = UnrecognizedVariantBehavior.Off };
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(manager, options);

        RuleRollout rollout = provider.GetRollout(Flag, new BusinessRuleContext());

        Assert.Equal(RuleRollout.Off, rollout);
        Assert.Empty(manager.IsEnabledCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetRollout_UnrecognizedVariant_UseEnabledState_FallsBackToEnabledState(bool enabled)
    {
        FakeFeatureManager manager = enabled ? new FakeFeatureManager().Enable(Flag) : new FakeFeatureManager().Disable(Flag);
        manager.WithVariant(Flag, "Big");
        FeatureManagementRolloutOptions options = new() { OnUnrecognizedVariant = UnrecognizedVariantBehavior.UseEnabledState };
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(manager, options);

        RuleRollout rollout = provider.GetRollout(Flag, new BusinessRuleContext());

        Assert.Equal(enabled ? RuleRollout.Enforced() : RuleRollout.Off, rollout);
        Assert.Equal(new[] { Flag }, manager.IsEnabledCalls);
    }

    // ---------------------------------------------------------------------
    // Targeting
    // ---------------------------------------------------------------------

    [Fact]
    public void GetRollout_WithoutSelector_PassesNoTargetingContext()
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag);
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(manager);

        provider.GetRollout(Flag, RuleTestHelper.CreateContext((CustomerIdKey, CustomerId)));

        Assert.Null(manager.LastTargetingContext);
        Assert.Null(manager.LastEnabledContext);
    }

    [Fact]
    public void GetRollout_WithSelector_PassesTargetingContextToVariantAndEnabledChecks()
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag);
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(manager, CreateTargetingOptions());

        provider.GetRollout(Flag, RuleTestHelper.CreateContext((CustomerIdKey, CustomerId)));

        Assert.NotNull(manager.LastTargetingContext);
        Assert.Equal(CustomerId, manager.LastTargetingContext!.UserId);
        Assert.Same(manager.LastTargetingContext, manager.LastEnabledContext);
    }

    [Fact]
    public void GetRollout_WithSelector_PassesTargetingContextToVariantLookup()
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag).WithVariant(Flag, "Shadow");
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(manager, CreateTargetingOptions());

        provider.GetRollout(Flag, RuleTestHelper.CreateContext((CustomerIdKey, CustomerId)));

        Assert.Equal(CustomerId, manager.LastTargetingContext?.UserId);
    }

    [Fact]
    public void GetRollout_WithSelector_PlainFeatureManagerUsesContextualOverload()
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag);
        FeatureManagementRuleFeatureFlagProvider provider = new((IFeatureManager)manager, CreateTargetingOptions());

        provider.GetRollout(Flag, RuleTestHelper.CreateContext((CustomerIdKey, CustomerId)));

        Assert.Equal(CustomerId, manager.LastTargetingContext?.UserId);
    }

    [Fact]
    public void GetRollout_SelectorReturnsNull_PassesNoTargetingContext()
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag);
        FeatureManagementRolloutOptions options = new() { TargetingContextSelector = ctx => null };
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(manager, options);

        provider.GetRollout(Flag, new BusinessRuleContext());

        Assert.Null(manager.LastTargetingContext);
    }

    [Fact]
    public void GetRollout_NullContext_DoesNotInvokeSelector()
    {
        int invocations = 0;
        FeatureManagementRolloutOptions options = new()
        {
            TargetingContextSelector = ctx =>
            {
                invocations++;
                return null;
            }
        };
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag), options);

        provider.GetRollout(Flag, null!);

        Assert.Equal(0, invocations);
    }

    [Fact]
    public void GetRollout_SelectorThrows_Propagates()
    {
        FeatureManagementRolloutOptions options = new() { TargetingContextSelector = ctx => throw new InvalidOperationException("boom") };
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag), options);

        Assert.Throws<InvalidOperationException>(() => provider.GetRollout(Flag, new BusinessRuleContext()));
    }

    // ---------------------------------------------------------------------
    // Flipping at runtime
    // ---------------------------------------------------------------------

    [Fact]
    public void GetRollout_ReadsManagerOnEveryCall()
    {
        FakeFeatureManager manager = new FakeFeatureManager().Disable(Flag);
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(manager);
        BusinessRuleContext context = new();

        RuleRollout before = provider.GetRollout(Flag, context);
        manager.Enable(Flag).WithVariant(Flag, "Shadow");
        RuleRollout after = provider.GetRollout(Flag, context);

        Assert.Equal(RuleRollout.Off, before);
        Assert.Equal(RuleRollout.Shadow(), after);
    }

    // ---------------------------------------------------------------------
    // End to end with FeatureGatedBusinessRule and BusinessRuleChecker
    // ---------------------------------------------------------------------

    [Fact]
    public void GatedRule_FlagOff_PassesWithoutEvaluatingInner()
    {
        int evaluations = 0;
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Disable(Flag));
        IBusinessRule gated = CreateCountingRule(() => evaluations++).GatedBy(Flag, provider);

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { gated }, new BusinessRuleContext());

        Assert.True(result.AllPassed);
        Assert.False(result.HasShadowResults);
        Assert.Equal(0, evaluations);
    }

    [Fact]
    public void GatedRule_ShadowVariant_PassesAndReportsShadowResult()
    {
        int evaluations = 0;
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag).WithVariant(Flag, "Shadow"));
        IBusinessRule gated = CreateCountingRule(() => evaluations++).GatedBy(Flag, provider);

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { gated }, new BusinessRuleContext());

        Assert.True(result.AllPassed);
        Assert.True(result.HasShadowResults);
        Assert.Equal(Code, Assert.Single(result.ShadowResults).Code);
        Assert.Equal(1, evaluations);
    }

    [Fact]
    public void GatedRule_EnabledFlag_EnforcesInnerVerdict()
    {
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag));
        IBusinessRule gated = CreateCountingRule(() => { }).GatedBy(Flag, provider);

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { gated }, new BusinessRuleContext());

        Assert.False(result.AllPassed);
        Assert.Equal(Code, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public void GatedRule_PartialVariant_BucketsByStableKey()
    {
        int bucket = RuleRolloutBucket.Compute(Flag, CustomerId);
        BusinessRuleContext context = RuleTestHelper.CreateContext((CustomerIdKey, CustomerId));
        FeatureManagementRuleFeatureFlagProvider inside = CreateVariantProvider(new FakeFeatureManager().Enable(Flag).WithVariant(Flag, $"Enforced-{bucket + 1}"));
        FeatureManagementRuleFeatureFlagProvider outside = CreateVariantProvider(new FakeFeatureManager().Enable(Flag).WithVariant(Flag, $"Enforced-{bucket}"));

        bool brokenInside = CreateCountingRule(() => { }).GatedBy(Flag, inside, ctx => ctx.Get<string>(CustomerIdKey)).IsBroken(context);
        bool brokenOutside = CreateCountingRule(() => { }).GatedBy(Flag, outside, ctx => ctx.Get<string>(CustomerIdKey)).IsBroken(context);

        Assert.True(brokenInside);
        Assert.False(brokenOutside);
    }

    [Fact]
    public void GatedRule_UnrecognizedVariant_PropagatesFormatException()
    {
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag).WithVariant(Flag, "Big"));
        IBusinessRule gated = CreateCountingRule(() => { }).GatedBy(Flag, provider);

        Assert.Throws<RolloutVariantFormatException>(() => gated.IsBroken(new BusinessRuleContext()));
    }

    [Fact]
    public void GatedRule_ProviderOnContext_OverridesInjected()
    {
        FeatureManagementRuleFeatureFlagProvider injected = CreateVariantProvider(new FakeFeatureManager().Disable(Flag));
        FeatureManagementRuleFeatureFlagProvider fromContext = CreateVariantProvider(new FakeFeatureManager().Enable(Flag));
        BusinessRuleContext context = new();
        context.Set(RuleFeatureFlags.ContextKey, fromContext);

        bool broken = CreateCountingRule(() => { }).GatedBy(Flag, injected).IsBroken(context);

        Assert.True(broken);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static FeatureManagementRuleFeatureFlagProvider CreateVariantProvider(FakeFeatureManager manager, FeatureManagementRolloutOptions? options = null)
    {
        return new FeatureManagementRuleFeatureFlagProvider((IVariantFeatureManager)manager, options);
    }

    private static FeatureManagementRolloutOptions CreateTargetingOptions()
    {
        return new FeatureManagementRolloutOptions
        {
            TargetingContextSelector = ctx => new TargetingContext { UserId = ctx.Get<string>(CustomerIdKey) }
        };
    }

    private static IBusinessRule CreateCountingRule(Action onEvaluate)
    {
        return BusinessRuleBuilder.Create(Code)
            .WithMessage("Credit score is too low")
            .When(() =>
            {
                onEvaluate();
                return true;
            })
            .Build();
    }
}
