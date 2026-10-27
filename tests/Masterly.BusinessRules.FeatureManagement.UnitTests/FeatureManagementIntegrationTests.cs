using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

namespace Masterly.BusinessRules.FeatureManagement.UnitTests;

/// <summary>
/// Runs the adapter against the real Microsoft.FeatureManagement <c>FeatureManager</c> fed by in-memory configuration,
/// so the variant schema (<c>variants</c>, <c>allocation</c>, <c>configuration_value</c>) and the targeting filter are
/// exercised for real rather than through <see cref="FakeFeatureManager"/>.
/// </summary>
public class FeatureManagementIntegrationTests
{
    private const string ShadowFlag = "credit-score-v2";
    private const string PlainOnFlag = "plain-on";
    private const string PlainOffFlag = "plain-off";
    private const string NamedVariantFlag = "sanctions-check";
    private const string DisabledWithVariantFlag = "disabled-with-variant";
    private const string TargetedFlag = "targeted";
    private const string UnknownFlag = "never-configured";
    private const string CustomerIdKey = "customerId";
    private const string TargetedUser = "alice";
    private const string OtherUser = "bob";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RealFeatureManager_VariantWithConfigurationValue_MapsToShadowPercent()
    {
        using ServiceProvider services = BuildServices();
        IAsyncRuleFeatureFlagProvider provider = services.GetRequiredService<IAsyncRuleFeatureFlagProvider>();

        RuleRollout rollout = await provider.GetRolloutAsync(ShadowFlag, new BusinessRuleContext(), TestToken);

        Assert.Equal(RuleRollout.Shadow(10), rollout);
    }

    [Fact]
    public async Task RealFeatureManager_VariantNamedByConvention_MapsByName()
    {
        using ServiceProvider services = BuildServices();
        IAsyncRuleFeatureFlagProvider provider = services.GetRequiredService<IAsyncRuleFeatureFlagProvider>();

        RuleRollout rollout = await provider.GetRolloutAsync(NamedVariantFlag, new BusinessRuleContext(), TestToken);

        Assert.Equal(RuleRollout.Enforced(25), rollout);
    }

