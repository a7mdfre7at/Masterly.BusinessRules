namespace Masterly.BusinessRules.UnitTests;

public class BusinessRuleContextTypedKeyTests
{
    private const string OrderKeyName = "order";
    private const string LimitKeyName = "limit";
    private const string CustomerKeyName = "customer";
    private const string MissingKeyName = "ordr";

    private static readonly ContextKey<Order> OrderKey = new ContextKey<Order>(OrderKeyName);
    private static readonly ContextKey<decimal> LimitKey = new ContextKey<decimal>(LimitKeyName);
    private static readonly ContextKey<string> CustomerKey = new ContextKey<string>(CustomerKeyName);

    // ---------------------------------------------------------------------
    // Typed Set / Get
    // ---------------------------------------------------------------------

    [Fact]
    public void Set_TypedKey_StoresUnderKeyName()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        Order order = new Order(42);

        context.Set(OrderKey, order);

        Assert.Same(order, context.Get<Order>(OrderKeyName));
        Assert.True(context.ContainsKey(OrderKeyName));
    }

    [Fact]
    public void Get_TypedKey_ReturnsValueWithoutCast()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(LimitKey, 1000m);

        decimal limit = context.Get(LimitKey);

        Assert.Equal(1000m, limit);
    }

    [Fact]
    public void Get_TypedKey_ReadsValueStoredThroughStringApi()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        Order order = new Order(7);
        context.Set(OrderKeyName, order);

        Assert.Same(order, context.Get(OrderKey));
    }

    [Fact]
    public void Set_TypedKeyTwice_ReplacesValue()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(LimitKey, 1m);

        context.Set(LimitKey, 2m);

        Assert.Equal(2m, context.Get(LimitKey));
        Assert.Equal(1, context.Count);
    }

    [Fact]
    public void Get_TypedKeyMissing_ThrowsContextKeyNotFoundException()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(LimitKey, 1000m);

        ContextKeyNotFoundException exception = Assert.Throws<ContextKeyNotFoundException>(() => context.Get(OrderKey));

        Assert.Equal(OrderKeyName, exception.Key);
        Assert.Equal(new[] { LimitKeyName }, exception.AvailableKeys);
    }

    [Fact]
    public void Get_TypedKeyWrongStoredType_ThrowsContextValueTypeMismatchException()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(OrderKeyName, 5);

        ContextValueTypeMismatchException exception = Assert.Throws<ContextValueTypeMismatchException>(() => context.Get(OrderKey));

        Assert.Equal(OrderKeyName, exception.Key);
        Assert.Equal(typeof(int), exception.ActualType);
        Assert.Equal(typeof(Order), exception.RequestedType);
    }

    [Fact]
    public void Get_DefaultTypedKey_ThrowsArgumentNullException()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        ContextKey<Order> key = default;

        Assert.Throws<ArgumentNullException>(() => context.Get(key));
    }

    // ---------------------------------------------------------------------
    // Typed TryGet / ContainsKey / Remove
    // ---------------------------------------------------------------------

    [Fact]
    public void TryGet_TypedKeyPresent_ReturnsTrueAndValue()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(CustomerKey, "acme");

        bool found = context.TryGet(CustomerKey, out string customer);

        Assert.True(found);
        Assert.Equal("acme", customer);
    }

    [Fact]
    public void TryGet_TypedKeyMissing_ReturnsFalseAndDefault()
    {
        BusinessRuleContext context = new BusinessRuleContext();

        bool found = context.TryGet(LimitKey, out decimal limit);

        Assert.False(found);
        Assert.Equal(0m, limit);
    }

    [Fact]
    public void TryGet_TypedKeyWrongStoredType_ReturnsFalseWithoutThrowing()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(LimitKeyName, "not a decimal");

        bool found = context.TryGet(LimitKey, out decimal limit);

        Assert.False(found);
        Assert.Equal(0m, limit);
    }

    [Fact]
    public void ContainsKey_TypedKeyPresent_ReturnsTrue()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(OrderKey, new Order(1));

        Assert.True(context.ContainsKey(OrderKey));
    }

    [Fact]
    public void ContainsKey_TypedKeyMissing_ReturnsFalse()
    {
        BusinessRuleContext context = new BusinessRuleContext();

        Assert.False(context.ContainsKey(OrderKey));
    }

    [Fact]
    public void ContainsKey_TypedKeyWithOtherStoredType_ReturnsTrueBecauseOnlyTheNameIsChecked()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(OrderKeyName, "a string");

        Assert.True(context.ContainsKey(OrderKey));
    }

    [Fact]
    public void Remove_TypedKeyPresent_RemovesAndReturnsTrue()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(OrderKey, new Order(1));

        bool removed = context.Remove(OrderKey);

        Assert.True(removed);
        Assert.False(context.ContainsKey(OrderKey));
        Assert.Equal(0, context.Count);
    }

    [Fact]
    public void Remove_TypedKeyMissing_ReturnsFalse()
    {
        BusinessRuleContext context = new BusinessRuleContext();

        Assert.False(context.Remove(OrderKey));
    }

    [Fact]
    public void TryGet_DefaultTypedKey_ThrowsArgumentNullException()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        ContextKey<Order> key = default;

        Assert.Throws<ArgumentNullException>(() => context.TryGet(key, out Order _));
    }

    [Fact]
    public void Set_DefaultTypedKey_ThrowsArgumentNullException()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        ContextKey<Order> key = default;

        Assert.Throws<ArgumentNullException>(() => context.Set(key, new Order(1)));
        Assert.Equal(0, context.Count);
    }

    [Fact]
    public void ContainsKey_DefaultTypedKey_ThrowsArgumentNullException()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        ContextKey<Order> key = default;

        Assert.Throws<ArgumentNullException>(() => context.ContainsKey(key));
    }

    [Fact]
    public void Remove_DefaultTypedKey_ThrowsArgumentNullException()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        ContextKey<Order> key = default;

        Assert.Throws<ArgumentNullException>(() => context.Remove(key));
    }

    // ---------------------------------------------------------------------
    // Keys
    // ---------------------------------------------------------------------

    [Fact]
    public void Keys_EmptyContext_IsEmpty()
    {
        BusinessRuleContext context = new BusinessRuleContext();

        Assert.Empty(context.Keys);
        Assert.Equal(0, context.Count);
    }

    [Fact]
    public void Keys_AfterSets_ListsEveryKeyName()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(OrderKey, new Order(1));
        context.Set(LimitKeyName, 10m);

        Assert.Equal(2, context.Keys.Count);
        Assert.Contains(OrderKeyName, context.Keys);
        Assert.Contains(LimitKeyName, context.Keys);
    }

    [Fact]
    public void Keys_IsLiveView_ReflectsRemoveAndClear()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(OrderKey, new Order(1));
        context.Set(LimitKey, 10m);
        IReadOnlyCollection<string> keys = context.Keys;

        context.Remove(OrderKey);
        Assert.Equal(new[] { LimitKeyName }, keys);

        context.Clear();
        Assert.Empty(keys);
    }

    [Fact]
    public void Keys_TypedContext_IncludesTheReservedTypedDataSlot()
    {
        BusinessRuleContext<Order> context = new BusinessRuleContext<Order>(new Order(1));

        Assert.Equal(new[] { "__typedData" }, context.Keys);
    }

    // ---------------------------------------------------------------------
    // String Get: descriptive exceptions
    // ---------------------------------------------------------------------

    [Fact]
    public void Get_MissingKey_ThrowsContextKeyNotFoundExceptionListingAvailableKeysSorted()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(OrderKeyName, new Order(1));
        context.Set(CustomerKeyName, "acme");
        context.Set(LimitKeyName, 1000m);

        ContextKeyNotFoundException exception = Assert.Throws<ContextKeyNotFoundException>(() => context.Get<Order>(MissingKeyName));

        Assert.Equal($"Context has no key '{MissingKeyName}'. Available keys: {CustomerKeyName}, {LimitKeyName}, {OrderKeyName}", exception.Message);
        Assert.Equal(MissingKeyName, exception.Key);
        Assert.Equal(new[] { CustomerKeyName, LimitKeyName, OrderKeyName }, exception.AvailableKeys);
    }

    [Fact]
    public void Get_MissingKeyOnEmptyContext_MessageSaysTheContextIsEmpty()
    {
        BusinessRuleContext context = new BusinessRuleContext();

        ContextKeyNotFoundException exception = Assert.Throws<ContextKeyNotFoundException>(() => context.Get<Order>(MissingKeyName));

        Assert.Equal($"Context has no key '{MissingKeyName}'. The context is empty.", exception.Message);
        Assert.Empty(exception.AvailableKeys);
    }

    [Fact]
    public void Get_MissingKey_ExceptionIsAKeyNotFoundException()
    {
        BusinessRuleContext context = new BusinessRuleContext();

        KeyNotFoundException exception = Assert.ThrowsAny<KeyNotFoundException>(() => context.Get<int>(MissingKeyName));

        Assert.IsType<ContextKeyNotFoundException>(exception);
    }

    [Fact]
    public void Get_WrongType_ThrowsContextValueTypeMismatchExceptionNamingBothTypes()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(OrderKeyName, 5);

        ContextValueTypeMismatchException exception = Assert.Throws<ContextValueTypeMismatchException>(() => context.Get<Order>(OrderKeyName));

        Assert.Equal($"Key '{OrderKeyName}' holds Int32, requested Order", exception.Message);
        Assert.Equal(OrderKeyName, exception.Key);
        Assert.Equal(typeof(int), exception.ActualType);
        Assert.Equal(typeof(Order), exception.RequestedType);
    }

    [Fact]
    public void Get_WrongType_ExceptionIsAnInvalidCastException()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(OrderKeyName, 5);

        InvalidCastException exception = Assert.ThrowsAny<InvalidCastException>(() => context.Get<Order>(OrderKeyName));

        Assert.IsType<ContextValueTypeMismatchException>(exception);
    }

    [Fact]
    public void Get_NullStoredForReferenceType_ReturnsNull()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set<Order?>(OrderKeyName, null);

        Order? order = context.Get<Order?>(OrderKeyName);

        Assert.Null(order);
    }

    [Fact]
    public void Get_NullStoredForNullableValueType_ReturnsNull()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set<decimal?>(LimitKeyName, null);

        decimal? limit = context.Get<decimal?>(LimitKeyName);

        Assert.Null(limit);
    }

    [Fact]
    public void Get_NullStoredForValueType_ThrowsContextValueTypeMismatchExceptionWithNullActualType()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set<Order?>(LimitKeyName, null);

        ContextValueTypeMismatchException exception = Assert.Throws<ContextValueTypeMismatchException>(() => context.Get<decimal>(LimitKeyName));

        Assert.Equal($"Key '{LimitKeyName}' holds null, requested Decimal", exception.Message);
        Assert.Null(exception.ActualType);
        Assert.Equal(typeof(decimal), exception.RequestedType);
    }

    [Fact]
    public void Get_BaseTypeRequested_ReturnsDerivedValue()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        Order order = new Order(3);
        context.Set(OrderKeyName, order);

        object asObject = context.Get<object>(OrderKeyName);
        IOrder asInterface = context.Get<IOrder>(OrderKeyName);

        Assert.Same(order, asObject);
        Assert.Same(order, asInterface);
    }

    [Fact]
    public void Get_NullKey_ThrowsArgumentNullException()
    {
        BusinessRuleContext context = new BusinessRuleContext();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => context.Get<int>(null!));

        Assert.Equal("key", exception.ParamName);
    }

    [Fact]
    public void Get_ExistingKeyAndType_StillWorksAsBefore()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set("count", 42);

        Assert.Equal(42, context.Get<int>("count"));
    }

    private interface IOrder
    {
        int Id { get; }
    }

    private sealed class Order : IOrder
    {
        public Order(int id)
        {
            Id = id;
        }

        public int Id { get; }
    }
}
