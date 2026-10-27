namespace Masterly.BusinessRules.Expressions.UnitTests;

public class ExpressionAsyncBusinessRuleTests
{
    private const string Code = "ORDER.TOO_LARGE";
    private const string Message = "Order exceeds the customer's limit.";
    private const string AmountKey = "amount";
    private const string LimitName = "maxOrderAmount";
    private const string Text = "amount > @maxOrderAmount";

    private static readonly RuleExpression Expression = RuleExpression.Parse(Text);

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private static BusinessRuleContext Context(decimal amount, decimal limit = 10_000m) => RuleTestHelper.CreateContext((AmountKey, amount), (LimitName, limit));

    // ---------------------------------------------------------------------
    // Construction and metadata
    // ---------------------------------------------------------------------

    [Fact]
    public void ToAsyncRule_ReturnsRuleWithMetadata()
    {
        ExpressionAsyncBusinessRule rule = Expression.ToAsyncRule(Code, Message);

        Assert.Equal(Code, rule.Code);
        Assert.Equal(Message, rule.Message);
        Assert.Equal(RuleSeverity.Error, rule.Severity);
        Assert.Equal(nameof(ExpressionAsyncBusinessRule), rule.Name);
        Assert.Equal(Text, rule.Description);
        Assert.Equal(string.Empty, rule.Category);
        Assert.Empty(rule.Tags);
        Assert.Same(Expression, rule.Expression);
        Assert.Null(rule.AsyncParameters);
        Assert.Null(rule.Parameters);
    }

    [Fact]
    public void ToAsyncRule_WithProvidersAndSeverity_KeepsThem()
    {
        InMemoryRuleParameterProvider asyncProvider = RuleTestHelper.CreateParameterProvider(LimitName, 1m);
        InMemoryRuleParameterProvider syncProvider = RuleTestHelper.CreateParameterProvider(LimitName, 2m);

        ExpressionAsyncBusinessRule rule = Expression.ToAsyncRule(Code, Message, asyncProvider, syncProvider, RuleSeverity.Info);

        Assert.Same(asyncProvider, rule.AsyncParameters);
        Assert.Same(syncProvider, rule.Parameters);
        Assert.Equal(RuleSeverity.Info, rule.Severity);
    }

