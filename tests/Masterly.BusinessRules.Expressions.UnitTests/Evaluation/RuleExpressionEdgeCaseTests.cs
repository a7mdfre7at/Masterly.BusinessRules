namespace Masterly.BusinessRules.Expressions.UnitTests;

/// <summary>
/// Floating-point edge values, short-circuiting lists, overflow and malformed literals, asserted on both the
/// interpreter and <see cref="RuleExpression.Compile{T}"/> so the two paths keep agreeing.
/// </summary>
public class RuleExpressionEdgeCaseTests
{
    private static BusinessRuleContext<Order> Typed(Order order) => new BusinessRuleContext<Order>(order);

    private static BusinessRuleContext<Order> WithValue(object value)
    {
        BusinessRuleContext<Order> context = Typed(new Order());
        context.Set("x", value);
        return context;
    }

    // ---------------------------------------------------------------------
    // NaN and infinities follow IEEE 754
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("x < 5", false)]
    [InlineData("x <= 5", false)]
    [InlineData("x > 5", false)]
    [InlineData("x >= 5", false)]
    [InlineData("x == x", false)]
    [InlineData("x != x", true)]
    [InlineData("x == 5", false)]
    [InlineData("x != 5", true)]
    [InlineData("x < x", false)]
    [InlineData("x >= x", false)]
    public void Evaluate_NaNOperand_FollowsIeeeSemantics(string text, bool expected)
    {
        RuleExpression expression = RuleExpression.Parse(text);
        BusinessRuleContext<Order> context = WithValue(double.NaN);

        Assert.Equal(expected, expression.Evaluate(context));
        Assert.Equal(expected, expression.Compile<Order>()(context));
    }

    [Theory]
    [InlineData("x < 5", false)]
    [InlineData("x == x", false)]
    [InlineData("x != x", true)]
    public void Evaluate_SingleNaNOperand_FollowsIeeeSemantics(string text, bool expected)
    {
        RuleExpression expression = RuleExpression.Parse(text);
        BusinessRuleContext<Order> context = WithValue(float.NaN);

        Assert.Equal(expected, expression.Evaluate(context));
        Assert.Equal(expected, expression.Compile<Order>()(context));
    }

    [Theory]
    [InlineData("x > 1000000", true)]
    [InlineData("x == x", true)]
    [InlineData("x >= x", true)]
    [InlineData("x < 0", false)]
    public void Evaluate_PositiveInfinity_OrdersAboveEveryNumber(string text, bool expected)
    {
        RuleExpression expression = RuleExpression.Parse(text);
        BusinessRuleContext<Order> context = WithValue(double.PositiveInfinity);

        Assert.Equal(expected, expression.Evaluate(context));
        Assert.Equal(expected, expression.Compile<Order>()(context));
    }

    [Theory]
    [InlineData("x < -1000000", true)]
    [InlineData("x == x", true)]
    [InlineData("x > 0", false)]
    public void Evaluate_NegativeInfinity_OrdersBelowEveryNumber(string text, bool expected)
    {
        RuleExpression expression = RuleExpression.Parse(text);
        BusinessRuleContext<Order> context = WithValue(double.NegativeInfinity);

        Assert.Equal(expected, expression.Evaluate(context));
        Assert.Equal(expected, expression.Compile<Order>()(context));
    }

    [Fact]
    public void Evaluate_NaNInAList_IsNeverAMember()
    {
        RuleExpression expression = RuleExpression.Parse("x in [1, x, 3]");
        BusinessRuleContext<Order> context = WithValue(double.NaN);

        Assert.False(expression.Evaluate(context));
        Assert.False(expression.Compile<Order>()(context));
    }

    // ---------------------------------------------------------------------
    // 'in' lists short-circuit on both paths
    // ---------------------------------------------------------------------

    [Fact]
    public void Evaluate_InListMatchesBeforeAnUnboundItem_IsTrueOnBothPaths()
    {
        RuleExpression expression = RuleExpression.Parse("x in [100, missing]");
        BusinessRuleContext<Order> context = WithValue(100);

        Assert.True(expression.Evaluate(context));
        Assert.True(expression.Compile<Order>()(context));
    }

    [Fact]
    public void Evaluate_InListUnboundItemBeforeTheMatch_ThrowsOnBothPaths()
    {
        RuleExpression expression = RuleExpression.Parse("x in [missing, 100]");
        BusinessRuleContext<Order> context = WithValue(100);

        Assert.Throws<RuleExpressionBindingException>(() => expression.Evaluate(context));
        Assert.Throws<RuleExpressionBindingException>(() => expression.Compile<Order>()(context));
    }

    [Fact]
    public void Evaluate_InListWithoutAMatch_IsFalseOnBothPaths()
    {
        RuleExpression expression = RuleExpression.Parse("x in [1, 100]");
        BusinessRuleContext<Order> context = WithValue(5);

        Assert.False(expression.Evaluate(context));
        Assert.False(expression.Compile<Order>()(context));
    }

    [Fact]
    public void Evaluate_EmptyInList_IsFalseOnBothPaths()
    {
        RuleExpression expression = RuleExpression.Parse("x in []");
        BusinessRuleContext<Order> context = WithValue(5);

        Assert.False(expression.Evaluate(context));
        Assert.False(expression.Compile<Order>()(context));
    }

    [Fact]
    public void Evaluate_InListValueIsComputedOnce_OnBothPaths()
    {
        RuleExpression expression = RuleExpression.Parse("x + 1 in [1, 2, 6]");
        BusinessRuleContext<Order> context = WithValue(5);

        Assert.True(expression.Evaluate(context));
        Assert.True(expression.Compile<Order>()(context));
    }

    // ---------------------------------------------------------------------
    // Pinned verdicts of the compiled operators
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("x == 100", 100, true)]
    [InlineData("x == 100", 5, false)]
    [InlineData("x > 99.5 && x < 100.5", 100, true)]
    [InlineData("x > 99.5 && x < 100.5", 5, false)]
    [InlineData("x % 3 == 1", 100, true)]
    [InlineData("x % 3 == 1", 99, false)]
    [InlineData("x in [1, 100]", 100, true)]
    [InlineData("x in [1, 100]", 5, false)]
    [InlineData("x != 'abc'", 5, true)]
    [InlineData("x == 'abc'", 5, false)]
    [InlineData("!(x > 1000)", 5, true)]
    [InlineData("!(x > 1000)", 5000, false)]
    [InlineData("x > 1 || missing > 1", 5, true)]
    public void Compile_Operators_ProduceThePinnedVerdict(string text, int value, bool expected)
    {
        RuleExpression expression = RuleExpression.Parse(text);
        BusinessRuleContext<Order> context = WithValue(value);

        Assert.Equal(expected, expression.Compile<Order>()(context));
        Assert.Equal(expected, expression.Evaluate(context));
    }

    // ---------------------------------------------------------------------
    // Overflow and malformed literals
    // ---------------------------------------------------------------------

    [Fact]
    public void Evaluate_DecimalOverflow_ThrowsEvaluationExceptionWithTheNodeText()
    {
        RuleExpression expression = RuleExpression.Parse("x * 79228162514264337593543950335 > 0");
        BusinessRuleContext<Order> context = WithValue(2);

        RuleExpressionEvaluationException interpreted = Assert.Throws<RuleExpressionEvaluationException>(() => expression.Evaluate(context));
        RuleExpressionEvaluationException compiled = Assert.Throws<RuleExpressionEvaluationException>(() => expression.Compile<Order>()(context));

        Assert.Contains("*", interpreted.NodeText);
        Assert.IsType<OverflowException>(interpreted.InnerException);
        Assert.Equal(interpreted.NodeText, compiled.NodeText);
    }

    [Fact]
    public void Parse_NumberOutOfRange_ThrowsParseExceptionWithPosition()
    {
        RuleExpressionParseException error = Assert.Throws<RuleExpressionParseException>(() => RuleExpression.Parse("x > 99999999999999999999999999999999"));

        Assert.Contains("out of range", error.Message);
        Assert.Equal(1, error.Line);
        Assert.Equal(5, error.Column);
    }

    [Fact]
    public void Parse_TrailingBackslashInString_ThrowsUnterminatedStringLiteral()
    {
        RuleExpressionParseException error = Assert.Throws<RuleExpressionParseException>(() => RuleExpression.Parse("name == 'abc\\"));

        Assert.Contains("Unterminated string literal", error.Message);
    }

    [Fact]
    public void Compile_CountOnANonCollection_ThrowsParseException()
    {
        RuleExpressionParseException error = Assert.Throws<RuleExpressionParseException>(() => RuleExpression.Parse("Data.Total.Count > 0").Compile<Order>());

        Assert.Contains("Count", error.Message);
    }
}
