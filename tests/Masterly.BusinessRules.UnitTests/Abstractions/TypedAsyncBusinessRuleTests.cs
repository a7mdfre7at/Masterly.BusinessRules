using System.Globalization;

namespace Masterly.BusinessRules.UnitTests;

public class TypedAsyncBusinessRuleTests
{
    private const string Code = "EMAIL.DUPLICATE";
    private const string Message = "Email address is already in use.";
    private const string DomainKey = "domain";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------
    // Metadata defaults
    // ---------------------------------------------------------------------

    [Fact]
    public void BaseAsyncBusinessRuleOfT_HasDefaultMetadata()
    {
        EmailMustBeUniqueRule rule = new EmailMustBeUniqueRule("taken@example.com");

        Assert.Equal(Code, rule.Code);
        Assert.Equal(Message, rule.Message);
        Assert.Equal(RuleSeverity.Error, rule.Severity);
        Assert.Equal(nameof(EmailMustBeUniqueRule), rule.Name);
        Assert.Equal(string.Empty, rule.Description);
        Assert.Equal(string.Empty, rule.Category);
        Assert.Empty(rule.Tags);
    }

    [Fact]
    public void BaseAsyncBusinessRuleOfT_CanOverrideMetadata()
    {
        DomainRule rule = new DomainRule();

        Assert.Equal(RuleSeverity.Info, rule.Severity);
        Assert.Equal("Domain", rule.Name);
        Assert.Equal("Checks the email domain", rule.Description);
        Assert.Equal("Identity", rule.Category);
        Assert.Equal(new[] { "email" }, rule.Tags);
    }

