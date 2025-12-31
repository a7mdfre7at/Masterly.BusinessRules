using System.Threading.Tasks;

namespace Masterly.BusinessRules.UnitTests;

public class CompositeAsyncBusinessRuleTests
{
    [Fact]
    public async Task CompositeAsyncBusinessRule_WithRulesCollection_EvaluatesAll()
    {
        IAsyncBusinessRule[] rules = new IAsyncBusinessRule[]
        {
            new AsyncRuleA(false),
            new AsyncRuleA(true),
            new AsyncRuleA(false)
        };

        CompositeAsyncBusinessRule composite = new CompositeAsyncBusinessRule(rules);
        BusinessRuleContext context = new BusinessRuleContext();

        Assert.True(await composite.IsBrokenAsync(context));
    }

    [Fact]
    public async Task CompositeAsyncBusinessRule_WithRulesCollection_NoBroken_ReturnsFalse()
    {
        IAsyncBusinessRule[] rules = new IAsyncBusinessRule[]
        {
            new AsyncRuleA(false),
            new AsyncRuleA(false),
            new AsyncRuleA(false)
        };

        CompositeAsyncBusinessRule composite = new CompositeAsyncBusinessRule(rules);
        BusinessRuleContext context = new BusinessRuleContext();

        Assert.False(await composite.IsBrokenAsync(context));
    }

    [Fact]
    public async Task CompositeAsyncBusinessRule_EvaluateAllAsync_ReturnsAllBroken()
    {
        IAsyncBusinessRule[] rules = new IAsyncBusinessRule[]
        {
            new AsyncRuleA(true),
            new AsyncRuleA(false),
            new AsyncRuleA(true)
        };

        CompositeAsyncBusinessRule composite = new CompositeAsyncBusinessRule(rules);
        BusinessRuleContext context = new BusinessRuleContext();

        IReadOnlyCollection<BusinessRuleResult> results = await composite.EvaluateAllAsync(context);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task CompositeAsyncBusinessRule_WithFunc_EvaluatesFunc()
    {
        CompositeAsyncBusinessRule composite = new CompositeAsyncBusinessRule(
            async (ctx, ct) => await Task.FromResult(true),
            "Test message",
            "TEST.CODE");

        BusinessRuleContext context = new BusinessRuleContext();

        Assert.True(await composite.IsBrokenAsync(context));
    }

    [Fact]
    public void CompositeAsyncBusinessRule_Rules_ReturnsRulesCollection()
    {
        IAsyncBusinessRule[] rules = new IAsyncBusinessRule[]
        {
            new AsyncRuleA(false),
            new AsyncRuleA(true)
        };

        CompositeAsyncBusinessRule composite = new CompositeAsyncBusinessRule(rules);

        Assert.NotNull(composite.Rules);
        Assert.Equal(2, composite.Rules.Count);
    }

    [Fact]
    public void CompositeAsyncBusinessRule_WithFunc_RulesIsNull()
    {
        CompositeAsyncBusinessRule composite = new CompositeAsyncBusinessRule(
            async (ctx, ct) => await Task.FromResult(true),
            "Test message",
            "TEST.CODE");

        Assert.Null(composite.Rules);
    }

    [Fact]
    public async Task CompositeAsyncBusinessRule_CheckAsync_ThrowsWhenBroken()
    {
        IAsyncBusinessRule[] rules = new IAsyncBusinessRule[] { new AsyncRuleA(true) };
        CompositeAsyncBusinessRule composite = new CompositeAsyncBusinessRule(rules);
        BusinessRuleContext context = new BusinessRuleContext();

        await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => composite.CheckAsync(context));
    }

    [Fact]
    public async Task CompositeAsyncBusinessRule_EvaluateAsync_ReturnsResult()
    {
        IAsyncBusinessRule[] rules = new IAsyncBusinessRule[] { new AsyncRuleA(true) };
        CompositeAsyncBusinessRule composite = new CompositeAsyncBusinessRule(rules);
        BusinessRuleContext context = new BusinessRuleContext();

        BusinessRuleResult? result = await composite.EvaluateAsync(context);

        Assert.NotNull(result);
        Assert.Equal("CompositeAsyncRule", result.Code);
    }

    [Fact]
    public async Task CompositeAsyncBusinessRule_EvaluateAllAsync_WithFunc_ReturnsResultOrEmpty()
    {
        CompositeAsyncBusinessRule composite = new CompositeAsyncBusinessRule(
            async (ctx, ct) => await Task.FromResult(true),
            "Test message",
            "TEST.CODE");

        BusinessRuleContext context = new BusinessRuleContext();
        IReadOnlyCollection<BusinessRuleResult> results = await composite.EvaluateAllAsync(context);

        Assert.Single(results);
        Assert.Equal("TEST.CODE", results.First().Code);
    }

    [Fact]
    public async Task CompositeAsyncBusinessRule_EvaluateAllAsync_WithFunc_NotBroken_ReturnsEmpty()
    {
        CompositeAsyncBusinessRule composite = new CompositeAsyncBusinessRule(
            async (ctx, ct) => await Task.FromResult(false),
            "Test message",
            "TEST.CODE");

        BusinessRuleContext context = new BusinessRuleContext();
        IReadOnlyCollection<BusinessRuleResult> results = await composite.EvaluateAllAsync(context);

        Assert.Empty(results);
    }
}
