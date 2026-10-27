namespace Masterly.BusinessRules.Expressions.UnitTests;

public class ExpressionBusinessRuleTests
{
    private const string Code = "ORDER.TOO_LARGE";
    private const string Message = "Order exceeds the customer's limit.";
    private const string AmountKey = "amount";
    private const string LimitName = "maxOrderAmount";
    private const string Text = "amount > @maxOrderAmount";

    private static readonly RuleExpression Expression = RuleExpression.Parse(Text);

    private static BusinessRuleContext Context(decimal amount, decimal limit = 10_000m) => RuleTestHelper.CreateContext((AmountKey, amount), (LimitName, limit));

    // ---------------------------------------------------------------------
    // Construction and metadata
    // ---------------------------------------------------------------------

    [Fact]
    public void ToRule_ReturnsRuleWithMetadata()
    {
        ExpressionBusinessRule rule = Expression.ToRule(Code, Message);

        Assert.Equal(Code, rule.Code);
        Assert.Equal(Message, rule.Message);
        Assert.Equal(RuleSeverity.Error, rule.Severity);
        Assert.Equal(nameof(ExpressionBusinessRule), rule.Name);
        Assert.Equal(Text, rule.Description);
        Assert.Equal(string.Empty, rule.Category);
        Assert.Empty(rule.Tags);
        Assert.Same(Expression, rule.Expression);
        Assert.Null(rule.Parameters);
    }

    [Fact]
    public void ToRule_WithSeverityAndProvider_KeepsBoth()
    {
        InMemoryRuleParameterProvider provider = RuleTestHelper.CreateParameterProvider(LimitName, 1m);

        ExpressionBusinessRule rule = Expression.ToRule(Code, Message, provider, RuleSeverity.Warning);

        Assert.Equal(RuleSeverity.Warning, rule.Severity);
        Assert.Same(provider, rule.Parameters);
    }

