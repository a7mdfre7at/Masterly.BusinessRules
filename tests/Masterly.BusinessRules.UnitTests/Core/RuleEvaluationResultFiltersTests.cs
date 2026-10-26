namespace Masterly.BusinessRules.UnitTests;

public class RuleEvaluationResultFiltersTests
{
    private const string Payments = "Payments";
    private const string CustomerFacing = "customer-facing";

    private static BusinessRuleResult Result(string code, string category, params string[] tags)
        => new BusinessRuleResult(code, code, RuleSeverity.Error, code, category, tags);

    private static RuleEvaluationResult Result()
        => new RuleEvaluationResult(
            new[]
            {
                Result("A", Payments, CustomerFacing),
                Result("B", "Identity", "internal"),
                Result("C", Payments, "CUSTOMER-FACING")
            },
            Array.Empty<IBusinessRule>());

    [Fact]
    public void ByTag_ReturnsMatchingResultsInOrderCaseInsensitively()
    {
        RuleEvaluationResult result = Result();

        Assert.Equal(new[] { "A", "C" }, result.ByTag("Customer-Facing").Select(broken => broken.Code));
    }

    [Fact]
    public void ByTag_NoMatch_ReturnsEmpty()
    {
        Assert.Empty(Result().ByTag("missing"));
    }

    [Fact]
    public void ByTag_NullTag_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Result().ByTag(null!));
    }

    [Fact]
    public void ByCategory_ReturnsMatchingResultsInOrder()
    {
        RuleEvaluationResult result = Result();

        Assert.Equal(new[] { "A", "C" }, result.ByCategory(Payments).Select(broken => broken.Code));
    }

    [Fact]
    public void ByCategory_IsCaseSensitive()
    {
        Assert.Empty(Result().ByCategory("payments"));
    }

    [Fact]
    public void ByCategory_NullCategory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Result().ByCategory(null!));
    }

    [Fact]
    public void Filters_EmptyResult_ReturnEmpty()
    {
        RuleEvaluationResult empty = new RuleEvaluationResult(Array.Empty<BusinessRuleResult>(), Array.Empty<IBusinessRule>());

        Assert.Empty(empty.ByTag(CustomerFacing));
        Assert.Empty(empty.ByCategory(Payments));
    }

    [Fact]
    public void Filters_LedgerBackedResult_UseBrokenRules()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create("A").WithCategory(Payments).WithTags(CustomerFacing).When(() => true).Build();

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rule, RuleTestHelper.CreatePassingRule());

        Assert.Equal("A", Assert.Single(result.ByTag(CustomerFacing)).Code);
        Assert.Equal("A", Assert.Single(result.ByCategory(Payments)).Code);
    }
}
