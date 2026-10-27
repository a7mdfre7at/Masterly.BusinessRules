using System.Reflection;

namespace Masterly.BusinessRules.DependencyInjection.UnitTests;

public class BusinessRuleServiceCollectionExtensionsTests
{
    private const string CheckoutSet = "checkout";
    private const string RefundSet = "refund";
    private const string OrderKey = "order";
    private const decimal Limit = 100m;

    private static readonly Assembly TestAssembly = typeof(Order).Assembly;

    // ---------------------------------------------------------------------
    // Guards
    // ---------------------------------------------------------------------

    [Fact]
    public void AddBusinessRules_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() => services.AddBusinessRules(TestAssembly));
        Assert.Throws<ArgumentNullException>(() => services.AddBusinessRules(options => { }));
    }

    [Fact]
    public void AddBusinessRules_NullAssemblies_ThrowsArgumentNullException()
    {
        ServiceCollection services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddBusinessRules((Assembly[])null!));
    }

    [Fact]
    public void AddBusinessRules_NullAssemblyInArray_ThrowsArgumentException()
    {
        ServiceCollection services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddBusinessRules(TestAssembly, null!));
    }

    [Fact]
    public void AddBusinessRules_NullConfigure_ThrowsArgumentNullException()
    {
        ServiceCollection services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddBusinessRules((Action<BusinessRuleOptions>)null!));
    }

    // ---------------------------------------------------------------------
    // Scanning
    // ---------------------------------------------------------------------

    [Fact]
    public void AddBusinessRules_Assembly_RegistersTypedSyncRulesInTypeNameOrder()
    {
        using ServiceProvider provider = BuildProvider(services => services.AddBusinessRules(TestAssembly));
        using IServiceScope scope = provider.CreateScope();

        IEnumerable<IBusinessRule<Order>> rules = scope.ServiceProvider.GetRequiredService<IEnumerable<IBusinessRule<Order>>>();

        Assert.Equal(
            new[] { TestRuleCodes.Audit, TestRuleCodes.TooLarge, TestRuleCodes.NoItems, TestRuleCodes.NegativeTotal },
            rules.Select(rule => rule.Code));
    }

    [Fact]
    public void AddBusinessRules_Assembly_RegistersTypedAsyncRules()
    {
        using ServiceProvider provider = BuildProvider(services => services.AddBusinessRules(TestAssembly));
        using IServiceScope scope = provider.CreateScope();

        IEnumerable<IAsyncBusinessRule<Order>> rules = scope.ServiceProvider.GetRequiredService<IEnumerable<IAsyncBusinessRule<Order>>>();

        Assert.Equal(new[] { TestRuleCodes.Audit, TestRuleCodes.TooManyItems }, rules.Select(rule => rule.Code));
    }

    [Fact]
    public void AddBusinessRules_Assembly_RegistersUntypedRulesByDefault()
    {
        using ServiceProvider provider = BuildProvider(services => services.AddBusinessRules(TestAssembly));
        using IServiceScope scope = provider.CreateScope();

        IBusinessRule syncRule = Assert.Single(scope.ServiceProvider.GetRequiredService<IEnumerable<IBusinessRule>>());
        IAsyncBusinessRule asyncRule = Assert.Single(scope.ServiceProvider.GetRequiredService<IEnumerable<IAsyncBusinessRule>>());

        Assert.IsType<ContextFlagRule>(syncRule);
        Assert.IsType<ContextFlagAsyncRule>(asyncRule);
    }

    [Fact]
    public void AddBusinessRules_IncludeUntypedRulesFalse_SkipsUntypedRules()
    {
        using ServiceProvider provider = BuildProvider(services => services.AddBusinessRules(options =>
        {
            options.IncludeUntypedRules = false;
            options.AddAssembly(TestAssembly);
        }));
        using IServiceScope scope = provider.CreateScope();

        Assert.Empty(scope.ServiceProvider.GetRequiredService<IEnumerable<IBusinessRule>>());
        Assert.Empty(scope.ServiceProvider.GetRequiredService<IEnumerable<IAsyncBusinessRule>>());
        Assert.NotEmpty(scope.ServiceProvider.GetRequiredService<IEnumerable<IBusinessRule<Order>>>());
    }

    [Fact]
    public void AddBusinessRules_Assembly_SkipsAbstractAndOpenGenericTypes()
    {
        ServiceCollection services = new ServiceCollection();

        services.AddBusinessRules(TestAssembly);

        Assert.DoesNotContain(services, descriptor => descriptor.ImplementationType == typeof(AbstractOrderRule));
        Assert.DoesNotContain(services, descriptor => descriptor.ImplementationType == typeof(AlwaysPassingRule<>));
    }

    [Fact]
    public void AddBusinessRules_Default_SkipsNonPublicTypes()
    {
        ServiceCollection services = new ServiceCollection();

        services.AddBusinessRules(TestAssembly);

        Assert.DoesNotContain(services, descriptor => descriptor.ImplementationType == typeof(InternalOrderRule));
    }

    [Fact]
    public void AddBusinessRules_IncludeNonPublicTypes_RegistersInternalRule()
    {
        using ServiceProvider provider = BuildProvider(services => services.AddBusinessRules(options =>
        {
            options.IncludeNonPublicTypes = true;
            options.AddAssembly(TestAssembly);
        }));
        using IServiceScope scope = provider.CreateScope();

        IEnumerable<IBusinessRule<Order>> rules = scope.ServiceProvider.GetRequiredService<IEnumerable<IBusinessRule<Order>>>();

        Assert.Contains(rules, rule => rule.Code == TestRuleCodes.Internal);
    }

    [Fact]
    public void AddBusinessRules_TypeFilter_ExcludesRejectedTypes()
    {
        using ServiceProvider provider = BuildProvider(services => services.AddBusinessRules(options =>
        {
            options.TypeFilter = type => type != typeof(OrderLimitRule);
            options.AddAssembly(TestAssembly);
        }));
        using IServiceScope scope = provider.CreateScope();

        IEnumerable<IBusinessRule<Order>> rules = scope.ServiceProvider.GetRequiredService<IEnumerable<IBusinessRule<Order>>>();

        Assert.DoesNotContain(rules, rule => rule.Code == TestRuleCodes.TooLarge);
        Assert.Contains(rules, rule => rule.Code == TestRuleCodes.NoItems);
    }

    [Fact]
    public void AddBusinessRules_CalledTwiceForTheSameAssembly_RegistersEachRuleOnce()
    {
        using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddBusinessRules(TestAssembly);
            services.AddBusinessRules(TestAssembly);
        });
        using IServiceScope scope = provider.CreateScope();

        IEnumerable<IBusinessRule<Order>> rules = scope.ServiceProvider.GetRequiredService<IEnumerable<IBusinessRule<Order>>>();

        Assert.Equal(4, rules.Count());
    }

    [Fact]
    public void AddBusinessRules_DualInterfaceRule_IsRegisteredUnderBothInterfaces()
    {
        ServiceCollection services = new ServiceCollection();

        services.AddBusinessRules(TestAssembly);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IBusinessRule<Order>) && descriptor.ImplementationType == typeof(OrderAuditRule));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAsyncBusinessRule<Order>) && descriptor.ImplementationType == typeof(OrderAuditRule));
    }

    [Fact]
    public void AddBusinessRules_NoAssemblies_RegistersRunnerAndRegistryOnly()
    {
        using ServiceProvider provider = BuildProvider(services => services.AddBusinessRules());
        using IServiceScope scope = provider.CreateScope();

        IRuleRunner<Order> runner = scope.ServiceProvider.GetRequiredService<IRuleRunner<Order>>();
        IRuleRegistry registry = scope.ServiceProvider.GetRequiredService<IRuleRegistry>();

        Assert.Empty(runner.Rules);
        Assert.Empty(runner.AsyncRules);
        Assert.Empty(registry.SetNames);
    }

    [Fact]
    public void AddBusinessRules_ReturnsTheServiceCollection()
    {
        ServiceCollection services = new ServiceCollection();

        IServiceCollection returned = services.AddBusinessRules(TestAssembly);

        Assert.Same(services, returned);
    }

    // ---------------------------------------------------------------------
    // Lifetimes
    // ---------------------------------------------------------------------

    [Fact]
    public void AddBusinessRules_DefaultLifetime_IsScoped()
    {
        using ServiceProvider provider = BuildProvider(services => services.AddBusinessRules(TestAssembly));
        using IServiceScope first = provider.CreateScope();
        using IServiceScope second = provider.CreateScope();

        IBusinessRule<Order> fromFirst = first.ServiceProvider.GetRequiredService<IEnumerable<IBusinessRule<Order>>>().First();
        IBusinessRule<Order> fromFirstAgain = first.ServiceProvider.GetRequiredService<IEnumerable<IBusinessRule<Order>>>().First();
        IBusinessRule<Order> fromSecond = second.ServiceProvider.GetRequiredService<IEnumerable<IBusinessRule<Order>>>().First();

        Assert.Same(fromFirst, fromFirstAgain);
        Assert.NotSame(fromFirst, fromSecond);
    }

    [Fact]
    public void AddBusinessRules_SingletonLifetime_SharesInstancesAcrossScopes()
    {
        using ServiceProvider provider = BuildProvider(services => services.AddBusinessRules(options =>
        {
            options.Lifetime = ServiceLifetime.Singleton;
            options.AddAssembly(TestAssembly);
        }));
        using IServiceScope first = provider.CreateScope();
        using IServiceScope second = provider.CreateScope();

        IRuleRunner<Order> fromFirst = first.ServiceProvider.GetRequiredService<IRuleRunner<Order>>();
        IRuleRunner<Order> fromSecond = second.ServiceProvider.GetRequiredService<IRuleRunner<Order>>();

        Assert.Same(fromFirst, fromSecond);
        Assert.Same(fromFirst.Rules[0], fromSecond.Rules[0]);
    }

    // ---------------------------------------------------------------------
    // Runner
    // ---------------------------------------------------------------------

    [Fact]
    public void AddBusinessRules_RegistersRuleRunnerOverTheScannedRules()
    {
        using ServiceProvider provider = BuildProvider(services => services.AddBusinessRules(TestAssembly));
        using IServiceScope scope = provider.CreateScope();

        IRuleRunner<Order> runner = scope.ServiceProvider.GetRequiredService<IRuleRunner<Order>>();

        Assert.Equal(4, runner.Rules.Count);
        Assert.Equal(2, runner.AsyncRules.Count);
    }

    [Fact]
    public void AddBusinessRules_RunnerForUnregisteredSubject_HasNoRules()
    {
        using ServiceProvider provider = BuildProvider(services => services.AddBusinessRules(TestAssembly));
        using IServiceScope scope = provider.CreateScope();

        IRuleRunner<string> runner = scope.ServiceProvider.GetRequiredService<IRuleRunner<string>>();

        Assert.Empty(runner.Rules);
        Assert.Empty(runner.AsyncRules);
        runner.CheckAll("anything");
    }

    [Fact]
    public async Task ScannedRuleWithDependency_ThroughTheRunner_UsesTheInjectedDependency()
    {
        using ServiceProvider provider = BuildProvider(services => services.AddBusinessRules(TestAssembly));
        using IServiceScope scope = provider.CreateScope();
        IRuleRunner<Order> runner = scope.ServiceProvider.GetRequiredService<IRuleRunner<Order>>();

        AsyncRuleEvaluationResult result = await runner.EvaluateAllAsync(new Order(3, Limit + 1m), TestContext.Current.CancellationToken);

        Assert.Equal(TestRuleCodes.TooLarge, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public void AddBusinessRules_ExistingRunnerRegistration_IsKept()
    {
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton(typeof(IRuleRunner<>), typeof(RuleRunner<>));

        services.AddBusinessRules(TestAssembly);

        ServiceDescriptor descriptor = Assert.Single(services, candidate => candidate.ServiceType == typeof(IRuleRunner<>));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    // ---------------------------------------------------------------------
    // Registry
    // ---------------------------------------------------------------------

    [Fact]
    public void AddBusinessRules_WithoutRuleSets_RegistersAnEmptyRegistry()
    {
        using ServiceProvider provider = BuildProvider(services => services.AddBusinessRules(TestAssembly));

        IRuleRegistry registry = provider.GetRequiredService<IRuleRegistry>();

        Assert.Empty(registry.SetNames);
    }

    [Fact]
    public void AddBusinessRules_Registry_IsASingleton()
    {
        using ServiceProvider provider = BuildProvider(services => services.AddBusinessRules(options => options.AddRuleSet(CheckoutSet, set => { })));
        using IServiceScope scope = provider.CreateScope();

        IRuleRegistry root = provider.GetRequiredService<IRuleRegistry>();
        IRuleRegistry scoped = scope.ServiceProvider.GetRequiredService<IRuleRegistry>();

        Assert.Same(root, scoped);
    }

    [Fact]
    public void AddBusinessRules_RuleSetsFromSeveralCalls_AreMergedIntoOneRegistry()
    {
        using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddBusinessRules(options => options.AddRuleSet(CheckoutSet, set => set.Add(RuleTestHelper.CreateBrokenRule(TestRuleCodes.NoItems))));
            services.AddBusinessRules(options => options
                .AddRuleSet(RefundSet, set => set.Add(RuleTestHelper.CreatePassingRule()))
                .AddRuleSet(CheckoutSet, RuleScope.Tenant("acme"), set => set.Disable(TestRuleCodes.NoItems)));
        });

        IRuleRegistry registry = provider.GetRequiredService<IRuleRegistry>();

        Assert.Equal(new[] { CheckoutSet, RefundSet }, registry.SetNames);
        Assert.Single(registry.Resolve(CheckoutSet));
        Assert.Empty(registry.Resolve(CheckoutSet, RuleScope.Tenant("acme")));
    }

    [Fact]
    public void AddBusinessRules_RuleSetWithProviderCallback_ReceivesTheRootProvider()
    {
        using ServiceProvider provider = BuildProvider(services => services.AddBusinessRules(options => options
            .AddRuleSet(CheckoutSet, (serviceProvider, set) => set
                .Add(ctx => new OrderLimitRule(serviceProvider.GetRequiredService<IOrderLimitSource>()).For(ctx.Get<Order>(OrderKey))))));
        IRuleRegistry registry = provider.GetRequiredService<IRuleRegistry>();

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => registry.CheckAll(CheckoutSet, null, RuleTestHelper.CreateContext((OrderKey, new Order(1, Limit + 1m)))));
        registry.CheckAll(CheckoutSet, null, RuleTestHelper.CreateContext((OrderKey, new Order(1, Limit))));

        Assert.Equal(TestRuleCodes.TooLarge, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void AddBusinessRules_RuleSetWithScopeAndProviderCallback_AppliesToTheScope()
    {
        using ServiceProvider provider = BuildProvider(services => services.AddBusinessRules(options => options
            .AddRuleSet(CheckoutSet, set => set.Add(RuleTestHelper.CreatePassingRule()))
            .AddRuleSet(CheckoutSet, RuleScope.Tenant("acme"), (serviceProvider, set) => set.Add(RuleTestHelper.CreateBrokenRule(TestRuleCodes.Audit)))));
        IRuleRegistry registry = provider.GetRequiredService<IRuleRegistry>();

        Assert.Single(registry.Resolve(CheckoutSet));
        Assert.Equal(2, registry.Resolve(CheckoutSet, RuleScope.Tenant("acme")).Count);
    }

    [Fact]
    public void AddBusinessRules_RuleSetCallbacks_RunOnlyWhenTheRegistryIsResolved()
    {
        int calls = 0;
        using ServiceProvider provider = BuildProvider(services => services.AddBusinessRules(options => options.AddRuleSet(CheckoutSet, set => calls++)));

        Assert.Equal(0, calls);
        provider.GetRequiredService<IRuleRegistry>();
        provider.GetRequiredService<IRuleRegistry>();

        Assert.Equal(1, calls);
    }

    [Fact]
    public void AddBusinessRules_ExistingRegistryRegistration_IsKept()
    {
        RuleRegistry custom = RuleRegistry.Create().AddSet(RefundSet, set => { }).Build();
        using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddSingleton<IRuleRegistry>(custom);
            services.AddBusinessRules(options => options.AddRuleSet(CheckoutSet, set => { }));
        });

        IRuleRegistry registry = provider.GetRequiredService<IRuleRegistry>();

        Assert.Same(custom, registry);
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection> register)
    {
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton<IOrderLimitSource>(new FixedOrderLimitSource(Limit));
        register(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
