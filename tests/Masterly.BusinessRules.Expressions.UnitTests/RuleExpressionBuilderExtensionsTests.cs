namespace Masterly.BusinessRules.Expressions.UnitTests;

public class RuleExpressionBuilderExtensionsTests
{
    private const string Code = "ORDER.TOO_LARGE";
    private const string Message = "Order exceeds the limit.";
    private const string AmountKey = "amount";
    private const string LimitName = "maxOrderAmount";
    private const string Text = "amount > @maxOrderAmount";
    private const decimal Fallback = 10_000m;
    private const string RuleName = "Order too large";
    private const string RuleDescription = "Orders above the customer limit need approval";
    private const string RuleCategory = "Orders";
    private const string Remediation = "Split the order";

    private static readonly RuleScope Acme = RuleScope.Tenant("acme");

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private static BusinessRuleContext Context(decimal amount) => RuleTestHelper.CreateContext((AmountKey, amount));

    // ---------------------------------------------------------------------
    // Guards
    // ---------------------------------------------------------------------

    [Fact]
    public void WhenExpression_NullBuilder_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((BusinessRuleBuilder)null!).WhenExpression(Text));
        Assert.Throws<ArgumentNullException>(() => ((BusinessRuleBuilder)null!).WhenExpression(RuleExpression.Parse(Text)));
        Assert.Throws<ArgumentNullException>(() => ((AsyncBusinessRuleBuilder)null!).WhenExpression(Text));
        Assert.Throws<ArgumentNullException>(() => ((AsyncBusinessRuleBuilder)null!).WhenExpression(RuleExpression.Parse(Text)));
    }

    [Fact]
    public void WhenExpression_NullText_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => BusinessRuleBuilder.Create(Code).WhenExpression((string)null!));
        Assert.Throws<ArgumentNullException>(() => AsyncBusinessRuleBuilder.Create(Code).WhenExpression((string)null!));
    }

    [Fact]
    public void WhenExpression_NullExpression_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => BusinessRuleBuilder.Create(Code).WhenExpression((RuleExpression)null!));
        Assert.Throws<ArgumentNullException>(() => AsyncBusinessRuleBuilder.Create(Code).WhenExpression((RuleExpression)null!));
    }

    [Fact]
    public void WhenExpression_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => BusinessRuleBuilder.Create(Code).WhenExpression(Text, null!));
        Assert.Throws<ArgumentNullException>(() => AsyncBusinessRuleBuilder.Create(Code).WhenExpression(Text, null!));
    }

    [Fact]
    public void WhenExpression_InvalidText_FailsAtBuilderTime()
    {
        Assert.Throws<RuleExpressionParseException>(() => BusinessRuleBuilder.Create(Code).WhenExpression("amount >"));
        Assert.Throws<RuleExpressionParseException>(() => AsyncBusinessRuleBuilder.Create(Code).WhenExpression("amount >"));
    }

    [Fact]
    public void WhenExpression_Options_AreApplied()
    {
        RuleExpressionOptions options = RuleExpressionOptions.Default.WithMaxLength(5);

        Assert.Throws<RuleExpressionParseException>(() => BusinessRuleBuilder.Create(Code).WhenExpression(Text, options));
        Assert.Throws<RuleExpressionParseException>(() => AsyncBusinessRuleBuilder.Create(Code).WhenExpression(Text, options));
    }

    [Fact]
    public void WhenExpression_ReturnsTheSameBuilderForChaining()
    {
        BusinessRuleBuilder builder = BusinessRuleBuilder.Create(Code);
        AsyncBusinessRuleBuilder asyncBuilder = AsyncBusinessRuleBuilder.Create(Code);

        Assert.Same(builder, builder.WhenExpression(Text));
        Assert.Same(asyncBuilder, asyncBuilder.WhenExpression(Text));
    }

    // ---------------------------------------------------------------------
    // Sync builder
    // ---------------------------------------------------------------------

    [Fact]
    public void WhenExpression_Sync_BuildsRuleWithMetadata()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WithMessage(Message).WithSeverity(RuleSeverity.Warning).WhenExpression(Text).Build();

        Assert.Equal(Code, rule.Code);
        Assert.Equal(Message, rule.Message);
        Assert.Equal(RuleSeverity.Warning, rule.Severity);
    }

    [Fact]
    public void WhenExpression_Sync_UndeclaredParameterFallsBackToContextBag()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WhenExpression(Text).Build();
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 12_000m), (LimitName, 10_000m));

        Assert.True(rule.IsBroken(context));
    }

    [Fact]
    public void WhenExpression_Sync_DeclaredParameterUsesFallback()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WithParameter(LimitName, Fallback).WhenExpression(Text).Build();

        Assert.True(rule.IsBroken(Context(12_000m)));
        Assert.False(rule.IsBroken(Context(500m)));
    }

    [Fact]
    public void WhenExpression_Sync_DeclaredParameterUsesProviderValue()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code)
            .WithParameters(RuleTestHelper.CreateParameterProvider(LimitName, 100_000m))
            .WithParameter(LimitName, Fallback)
            .WhenExpression(Text)
            .Build();

        Assert.False(rule.IsBroken(Context(12_000m)));
    }

    [Fact]
    public void WhenExpression_Sync_DeclaredParameterWinsOverContextBag()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WithParameter(LimitName, 100_000m).WhenExpression(Text).Build();
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 12_000m), (LimitName, 1m));

        Assert.False(rule.IsBroken(context));
    }

    [Fact]
    public void WhenExpression_Sync_DeclaredParameterResolvesPerScope()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code)
            .WithParameters(new InMemoryRuleParameterProvider().Set(LimitName, 1_000m).Set(LimitName, 100_000m, Acme))
            .WithParameter(LimitName, Fallback)
            .WhenExpression(Text)
            .Build();
        BusinessRuleContext acme = Context(12_000m);
        acme.Set(RuleParameters.ScopeKey, Acme);

        Assert.True(rule.IsBroken(Context(12_000m)));
        Assert.False(rule.IsBroken(acme));
    }

    [Fact]
    public void WhenExpression_Sync_ParsedExpressionOverload_Works()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WithParameter(LimitName, Fallback).WhenExpression(RuleExpression.Parse(Text)).Build();

        Assert.True(rule.IsBroken(Context(12_000m)));
    }

    [Fact]
    public void WhenExpression_Sync_ParameterlessIsBroken_ThrowsInvalidOperationException()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WhenExpression(Text).Build();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => rule.IsBroken());

        Assert.Equal(ExpressionBusinessRule.ContextRequiredMessage, error.Message);
    }

    [Fact]
    public void WhenExpression_Sync_CheckThrowsWithCode()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WithMessage(Message).WithParameter(LimitName, Fallback).WhenExpression(Text).Build();

        BusinessRuleValidationException error = Assert.Throws<BusinessRuleValidationException>(() => rule.Check(Context(12_000m)));

        Assert.Equal(Code, Assert.Single(error.BrokenRules).Code);
    }

    [Fact]
    public void WhenExpression_Sync_ContextProviderOverridesDeclaredProviderForDeclaredNames()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code)
            .WithParameters(RuleTestHelper.CreateParameterProvider(LimitName, 1m))
            .WithParameter(LimitName, Fallback)
            .WhenExpression(Text)
            .Build();
        BusinessRuleContext context = Context(12_000m);
        context.Set(RuleParameters.ContextKey, RuleTestHelper.CreateParameterProvider(LimitName, 100_000m));

        Assert.False(rule.IsBroken(context));
    }

    // ---------------------------------------------------------------------
    // Async builder
    // ---------------------------------------------------------------------

    [Fact]
    public async Task WhenExpression_Async_BuildsRuleWithMetadata()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WhenExpression(Text).Build();
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 12_000m), (LimitName, 10_000m));

        Assert.Equal(Code, rule.Code);
        Assert.Equal(Message, rule.Message);
        Assert.True(await rule.IsBrokenAsync(context, TestToken));
    }

    [Fact]
    public async Task WhenExpression_Async_DeclaredParameterUsesFallback()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WithParameter(LimitName, Fallback).WhenExpression(Text).Build();

        Assert.True(await rule.IsBrokenAsync(Context(12_000m), TestToken));
        Assert.False(await rule.IsBrokenAsync(Context(500m), TestToken));
    }

    [Fact]
    public async Task WhenExpression_Async_DeclaredParameterUsesAsyncProvider()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code)
            .WithParameters((IAsyncRuleParameterProvider)RuleTestHelper.CreateParameterProvider(LimitName, 100_000m))
            .WithParameter(LimitName, Fallback)
            .WhenExpression(Text)
            .Build();

        Assert.False(await rule.IsBrokenAsync(Context(12_000m), TestToken));
    }

    [Fact]
    public async Task WhenExpression_Async_DeclaredParameterUsesSyncProvider()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code)
            .WithParameters((IRuleParameterProvider)RuleTestHelper.CreateParameterProvider(LimitName, 100_000m))
            .WithParameter(LimitName, Fallback)
            .WhenExpression(Text)
            .Build();

        Assert.False(await rule.IsBrokenAsync(Context(12_000m), TestToken));
    }

    [Fact]
    public async Task WhenExpression_Async_DeclaredParameterResolvesPerScope()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code)
            .WithParameters((IAsyncRuleParameterProvider)new InMemoryRuleParameterProvider().Set(LimitName, 1_000m).Set(LimitName, 100_000m, Acme))
            .WithParameter(LimitName, Fallback)
            .WhenExpression(Text)
            .Build();
        BusinessRuleContext acme = Context(12_000m);
        acme.Set(RuleParameters.ScopeKey, Acme);

        Assert.True(await rule.IsBrokenAsync(Context(12_000m), TestToken));
        Assert.False(await rule.IsBrokenAsync(acme, TestToken));
    }

    [Fact]
    public async Task WhenExpression_Async_UndeclaredParameterFallsBackToContextBag()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WhenExpression(RuleExpression.Parse(Text)).Build();
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m), (LimitName, 10_000m));

        Assert.False(await rule.IsBrokenAsync(context, TestToken));
    }

    [Fact]
    public async Task WhenExpression_Async_UndeclaredParameterUsesAsyncContextKeyProvider()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WhenExpression(Text).Build();
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 12_000m), (LimitName, 1m));
        context.Set(RuleParameters.AsyncContextKey, RuleTestHelper.CreateParameterProvider(LimitName, 100_000m));

        Assert.False(await rule.IsBrokenAsync(context, TestToken));
    }

    [Fact]
    public async Task WhenExpression_Async_CancelledToken_ThrowsOperationCanceledException()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WhenExpression(Text).Build();
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rule.IsBrokenAsync(Context(12_000m), source.Token));
    }

    [Fact]
    public async Task WhenExpression_Async_CheckAsyncThrowsWithCode()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WithParameter(LimitName, Fallback).WhenExpression(Text).Build();

        BusinessRuleValidationException error = await Assert.ThrowsAsync<BusinessRuleValidationException>(() => rule.CheckAsync(Context(12_000m), TestToken));

        Assert.Equal(Code, Assert.Single(error.BrokenRules).Code);
    }

    [Fact]
    public async Task WhenExpression_Async_UnboundIdentifier_ThrowsBindingException()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WhenExpression(Text).Build();

        await Assert.ThrowsAsync<RuleExpressionBindingException>(() => rule.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    // ---------------------------------------------------------------------
    // Build() returns the expression rule types (core WithRuleFactory seam)
    // ---------------------------------------------------------------------

    [Fact]
    public void WhenExpression_Sync_Build_ReturnsExpressionBusinessRule()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WhenExpression(Text).Build();

        ExpressionBusinessRule expressionRule = Assert.IsType<ExpressionBusinessRule>(rule);
        Assert.Equal(Text, expressionRule.Expression.Text);
        Assert.Null(expressionRule.Parameters);
    }

    [Fact]
    public void WhenExpression_Async_Build_ReturnsExpressionAsyncBusinessRule()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WhenExpression(Text).Build();

        ExpressionAsyncBusinessRule expressionRule = Assert.IsType<ExpressionAsyncBusinessRule>(rule);
        Assert.Equal(Text, expressionRule.Expression.Text);
        Assert.Null(expressionRule.AsyncParameters);
        Assert.Null(expressionRule.Parameters);
    }

    [Fact]
    public void WhenExpression_Sync_Build_ForwardsEveryBuilderMetadataMember()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code)
            .WithMessage(Message)
            .WithSeverity(RuleSeverity.Warning)
            .WithName(RuleName)
            .WithDescription(RuleDescription)
            .WithCategory(RuleCategory)
            .WithTags("orders", "limits")
            .WhenExpression(Text)
            .Build();

        Assert.Equal(Code, rule.Code);
        Assert.Equal(Message, rule.Message);
        Assert.Equal(RuleSeverity.Warning, rule.Severity);
        Assert.Equal(RuleName, rule.Name);
        Assert.Equal(RuleDescription, rule.Description);
        Assert.Equal(RuleCategory, rule.Category);
        Assert.Equal(new[] { "orders", "limits" }, rule.Tags);
    }

    [Fact]
    public void WhenExpression_Async_Build_ForwardsEveryBuilderMetadataMember()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code)
            .WithMessage(Message)
            .WithSeverity(RuleSeverity.Info)
            .WithName(RuleName)
            .WithDescription(RuleDescription)
            .WithCategory(RuleCategory)
            .WithTags("orders")
            .WhenExpression(Text)
            .Build();

        Assert.Equal(Code, rule.Code);
        Assert.Equal(Message, rule.Message);
        Assert.Equal(RuleSeverity.Info, rule.Severity);
        Assert.Equal(RuleName, rule.Name);
        Assert.Equal(RuleDescription, rule.Description);
        Assert.Equal(RuleCategory, rule.Category);
        Assert.Equal(new[] { "orders" }, rule.Tags);
    }

    [Fact]
    public void WhenExpression_NoDescriptionOrName_DescriptionIsTheTextAndNameIsTheCode()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WhenExpression(Text).Build();
        IAsyncBusinessRule asyncRule = AsyncBusinessRuleBuilder.Create(Code).WhenExpression(Text).Build();

        Assert.Equal(Text, rule.Description);
        Assert.Equal(Code, rule.Name);
        Assert.Equal(Text, asyncRule.Description);
        Assert.Equal(Code, asyncRule.Name);
    }

    [Fact]
    public void WhenExpression_Sync_Evaluate_AttachesTheBuilderDetails()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code)
            .WithParameter(LimitName, Fallback)
            .WithActual(context => context.Get<decimal>(AmountKey))
            .WithRemediation(Remediation)
            .WhenExpression(Text)
            .Build();

        BusinessRuleValidationException error = Assert.Throws<BusinessRuleValidationException>(() => rule.Check(Context(12_000m)));

        BusinessRuleResult result = Assert.Single(error.BrokenRules);
        Assert.Equal(12_000m, result.Actual);
        Assert.Equal(Remediation, result.Remediation);
    }

    [Fact]
    public async Task WhenExpression_Async_EvaluateAsync_AttachesTheBuilderDetails()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code)
            .WithParameter(LimitName, Fallback)
            .WithActual(context => context.Get<decimal>(AmountKey))
            .WithRemediation(Remediation)
            .WhenExpression(Text)
            .Build();

        BusinessRuleResult? result = await rule.EvaluateAsync(Context(12_000m), TestToken);

        Assert.NotNull(result);
        Assert.Equal(12_000m, result!.Actual);
        Assert.Equal(Remediation, result.Remediation);
    }

    [Fact]
    public async Task WhenExpression_Async_EvaluateAsync_PassingRule_ReturnsNull()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WithParameter(LimitName, Fallback).WhenExpression(Text).Build();

        Assert.Null(await rule.EvaluateAsync(Context(500m), TestToken));
    }

    [Fact]
    public void WhenExpression_Sync_ExplainExpression_BindsTheDeclaredParameter()
    {
        ExpressionBusinessRule rule = Assert.IsType<ExpressionBusinessRule>(
            BusinessRuleBuilder.Create(Code).WithParameter(LimitName, Fallback).WhenExpression(Text).Build());

        RuleExpressionExplanation why = rule.ExplainExpression(Context(12_000m));

        Assert.True(why.Result);
        Assert.Equal("amount(12000) > @maxOrderAmount(10000) => true", why.Rendered);
    }

    [Fact]
    public async Task WhenExpression_Async_ExplainExpressionAsync_BindsTheDeclaredParameterThroughTheProvider()
    {
        ExpressionAsyncBusinessRule rule = Assert.IsType<ExpressionAsyncBusinessRule>(
            AsyncBusinessRuleBuilder.Create(Code)
                .WithParameters((IAsyncRuleParameterProvider)RuleTestHelper.CreateParameterProvider(LimitName, 20_000m))
                .WithParameter(LimitName, Fallback)
                .WhenExpression(Text)
                .Build());

        RuleExpressionExplanation why = await rule.ExplainExpressionAsync(Context(12_000m), cancellationToken: TestToken);

        Assert.False(why.Result);
        Assert.Equal("amount(12000) > @maxOrderAmount(20000) => false", why.Rendered);
    }

    [Fact]
    public void WhenExpression_Sync_NullContext_ThrowsArgumentNullException()
    {
        ExpressionBusinessRule rule = Assert.IsType<ExpressionBusinessRule>(BusinessRuleBuilder.Create(Code).WhenExpression(Text).Build());

        Assert.Throws<ArgumentNullException>(() => rule.IsBroken(null!));
        Assert.Throws<ArgumentNullException>(() => rule.ExplainExpression(null!));
    }

    [Fact]
    public async Task WhenExpression_Async_NullContext_ThrowsArgumentNullException()
    {
        ExpressionAsyncBusinessRule rule = Assert.IsType<ExpressionAsyncBusinessRule>(AsyncBusinessRuleBuilder.Create(Code).WhenExpression(Text).Build());

        await Assert.ThrowsAsync<ArgumentNullException>(() => rule.IsBrokenAsync(null!, TestToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => rule.ExplainExpressionAsync(null!, cancellationToken: TestToken));
    }

    [Fact]
    public async Task WhenExpression_Async_DeclaredParameter_CancelledToken_ThrowsOperationCanceledException()
    {
        ExpressionAsyncBusinessRule rule = Assert.IsType<ExpressionAsyncBusinessRule>(
            AsyncBusinessRuleBuilder.Create(Code).WithParameter(LimitName, Fallback).WhenExpression(Text).Build());
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rule.IsBrokenAsync(Context(12_000m), source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rule.ExplainExpressionAsync(Context(12_000m), cancellationToken: source.Token));
    }

    [Fact]
    public void WhenExpression_Sync_LaterWhen_ReplacesTheExpression()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WhenExpression(Text).When(() => true).Build();

        Assert.IsNotType<ExpressionBusinessRule>(rule);
        Assert.True(rule.IsBroken());
    }

    [Fact]
    public async Task WhenExpression_Async_LaterWhenAsync_ReplacesTheExpression()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WhenExpression(Text).WhenAsync(_ => Task.FromResult(true)).Build();

        Assert.IsNotType<ExpressionAsyncBusinessRule>(rule);
        Assert.True(await rule.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public void WhenExpression_Sync_UnderAnd_ForwardsTheContext()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WithParameter(LimitName, Fallback).WhenExpression(Text).Build();

        IBusinessRule composed = rule.And(RuleTestHelper.CreateBrokenRule());

        Assert.True(composed.IsBroken(Context(12_000m)));
        Assert.False(composed.IsBroken(Context(500m)));
    }
}
