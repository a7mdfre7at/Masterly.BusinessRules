using System.Globalization;

namespace Masterly.BusinessRules.UnitTests;

public class DescribeHookTests
{
    private const string Code = "LIMIT.EXCEEDED";
    private const string Message = "Transaction limit exceeded";
    private const string Name = "TransactionLimit";
    private const string Category = "Payments";
    private const string AmountKey = "amount";
    private const string Hint = "Reduce the amount or request a limit increase.";

    private sealed class LimitRule : BaseBusinessRule
    {
        private readonly decimal _amount;
        private readonly decimal _limit;

        public LimitRule(decimal amount, decimal limit)
        {
            _amount = amount;
            _limit = limit;
        }

        public int DescribeCalls { get; private set; }

        public BusinessRuleContext? LastContext { get; private set; }

        public bool ContextWasNull { get; private set; }

        public override string Code => DescribeHookTests.Code;
        public override string Message => DescribeHookTests.Message;
        public override string Name => DescribeHookTests.Name;
        public override string Category => DescribeHookTests.Category;
        public override IReadOnlyList<string> Tags => new[] { "customer-facing" };

        public override bool IsBroken() => _amount > _limit;

        protected override RuleResultDetails Describe(BusinessRuleContext? context)
        {
            DescribeCalls++;
            LastContext = context;
            ContextWasNull = context == null;
            return RuleResultDetails
                .Detail($"Amount {_amount:N2} exceeds limit {_limit:N2}")
                .Actual(_amount)
                .Expected($"<= {_limit}")
                .Remediation(Hint)
                .With("currency", "USD");
        }
    }

    private sealed class ContextDescribeRule : BaseBusinessRule
    {
        public override string Code => "CTX";
        public override string Message => "Context rule";

        public override bool IsBroken(BusinessRuleContext context) => context.Get<decimal>(AmountKey) > 0m;

        public override bool IsBroken() => true;

        protected override RuleResultDetails Describe(BusinessRuleContext? context)
        {
            decimal amount = context!.Get<decimal>(AmountKey); // intentional: the test only calls the context overload
            return RuleResultDetails.Detail($"Amount is {amount:N2}").Actual(amount);
        }
    }

    private sealed class ThrowingDescribeRule : BaseBusinessRule
    {
        private readonly Exception _exception;

        public ThrowingDescribeRule(Exception exception) => _exception = exception;

        public override string Code => "THROWING";
        public override string Message => "Throwing describe";

        public override bool IsBroken() => true;

        protected override RuleResultDetails Describe(BusinessRuleContext? context) => throw _exception;
    }

    private sealed class NullDescribeRule : BaseBusinessRule
    {
        public override string Code => "NULL";
        public override string Message => "Null describe";

        public override bool IsBroken() => true;

        protected override RuleResultDetails Describe(BusinessRuleContext? context) => null!; // intentional: exercising the guard
    }

    private sealed class CustomEvaluateRule : BaseBusinessRule
    {
        public override string Code => "CUSTOM";
        public override string Message => "Custom";

        public override bool IsBroken() => true;

        public override BusinessRuleResult Evaluate() => new BusinessRuleResult(Code, "Overridden message", RuleSeverity.Warning);
    }

    [Fact]
    public void Evaluate_BrokenRule_CarriesMetadata()
    {
        LimitRule rule = new LimitRule(5200m, 5000m);

        BusinessRuleResult result = rule.Evaluate();

        Assert.Equal(Code, result.Code);
        Assert.Equal(Message, result.Message);
        Assert.Equal(RuleSeverity.Error, result.Severity);
        Assert.Equal(Name, result.Name);
        Assert.Equal(Category, result.Category);
        Assert.Equal(new[] { "customer-facing" }, result.Tags);
    }

    [Fact]
    public void Evaluate_BrokenRule_CarriesDescribeDetails()
    {
        LimitRule rule = new LimitRule(5200m, 5000m);

        BusinessRuleResult result = rule.Evaluate();

        Assert.Equal("Amount 5,200.00 exceeds limit 5,000.00", result.Detail);
        Assert.Equal(5200m, result.Actual);
        Assert.Equal("<= 5000", result.Expected);
        Assert.Equal(Hint, result.Remediation);
        Assert.Equal("USD", result.Data["currency"]);
        Assert.Empty(result.Details);
    }

    [Fact]
    public void Evaluate_Parameterless_PassesNullContextToDescribe()
    {
        LimitRule rule = new LimitRule(5200m, 5000m);

        rule.Evaluate();

        Assert.Equal(1, rule.DescribeCalls);
        Assert.True(rule.ContextWasNull);
    }

    [Fact]
    public void Evaluate_WithContext_PassesContextToDescribe()
    {
        LimitRule rule = new LimitRule(5200m, 5000m);
        BusinessRuleContext context = RuleTestHelper.CreateContext();

        rule.Evaluate(context);

        Assert.Same(context, rule.LastContext);
    }

