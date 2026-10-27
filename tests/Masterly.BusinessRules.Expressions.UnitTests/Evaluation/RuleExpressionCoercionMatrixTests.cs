namespace Masterly.BusinessRules.Expressions.UnitTests;

/// <summary>
/// Exhaustive matrices over the coercion table: every CLR numeric type against every other on both evaluation
/// paths (interpreter and <c>Compile&lt;T&gt;()</c>), and every comparison operator against null on either side.
/// </summary>
public class RuleExpressionCoercionMatrixTests
{
    private const string ValueKey = "value";
    private const string OtherKey = "other";
    private const string EqualOperator = "==";
    private const string NotEqualOperator = "!=";
    private const string EqualIgnoreCaseOperator = "~=";

    private static readonly string[] OrderingOperators = { "<", "<=", ">", ">=" };

    private static readonly Func<int, object>[] NumericFactories =
    {
        number => (sbyte)number,
        number => (byte)number,
        number => (short)number,
        number => (ushort)number,
        number => number,
        number => (uint)number,
        number => (long)number,
        number => (ulong)number,
        number => (float)number,
        number => (double)number,
        number => (decimal)number,
    };

    public static TheoryData<object, object> SameNumberPairs() => NumericPairs(7, 7);

    public static TheoryData<object, object> SmallerNumberPairs() => NumericPairs(7, 8);

    public static TheoryData<string, bool, bool, bool> NullOperatorCases()
    {
        TheoryData<string, bool, bool, bool> cases = new TheoryData<string, bool, bool, bool>();
        foreach (string equality in new[] { EqualOperator, EqualIgnoreCaseOperator })
        {
            cases.Add(equality, true, false, false);
            cases.Add(equality, false, true, false);
            cases.Add(equality, true, true, true);
        }

        cases.Add(NotEqualOperator, true, false, true);
        cases.Add(NotEqualOperator, false, true, true);
        cases.Add(NotEqualOperator, true, true, false);

        foreach (string ordering in OrderingOperators)
        {
            cases.Add(ordering, true, false, false);
            cases.Add(ordering, false, true, false);
            cases.Add(ordering, true, true, false);
        }

        return cases;
    }

    private static TheoryData<object, object> NumericPairs(int left, int right)
    {
        TheoryData<object, object> pairs = new TheoryData<object, object>();
        foreach (Func<int, object> leftFactory in NumericFactories)
        {
            foreach (Func<int, object> rightFactory in NumericFactories)
            {
                pairs.Add(leftFactory(left), rightFactory(right));
            }
        }

        return pairs;
    }

    private static BusinessRuleContext<Order> Context(object? value, object? other)
    {
        BusinessRuleContext<Order> context = new BusinessRuleContext<Order>(TestOrders.Standard());
        context.Set(ValueKey, value!);
        context.Set(OtherKey, other!);
        return context;
    }

    // ---------------------------------------------------------------------
    // Numeric promotion: every type against every type
    // ---------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(SameNumberPairs))]
    public void NumericMatrix_SameValueAcrossTypes_IsEqual(object value, object other)
    {
        RuleExpression expression = RuleExpression.Parse("value == other && !(value != other) && value ~= other && value <= other && value >= other");

        bool result = expression.Evaluate(Context(value, other));

        Assert.True(result);
    }

    [Theory]
    [MemberData(nameof(SmallerNumberPairs))]
    public void NumericMatrix_SmallerValueAcrossTypes_OrdersBelow(object value, object other)
    {
        RuleExpression expression = RuleExpression.Parse("value < other && value <= other && !(value > other) && !(value >= other) && value != other");

        bool result = expression.Evaluate(Context(value, other));

        Assert.True(result);
    }

    [Theory]
    [MemberData(nameof(SameNumberPairs))]
    public void NumericMatrix_ArithmeticAcrossTypes_PromotesBeforeOperating(object value, object other)
    {
        RuleExpression expression = RuleExpression.Parse("value + other == 14 && value - other == 0 && value * other == 49 && value / other == 1 && value % other == 0");

        bool result = expression.Evaluate(Context(value, other));

        Assert.True(result);
    }

    [Theory]
    [MemberData(nameof(SameNumberPairs))]
    public void NumericMatrix_InListAcrossTypes_UsesPromotedEquality(object value, object other)
    {
        RuleExpression expression = RuleExpression.Parse("value in [1, other, 100] && other in [value]");

        bool result = expression.Evaluate(Context(value, other));

        Assert.True(result);
    }

    [Theory]
    [MemberData(nameof(SmallerNumberPairs))]
    public void NumericMatrix_CompiledPath_AgreesWithTheInterpreter(object value, object other)
    {
        RuleExpression expression = RuleExpression.Parse("value < other && value + 1 == other && value in [other - 1]");
        BusinessRuleContext<Order> context = Context(value, other);

        Func<BusinessRuleContext<Order>, bool> compiled = expression.Compile<Order>();

        Assert.True(expression.Evaluate(context));
        Assert.True(compiled(context));
    }

    // ---------------------------------------------------------------------
    // Null semantics: every comparison operator, null on either side
    // ---------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(NullOperatorCases))]
    public void NullMatrix_ComparisonsWithNull_AreFalseExceptEquality(string op, bool leftIsNull, bool rightIsNull, bool expected)
    {
        RuleExpression expression = RuleExpression.Parse($"value {op} other");
        BusinessRuleContext<Order> context = Context(leftIsNull ? null : 5, rightIsNull ? null : 5);

        bool result = expression.Evaluate(context);

        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(NullOperatorCases))]
    public void NullMatrix_CompiledPath_AgreesWithTheInterpreter(string op, bool leftIsNull, bool rightIsNull, bool expected)
    {
        RuleExpression expression = RuleExpression.Parse($"value {op} other");
        BusinessRuleContext<Order> context = Context(leftIsNull ? null : "abc", rightIsNull ? null : "abc");

        Func<BusinessRuleContext<Order>, bool> compiled = expression.Compile<Order>();

        Assert.Equal(expected, compiled(context));
        Assert.Equal(expected, expression.Evaluate(context));
    }

    [Theory]
    [MemberData(nameof(NullOperatorCases))]
    public void NullMatrix_NullSegmentPath_BehavesLikeANullValue(string op, bool leftIsNull, bool rightIsNull, bool expected)
    {
        string left = leftIsNull ? "Data.Customer.Address.City" : "'Amman'";
        string right = rightIsNull ? "Data.Customer.Address.City" : "'Amman'";
        RuleExpression expression = RuleExpression.Parse($"{left} {op} {right}");
        BusinessRuleContext<Order> context = new BusinessRuleContext<Order>(TestOrders.Empty());

        bool interpreted = expression.Evaluate(context);
        bool compiled = expression.Compile<Order>()(context);

        Assert.Equal(expected, interpreted);
        Assert.Equal(expected, compiled);
    }

    [Theory]
    [InlineData("value + other == null")]
    [InlineData("value - other == null")]
    [InlineData("value * other == null")]
    [InlineData("value / other == null")]
    [InlineData("value % other == null")]
    [InlineData("-other == null")]
    public void NullMatrix_ArithmeticWithNull_YieldsNull(string text)
    {
        RuleExpression expression = RuleExpression.Parse(text);
        BusinessRuleContext<Order> context = Context(5, null);

        bool result = expression.Evaluate(context);

        Assert.True(result);
    }
}
