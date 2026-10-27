using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

namespace Masterly.BusinessRules.FeatureManagement.UnitTests;

/// <summary>
/// Async side of the adapter: <see cref="IAsyncRuleFeatureFlagProvider.GetRolloutAsync"/>. Mirrors
/// <see cref="FeatureManagementRuleFeatureFlagProviderTests"/> and adds the cancellation cases.
/// </summary>
public class FeatureManagementRuleFeatureFlagProviderAsyncTests
{
    private const string Flag = "sanctions-check";
    private const string Code = "SANCTIONS.HIT";
    private const string CustomerIdKey = "customerId";
    private const string CustomerId = "cust-42";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------
    // Guards and cancellation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetRolloutAsync_NullFlag_ThrowsArgumentNullException()
    {
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager());

        await Assert.ThrowsAsync<ArgumentNullException>(() => provider.GetRolloutAsync(null!, new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task GetRolloutAsync_NullContext_IsTolerated()
    {
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag));

        RuleRollout rollout = await provider.GetRolloutAsync(Flag, null!, TestToken);

        Assert.Equal(RuleRollout.Enforced(), rollout);
    }

    [Fact]
    public async Task GetRolloutAsync_CancelledToken_ThrowsBeforeCallingManager()
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag);
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(manager);
        using CancellationTokenSource source = new();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetRolloutAsync(Flag, new BusinessRuleContext(), source.Token));

        Assert.Empty(manager.GetVariantCalls);
        Assert.Empty(manager.IsEnabledCalls);
    }

    [Fact]
    public async Task GetRolloutAsync_ForwardsTokenToVariantManager()
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag);
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(manager);
        using CancellationTokenSource source = new();

        await provider.GetRolloutAsync(Flag, new BusinessRuleContext(), source.Token);

        Assert.Equal(source.Token, manager.LastCancellationToken);
    }

    [Fact]
    public async Task GetRolloutAsync_PlainFeatureManager_CancelledToken_Throws()
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag);
        FeatureManagementRuleFeatureFlagProvider provider = new((IFeatureManager)manager);
        using CancellationTokenSource source = new();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetRolloutAsync(Flag, new BusinessRuleContext(), source.Token));

        Assert.Empty(manager.IsEnabledCalls);
    }

    // ---------------------------------------------------------------------
    // Enabled state (no variants)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetRolloutAsync_UnknownFlag_IsOff()
    {
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager());

        Assert.Equal(RuleRollout.Off, await provider.GetRolloutAsync(Flag, new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task GetRolloutAsync_DisabledFlag_IsOff()
    {
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Disable(Flag));

        Assert.Equal(RuleRollout.Off, await provider.GetRolloutAsync(Flag, new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task GetRolloutAsync_EnabledFlagWithoutVariant_IsEnforced()
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag);
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(manager);

        RuleRollout rollout = await provider.GetRolloutAsync(Flag, new BusinessRuleContext(), TestToken);

        Assert.Equal(RuleRollout.Enforced(), rollout);
        Assert.Equal(new[] { Flag }, manager.GetVariantCalls);
        Assert.Equal(new[] { Flag }, manager.IsEnabledCalls);
    }

    [Fact]
    public async Task GetRolloutAsync_EnabledFlag_UsesConfiguredEnabledRollout()
    {
        FeatureManagementRolloutOptions options = new() { EnabledRollout = RuleRollout.Shadow(50) };
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag), options);

        Assert.Equal(RuleRollout.Shadow(50), await provider.GetRolloutAsync(Flag, new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task GetRolloutAsync_PlainFeatureManager_IgnoresVariants()
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag).WithVariant(Flag, "Shadow");
        FeatureManagementRuleFeatureFlagProvider provider = new((IFeatureManager)manager);

        RuleRollout rollout = await provider.GetRolloutAsync(Flag, new BusinessRuleContext(), TestToken);

        Assert.Equal(RuleRollout.Enforced(), rollout);
        Assert.Empty(manager.GetVariantCalls);
    }

    // ---------------------------------------------------------------------
    // Variant mapping
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("Shadow", RuleRolloutMode.Shadow, 100)]
    [InlineData("Enforced-10", RuleRolloutMode.Enforced, 10)]
    [InlineData("off", RuleRolloutMode.Off, 0)]
    public async Task GetRolloutAsync_VariantNamedByConvention_MapsToRollout(string variantName, RuleRolloutMode mode, int percent)
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag).WithVariant(Flag, variantName);
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(manager);

        RuleRollout rollout = await provider.GetRolloutAsync(Flag, new BusinessRuleContext(), TestToken);

        Assert.Equal(new RuleRollout(mode, percent), rollout);
        Assert.Empty(manager.IsEnabledCalls);
    }

    [Fact]
    public async Task GetRolloutAsync_VariantWithConfiguration_ReadsModeAndPercent()
    {
        Variant variant = Variants.WithConfiguration("rollout", (RolloutVariantConvention.ModeKey, "Enforced"), (RolloutVariantConvention.PercentKey, "10"));
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag).WithVariant(Flag, variant));

        Assert.Equal(RuleRollout.Enforced(10), await provider.GetRolloutAsync(Flag, new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task GetRolloutAsync_ExplicitMappingWinsOverName()
    {
        FeatureManagementRolloutOptions options = new FeatureManagementRolloutOptions().MapVariant("Shadow", RuleRollout.Enforced());
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag).WithVariant(Flag, "Shadow"), options);

        Assert.Equal(RuleRollout.Enforced(), await provider.GetRolloutAsync(Flag, new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task GetRolloutAsync_VariantAssignedWhileDisabled_VariantDecides()
    {
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Disable(Flag).WithVariant(Flag, "Enforced"));

        Assert.Equal(RuleRollout.Enforced(), await provider.GetRolloutAsync(Flag, new BusinessRuleContext(), TestToken));
    }

    // ---------------------------------------------------------------------
    // Unrecognised variants
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetRolloutAsync_UnrecognizedVariant_DefaultThrows()
    {
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag).WithVariant(Flag, "Big"));

        RolloutVariantFormatException exception = await Assert.ThrowsAsync<RolloutVariantFormatException>(() => provider.GetRolloutAsync(Flag, new BusinessRuleContext(), TestToken));

        Assert.Equal(Flag, exception.Flag);
        Assert.Equal("Big", exception.Variant);
    }

    [Fact]
    public async Task GetRolloutAsync_UnrecognizedVariant_OffBehavior_IsOff()
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag).WithVariant(Flag, "Big");
        FeatureManagementRolloutOptions options = new() { OnUnrecognizedVariant = UnrecognizedVariantBehavior.Off };
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(manager, options);

        RuleRollout rollout = await provider.GetRolloutAsync(Flag, new BusinessRuleContext(), TestToken);

        Assert.Equal(RuleRollout.Off, rollout);
        Assert.Empty(manager.IsEnabledCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetRolloutAsync_UnrecognizedVariant_UseEnabledState_FallsBackToEnabledState(bool enabled)
    {
        FakeFeatureManager manager = enabled ? new FakeFeatureManager().Enable(Flag) : new FakeFeatureManager().Disable(Flag);
        manager.WithVariant(Flag, "Big");
        FeatureManagementRolloutOptions options = new() { OnUnrecognizedVariant = UnrecognizedVariantBehavior.UseEnabledState };
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(manager, options);

        RuleRollout rollout = await provider.GetRolloutAsync(Flag, new BusinessRuleContext(), TestToken);

        Assert.Equal(enabled ? RuleRollout.Enforced() : RuleRollout.Off, rollout);
    }

    // ---------------------------------------------------------------------
    // Targeting
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetRolloutAsync_WithSelector_PassesTargetingContext()
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag);
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(manager, CreateTargetingOptions());

        await provider.GetRolloutAsync(Flag, RuleTestHelper.CreateContext((CustomerIdKey, CustomerId)), TestToken);

        Assert.Equal(CustomerId, manager.LastTargetingContext?.UserId);
    }

    [Fact]
    public async Task GetRolloutAsync_WithoutSelector_PassesNoTargetingContext()
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag);
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(manager);

        await provider.GetRolloutAsync(Flag, RuleTestHelper.CreateContext((CustomerIdKey, CustomerId)), TestToken);

        Assert.Null(manager.LastTargetingContext);
    }

    [Fact]
    public async Task GetRolloutAsync_WithSelector_PlainFeatureManagerUsesContextualOverload()
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag);
        FeatureManagementRuleFeatureFlagProvider provider = new((IFeatureManager)manager, CreateTargetingOptions());

        await provider.GetRolloutAsync(Flag, RuleTestHelper.CreateContext((CustomerIdKey, CustomerId)), TestToken);

        Assert.Equal(CustomerId, manager.LastTargetingContext?.UserId);
    }

    // ---------------------------------------------------------------------
    // End to end with FeatureGatedAsyncBusinessRule and AsyncBusinessRuleChecker
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GatedRule_FlagOff_PassesWithoutEvaluatingInner()
    {
        int evaluations = 0;
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Disable(Flag));
        IAsyncBusinessRule gated = CreateCountingRule(() => evaluations++).GatedBy(Flag, provider);

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { gated }, cancellationToken: TestToken);

        Assert.True(result.AllPassed);
        Assert.False(result.HasShadowResults);
        Assert.Equal(0, evaluations);
    }

    [Fact]
    public async Task GatedRule_ShadowVariant_PassesAndReportsShadowResult()
    {
        int evaluations = 0;
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag).WithVariant(Flag, "Shadow"));
        IAsyncBusinessRule gated = CreateCountingRule(() => evaluations++).GatedBy(Flag, provider);

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { gated }, cancellationToken: TestToken);

        Assert.True(result.AllPassed);
        Assert.Equal(Code, Assert.Single(result.ShadowResults).Code);
        Assert.Equal(1, evaluations);
    }

    [Fact]
    public async Task GatedRule_EnabledFlag_EnforcesInnerVerdict()
    {
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag));
        IAsyncBusinessRule gated = CreateCountingRule(() => { }).GatedBy(Flag, provider);

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { gated }, cancellationToken: TestToken);

        Assert.False(result.AllPassed);
        Assert.Equal(Code, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public async Task GatedRule_PartialVariant_BucketsByStableKey()
    {
        int bucket = RuleRolloutBucket.Compute(Flag, CustomerId);
        BusinessRuleContext context = RuleTestHelper.CreateContext((CustomerIdKey, CustomerId));
        FeatureManagementRuleFeatureFlagProvider inside = CreateVariantProvider(new FakeFeatureManager().Enable(Flag).WithVariant(Flag, $"Enforced-{bucket + 1}"));
        FeatureManagementRuleFeatureFlagProvider outside = CreateVariantProvider(new FakeFeatureManager().Enable(Flag).WithVariant(Flag, $"Enforced-{bucket}"));

        bool brokenInside = await CreateCountingRule(() => { }).GatedBy(Flag, inside, ctx => ctx.Get<string>(CustomerIdKey)).IsBrokenAsync(context, TestToken);
        bool brokenOutside = await CreateCountingRule(() => { }).GatedBy(Flag, outside, ctx => ctx.Get<string>(CustomerIdKey)).IsBrokenAsync(context, TestToken);

        Assert.True(brokenInside);
        Assert.False(brokenOutside);
    }

    [Fact]
    public async Task GatedRule_UsesProviderAsyncSide()
    {
        FakeFeatureManager manager = new FakeFeatureManager().Enable(Flag);
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(manager);
        FeatureGatedAsyncBusinessRule gated = CreateCountingRule(() => { }).GatedBy(Flag, provider);
        using CancellationTokenSource source = new();

        await gated.IsBrokenAsync(new BusinessRuleContext(), source.Token);

        Assert.Same(provider, gated.AsyncProvider);
        Assert.Equal(source.Token, manager.LastCancellationToken);
    }

    [Fact]
    public async Task GatedRule_UnrecognizedVariant_PropagatesFormatException()
    {
        FeatureManagementRuleFeatureFlagProvider provider = CreateVariantProvider(new FakeFeatureManager().Enable(Flag).WithVariant(Flag, "Big"));
        IAsyncBusinessRule gated = CreateCountingRule(() => { }).GatedBy(Flag, provider);

        await Assert.ThrowsAsync<RolloutVariantFormatException>(() => gated.IsBrokenAsync(new BusinessRuleContext(), TestToken));
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

    private static IAsyncBusinessRule CreateCountingRule(Action onEvaluate)
    {
        return AsyncBusinessRuleBuilder.Create(Code)
            .WithMessage("Customer is on a sanctions list")
            .WhenAsync((ctx, ct) =>
            {
                onEvaluate();
                return Task.FromResult(true);
            })
            .Build();
    }
}
