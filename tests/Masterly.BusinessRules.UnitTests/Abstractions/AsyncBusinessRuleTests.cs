namespace Masterly.BusinessRules.UnitTests;

public class AsyncBusinessRuleTests
{
    private sealed class AlwaysBrokenAsyncRule : BaseAsyncBusinessRule
    {
        public override string Code => "ASYNC001";
        public override string Message => "Always broken async.";
        public override Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class AlwaysValidAsyncRule : BaseAsyncBusinessRule
    {
        public override string Code => "ASYNC002";
        public override string Message => "Always valid async.";
        public override Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    [Fact]
    public async Task BrokenRule_ShouldThrowAsync()
    {
        AlwaysBrokenAsyncRule rule = new();
        BusinessRuleContext context = new();
        BusinessRuleValidationException ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() => rule.CheckAsync(context, TestContext.Current.CancellationToken));
        Assert.Single(ex.BrokenRules);
    }

    [Fact]
    public async Task ValidRule_ShouldNotThrowAsync()
    {
        AlwaysValidAsyncRule rule = new();
        BusinessRuleContext context = new();
        await rule.CheckAsync(context, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AsyncChecker_ShouldEvaluateAll()
    {
        IAsyncBusinessRule[] rules = [new AlwaysValidAsyncRule(), new AlwaysBrokenAsyncRule()];
        BusinessRuleContext context = new();

        BusinessRuleValidationException ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            AsyncBusinessRuleChecker.CheckAllAsync(context, rules));

        Assert.Single(ex.BrokenRules);
    }
}
