namespace Masterly.BusinessRules.Expressions.UnitTests;

/// <summary>
/// Pins the coercion table documented in the README grammar section: numeric promotion, enum/string/number
/// comparison, ordinal strings, null-safe paths, membership, Count/Length and the Data root.
/// </summary>
public class RuleExpressionCoercionTests
{
    private const string ValueKey = "value";
    private const string OtherKey = "other";

    private static BusinessRuleContext Context(object? value) => RuleTestHelper.CreateContext((ValueKey, value!));

    private static BusinessRuleContext Context(object? value, object? other) => RuleTestHelper.CreateContext((ValueKey, value!), (OtherKey, other!));

    private static bool Evaluate(string text, BusinessRuleContext context) => RuleExpression.Parse(text).Evaluate(context);

    public static TheoryData<object, string, bool> NumericPromotionCases() => new TheoryData<object, string, bool>
    {
        { 100, "value == 100", true },
        { 100L, "value == 100", true },
        { (short)100, "value == 100", true },
        { (byte)100, "value == 100", true },
        { (sbyte)100, "value == 100", true },
        { (ushort)100, "value == 100", true },
        { 100u, "value == 100", true },
        { 100ul, "value == 100", true },
        { 100m, "value == 100", true },
        { 100.0, "value == 100", true },
        { 100f, "value == 100", true },
        { 12.5, "value == 12.5", true },
        { 12.5f, "value == 12.5", true },
        { 12.5m, "value == 12.5", true },
        { 100, "value == 100.0", true },
        { 100, "value > 99.99", true },
        { 100, "value < 100.01", true },
        { 1, "value / 3 == 0.3333333333333333333333333333", true },
        { 1.0, "value / 4 == 0.25", true },
        { 1, "value / 4 == 0.25", true },
        { 7, "value % 3 == 1", true },
        { 7.0, "value % 3 == 1", true },
        { 2, "value * 2.5 == 5", true },
        { 2.5, "value * 2 == 5", true },
        { 10, "value - 2.5 == 7.5", true },
        { 10, "value + 1 > 10", true },
        { -5, "-value == 5", true },
        { -5.5, "-value == 5.5", true },
        { 0.1, "value + 0.2 == 0.3", false },
        { 0.1m, "value + 0.2 == 0.3", true },
        { int.MaxValue, "value + 1 == 2147483648", true },
        { long.MaxValue, "value > 1", true },
    };

    [Theory]
    [MemberData(nameof(NumericPromotionCases))]
    public void Numbers_PromoteToDecimalOrDouble(object value, string text, bool expected)
    {
        Assert.Equal(expected, Evaluate(text, Context(value)));
    }

    [Theory]
    [InlineData(1, 1.0, "value == other", true)]
    [InlineData(1, 1.5, "value < other", true)]
    [InlineData(2L, 1.5f, "value > other", true)]
    [InlineData(2, 3L, "value < other", true)]
    [InlineData(2.5, 2.5f, "value == other", true)]
    [InlineData(3, 3, "value >= other", true)]
    [InlineData(3, 4, "value >= other", false)]
    [InlineData(3, 4, "value != other", true)]
    public void Numbers_MixedRuntimeTypes_ComparePromoted(object value, object other, string text, bool expected)
    {
        Assert.Equal(expected, Evaluate(text, Context(value, other)));
    }

    [Fact]
    public void Numbers_DecimalRuntimeValues_PromoteToDecimal()
    {
        Assert.True(Evaluate("value + other == 0.3", Context(0.1m, 0.2m)));
    }

    [Fact]
    public void Numbers_DecimalDivisionByZero_ThrowsEvaluationException()
    {
        RuleExpressionEvaluationException error = Assert.Throws<RuleExpressionEvaluationException>(() => Evaluate("value / 0 == 1", Context(1)));

        Assert.Equal("value / 0", error.NodeText);
        Assert.IsType<DivideByZeroException>(error.InnerException);
    }

