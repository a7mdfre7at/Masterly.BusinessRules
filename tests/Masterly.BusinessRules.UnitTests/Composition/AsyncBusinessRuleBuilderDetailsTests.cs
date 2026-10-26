using System.Globalization;

namespace Masterly.BusinessRules.UnitTests;

public class AsyncBusinessRuleBuilderDetailsTests
{
    private const string Code = "LIMIT.EXCEEDED";
    private const string Message = "Transaction limit exceeded";
    private const string AmountKey = "amount";
    private const string LimitKey = "limit";
    private const string Hint = "Split the transaction.";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private static BusinessRuleContext Context(decimal amount, decimal limit)
        => RuleTestHelper.CreateContext((AmountKey, amount), (LimitKey, limit));

    private static AsyncBusinessRuleBuilder BrokenBuilder()
        => AsyncBusinessRuleBuilder.Create(Code)
            .WithMessage(Message)
            .WhenAsync((ctx, _) => Task.FromResult(ctx.Get<decimal>(AmountKey) > ctx.Get<decimal>(LimitKey)));

    [Fact]
    public async Task Build_WithAllDetailFunctions_ResultCarriesThem()
    {
        IAsyncBusinessRule rule = BrokenBuilder()
            .WithDetail(ctx => $"Amount {ctx.Get<decimal>(AmountKey):N2} exceeds limit {ctx.Get<decimal>(LimitKey):N2}")
            .WithActual(ctx => ctx.Get<decimal>(AmountKey))
            .WithExpected(ctx => $"<= {ctx.Get<decimal>(LimitKey)}")
            .WithRemediation(Hint)
            .Build();

        BusinessRuleResult? result = await rule.EvaluateAsync(Context(5200m, 5000m), TestToken);

        Assert.NotNull(result);
        Assert.Equal("Amount 5,200.00 exceeds limit 5,000.00", result!.Detail);
        Assert.Equal(5200m, result.Actual);
        Assert.Equal("<= 5000", result.Expected);
        Assert.Equal(Hint, result.Remediation);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task Build_WithConstantExpectedAndRemediationFunction_ResultCarriesThem()
    {
        IAsyncBusinessRule rule = BrokenBuilder()
            .WithExpected(5000m)
            .WithRemediation(ctx => $"Reduce by {ctx.Get<decimal>(AmountKey) - ctx.Get<decimal>(LimitKey)}")
            .Build();

        BusinessRuleResult? result = await rule.EvaluateAsync(Context(5200m, 5000m), TestToken);

        Assert.Equal(5000m, result!.Expected);
        Assert.Equal("Reduce by 200", result.Remediation);
        Assert.Null(result.Detail);
        Assert.Null(result.Actual);
    }

    [Fact]
    public async Task Build_WithoutDetailFunctions_ResultHasEmptyDetails()
    {
        IAsyncBusinessRule rule = BrokenBuilder().Build();

        BusinessRuleResult? result = await rule.EvaluateAsync(Context(5200m, 5000m), TestToken);

        Assert.Null(result!.Detail);
        Assert.Null(result.Actual);
        Assert.Null(result.Expected);
        Assert.Null(result.Remediation);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task Build_ResultCarriesMetadata()
    {
        IAsyncBusinessRule rule = BrokenBuilder().WithName("Limit").WithCategory("Payments").WithTags("customer-facing").Build();

        BusinessRuleResult? result = await rule.EvaluateAsync(Context(5200m, 5000m), TestToken);

        Assert.Equal("Limit", result!.Name);
        Assert.Equal("Payments", result.Category);
        Assert.True(result.HasTag("customer-facing"));
    }

    [Fact]
    public async Task Build_WithoutName_ResultNameIsCode()
    {
        IAsyncBusinessRule rule = BrokenBuilder().Build();

        BusinessRuleResult? result = await rule.EvaluateAsync(Context(5200m, 5000m), TestToken);

        Assert.Equal(Code, result!.Name);
    }

    [Fact]
    public async Task EvaluateAsync_PassingRule_ReturnsNullAndDoesNotInvokeDetailFunctions()
    {
        int calls = 0;
        IAsyncBusinessRule rule = BrokenBuilder().WithActual(ctx => ++calls).Build();

        BusinessRuleResult? result = await rule.EvaluateAsync(Context(100m, 5000m), TestToken);

        Assert.Null(result);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task IsBrokenAsync_DoesNotInvokeDetailFunctions()
    {
        int calls = 0;
        IAsyncBusinessRule rule = BrokenBuilder().WithActual(ctx => ++calls).Build();

        await rule.IsBrokenAsync(Context(5200m, 5000m), TestToken);

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task CheckAsync_BrokenRule_ExceptionCarriesContextDetails()
    {
        IAsyncBusinessRule rule = BrokenBuilder().WithActual(ctx => ctx.Get<decimal>(AmountKey)).Build();

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => rule.CheckAsync(Context(5200m, 5000m), TestToken));

        Assert.Equal(5200m, Assert.Single(exception.BrokenRules).Actual);
    }

    [Fact]
    public async Task CheckAsync_ThrowingDetailFunction_StillThrowsValidationException()
    {
        IAsyncBusinessRule rule = BrokenBuilder().WithActual(ctx => throw new InvalidOperationException("boom")).Build();

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => rule.CheckAsync(Context(5200m, 5000m), TestToken));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(Code, result.Code);
        Assert.Null(result.Actual);
    }

    [Fact]
    public async Task EvaluateAsync_ThrowingDetailFunction_DegradesToEmptyDetails()
    {
        IAsyncBusinessRule rule = BrokenBuilder()
            .WithDetail(ctx => $"Missing {ctx.Get<decimal>("missing")}")
            .WithRemediation(Hint)
            .Build();

        BusinessRuleResult? result = await rule.EvaluateAsync(Context(5200m, 5000m), TestToken);

        Assert.Equal(Message, result!.Message);
        Assert.Null(result.Detail);
        Assert.Null(result.Remediation);
    }

    [Fact]
    public async Task EvaluateAsync_OperationCanceledInDetailFunction_Propagates()
    {
        IAsyncBusinessRule rule = BrokenBuilder().WithActual(ctx => throw new OperationCanceledException()).Build();

        await Assert.ThrowsAsync<OperationCanceledException>(() => rule.EvaluateAsync(Context(5200m, 5000m), TestToken));
    }

    [Fact]
    public async Task EvaluateAsync_CancelledToken_ConditionObservesItBeforeDetails()
    {
        int calls = 0;
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code)
            .WhenAsync((_, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(true);
            })
            .WithActual(ctx => ++calls)
            .Build();
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rule.EvaluateAsync(Context(1m, 0m), source.Token));

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task EvaluateAsync_ContextWithFormatProvider_FormatsDetail()
    {
        IAsyncBusinessRule rule = BrokenBuilder().WithDetail(ctx => $"Amount {ctx.Get<decimal>(AmountKey):N2}").Build();
        BusinessRuleContext context = Context(5200m, 5000m);
        context.FormatProvider = new CultureInfo("de-DE");

        BusinessRuleResult? result = await rule.EvaluateAsync(context, TestToken);

        Assert.Equal("Amount 5.200,00", result!.Detail);
    }

    [Fact]
    public void WithDetail_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AsyncBusinessRuleBuilder.Create(Code).WithDetail(null!));
    }

    [Fact]
    public void WithActual_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AsyncBusinessRuleBuilder.Create(Code).WithActual(null!));
    }

    [Fact]
    public void WithExpected_NullFunction_ThrowsArgumentNullException()
    {
        Func<BusinessRuleContext, object?> expected = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => AsyncBusinessRuleBuilder.Create(Code).WithExpected(expected));
    }

    [Fact]
    public void WithRemediation_NullString_ThrowsArgumentNullException()
    {
        string remediation = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => AsyncBusinessRuleBuilder.Create(Code).WithRemediation(remediation));
    }

    [Fact]
    public void WithRemediation_NullFunction_ThrowsArgumentNullException()
    {
        Func<BusinessRuleContext, string> remediation = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => AsyncBusinessRuleBuilder.Create(Code).WithRemediation(remediation));
    }

    [Fact]
    public async Task Checker_CarriesBuilderDetails()
    {
        IAsyncBusinessRule rule = BrokenBuilder().WithActual(ctx => ctx.Get<decimal>(AmountKey)).WithCategory("Payments").Build();

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(Context(5200m, 5000m), rule));

        BusinessRuleResult result = Assert.Single(exception.ByCategory("Payments"));
        Assert.Equal(5200m, result.Actual);
    }
}
