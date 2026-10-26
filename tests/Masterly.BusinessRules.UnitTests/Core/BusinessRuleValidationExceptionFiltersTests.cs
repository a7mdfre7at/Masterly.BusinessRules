namespace Masterly.BusinessRules.UnitTests;

public class BusinessRuleValidationExceptionFiltersTests
{
    private const string Payments = "Payments";
    private const string Identity = "Identity";
    private const string CustomerFacing = "customer-facing";

    private static BusinessRuleResult Result(string code, string category, params string[] tags)
        => new BusinessRuleResult(code, code, RuleSeverity.Error, code, category, tags);

    private static BusinessRuleValidationException Exception()
        => new BusinessRuleValidationException(new[]
        {
            Result("A", Payments, CustomerFacing),
            Result("B", Identity, "internal"),
            Result("C", Payments, "Customer-Facing", "audit"),
            new BusinessRuleResult("D", "D", RuleSeverity.Error)
        });

    [Fact]
    public void ByTag_ReturnsMatchingResultsInOrderCaseInsensitively()
    {
        BusinessRuleValidationException exception = Exception();

        List<BusinessRuleResult> matches = exception.ByTag("CUSTOMER-facing").ToList();

        Assert.Equal(new[] { "A", "C" }, matches.Select(result => result.Code));
    }

    [Fact]
    public void ByTag_NoMatch_ReturnsEmpty()
    {
        BusinessRuleValidationException exception = Exception();

        Assert.Empty(exception.ByTag("missing"));
    }

    [Fact]
    public void ByTag_NullTag_ThrowsArgumentNullException()
    {
        BusinessRuleValidationException exception = Exception();

        Assert.Throws<ArgumentNullException>(() => exception.ByTag(null!));
    }

    [Fact]
    public void ByCategory_ReturnsMatchingResultsInOrder()
    {
        BusinessRuleValidationException exception = Exception();

        List<BusinessRuleResult> matches = exception.ByCategory(Payments).ToList();

        Assert.Equal(new[] { "A", "C" }, matches.Select(result => result.Code));
    }

    [Fact]
    public void ByCategory_IsCaseSensitive()
    {
        BusinessRuleValidationException exception = Exception();

        Assert.Empty(exception.ByCategory("payments"));
    }

    [Fact]
    public void ByCategory_EmptyCategory_ReturnsResultsWithoutCategory()
    {
        BusinessRuleValidationException exception = Exception();

        BusinessRuleResult only = Assert.Single(exception.ByCategory(string.Empty));
        Assert.Equal("D", only.Code);
    }

    [Fact]
    public void ByCategory_NullCategory_ThrowsArgumentNullException()
    {
        BusinessRuleValidationException exception = Exception();

        Assert.Throws<ArgumentNullException>(() => exception.ByCategory(null!));
    }

    [Fact]
    public void ByTag_EmptyException_ReturnsEmpty()
    {
        BusinessRuleValidationException exception = new BusinessRuleValidationException(Array.Empty<BusinessRuleResult>());

        Assert.Empty(exception.ByTag(CustomerFacing));
        Assert.Empty(exception.ByCategory(Payments));
    }
}