    [Fact]
    public void Constructor_NullExpression_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ExpressionBusinessRule(null!, Code, Message));
    }

    [Fact]
    public void Constructor_NullCode_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ExpressionBusinessRule(Expression, null!, Message));
    }

    [Fact]
    public void Constructor_NullMessage_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ExpressionBusinessRule(Expression, Code, null!));
    }

    // ---------------------------------------------------------------------
    // Evaluation
    // ---------------------------------------------------------------------

    [Fact]
    public void IsBroken_WithoutContext_ThrowsInvalidOperationException()
    {
        ExpressionBusinessRule rule = Expression.ToRule(Code, Message);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => rule.IsBroken());

        Assert.Equal(ExpressionBusinessRule.ContextRequiredMessage, error.Message);
    }

    [Fact]
    public void Check_WithoutContext_ThrowsInvalidOperationException()
    {
        ExpressionBusinessRule rule = Expression.ToRule(Code, Message);

        Assert.Throws<InvalidOperationException>(() => rule.Check());
    }

    [Fact]
    public void IsBroken_NullContext_ThrowsArgumentNullException()
    {
        ExpressionBusinessRule rule = Expression.ToRule(Code, Message);

        Assert.Throws<ArgumentNullException>(() => rule.IsBroken((BusinessRuleContext)null!));
    }

    [Fact]
    public void IsBroken_ConditionHolds_ReturnsTrue()
    {
        ExpressionBusinessRule rule = Expression.ToRule(Code, Message);

        RuleTestHelper.AssertBroken(rule, Context(12_000m));
    }

    [Fact]
    public void IsBroken_ConditionFails_ReturnsFalse()
    {
        ExpressionBusinessRule rule = Expression.ToRule(Code, Message);

        RuleTestHelper.AssertNotBroken(rule, Context(500m));
    }

    [Fact]
    public void IsBroken_UsesTheInjectedProvider()
    {
        ExpressionBusinessRule rule = Expression.ToRule(Code, Message, RuleTestHelper.CreateParameterProvider(LimitName, 100_000m));

        Assert.False(rule.IsBroken(Context(12_000m)));
    }

    [Fact]
    public void Check_Broken_ThrowsValidationExceptionWithCode()
    {
        ExpressionBusinessRule rule = Expression.ToRule(Code, Message);

        BusinessRuleValidationException error = Assert.Throws<BusinessRuleValidationException>(() => rule.Check(Context(12_000m)));

        BusinessRuleResult result = Assert.Single(error.BrokenRules);
        Assert.Equal(Code, result.Code);
        Assert.Equal(Message, result.Message);
    }

    [Fact]
    public void Check_Passing_DoesNotThrow()
    {
        ExpressionBusinessRule rule = Expression.ToRule(Code, Message);

        rule.Check(Context(500m));
    }

    [Fact]
    public void Evaluate_IsAFactoryCarryingMetadata()
    {
        ExpressionBusinessRule rule = Expression.ToRule(Code, Message, severity: RuleSeverity.Warning);

        BusinessRuleResult result = rule.Evaluate(Context(500m));

        Assert.Equal(Code, result.Code);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
    }

    [Fact]
    public void IsBroken_UnboundIdentifier_ThrowsBindingException()
    {
        ExpressionBusinessRule rule = Expression.ToRule(Code, Message);

        Assert.Throws<RuleExpressionBindingException>(() => rule.IsBroken(new BusinessRuleContext()));
    }

    // ---------------------------------------------------------------------
    // Explanation
    // ---------------------------------------------------------------------

    [Fact]
    public void ExplainExpression_RendersBoundValues()
    {
        ExpressionBusinessRule rule = Expression.ToRule(Code, Message);

        RuleExpressionExplanation why = rule.ExplainExpression(Context(12_000m));

        Assert.Equal("amount(12000) > @maxOrderAmount(10000) => true", why.Rendered);
    }

    [Fact]
    public void ExplainExpression_UsesTheInjectedProviderAndRedactor()
    {
        ExpressionBusinessRule rule = Expression.ToRule(Code, Message, RuleTestHelper.CreateParameterProvider(LimitName, 100_000m));

        RuleExpressionExplanation why = rule.ExplainExpression(Context(12_000m), (name, value) => name == AmountKey ? "***" : value?.ToString());

        Assert.Equal("amount(***) > @maxOrderAmount(100000) => false", why.Rendered);
    }

    [Fact]
    public void ExplainExpression_NullContext_ThrowsArgumentNullException()
    {
        ExpressionBusinessRule rule = Expression.ToRule(Code, Message);

        Assert.Throws<ArgumentNullException>(() => rule.ExplainExpression(null!));
    }

    // ---------------------------------------------------------------------
    // Integration with core
    // ---------------------------------------------------------------------

    [Fact]
    public void And_ForwardsTheContextToTheExpressionRule()
    {
        IBusinessRule combined = Expression.ToRule(Code, Message).And(RuleTestHelper.CreateBrokenRule());

        Assert.True(combined.IsBroken(Context(12_000m)));
        Assert.False(combined.IsBroken(Context(500m)));
    }

    [Fact]
    public void Checker_CheckAll_UsesTheContext()
    {
        IBusinessRule[] rules = { Expression.ToRule(Code, Message), RuleTestHelper.CreatePassingRule() };

        BusinessRuleValidationException error = Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.CheckAll(Context(12_000m), rules));

        Assert.Equal(Code, Assert.Single(error.BrokenRules).Code);
    }

    [Fact]
    public void Checker_EvaluateAll_RecordsPassAndBreak()
    {
        IBusinessRule[] rules = { Expression.ToRule(Code, Message), RuleTestHelper.CreatePassingRule() };

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rules, Context(500m));

        Assert.False(result.HasBrokenRules);
    }

    [Fact]
    public void Explain_CoreExtension_StillReturnsTheRuleTree()
    {
        ExpressionBusinessRule rule = Expression.ToRule(Code, Message);

        RuleExplanation tree = rule.Explain(Context(12_000m));

        Assert.Equal(RuleOutcome.Broken, tree.Outcome);
        Assert.Same(rule, tree.Rule);
        Assert.Equal(Code, tree.Code);
    }

    [Fact]
    public void WithSeverity_WrapsTheExpressionRule()
    {
        IBusinessRule softened = Expression.ToRule(Code, Message).WithSeverity(RuleSeverity.Warning);

        Assert.Equal(RuleSeverity.Warning, softened.Severity);
        Assert.True(softened.IsBroken(Context(12_000m)));
    }
}
