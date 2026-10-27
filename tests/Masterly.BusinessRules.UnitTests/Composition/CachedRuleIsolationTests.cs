namespace Masterly.BusinessRules.UnitTests;

/// <summary>
/// What a <see cref="CachedBusinessRule"/> must not cache (a verdict resting on a captured fault, a verdict invalidated
/// while it was computed) and that one slow key never blocks the others.
/// </summary>
public class CachedRuleIsolationTests
{
    private const string TenantKey = "tenant";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------
    // Captured faults are never cached
    // ---------------------------------------------------------------------

    [Fact]
    public void Explain_CaptureExceptionsOverATransientFault_DoesNotCacheTheFaultedVerdict()
    {
        int calls = 0;
        CachedBusinessRule cached = FaultsOnFirstCall(() => calls++).Cached(Ttl);
        BusinessRuleContext context = new BusinessRuleContext();
        RuleExplanation explanation = cached.Explain(context, captureExceptions: true);

        bool isBroken = cached.IsBroken(context);

        Assert.Equal(RuleOutcome.Broken, explanation.Outcome);
        Assert.Equal(RuleOutcome.Faulted, Assert.Single(explanation.Children).Outcome);
        Assert.False(isBroken);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Explain_CaptureExceptionsOverATransientFault_CachesTheNextRealVerdict()
    {
        int calls = 0;
        CachedBusinessRule cached = FaultsOnFirstCall(() => calls++).Cached(Ttl);
        cached.Explain(captureExceptions: true);
        cached.IsBroken();

        bool isBroken = cached.IsBroken();

        Assert.False(isBroken);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Explain_CaptureExceptionsOverAGroupWithAFaultingMember_DoesNotCacheTheVerdict()
    {
        int calls = 0;
        CachedBusinessRule cached = RuleGroup.All("GROUP", "group", FaultsOnFirstCall(() => calls++)).Cached(Ttl);
        cached.Explain(new BusinessRuleContext(), captureExceptions: true);

        cached.IsBroken(new BusinessRuleContext());

        Assert.Equal(2, calls);
    }

    [Fact]
    public void Explain_CaptureExceptionsWithoutAFault_CachesTheVerdict()
    {
        int calls = 0;
        CachedBusinessRule cached = Counting(() => calls++, broken: true).Cached(Ttl);
        cached.Explain(new BusinessRuleContext(), captureExceptions: true);

        bool isBroken = cached.IsBroken(new BusinessRuleContext());

        Assert.True(isBroken);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Explain_PerKeyCacheOverATransientFault_DoesNotCacheTheKey()
    {
        int calls = 0;
        CachedBusinessRule cached = FaultsOnFirstCall(() => calls++).Cached(Ttl, ctx => ctx.Get<string>(TenantKey));
        BusinessRuleContext acme = Tenant("acme");
        cached.Explain(acme, captureExceptions: true);

        cached.IsBroken(acme);

        Assert.Equal(2, calls);
    }

    // ---------------------------------------------------------------------
    // One slow key never blocks another
    // ---------------------------------------------------------------------

    [Fact]
    public async Task IsBroken_SlowKeyEvaluating_DoesNotBlockAnotherKeysEvaluation()
    {
        using ManualResetEventSlim entered = new ManualResetEventSlim();
        using ManualResetEventSlim release = new ManualResetEventSlim();
        CachedBusinessRule cached = BlockingForSlowTenant(entered, release).Cached(Ttl, ctx => ctx.Get<string>(TenantKey));
        Task<bool> slow = Task.Run(() => cached.IsBroken(Tenant("slow")), TestToken);
        entered.Wait(Patience, TestToken);

        bool fast = await Task.Run(() => cached.IsBroken(Tenant("fast")), TestToken).WaitAsync(Patience, TestToken);
        bool slowStillRunning = !slow.IsCompleted;
        release.Set();

        Assert.False(fast);
        Assert.True(slowStillRunning);
        Assert.True(await slow.WaitAsync(Patience, TestToken));
    }

    [Fact]
    public async Task IsBroken_SlowKeyEvaluating_ServesAnotherKeysCacheHit()
    {
        int fastCalls = 0;
        using ManualResetEventSlim entered = new ManualResetEventSlim();
        using ManualResetEventSlim release = new ManualResetEventSlim();
        CachedBusinessRule cached = BlockingForSlowTenant(entered, release, () => fastCalls++).Cached(Ttl, ctx => ctx.Get<string>(TenantKey));
        cached.IsBroken(Tenant("fast"));
        Task<bool> slow = Task.Run(() => cached.IsBroken(Tenant("slow")), TestToken);
        entered.Wait(Patience, TestToken);

        bool hit = await Task.Run(() => cached.IsBroken(Tenant("fast")), TestToken).WaitAsync(Patience, TestToken);
        release.Set();

        Assert.False(hit);
        Assert.Equal(1, fastCalls);
        Assert.True(await slow.WaitAsync(Patience, TestToken));
    }

    // ---------------------------------------------------------------------
    // Invalidation while a verdict is computed
    // ---------------------------------------------------------------------

    [Fact]
    public async Task InvalidateCache_WhileAnEvaluationRuns_DiscardsTheStaleVerdict()
    {
        int calls = 0;
        bool broken = true;
        using ManualResetEventSlim entered = new ManualResetEventSlim();
        using ManualResetEventSlim release = new ManualResetEventSlim();
        IBusinessRule inner = BusinessRuleBuilder.Create("STALE").WithMessage("m").When(() =>
        {
            bool verdict = broken;
            calls++;
            entered.Set();
            release.Wait(Patience);
            return verdict;
        }).Build();
        CachedBusinessRule cached = inner.Cached(Ttl);
        Task<bool> stale = Task.Run(() => cached.IsBroken(), TestToken);
        entered.Wait(Patience, TestToken);
        broken = false;
        await Task.Run(() => cached.InvalidateCache(), TestToken).WaitAsync(Patience, TestToken);
        release.Set();
        bool staleVerdict = await stale.WaitAsync(Patience, TestToken);

        bool isBroken = cached.IsBroken();

        Assert.True(staleVerdict);
        Assert.False(isBroken);
        Assert.Equal(2, calls);
    }

    // ---------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------

    private static IBusinessRule FaultsOnFirstCall(Action onEvaluate)
    {
        int evaluations = 0;
        return BusinessRuleBuilder.Create("TRANSIENT").WithMessage("transient").When(() =>
        {
            onEvaluate();
            evaluations++;
            return evaluations == 1 ? throw new TimeoutException("transient") : false;
        }).Build();
    }

    private static IBusinessRule Counting(Action onEvaluate, bool broken)
    {
        return BusinessRuleBuilder.Create("COUNTING").WithMessage("counting").When(() =>
        {
            onEvaluate();
            return broken;
        }).Build();
    }

    private static IBusinessRule BlockingForSlowTenant(ManualResetEventSlim entered, ManualResetEventSlim release, Action? onFastEvaluate = null)
    {
        return BusinessRuleBuilder.Create("PER.TENANT").WithMessage("m").When(ctx =>
        {
            if (ctx.Get<string>(TenantKey) != "slow")
            {
                onFastEvaluate?.Invoke();
                return false;
            }

            entered.Set();
            release.Wait(Patience);
            return true;
        }).Build();
    }

    private static BusinessRuleContext Tenant(string tenant)
    {
        return RuleTestHelper.CreateContext((TenantKey, tenant));
    }
}
