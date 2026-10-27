using System.Reflection;

namespace Masterly.BusinessRules.DependencyInjection.UnitTests;

public class BusinessRuleOptionsTests
{
    private const string CheckoutSet = "checkout";

    private static readonly Assembly TestAssembly = typeof(Order).Assembly;
    private static readonly Assembly CoreAssembly = typeof(IBusinessRule).Assembly;

    // ---------------------------------------------------------------------
    // Defaults
    // ---------------------------------------------------------------------

    [Fact]
    public void Defaults_AreScopedPublicWithoutUntypedRulesAndNoFilter()
    {
        BusinessRuleOptions options = new BusinessRuleOptions();

        Assert.Equal(ServiceLifetime.Scoped, options.Lifetime);
        Assert.False(options.IncludeNonPublicTypes);
        Assert.False(options.IncludeUntypedRules);
        Assert.Null(options.TypeFilter);
        Assert.Empty(options.Assemblies);
        Assert.Equal(0, options.RuleSetCount);
    }

    // ---------------------------------------------------------------------
    // Assemblies
    // ---------------------------------------------------------------------

    [Fact]
    public void AddAssembly_NullAssembly_ThrowsArgumentNullException()
    {
        BusinessRuleOptions options = new BusinessRuleOptions();

        Assert.Throws<ArgumentNullException>(() => options.AddAssembly(null!));
    }

    [Fact]
    public void AddAssembly_SameAssemblyTwice_AddsItOnce()
    {
        BusinessRuleOptions options = new BusinessRuleOptions();

        options.AddAssembly(TestAssembly).AddAssembly(TestAssembly);

        Assert.Equal(TestAssembly, Assert.Single(options.Assemblies));
    }

    [Fact]
    public void AddAssemblies_NullArray_ThrowsArgumentNullException()
    {
        BusinessRuleOptions options = new BusinessRuleOptions();

        Assert.Throws<ArgumentNullException>(() => options.AddAssemblies(null!));
    }

    [Fact]
    public void AddAssemblies_NullElement_ThrowsArgumentExceptionNamingThePosition()
    {
        BusinessRuleOptions options = new BusinessRuleOptions();

        ArgumentException exception = Assert.Throws<ArgumentException>(() => options.AddAssemblies(TestAssembly, null!));

        Assert.Contains("position 1", exception.Message);
        Assert.Empty(options.Assemblies);
    }

    [Fact]
    public void AddAssemblies_KeepsTheOrderGiven()
    {
        BusinessRuleOptions options = new BusinessRuleOptions();

        options.AddAssemblies(CoreAssembly, TestAssembly);

        Assert.Equal(new[] { CoreAssembly, TestAssembly }, options.Assemblies);
    }

    [Fact]
    public void AddAssemblyContaining_AddsTheMarkerTypesAssembly()
    {
        BusinessRuleOptions options = new BusinessRuleOptions();

        options.AddAssemblyContaining<Order>();

        Assert.Equal(TestAssembly, Assert.Single(options.Assemblies));
    }

    [Fact]
    public void AddAssembly_ReturnsTheSameOptionsForChaining()
    {
        BusinessRuleOptions options = new BusinessRuleOptions();

        Assert.Same(options, options.AddAssembly(TestAssembly));
        Assert.Same(options, options.AddAssemblies(TestAssembly));
        Assert.Same(options, options.AddAssemblyContaining<Order>());
    }

    // ---------------------------------------------------------------------
    // Rule sets
    // ---------------------------------------------------------------------

    [Fact]
    public void AddRuleSet_NullName_ThrowsArgumentNullException()
    {
        BusinessRuleOptions options = new BusinessRuleOptions();

        Assert.Throws<ArgumentNullException>(() => options.AddRuleSet(null!, set => { }));
        Assert.Throws<ArgumentNullException>(() => options.AddRuleSet(null!, (provider, set) => { }));
    }

    [Fact]
    public void AddRuleSet_WhiteSpaceName_ThrowsArgumentException()
    {
        BusinessRuleOptions options = new BusinessRuleOptions();

        Assert.Throws<ArgumentException>(() => options.AddRuleSet("  ", set => { }));
        Assert.Throws<ArgumentException>(() => options.AddRuleSet(string.Empty, RuleScope.Global, (provider, set) => { }));
    }

    [Fact]
    public void AddRuleSet_NullScope_ThrowsArgumentNullException()
    {
        BusinessRuleOptions options = new BusinessRuleOptions();

        Assert.Throws<ArgumentNullException>(() => options.AddRuleSet(CheckoutSet, null!, set => { }));
        Assert.Throws<ArgumentNullException>(() => options.AddRuleSet(CheckoutSet, null!, (provider, set) => { }));
    }

    [Fact]
    public void AddRuleSet_NullConfigure_ThrowsArgumentNullException()
    {
        BusinessRuleOptions options = new BusinessRuleOptions();

        Assert.Throws<ArgumentNullException>(() => options.AddRuleSet(CheckoutSet, (Action<RuleSetBuilder>)null!));
        Assert.Throws<ArgumentNullException>(() => options.AddRuleSet(CheckoutSet, RuleScope.Global, (Action<RuleSetBuilder>)null!));
        Assert.Throws<ArgumentNullException>(() => options.AddRuleSet(CheckoutSet, (Action<IServiceProvider, RuleSetBuilder>)null!));
        Assert.Throws<ArgumentNullException>(() => options.AddRuleSet(CheckoutSet, RuleScope.Global, (Action<IServiceProvider, RuleSetBuilder>)null!));
    }

    [Fact]
    public void AddRuleSet_EveryOverload_IncrementsRuleSetCount()
    {
        BusinessRuleOptions options = new BusinessRuleOptions();

        options
            .AddRuleSet(CheckoutSet, set => { })
            .AddRuleSet(CheckoutSet, RuleScope.Tenant("acme"), set => { })
            .AddRuleSet(CheckoutSet, (provider, set) => { })
            .AddRuleSet(CheckoutSet, RuleScope.Tenant("acme"), (provider, set) => { });

        Assert.Equal(4, options.RuleSetCount);
    }

    [Fact]
    public void AddRuleSet_DoesNotRunTheCallbackEagerly()
    {
        BusinessRuleOptions options = new BusinessRuleOptions();
        int calls = 0;

        options.AddRuleSet(CheckoutSet, set => calls++);

        Assert.Equal(0, calls);
    }

    [Fact]
    public void AddRuleSet_ReturnsTheSameOptionsForChaining()
    {
        BusinessRuleOptions options = new BusinessRuleOptions();

        Assert.Same(options, options.AddRuleSet(CheckoutSet, set => { }));
    }
}