    [Fact]
    public void Constructor_NullExpression_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ExpressionAsyncBusinessRule(null!, Code, Message));
    }

    [Fact]
    public void Constructor_NullCode_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ExpressionAsyncBusinessRule(Expression, null!, Message));
    }

    [Fact]
    public void Constructor_NullMessage_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ExpressionAsyncBusinessRule(Expression, Code, null!));
    }

    // ---------------------------------------------------------------------
    // Evaluation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task IsBrokenAsync_ConditionHolds_ReturnsTrue()
    {
        ExpressionAsyncBusinessRule rule = Expression.ToAsyncRule(Code, Message);

        await RuleTestHelper.AssertBrokenAsync(rule, Context(12_000m), TestToken);
    }

    [Fact]
    public async Task IsBrokenAsync_ConditionFails_ReturnsFalse()
    {
        ExpressionAsyncBusinessRule rule = Expression.ToAsyncRule(Code, Message);

        await RuleTestHelper.AssertNotBrokenAsync(rule, Context(500m), TestToken);
    }

    [Fact]
    public async Task IsBrokenAsync_NullContext_ThrowsArgumentNullException()
    {
        ExpressionAsyncBusinessRule rule = Expression.ToAsyncRule(Code, Message);

        await Assert.ThrowsAsync<ArgumentNullException>(() => rule.IsBrokenAsync(null!, TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_AsyncProviderWinsOverSyncProvider()
    {
        ExpressionAsyncBusinessRule rule = Expression.ToAsyncRule(Code, Message, RuleTestHelper.CreateParameterProvider(LimitName, 100_000m), RuleTestHelper.CreateParameterProvider(LimitName, 1m));

        Assert.False(await rule.IsBrokenAsync(Context(12_000m), TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_SyncProviderUsedWhenNoAsyncProvider()
    {
        ExpressionAsyncBusinessRule rule = Expression.ToAsyncRule(Code, Message, null, RuleTestHelper.CreateParameterProvider(LimitName, 100_000m));

        Assert.False(await rule.IsBrokenAsync(Context(12_000m), TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        ExpressionAsyncBusinessRule rule = Expression.ToAsyncRule(Code, Message);
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rule.IsBrokenAsync(Context(12_000m), source.Token));
    }

    [Fact]
    public async Task CheckAsync_Broken_ThrowsValidationExceptionWithCode()
    {
        ExpressionAsyncBusinessRule rule = Expression.ToAsyncRule(Code, Message);

        BusinessRuleValidationException error = await Assert.ThrowsAsync<BusinessRuleValidationException>(() => rule.CheckAsync(Context(12_000m), TestToken));

        Assert.Equal(Code, Assert.Single(error.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckAsync_Passing_DoesNotThrow()
    {
        ExpressionAsyncBusinessRule rule = Expression.ToAsyncRule(Code, Message);

        await rule.CheckAsync(Context(500m), TestToken);
    }

    [Fact]
    public async Task EvaluateAsync_Broken_ReturnsResult()
    {
        ExpressionAsyncBusinessRule rule = Expression.ToAsyncRule(Code, Message, severity: RuleSeverity.Warning);

        BusinessRuleResult? result = await rule.EvaluateAsync(Context(12_000m), TestToken);

        Assert.NotNull(result);
        Assert.Equal(Code, result!.Code);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
    }

    [Fact]
    public async Task EvaluateAsync_Passing_ReturnsNull()
    {
        ExpressionAsyncBusinessRule rule = Expression.ToAsyncRule(Code, Message);

        Assert.Null(await rule.EvaluateAsync(Context(500m), TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_UnboundIdentifier_ThrowsBindingException()
    {
        ExpressionAsyncBusinessRule rule = Expression.ToAsyncRule(Code, Message);

        await Assert.ThrowsAsync<RuleExpressionBindingException>(() => rule.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    // ---------------------------------------------------------------------
    // Explanation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExplainExpressionAsync_RendersBoundValues()
    {
        ExpressionAsyncBusinessRule rule = Expression.ToAsyncRule(Code, Message);

        RuleExpressionExplanation why = await rule.ExplainExpressionAsync(Context(12_000m), cancellationToken: TestToken);

        Assert.Equal("amount(12000) > @maxOrderAmount(10000) => true", why.Rendered);
    }

    [Fact]
    public async Task ExplainExpressionAsync_UsesProvidersAndRedactor()
    {
        ExpressionAsyncBusinessRule rule = Expression.ToAsyncRule(Code, Message, RuleTestHelper.CreateParameterProvider(LimitName, 100_000m));

        RuleExpressionExplanation why = await rule.ExplainExpressionAsync(Context(12_000m), (name, value) => name == AmountKey ? "***" : value?.ToString(), TestToken);

        Assert.Equal("amount(***) > @maxOrderAmount(100000) => false", why.Rendered);
    }

    [Fact]
    public async Task ExplainExpressionAsync_NullContext_ThrowsArgumentNullException()
    {
        ExpressionAsyncBusinessRule rule = Expression.ToAsyncRule(Code, Message);

        await Assert.ThrowsAsync<ArgumentNullException>(() => rule.ExplainExpressionAsync(null!, cancellationToken: TestToken));
    }

    // ---------------------------------------------------------------------
    // Integration with core
    // ---------------------------------------------------------------------

    [Fact]
    public async Task And_ForwardsTheContextToTheExpressionRule()
    {
        IAsyncBusinessRule combined = Expression.ToAsyncRule(Code, Message).And(RuleTestHelper.CreateBrokenRule().ToAsync());

        Assert.True(await combined.IsBrokenAsync(Context(12_000m), TestToken));
        Assert.False(await combined.IsBrokenAsync(Context(500m), TestToken));
    }

    [Fact]
    public async Task Checker_CheckAllAsync_UsesTheContext()
    {
        IAsyncBusinessRule[] rules = { Expression.ToAsyncRule(Code, Message), RuleTestHelper.CreatePassingRule().ToAsync() };

        BusinessRuleValidationException error = await Assert.ThrowsAsync<BusinessRuleValidationException>(() => AsyncBusinessRuleChecker.CheckAllAsync(Context(12_000m), rules, cancellationToken: TestToken));

        Assert.Equal(Code, Assert.Single(error.BrokenRules).Code);
    }

    [Fact]
    public async Task ExplainAsync_CoreExtension_StillReturnsTheRuleTree()
    {
        ExpressionAsyncBusinessRule rule = Expression.ToAsyncRule(Code, Message);

        RuleExplanation tree = await rule.ExplainAsync(Context(12_000m), cancellationToken: TestToken);

        Assert.Equal(RuleOutcome.Broken, tree.Outcome);
    }
}
