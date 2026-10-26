using System.Globalization;

namespace Masterly.BusinessRules.UnitTests;

public class AsyncDescribeHookTests
{
    private const string Code = "EMAIL.DUPLICATE";
    private const string Message = "Email address is already in use";
    private const string Name = "UniqueEmail";
    private const string Category = "Identity";
    private const string EmailKey = "email";
    private const string Hint = "Sign in instead.";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private sealed class DuplicateEmailRule : BaseAsyncBusinessRule
    {
        private readonly bool _duplicate;

        public DuplicateEmailRule(bool duplicate) => _duplicate = duplicate;

        public int DescribeCalls { get; private set; }

        public BusinessRuleContext? LastContext { get; private set; }

        public override string Code => AsyncDescribeHookTests.Code;
        public override string Message => AsyncDescribeHookTests.Message;
        public override string Name => AsyncDescribeHookTests.Name;
        public override string Category => AsyncDescribeHookTests.Category;
        public override IReadOnlyList<string> Tags => new[] { "identity", "customer-facing" };

        public override Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(_duplicate);

        protected override RuleResultDetails Describe(BusinessRuleContext? context)
        {
            DescribeCalls++;
            LastContext = context;
            string email = context!.Get<string>(EmailKey); // intentional: async rules always receive a context
            return RuleResultDetails
                .Detail($"Email {email} is taken")
                .Actual(email)
                .Expected("an unused address")
                .Remediation(Hint)
                .With("field", EmailKey);
        }
    }

    private sealed class AmountRule : BaseAsyncBusinessRule
    {
        public override string Code => "AMOUNT";
        public override string Message => "Amount rule";

        public override Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        protected override RuleResultDetails Describe(BusinessRuleContext? context)
        {
            decimal amount = context!.Get<decimal>("amount"); // intentional: async rules always receive a context
            return RuleResultDetails.Detail($"Amount is {amount:N2}");
        }
    }

    private sealed class ThrowingDescribeRule : BaseAsyncBusinessRule
    {
        private readonly Exception _exception;

        public ThrowingDescribeRule(Exception exception) => _exception = exception;

        public override string Code => "THROWING";
        public override string Message => "Throwing describe";

        public override Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        protected override RuleResultDetails Describe(BusinessRuleContext? context) => throw _exception;
    }

    private sealed class NullDescribeRule : BaseAsyncBusinessRule
    {
        public override string Code => "NULL";
        public override string Message => "Null describe";

        public override Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        protected override RuleResultDetails Describe(BusinessRuleContext? context) => null!; // intentional: exercising the guard
    }

    private static BusinessRuleContext EmailContext() => RuleTestHelper.CreateContext((EmailKey, "a@b.c"));

    [Fact]
    public async Task EvaluateAsync_BrokenRule_CarriesMetadata()
    {
        DuplicateEmailRule rule = new DuplicateEmailRule(duplicate: true);

        BusinessRuleResult? result = await rule.EvaluateAsync(EmailContext(), TestToken);

        Assert.NotNull(result);
        Assert.Equal(Code, result!.Code);
        Assert.Equal(Message, result.Message);
        Assert.Equal(Name, result.Name);
        Assert.Equal(Category, result.Category);
        Assert.Equal(new[] { "identity", "customer-facing" }, result.Tags);
    }

    [Fact]
    public async Task EvaluateAsync_BrokenRule_CarriesDescribeDetails()
    {
        DuplicateEmailRule rule = new DuplicateEmailRule(duplicate: true);

        BusinessRuleResult? result = await rule.EvaluateAsync(EmailContext(), TestToken);

        Assert.NotNull(result);
        Assert.Equal("Email a@b.c is taken", result!.Detail);
        Assert.Equal("a@b.c", result.Actual);
        Assert.Equal("an unused address", result.Expected);
        Assert.Equal(Hint, result.Remediation);
        Assert.Equal(EmailKey, result.Data["field"]);
    }

    [Fact]
    public async Task EvaluateAsync_PassesContextToDescribe()
    {
        DuplicateEmailRule rule = new DuplicateEmailRule(duplicate: true);
        BusinessRuleContext context = EmailContext();

        await rule.EvaluateAsync(context, TestToken);

        Assert.Equal(1, rule.DescribeCalls);
        Assert.Same(context, rule.LastContext);
    }

