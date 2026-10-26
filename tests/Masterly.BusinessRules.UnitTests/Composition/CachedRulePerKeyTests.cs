namespace Masterly.BusinessRules.UnitTests;

public class CachedRulePerKeyTests
{
    private const string Code = "CACHE.KEYED";
    private const string Message = "Keyed cache test";
    private const string TenantKey = "tenant";
    private const string Acme = "acme";
    private const string Globex = "globex";

    private static readonly TimeSpan Duration = TimeSpan.FromMinutes(5);

    private static string SelectTenant(BusinessRuleContext context) => context.Get<string>(TenantKey);

    // ---------------------------------------------------------------------
    // Construction
    // ---------------------------------------------------------------------

    [Fact]
    public void Constructor_WithoutSelector_DoesNotCachePerKey()
    {
        CachedBusinessRule cached = new CachedBusinessRule(RuleTestHelper.CreateBrokenRule(), Duration);

        Assert.False(cached.CachesPerKey);
    }

    [Fact]
    public void Constructor_WithSelector_CachesPerKey()
    {
        CachedBusinessRule cached = new CachedBusinessRule(RuleTestHelper.CreateBrokenRule(), Duration, SelectTenant);

        Assert.True(cached.CachesPerKey);
        Assert.IsAssignableFrom<IDecoratedBusinessRule>(cached);
    }

    [Fact]
    public void Constructor_NullSelector_BehavesLikeTheTwoArgumentConstructor()
    {
        CachedBusinessRule cached = new CachedBusinessRule(RuleTestHelper.CreateBrokenRule(), Duration, null);

        Assert.False(cached.CachesPerKey);
    }