    [Fact]
    public void Numbers_DoubleDivisionByZero_IsInfinity()
    {
        Assert.True(Evaluate("value / 0 > 1000000", Context(1.0)));
    }

    // ---------------------------------------------------------------------
    // Enums
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(CustomerTier.Vip, "value == 'VIP'", true)]
    [InlineData(CustomerTier.Vip, "value == 'vip'", true)]
    [InlineData(CustomerTier.Vip, "value == 'Vip'", true)]
    [InlineData(CustomerTier.Vip, "value != 'Gold'", true)]
    [InlineData(CustomerTier.Vip, "value ~= 'VIP'", true)]
    [InlineData(CustomerTier.Gold, "value == 'VIP'", false)]
    [InlineData(CustomerTier.Gold, "'gold' == value", true)]
    [InlineData(CustomerTier.Gold, "value == 1", true)]
    [InlineData(CustomerTier.Gold, "value == 2", false)]
    [InlineData(CustomerTier.Vip, "value > 1", true)]
    [InlineData(CustomerTier.Standard, "value < 1", true)]
    [InlineData(CustomerTier.Vip, "value in ['gold', 'vip']", true)]
    [InlineData(CustomerTier.Standard, "value in ['gold', 'vip']", false)]
    [InlineData(CustomerTier.Vip, "value == 'Unknown'", false)]
    public void Enums_CompareToStringsByNameIgnoringCase_AndToNumbersByValue(CustomerTier value, string text, bool expected)
    {
        Assert.Equal(expected, Evaluate(text, Context(value)));
    }

    [Fact]
    public void Enums_SameType_CompareByValue()
    {
        Assert.True(Evaluate("value == other", Context(CustomerTier.Gold, CustomerTier.Gold)));
        Assert.True(Evaluate("value < other", Context(CustomerTier.Gold, CustomerTier.Vip)));
        Assert.False(Evaluate("value == other", Context(CustomerTier.Gold, CustomerTier.Vip)));
    }

    [Fact]
    public void Enums_DifferentTypes_AreNeverEqual()
    {
        Assert.False(Evaluate("value == other", Context(CustomerTier.Gold, DayOfWeek.Monday)));
    }

    [Fact]
    public void Enums_DifferentTypes_CannotBeOrdered()
    {
        Assert.Throws<RuleExpressionEvaluationException>(() => Evaluate("value < other", Context(CustomerTier.Gold, DayOfWeek.Monday)));
    }

    [Fact]
    public void Enums_OrderedAgainstString_ThrowsEvaluationException()
    {
        RuleExpressionEvaluationException error = Assert.Throws<RuleExpressionEvaluationException>(() => Evaluate("value > 'gold'", Context(CustomerTier.Vip)));

        Assert.Equal("value > 'gold'", error.NodeText);
    }

    // ---------------------------------------------------------------------
    // Strings
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("Alice", "value == 'Alice'", true)]
    [InlineData("Alice", "value == 'alice'", false)]
    [InlineData("Alice", "value ~= 'alice'", true)]
    [InlineData("Alice", "value ~= 'ALICE'", true)]
    [InlineData("Alice", "value ~= 'Bob'", false)]
    [InlineData("Alice", "value != 'alice'", true)]
    [InlineData("a", "value < 'b'", true)]
    [InlineData("B", "value < 'a'", true)]
    [InlineData("abc", "value >= 'abc'", true)]
    [InlineData("Alice", "value + ' Smith' == 'Alice Smith'", true)]
    [InlineData("Alice", "value.startsWith('A')", true)]
    [InlineData("Alice", "value.startsWith('a')", false)]
    [InlineData("Alice", "value.startsWith/ci('a')", true)]
    [InlineData("Alice", "value.endsWith('ice')", true)]
    [InlineData("Alice", "value.endsWith('ICE')", false)]
    [InlineData("Alice", "value.endsWith/ci('ICE')", true)]
    [InlineData("Alice", "value.contains('lic')", true)]
    [InlineData("Alice", "value.contains('LIC')", false)]
    [InlineData("Alice", "value.contains/ci('LIC')", true)]
    [InlineData("Alice", "value.Length == 5", true)]
    [InlineData("Alice", "value.Count == 5", true)]
    [InlineData("", "value.Length == 0", true)]
    [InlineData("Alice", "value in ['Bob', 'Alice']", true)]
    [InlineData("alice", "value in ['Bob', 'Alice']", false)]
    public void Strings_CompareOrdinally_UnlessCaseInsensitiveForms(string value, string text, bool expected)
    {
        Assert.Equal(expected, Evaluate(text, Context(value)));
    }

    [Fact]
    public void Strings_TildeEqualsOnNumbers_BehavesLikeEquals()
    {
        Assert.True(Evaluate("value ~= 100", Context(100)));
        Assert.False(Evaluate("value ~= 101", Context(100)));
    }

    [Fact]
    public void Strings_FunctionWithNonStringArgument_ThrowsEvaluationException()
    {
        RuleExpressionEvaluationException error = Assert.Throws<RuleExpressionEvaluationException>(() => Evaluate("value.startsWith(1)", Context("Alice")));

        Assert.Equal("value.startsWith(1)", error.NodeText);
    }

    [Fact]
    public void Strings_FunctionOnNumber_ThrowsEvaluationException()
    {
        Assert.Throws<RuleExpressionEvaluationException>(() => Evaluate("value.startsWith('1')", Context(100)));
    }

    [Fact]
    public void Strings_FunctionOnNullTarget_IsFalse()
    {
        Assert.False(Evaluate("value.startsWith('A')", Context(null)));
        Assert.False(Evaluate("value.contains('A')", Context(null)));
    }

    [Fact]
    public void Strings_FunctionWithNullArgument_IsFalse()
    {
        Assert.False(Evaluate("value.startsWith(other)", Context("Alice", null)));
    }

    // ---------------------------------------------------------------------
    // Type mismatches
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("value == 'abc'")]
    [InlineData("value == true")]
    [InlineData("value ~= 'abc'")]
    public void Mismatch_EqualityAcrossTypes_IsFalseNotAnError(string text)
    {
        Assert.False(Evaluate(text, Context(100)));
        Assert.True(Evaluate(text.Replace("==", "!=").Replace("~=", "!="), Context(100)));
    }

    [Fact]
    public void Mismatch_GuidAgainstString_IsFalse()
    {
        Guid id = Guid.NewGuid();

        Assert.False(Evaluate($"value == '{id}'", Context(id)));
        Assert.True(Evaluate("value == other", Context(id, id)));
    }

    [Theory]
    [InlineData("value < 'abc'", 100)]
    [InlineData("value > true", 100)]
    [InlineData("value + 1 == 2", "abc")]
    [InlineData("value - 'x' == 2", "abc")]
    [InlineData("value * 2 == 2", true)]
    [InlineData("-value == 1", "abc")]
    [InlineData("-value == 1", true)]
    [InlineData("!value", 5)]
    [InlineData("!value", "true")]
    [InlineData("value && true", 5)]
    [InlineData("true && value", 5)]
    [InlineData("value || false", "x")]
    [InlineData("value", 5)]
    [InlineData("value", "true")]
    [InlineData("value in 'abc'", "a")]
    [InlineData("value in 5", 5)]
    [InlineData("value in other", 1)]
    public void Mismatch_OperatorOnUnsupportedOperands_ThrowsEvaluationException(string text, object value)
    {
        RuleExpressionEvaluationException error = Assert.Throws<RuleExpressionEvaluationException>(() => Evaluate(text, Context(value, 5)));

        Assert.False(string.IsNullOrEmpty(error.NodeText));
        Assert.Contains(error.NodeText, error.Message);
    }

    [Fact]
    public void Mismatch_NullAsRootResult_ThrowsEvaluationException()
    {
        Assert.Throws<RuleExpressionEvaluationException>(() => Evaluate("value", Context(null)));
    }

    // ---------------------------------------------------------------------
    // Null safety
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("value.Customer.Address.City == null", true)]
    [InlineData("value.Customer.Address.City != null", false)]
    [InlineData("value.Customer.Address.City == 'Amman'", false)]
    [InlineData("value.Customer.Address.City != 'Amman'", true)]
    [InlineData("value.Customer.Address.City > 'a'", false)]
    [InlineData("value.Customer.Address.City < 'a'", false)]
    [InlineData("value.Customer.Address.City >= 'a'", false)]
    [InlineData("value.Customer.Address.City <= 'a'", false)]
    [InlineData("value.Customer.Address.City.Length == null", true)]
    [InlineData("value.Customer.Age + 1 == null", true)]
    [InlineData("value.Customer.Age + 1 > 0", false)]
    [InlineData("-value.Customer.Age == null", true)]
    [InlineData("value.Customer.Name.startsWith('A')", false)]
    [InlineData("value.Customer.Tier in ['Gold']", false)]
    [InlineData("value.Customer.Tier == null", true)]
    [InlineData("null == null", true)]
    [InlineData("null != null", false)]
    [InlineData("null > 1", false)]
    [InlineData("1 > null", false)]
    [InlineData("value.Customer in [null]", true)]
    public void NullPaths_YieldNull_AndComparisonsWithNullAreFalseExceptEquality(string text, bool expected)
    {
        Order order = TestOrders.Empty();

        Assert.Equal(expected, Evaluate(text, Context(order)));
    }

    [Fact]
    public void NullPaths_StoredNullContextValue_BindsAsNull()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set<string?>(ValueKey, null);

        Assert.True(Evaluate("value == null", context));
        Assert.False(Evaluate("value == 'x'", context));
    }

    [Fact]
    public void NullPaths_NullableValueType_WithValue_Compares()
    {
        Order order = TestOrders.Standard();

        Assert.True(Evaluate("value.Customer.Age > 18", Context(order)));
        Assert.True(Evaluate("value.Customer.Age == 34", Context(order)));
    }

    [Fact]
    public void NullPaths_InListWithNullItem_MatchesNull()
    {
        Assert.True(Evaluate("value in [1, null]", Context(null)));
        Assert.False(Evaluate("value in [1, 2]", Context(null)));
    }

    [Fact]
    public void NullPaths_InNullCollection_IsFalse()
    {
        Assert.False(Evaluate("value in other", Context(1, null)));
    }

    // ---------------------------------------------------------------------
    // Membership
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(2, "value in [1, 2, 3]", true)]
    [InlineData(2.0, "value in [1, 2, 3]", true)]
    [InlineData(4, "value in [1, 2, 3]", false)]
    [InlineData(2, "value in []", false)]
    [InlineData("x", "value in [1, 'x']", true)]
    [InlineData(1, "value in [1 + 0, 5]", true)]
    public void In_UsesEqualityRules(object value, string text, bool expected)
    {
        Assert.Equal(expected, Evaluate(text, Context(value)));
    }

    [Fact]
    public void In_CollectionFromContext_UsesEqualityRules()
    {
        BusinessRuleContext context = Context("GOLD", new List<string> { "GOLD", "VIP" });

        Assert.True(Evaluate("value in other", context));
        Assert.False(Evaluate("'gold' in other", context));
    }

    [Fact]
    public void In_ArrayOfNumbers_PromotesItems()
    {
        Assert.True(Evaluate("value in other", Context(2.0, new[] { 1, 2, 3 })));
    }

    [Fact]
    public void In_EnumCollectionAgainstString_ComparesByName()
    {
        Assert.True(Evaluate("'vip' in other", Context(null, new[] { CustomerTier.Gold, CustomerTier.Vip })));
    }

    [Fact]
    public void Contains_OnCollection_UsesEqualityRules()
    {
        BusinessRuleContext context = Context(new[] { "rush", "fragile" });

        Assert.True(Evaluate("value.contains('rush')", context));
        Assert.False(Evaluate("value.contains('RUSH')", context));
        Assert.True(Evaluate("value.contains/ci('RUSH')", context));
        Assert.False(Evaluate("value.contains('missing')", context));
    }

    // ---------------------------------------------------------------------
    // Count / Length / Data
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("value.Items.Count == 2")]
    [InlineData("value.Tags.Length == 2")]
    [InlineData("value.Tags.Count == 2")]
    [InlineData("value.Codes.Count == 3")]
    [InlineData("value.Customer.Name.Length == 5")]
    [InlineData("value.Customer.Name.Count == 5")]
    [InlineData("value.Items.Count * 2 == 4")]
    public void CountAndLength_WorkOnCollectionsAndStrings(string text)
    {
        Assert.True(Evaluate(text, Context(TestOrders.Standard())));
    }

    [Fact]
    public void Count_OnNonGenericCollection_UsesICollection()
    {
        System.Collections.ArrayList list = new System.Collections.ArrayList { 1, 2, 3 };

        Assert.True(Evaluate("value.Count == 3", Context(list)));
    }

    [Fact]
    public void Count_OnEnumerableWithoutCount_ThrowsBindingException()
    {
        IEnumerable<int> lazy = Enumerable.Range(1, 3).Where(_ => true);

        Assert.Throws<RuleExpressionBindingException>(() => Evaluate("value.Count == 3", Context(lazy)));
    }

    [Fact]
    public void Count_OnEmptyCollection_IsZero()
    {
        Assert.True(Evaluate("value.Items.Count == 0", Context(TestOrders.Empty())));
    }

    [Fact]
    public void Data_BindsToTypedContextData()
    {
        BusinessRuleContext<Order> context = new BusinessRuleContext<Order>(TestOrders.Standard());

        Assert.True(Evaluate("Data.Items.Count == 2", context));
        Assert.True(Evaluate("Data.Total > 10000", context));
        Assert.True(Evaluate("Data.Customer.Tier == 'gold'", context));
    }

    [Fact]
    public void Data_WithoutTypedData_FallsBackToKeyNamedData()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext(("Data", 5));

        Assert.True(Evaluate("Data == 5", context));
    }

    [Fact]
    public void Data_TypedDataWinsOverKeyNamedData()
    {
        BusinessRuleContext<Order> context = new BusinessRuleContext<Order>(TestOrders.Standard());
        context.Set("Data", 5);

        Assert.True(Evaluate("Data.Total == 12000", context));
    }

    [Fact]
    public void Data_Absent_ThrowsBindingException()
    {
        RuleExpressionBindingException error = Assert.Throws<RuleExpressionBindingException>(() => Evaluate("Data.Total > 1", new BusinessRuleContext()));

        Assert.Equal(RuleExpression.DataIdentifier, error.Identifier);
    }

    // ---------------------------------------------------------------------
    // Other value types
    // ---------------------------------------------------------------------

    [Fact]
    public void DateTimes_OrderThroughIComparable()
    {
        DateTime earlier = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime later = earlier.AddDays(1);

        Assert.True(Evaluate("value < other", Context(earlier, later)));
        Assert.True(Evaluate("value == other", Context(earlier, earlier)));
        Assert.False(Evaluate("value >= other", Context(earlier, later)));
    }

    [Fact]
    public void Booleans_CompareAndCombine()
    {
        Assert.True(Evaluate("value == true && other == false", Context(true, false)));
        Assert.True(Evaluate("value != other", Context(true, false)));
        Assert.True(Evaluate("value", Context(true)));
        Assert.False(Evaluate("!value", Context(true)));
    }

    [Fact]
    public void Logical_ShortCircuits_AndDoesNotEvaluateTheRightOperand()
    {
        BusinessRuleContext context = Context(false);

        Assert.False(Evaluate("value && missing > 1", context));
        Assert.True(Evaluate("!value || missing > 1", context));
    }

    [Fact]
    public void Logical_WithoutShortCircuit_EvaluatesTheRightOperand()
    {
        BusinessRuleContext context = Context(true);

        Assert.Throws<RuleExpressionBindingException>(() => Evaluate("value && missing > 1", context));
    }
}
