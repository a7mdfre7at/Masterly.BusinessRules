namespace Masterly.BusinessRules.UnitTests;

/// <summary>
/// What a <see cref="CachedAsyncBusinessRule"/> must not cache (a verdict resting on a captured fault, a verdict
/// invalidated while it was computed) and that one slow key never blocks the others.
/// </summary>
public class CachedAsyncRuleIsolationTests
{
    private const string TenantKey = "tenant";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------
    // Captured faults are never cached
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExplainAsync_CaptureExceptionsOverATransientFault_DoesNotCacheTheFaultedVerdict()
    {
        int calls = 0;
        CachedAsyncBusinessRule cached = FaultsOnFirstCall(() => calls++).Cached(Ttl);
        BusinessRuleContext context = new BusinessRuleContext();
        RuleExplanation explanation = await cached.ExplainAsync(context, captureExceptions: true, TestToken);

        bool isBroken = await cached.IsBrokenAsync(context, TestToken);

        Assert.Equal(RuleOutcome.Broken, explanation.Outcome);
        Assert.Equal(RuleOutcome.Faulted, Assert.Single(explanation.Children).Outcome);
        Assert.False(isBroken);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ExplainAsync_CaptureExceptionsOverATransientFault_CachesTheNextRealVerdict()
    {
        int calls = 0;
        CachedAsyncBusinessRule cached = FaultsOnFirstCall(() => calls++).Cached(Ttl);
        await cached.ExplainAsync(new BusinessRuleContext(), captureExceptions: true, TestToken);
        await cached.IsBrokenAsync(new BusinessRuleContext(), TestToken);

        bool isBroken = await cached.IsBrokenAsync(new BusinessRuleContext(), TestToken);

        Assert.False(isBroken);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ExplainAsync_CaptureExceptionsOverAGroupWithAFaultingMember_DoesNotCacheTheVerdict()
    {
        int calls = 0;
        CachedAsyncBusinessRule cached = AsyncRuleGroup.All("GROUP", "group", FaultsOnFirstCall(() => calls++)).Cached(Ttl);
        await cached.ExplainAsync(new BusinessRuleContext(), captureExceptions: true, TestToken);

        await cached.IsBrokenAsync(new BusinessRuleContext(), TestToken);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ExplainAsync_CaptureExceptionsWithoutAFault_CachesTheVerdict()
    {
        int calls = 0;
        CachedAsyncBusinessRule cached = Counting(() => calls++, broken: true).Cached(Ttl);
        await cached.ExplainAsync(new BusinessRuleContext(), captureExceptions: true, TestToken);

        bool isBroken = await cached.IsBrokenAsync(new BusinessRuleContext(), TestToken);

        Assert.True(isBroken);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExplainAsync_PerKeyCacheOverATransientFault_DoesNotCacheTheKey()
    {
        int calls = 0;
        CachedAsyncBusinessRule cached = FaultsOnFirstCall(() => calls++).Cached(Ttl, ctx => ctx.Get<string>(TenantKey));
        BusinessRuleContext acme = Tenant("acme");
        await cached.ExplainAsync(acme, captureExceptions: true, TestToken);

        await cached.IsBrokenAsync(acme, TestToken);

        Assert.Equal(2, calls);
    }

    // ---------------------------------------------------------------------
    // One slow key never blocks another
    // ---------------------------------------------------------------------

    [Fact]
    public async Task IsBrokenAsync_SlowKeyEvaluating_DoesNotBlockAnotherKeysEvaluation()
    {
        TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        CachedAsyncBusinessRule cached = BlockingForSlowTenant(entered, release).Cached(Ttl, ctx => ctx.Get<string>(TenantKey));
        Task<bool> slow = cached.IsBrokenAsync(Tenant("slow"), TestToken);
        await entered.Task.WaitAsync(Patience, TestToken);

        bool fast = await cached.IsBrokenAsync(Tenant("fast"), TestToken).WaitAsync(Patience, TestToken);
        bool slowStillRunning = !slow.IsCompleted;
        release.SetResult(true);

        Assert.False(fast);
        Assert.True(slowStillRunning);
        Assert.True(await slow.WaitAsync(Patience, TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_SlowKeyEvaluating_ServesAnotherKeysCacheHit()
    {
        int fastCalls = 0;
        TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        CachedAsyncBusinessRule cached = BlockingForSlowTenant(entered, release, () => fastCalls++).Cached(Ttl, ctx => ctx.Get<string>(TenantKey));
        await cached.IsBrokenAsync(Tenant("fast"), TestToken);
        Task<bool> slow = cached.IsBrokenAsync(Tenant("slow"), TestToken);
        await entered.Task.WaitAsync(Patience, TestToken);

        bool hit = await cached.IsBrokenAsync(Tenant("fast"), TestToken).WaitAsync(Patience, TestToken);
        release.SetResult(true);

        Assert.False(hit);
        Assert.Equal(1, fastCalls);
        Assert.True(await slow.WaitAsync(Patience, TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_CancelledWhileAnotherKeyEvaluates_CancelsOnlyTheCaller()
    {
        TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        CachedAsyncBusinessRule cached = BlockingForSlowTenant(entered, release).Cached(Ttl, ctx => ctx.Get<string>(TenantKey));
        Task<bool> slow = cached.IsBrokenAsync(Tenant("slow"), TestToken);
        await entered.Task.WaitAsync(Patience, TestToken);
        using CancellationTokenSource cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cached.IsBrokenAsync(Tenant("fast"), cancelled.Token));
        release.SetResult(true);

        Assert.True(await slow.WaitAsync(Patience, TestToken));
    }

    // ---------------------------------------------------------------------
    // Invalidation while a verdict is computed
    // ---------------------------------------------------------------------

    [Fact]
    public async Task InvalidateCacheAsync_WhileAnEvaluationRuns_DiscardsTheStaleVerdict()
    {
        int calls = 0;
        bool broken = true;
        TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        IAsyncBusinessRule inner = AsyncBusinessRuleBuilder.Create("STALE").WithMessage("m").WhenAsync(async (ctx, ct) =>
        {
            bool verdict = broken;
            calls++;
            entered.TrySetResult(true);
            await release.Task;
            return verdict;
        }).Build();
        CachedAsyncBusinessRule cached = inner.Cached(Ttl);
        Task<bool> stale = cached.IsBrokenAsync(new BusinessRuleContext(), TestToken);
        await entered.Task.WaitAsync(Patience, TestToken);
        broken = false;
        await cached.InvalidateCacheAsync(TestToken).WaitAsync(Patience, TestToken);
        release.SetResult(true);
        bool staleVerdict = await stale.WaitAsync(Patience, TestToken);

        bool isBroken = await cached.IsBrokenAsync(new BusinessRuleContext(), TestToken);

        Assert.True(staleVerdict);
        Assert.False(isBroken);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Cached_WithoutAKeySelector_ReturnsTheConcreteCachedRule()
    {
        CachedAsyncBusinessRule cached = RuleTestHelper.CreateBrokenAsyncRule().Cached(Ttl);

        await cached.InvalidateCacheAsync(TestToken);

        Assert.False(cached.CachesPerKey);
    }

    // ---------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------

    private static IAsyncBusinessRule FaultsOnFirstCall(Action onEvaluate)
    {
        int evaluations = 0;
        return AsyncBusinessRuleBuilder.Create("TRANSIENT").WithMessage("transient").WhenAsync(ctx =>
        {
            onEvaluate();
            evaluations++;
            return evaluations == 1 ? throw new TimeoutException("transient") : Task.FromResult(false);
        }).Build();
    }

    private static IAsyncBusinessRule Counting(Action onEvaluate, bool broken)
    {
        return AsyncBusinessRuleBuilder.Create("COUNTING").WithMessage("counting").WhenAsync(ctx =>
        {
            onEvaluate();
            return Task.FromResult(broken);
        }).Build();
    }

    private static IAsyncBusinessRule BlockingForSlowTenant(TaskCompletionSource<bool> entered, TaskCompletionSource<bool> release, Action? onFastEvaluate = null)
    {
        return AsyncBusinessRuleBuilder.Create("PER.TENANT").WithMessage("m").WhenAsync(async (ctx, ct) =>
        {
            if (ctx.Get<string>(TenantKey) != "slow")
            {
                onFastEvaluate?.Invoke();
                return false;
            }

            entered.TrySetResult(true);
            await release.Task;
            return true;
        }).Build();
    }

    private static BusinessRuleContext Tenant(string tenant)
    {
        return RuleTestHelper.CreateContext((TenantKey, tenant));
    }
}