    [Fact]
    public async Task RealFeatureManager_PlainEnabledFlag_IsEnforced()
    {
        using ServiceProvider services = BuildServices();
        IAsyncRuleFeatureFlagProvider provider = services.GetRequiredService<IAsyncRuleFeatureFlagProvider>();

        Assert.Equal(RuleRollout.Enforced(), await provider.GetRolloutAsync(PlainOnFlag, new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task RealFeatureManager_PlainDisabledFlag_IsOff()
    {
        using ServiceProvider services = BuildServices();
        IAsyncRuleFeatureFlagProvider provider = services.GetRequiredService<IAsyncRuleFeatureFlagProvider>();

        Assert.Equal(RuleRollout.Off, await provider.GetRolloutAsync(PlainOffFlag, new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task RealFeatureManager_UnknownFlag_IsOff()
    {
        using ServiceProvider services = BuildServices();
        IAsyncRuleFeatureFlagProvider provider = services.GetRequiredService<IAsyncRuleFeatureFlagProvider>();

        Assert.Equal(RuleRollout.Off, await provider.GetRolloutAsync(UnknownFlag, new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task RealFeatureManager_DisabledFlagWithDefaultWhenDisabledVariant_VariantDecides()
    {
        using ServiceProvider services = BuildServices();
        IAsyncRuleFeatureFlagProvider provider = services.GetRequiredService<IAsyncRuleFeatureFlagProvider>();

        RuleRollout rollout = await provider.GetRolloutAsync(DisabledWithVariantFlag, new BusinessRuleContext(), TestToken);

        Assert.Equal(RuleRollout.Off, rollout);
    }

    [Fact]
    public void RealFeatureManager_SyncSide_BlocksAndReturnsSameRollout()
    {
        using ServiceProvider services = BuildServices();
        IRuleFeatureFlagProvider provider = services.GetRequiredService<IRuleFeatureFlagProvider>();

        RuleRollout rollout = provider.GetRollout(ShadowFlag, new BusinessRuleContext());

        Assert.Equal(RuleRollout.Shadow(10), rollout);
    }

    [Theory]
    [InlineData(TargetedUser, true)]
    [InlineData(OtherUser, false)]
    public async Task RealFeatureManager_TargetingFilter_SeesSelectedTargetingContext(string userId, bool expectedEnforced)
    {
        using ServiceProvider services = BuildServices(options =>
        {
            options.TargetingContextSelector = ctx => new TargetingContext { UserId = ctx.Get<string>(CustomerIdKey) };
        });
        IAsyncRuleFeatureFlagProvider provider = services.GetRequiredService<IAsyncRuleFeatureFlagProvider>();

        RuleRollout rollout = await provider.GetRolloutAsync(TargetedFlag, RuleTestHelper.CreateContext((CustomerIdKey, userId)), TestToken);

        Assert.Equal(expectedEnforced ? RuleRollout.Enforced() : RuleRollout.Off, rollout);
    }

    [Fact]
    public async Task RealFeatureManager_GatedAsyncRule_ReportsShadowResult()
    {
        using ServiceProvider services = BuildServices();
        IAsyncRuleFeatureFlagProvider provider = services.GetRequiredService<IAsyncRuleFeatureFlagProvider>();
        IAsyncBusinessRule gated = AsyncBusinessRuleBuilder.Create("CREDIT.SCORE")
            .WithMessage("Credit score is too low")
            .WhenAsync((ctx, ct) => Task.FromResult(true))
            .Build()
            .GatedBy(ShadowFlag, provider, stableKey: ctx => ctx.Get<string>(CustomerIdKey));
        string insideCohort = FindStableKeyInside(ShadowFlag, percent: 10);
        BusinessRuleContext context = RuleTestHelper.CreateContext((CustomerIdKey, insideCohort));

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(context, new[] { gated }, cancellationToken: TestToken);

        Assert.True(result.AllPassed);
        Assert.Equal("CREDIT.SCORE", Assert.Single(result.ShadowResults).Code);
    }

    [Fact]
    public void RealFeatureManager_GatedSyncRule_EnforcesPlainFlag()
    {
        using ServiceProvider services = BuildServices();
        IRuleFeatureFlagProvider provider = services.GetRequiredService<IRuleFeatureFlagProvider>();
        IBusinessRule gated = RuleTestHelper.CreateBrokenRule("PLAIN.RULE").GatedBy(PlainOnFlag, provider);

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { gated }, new BusinessRuleContext());

        Assert.False(result.AllPassed);
        Assert.Equal("PLAIN.RULE", Assert.Single(result.BrokenRules).Code);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static ServiceProvider BuildServices(Action<FeatureManagementRolloutOptions>? configure = null)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(FeatureFlagConfiguration()).Build();
        ServiceCollection services = new();
        services.AddFeatureManagement(configuration);
        services.AddBusinessRulesFeatureManagement(configure);
        return services.BuildServiceProvider();
    }

    private static Dictionary<string, string?> FeatureFlagConfiguration()
    {
        const string Flags = "feature_management:feature_flags";

        return new Dictionary<string, string?>
        {
            [$"{Flags}:0:id"] = ShadowFlag,
            [$"{Flags}:0:enabled"] = "true",
            [$"{Flags}:0:variants:0:name"] = "rollout",
            [$"{Flags}:0:variants:0:configuration_value:{RolloutVariantConvention.ModeKey}"] = "Shadow",
            [$"{Flags}:0:variants:0:configuration_value:{RolloutVariantConvention.PercentKey}"] = "10",
            [$"{Flags}:0:allocation:default_when_enabled"] = "rollout",

            [$"{Flags}:1:id"] = PlainOnFlag,
            [$"{Flags}:1:enabled"] = "true",

            [$"{Flags}:2:id"] = PlainOffFlag,
            [$"{Flags}:2:enabled"] = "false",

            [$"{Flags}:3:id"] = NamedVariantFlag,
            [$"{Flags}:3:enabled"] = "true",
            [$"{Flags}:3:variants:0:name"] = "Enforced-25",
            [$"{Flags}:3:allocation:default_when_enabled"] = "Enforced-25",

            [$"{Flags}:4:id"] = DisabledWithVariantFlag,
            [$"{Flags}:4:enabled"] = "false",
            [$"{Flags}:4:variants:0:name"] = "Off",
            [$"{Flags}:4:variants:1:name"] = "Enforced",
            [$"{Flags}:4:allocation:default_when_disabled"] = "Off",
            [$"{Flags}:4:allocation:default_when_enabled"] = "Enforced",

            [$"{Flags}:5:id"] = TargetedFlag,
            [$"{Flags}:5:enabled"] = "true",
            [$"{Flags}:5:conditions:client_filters:0:name"] = "Microsoft.Targeting",
            [$"{Flags}:5:conditions:client_filters:0:parameters:Audience:Users:0"] = TargetedUser,
            [$"{Flags}:5:conditions:client_filters:0:parameters:Audience:DefaultRolloutPercentage"] = "0"
        };
    }

    private static string FindStableKeyInside(string flag, int percent)
    {
        for (int candidate = 0; candidate < 10_000; candidate++)
        {
            string key = $"cust-{candidate}";

            if (RuleRolloutBucket.Compute(flag, key) < percent)
            {
                return key;
            }
        }

        throw new InvalidOperationException("No stable key fell inside the cohort.");
    }
}