    [Fact]
    public async Task EvaluateAsync_PassingRule_ReturnsNullAndDoesNotCallDescribe()
    {
        DuplicateEmailRule rule = new DuplicateEmailRule(duplicate: false);

        BusinessRuleResult? result = await rule.EvaluateAsync(EmailContext(), TestToken);

        Assert.Null(result);
        Assert.Equal(0, rule.DescribeCalls);
    }

    [Fact]
    public async Task IsBrokenAsync_DoesNotCallDescribe()
    {
        DuplicateEmailRule rule = new DuplicateEmailRule(duplicate: true);

        await rule.IsBrokenAsync(EmailContext(), TestToken);

        Assert.Equal(0, rule.DescribeCalls);
    }

    [Fact]
    public async Task CheckAsync_PassingRule_DoesNotCallDescribe()
    {
        DuplicateEmailRule rule = new DuplicateEmailRule(duplicate: false);

        await rule.CheckAsync(EmailContext(), TestToken);

        Assert.Equal(0, rule.DescribeCalls);
    }

    [Fact]
    public async Task CheckAsync_BrokenRule_ExceptionCarriesDetails()
    {
        DuplicateEmailRule rule = new DuplicateEmailRule(duplicate: true);

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => rule.CheckAsync(EmailContext(), TestToken));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal("a@b.c", result.Actual);
        Assert.Equal(Name, result.Name);
    }

    [Fact]
    public async Task EvaluateAsync_ContextWithFormatProvider_FormatsDetailWithProvider()
    {
        AmountRule rule = new AmountRule();
        BusinessRuleContext context = RuleTestHelper.CreateContext(("amount", 1234.5m));
        context.FormatProvider = new CultureInfo("de-DE");

        BusinessRuleResult? result = await rule.EvaluateAsync(context, TestToken);

        Assert.Equal("Amount is 1.234,50", result!.Detail);
    }

    [Fact]
    public async Task EvaluateAsync_ContextWithoutFormatProvider_FormatsInvariant()
    {
        AmountRule rule = new AmountRule();
        BusinessRuleContext context = RuleTestHelper.CreateContext(("amount", 1234.5m));

        BusinessRuleResult? result = await rule.EvaluateAsync(context, TestToken);

        Assert.Equal("Amount is 1,234.50", result!.Detail);
    }

    [Fact]
    public async Task Describe_Throws_ResultIsBuiltWithoutDetails()
    {
        ThrowingDescribeRule rule = new ThrowingDescribeRule(new InvalidOperationException("boom"));

        BusinessRuleResult? result = await rule.EvaluateAsync(EmailContext(), TestToken);

        Assert.NotNull(result);
        Assert.Equal("THROWING", result!.Code);
        Assert.Null(result.Detail);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task Describe_Throws_CheckAsyncStillThrowsValidationException()
    {
        ThrowingDescribeRule rule = new ThrowingDescribeRule(new InvalidOperationException("boom"));

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => rule.CheckAsync(EmailContext(), TestToken));

        Assert.Equal("THROWING", Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task Describe_ThrowsOperationCanceled_Propagates()
    {
        ThrowingDescribeRule rule = new ThrowingDescribeRule(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => rule.EvaluateAsync(EmailContext(), TestToken));
    }

    [Fact]
    public async Task Describe_ReturnsNull_TreatedAsEmpty()
    {
        NullDescribeRule rule = new NullDescribeRule();

        BusinessRuleResult? result = await rule.EvaluateAsync(EmailContext(), TestToken);

        Assert.Null(result!.Detail);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task EvaluateAsync_CancelledToken_PropagatesBeforeDescribe()
    {
        DuplicateEmailRule rule = new DuplicateEmailRule(duplicate: true);
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(EmailContext(), new[] { rule }, cancellationToken: source.Token));

        Assert.Equal(0, rule.DescribeCalls);
    }

    [Fact]
    public async Task Checker_CarriesContextAwareDetails()
    {
        AmountRule rule = new AmountRule();
        BusinessRuleContext context = RuleTestHelper.CreateContext(("amount", 9m));

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(context, rule);

        Assert.Equal("Amount is 9.00", Assert.Single(result.BrokenRules).Detail);
    }
}
