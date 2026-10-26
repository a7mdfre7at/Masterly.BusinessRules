namespace Masterly.BusinessRules.UnitTests;

public class RuleTestHelperTypedContextTests
{
    private static readonly ContextKey<string> OrderKey = new ContextKey<string>("order");
    private static readonly ContextKey<decimal> LimitKey = new ContextKey<decimal>("limit");
    private static readonly ContextKey<int> CountKey = new ContextKey<int>("count");
    private static readonly ContextKey<bool> FlagKey = new ContextKey<bool>("flag");

    [Fact]
    public void CreateContext_OneTypedPair_StoresValueUnderKeyName()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((OrderKey, "order-1"));

        Assert.Equal(1, context.Count);
        Assert.Equal("order-1", context.Get(OrderKey));
        Assert.Equal("order-1", context.Get<string>("order"));
    }

    [Fact]
    public void CreateContext_TwoTypedPairsOfDifferentTypes_StoresBoth()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((OrderKey, "order-1"), (LimitKey, 1000m));

        Assert.Equal(2, context.Count);
        Assert.Equal("order-1", context.Get(OrderKey));
        Assert.Equal(1000m, context.Get(LimitKey));
    }

    [Fact]
    public void CreateContext_ThreeTypedPairs_StoresAll()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((OrderKey, "order-1"), (LimitKey, 1000m), (CountKey, 3));

        Assert.Equal(3, context.Count);
        Assert.Equal(3, context.Get(CountKey));
    }

    [Fact]
    public void CreateContext_FourTypedPairs_StoresAll()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((OrderKey, "order-1"), (LimitKey, 1000m), (CountKey, 3), (FlagKey, true));

        Assert.Equal(4, context.Count);
        Assert.True(context.Get(FlagKey));
        Assert.Equal(new[] { "count", "flag", "limit", "order" }, context.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void CreateContext_SameKeyTwice_LastValueWins()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((LimitKey, 1m), (LimitKey, 2m));

        Assert.Equal(1, context.Count);
        Assert.Equal(2m, context.Get(LimitKey));
    }

    [Fact]
    public void CreateContext_TypedPairs_RuleReadsThemThroughTypedGet()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((LimitKey, 1000m));
        IBusinessRule rule = BusinessRuleBuilder.Create("LIMIT")
            .When(ctx => 500m > ctx.Get(LimitKey))
            .Build();

        RuleTestHelper.AssertNotBroken(rule, context);
    }

    [Fact]
    public void CreateContext_StringPairs_StillResolvesToTheStringOverload()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext(("userId", 123), ("role", "admin"));

        Assert.Equal(123, context.Get<int>("userId"));
        Assert.Equal("admin", context.Get<string>("role"));
    }

    [Fact]
    public void CreateContext_DefaultTypedKey_ThrowsArgumentNullException()
    {
        ContextKey<int> key = default;

        Assert.Throws<ArgumentNullException>(() => RuleTestHelper.CreateContext((key, 1)));
    }
}
