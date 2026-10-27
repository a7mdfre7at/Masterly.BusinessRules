using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;

namespace Masterly.BusinessRules.FeatureManagement.UnitTests;

public class FeatureManagementServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBusinessRulesFeatureManagement_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => services.AddBusinessRulesFeatureManagement());
    }

    [Fact]
    public void AddBusinessRulesFeatureManagement_UndefinedLifetime_ThrowsArgumentOutOfRangeException()
    {
        ServiceCollection services = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddBusinessRulesFeatureManagement(lifetime: (ServiceLifetime)42));
    }

    [Fact]
    public void AddBusinessRulesFeatureManagement_ReturnsSameCollection()
    {
        ServiceCollection services = new();

        IServiceCollection returned = services.AddBusinessRulesFeatureManagement();

        Assert.Same(services, returned);
    }

    [Fact]
    public void AddBusinessRulesFeatureManagement_RegistersBothInterfacesOverOneSingleton()
    {
        ServiceCollection services = new();
        services.AddSingleton<IVariantFeatureManager>(new FakeFeatureManager());
        services.AddBusinessRulesFeatureManagement();
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        FeatureManagementRuleFeatureFlagProvider concrete = provider.GetRequiredService<FeatureManagementRuleFeatureFlagProvider>();
        IRuleFeatureFlagProvider sync = provider.GetRequiredService<IRuleFeatureFlagProvider>();
        IAsyncRuleFeatureFlagProvider async = provider.GetRequiredService<IAsyncRuleFeatureFlagProvider>();

        Assert.Same(concrete, sync);
        Assert.Same(concrete, async);
        Assert.Same(concrete, provider.GetRequiredService<FeatureManagementRuleFeatureFlagProvider>());
    }

    [Fact]
    public void AddBusinessRulesFeatureManagement_PrefersVariantFeatureManager()
    {
        ServiceCollection services = new();
        services.AddSingleton<IVariantFeatureManager>(new FakeFeatureManager());
        services.AddSingleton<IFeatureManager>(new FakeFeatureManager());
        services.AddBusinessRulesFeatureManagement();
        using ServiceProvider provider = services.BuildServiceProvider();

        FeatureManagementRuleFeatureFlagProvider resolved = provider.GetRequiredService<FeatureManagementRuleFeatureFlagProvider>();

        Assert.True(resolved.SupportsVariants);
    }

    [Fact]
    public void AddBusinessRulesFeatureManagement_FallsBackToFeatureManager()
    {
        ServiceCollection services = new();
        services.AddSingleton<IFeatureManager>(new FakeFeatureManager());
        services.AddBusinessRulesFeatureManagement();
        using ServiceProvider provider = services.BuildServiceProvider();

        FeatureManagementRuleFeatureFlagProvider resolved = provider.GetRequiredService<FeatureManagementRuleFeatureFlagProvider>();

        Assert.False(resolved.SupportsVariants);
    }

    [Fact]
    public void AddBusinessRulesFeatureManagement_NoFeatureManager_ThrowsOnResolve()
    {
        ServiceCollection services = new();
        services.AddBusinessRulesFeatureManagement();
        using ServiceProvider provider = services.BuildServiceProvider();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IRuleFeatureFlagProvider>());

        Assert.Contains("AddFeatureManagement", exception.Message);
    }

    [Fact]
    public void AddBusinessRulesFeatureManagement_AppliesConfigure()
    {
        ServiceCollection services = new();
        services.AddSingleton<IVariantFeatureManager>(new FakeFeatureManager());
        services.AddBusinessRulesFeatureManagement(options =>
        {
            options.EnabledRollout = RuleRollout.Shadow();
            options.MapVariant("dark", RuleRollout.Shadow(10));
        });
        using ServiceProvider provider = services.BuildServiceProvider();

        FeatureManagementRuleFeatureFlagProvider resolved = provider.GetRequiredService<FeatureManagementRuleFeatureFlagProvider>();

        Assert.Equal(RuleRollout.Shadow(), resolved.Options.EnabledRollout);
        Assert.Equal(RuleRollout.Shadow(10), resolved.Options.VariantRollouts["dark"]);
        Assert.Same(resolved.Options, provider.GetRequiredService<FeatureManagementRolloutOptions>());
    }

    [Fact]
    public void AddBusinessRulesFeatureManagement_ConfiguredOptionsDriveRollout()
    {
        ServiceCollection services = new();
        services.AddSingleton<IVariantFeatureManager>(new FakeFeatureManager().Enable("flag"));
        services.AddBusinessRulesFeatureManagement(options => options.EnabledRollout = RuleRollout.Shadow());
        using ServiceProvider provider = services.BuildServiceProvider();

        RuleRollout rollout = provider.GetRequiredService<IRuleFeatureFlagProvider>().GetRollout("flag", new BusinessRuleContext());

        Assert.Equal(RuleRollout.Shadow(), rollout);
    }

    [Fact]
    public void AddBusinessRulesFeatureManagement_ExistingProviderRegistration_IsLeftAlone()
    {
        InMemoryRuleFeatureFlagProvider existing = new();
        ServiceCollection services = new();
        services.AddSingleton<IVariantFeatureManager>(new FakeFeatureManager());
        services.AddSingleton<IRuleFeatureFlagProvider>(existing);
        services.AddBusinessRulesFeatureManagement();
        using ServiceProvider provider = services.BuildServiceProvider();

        IRuleFeatureFlagProvider sync = provider.GetRequiredService<IRuleFeatureFlagProvider>();
        IAsyncRuleFeatureFlagProvider async = provider.GetRequiredService<IAsyncRuleFeatureFlagProvider>();

        Assert.Same(existing, sync);
        Assert.IsType<FeatureManagementRuleFeatureFlagProvider>(async);
    }

    [Fact]
    public void AddBusinessRulesFeatureManagement_CalledTwice_FirstOptionsWin()
    {
        ServiceCollection services = new();
        services.AddSingleton<IVariantFeatureManager>(new FakeFeatureManager());
        services.AddBusinessRulesFeatureManagement(options => options.EnabledRollout = RuleRollout.Shadow());
        services.AddBusinessRulesFeatureManagement(options => options.EnabledRollout = RuleRollout.Enforced(10));
        using ServiceProvider provider = services.BuildServiceProvider();

        FeatureManagementRuleFeatureFlagProvider resolved = provider.GetRequiredService<FeatureManagementRuleFeatureFlagProvider>();

        Assert.Equal(RuleRollout.Shadow(), resolved.Options.EnabledRollout);
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(FeatureManagementRuleFeatureFlagProvider));
    }

    [Fact]
    public void AddBusinessRulesFeatureManagement_ScopedLifetime_ResolvesPerScope()
    {
        ServiceCollection services = new();
        services.AddScoped<IVariantFeatureManager>(_ => new FakeFeatureManager());
        services.AddBusinessRulesFeatureManagement(lifetime: ServiceLifetime.Scoped);
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        using IServiceScope first = provider.CreateScope();
        using IServiceScope second = provider.CreateScope();
        IRuleFeatureFlagProvider inFirst = first.ServiceProvider.GetRequiredService<IRuleFeatureFlagProvider>();
        IAsyncRuleFeatureFlagProvider alsoInFirst = first.ServiceProvider.GetRequiredService<IAsyncRuleFeatureFlagProvider>();
        IRuleFeatureFlagProvider inSecond = second.ServiceProvider.GetRequiredService<IRuleFeatureFlagProvider>();

        Assert.Same(inFirst, alsoInFirst);
        Assert.NotSame(inFirst, inSecond);
    }

    [Fact]
    public void AddBusinessRulesFeatureManagement_TransientLifetime_ResolvesNewInstances()
    {
        ServiceCollection services = new();
        services.AddSingleton<IVariantFeatureManager>(new FakeFeatureManager());
        services.AddBusinessRulesFeatureManagement(lifetime: ServiceLifetime.Transient);
        using ServiceProvider provider = services.BuildServiceProvider();

        IRuleFeatureFlagProvider first = provider.GetRequiredService<IRuleFeatureFlagProvider>();
        IRuleFeatureFlagProvider second = provider.GetRequiredService<IRuleFeatureFlagProvider>();

        Assert.NotSame(first, second);
    }

    [Fact]
    public void AddBusinessRulesFeatureManagement_RegistersWithRequestedLifetime()
    {
        ServiceCollection services = new();

        services.AddBusinessRulesFeatureManagement(lifetime: ServiceLifetime.Scoped);

        ServiceDescriptor concrete = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(FeatureManagementRuleFeatureFlagProvider));
        ServiceDescriptor sync = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IRuleFeatureFlagProvider));
        ServiceDescriptor async = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IAsyncRuleFeatureFlagProvider));
        ServiceDescriptor options = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(FeatureManagementRolloutOptions));
        Assert.Equal(ServiceLifetime.Scoped, concrete.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, sync.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, async.Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, options.Lifetime);
    }
}
