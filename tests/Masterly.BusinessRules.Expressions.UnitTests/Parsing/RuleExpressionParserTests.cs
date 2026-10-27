namespace Masterly.BusinessRules.Expressions.UnitTests;

public class RuleExpressionParserTests
{
    private const string AmountKey = "amount";
    private const string NameKey = "name";
    private const string FlagKey = "flag";
    private const string CiKey = "ci";

    private static BusinessRuleContext Context(decimal amount = 100m, string name = "Alice", bool flag = true)
        => RuleTestHelper.CreateContext((AmountKey, amount), (NameKey, name), (FlagKey, flag), (CiKey, 2));

    // ---------------------------------------------------------------------
    // Guards
    // ---------------------------------------------------------------------

    [Fact]
    public void Parse_NullText_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleExpression.Parse(null!));
    }

    [Fact]
    public void Parse_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleExpression.Parse("amount > 1", null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public void Parse_EmptyOrWhiteSpace_ThrowsParseException(string text)
    {
        RuleExpressionParseException error = Assert.Throws<RuleExpressionParseException>(() => RuleExpression.Parse(text));

        Assert.Contains("empty", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------
    // Valid grammar
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("amount > 50", true)]
    [InlineData("amount >= 100", true)]
    [InlineData("amount < 100", false)]
    [InlineData("amount <= 100", true)]
    [InlineData("amount == 100", true)]
    [InlineData("amount != 100", false)]
    [InlineData("1 + 2 * 3 == 7", true)]
    [InlineData("(1 + 2) * 3 == 9", true)]
    [InlineData("10 - 2 - 3 == 5", true)]
    [InlineData("10 - (2 - 3) == 11", true)]
    [InlineData("2 * 3 % 4 == 2", true)]
    [InlineData("-amount == -100", true)]
    [InlineData("- -amount == 100", true)]
    [InlineData("amount > 50 && flag", true)]
    [InlineData("amount > 500 || flag", true)]
    [InlineData("amount > 500 || !flag", false)]
    [InlineData("amount > 50 and flag", true)]
    [InlineData("amount > 500 or flag", true)]
    [InlineData("not flag", false)]
    [InlineData("NOT flag", false)]
    [InlineData("amount > 500 OR flag", true)]
    [InlineData("flag == TRUE", true)]
    [InlineData("flag != False", true)]
    [InlineData("name != NULL", true)]
    [InlineData("!amount == 1", true)]
    [InlineData("!(amount == 100)", false)]
    [InlineData("!flag || amount == 100", true)]
    [InlineData("amount in [50, 100]", true)]
    [InlineData("amount in [1, 2]", false)]
    [InlineData("amount IN [100]", true)]
    [InlineData("name in ['Alice', 'Bob']", true)]
    [InlineData("name == \"Alice\"", true)]
    [InlineData("name.startsWith('Al')", true)]
    [InlineData("name.StartsWith('al')", false)]
    [InlineData("name.startsWith/ci('al')", true)]
    [InlineData("name.endsWith('ce')", true)]
    [InlineData("name.contains('lic')", true)]
    [InlineData("name.contains/ci('LIC')", true)]
    [InlineData("name.Length / ci == 2.5", true)]
    [InlineData("name.Length == 5", true)]
    [InlineData("((amount)) == 100", true)]
    [InlineData("amount > 1\n  && flag", true)]
    public void Parse_ValidExpression_EvaluatesAsExpected(string text, bool expected)
    {
        RuleExpression expression = RuleExpression.Parse(text);

        bool result = expression.Evaluate(Context());

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("'it\\'s'", "it's")]
    [InlineData("\"say \\\"hi\\\"\"", "say \"hi\"")]
    [InlineData("'back\\\\slash'", "back\\slash")]
    [InlineData("'tab\\there'", "tab\there")]
    [InlineData("'line\\nbreak'", "line\nbreak")]
    [InlineData("'return\\r'", "return\r")]
    [InlineData("'quote \" inside single'", "quote \" inside single")]
    public void Parse_StringEscapes_Unescape(string literal, string expected)
    {
        RuleExpression expression = RuleExpression.Parse($"name == {literal}");

        Assert.True(expression.Evaluate(Context(name: expected)));
    }

    [Theory]
    [InlineData("12.50", 12.5)]
    [InlineData("0.5", 0.5)]
    [InlineData("1000000", 1000000)]
    [InlineData("007", 7)]
    public void Parse_NumberLiteral_ParsesInvariantDecimal(string literal, double value)
    {
        RuleExpression expression = RuleExpression.Parse($"amount == {literal}");

        Assert.True(expression.Evaluate(Context((decimal)value)));
    }

    [Fact]
    public void Parse_WordOperators_NormaliseToSymbols()
    {
        RuleExpression expression = RuleExpression.Parse("flag and not (amount > 500 or amount in [1])");

        RuleExpressionExplanation explanation = expression.Explain(Context());

        Assert.Equal("flag && !(amount > 500 || amount in [1])", explanation.Steps[^1].NodeText);
    }

    [Fact]
    public void Parse_Text_PreservesOriginalSource()
    {
        const string text = "amount  >   1 and flag";

        RuleExpression expression = RuleExpression.Parse(text);

        Assert.Equal(text, expression.Text);
        Assert.Equal(text, expression.ToString());
    }

    [Fact]
    public void Parse_Options_AreRetained()
    {
        RuleExpressionOptions options = RuleExpressionOptions.Default.WithMaxLength(100);

        RuleExpression expression = RuleExpression.Parse("amount > 1", options);

        Assert.Same(options, expression.Options);
    }

    [Fact]
    public void Parse_WithoutOptions_UsesDefault()
    {
        RuleExpression expression = RuleExpression.Parse("amount > 1");

        Assert.Same(RuleExpressionOptions.Default, expression.Options);
    }

    [Fact]
    public void Parse_CollectsIdentifiersAndParameterNames_InFirstAppearanceOrder()
    {
        RuleExpression expression = RuleExpression.Parse("amount > @max && @max < other.Total || Data.Items.Count == 0 && amount == @min");

        Assert.Equal(new[] { "amount", "other", "Data" }, expression.Identifiers);
        Assert.Equal(new[] { "max", "min" }, expression.ParameterNames);
    }

    [Fact]
    public void Parse_NoParameters_ParameterNamesIsEmpty()
    {
        RuleExpression expression = RuleExpression.Parse("amount > 1");

        Assert.Empty(expression.ParameterNames);
    }

    [Theory]
    [InlineData("amount", 1)]
    [InlineData("amount > 1", 2)]
    [InlineData("a.b.c > 1", 4)]
    [InlineData("a > 1 && b > 2", 3)]
    [InlineData("!(a > 1)", 3)]
    [InlineData("a in [1, 2]", 2)]
    public void Depth_ReportsLongestPath(string text, int expected)
    {
        RuleExpression expression = RuleExpression.Parse(text);

        Assert.Equal(expected, expression.Depth);
    }

    // ---------------------------------------------------------------------
    // Parse errors with positions
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("amount >", 1, 9)]
    [InlineData("amount > )", 1, 10)]
    [InlineData("(amount > 1", 1, 12)]
    [InlineData("amount = 1", 1, 8)]
    [InlineData("amount ~ 1", 1, 8)]
    [InlineData("amount & flag", 1, 8)]
    [InlineData("amount | flag", 1, 8)]
    [InlineData("'unterminated", 1, 1)]
    [InlineData("\"unterminated", 1, 1)]
    [InlineData("'bad \\q escape'", 1, 6)]
    [InlineData("amount in", 1, 10)]
    [InlineData("amount in [1, 2", 1, 16)]
    [InlineData("[1, 2]", 1, 1)]
    [InlineData("amount.foo(1)", 1, 8)]
    [InlineData("amount.", 1, 8)]
    [InlineData("1.2.3", 1, 5)]
    [InlineData("1.", 1, 3)]
    [InlineData("@", 1, 1)]
    [InlineData("@ name", 1, 1)]
    [InlineData("1abc", 1, 1)]
    [InlineData("amount == ==", 1, 11)]
    [InlineData("amount > 1 1", 1, 12)]
    [InlineData("amount # 1", 1, 8)]
    [InlineData("in", 1, 1)]
    [InlineData("amount > and", 1, 10)]
    [InlineData("name.startsWith/ci", 1, 19)]
    [InlineData("name.startsWith/ci.x('a')", 1, 19)]
    [InlineData("name.startsWith()", 1, 17)]
    [InlineData("name.startsWith('a', 'b')", 1, 20)]
    [InlineData("amount >\n  'x' >", 2, 7)]
    [InlineData("amount >\n\n  ]", 3, 3)]
    public void Parse_InvalidExpression_ReportsLineAndColumn(string text, int line, int column)
    {
        RuleExpressionParseException error = Assert.Throws<RuleExpressionParseException>(() => RuleExpression.Parse(text));

        Assert.Equal(line, error.Line);
        Assert.Equal(column, error.Column);
        Assert.Contains($"line {line}, column {column}", error.Message);
    }

    [Fact]
    public void Parse_UnknownFunction_NamesTheSupportedFunctions()
    {
        RuleExpressionParseException error = Assert.Throws<RuleExpressionParseException>(() => RuleExpression.Parse("name.trim()"));

        Assert.Contains("trim", error.Message);
        Assert.Contains("startsWith", error.Message);
        Assert.Contains("endsWith", error.Message);
        Assert.Contains("contains", error.Message);
    }

    // ---------------------------------------------------------------------
    // Limits
    // ---------------------------------------------------------------------

    [Fact]
    public void Parse_LongerThanMaxLength_ThrowsParseException()
    {
        RuleExpressionOptions options = RuleExpressionOptions.Default.WithMaxLength(8);

        RuleExpressionParseException error = Assert.Throws<RuleExpressionParseException>(() => RuleExpression.Parse("amount > 1", options));

        Assert.Equal(1, error.Line);
        Assert.Equal(9, error.Column);
        Assert.Contains("limit is 8", error.Message);
    }

    [Fact]
    public void Parse_ExactlyMaxLength_Succeeds()
    {
        RuleExpressionOptions options = RuleExpressionOptions.Default.WithMaxLength(10);

        RuleExpression expression = RuleExpression.Parse("amount > 1", options);

        Assert.Equal(10, expression.Text.Length);
    }

    [Fact]
    public void Parse_DeeperNestingThanMaxDepth_ThrowsParseException()
    {
        RuleExpressionOptions options = RuleExpressionOptions.Default.WithMaxDepth(2);

        RuleExpressionParseException error = Assert.Throws<RuleExpressionParseException>(() => RuleExpression.Parse("!!!flag", options));

        Assert.Equal(3, error.Column);
    }

    [Fact]
    public void Parse_TreeDeeperThanMaxDepth_ThrowsParseException()
    {
        RuleExpressionOptions options = RuleExpressionOptions.Default.WithMaxDepth(2);

        RuleExpressionParseException error = Assert.Throws<RuleExpressionParseException>(() => RuleExpression.Parse("a > 1 && b > 2", options));

        Assert.Contains("3 levels", error.Message);
    }

    [Fact]
    public void Parse_TreeAtMaxDepth_Succeeds()
    {
        RuleExpressionOptions options = RuleExpressionOptions.Default.WithMaxDepth(2);

        RuleExpression expression = RuleExpression.Parse("amount > 1", options);

        Assert.Equal(2, expression.Depth);
    }

    [Fact]
    public void Parse_ManyParentheses_WithinLimit_Succeeds()
    {
        string text = new string('(', 50) + "flag" + new string(')', 50);

        RuleExpression expression = RuleExpression.Parse(text);

        Assert.True(expression.Evaluate(Context()));
    }

    // ---------------------------------------------------------------------
    // TryParse
    // ---------------------------------------------------------------------

    [Fact]
    public void TryParse_ValidText_ReturnsTrueAndExpression()
    {
        bool parsed = RuleExpression.TryParse("amount > 1", null, out RuleExpression? expression);

        Assert.True(parsed);
        Assert.NotNull(expression);
        Assert.Same(RuleExpressionOptions.Default, expression!.Options);
    }

    [Fact]
    public void TryParse_InvalidText_ReturnsFalse()
    {
        bool parsed = RuleExpression.TryParse("amount >", null, out RuleExpression? expression);

        Assert.False(parsed);
        Assert.Null(expression);
    }

    [Fact]
    public void TryParse_NullText_ReturnsFalse()
    {
        bool parsed = RuleExpression.TryParse(null, null, out RuleExpression? expression);

        Assert.False(parsed);
        Assert.Null(expression);
    }

    [Fact]
    public void TryParse_WithOptions_UsesThem()
    {
        RuleExpressionOptions options = RuleExpressionOptions.Default.WithMaxLength(3);

        bool parsed = RuleExpression.TryParse("amount > 1", options, out RuleExpression? expression);

        Assert.False(parsed);
        Assert.Null(expression);
    }

    // ---------------------------------------------------------------------
    // Exception type
    // ---------------------------------------------------------------------

    [Fact]
    public void ParseException_ExposesLineAndColumn()
    {
        RuleExpressionParseException error = new RuleExpressionParseException("boom", 3, 7);

        Assert.Equal(3, error.Line);
        Assert.Equal(7, error.Column);
        Assert.Equal("boom (line 3, column 7)", error.Message);
    }
}
