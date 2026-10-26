using System.Globalization;

namespace Masterly.BusinessRules.UnitTests;

public class BusinessRuleResultMetadataTests
{
    private const string Code = "LIMIT.EXCEEDED";
    private const string Message = "Transaction limit exceeded";
    private const string Name = "TransactionLimit";
    private const string Category = "Payments";
    private static readonly IReadOnlyList<string> Tags = new[] { "customer-facing", "Finance" };

    [Fact]
    public void Constructor_CodeMessageSeverity_DefaultsMetadataAndDetails()
    {
        BusinessRuleResult result = new BusinessRuleResult(Code, Message, RuleSeverity.Warning);

        Assert.Equal(Code, result.Name);
        Assert.Equal(string.Empty, result.Category);
        Assert.Empty(result.Tags);
        Assert.Null(result.Detail);
        Assert.Null(result.Actual);
        Assert.Null(result.Expected);
        Assert.Null(result.Remediation);
        Assert.Empty(result.Data);
        Assert.Empty(result.Details);
    }

    [Fact]
    public void Constructor_WithMemberDetails_DefaultsMetadata()
    {
        BusinessRuleResult member = new BusinessRuleResult("A", "A", RuleSeverity.Error);

        BusinessRuleResult result = new BusinessRuleResult(Code, Message, RuleSeverity.Error, new[] { member });

        Assert.Equal(Code, result.Name);
        Assert.Empty(result.Tags);
        Assert.Null(result.Detail);
        Assert.Same(member, Assert.Single(result.Details));
    }

    [Fact]
    public void Constructor_WithRuleResultDetails_PopulatesRichMembersWithInvariantFormatting()
    {
        decimal amount = 1234.5m;
        RuleResultDetails details = RuleResultDetails
            .Detail($"Amount {amount:N2}")
            .Actual(amount)
            .Expected("<= 1000")
            .Remediation("Split the transaction.")
            .With("currency", "USD");

        BusinessRuleResult result = new BusinessRuleResult(Code, Message, RuleSeverity.Error, details);

        Assert.Equal("Amount 1,234.50", result.Detail);
        Assert.Equal(amount, result.Actual);
        Assert.Equal("<= 1000", result.Expected);
        Assert.Equal("Split the transaction.", result.Remediation);
        Assert.Equal("USD", result.Data["currency"]);
        Assert.Equal(Code, result.Name);
        Assert.Empty(result.Details);
    }

