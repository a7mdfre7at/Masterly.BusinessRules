using System.Collections.Concurrent;

namespace Masterly.BusinessRules.UnitTests;

/// <summary>
/// The observer callbacks of an async batch that ends by an exception (cancellation, a propagating fault, an invalid
/// batch) and of a shadow-mode async rule whose inner rule throws.
/// </summary>
public class AsyncBusinessRuleCheckerAbortTests
{
    private const string PassingCode = "PASS";
    private const string CancelledCode = "CANCELLED";
    private const string ThrowingCode = "THROWS";
    private const string ShadowCode = "SHADOW.RULE";
    private const string Flag = "shadow-flag";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------
    // Cancellation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CheckAllAsync_RuleCancelled_RaisesOnRuleCancelledAndOnBatchAbortedOnly()
    {
        OperationCanceledException cancellation = new OperationCanceledException();
        RecordingObserver observer = new RecordingObserver();
        IAsyncBusinessRule[] rules = { RuleTestHelper.CreatePassingAsyncRule(PassingCode), Throwing(CancelledCode, cancellation) };

        OperationCanceledException thrown = await Assert.ThrowsAsync<OperationCanceledException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), rules, new AsyncRuleEvaluationOptions { Observer = observer, OnFault = FaultPolicy.Record }, TestToken));

        Assert.Same(cancellation, thrown);
        Assert.Equal(
            new[] { "batch-started:2", "before:PASS", "after:PASS", "before:CANCELLED", "cancelled:CANCELLED", "aborted:OperationCanceledException" },
            observer.Calls);
        Assert.Same(cancellation, observer.AbortedWith);
    }

    [Fact]
    public async Task EvaluateAllAsync_TokenCancelledDuringARule_RaisesTheCancellationHooksWithAnUncancelledToken()
    {
        using CancellationTokenSource source = new CancellationTokenSource();
        RecordingObserver observer = new RecordingObserver();
        IAsyncBusinessRule cancelling = AsyncBusinessRuleBuilder.Create(CancelledCode)
            .WithMessage("m")
            .WhenAsync(async (ctx, ct) =>
            {
                source.Cancel();
                await Task.Delay(Timeout.Infinite, ct);
                return false;
            })
            .Build();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { cancelling }, new AsyncRuleEvaluationOptions { Observer = observer }, source.Token));

        Assert.Equal(new[] { "batch-started:1", "before:CANCELLED", "cancelled:CANCELLED", "aborted:TaskCanceledException" }, observer.Calls);
        Assert.False(observer.HookTokenWasCancelled);
    }

    [Fact]
    public async Task EvaluateAllAsync_ParallelRuleCancelled_RaisesOnRuleCancelledForItAndAbortsTheBatchOnce()
    {
        RecordingObserver observer = new RecordingObserver();
        IAsyncBusinessRule[] rules = { Throwing(CancelledCode, new OperationCanceledException()), RuleTestHelper.CreatePassingAsyncRule(PassingCode) };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), rules, new AsyncRuleEvaluationOptions { Observer = observer, RunInParallel = true }, TestToken));

        Assert.Single(observer.Calls, call => call == "cancelled:CANCELLED");
        Assert.Single(observer.Calls, call => call.StartsWith("aborted:", StringComparison.Ordinal));
        Assert.DoesNotContain("completed", observer.Calls);
    }

    [Fact]
    public async Task CheckAllAsync_ObserverWithoutTheNewHooks_StillPropagatesTheCancellation()
    {
        MinimalObserver observer = new MinimalObserver();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), new[] { Throwing(CancelledCode, new OperationCanceledException()) }, new AsyncRuleEvaluationOptions { Observer = observer }, TestToken));

        Assert.Equal(1, observer.BeforeCount);
    }

    // ---------------------------------------------------------------------
    // Faults and invalid batches
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CheckAllAsync_FaultUnderThrow_RaisesOnRuleFaultedThenOnBatchAborted()
    {
        InvalidOperationException fault = new InvalidOperationException("boom");
        RecordingObserver observer = new RecordingObserver();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), new[] { Throwing(ThrowingCode, fault) }, new AsyncRuleEvaluationOptions { Observer = observer }, TestToken));

        Assert.Equal(new[] { "batch-started:1", "before:THROWS", "faulted:THROWS", "aborted:InvalidOperationException" }, observer.Calls);
        Assert.Same(fault, observer.AbortedWith);
    }

    [Fact]
    public async Task EvaluateAllAsync_FaultUnderRecord_CompletesTheBatchWithoutAborting()
    {
        RecordingObserver observer = new RecordingObserver();

        await AsyncBusinessRuleChecker.EvaluateAllAsync(
            new BusinessRuleContext(), new[] { Throwing(ThrowingCode, new InvalidOperationException("boom")) }, new AsyncRuleEvaluationOptions { Observer = observer, OnFault = FaultPolicy.Record }, TestToken);

        Assert.Equal("completed", observer.Calls.Last());
        Assert.Null(observer.AbortedWith);
    }

    [Fact]
    public async Task EvaluateAllAsync_NullRuleInTheBatch_RaisesOnBatchAbortedWithTheArgumentException()
    {
        RecordingObserver observer = new RecordingObserver();
        IAsyncBusinessRule[] rules = { RuleTestHelper.CreatePassingAsyncRule(PassingCode), null! };

        ArgumentException thrown = await Assert.ThrowsAsync<ArgumentException>(
            () => AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), rules, new AsyncRuleEvaluationOptions { Observer = observer }, TestToken));

        Assert.Same(thrown, observer.AbortedWith);
        Assert.Equal("rules", thrown.ParamName);
    }

    // ---------------------------------------------------------------------
    // Shadow mode never blocks, even when the inner rule throws
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_ShadowRuleThrows_DoesNotThrowAndCollectsAStandInShadowResult()
    {
        IAsyncBusinessRule shadow = Shadow(Throwing(ShadowCode, new InvalidOperationException("dark launch bug")));

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(
            new BusinessRuleContext(), new[] { shadow, RuleTestHelper.CreatePassingAsyncRule(PassingCode) }, new AsyncRuleEvaluationOptions(), TestToken);

        Assert.True(result.AllPassed);
        Assert.Equal(ShadowCode, Assert.Single(result.ShadowResults).Code);
        Assert.Equal(RuleOutcome.Passed, result.Entries[0].Outcome);
        Assert.Empty(result.Faulted);
    }

    [Fact]
    public async Task CheckAllAsync_ShadowRuleThrowsUnderTheDefaultFaultPolicy_DoesNotThrow()
    {
        IAsyncBusinessRule shadow = Shadow(Throwing(ShadowCode, new InvalidOperationException("dark launch bug")));

        Exception? exception = await Record.ExceptionAsync(() => AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), new[] { shadow }, new AsyncRuleEvaluationOptions(), TestToken));

        Assert.Null(exception);
    }

    [Fact]
    public async Task EvaluateAllAsync_ShadowRuleThrows_RaisesOnShadowViolationThenOnShadowFault()
    {
        InvalidOperationException fault = new InvalidOperationException("dark launch bug");
        RecordingObserver observer = new RecordingObserver();

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { Shadow(Throwing(ShadowCode, fault)) }, new AsyncRuleEvaluationOptions { Observer = observer }, TestToken);

        Assert.Equal(new[] { "batch-started:1", "before:SHADOW.RULE", "after:SHADOW.RULE", "shadow:SHADOW.RULE", "shadow-fault:SHADOW.RULE", "completed" }, observer.Calls);
        Assert.Same(fault, observer.ShadowFault);
    }

    [Fact]
    public async Task EvaluateAllAsync_ParallelShadowRuleThrows_ReportsTheShadowFault()
    {
        RecordingObserver observer = new RecordingObserver();
        IAsyncBusinessRule[] rules = { Shadow(Throwing(ShadowCode, new InvalidOperationException("dark launch bug"))), RuleTestHelper.CreatePassingAsyncRule(PassingCode) };

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(
            new BusinessRuleContext(), rules, new AsyncRuleEvaluationOptions { Observer = observer, RunInParallel = true }, TestToken);

        Assert.Single(result.ShadowResults);
        Assert.Contains("shadow-fault:SHADOW.RULE", observer.Calls);
    }

    [Fact]
    public async Task IsBrokenAsync_ShadowRuleThrowsOutsideABatch_PassesWithoutThrowing()
    {
        IAsyncBusinessRule shadow = Shadow(Throwing(ShadowCode, new InvalidOperationException("dark launch bug")));

        bool isBroken = await shadow.IsBrokenAsync(new BusinessRuleContext(), TestToken);

        Assert.False(isBroken);
    }

    [Fact]
    public async Task IsBrokenAsync_ShadowRuleCancelled_PropagatesTheCancellation()
    {
        IAsyncBusinessRule shadow = Shadow(Throwing(ShadowCode, new OperationCanceledException()));

        await Assert.ThrowsAsync<OperationCanceledException>(() => shadow.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_EnforcedRuleThrows_PropagatesTheFault()
    {
        IAsyncBusinessRule enforced = Throwing(ShadowCode, new InvalidOperationException("boom")).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        await Assert.ThrowsAsync<InvalidOperationException>(() => enforced.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task ExplainAsync_ShadowRuleThrows_RecordsAFaultedInnerNodeUnderAPassingShadowNode()
    {
        IAsyncBusinessRule shadow = Shadow(Throwing(ShadowCode, new InvalidOperationException("dark launch bug")));

        RuleExplanation explanation = await shadow.ExplainAsync(new BusinessRuleContext(), TestToken);

        Assert.Equal(RuleOutcome.Passed, explanation.Outcome);
        Assert.Equal("SHADOW", explanation.Operator);
        RuleExplanation inner = Assert.Single(explanation.Children);
        Assert.Equal(RuleOutcome.Faulted, inner.Outcome);
        Assert.IsType<InvalidOperationException>(inner.Fault);
    }

    // ---------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------

    private static IAsyncBusinessRule Throwing(string code, Exception exception)
    {
        return AsyncBusinessRuleBuilder.Create(code)
            .WithMessage($"{code} message")
            .WhenAsync(async (ctx, ct) =>
            {
                await Task.Yield();
                throw exception;
            })
            .Build();
    }

    private static IAsyncBusinessRule Shadow(IAsyncBusinessRule inner)
    {
        return inner.GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));
    }

    private sealed class RecordingObserver : IAsyncRuleExecutionObserver
    {
        private readonly ConcurrentQueue<string> _calls = new ConcurrentQueue<string>();

        public IReadOnlyList<string> Calls => _calls.ToArray();

        public Exception? AbortedWith { get; private set; }

        public Exception? ShadowFault { get; private set; }

        public bool HookTokenWasCancelled { get; private set; }

        public Task OnBeforeEvaluateAsync(IAsyncBusinessRule rule, CancellationToken cancellationToken = default) => Record($"before:{rule.Code}");

        public Task OnAfterEvaluateAsync(IAsyncBusinessRule rule, BusinessRuleResult? result, CancellationToken cancellationToken = default) => Record($"after:{rule.Code}");

        public Task OnRuleBrokenAsync(IAsyncBusinessRule rule, BusinessRuleResult result, CancellationToken cancellationToken = default) => Record($"broken:{rule.Code}");

        public Task OnBatchStartedAsync(int ruleCount, CancellationToken cancellationToken = default) => Record($"batch-started:{ruleCount}");

        public Task OnRuleFaultedAsync(IAsyncBusinessRule rule, Exception exception, CancellationToken cancellationToken = default) => Record($"faulted:{rule.Code}");

        public Task OnBatchCompletedAsync(AsyncRuleEvaluationResult result, CancellationToken cancellationToken = default) => Record("completed");

        public Task OnShadowViolationAsync(IAsyncBusinessRule rule, BusinessRuleResult result, CancellationToken cancellationToken = default) => Record($"shadow:{rule.Code}");

        public Task OnRuleCancelledAsync(IAsyncBusinessRule rule, OperationCanceledException exception, CancellationToken cancellationToken = default)
        {
            HookTokenWasCancelled |= cancellationToken.IsCancellationRequested;
            return Record($"cancelled:{rule.Code}");
        }

        public Task OnBatchAbortedAsync(Exception exception, CancellationToken cancellationToken = default)
        {
            HookTokenWasCancelled |= cancellationToken.IsCancellationRequested;
            AbortedWith = exception;
            return Record($"aborted:{exception.GetType().Name}");
        }

        public Task OnShadowFaultAsync(IAsyncBusinessRule rule, Exception exception, CancellationToken cancellationToken = default)
        {
            ShadowFault = exception;
            return Record($"shadow-fault:{rule.Code}");
        }

        private Task Record(string call)
        {
            _calls.Enqueue(call);
            return Task.CompletedTask;
        }
    }

    private sealed class MinimalObserver : IAsyncRuleExecutionObserver
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