    [Fact]
    public void Constructor_NullInnerRule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CachedBusinessRule(null!, Duration, SelectTenant));
    }

    [Fact]
    public void Cached_Extension_WithSelector_ReturnsPerKeyCache()
    {
        CachedBusinessRule cached = RuleTestHelper.CreateBrokenRule().Cached(Duration, SelectTenant);

        Assert.True(cached.CachesPerKey);
    }

    [Fact]
    public void Cached_Extension_NullSelector_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleTestHelper.CreateBrokenRule().Cached(Duration, (Func<BusinessRuleContext, string>)null!));
    }

    [Fact]
    public void Cached_Extension_PreservesMetadata()
    {
        IBusinessRule inner = BusinessRuleBuilder.Create(Code).WithMessage(Message).WithSeverity(RuleSeverity.Warning).WithCategory("Cache").WithTags("a").When(() => true).Build();

        CachedBusinessRule cached = inner.Cached(Duration, SelectTenant);

        Assert.Equal(Code, cached.Code);
        Assert.Equal(Message, cached.Message);
        Assert.Equal(RuleSeverity.Warning, cached.Severity);
        Assert.Equal("Cache", cached.Category);
        Assert.Equal(new[] { "a" }, cached.Tags);
        Assert.Same(inner, cached.InnerRule);
    }

    // ---------------------------------------------------------------------
    // Per-key behaviour
    // ---------------------------------------------------------------------

    [Fact]
    public void IsBroken_SameKey_EvaluatesOnce()
    {
        int evaluations = 0;
        CachedBusinessRule cached = CreateCountingRule(() => evaluations++).Cached(Duration, SelectTenant);
        BusinessRuleContext context = CreateContext(Acme);

        cached.IsBroken(context);
        cached.IsBroken(context);
        cached.IsBroken(CreateContext(Acme));

        Assert.Equal(1, evaluations);
    }

    [Fact]
    public void IsBroken_DifferentKeys_EvaluateSeparately()
    {
        int evaluations = 0;
        CachedBusinessRule cached = CreateCountingRule(() => evaluations++).Cached(Duration, SelectTenant);

        cached.IsBroken(CreateContext(Acme));
        cached.IsBroken(CreateContext(Globex));
        cached.IsBroken(CreateContext(Acme));
        cached.IsBroken(CreateContext(Globex));

        Assert.Equal(2, evaluations);
    }

    [Fact]
    public void IsBroken_DifferentKeys_KeepSeparateVerdicts()
    {
        IBusinessRule inner = BusinessRuleBuilder.Create(Code).WithMessage(Message).When(ctx => ctx.Get<string>(TenantKey) == Acme).Build();
        CachedBusinessRule cached = inner.Cached(Duration, SelectTenant);

        bool acmeFirst = cached.IsBroken(CreateContext(Acme));
        bool globexFirst = cached.IsBroken(CreateContext(Globex));
        bool acmeAgain = cached.IsBroken(CreateContext(Acme));
        bool globexAgain = cached.IsBroken(CreateContext(Globex));

        Assert.True(acmeFirst);
        Assert.False(globexFirst);
        Assert.True(acmeAgain);
        Assert.False(globexAgain);
    }

    [Fact]
    public void IsBroken_ScopedParameters_AreCachedPerTenant()
    {
        InMemoryRuleParameterProvider parameters = new InMemoryRuleParameterProvider().Set("limit", 10m).Set("limit", 100m, RuleScope.Tenant(Acme));
        IBusinessRule inner = BusinessRuleBuilder.Create(Code).WithMessage(Message)
            .WithParameters(parameters)
            .WithParameter("limit", 0m)
            .When((ctx, p) => 50m > p.Get<decimal>("limit"))
            .Build();
        CachedBusinessRule cached = inner.Cached(Duration, ctx => ctx.Get(RuleParameters.ScopeKey).ToString());

        bool acme = cached.IsBroken(RuleTestHelper.CreateContext((RuleParameters.ScopeKey, RuleScope.Tenant(Acme))));
        bool globex = cached.IsBroken(RuleTestHelper.CreateContext((RuleParameters.ScopeKey, RuleScope.Tenant(Globex))));

        Assert.False(acme);
        Assert.True(globex);
    }

    [Fact]
    public void IsBroken_Parameterless_UsesTheSharedEntry()
    {
        int evaluations = 0;
        CachedBusinessRule cached = CreateCountingRule(() => evaluations++).Cached(Duration, SelectTenant);

        cached.IsBroken();
        cached.IsBroken();
        cached.IsBroken(CreateContext(Acme));

        Assert.Equal(2, evaluations);
    }

    [Fact]
    public void IsBroken_SelectorReturnsNull_ThrowsRuleConfigurationException()
    {
        CachedBusinessRule cached = RuleTestHelper.CreateBrokenRule(Code).Cached(Duration, ctx => null!);

        RuleConfigurationException exception = Assert.Throws<RuleConfigurationException>(() => cached.IsBroken(new BusinessRuleContext()));

        Assert.Equal(Code, exception.RuleCode);
    }

    [Fact]
    public void IsBroken_SelectorThrows_PropagatesTheException()
    {
        CachedBusinessRule cached = RuleTestHelper.CreateBrokenRule().Cached(Duration, SelectTenant);

        Assert.Throws<ContextKeyNotFoundException>(() => cached.IsBroken(new BusinessRuleContext()));
    }

    [Fact]
    public void IsBroken_ExpiredEntry_IsReevaluated()
    {
        int evaluations = 0;
        CachedBusinessRule cached = CreateCountingRule(() => evaluations++).Cached(TimeSpan.Zero, SelectTenant);

        cached.IsBroken(CreateContext(Acme));
        cached.IsBroken(CreateContext(Acme));

        Assert.Equal(2, evaluations);
    }

    [Fact]
    public void Check_SameKey_UsesCache()
    {
        int evaluations = 0;
        CachedBusinessRule cached = CreateCountingRule(() => evaluations++).Cached(Duration, SelectTenant);

        Assert.Throws<BusinessRuleValidationException>(() => cached.Check(CreateContext(Acme)));
        Assert.Throws<BusinessRuleValidationException>(() => cached.Check(CreateContext(Acme)));

        Assert.Equal(1, evaluations);
    }

    // ---------------------------------------------------------------------
    // Invalidation
    // ---------------------------------------------------------------------

    [Fact]
    public void InvalidateCache_ClearsEveryKey()
    {
        int evaluations = 0;
        CachedBusinessRule cached = CreateCountingRule(() => evaluations++).Cached(Duration, SelectTenant);
        cached.IsBroken(CreateContext(Acme));
        cached.IsBroken(CreateContext(Globex));
        cached.IsBroken();

        cached.InvalidateCache();
        cached.IsBroken(CreateContext(Acme));
        cached.IsBroken(CreateContext(Globex));
        cached.IsBroken();

        Assert.Equal(6, evaluations);
    }

    [Fact]
    public void InvalidateCache_Key_ClearsOnlyThatKey()
    {
        int evaluations = 0;
        CachedBusinessRule cached = CreateCountingRule(() => evaluations++).Cached(Duration, SelectTenant);
        cached.IsBroken(CreateContext(Acme));
        cached.IsBroken(CreateContext(Globex));

        cached.InvalidateCache(Acme);
        cached.IsBroken(CreateContext(Acme));
        cached.IsBroken(CreateContext(Globex));

        Assert.Equal(3, evaluations);
    }

    [Fact]
    public void InvalidateCache_UnknownKey_IsIgnored()
    {
        CachedBusinessRule cached = RuleTestHelper.CreateBrokenRule().Cached(Duration, SelectTenant);
        CachedBusinessRule shared = RuleTestHelper.CreateBrokenRule().Cached(Duration);

        cached.InvalidateCache("unknown");
        shared.InvalidateCache("unknown");
    }

    [Fact]
    public void InvalidateCache_NullKey_ThrowsArgumentNullException()
    {
        CachedBusinessRule cached = RuleTestHelper.CreateBrokenRule().Cached(Duration, SelectTenant);

        Assert.Throws<ArgumentNullException>(() => cached.InvalidateCache(null!));
    }

    // ---------------------------------------------------------------------
    // Explain
    // ---------------------------------------------------------------------

    [Fact]
    public void Explain_SecondCallForSameKey_ReportsCacheHit()
    {
        CachedBusinessRule cached = RuleTestHelper.CreateBrokenRule().Cached(Duration, SelectTenant);
        cached.IsBroken(CreateContext(Acme));

        RuleExplanation hit = cached.Explain(CreateContext(Acme));
        RuleExplanation miss = cached.Explain(CreateContext(Globex));

        Assert.Equal(RuleOutcome.CacheHit, hit.Outcome);
        Assert.Equal(RuleOutcome.Broken, miss.Outcome);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static IBusinessRule CreateCountingRule(Action onEvaluate)
    {
        return BusinessRuleBuilder.Create(Code).WithMessage(Message).When(ctx => { onEvaluate(); return true; }).Build();
    }

    private static BusinessRuleContext CreateContext(string tenant)
    {
        return RuleTestHelper.CreateContext((TenantKey, tenant));
    }
}