    // ---------------------------------------------------------------------
    // IsBrokenAsync
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("taken@example.com", true)]
    [InlineData("free@example.com", false)]
    public async Task IsBrokenAsync_WithSubject_EvaluatesTheSubject(string email, bool expected)
    {
        EmailMustBeUniqueRule rule = new EmailMustBeUniqueRule("taken@example.com");

        bool isBroken = await rule.IsBrokenAsync(new User(email), new BusinessRuleContext(), TestToken);

        Assert.Equal(expected, isBroken);
    }

    [Fact]
    public async Task IsBrokenAsync_ReceivesSubjectAndContext()
    {
        DomainRule rule = new DomainRule();
        BusinessRuleContext context = RuleTestHelper.CreateContext((DomainKey, "example.com"));

        Assert.False(await rule.IsBrokenAsync(new User("a@example.com"), context, TestToken));
        Assert.True(await rule.IsBrokenAsync(new User("a@other.com"), context, TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_SameInstance_ServesManySubjects()
    {
        EmailMustBeUniqueRule rule = new EmailMustBeUniqueRule("taken@example.com");
        BusinessRuleContext context = new BusinessRuleContext();

        Assert.True(await rule.IsBrokenAsync(new User("taken@example.com"), context, TestToken));
        Assert.False(await rule.IsBrokenAsync(new User("free@example.com"), context, TestToken));
        Assert.True(await rule.IsBrokenAsync(new User("taken@example.com"), context, TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_StructSubject_IsSupported()
    {
        NegativeNumberRule rule = new NegativeNumberRule();

        Assert.True(await rule.IsBrokenAsync(-1, new BusinessRuleContext(), TestToken));
        Assert.False(await rule.IsBrokenAsync(0, new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_CancelledToken_PropagatesCancellation()
    {
        CancellingRule rule = new CancellingRule();
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rule.IsBrokenAsync(new User("a@b.c"), new BusinessRuleContext(), source.Token));
    }

    // ---------------------------------------------------------------------
    // EvaluateAsync / Describe
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAsync_WhenPassing_ReturnsNull()
    {
        EmailMustBeUniqueRule rule = new EmailMustBeUniqueRule("taken@example.com");

        BusinessRuleResult? result = await rule.EvaluateAsync(new User("free@example.com"), new BusinessRuleContext(), TestToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task EvaluateAsync_WhenBroken_ReturnsResultWithMetadata()
    {
        EmailMustBeUniqueRule rule = new EmailMustBeUniqueRule("taken@example.com");

        BusinessRuleResult? result = await rule.EvaluateAsync(new User("taken@example.com"), new BusinessRuleContext(), TestToken);

        Assert.NotNull(result);
        Assert.Equal(Code, result.Code);
        Assert.Equal(Message, result.Message);
        Assert.Equal(nameof(EmailMustBeUniqueRule), result.Name);
    }

    [Fact]
    public async Task EvaluateAsync_WhenBroken_CallsDescribeWithSubjectAndContext()
    {
        DescribingRule rule = new DescribingRule();
        BusinessRuleContext context = new BusinessRuleContext { FormatProvider = CultureInfo.InvariantCulture };

        BusinessRuleResult? result = await rule.EvaluateAsync(new User("x@y.z"), context, TestToken);

        Assert.NotNull(result);
        Assert.Same(context, rule.LastContext);
        Assert.Equal("x@y.z", rule.LastSubject!.Email);
        Assert.Equal("x@y.z", result.Actual);
        Assert.Equal("Email x@y.z is taken.", result.Detail);
    }

    [Fact]
    public async Task EvaluateAsync_WhenPassing_DoesNotCallDescribe()
    {
        DescribingRule rule = new DescribingRule();

        BusinessRuleResult? result = await rule.EvaluateAsync(new User(string.Empty), new BusinessRuleContext(), TestToken);

        Assert.Null(result);
        Assert.Equal(0, rule.DescribeCalls);
    }

    [Fact]
    public async Task EvaluateAsync_WhenDescribeThrows_DegradesToEmptyDetails()
    {
        ThrowingDescribeRule rule = new ThrowingDescribeRule();

        BusinessRuleResult? result = await rule.EvaluateAsync(new User("x"), new BusinessRuleContext(), TestToken);

        Assert.NotNull(result);
        Assert.Equal(Code, result.Code);
        Assert.Null(result.Detail);
    }

    // ---------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------

    private sealed class User
    {
        public User(string email) => Email = email;
        public string Email { get; }
    }

    private sealed class EmailMustBeUniqueRule : BaseAsyncBusinessRule<User>
    {
        private readonly string _takenEmail;
        public EmailMustBeUniqueRule(string takenEmail) => _takenEmail = takenEmail;
        public override string Code => TypedAsyncBusinessRuleTests.Code;
        public override string Message => TypedAsyncBusinessRuleTests.Message;

        public override Task<bool> IsBrokenAsync(User user, BusinessRuleContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(user.Email == _takenEmail);
    }

    private sealed class DomainRule : BaseAsyncBusinessRule<User>
    {
        public override string Code => "EMAIL.DOMAIN";
        public override string Message => "Email must belong to the allowed domain.";
        public override RuleSeverity Severity => RuleSeverity.Info;
        public override string Name => "Domain";
        public override string Description => "Checks the email domain";
        public override string Category => "Identity";
        public override IReadOnlyList<string> Tags => new[] { "email" };

        public override Task<bool> IsBrokenAsync(User user, BusinessRuleContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(!user.Email.EndsWith(context.Get<string>(DomainKey), StringComparison.Ordinal));
    }

    private sealed class NegativeNumberRule : BaseAsyncBusinessRule<int>
    {
        public override string Code => "NUMBER.NEGATIVE";
        public override string Message => "Number must not be negative.";

        public override Task<bool> IsBrokenAsync(int number, BusinessRuleContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(number < 0);
    }

    private sealed class CancellingRule : BaseAsyncBusinessRule<User>
    {
        public override string Code => "CANCEL";
        public override string Message => "Cancels.";

        public override async Task<bool> IsBrokenAsync(User user, BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            return true;
        }
    }

    private sealed class DescribingRule : BaseAsyncBusinessRule<User>
    {
        public int DescribeCalls { get; private set; }
        public User? LastSubject { get; private set; }
        public BusinessRuleContext? LastContext { get; private set; }
        public override string Code => TypedAsyncBusinessRuleTests.Code;
        public override string Message => TypedAsyncBusinessRuleTests.Message;

        public override Task<bool> IsBrokenAsync(User user, BusinessRuleContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(user.Email.Length > 0);

        protected override RuleResultDetails Describe(User user, BusinessRuleContext? context)
        {
            DescribeCalls++;
            LastSubject = user;
            LastContext = context;
            return RuleResultDetails.Detail($"Email {user.Email} is taken.").Actual(user.Email);
        }
    }

    private sealed class ThrowingDescribeRule : BaseAsyncBusinessRule<User>
    {
        public override string Code => TypedAsyncBusinessRuleTests.Code;
        public override string Message => TypedAsyncBusinessRuleTests.Message;

        public override Task<bool> IsBrokenAsync(User user, BusinessRuleContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        protected override RuleResultDetails Describe(User user, BusinessRuleContext? context) => throw new InvalidOperationException("boom");
    }
}
