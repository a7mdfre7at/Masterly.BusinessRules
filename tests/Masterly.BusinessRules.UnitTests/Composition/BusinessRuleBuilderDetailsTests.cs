using System.Globalization;

namespace Masterly.BusinessRules.UnitTests;

public class BusinessRuleBuilderDetailsTests
{
    private const string Code = "LIMIT.EXCEEDED";
    private const string Message = "Transaction limit exceeded";
    private const string AmountKey = "amount";
    private const string LimitKey = "limit";
    private const string Hint = "Split the transaction.";

    private static BusinessRuleContext Context(decimal amount, decimal limit)
        => RuleTestHelper.CreateContext((AmountKey, amount), (LimitKey, limit));

    private static BusinessRuleBuilder BrokenBuilder()
        => BusinessRuleBuilder.Create(Code).WithMessage(Message).When(ctx => ctx.Get<decimal>(AmountKey) > ctx.Get<decimal>(LimitKey));

    [Fact]
    public void Build_WithAllDetailFunctions_ResultCarriesThem()
    {
        IBusinessRule rule = BrokenBuilder()
            .WithDetail(ctx => $"Amount {ctx.Get<decimal>(AmountKey):N2} exceeds limit {ctx.Get<decimal>(LimitKey):N2}")
            .WithActual(ctx => ctx.Get<decimal>(AmountKey))
            .WithExpected(ctx => $"<= {ctx.Get<decimal>(LimitKey)}")
            .WithRemediation(Hint)
            .Build();

        BusinessRuleResult result = rule.Evaluate(Context(5200m, 5000m));

        Assert.Equal("Amount 5,200.00 exceeds limit 5,000.00", result.Detail);
        Assert.Equal(5200m, result.Actual);
        Assert.Equal("<= 5000", result.Expected);
        Assert.Equal(Hint, result.Remediation);
        Assert.Empty(result.Data);
    }

    [Fact]
    public void Build_WithConstantExpectedAndRemediationFunction_ResultCarriesThem()
    {
        IBusinessRule rule = BrokenBuilder()
            .WithExpected(5000m)
            .WithRemediation(ctx => $"Reduce by {ctx.Get<decimal>(AmountKey) - ctx.Get<decimal>(LimitKey)}")
            .Build();

        BusinessRuleResult result = rule.Evaluate(Context(5200m, 5000m));

        Assert.Equal(5000m, result.Expected);
        Assert.Equal("Reduce by 200", result.Remediation);
        Assert.Null(result.Detail);
        Assert.Null(result.Actual);
    }

    [Fact]
    public void Build_WithoutDetailFunctions_ResultHasEmptyDetails()
    {
        IBusinessRule rule = BrokenBuilder().Build();

        BusinessRuleResult result = rule.Evaluate(Context(5200m, 5000m));

        Assert.Null(result.Detail);
        Assert.Null(result.Actual);
        Assert.Null(result.Expected);
        Assert.Null(result.Remediation);
        Assert.Empty(result.Data);
    }

    [Fact]
    public void Build_ResultCarriesMetadata()
    {
        IBusinessRule rule = BrokenBuilder().WithName("Limit").WithCategory("Payments").WithTags("customer-facing").Build();

        BusinessRuleResult result = rule.Evaluate(Context(5200m, 5000m));

        Assert.Equal("Limit", result.Name);
        Assert.Equal("Payments", result.Category);
        Assert.True(result.HasTag("customer-facing"));
    }

    [Fact]
    public void Build_WithoutName_ResultNameIsCode()
    {
        IBusinessRule rule = BrokenBuilder().Build();

        Assert.Equal(Code, rule.Evaluate().Name);
    }

    [Fact]
    public void Check_WithContext_ExceptionCarriesContextDetails()
    {
        IBusinessRule rule = BrokenBuilder().WithActual(ctx => ctx.Get<decimal>(AmountKey)).Build();

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => rule.Check(Context(5200m, 5000m)));

