namespace Masterly.BusinessRules.UnitTests;

public class DummyAsyncRule(bool _isBroken) : BaseAsyncBusinessRule
{
    public override string Code => "ASYNC_DUMMY";
    public override string Message => "Async dummy rule failed.";
    public override Task<bool> IsBrokenAsync(BusinessRuleContext context, System.Threading.CancellationToken cancellationToken = default)
        => Task.FromResult(_isBroken);
}

public class AsyncBusinessRuleCheckerTests
{
    [Fact]
    public async Task CheckAllAsync_NoBrokenRules_DoesNotThrow()
    {
        await AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), new DummyAsyncRule(false));
    }

    [Fact]
    public async Task CheckAllAsync_BrokenRule_ThrowsException()
    {
        await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), new DummyAsyncRule(true)));
    }
}
