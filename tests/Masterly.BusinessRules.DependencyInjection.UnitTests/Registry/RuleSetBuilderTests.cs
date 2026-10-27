namespace Masterly.BusinessRules.DependencyInjection.UnitTests;

public class RuleSetBuilderTests
{
    private const string CheckoutSet = "checkout";
    private const string CartEmptyCode = "CART.EMPTY";
    private const string LimitCode = "ORDER.LIMIT";

    // ---------------------------------------------------------------------
    // Identity
    // ---------------------------------------------------------------------

    [Fact]
    public void Builder_ExposesNameAndScope()
    {
        RuleScope acme = RuleScope.Tenant("acme");

        RuleSetBuilder set = Configure(acme, _ => { });

        Assert.Equal(CheckoutSet, set.Name);
        Assert.Equal(acme, set.Scope);
    }

    [Fact]
    public void Builder_StartsEmpty()
    {
        RuleSetBuilder set = Configure(_ => { });

        Assert.Equal(0, set.RuleCount);
        Assert.Equal(0, set.AsyncRuleCount);
        Assert.Empty(set.DisabledCodes);
    }

    // ---------------------------------------------------------------------
    // Add
    // ---------------------------------------------------------------------

    [Fact]
    public void Add_NullFactory_ThrowsArgumentNullException()
    {
        Configure(set => Assert.Throws<ArgumentNullException>(() => set.Add((Func<BusinessRuleContext, IBusinessRule>)null!)));
    }

    [Fact]
    public void Add_NullRule_ThrowsArgumentNullException()
    {
        Configure(set => Assert.Throws<ArgumentNullException>(() => set.Add((IBusinessRule)null!)));
    }

    [Fact]
    public void Add_Factory_IncrementsRuleCountAndReturnsTheBuilder()
    {
        Configure(set =>
        {
            RuleSetBuilder returned = set.Add(_ => RuleTestHelper.CreatePassingRule());

            Assert.Same(set, returned);
            Assert.Equal(1, set.RuleCount);
            Assert.Equal(0, set.AsyncRuleCount);
        });
    }

    [Fact]
    public void Add_Instance_ResolvesTheSameInstanceEveryTime()
    {
        IBusinessRule rule = RuleTestHelper.CreatePassingRule();
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.Add(rule)).Build();

