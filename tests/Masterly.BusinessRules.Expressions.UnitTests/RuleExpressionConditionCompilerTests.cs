namespace Masterly.BusinessRules.Expressions.UnitTests;

public class RuleExpressionConditionCompilerTests
{
    private const string Code = "ORDER.TOO_LARGE";
    private const string AmountKey = "amount";
    private const string LimitName = "maxOrderAmount";
    private const string Text = "amount > @maxOrderAmount";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private static BusinessRuleContext Context(decimal amount, decimal limit = 10_000m) => RuleTestHelper.CreateContext((AmountKey, amount), (LimitName, limit));

    [Fact]
    public void Constructor_Default_UsesDefaultOptionsAndNoProviders()
    {
        RuleExpressionConditionCompiler compiler = new RuleExpressionConditionCompiler();

        Assert.Same(RuleExpressionOptions.Default, compiler.Options);
        Assert.Null(compiler.Parameters);
        Assert.Null(compiler.AsyncParameters);
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RuleExpressionConditionCompiler(null!));
    }

    [Fact]
    public void Constructor_KeepsOptionsAndProviders()
    {
        RuleExpressionOptions options = RuleExpressionOptions.Default.WithMaxLength(50);
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider();

        RuleExpressionConditionCompiler compiler = new RuleExpressionConditionCompiler(options, provider, provider);

        Assert.Same(options, compiler.Options);
        Assert.Same(provider, compiler.Parameters);
        Assert.Same(provider, compiler.AsyncParameters);
    }

    // ---------------------------------------------------------------------
    // Sync seam
    // ---------------------------------------------------------------------

    [Fact]
    public void Compile_ReturnsDelegateEvaluatingTheExpression()
    {
        Func<BusinessRuleContext, bool> condition = new RuleExpressionConditionCompiler().Compile(Text);

        Assert.True(condition(Context(12_000m)));
        Assert.False(condition(Context(500m)));
    }

    [Fact]
    public void Compile_NullExpression_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RuleExpressionConditionCompiler().Compile(null!));
    }

    [Fact]
    public void Compile_InvalidExpression_ThrowsParseException()
    {
        Assert.Throws<RuleExpressionParseException>(() => new RuleExpressionConditionCompiler().Compile("amount >"));
    }

    [Fact]
    public void Compile_UsesTheOptions()
    {
        RuleExpressionConditionCompiler compiler = new RuleExpressionConditionCompiler(RuleExpressionOptions.Default.WithMaxLength(5));

        Assert.Throws<RuleExpressionParseException>(() => compiler.Compile(Text));
    }

    [Fact]
    public void Compile_UsesTheInjectedProvider()
    {
        RuleExpressionConditionCompiler compiler = new RuleExpressionConditionCompiler(RuleExpressionOptions.Default, RuleTestHelper.CreateParameterProvider(LimitName, 100_000m));

        Assert.False(compiler.Compile(Text)(Context(12_000m)));
    }

    [Fact]
    public void When_WithCompiler_BuildsAWorkingRule()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).When(new RuleExpressionConditionCompiler(), Text).Build();

        Assert.True(rule.IsBroken(Context(12_000m)));
        Assert.False(rule.IsBroken(Context(500m)));
    }

    [Fact]
    public void When_WithCompiler_InvalidExpression_FailsAtBuilderTime()
    {
        BusinessRuleBuilder builder = BusinessRuleBuilder.Create(Code);

        Assert.Throws<RuleExpressionParseException>(() => builder.When(new RuleExpressionConditionCompiler(), "amount >"));
    }

    [Fact]
    public void When_WithCompiler_ParameterlessIsBroken_ThrowsBindingException()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).When(new RuleExpressionConditionCompiler(), Text).Build();

        Assert.Throws<RuleExpressionBindingException>(() => rule.IsBroken());
    }

    // ---------------------------------------------------------------------
    // Async seam
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CompileAsyncCondition_ReturnsDelegateEvaluatingTheExpression()
    {
        Func<BusinessRuleContext, CancellationToken, Task<bool>> condition = new RuleExpressionConditionCompiler().CompileAsyncCondition(Text);

        Assert.True(await condition(Context(12_000m), TestToken));
        Assert.False(await condition(Context(500m), TestToken));
    }

    [Fact]
    public void CompileAsyncCondition_InvalidExpression_ThrowsParseException()
    {
        Assert.Throws<RuleExpressionParseException>(() => new RuleExpressionConditionCompiler().CompileAsyncCondition("amount >"));
    }

    [Fact]
    public async Task CompileAsyncCondition_PrefersTheAsyncProvider()
    {
        RuleExpressionConditionCompiler compiler = new RuleExpressionConditionCompiler(
            RuleExpressionOptions.Default,
            RuleTestHelper.CreateParameterProvider(LimitName, 1m),
            RuleTestHelper.CreateParameterProvider(LimitName, 100_000m));

        Assert.False(await compiler.CompileAsyncCondition(Text)(Context(12_000m), TestToken));
    }

    [Fact]
    public async Task CompileAsyncCondition_CancelledToken_ThrowsOperationCanceledException()
    {
        Func<BusinessRuleContext, CancellationToken, Task<bool>> condition = new RuleExpressionConditionCompiler().CompileAsyncCondition(Text);
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => condition(Context(12_000m), source.Token));
    }

    [Fact]
    public async Task ExplicitAsyncCompile_IsTheAsyncDelegate()
    {
        IAsyncRuleConditionCompiler compiler = new RuleExpressionConditionCompiler();

        Func<BusinessRuleContext, CancellationToken, Task<bool>> condition = compiler.Compile(Text);

        Assert.True(await condition(Context(12_000m), TestToken));
    }

    [Fact]
    public async Task WhenAsync_WithCompiler_BuildsAWorkingRule()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WhenAsync(new RuleExpressionConditionCompiler(), Text).Build();

        Assert.True(await rule.IsBrokenAsync(Context(12_000m), TestToken));
        Assert.False(await rule.IsBrokenAsync(Context(500m), TestToken));
    }

    [Fact]
    public void WhenAsync_WithCompiler_InvalidExpression_FailsAtBuilderTime()
    {
        AsyncBusinessRuleBuilder builder = AsyncBusinessRuleBuilder.Create(Code);

        Assert.Throws<RuleExpressionParseException>(() => builder.WhenAsync(new RuleExpressionConditionCompiler(), "amount >"));
    }
}