    [Fact]
    public void Evaluate_WithContext_ReadsContextValues()
    {
        ContextDescribeRule rule = new ContextDescribeRule();
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 42.5m));

        BusinessRuleResult result = rule.Evaluate(context);

        Assert.Equal("Amount is 42.50", result.Detail);
        Assert.Equal(42.5m, result.Actual);
    }

    [Fact]
    public void Evaluate_ContextWithFormatProvider_FormatsDetailWithProvider()
    {
        ContextDescribeRule rule = new ContextDescribeRule();
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 1234.5m));
        context.FormatProvider = new CultureInfo("de-DE");

        BusinessRuleResult result = rule.Evaluate(context);

        Assert.Equal("Amount is 1.234,50", result.Detail);
    }

    [Fact]
    public void Evaluate_ContextWithoutFormatProvider_FormatsInvariant()
    {
        ContextDescribeRule rule = new ContextDescribeRule();
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 1234.5m));

        BusinessRuleResult result = rule.Evaluate(context);

        Assert.Equal("Amount is 1,234.50", result.Detail);
    }

    [Fact]
    public void IsBroken_DoesNotCallDescribe()
    {
        LimitRule rule = new LimitRule(5200m, 5000m);

        rule.IsBroken();
        rule.IsBroken(RuleTestHelper.CreateContext());

        Assert.Equal(0, rule.DescribeCalls);
    }

    [Fact]
    public void Check_PassingRule_DoesNotCallDescribe()
    {
        LimitRule rule = new LimitRule(100m, 5000m);

        rule.Check();
        rule.Check(RuleTestHelper.CreateContext());

        Assert.Equal(0, rule.DescribeCalls);
    }

    [Fact]
    public void Check_BrokenRule_ExceptionCarriesDetails()
    {
        LimitRule rule = new LimitRule(5200m, 5000m);

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => rule.Check());

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(5200m, result.Actual);
        Assert.Equal(Name, result.Name);
        Assert.Equal(1, rule.DescribeCalls);
    }

    [Fact]
    public void Check_WithContext_RoutesThroughContextOverload()
    {
        ContextDescribeRule rule = new ContextDescribeRule();
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 7m));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => rule.Check(context));

        Assert.Equal("Amount is 7.00", Assert.Single(exception.BrokenRules).Detail);
    }

    [Fact]
    public void Describe_Throws_ResultIsBuiltWithoutDetails()
    {
        ThrowingDescribeRule rule = new ThrowingDescribeRule(new InvalidOperationException("boom"));

        BusinessRuleResult result = rule.Evaluate();

        Assert.Equal("THROWING", result.Code);
        Assert.Equal("Throwing describe", result.Message);
        Assert.Null(result.Detail);
        Assert.Null(result.Actual);
        Assert.Empty(result.Data);
    }

    [Fact]
    public void Describe_Throws_CheckStillThrowsValidationException()
    {
        ThrowingDescribeRule rule = new ThrowingDescribeRule(new InvalidOperationException("boom"));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => rule.Check(RuleTestHelper.CreateContext()));

        Assert.Equal("THROWING", Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void Describe_ThrowsOperationCanceled_Propagates()
    {
        ThrowingDescribeRule rule = new ThrowingDescribeRule(new OperationCanceledException());

        Assert.Throws<OperationCanceledException>(() => rule.Evaluate());
    }

    [Fact]
    public void Describe_ReturnsNull_TreatedAsEmpty()
    {
        NullDescribeRule rule = new NullDescribeRule();

        BusinessRuleResult result = rule.Evaluate();

        Assert.Null(result.Detail);
        Assert.Empty(result.Data);
    }

    [Fact]
    public void Evaluate_IsVirtual_OverrideIsUsedByCheck()
    {
        CustomEvaluateRule rule = new CustomEvaluateRule();

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => rule.Check());

        Assert.Equal("Overridden message", Assert.Single(exception.BrokenRules).Message);
        Assert.Equal(RuleSeverity.Warning, Assert.Single(exception.BrokenRules).Severity);
    }

    [Fact]
    public void Evaluate_WithContext_IsIndependentOfParameterlessOverride()
    {
        IBusinessRule rule = new CustomEvaluateRule();

        BusinessRuleResult result = rule.Evaluate(RuleTestHelper.CreateContext());

        // BaseBusinessRule.Evaluate(context) is independent of Evaluate(); the override only affects the parameterless path.
        Assert.Equal("Custom", result.Message);
        Assert.Equal(RuleSeverity.Error, result.Severity);
    }

    [Fact]
    public void Checker_WithContext_CarriesContextAwareDetails()
    {
        ContextDescribeRule rule = new ContextDescribeRule();
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 9m));

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(context, rule);

        Assert.Equal("Amount is 9.00", Assert.Single(result.BrokenRules).Detail);
    }

    [Fact]
    public void Checker_WithoutContext_CallsDescribeWithNullContext()
    {
        LimitRule rule = new LimitRule(5200m, 5000m);

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rule);

        Assert.True(rule.ContextWasNull);
        Assert.Equal(5200m, Assert.Single(result.BrokenRules).Actual);
    }
}