public class CachedAsyncRulePerKeyTests
{
    private const string Code = "CACHE.KEYED.ASYNC";
    private const string Message = "Keyed async cache test";
    private const string TenantKey = "tenant";
    private const string Acme = "acme";
    private const string Globex = "globex";

    private static readonly TimeSpan Duration = TimeSpan.FromMinutes(5);

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private static string SelectTenant(BusinessRuleContext context) => context.Get<string>(TenantKey);

    // ---------------------------------------------------------------------
    // Construction
    // ---------------------------------------------------------------------

    [Fact]
    public void Constructor_WithoutSelector_DoesNotCachePerKey()
    {
        CachedAsyncBusinessRule cached = new CachedAsyncBusinessRule(RuleTestHelper.CreateBrokenAsyncRule(), Duration);

        Assert.False(cached.CachesPerKey);
    }

    [Fact]
    public void Constructor_WithSelector_CachesPerKey()
    {
        CachedAsyncBusinessRule cached = new CachedAsyncBusinessRule(RuleTestHelper.CreateBrokenAsyncRule(), Duration, SelectTenant);

        Assert.True(cached.CachesPerKey);
        Assert.IsAssignableFrom<IDecoratedAsyncBusinessRule>(cached);
    }

    [Fact]
    public void Constructor_NullInnerRule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CachedAsyncBusinessRule(null!, Duration, SelectTenant));
    }

    [Fact]
    public void Cached_Extension_WithSelector_ReturnsPerKeyCache()
    {
        CachedAsyncBusinessRule cached = RuleTestHelper.CreateBrokenAsyncRule().Cached(Duration, SelectTenant);

        Assert.True(cached.CachesPerKey);
    }

    [Fact]
    public void Cached_Extension_NullSelector_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleTestHelper.CreateBrokenAsyncRule().Cached(Duration, (Func<BusinessRuleContext, string>)null!));
    }

    [Fact]
    public void Cached_Extension_PreservesMetadata()
    {
        IAsyncBusinessRule inner = AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WithSeverity(RuleSeverity.Warning).WithCategory("Cache").WithTags("a").WhenAsync((ctx, ct) => Task.FromResult(true)).Build();

        CachedAsyncBusinessRule cached = inner.Cached(Duration, SelectTenant);

        Assert.Equal(Code, cached.Code);
        Assert.Equal(Message, cached.Message);
        Assert.Equal(RuleSeverity.Warning, cached.Severity);
        Assert.Equal("Cache", cached.Category);
        Assert.Equal(new[] { "a" }, cached.Tags);
        Assert.Same(inner, cached.InnerRule);
    }

    // ---------------------------------------------------------------------
    // Per-key behaviour
    // ---------------------------------------------------------------------

    [Fact]
    public async Task IsBrokenAsync_SameKey_EvaluatesOnce()
    {
        int evaluations = 0;
        CachedAsyncBusinessRule cached = CreateCountingRule(() => evaluations++).Cached(Duration, SelectTenant);

        await cached.IsBrokenAsync(CreateContext(Acme), TestToken);
        await cached.IsBrokenAsync(CreateContext(Acme), TestToken);

        Assert.Equal(1, evaluations);
    }

    [Fact]
    public async Task IsBrokenAsync_DifferentKeys_EvaluateSeparately()
    {
        int evaluations = 0;
        CachedAsyncBusinessRule cached = CreateCountingRule(() => evaluations++).Cached(Duration, SelectTenant);

        await cached.IsBrokenAsync(CreateContext(Acme), TestToken);
        await cached.IsBrokenAsync(CreateContext(Globex), TestToken);
        await cached.IsBrokenAsync(CreateContext(Acme), TestToken);
        await cached.IsBrokenAsync(CreateContext(Globex), TestToken);

        Assert.Equal(2, evaluations);
    }

    [Fact]
    public async Task IsBrokenAsync_DifferentKeys_KeepSeparateVerdicts()
    {
        IAsyncBusinessRule inner = AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WhenAsync(ctx => Task.FromResult(ctx.Get<string>(TenantKey) == Acme)).Build();
        CachedAsyncBusinessRule cached = inner.Cached(Duration, SelectTenant);

        bool acmeFirst = await cached.IsBrokenAsync(CreateContext(Acme), TestToken);
        bool globexFirst = await cached.IsBrokenAsync(CreateContext(Globex), TestToken);
        bool acmeAgain = await cached.IsBrokenAsync(CreateContext(Acme), TestToken);
        bool globexAgain = await cached.IsBrokenAsync(CreateContext(Globex), TestToken);

        Assert.True(acmeFirst);
        Assert.False(globexFirst);
        Assert.True(acmeAgain);
        Assert.False(globexAgain);
    }

    [Fact]
    public async Task IsBrokenAsync_ScopedParameters_AreCachedPerTenant()
    {
        InMemoryRuleParameterProvider parameters = new InMemoryRuleParameterProvider().Set("limit", 10m).Set("limit", 100m, RuleScope.Tenant(Acme));
        IAsyncBusinessRule inner = AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message)
            .WithParameters(parameters)
            .WithParameter("limit", 0m)
            .WhenAsync((ctx, p, ct) => Task.FromResult(50m > p.Get<decimal>("limit")))
            .Build();
        CachedAsyncBusinessRule cached = inner.Cached(Duration, ctx => ctx.Get(RuleParameters.ScopeKey).ToString());

        bool acme = await cached.IsBrokenAsync(RuleTestHelper.CreateContext((RuleParameters.ScopeKey, RuleScope.Tenant(Acme))), TestToken);
        bool globex = await cached.IsBrokenAsync(RuleTestHelper.CreateContext((RuleParameters.ScopeKey, RuleScope.Tenant(Globex))), TestToken);

        Assert.False(acme);
        Assert.True(globex);
    }

    [Fact]
    public async Task IsBrokenAsync_SelectorReturnsNull_ThrowsRuleConfigurationException()
    {
        CachedAsyncBusinessRule cached = RuleTestHelper.CreateBrokenAsyncRule(Code).Cached(Duration, ctx => null!);

        RuleConfigurationException exception = await Assert.ThrowsAsync<RuleConfigurationException>(() => cached.IsBrokenAsync(new BusinessRuleContext(), TestToken));

        Assert.Equal(Code, exception.RuleCode);
    }

    [Fact]
    public async Task IsBrokenAsync_SelectorThrows_PropagatesTheException()
    {
        CachedAsyncBusinessRule cached = RuleTestHelper.CreateBrokenAsyncRule().Cached(Duration, SelectTenant);

        await Assert.ThrowsAsync<ContextKeyNotFoundException>(() => cached.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_ExpiredEntry_IsReevaluated()
    {
        int evaluations = 0;
        CachedAsyncBusinessRule cached = CreateCountingRule(() => evaluations++).Cached(TimeSpan.Zero, SelectTenant);

        await cached.IsBrokenAsync(CreateContext(Acme), TestToken);
        await cached.IsBrokenAsync(CreateContext(Acme), TestToken);

        Assert.Equal(2, evaluations);
    }

    [Fact]
    public async Task IsBrokenAsync_CancelledDuringEvaluation_DoesNotPopulateTheCache()
    {
        int evaluations = 0;
        using CancellationTokenSource source = new CancellationTokenSource();
        IAsyncBusinessRule inner = AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message)
            .WhenAsync((ctx, ct) =>
            {
                evaluations++;
                source.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(true);
            })
            .Build();
        CachedAsyncBusinessRule cached = inner.Cached(Duration, SelectTenant);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cached.IsBrokenAsync(CreateContext(Acme), source.Token));
        await cached.IsBrokenAsync(CreateContext(Acme), TestToken);

        Assert.Equal(2, evaluations);
    }

    [Fact]
    public async Task IsBrokenAsync_AlreadyCancelledToken_ThrowsBeforeEvaluating()
    {
        int evaluations = 0;
        CachedAsyncBusinessRule cached = CreateCountingRule(() => evaluations++).Cached(Duration, SelectTenant);
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cached.IsBrokenAsync(CreateContext(Acme), source.Token));

        Assert.Equal(0, evaluations);
    }

    [Fact]
    public async Task EvaluateAsync_SameKey_UsesCache()
    {
        int evaluations = 0;
        CachedAsyncBusinessRule cached = CreateCountingRule(() => evaluations++).Cached(Duration, SelectTenant);

        BusinessRuleResult? first = await cached.EvaluateAsync(CreateContext(Acme), TestToken);
        BusinessRuleResult? second = await cached.EvaluateAsync(CreateContext(Acme), TestToken);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(1, evaluations);
    }

    [Fact]
    public async Task CheckAsync_SameKey_UsesCache()
    {
        int evaluations = 0;
        CachedAsyncBusinessRule cached = CreateCountingRule(() => evaluations++).Cached(Duration, SelectTenant);

        await Assert.ThrowsAsync<BusinessRuleValidationException>(() => cached.CheckAsync(CreateContext(Acme), TestToken));
        await Assert.ThrowsAsync<BusinessRuleValidationException>(() => cached.CheckAsync(CreateContext(Acme), TestToken));

        Assert.Equal(1, evaluations);
    }

    // ---------------------------------------------------------------------
    // Invalidation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task InvalidateCacheAsync_ClearsEveryKey()
    {
        int evaluations = 0;
        CachedAsyncBusinessRule cached = CreateCountingRule(() => evaluations++).Cached(Duration, SelectTenant);
        await cached.IsBrokenAsync(CreateContext(Acme), TestToken);
        await cached.IsBrokenAsync(CreateContext(Globex), TestToken);

        await cached.InvalidateCacheAsync(TestToken);
        await cached.IsBrokenAsync(CreateContext(Acme), TestToken);
        await cached.IsBrokenAsync(CreateContext(Globex), TestToken);

        Assert.Equal(4, evaluations);
    }

    [Fact]
    public async Task InvalidateCacheAsync_Key_ClearsOnlyThatKey()
    {
        int evaluations = 0;
        CachedAsyncBusinessRule cached = CreateCountingRule(() => evaluations++).Cached(Duration, SelectTenant);
        await cached.IsBrokenAsync(CreateContext(Acme), TestToken);
        await cached.IsBrokenAsync(CreateContext(Globex), TestToken);

        await cached.InvalidateCacheAsync(Acme, TestToken);
        await cached.IsBrokenAsync(CreateContext(Acme), TestToken);
        await cached.IsBrokenAsync(CreateContext(Globex), TestToken);

        Assert.Equal(3, evaluations);
    }

    [Fact]
    public async Task InvalidateCacheAsync_UnknownKey_IsIgnored()
    {
        CachedAsyncBusinessRule cached = RuleTestHelper.CreateBrokenAsyncRule().Cached(Duration, SelectTenant);
        CachedAsyncBusinessRule shared = new CachedAsyncBusinessRule(RuleTestHelper.CreateBrokenAsyncRule(), Duration);

        await cached.InvalidateCacheAsync("unknown", TestToken);
        await shared.InvalidateCacheAsync("unknown", TestToken);
    }

    [Fact]
    public async Task InvalidateCacheAsync_NullKey_ThrowsArgumentNullException()
    {
        CachedAsyncBusinessRule cached = RuleTestHelper.CreateBrokenAsyncRule().Cached(Duration, SelectTenant);

        await Assert.ThrowsAsync<ArgumentNullException>(() => cached.InvalidateCacheAsync(null!, TestToken));
    }

    [Fact]
    public async Task InvalidateCacheAsync_Key_CancelledToken_Throws()
    {
        CachedAsyncBusinessRule cached = RuleTestHelper.CreateBrokenAsyncRule().Cached(Duration, SelectTenant);
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cached.InvalidateCacheAsync(Acme, source.Token));
    }

    // ---------------------------------------------------------------------
    // Explain
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExplainAsync_SecondCallForSameKey_ReportsCacheHit()
    {
        CachedAsyncBusinessRule cached = RuleTestHelper.CreateBrokenAsyncRule().Cached(Duration, SelectTenant);
        await cached.IsBrokenAsync(CreateContext(Acme), TestToken);

        RuleExplanation hit = await cached.ExplainAsync(CreateContext(Acme), TestToken);
        RuleExplanation miss = await cached.ExplainAsync(CreateContext(Globex), TestToken);

        Assert.Equal(RuleOutcome.CacheHit, hit.Outcome);
        Assert.Equal(RuleOutcome.Broken, miss.Outcome);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static IAsyncBusinessRule CreateCountingRule(Action onEvaluate)
    {
        return AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WhenAsync((ctx, ct) => { onEvaluate(); return Task.FromResult(true); }).Build();
    }

    private static BusinessRuleContext CreateContext(string tenant)
    {
        return RuleTestHelper.CreateContext((TenantKey, tenant));
    }
}