        Assert.Same(rule, Assert.Single(registry.Resolve(CheckoutSet)));
        Assert.Same(rule, Assert.Single(registry.Resolve(CheckoutSet)));
    }

    // ---------------------------------------------------------------------
    // AddAsyncRule
    // ---------------------------------------------------------------------

    [Fact]
    public void AddAsyncRule_NullFactory_ThrowsArgumentNullException()
    {
        Configure(set => Assert.Throws<ArgumentNullException>(() => set.AddAsyncRule((Func<BusinessRuleContext, IAsyncBusinessRule>)null!)));
    }

    [Fact]
    public void AddAsyncRule_NullRule_ThrowsArgumentNullException()
    {
        Configure(set => Assert.Throws<ArgumentNullException>(() => set.AddAsyncRule((IAsyncBusinessRule)null!)));
    }

    [Fact]
    public void AddAsyncRule_Factory_IncrementsAsyncRuleCountAndReturnsTheBuilder()
    {
        Configure(set =>
        {
            RuleSetBuilder returned = set.AddAsyncRule(_ => RuleTestHelper.CreatePassingAsyncRule());

            Assert.Same(set, returned);
            Assert.Equal(1, set.AsyncRuleCount);
            Assert.Equal(0, set.RuleCount);
        });
    }

    [Fact]
    public void AddAsyncRule_Instance_ResolvesTheSameInstanceEveryTime()
    {
        IAsyncBusinessRule rule = RuleTestHelper.CreatePassingAsyncRule();
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.AddAsyncRule(rule)).Build();

        Assert.Same(rule, Assert.Single(registry.ResolveAsyncRules(CheckoutSet)));
    }

    // ---------------------------------------------------------------------
    // Disable
    // ---------------------------------------------------------------------

    [Fact]
    public void Disable_NullCode_ThrowsArgumentNullException()
    {
        Configure(set => Assert.Throws<ArgumentNullException>(() => set.Disable(null!)));
    }

    [Fact]
    public void Disable_WhiteSpaceCode_ThrowsArgumentException()
    {
        Configure(set => Assert.Throws<ArgumentException>(() => set.Disable("   ")));
    }

    [Fact]
    public void Disable_RecordsTheCodeOnce()
    {
        Configure(set =>
        {
            RuleSetBuilder returned = set.Disable(CartEmptyCode).Disable(CartEmptyCode);

            Assert.Same(set, returned);
            Assert.Equal(CartEmptyCode, Assert.Single(set.DisabledCodes));
        });
    }

    [Fact]
    public void Disable_IsCaseSensitive()
    {
        Configure(set =>
        {
            set.Disable(CartEmptyCode).Disable(CartEmptyCode.ToLowerInvariant());

            Assert.Equal(2, set.DisabledCodes.Count);
        });
    }

    // ---------------------------------------------------------------------
    // Replace
    // ---------------------------------------------------------------------

    [Fact]
    public void Replace_NullCode_ThrowsArgumentNullException()
    {
        Configure(set => Assert.Throws<ArgumentNullException>(() => set.Replace(null!, _ => RuleTestHelper.CreatePassingRule())));
    }

    [Fact]
    public void Replace_NullFactory_ThrowsArgumentNullException()
    {
        Configure(set => Assert.Throws<ArgumentNullException>(() => set.Replace(CartEmptyCode, null!)));
    }

    [Fact]
    public void Replace_DisablesTheCodeAndAddsTheFactory()
    {
        Configure(set =>
        {
            RuleSetBuilder returned = set.Replace(CartEmptyCode, _ => RuleTestHelper.CreatePassingRule(LimitCode));

            Assert.Same(set, returned);
            Assert.Equal(CartEmptyCode, Assert.Single(set.DisabledCodes));
            Assert.Equal(1, set.RuleCount);
        });
    }

    [Fact]
    public void ReplaceAsyncRule_NullCode_ThrowsArgumentNullException()
    {
        Configure(set => Assert.Throws<ArgumentNullException>(() => set.ReplaceAsyncRule(null!, _ => RuleTestHelper.CreatePassingAsyncRule())));
    }

    [Fact]
    public void ReplaceAsyncRule_NullFactory_ThrowsArgumentNullException()
    {
        Configure(set => Assert.Throws<ArgumentNullException>(() => set.ReplaceAsyncRule(CartEmptyCode, null!)));
    }

    [Fact]
    public void ReplaceAsyncRule_DisablesTheCodeAndAddsTheAsyncFactory()
    {
        Configure(set =>
        {
            set.ReplaceAsyncRule(CartEmptyCode, _ => RuleTestHelper.CreatePassingAsyncRule(LimitCode));

            Assert.Equal(CartEmptyCode, Assert.Single(set.DisabledCodes));
            Assert.Equal(1, set.AsyncRuleCount);
            Assert.Equal(0, set.RuleCount);
        });
    }

    // ---------------------------------------------------------------------
    // Frozen
    // ---------------------------------------------------------------------

    [Fact]
    public void Builder_AfterAddSetReturns_RejectsEveryMutation()
    {
        RuleSetBuilder set = Configure(_ => { });

        Assert.Throws<InvalidOperationException>(() => set.Add(_ => RuleTestHelper.CreatePassingRule()));
        Assert.Throws<InvalidOperationException>(() => set.Add(RuleTestHelper.CreatePassingRule()));
        Assert.Throws<InvalidOperationException>(() => set.AddAsyncRule(_ => RuleTestHelper.CreatePassingAsyncRule()));
        Assert.Throws<InvalidOperationException>(() => set.AddAsyncRule(RuleTestHelper.CreatePassingAsyncRule()));
        Assert.Throws<InvalidOperationException>(() => set.Disable(CartEmptyCode));
        Assert.Throws<InvalidOperationException>(() => set.Replace(CartEmptyCode, _ => RuleTestHelper.CreatePassingRule()));
        Assert.Throws<InvalidOperationException>(() => set.ReplaceAsyncRule(CartEmptyCode, _ => RuleTestHelper.CreatePassingAsyncRule()));
    }

    [Fact]
    public void Builder_FrozenLayer_IsNotAffectedByLaterMutationAttempts()
    {
        RuleSetBuilder? escaped = null;
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, set => escaped = set.Add(RuleTestHelper.CreatePassingRule(LimitCode)))
            .Build();

        Assert.Throws<InvalidOperationException>(() => escaped!.Disable(LimitCode));

        Assert.Equal(LimitCode, Assert.Single(registry.Resolve(CheckoutSet)).Code);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static RuleSetBuilder Configure(Action<RuleSetBuilder> configure)
    {
        return Configure(RuleScope.Global, configure);
    }

    private static RuleSetBuilder Configure(RuleScope scope, Action<RuleSetBuilder> configure)
    {
        RuleSetBuilder? captured = null;
        RuleRegistry.Create().AddSet(CheckoutSet, scope, set =>
        {
            captured = set;
            configure(set);
        });

        return captured!;
    }
}
