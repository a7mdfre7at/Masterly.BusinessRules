namespace Masterly.BusinessRules.UnitTests;

public class AsyncBusinessRuleCheckerLedgerTests
{
    private const string PassingCode = "PASS";
    private const string BrokenCode = "BROKEN";
    private const string ThrowingCode = "THROWS";
    private const string FlagKey = "flag";
    private const string Boom = "boom";

    private static readonly TimeSpan SlowRuleDuration = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan ParallelTimeout = TimeSpan.FromSeconds(10);

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------
    // Ledger shape
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_WithOptions_RecordsOneEntryPerRuleInSubmissionOrder()
    {
        IAsyncBusinessRule first = CreateRule("A", broken: false);
        IAsyncBusinessRule second = CreateRule("B", broken: true);
        IAsyncBusinessRule third = CreateRule("C", broken: false);

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new[] { first, second, third }, new AsyncRuleEvaluationOptions(), TestToken);

        Assert.Equal(3, result.Entries.Count);
        Assert.Equal(new[] { 0, 1, 2 }, result.Entries.Select(e => e.Order));
        Assert.Equal(new[] { "A", "B", "C" }, result.Entries.Select(e => e.Code));
        Assert.Equal(new[] { RuleOutcome.Passed, RuleOutcome.Broken, RuleOutcome.Passed }, result.Entries.Select(e => e.Outcome));
        Assert.Same(second, result.Entries[1].AsyncRule);
        Assert.Null(result.Entries[1].Rule);
        Assert.NotNull(result.Entries[1].Result);
        Assert.All(result.Entries, e => Assert.Null(e.Fault));
    }

    [Fact]
    public async Task EvaluateAllAsync_WithOptions_DerivesBrokenAndPassedRulesFromLedger()
    {
        IAsyncBusinessRule passing = CreateRule(PassingCode, broken: false);
        IAsyncBusinessRule broken = CreateRule(BrokenCode, broken: true);

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new[] { passing, broken }, new AsyncRuleEvaluationOptions(), TestToken);

        Assert.Equal(BrokenCode, Assert.Single(result.BrokenRules).Code);
        Assert.Same(passing, Assert.Single(result.PassedRules));
    }

    [Fact]
    public async Task EvaluateAllAsync_WithOptions_EmptyRules_ReturnsEmptyLedger()
    {
        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(Array.Empty<IAsyncBusinessRule>(), new AsyncRuleEvaluationOptions(), TestToken);

        Assert.Empty(result.Entries);
        Assert.Null(result.Slowest);
        Assert.True(result.AllPassed);
        Assert.False(result.HasFaults);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithOptions_TotalDurationCoversEveryEntry()
    {
        IAsyncBusinessRule slow = CreateSlowRule("SLOW", SlowRuleDuration);
        IAsyncBusinessRule fast = CreateRule("FAST", broken: false);

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new[] { slow, fast }, new AsyncRuleEvaluationOptions(), TestToken);

        Assert.Equal("SLOW", result.Slowest!.Code);
        Assert.All(result.Entries, e => Assert.True(result.TotalDuration >= e.Duration));
    }

    [Fact]
    public async Task EvaluateAllAsync_WithOptions_SlowestIsTheSlowRule()
    {
        IAsyncBusinessRule fast = CreateRule("FAST", broken: false);
        IAsyncBusinessRule slow = CreateSlowRule("SLOW", SlowRuleDuration);

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new[] { fast, slow }, new AsyncRuleEvaluationOptions(), TestToken);

        Assert.Equal("SLOW", result.Slowest!.Code);
        Assert.True(result.Slowest.Duration >= TimeSpan.FromMilliseconds(5));
    }

    [Fact]
    public async Task EvaluateAllAsync_LegacyOverload_AlsoRecordsLedger()
    {
        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(CreateRule("A", broken: false), CreateRule("B", broken: true));

        Assert.Equal(2, result.Entries.Count);
        Assert.Equal(RuleOutcome.Broken, result.Entries[1].Outcome);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithOptions_ComposedRule_SurfacesMemberDetails()
    {
        IAsyncBusinessRule group = AsyncRuleGroup.All("GROUP", "group", CreateRule("M1", broken: true), CreateRule("M2", broken: false));

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new[] { group }, new AsyncRuleEvaluationOptions(), TestToken);

        RuleEvaluationEntry entry = Assert.Single(result.Entries);
        Assert.Equal("M1", Assert.Single(entry.Result!.Details).Code);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithOptionsAndContext_ForwardsContextToRules()
    {
        IAsyncBusinessRule contextRule = AsyncBusinessRuleBuilder.Create("CTX").WithMessage("ctx").WhenAsync(ctx => Task.FromResult(ctx.Get<bool>(FlagKey))).Build();
        BusinessRuleContext context = RuleTestHelper.CreateContext((FlagKey, true));

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(context, new[] { contextRule }, new AsyncRuleEvaluationOptions(), TestToken);

        Assert.True(result.HasBrokenRules);
    }

    // ---------------------------------------------------------------------
    // Parallel
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_Parallel_KeepsLedgerInSubmissionOrder()
    {
        IAsyncBusinessRule slowest = CreateSlowRule("A", TimeSpan.FromMilliseconds(40));
        IAsyncBusinessRule middle = CreateSlowRule("B", TimeSpan.FromMilliseconds(15));
        IAsyncBusinessRule fastest = CreateRule("C", broken: true);

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new[] { slowest, middle, fastest }, new AsyncRuleEvaluationOptions { RunInParallel = true }, TestToken);

        Assert.Equal(new[] { "A", "B", "C" }, result.Entries.Select(e => e.Code));
        Assert.Equal(new[] { 0, 1, 2 }, result.Entries.Select(e => e.Order));
        Assert.Equal("C", Assert.Single(result.BrokenRules).Code);
        Assert.Equal(2, result.PassedRules.Count);
    }

    [Fact]
    public async Task EvaluateAllAsync_Parallel_RunsRulesConcurrently()
    {
        const int ruleCount = 4;
        int started = 0;
        TaskCompletionSource<bool> allStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        IAsyncBusinessRule[] rules = Enumerable.Range(0, ruleCount).Select(i => AsyncBusinessRuleBuilder.Create($"R{i}").WithMessage("r")
            .WhenAsync(async (ctx, ct) =>
            {
                if (Interlocked.Increment(ref started) == ruleCount)
                {
                    allStarted.TrySetResult(true);
                }

                // Every rule waits until all four have started: a sequential batch would time out here.
                await allStarted.Task.WaitAsync(ParallelTimeout, ct);
                return false;
            }).Build()).ToArray();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(rules, new AsyncRuleEvaluationOptions { RunInParallel = true }, TestToken);

        Assert.Equal(ruleCount, started);
        Assert.True(result.AllPassed);
    }

    [Fact]
    public async Task EvaluateAllAsync_Parallel_FaultPolicyRecord_IsolatesFaultPerTask()
    {
        IAsyncBusinessRule[] rules = { CreateThrowingRule(ThrowingCode), CreateRule(BrokenCode, broken: true), CreateRule(PassingCode, broken: false) };

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(rules, new AsyncRuleEvaluationOptions { RunInParallel = true, OnFault = FaultPolicy.Record }, TestToken);

        Assert.True(result.HasFaults);
        Assert.Equal(RuleOutcome.Faulted, result.Entries[0].Outcome);
        Assert.Equal(BrokenCode, Assert.Single(result.BrokenRules).Code);
        Assert.Single(result.PassedRules);
    }

    [Fact]
    public async Task EvaluateAllAsync_Parallel_FaultPolicyThrow_PropagatesTheException()
    {
        IAsyncBusinessRule[] rules = { CreateRule(PassingCode, broken: false), CreateThrowingRule(ThrowingCode) };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => AsyncBusinessRuleChecker.EvaluateAllAsync(rules, new AsyncRuleEvaluationOptions { RunInParallel = true }, TestToken));
    }

    [Fact]
    public async Task EvaluateAllAsync_ParallelWithStopOnFirstFailure_FailFastWinsAndRunsSequentially()
    {
        int secondEvaluations = 0;
        IAsyncBusinessRule broken = CreateRule(BrokenCode, broken: true);
        IAsyncBusinessRule counting = AsyncBusinessRuleBuilder.Create("COUNT").WithMessage("count")
            .WhenAsync(ctx => { secondEvaluations++; return Task.FromResult(false); }).Build();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(
            new[] { broken, counting }, new AsyncRuleEvaluationOptions { RunInParallel = true, StopOnFirstFailure = true }, TestToken);

        Assert.Equal(0, secondEvaluations);
        Assert.Equal(RuleOutcome.NotEvaluated, result.Entries[1].Outcome);
    }

    // ---------------------------------------------------------------------
    // Fault policy
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_FaultPolicyThrow_PropagatesTheException()
    {
        IAsyncBusinessRule[] rules = { CreateThrowingRule(ThrowingCode), CreateRule(PassingCode, broken: false) };

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => AsyncBusinessRuleChecker.EvaluateAllAsync(rules, new AsyncRuleEvaluationOptions(), TestToken));

        Assert.Equal(Boom, exception.Message);
    }

    [Fact]
    public async Task EvaluateAllAsync_FaultPolicyThrow_RaisesOnRuleFaultedButNotOnBatchCompleted()
    {
        RecordingAsyncObserver observer = new RecordingAsyncObserver();
        IAsyncBusinessRule throwing = CreateThrowingRule(ThrowingCode);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => AsyncBusinessRuleChecker.EvaluateAllAsync(new[] { throwing }, new AsyncRuleEvaluationOptions { Observer = observer }, TestToken));

        Assert.Same(throwing, Assert.Single(observer.Faulted).rule);
        Assert.Null(observer.CompletedResult);
        Assert.Equal(0, observer.AfterCount);
    }

    [Fact]
    public async Task EvaluateAllAsync_FaultPolicyRecord_RecordsFaultAndContinues()
    {
        IAsyncBusinessRule throwing = CreateThrowingRule(ThrowingCode);
        IAsyncBusinessRule broken = CreateRule(BrokenCode, broken: true);
        IAsyncBusinessRule passing = CreateRule(PassingCode, broken: false);

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new[] { throwing, broken, passing }, new AsyncRuleEvaluationOptions { OnFault = FaultPolicy.Record }, TestToken);

        RuleEvaluationEntry faulted = result.Entries[0];
        Assert.Equal(RuleOutcome.Faulted, faulted.Outcome);
        Assert.IsType<InvalidOperationException>(faulted.Fault);
        Assert.Null(faulted.Result);
        Assert.Same(faulted, Assert.Single(result.Faulted));
        Assert.Equal(BrokenCode, Assert.Single(result.BrokenRules).Code);
        Assert.Same(passing, Assert.Single(result.PassedRules));
    }

    [Fact]
    public async Task EvaluateAllAsync_FaultPolicyRecord_RaisesOnRuleFaultedAndSkipsAfterEvaluate()
    {
        RecordingAsyncObserver observer = new RecordingAsyncObserver();

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new[] { CreateThrowingRule(ThrowingCode) }, new AsyncRuleEvaluationOptions { OnFault = FaultPolicy.Record, Observer = observer }, TestToken);

        Assert.Single(observer.Faulted);
        Assert.Equal(0, observer.AfterCount);
        Assert.Equal(0, observer.ElapsedCount);
        Assert.Equal(0, observer.BrokenCount);
        Assert.NotNull(observer.CompletedResult);
    }

    [Fact]
    public async Task EvaluateAllAsync_FaultPolicyTreatAsBroken_AddsResultWithRuleMetadata()
    {
        IAsyncBusinessRule throwing = AsyncBusinessRuleBuilder.Create(ThrowingCode)
            .WithMessage("Throwing rule")
            .WithSeverity(RuleSeverity.Warning)
            .WhenAsync(ctx => throw new InvalidOperationException(Boom))
            .Build();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new[] { throwing }, new AsyncRuleEvaluationOptions { OnFault = FaultPolicy.TreatAsBroken }, TestToken);

        RuleEvaluationEntry entry = Assert.Single(result.Entries);
        Assert.Equal(RuleOutcome.Faulted, entry.Outcome);
        Assert.True(entry.IsBroken);
        BusinessRuleResult brokenResult = Assert.Single(result.BrokenRules);
        Assert.Equal(ThrowingCode, brokenResult.Code);
        Assert.Equal("Throwing rule", brokenResult.Message);
        Assert.Equal(RuleSeverity.Warning, brokenResult.Severity);
        Assert.Empty(result.PassedRules);
    }

    [Fact]
    public async Task EvaluateAllAsync_FaultPolicyTreatAsBroken_RaisesOnRuleBroken()
    {
        RecordingAsyncObserver observer = new RecordingAsyncObserver();

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new[] { CreateThrowingRule(ThrowingCode) }, new AsyncRuleEvaluationOptions { OnFault = FaultPolicy.TreatAsBroken, Observer = observer }, TestToken);

        Assert.Single(observer.Faulted);
        Assert.Equal(1, observer.BrokenCount);
    }

    [Fact]
    public async Task EvaluateAllAsync_FaultPolicyTreatAsBroken_ThrowingMessageGetter_FallsBackWithoutMaskingTheFault()
    {
        ThrowingMessageAsyncRule rule = new ThrowingMessageAsyncRule();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new IAsyncBusinessRule[] { rule }, new AsyncRuleEvaluationOptions { OnFault = FaultPolicy.TreatAsBroken }, TestToken);

        RuleEvaluationEntry entry = Assert.Single(result.Entries);
        Assert.IsType<InvalidOperationException>(entry.Fault);
        Assert.Equal(nameof(ThrowingMessageAsyncRule), entry.Result!.Code);
        Assert.Contains("Rule evaluation faulted", entry.Result.Message);
        Assert.Equal(RuleSeverity.Error, entry.Result.Severity);
    }

    [Theory]
    [InlineData(FaultPolicy.Record)]
    [InlineData(FaultPolicy.TreatAsBroken)]
    public async Task EvaluateAllAsync_ThrowingCodeGetter_RecordsTheFaultWithoutMaskingIt(FaultPolicy policy)
    {
        ThrowingCodeAsyncRule rule = new ThrowingCodeAsyncRule();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new IAsyncBusinessRule[] { rule }, new AsyncRuleEvaluationOptions { OnFault = policy }, TestToken);

        RuleEvaluationEntry entry = Assert.Single(result.Entries);
        Assert.Equal(RuleOutcome.Faulted, entry.Outcome);
        Assert.IsType<InvalidOperationException>(entry.Fault);
        Assert.Equal(Boom, entry.Fault!.Message);
        Assert.Throws<NotSupportedException>(() => entry.Code);
    }

    [Fact]
    public async Task EvaluateAllAsync_FaultPolicyTreatAsBroken_ThrowingCodeGetter_FallsBackToTypeName()
    {
        ThrowingCodeAsyncRule rule = new ThrowingCodeAsyncRule();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new IAsyncBusinessRule[] { rule }, new AsyncRuleEvaluationOptions { OnFault = FaultPolicy.TreatAsBroken }, TestToken);

        Assert.Equal(nameof(ThrowingCodeAsyncRule), Assert.Single(result.BrokenRules).Code);
    }

    [Theory]
    [InlineData(FaultPolicy.Throw)]
    [InlineData(FaultPolicy.Record)]
    [InlineData(FaultPolicy.TreatAsBroken)]
    public async Task EvaluateAllAsync_RuleThrowsOperationCanceled_AlwaysPropagates(FaultPolicy policy)
    {
        IAsyncBusinessRule cancelling = AsyncBusinessRuleBuilder.Create("CANCEL").WithMessage("cancel").WhenAsync(ctx => throw new OperationCanceledException()).Build();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => AsyncBusinessRuleChecker.EvaluateAllAsync(new[] { cancelling }, new AsyncRuleEvaluationOptions { OnFault = policy }, TestToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EvaluateAllAsync_PreCancelledToken_ThrowsEvenWithRecordPolicy(bool runInParallel)
    {
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();
        IAsyncBusinessRule[] rules = { CreateRule(PassingCode, broken: false) };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => AsyncBusinessRuleChecker.EvaluateAllAsync(rules, new AsyncRuleEvaluationOptions { OnFault = FaultPolicy.Record, RunInParallel = runInParallel }, source.Token));
    }

    // ---------------------------------------------------------------------
    // Fail-fast
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_StopOnFirstFailure_MarksRemainingRulesNotEvaluated()
    {
        int thirdEvaluations = 0;
        IAsyncBusinessRule first = CreateRule("A", broken: false);
        IAsyncBusinessRule second = CreateRule("B", broken: true);
        IAsyncBusinessRule third = AsyncBusinessRuleBuilder.Create("C").WithMessage("c").WhenAsync(ctx => { thirdEvaluations++; return Task.FromResult(false); }).Build();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new[] { first, second, third }, new AsyncRuleEvaluationOptions { StopOnFirstFailure = true }, TestToken);

        Assert.Equal(3, result.Entries.Count);
        Assert.Equal(RuleOutcome.NotEvaluated, result.Entries[2].Outcome);
        Assert.Equal(TimeSpan.Zero, result.Entries[2].Duration);
        Assert.Equal(2, result.Entries[2].Order);
        Assert.Equal(0, thirdEvaluations);
        Assert.Same(first, Assert.Single(result.PassedRules));
    }

    [Fact]
    public async Task EvaluateAllAsync_WithOptions_NeverThrowsForBrokenRulesRegardlessOfThrowOn()
    {
        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(
            new[] { CreateRule("ERR", broken: true, RuleSeverity.Error) }, new AsyncRuleEvaluationOptions { ThrowOn = RuleSeverity.Error }, TestToken);

        Assert.True(result.HasErrors);
    }

    // ---------------------------------------------------------------------
    // Observers from EvaluateAllAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_WithOptions_FiresEveryObserverCallback()
    {
        RecordingAsyncObserver observer = new RecordingAsyncObserver();
        IAsyncBusinessRule[] rules = { CreateRule("A", broken: true), CreateRule("B", broken: false), CreateRule("C", broken: false) };

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(rules, new AsyncRuleEvaluationOptions { Observer = observer }, TestToken);

        Assert.Equal(3, observer.BatchStartedCount);
        Assert.Equal(3, observer.BeforeCount);
        Assert.Equal(3, observer.AfterCount);
        Assert.Equal(3, observer.ElapsedCount);
        Assert.Equal(1, observer.BrokenCount);
        Assert.Same(result, observer.CompletedResult);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithOptions_ObserverCallbacksArriveInDocumentedOrder()
    {
        RecordingAsyncObserver observer = new RecordingAsyncObserver();
        IAsyncBusinessRule[] rules = { CreateRule("A", broken: true), CreateRule("B", broken: false) };

        await AsyncBusinessRuleChecker.EvaluateAllAsync(rules, new AsyncRuleEvaluationOptions { Observer = observer }, TestToken);

        Assert.Equal(
            new[] { "started:2", "before:A", "after:A", "elapsed:A", "broken:A", "before:B", "after:B", "elapsed:B", "completed" },
            observer.Sequence);
    }

    [Fact]
    public async Task EvaluateAllAsync_Parallel_ObserverSeesEveryRuleAndOneBatch()
    {
        RecordingAsyncObserver observer = new RecordingAsyncObserver();
        IAsyncBusinessRule[] rules = { CreateRule("A", broken: true), CreateRule("B", broken: false), CreateRule("C", broken: true) };

        await AsyncBusinessRuleChecker.EvaluateAllAsync(rules, new AsyncRuleEvaluationOptions { Observer = observer, RunInParallel = true }, TestToken);

        Assert.Equal("started:3", observer.Sequence.First());
        Assert.Equal("completed", observer.Sequence.Last());
        Assert.Equal(3, observer.BeforeCount);
        Assert.Equal(3, observer.ElapsedCount);
        Assert.Equal(2, observer.BrokenCount);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithOptions_ObserverElapsedMatchesLedgerDuration()
    {
        RecordingAsyncObserver observer = new RecordingAsyncObserver();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new[] { CreateSlowRule("SLOW", TimeSpan.FromMilliseconds(2)) }, new AsyncRuleEvaluationOptions { Observer = observer }, TestToken);

        Assert.Equal(result.Entries[0].Duration, Assert.Single(observer.Elapsed));
    }

    [Fact]
    public async Task CheckAllAsync_LegacyObserverOverload_ReceivesBatchHooks()
    {
        RecordingAsyncObserver observer = new RecordingAsyncObserver();

        await AsyncBusinessRuleChecker.CheckAllAsync(new[] { CreateRule("A", broken: false) }, observer: observer, cancellationToken: TestToken);

        Assert.Equal(1, observer.BatchStartedCount);
        Assert.NotNull(observer.CompletedResult);
        Assert.Equal(1, observer.ElapsedCount);
    }

    // ---------------------------------------------------------------------
    // CheckAllAsync with ThrowOn
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CheckAllAsync_ThrowOnError_ThrowsErrorsAndCollectsLowerSeverities()
    {
        IAsyncBusinessRule[] rules =
        {
            CreateRule("WARN", broken: true, RuleSeverity.Warning),
            CreateRule("ERR", broken: true, RuleSeverity.Error),
            CreateRule("INFO", broken: true, RuleSeverity.Info)
        };

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(rules, new AsyncRuleEvaluationOptions { ThrowOn = RuleSeverity.Error }, TestToken));

        Assert.Equal("ERR", Assert.Single(exception.BrokenRules).Code);
        Assert.Equal(new[] { "WARN", "INFO" }, exception.CollectedRules.Select(r => r.Code));
        Assert.Equal(3, exception.Entries.Count);
    }

    [Fact]
    public async Task CheckAllAsync_ThrowOnError_OnlyWarningsBroken_DoesNotThrow()
    {
        IAsyncBusinessRule[] rules = { CreateRule("WARN", broken: true, RuleSeverity.Warning) };

        await AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), rules, new AsyncRuleEvaluationOptions { ThrowOn = RuleSeverity.Error }, TestToken);
    }

    [Fact]
    public async Task CheckAllAsync_ThrowOnError_Parallel_StillCollectsWarnings()
    {
        IAsyncBusinessRule[] rules = { CreateRule("WARN", broken: true, RuleSeverity.Warning), CreateRule("ERR", broken: true, RuleSeverity.Error) };

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(rules, new AsyncRuleEvaluationOptions { ThrowOn = RuleSeverity.Error, RunInParallel = true }, TestToken));

        Assert.Equal("ERR", Assert.Single(exception.BrokenRules).Code);
        Assert.Equal("WARN", Assert.Single(exception.CollectedRules).Code);
    }

    [Fact]
    public async Task CheckAllAsync_DefaultThrowOn_ThrowsForInfoWithNothingCollected()
    {
        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new[] { CreateRule("INFO", broken: true, RuleSeverity.Info) }, new AsyncRuleEvaluationOptions(), TestToken));

        Assert.Single(exception.BrokenRules);
        Assert.Empty(exception.CollectedRules);
        Assert.Single(exception.Entries);
    }

    [Fact]
    public async Task CheckAllAsync_StopOnFirstFailureWithThrowOnError_DoesNotStopAtAWarning()
    {
        IAsyncBusinessRule[] rules = { CreateRule("WARN", broken: true, RuleSeverity.Warning), CreateRule("ERR", broken: true, RuleSeverity.Error) };

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(rules, new AsyncRuleEvaluationOptions { StopOnFirstFailure = true, ThrowOn = RuleSeverity.Error }, TestToken));

        Assert.Equal("ERR", Assert.Single(exception.BrokenRules).Code);
        Assert.Equal("WARN", Assert.Single(exception.CollectedRules).Code);
    }

    [Fact]
    public async Task CheckAllAsync_StopOnFirstFailureWithThrowOnError_StopsAtTheFirstError()
    {
        int secondEvaluations = 0;
        IAsyncBusinessRule error = CreateRule("ERR", broken: true, RuleSeverity.Error);
        IAsyncBusinessRule counting = AsyncBusinessRuleBuilder.Create("COUNT").WithMessage("count").WhenAsync(ctx => { secondEvaluations++; return Task.FromResult(true); }).Build();

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new[] { error, counting }, new AsyncRuleEvaluationOptions { StopOnFirstFailure = true, ThrowOn = RuleSeverity.Error }, TestToken));

        Assert.Equal(0, secondEvaluations);
        Assert.Equal(RuleOutcome.NotEvaluated, exception.Entries[1].Outcome);
    }

    [Fact]
    public async Task CheckAllAsync_WithOptions_RaisesOnBatchCompletedBeforeThrowing()
    {
        RecordingAsyncObserver observer = new RecordingAsyncObserver();

        await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new[] { CreateRule(BrokenCode, broken: true) }, new AsyncRuleEvaluationOptions { Observer = observer }, TestToken));

        Assert.NotNull(observer.CompletedResult);
        Assert.Equal("completed", observer.Sequence.Last());
    }

    [Fact]
    public async Task CheckAllAsync_FaultPolicyRecord_NoBrokenRules_ReturnsNormallyAndObserverSeesTheFault()
    {
        RecordingAsyncObserver observer = new RecordingAsyncObserver();
        IAsyncBusinessRule[] rules = { CreateThrowingRule(ThrowingCode), CreateRule(PassingCode, broken: false) };

        await AsyncBusinessRuleChecker.CheckAllAsync(rules, new AsyncRuleEvaluationOptions { OnFault = FaultPolicy.Record, Observer = observer }, TestToken);

        Assert.Single(observer.Faulted);
        Assert.True(observer.CompletedResult!.HasFaults);
    }

    [Fact]
    public async Task CheckAllAsync_FaultPolicyRecord_WithBrokenRule_ExceptionCarriesTheFaultInEntries()
    {
        IAsyncBusinessRule[] rules = { CreateThrowingRule(ThrowingCode), CreateRule(BrokenCode, broken: true) };

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(rules, new AsyncRuleEvaluationOptions { OnFault = FaultPolicy.Record }, TestToken));

        Assert.Equal(BrokenCode, Assert.Single(exception.BrokenRules).Code);
        Assert.Equal(RuleOutcome.Faulted, exception.Entries[0].Outcome);
        Assert.IsType<InvalidOperationException>(exception.Entries[0].Fault);
    }

    [Fact]
    public async Task CheckAllAsync_FaultPolicyThrow_PropagatesOriginalException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new[] { CreateThrowingRule(ThrowingCode) }, new AsyncRuleEvaluationOptions(), TestToken));
    }

    [Fact]
    public async Task CheckAllAsync_FaultPolicyTreatAsBroken_ThrowsWithSynthesisedResult()
    {
        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new[] { CreateThrowingRule(ThrowingCode) }, new AsyncRuleEvaluationOptions { OnFault = FaultPolicy.TreatAsBroken }, TestToken));

        Assert.Equal(ThrowingCode, Assert.Single(exception.BrokenRules).Code);
        Assert.True(Assert.Single(exception.Entries).IsFaulted);
    }

    // ---------------------------------------------------------------------
    // Guards
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CheckAllAsync_NullRules_ThrowsArgumentNullException()
    {
        IEnumerable<IAsyncBusinessRule> rules = null!; // intentional: exercising the null guard

        await Assert.ThrowsAsync<ArgumentNullException>(() => AsyncBusinessRuleChecker.CheckAllAsync(rules, new AsyncRuleEvaluationOptions(), TestToken));
    }

    [Fact]
    public async Task CheckAllAsync_NullOptions_ThrowsArgumentNullException()
    {
        AsyncRuleEvaluationOptions options = null!; // intentional: exercising the null guard

        await Assert.ThrowsAsync<ArgumentNullException>(() => AsyncBusinessRuleChecker.CheckAllAsync(Array.Empty<IAsyncBusinessRule>(), options, TestToken));
    }

    [Fact]
    public async Task CheckAllAsync_NullContext_ThrowsArgumentNullException()
    {
        BusinessRuleContext context = null!; // intentional: exercising the null guard

        await Assert.ThrowsAsync<ArgumentNullException>(() => AsyncBusinessRuleChecker.CheckAllAsync(context, Array.Empty<IAsyncBusinessRule>(), new AsyncRuleEvaluationOptions(), TestToken));
    }

    [Fact]
    public async Task EvaluateAllAsync_NullRules_ThrowsArgumentNullException()
    {
        IEnumerable<IAsyncBusinessRule> rules = null!; // intentional: exercising the null guard

        await Assert.ThrowsAsync<ArgumentNullException>(() => AsyncBusinessRuleChecker.EvaluateAllAsync(rules, new AsyncRuleEvaluationOptions(), TestToken));
    }

    [Fact]
    public async Task EvaluateAllAsync_NullContext_ThrowsArgumentNullException()
    {
        BusinessRuleContext context = null!; // intentional: exercising the null guard

        await Assert.ThrowsAsync<ArgumentNullException>(() => AsyncBusinessRuleChecker.EvaluateAllAsync(context, Array.Empty<IAsyncBusinessRule>(), new AsyncRuleEvaluationOptions(), TestToken));
    }

    [Fact]
    public async Task EvaluateAllAsync_NullOptions_ThrowsArgumentNullException()
    {
        AsyncRuleEvaluationOptions options = null!; // intentional: exercising the null guard

        await Assert.ThrowsAsync<ArgumentNullException>(() => AsyncBusinessRuleChecker.EvaluateAllAsync(Array.Empty<IAsyncBusinessRule>(), options, TestToken));
    }

    [Fact]
    public async Task EvaluateAllAsync_NullRuleInsideBatch_ThrowsArgumentException()
    {
        IAsyncBusinessRule[] rules = { CreateRule("A", broken: false), null! }; // intentional: exercising the null guard

        await Assert.ThrowsAsync<ArgumentException>(() => AsyncBusinessRuleChecker.EvaluateAllAsync(rules, new AsyncRuleEvaluationOptions(), TestToken));
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static IAsyncBusinessRule CreateRule(string code, bool broken, RuleSeverity severity = RuleSeverity.Error)
    {
        return AsyncBusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .WithSeverity(severity)
            .WhenAsync(ctx => Task.FromResult(broken))
            .Build();
    }

    private static IAsyncBusinessRule CreateThrowingRule(string code)
    {
        return AsyncBusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .WhenAsync(ctx => throw new InvalidOperationException(Boom))
            .Build();
    }

    private static IAsyncBusinessRule CreateSlowRule(string code, TimeSpan duration)
    {
        return AsyncBusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .WhenAsync(async (ctx, ct) =>
            {
                await Task.Delay(duration, ct);
                return false;
            })
            .Build();
    }

    private sealed class ThrowingMessageAsyncRule : IAsyncBusinessRule
    {
        public string Code => "THROWING.MESSAGE";

        public string Message => throw new NotSupportedException("message getter throws");

        public Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(Boom);

        public Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => IsBrokenAsync(context, cancellationToken).ContinueWith(_ => (BusinessRuleResult?)null, cancellationToken);

        public Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => IsBrokenAsync(context, cancellationToken);
    }

    private sealed class ThrowingCodeAsyncRule : IAsyncBusinessRule
    {
        public string Code => throw new NotSupportedException("code getter throws");

        public string Message => "code getter throws";

        public Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(Boom);

        public Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => IsBrokenAsync(context, cancellationToken).ContinueWith(_ => (BusinessRuleResult?)null, cancellationToken);

        public Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => IsBrokenAsync(context, cancellationToken);
    }

    private sealed class RecordingAsyncObserver : IAsyncRuleExecutionObserver
    {
        private readonly object _lock = new object();

        public List<string> Sequence { get; } = new List<string>();

        public List<(IAsyncBusinessRule rule, Exception exception)> Faulted { get; } = new List<(IAsyncBusinessRule rule, Exception exception)>();

        public List<TimeSpan> Elapsed { get; } = new List<TimeSpan>();

        public int BatchStartedCount { get; private set; }

        public int BeforeCount { get; private set; }

        public int AfterCount { get; private set; }

        public int ElapsedCount { get; private set; }

        public int BrokenCount { get; private set; }

        public AsyncRuleEvaluationResult? CompletedResult { get; private set; }

        public Task OnBatchStartedAsync(int ruleCount, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                BatchStartedCount = ruleCount;
                Sequence.Add($"started:{ruleCount}");
            }

            return Task.CompletedTask;
        }

        public Task OnBeforeEvaluateAsync(IAsyncBusinessRule rule, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                BeforeCount++;
                Sequence.Add($"before:{rule.Code}");
            }

            return Task.CompletedTask;
        }

        public Task OnAfterEvaluateAsync(IAsyncBusinessRule rule, BusinessRuleResult? result, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                AfterCount++;
                Sequence.Add($"after:{rule.Code}");
            }

            return Task.CompletedTask;
        }

        public Task OnAfterEvaluateAsync(IAsyncBusinessRule rule, bool isBroken, TimeSpan elapsed, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                ElapsedCount++;
                Elapsed.Add(elapsed);
                Sequence.Add($"elapsed:{rule.Code}");
            }

            return Task.CompletedTask;
        }

        public Task OnRuleBrokenAsync(IAsyncBusinessRule rule, BusinessRuleResult result, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                BrokenCount++;
                Sequence.Add($"broken:{rule.Code}");
            }

            return Task.CompletedTask;
        }

        public Task OnRuleFaultedAsync(IAsyncBusinessRule rule, Exception exception, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                Faulted.Add((rule, exception));
                Sequence.Add($"faulted:{rule.Code}");
            }

            return Task.CompletedTask;
        }

        public Task OnBatchCompletedAsync(AsyncRuleEvaluationResult result, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                CompletedResult = result;
                Sequence.Add("completed");
            }

            return Task.CompletedTask;
        }
    }
}