    [Fact]
    public void Constructor_WithNullRuleResultDetails_ThrowsArgumentNullException()
    {
        RuleResultDetails details = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => new BusinessRuleResult(Code, Message, RuleSeverity.Error, details));
    }

    [Fact]
    public void Constructor_WithMetadata_PopulatesNameCategoryTags()
    {
        BusinessRuleResult result = new BusinessRuleResult(Code, Message, RuleSeverity.Info, Name, Category, Tags);

        Assert.Equal(Name, result.Name);
        Assert.Equal(Category, result.Category);
        Assert.Same(Tags, result.Tags);
        Assert.Null(result.Detail);
        Assert.Empty(result.Details);
    }

    [Fact]
    public void Constructor_WithMetadata_NullTags_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BusinessRuleResult(Code, Message, RuleSeverity.Info, Name, Category, null!));
    }

    [Fact]
    public void FullConstructor_FormatsDetailWithProvider()
    {
        decimal amount = 1234.5m;
        RuleResultDetails details = RuleResultDetails.Detail($"Amount {amount:N2}");

        BusinessRuleResult result = new BusinessRuleResult(
            Code, Message, RuleSeverity.Error, Name, Category, Tags, details, Array.Empty<BusinessRuleResult>(), new CultureInfo("de-DE"));

        Assert.Equal("Amount 1.234,50", result.Detail);
    }

    [Fact]
    public void FullConstructor_NullProvider_FormatsInvariant()
    {
        decimal amount = 1234.5m;
        RuleResultDetails details = RuleResultDetails.Detail($"Amount {amount:N2}");

        BusinessRuleResult result = new BusinessRuleResult(
            Code, Message, RuleSeverity.Error, Name, Category, Tags, details, Array.Empty<BusinessRuleResult>(), null);

        Assert.Equal("Amount 1,234.50", result.Detail);
    }

    [Fact]
    public void FullConstructor_CopiesMemberDetails()
    {
        List<BusinessRuleResult> members = new List<BusinessRuleResult> { new BusinessRuleResult("A", "A", RuleSeverity.Error) };

        BusinessRuleResult result = new BusinessRuleResult(
            Code, Message, RuleSeverity.Error, Name, Category, Tags, RuleResultDetails.Empty, members, null);
        members.Add(new BusinessRuleResult("B", "B", RuleSeverity.Error));

        Assert.Single(result.Details);
    }

    [Fact]
    public void FullConstructor_NullDetails_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BusinessRuleResult(
            Code, Message, RuleSeverity.Error, Name, Category, Tags, null!, Array.Empty<BusinessRuleResult>(), null));
    }

    [Fact]
    public void FullConstructor_NullMemberDetails_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BusinessRuleResult(
            Code, Message, RuleSeverity.Error, Name, Category, Tags, RuleResultDetails.Empty, null!, null));
    }

    [Fact]
    public void WithSeverity_SameSeverity_ReturnsSameInstance()
    {
        BusinessRuleResult result = new BusinessRuleResult(Code, Message, RuleSeverity.Error);

        BusinessRuleResult same = result.WithSeverity(RuleSeverity.Error);

        Assert.Same(result, same);
    }

    [Fact]
    public void WithSeverity_DifferentSeverity_CopiesEveryOtherMember()
    {
        decimal amount = 5200m;
        BusinessRuleResult member = new BusinessRuleResult("A", "A", RuleSeverity.Error);
        RuleResultDetails details = RuleResultDetails.Detail($"Amount {amount}").Actual(amount).Expected(5000m).Remediation("Lower it.").With("k", "v");
        BusinessRuleResult source = new BusinessRuleResult(Code, Message, RuleSeverity.Error, Name, Category, Tags, details, new[] { member }, null);

        BusinessRuleResult copy = source.WithSeverity(RuleSeverity.Warning);

        Assert.NotSame(source, copy);
        Assert.Equal(RuleSeverity.Warning, copy.Severity);
        Assert.Equal(RuleSeverity.Error, source.Severity);
        Assert.Equal(Code, copy.Code);
        Assert.Equal(Message, copy.Message);
        Assert.Equal(Name, copy.Name);
        Assert.Equal(Category, copy.Category);
        Assert.Same(Tags, copy.Tags);
        Assert.Equal("Amount 5200", copy.Detail);
        Assert.Equal(amount, copy.Actual);
        Assert.Equal(5000m, copy.Expected);
        Assert.Equal("Lower it.", copy.Remediation);
        Assert.Equal("v", copy.Data["k"]);
        Assert.Same(member, Assert.Single(copy.Details));
    }

    [Fact]
    public void HasTag_MatchesCaseInsensitively()
    {
        BusinessRuleResult result = new BusinessRuleResult(Code, Message, RuleSeverity.Error, Name, Category, Tags);

        Assert.True(result.HasTag("customer-facing"));
        Assert.True(result.HasTag("CUSTOMER-FACING"));
        Assert.True(result.HasTag("finance"));
        Assert.False(result.HasTag("internal"));
    }

    [Fact]
    public void HasTag_NoTags_ReturnsFalse()
    {
        BusinessRuleResult result = new BusinessRuleResult(Code, Message, RuleSeverity.Error);

        Assert.False(result.HasTag("anything"));
    }

    [Fact]
    public void HasTag_NullTag_ThrowsArgumentNullException()
    {
        BusinessRuleResult result = new BusinessRuleResult(Code, Message, RuleSeverity.Error);

        Assert.Throws<ArgumentNullException>(() => result.HasTag(null!));
    }
}
