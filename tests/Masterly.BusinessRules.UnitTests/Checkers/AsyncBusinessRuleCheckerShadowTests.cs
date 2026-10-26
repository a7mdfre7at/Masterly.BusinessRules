namespace Masterly.BusinessRules.UnitTests;

public class AsyncBusinessRuleCheckerShadowTests
{
    private const string ShadowCode = "SHADOW.RULE";
    private const string ShadowMessage = "Shadow rule would have failed";
    private const string PassingCode = "PASS";
    private const string BrokenCode = "BROKEN";
    private const string Flag = "credit-score-v2";
    private const string OtherFlag = "sanctions-check";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------
    // ShadowResults
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_WithOptions_ShadowViolation_IsCollectedNotBroken()
    {
        IAsyncBusinessRule shadow = CreateShadowRule();
        IAsyncBusinessRule passing = RuleTestHelper.CreatePassingAsyncRule(PassingCode);

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { shadow, passing }, new AsyncRuleEvaluationOptions(), TestToken);

        BusinessRuleResult shadowResult = Assert.Single(result.ShadowResults);
        Assert.Equal(ShadowCode, shadowResult.Code);
        Assert.Equal(ShadowMessage, shadowResult.Message);
        Assert.True(result.HasShadowResults);
        Assert.Empty(result.BrokenRules);
        Assert.True(result.AllPassed);
        Assert.Equal(2, result.PassedRules.Count);
        Assert.Equal(RuleOutcome.Passed, result.Entries[0].Outcome);
        Assert.Null(result.Entries[0].Result);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithOptions_ShadowRulePassing_CollectsNothing()
    {
        IAsyncBusinessRule shadow = RuleTestHelper.CreatePassingAsyncRule(ShadowCode).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { shadow }, new AsyncRuleEvaluationOptions(), TestToken);

        Assert.Empty(result.ShadowResults);
        Assert.False(result.HasShadowResults);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithOptions_EnforcedRule_IsBrokenNotShadow()
    {
        IAsyncBusinessRule enforced = RuleTestHelper.CreateBrokenAsyncRule(ShadowCode, ShadowMessage).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { enforced }, new AsyncRuleEvaluationOptions(), TestToken);

