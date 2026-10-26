namespace Masterly.BusinessRules.UnitTests;

public class AsyncRuleEvaluationResultFiltersTests
{
    private const string Payments = "Payments";
    private const string CustomerFacing = "customer-facing";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private static BusinessRuleResult Result(string code, string category, params string[] tags)
        => new BusinessRuleResult(code, code, RuleSeverity.Error, code, category, tags);

    private static AsyncRuleEvaluationResult Result()
        => new AsyncRuleEvaluationResult(
            new[]
            {
                Result("A", Payments, CustomerFacing),
                Result("B", "Identity", "internal"),
                Result("C", Payments, "CUSTOMER-FACING")
            },
            Array.Empty<IAsyncBusinessRule>());

    [Fact]
    public void ByTag_ReturnsMatchingResultsInOrderCaseInsensitively()
    {
        AsyncRuleEvaluationResult result = Result();

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
        AsyncRuleEvaluationResult result = Result();

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
        AsyncRuleEvaluationResult empty = new AsyncRuleEvaluationResult(Array.Empty<BusinessRuleResult>(), Array.Empty<IAsyncBusinessRule>());

        Assert.Empty(empty.ByTag(CustomerFacing));
        Assert.Empty(empty.ByCategory(Payments));
    }

    [Fact]
    public async Task Filters_LedgerBackedResult_UseBrokenRules()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create("A")
            .WithCategory(Payments)
            .WithTags(CustomerFacing)
            .WhenAsync((_, _) => Task.FromResult(true))
            .Build();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(
            new[] { rule, RuleTestHelper.CreatePassingAsyncRule() }, runInParallel: false, TestToken);

        Assert.Equal("A", Assert.Single(result.ByTag(CustomerFacing)).Code);
        Assert.Equal("A", Assert.Single(result.ByCategory(Payments)).Code);
    }
}
