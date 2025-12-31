using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules.UnitTests;

public class AsyncRuleA(bool _isBroken) : BaseAsyncBusinessRule
{
    public override string Code => "ASYNC_RULE_A";
    public override string Message => "Async Rule A";

    public override Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(_isBroken);
}

public class AsyncBusinessRuleExtensionsTests
{
    [Fact]
    public async Task And_BothBroken_ReturnsBroken()
    {
        IAsyncBusinessRule rule = new AsyncRuleA(true).And(new AsyncRuleA(true));
        Assert.True(await rule.IsBrokenAsync(new BusinessRuleContext()));
    }

    [Fact]
    public async Task And_OneBroken_ReturnsNotBroken()
    {
        IAsyncBusinessRule rule = new AsyncRuleA(true).And(new AsyncRuleA(false));
        Assert.False(await rule.IsBrokenAsync(new BusinessRuleContext()));
    }

    [Fact]
    public async Task And_NeitherBroken_ReturnsNotBroken()
    {
        IAsyncBusinessRule rule = new AsyncRuleA(false).And(new AsyncRuleA(false));
        Assert.False(await rule.IsBrokenAsync(new BusinessRuleContext()));
    }

    [Fact]
    public async Task Or_BothBroken_ReturnsBroken()
    {
        IAsyncBusinessRule rule = new AsyncRuleA(true).Or(new AsyncRuleA(true));
        Assert.True(await rule.IsBrokenAsync(new BusinessRuleContext()));
    }

    [Fact]
    public async Task Or_OneBroken_ReturnsBroken()
    {
        IAsyncBusinessRule rule = new AsyncRuleA(true).Or(new AsyncRuleA(false));
        Assert.True(await rule.IsBrokenAsync(new BusinessRuleContext()));
    }

    [Fact]
    public async Task Or_NeitherBroken_ReturnsNotBroken()
    {
        IAsyncBusinessRule rule = new AsyncRuleA(false).Or(new AsyncRuleA(false));
        Assert.False(await rule.IsBrokenAsync(new BusinessRuleContext()));
    }

    [Fact]
    public async Task Not_Broken_ReturnsNotBroken()
    {
        IAsyncBusinessRule rule = new AsyncRuleA(true).Not();
        Assert.False(await rule.IsBrokenAsync(new BusinessRuleContext()));
    }

    [Fact]
    public async Task Not_NotBroken_ReturnsBroken()
    {
        IAsyncBusinessRule rule = new AsyncRuleA(false).Not();
        Assert.True(await rule.IsBrokenAsync(new BusinessRuleContext()));
    }

    [Fact]
    public async Task ComplexComposition_EvaluatesCorrectly()
    {
        // (true AND true) OR false = true OR false = true
        IAsyncBusinessRule rule = new AsyncRuleA(true)
            .And(new AsyncRuleA(true))
            .Or(new AsyncRuleA(false));

        Assert.True(await rule.IsBrokenAsync(new BusinessRuleContext()));
    }

    [Fact]
    public async Task ToAsync_ConvertsCorrectly()
    {
        RuleA syncRule = new RuleA(true);
        IAsyncBusinessRule asyncRule = syncRule.ToAsync();

        Assert.True(await asyncRule.IsBrokenAsync(new BusinessRuleContext()));
        Assert.Equal(syncRule.Code, asyncRule.Code);
        Assert.Equal(syncRule.Message, asyncRule.Message);
    }

    [Fact]
    public async Task When_ConditionTrue_EvaluatesRule()
    {
        IAsyncBusinessRule rule = new AsyncRuleA(true).When(ctx => true);
        Assert.True(await rule.IsBrokenAsync(new BusinessRuleContext()));
    }

    [Fact]
    public async Task When_ConditionFalse_SkipsRule()
    {
        IAsyncBusinessRule rule = new AsyncRuleA(true).When(ctx => false);
        Assert.False(await rule.IsBrokenAsync(new BusinessRuleContext()));
    }

    [Fact]
    public async Task Cached_ReturnsCachedResult()
    {
        int evaluationCount = 0;
        IAsyncBusinessRule innerRule = AsyncBusinessRuleBuilder.Create("CACHED_TEST")
            .WithMessage("Test")
            .WhenAsync(async (ctx, ct) =>
            {
                evaluationCount++;
                return true;
            })
            .Build();

        IAsyncBusinessRule cachedRule = innerRule.Cached(TimeSpan.FromMinutes(5));
        BusinessRuleContext context = new BusinessRuleContext();

        await cachedRule.IsBrokenAsync(context);
        await cachedRule.IsBrokenAsync(context);
        await cachedRule.IsBrokenAsync(context);

        Assert.Equal(1, evaluationCount);
    }
}