        Assert.Equal(ShadowCode, Assert.Single(result.BrokenRules).Code);
        Assert.Empty(result.ShadowResults);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithOptions_SeveralShadowViolations_AreCollectedInOrder()
    {
        IAsyncBusinessRule first = RuleTestHelper.CreateBrokenAsyncRule("S1").GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));
        IAsyncBusinessRule second = RuleTestHelper.CreateBrokenAsyncRule(BrokenCode);
        IAsyncBusinessRule third = RuleTestHelper.CreateBrokenAsyncRule("S3").GatedBy(OtherFlag, RuleTestHelper.CreateFlagProvider(OtherFlag, RuleRolloutMode.Shadow));

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { first, second, third }, new AsyncRuleEvaluationOptions(), TestToken);

        Assert.Equal(new[] { "S1", "S3" }, result.ShadowResults.Select(r => r.Code));
        Assert.Equal(BrokenCode, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public async Task EvaluateAllAsync_Parallel_CollectsEveryShadowViolation()
    {
        IAsyncBusinessRule[] rules = Enumerable.Range(0, 5)
            .Select(index => (IAsyncBusinessRule)RuleTestHelper.CreateBrokenAsyncRule($"S{index}").GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow)))
            .ToArray();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), rules, new AsyncRuleEvaluationOptions { RunInParallel = true }, TestToken);

        Assert.Equal(new[] { "S0", "S1", "S2", "S3", "S4" }, result.ShadowResults.Select(r => r.Code).OrderBy(code => code));
        Assert.True(result.AllPassed);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithoutContext_GatesAgainstAFreshContext()
    {
        IAsyncBusinessRule shadow = CreateShadowRule();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(shadow);

        Assert.Equal(ShadowCode, Assert.Single(result.ShadowResults).Code);
        Assert.Empty(result.BrokenRules);
    }

    [Fact]
    public async Task EvaluateAllAsync_BooleanOverload_CollectsShadowResults()
    {
        IAsyncBusinessRule shadow = CreateShadowRule();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { shadow }, runInParallel: false, TestToken);

        Assert.Equal(ShadowCode, Assert.Single(result.ShadowResults).Code);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithOptions_NestedShadowRuleInGroup_IsCollected()
    {
        IAsyncBusinessRule shadow = CreateShadowRule();
        IAsyncBusinessRule group = AsyncRuleGroup.All("GROUP", "group", shadow, RuleTestHelper.CreatePassingAsyncRule(PassingCode));

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { group }, new AsyncRuleEvaluationOptions(), TestToken);

        Assert.Equal(ShadowCode, Assert.Single(result.ShadowResults).Code);
        Assert.True(result.AllPassed);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithOptions_ShadowInsideCachedDecorator_IsCollected()
    {
        IAsyncBusinessRule cachedShadow = CreateShadowRule().Cached(TimeSpan.FromMinutes(5));

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { cachedShadow }, new AsyncRuleEvaluationOptions(), TestToken);

        Assert.Equal(ShadowCode, Assert.Single(result.ShadowResults).Code);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithOptions_StopOnFirstFailure_ShadowViolationDoesNotStopTheBatch()
    {
        IAsyncBusinessRule shadow = CreateShadowRule();
        IAsyncBusinessRule broken = RuleTestHelper.CreateBrokenAsyncRule(BrokenCode);

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { shadow, broken }, new AsyncRuleEvaluationOptions { StopOnFirstFailure = true }, TestToken);

        Assert.Equal(RuleOutcome.Broken, result.Entries[1].Outcome);
        Assert.Single(result.ShadowResults);
    }

    // ---------------------------------------------------------------------
    // CheckAllAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CheckAllAsync_WithOptions_ShadowViolation_DoesNotThrow()
    {
        IAsyncBusinessRule shadow = CreateShadowRule();

        await AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), new[] { shadow }, new AsyncRuleEvaluationOptions(), TestToken);
    }

    [Fact]
    public async Task CheckAllAsync_WithOptions_ShadowViolation_ReachesOnBatchCompletedResult()
    {
        RecordingAsyncObserver observer = new RecordingAsyncObserver();
        IAsyncBusinessRule shadow = CreateShadowRule();

        await AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), new[] { shadow }, new AsyncRuleEvaluationOptions { Observer = observer }, TestToken);

        Assert.NotNull(observer.Completed);
        Assert.Equal(ShadowCode, Assert.Single(observer.Completed!.ShadowResults).Code);
    }

    [Fact]
    public async Task CheckAllAsync_EnforcedViolation_Throws()
    {
        IAsyncBusinessRule enforced = RuleTestHelper.CreateBrokenAsyncRule(ShadowCode).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        await Assert.ThrowsAsync<BusinessRuleValidationException>(() => AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), new[] { enforced }, new AsyncRuleEvaluationOptions(), TestToken));
    }

    // ---------------------------------------------------------------------
    // Observer
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_WithObserver_ShadowViolation_RaisesOnShadowViolationAsyncWithGatedRule()
    {
        RecordingAsyncObserver observer = new RecordingAsyncObserver();
        FeatureGatedAsyncBusinessRule shadow = CreateShadowRule();

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { (IAsyncBusinessRule)shadow }, new AsyncRuleEvaluationOptions { Observer = observer }, TestToken);

        (IAsyncBusinessRule rule, BusinessRuleResult result) = Assert.Single(observer.ShadowViolations);
        Assert.Same(shadow, rule);
        Assert.Equal(ShadowCode, result.Code);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithObserver_ShadowViolation_IsReportedAsPassedByTheOtherCallbacks()
    {
        RecordingAsyncObserver observer = new RecordingAsyncObserver();
        IAsyncBusinessRule shadow = CreateShadowRule();

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { shadow }, new AsyncRuleEvaluationOptions { Observer = observer }, TestToken);

        Assert.Empty(observer.Broken);
        Assert.Null(Assert.Single(observer.AfterResults));
        Assert.False(Assert.Single(observer.AfterVerdicts));
    }

    [Fact]
    public async Task EvaluateAllAsync_WithObserver_CallbackOrder_ShadowViolationFollowsTheRuleCallbacks()
    {
        RecordingAsyncObserver observer = new RecordingAsyncObserver();
        IAsyncBusinessRule shadow = CreateShadowRule();

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { shadow }, new AsyncRuleEvaluationOptions { Observer = observer }, TestToken);

        Assert.Equal(new[] { "BatchStarted", "Before", "AfterResult", "AfterVerdict", "Shadow", "BatchCompleted" }, observer.Sequence);
    }

    [Fact]
    public async Task EvaluateAllAsync_Parallel_WithObserver_RaisesOneShadowCallbackPerViolation()
    {
        RecordingAsyncObserver observer = new RecordingAsyncObserver();
        IAsyncBusinessRule[] rules = Enumerable.Range(0, 3)
            .Select(index => (IAsyncBusinessRule)RuleTestHelper.CreateBrokenAsyncRule($"S{index}").GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow)))
            .ToArray();

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), rules, new AsyncRuleEvaluationOptions { RunInParallel = true, Observer = observer }, TestToken);

        Assert.Equal(3, observer.ShadowViolations.Count);
        Assert.Equal("BatchCompleted", observer.Sequence.Last());
    }

    [Fact]
    public async Task EvaluateAllAsync_WithObserver_NestedShadowRule_ReportsTheGatedRuleNotTheGroup()
    {
        RecordingAsyncObserver observer = new RecordingAsyncObserver();
        FeatureGatedAsyncBusinessRule shadow = CreateShadowRule();
        IAsyncBusinessRule group = AsyncRuleGroup.All("GROUP", "group", shadow);

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { group }, new AsyncRuleEvaluationOptions { Observer = observer }, TestToken);

        Assert.Same(shadow, Assert.Single(observer.ShadowViolations).Rule);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithObserver_SyncShadowRuleThroughAdapter_IsWrappedForTheObserver()
    {
        RecordingAsyncObserver observer = new RecordingAsyncObserver();
        FeatureGatedBusinessRule syncShadow = RuleTestHelper.CreateBrokenRule(ShadowCode).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));
        IAsyncBusinessRule adapted = syncShadow.ToAsync();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { adapted }, new AsyncRuleEvaluationOptions { Observer = observer }, TestToken);

        Assert.Equal(ShadowCode, Assert.Single(result.ShadowResults).Code);
        SyncToAsyncRuleAdapter reported = Assert.IsType<SyncToAsyncRuleAdapter>(Assert.Single(observer.ShadowViolations).Rule);
        Assert.Same(syncShadow, reported.InnerRule);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithObserverNotOverridingTheHook_DoesNotThrow()
    {
        MinimalAsyncObserver observer = new MinimalAsyncObserver();
        IAsyncBusinessRule shadow = CreateShadowRule();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { shadow }, new AsyncRuleEvaluationOptions { Observer = observer }, TestToken);

        Assert.Single(result.ShadowResults);
        Assert.Equal(1, observer.BeforeCount);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithObserver_ForwardsTheCancellationTokenToOnShadowViolationAsync()
    {
        RecordingAsyncObserver observer = new RecordingAsyncObserver();
        IAsyncBusinessRule shadow = CreateShadowRule();
        using CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(TestToken);

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { shadow }, new AsyncRuleEvaluationOptions { Observer = observer }, source.Token);

        Assert.Equal(source.Token, Assert.Single(observer.ShadowTokens));
    }

    // ---------------------------------------------------------------------
    // Context hygiene and cancellation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_WithOptions_LeavesTheContextStoreUntouched()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext(("customerId", "c-1"));
        IAsyncBusinessRule shadow = CreateShadowRule();

        await AsyncBusinessRuleChecker.EvaluateAllAsync(context, new[] { shadow }, new AsyncRuleEvaluationOptions(), TestToken);

        Assert.Equal(new[] { "customerId" }, context.Keys);
        Assert.Equal(1, context.Count);
    }

    [Fact]
    public async Task EvaluateAllAsync_ShadowPlumbingNeverAppearsInTheContextStoreDuringTheBatch()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        int keysSeenDuringBatch = -1;
        IAsyncBusinessRule probe = AsyncBusinessRuleBuilder.Create("PROBE").WithMessage("probe").WhenAsync(ctx => { keysSeenDuringBatch = ctx.Count; return Task.FromResult(false); }).Build();

        await AsyncBusinessRuleChecker.EvaluateAllAsync(context, new[] { probe, CreateShadowRule() }, new AsyncRuleEvaluationOptions(), TestToken);

        Assert.Equal(0, keysSeenDuringBatch);
        Assert.Equal(0, context.Count);
    }

    [Fact]
    public async Task EvaluateAllAsync_WhenAFaultPropagates_LeavesTheContextStoreUntouched()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        IAsyncBusinessRule throwing = AsyncBusinessRuleBuilder.Create("THROWS").WithMessage("throws").WhenAsync(ctx => throw new InvalidOperationException("boom")).Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => AsyncBusinessRuleChecker.EvaluateAllAsync(context, new[] { throwing }, new AsyncRuleEvaluationOptions(), TestToken));

        Assert.Equal(0, context.Count);
    }

    [Fact]
    public async Task EvaluateAllAsync_CancelledToken_PropagatesAndRemovesTheSink()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        IAsyncBusinessRule shadow = CreateShadowRule();
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AsyncBusinessRuleChecker.EvaluateAllAsync(context, new[] { shadow }, new AsyncRuleEvaluationOptions(), source.Token));

        Assert.Equal(0, context.Count);
    }

    // ---------------------------------------------------------------------
    // Typed checker projection
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_TypedOverload_CollectsShadowResults()
    {
        IAsyncBusinessRule<string> typed = AsyncBusinessRuleBuilder<string>.Create(ShadowCode).WithMessage(ShadowMessage).WhenAsync(subject => Task.FromResult(subject.Length > 0)).Build();
        IAsyncBusinessRule shadow = typed.For("subject").GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { shadow }, new AsyncRuleEvaluationOptions(), TestToken);

        Assert.Equal(ShadowCode, Assert.Single(result.ShadowResults).Code);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static FeatureGatedAsyncBusinessRule CreateShadowRule()
    {
        return RuleTestHelper.CreateBrokenAsyncRule(ShadowCode, ShadowMessage).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));
    }

    private sealed class RecordingAsyncObserver : IAsyncRuleExecutionObserver
    {
        private readonly object _lock = new object();

        public List<string> Sequence { get; } = new List<string>();
        public List<IAsyncBusinessRule> Broken { get; } = new List<IAsyncBusinessRule>();
        public List<BusinessRuleResult?> AfterResults { get; } = new List<BusinessRuleResult?>();
        public List<bool> AfterVerdicts { get; } = new List<bool>();
        public List<(IAsyncBusinessRule Rule, BusinessRuleResult Result)> ShadowViolations { get; } = new List<(IAsyncBusinessRule, BusinessRuleResult)>();
        public List<CancellationToken> ShadowTokens { get; } = new List<CancellationToken>();
        public AsyncRuleEvaluationResult? Completed { get; private set; }

        public Task OnBeforeEvaluateAsync(IAsyncBusinessRule rule, CancellationToken cancellationToken = default)
        {
            Record("Before");
            return Task.CompletedTask;
        }

        public Task OnAfterEvaluateAsync(IAsyncBusinessRule rule, BusinessRuleResult? result, CancellationToken cancellationToken = default)
        {
            Record("AfterResult");
            lock (_lock)
            {
                AfterResults.Add(result);
            }

            return Task.CompletedTask;
        }

        public Task OnRuleBrokenAsync(IAsyncBusinessRule rule, BusinessRuleResult result, CancellationToken cancellationToken = default)
        {
            Record("Broken");
            lock (_lock)
            {
                Broken.Add(rule);
            }

            return Task.CompletedTask;
        }

        public Task OnBatchStartedAsync(int ruleCount, CancellationToken cancellationToken = default)
        {
            Record("BatchStarted");
            return Task.CompletedTask;
        }

        public Task OnAfterEvaluateAsync(IAsyncBusinessRule rule, bool isBroken, TimeSpan elapsed, CancellationToken cancellationToken = default)
        {
            Record("AfterVerdict");
            lock (_lock)
            {
                AfterVerdicts.Add(isBroken);
            }

            return Task.CompletedTask;
        }

        public Task OnBatchCompletedAsync(AsyncRuleEvaluationResult result, CancellationToken cancellationToken = default)
        {
            Record("BatchCompleted");
            Completed = result;
            return Task.CompletedTask;
        }

        public Task OnShadowViolationAsync(IAsyncBusinessRule rule, BusinessRuleResult result, CancellationToken cancellationToken = default)
        {
            Record("Shadow");
            lock (_lock)
            {
                ShadowViolations.Add((rule, result));
                ShadowTokens.Add(cancellationToken);
            }

            return Task.CompletedTask;
        }

        private void Record(string step)
        {
            lock (_lock)
            {
                Sequence.Add(step);
            }
        }
    }

    private sealed class MinimalAsyncObserver : IAsyncRuleExecutionObserver
    {
        public int BeforeCount { get; private set; }

        public Task OnBeforeEvaluateAsync(IAsyncBusinessRule rule, CancellationToken cancellationToken = default)
        {
            BeforeCount++;
            return Task.CompletedTask;
        }

        public Task OnAfterEvaluateAsync(IAsyncBusinessRule rule, BusinessRuleResult? result, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OnRuleBrokenAsync(IAsyncBusinessRule rule, BusinessRuleResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
