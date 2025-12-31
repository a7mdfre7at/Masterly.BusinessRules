using System.Threading.Tasks;

namespace Masterly.BusinessRules.UnitTests;

public class CachedRuleTests
{
    [Fact]
    public void CachedBusinessRule_CachesResult()
    {
        int evaluationCount = 0;
        IBusinessRule innerRule = BusinessRuleBuilder.Create("CACHE.TEST")
            .WithMessage("Cache test")
            .When(() =>
            {
                evaluationCount++;
                return true;
            })
            .Build();

        CachedBusinessRule cachedRule = new CachedBusinessRule(innerRule, TimeSpan.FromMinutes(5));

        cachedRule.IsBroken();
        cachedRule.IsBroken();
        cachedRule.IsBroken();

        Assert.Equal(1, evaluationCount);
    }

    [Fact]
    public void CachedBusinessRule_PreservesMetadata()
    {
        IBusinessRule innerRule = BusinessRuleBuilder.Create("META.TEST")
            .WithMessage("Metadata test")
            .WithSeverity(RuleSeverity.Warning)
            .When(() => true)
            .Build();

        CachedBusinessRule cachedRule = new CachedBusinessRule(innerRule, TimeSpan.FromMinutes(5));

        Assert.Equal("META.TEST", cachedRule.Code);
        Assert.Equal("Metadata test", cachedRule.Message);
        Assert.Equal(RuleSeverity.Warning, cachedRule.Severity);
    }

    [Fact]
    public void CachedBusinessRule_InvalidateCache_ForcesReevaluation()
    {
        int evaluationCount = 0;
        IBusinessRule innerRule = BusinessRuleBuilder.Create("INVALIDATE.TEST")
            .WithMessage("Invalidate test")
            .When(() =>
            {
                evaluationCount++;
                return true;
            })
            .Build();

        CachedBusinessRule cachedRule = new CachedBusinessRule(innerRule, TimeSpan.FromMinutes(5));

        cachedRule.IsBroken();
        cachedRule.InvalidateCache();
        cachedRule.IsBroken();

        Assert.Equal(2, evaluationCount);
    }

    [Fact]
    public void CachedBusinessRule_WithContext_CachesCorrectly()
    {
        int evaluationCount = 0;
        IBusinessRule innerRule = BusinessRuleBuilder.Create("CONTEXT.CACHE")
            .WithMessage("Context cache")
            .When(ctx =>
            {
                evaluationCount++;
                return true;
            })
            .Build();

        CachedBusinessRule cachedRule = new CachedBusinessRule(innerRule, TimeSpan.FromMinutes(5));
        BusinessRuleContext context = new BusinessRuleContext();

        cachedRule.IsBroken(context);
        cachedRule.IsBroken(context);

        Assert.Equal(1, evaluationCount);
    }

    [Fact]
    public void CachedBusinessRule_Check_UsesCache()
    {
        int evaluationCount = 0;
        IBusinessRule innerRule = BusinessRuleBuilder.Create("CHECK.CACHE")
            .WithMessage("Check cache")
            .When(() =>
            {
                evaluationCount++;
                return true;
            })
            .Build();

        CachedBusinessRule cachedRule = new CachedBusinessRule(innerRule, TimeSpan.FromMinutes(5));

        try { cachedRule.Check(); } catch { }
        try { cachedRule.Check(); } catch { }

        Assert.Equal(1, evaluationCount);
    }

    [Fact]
    public void Cached_Extension_CreatesCachedRule()
    {
        IBusinessRule rule = new RuleA(false).Cached(TimeSpan.FromMinutes(1));

        Assert.IsType<CachedBusinessRule>(rule);
    }
}

public class CachedAsyncRuleTests
{
    [Fact]
    public async Task CachedAsyncBusinessRule_CachesResult()
    {
        int evaluationCount = 0;
        IAsyncBusinessRule innerRule = AsyncBusinessRuleBuilder.Create("ASYNC.CACHE")
            .WithMessage("Async cache test")
            .WhenAsync(async (ctx, ct) =>
            {
                evaluationCount++;
                return await Task.FromResult(true);
            })
            .Build();

        CachedAsyncBusinessRule cachedRule = new CachedAsyncBusinessRule(innerRule, TimeSpan.FromMinutes(5));
        BusinessRuleContext context = new BusinessRuleContext();

        await cachedRule.IsBrokenAsync(context);
        await cachedRule.IsBrokenAsync(context);
        await cachedRule.IsBrokenAsync(context);

        Assert.Equal(1, evaluationCount);
    }

    [Fact]
    public async Task CachedAsyncBusinessRule_InvalidateCacheAsync_ForcesReevaluation()
    {
        int evaluationCount = 0;
        IAsyncBusinessRule innerRule = AsyncBusinessRuleBuilder.Create("ASYNC.INVALIDATE")
            .WithMessage("Async invalidate test")
            .WhenAsync(async (ctx, ct) =>
            {
                evaluationCount++;
                return await Task.FromResult(true);
            })
            .Build();

        CachedAsyncBusinessRule cachedRule = new CachedAsyncBusinessRule(innerRule, TimeSpan.FromMinutes(5));
        BusinessRuleContext context = new BusinessRuleContext();

        await cachedRule.IsBrokenAsync(context);
        await cachedRule.InvalidateCacheAsync();
        await cachedRule.IsBrokenAsync(context);

        Assert.Equal(2, evaluationCount);
    }

    [Fact]
    public async Task CachedAsyncBusinessRule_EvaluateAsync_UsesCache()
    {
        int evaluationCount = 0;
        IAsyncBusinessRule innerRule = AsyncBusinessRuleBuilder.Create("ASYNC.EVAL.CACHE")
            .WithMessage("Async eval cache")
            .WhenAsync(async (ctx, ct) =>
            {
                evaluationCount++;
                return await Task.FromResult(true);
            })
            .Build();

        CachedAsyncBusinessRule cachedRule = new CachedAsyncBusinessRule(innerRule, TimeSpan.FromMinutes(5));
        BusinessRuleContext context = new BusinessRuleContext();

        await cachedRule.EvaluateAsync(context);
        await cachedRule.EvaluateAsync(context);

        Assert.Equal(1, evaluationCount);
    }
}
