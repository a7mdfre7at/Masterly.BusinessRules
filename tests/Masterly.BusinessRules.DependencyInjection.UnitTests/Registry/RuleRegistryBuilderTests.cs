namespace Masterly.BusinessRules.DependencyInjection.UnitTests;

public class RuleRegistryBuilderTests
{
    private const string CheckoutSet = "checkout";
    private const string RefundSet = "refund";
    private const string Acme = "acme";

    // ---------------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------------

    [Fact]
    public void Create_ReturnsAnEmptyBuilder()
    {
        RuleRegistryBuilder builder = RuleRegistry.Create();

        Assert.Equal(0, builder.LayerCount);
    }

    [Fact]
    public void Create_ReturnsANewBuilderEveryTime()
    {
        Assert.NotSame(RuleRegistry.Create(), RuleRegistry.Create());
    }

    // ---------------------------------------------------------------------
    // AddSet guards
    // ---------------------------------------------------------------------

    [Fact]
    public void AddSet_NullName_ThrowsArgumentNullException()
    {
        RuleRegistryBuilder builder = RuleRegistry.Create();

        Assert.Throws<ArgumentNullException>(() => builder.AddSet(null!, set => { }));
        Assert.Throws<ArgumentNullException>(() => builder.AddSet(null!, RuleScope.Global, set => { }));
    }

    [Fact]
    public void AddSet_WhiteSpaceName_ThrowsArgumentException()
    {
        RuleRegistryBuilder builder = RuleRegistry.Create();

        Assert.Throws<ArgumentException>(() => builder.AddSet(" ", set => { }));
        Assert.Throws<ArgumentException>(() => builder.AddSet(string.Empty, RuleScope.Global, set => { }));
    }

    [Fact]
    public void AddSet_NullScope_ThrowsArgumentNullException()
    {
        RuleRegistryBuilder builder = RuleRegistry.Create();

        Assert.Throws<ArgumentNullException>(() => builder.AddSet(CheckoutSet, null!, set => { }));
    }

    [Fact]
    public void AddSet_NullConfigure_ThrowsArgumentNullException()
    {
        RuleRegistryBuilder builder = RuleRegistry.Create();

        Assert.Throws<ArgumentNullException>(() => builder.AddSet(CheckoutSet, null!));
        Assert.Throws<ArgumentNullException>(() => builder.AddSet(CheckoutSet, RuleScope.Global, null!));
    }

    [Fact]
    public void AddSet_InvalidArguments_DoNotAddALayer()
    {
        RuleRegistryBuilder builder = RuleRegistry.Create();

        Assert.ThrowsAny<ArgumentException>(() => builder.AddSet(" ", set => { }));

        Assert.Equal(0, builder.LayerCount);
    }

    // ---------------------------------------------------------------------
    // AddSet behaviour
    // ---------------------------------------------------------------------

    [Fact]
    public void AddSet_WithoutScope_ConfiguresAGlobalLayer()
    {
        RuleSetBuilder? seen = null;

        RuleRegistry.Create().AddSet(CheckoutSet, set => seen = set);

        Assert.NotNull(seen);
        Assert.Equal(CheckoutSet, seen!.Name);
        Assert.Equal(RuleScope.Global, seen.Scope);
    }

    [Fact]
    public void AddSet_WithScope_ConfiguresALayerForThatScope()
    {
        RuleSetBuilder? seen = null;
        RuleScope acme = RuleScope.Tenant(Acme);

        RuleRegistry.Create().AddSet(CheckoutSet, acme, set => seen = set);

        Assert.NotNull(seen);
        Assert.Equal(acme, seen!.Scope);
    }

    [Fact]
    public void AddSet_IncrementsLayerCountAndReturnsTheBuilder()
    {
        RuleRegistryBuilder builder = RuleRegistry.Create();

        RuleRegistryBuilder returned = builder.AddSet(CheckoutSet, set => { }).AddSet(CheckoutSet, RuleScope.Tenant(Acme), set => { });

        Assert.Same(builder, returned);
        Assert.Equal(2, builder.LayerCount);
    }

    [Fact]
    public void AddSet_EscapedSetBuilder_IsFrozenAfterTheCallReturns()
    {
        RuleSetBuilder? escaped = null;
        RuleRegistry.Create().AddSet(CheckoutSet, set => escaped = set);

        Assert.Throws<InvalidOperationException>(() => escaped!.Add(RuleTestHelper.CreatePassingRule()));
        Assert.Throws<InvalidOperationException>(() => escaped!.AddAsyncRule(RuleTestHelper.CreatePassingAsyncRule()));
        Assert.Throws<InvalidOperationException>(() => escaped!.Disable("ANY"));
    }

    [Fact]
    public void AddSet_ConfigureThrows_PropagatesAndAddsNoLayer()
    {
        RuleRegistryBuilder builder = RuleRegistry.Create();

        Assert.Throws<InvalidOperationException>(() => builder.AddSet(CheckoutSet, set => throw new InvalidOperationException("boom")));

        Assert.Equal(0, builder.LayerCount);
    }

    // ---------------------------------------------------------------------
    // Build
    // ---------------------------------------------------------------------

    [Fact]
    public void Build_ReturnsARegistryWithTheDeclaredSets()
    {
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(RefundSet, set => { })
            .AddSet(CheckoutSet, set => { })
            .AddSet(CheckoutSet, RuleScope.Tenant(Acme), set => { })
            .Build();

        Assert.Equal(new[] { CheckoutSet, RefundSet }, registry.SetNames);
    }

    [Fact]
    public void Build_EmptyBuilder_ReturnsAnEmptyRegistry()
    {
        RuleRegistry registry = RuleRegistry.Create().Build();

        Assert.Empty(registry.SetNames);
        Assert.False(registry.Contains(CheckoutSet));
    }

    [Fact]
    public void Build_Twice_ThrowsInvalidOperationException()
    {
        RuleRegistryBuilder builder = RuleRegistry.Create().AddSet(CheckoutSet, set => { });
        builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void AddSet_AfterBuild_ThrowsInvalidOperationException()
    {
        RuleRegistryBuilder builder = RuleRegistry.Create();
        builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.AddSet(CheckoutSet, set => { }));
        Assert.Throws<InvalidOperationException>(() => builder.AddSet(CheckoutSet, RuleScope.Global, set => { }));
    }

    [Fact]
    public void Build_ReturnsAnIRuleRegistry()
    {
        RuleRegistry registry = RuleRegistry.Create().Build();

        Assert.IsAssignableFrom<IRuleRegistry>(registry);
    }
}