        Assert.Equal(5200m, Assert.Single(exception.BrokenRules).Actual);
    }

    [Fact]
    public void Check_PassingRule_DoesNotInvokeDetailFunctions()
    {
        int calls = 0;
        IBusinessRule rule = BrokenBuilder().WithActual(ctx => ++calls).Build();

        rule.Check(Context(100m, 5000m));

        Assert.Equal(0, calls);
    }

    [Fact]
    public void IsBroken_DoesNotInvokeDetailFunctions()
    {
        int calls = 0;
        IBusinessRule rule = BrokenBuilder().WithActual(ctx => ++calls).Build();

        rule.IsBroken(Context(5200m, 5000m));

        Assert.Equal(0, calls);
    }

    [Fact]
    public void Evaluate_Parameterless_RunsDetailFunctionsAgainstEmptyContext()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).When(() => true).WithExpected(ctx => ctx.Count).Build();

        BusinessRuleResult result = rule.Evaluate();

        Assert.Equal(0, result.Expected);
    }

    [Fact]
    public void Evaluate_Parameterless_ThrowingDetailFunction_DegradesToEmptyDetails()
    {
        IBusinessRule rule = BrokenBuilder()
            .WithDetail(ctx => $"Amount {ctx.Get<decimal>(AmountKey)}")
            .WithRemediation(Hint)
            .Build();

        BusinessRuleResult result = rule.Evaluate();

        Assert.Equal(Code, result.Code);
        Assert.Equal(Message, result.Message);
        Assert.Null(result.Detail);
        Assert.Null(result.Remediation);
    }

    [Fact]
    public void Check_ThrowingDetailFunction_StillThrowsValidationException()
    {
        IBusinessRule rule = BrokenBuilder().WithActual(ctx => throw new InvalidOperationException("boom")).Build();

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => rule.Check(Context(5200m, 5000m)));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(Code, result.Code);
        Assert.Null(result.Actual);
    }

    [Fact]
    public void Evaluate_OperationCanceledInDetailFunction_Propagates()
    {
        IBusinessRule rule = BrokenBuilder().WithActual(ctx => throw new OperationCanceledException()).Build();

        Assert.Throws<OperationCanceledException>(() => rule.Evaluate(Context(5200m, 5000m)));
    }

    [Fact]
    public void Evaluate_ContextWithFormatProvider_FormatsDetail()
    {
        IBusinessRule rule = BrokenBuilder().WithDetail(ctx => $"Amount {ctx.Get<decimal>(AmountKey):N2}").Build();
        BusinessRuleContext context = Context(5200m, 5000m);
        context.FormatProvider = new CultureInfo("de-DE");

        BusinessRuleResult result = rule.Evaluate(context);

        Assert.Equal("Amount 5.200,00", result.Detail);
    }

    [Fact]
    public void WithDetail_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => BusinessRuleBuilder.Create(Code).WithDetail(null!));
    }

    [Fact]
    public void WithActual_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => BusinessRuleBuilder.Create(Code).WithActual(null!));
    }

    [Fact]
    public void WithExpected_NullFunction_ThrowsArgumentNullException()
    {
        Func<BusinessRuleContext, object?> expected = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => BusinessRuleBuilder.Create(Code).WithExpected(expected));
    }

    [Fact]
    public void WithRemediation_NullString_ThrowsArgumentNullException()
    {
        string remediation = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => BusinessRuleBuilder.Create(Code).WithRemediation(remediation));
    }

    [Fact]
    public void WithRemediation_NullFunction_ThrowsArgumentNullException()
    {
        Func<BusinessRuleContext, string> remediation = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => BusinessRuleBuilder.Create(Code).WithRemediation(remediation));
    }

    [Fact]
    public void Checker_WithContext_CarriesBuilderDetails()
    {
        IBusinessRule rule = BrokenBuilder().WithActual(ctx => ctx.Get<decimal>(AmountKey)).WithCategory("Payments").Build();

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckAll(Context(5200m, 5000m), rule));

        BusinessRuleResult result = Assert.Single(exception.ByCategory("Payments"));
        Assert.Equal(5200m, result.Actual);
    }
}
