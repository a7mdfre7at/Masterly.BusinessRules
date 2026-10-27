using System.Collections.Concurrent;
using System.Diagnostics;
using Masterly.BusinessRules.Diagnostics.UnitTests.Support;

namespace Masterly.BusinessRules.Diagnostics.UnitTests.Tracing;

[Collection(DiagnosticsCollections.Activities)]
public class TracingAsyncRuleExecutionObserverTests
{
    private const string BrokenCode = "ORDER.NO_ITEMS";
    private const string PassingCode = "ORDER.OK";
    private const string ThrowingCode = "ORDER.THROWS";
    private const string Flag = "shadow-flag";

    // ---------------------------------------------------------------------
    // Construction and guards
    // ---------------------------------------------------------------------

    [Fact]
    public void Constructor_NullOptions_UsesDefaults()
    {
        TracingAsyncRuleExecutionObserver observer = new TracingAsyncRuleExecutionObserver(null);

        Assert.NotNull(observer.Options);
        Assert.NotNull(new TracingAsyncRuleExecutionObserver().Options);
    }

    [Fact]
    public async Task Callbacks_NullArguments_Throw()
    {
        TracingAsyncRuleExecutionObserver observer = new TracingAsyncRuleExecutionObserver();

        await Assert.ThrowsAsync<ArgumentNullException>(() => observer.OnBeforeEvaluateAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => observer.OnAfterEvaluateAsync(null!, true, TimeSpan.Zero));
        await Assert.ThrowsAsync<ArgumentNullException>(() => observer.OnRuleFaultedAsync(null!, new InvalidOperationException()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => observer.OnRuleFaultedAsync(TestRules.Async(BrokenCode, true), null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => observer.OnBatchCompletedAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => observer.OnShadowViolationAsync(null!, new BusinessRuleResult(BrokenCode, "m", RuleSeverity.Error)));
        await Assert.ThrowsAsync<ArgumentNullException>(() => observer.OnShadowViolationAsync(TestRules.Async(BrokenCode, true), null!));
    }

    [Fact]
    public void Callbacks_Always_CompleteSynchronously()
    {
        using ActivityCapture capture = new ActivityCapture();
        TracingAsyncRuleExecutionObserver observer = new TracingAsyncRuleExecutionObserver(TracingOptionsWithoutMetrics());
        IAsyncBusinessRule rule = TestRules.Async(BrokenCode, true);
        CancellationToken cancelled = new CancellationToken(canceled: true);

        Task[] tasks =
        {
            observer.OnBatchStartedAsync(1, cancelled),
            observer.OnBeforeEvaluateAsync(rule, cancelled),
            observer.OnAfterEvaluateAsync(rule, null, cancelled),
            observer.OnAfterEvaluateAsync(rule, true, TimeSpan.Zero, cancelled),
            observer.OnRuleBrokenAsync(rule, new BusinessRuleResult(BrokenCode, "m", RuleSeverity.Error), cancelled),
            observer.OnShadowViolationAsync(rule, new BusinessRuleResult(BrokenCode, "m", RuleSeverity.Error), cancelled),
            observer.OnBatchCompletedAsync(new AsyncRuleEvaluationResult(Array.Empty<RuleEvaluationEntry>(), TimeSpan.Zero), cancelled)
        };

        Assert.All(tasks, task => Assert.True(task.IsCompletedSuccessfully));
        Assert.Single(capture.RuleSpans);
        Assert.Single(capture.BatchSpans);
    }

    // ---------------------------------------------------------------------
    // Span structure
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_TwoRules_ProducesBatchSpanWithOneChildPerRule()
    {
        using ActivityCapture capture = new ActivityCapture();
        IAsyncBusinessRule[] rules = { TestRules.Async(BrokenCode, true), TestRules.Async(PassingCode, false) };

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), rules, new AsyncRuleEvaluationOptions { Observer = new TracingAsyncRuleExecutionObserver(TracingOptionsWithoutMetrics()) });

        Activity batch = capture.SingleBatchSpan();
        Assert.Equal(2, capture.RuleSpans.Count);
        Assert.All(capture.RuleSpans, span => Assert.Same(batch, span.Parent));
        Assert.Equal(2, batch.GetTagItem(BusinessRuleSpanAttributes.BatchRuleCount));
        Assert.Equal(1, batch.GetTagItem(BusinessRuleSpanAttributes.BatchBrokenCount));
        Assert.Equal(RuleOutcomeTags.Broken, ActivityCapture.Tag(batch, BusinessRuleSpanAttributes.BatchOutcome));
        Assert.Null(Activity.Current);
    }

    [Fact]
    public async Task EvaluateAllAsync_BrokenRule_SpanCarriesAttributesAndCodeAsDisplayName()
    {
        using ActivityCapture capture = new ActivityCapture();

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { TestRules.Async(BrokenCode, true, RuleSeverity.Info) }, new AsyncRuleEvaluationOptions { Observer = new TracingAsyncRuleExecutionObserver(TracingOptionsWithoutMetrics()) });

        Activity span = capture.SingleRuleSpan();
        Assert.Equal(BrokenCode, span.DisplayName);
        Assert.Equal(BrokenCode, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Code));
        Assert.Equal($"{BrokenCode} name", ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Name));
        Assert.Equal(TestRules.Category, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Category));
        Assert.Equal(RuleSeverityTags.Info, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Severity));
        Assert.Equal(RuleOutcomeTags.Broken, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Outcome));
        Assert.Empty(span.Events);
    }

    [Fact]
    public async Task EvaluateAllAsync_ComposedRule_SpanHasNoCodeTag()
    {
        using ActivityCapture capture = new ActivityCapture();
        IAsyncBusinessRule composed = TestRules.Async("A", true).Or(TestRules.Async("B", false));

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { composed }, new AsyncRuleEvaluationOptions { Observer = new TracingAsyncRuleExecutionObserver(TracingOptionsWithoutMetrics()) });

        Activity span = capture.SingleRuleSpan();
        Assert.Null(span.GetTagItem(BusinessRuleSpanAttributes.Code));
        Assert.Equal(BusinessRulesActivitySource.RuleActivityName, span.DisplayName);
    }

    [Fact]
    public async Task EvaluateAllAsync_Parallel_EveryRuleSpanIsAChildOfTheBatchSpan()
    {
        using ActivityCapture capture = new ActivityCapture();
        IAsyncBusinessRule[] rules =
        {
            TestRules.Async("A", true, onEvaluate: () => Task.Delay(10)),
            TestRules.Async("B", false, onEvaluate: () => Task.Delay(1)),
            TestRules.Async("C", true, onEvaluate: () => Task.Delay(5))
        };

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), rules, new AsyncRuleEvaluationOptions { Observer = new TracingAsyncRuleExecutionObserver(TracingOptionsWithoutMetrics()), RunInParallel = true });

        Activity batch = capture.SingleBatchSpan();
        Assert.Equal(3, capture.RuleSpans.Count);
        Assert.All(capture.RuleSpans, span => Assert.Same(batch, span.Parent));
        Assert.Equal(new[] { "A", "B", "C" }, capture.RuleSpans.Select(span => ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Code)).OrderBy(code => code));
        Assert.Equal(RuleOutcomeTags.Passed, ActivityCapture.Tag(capture.RuleSpans.Single(span => ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Code) == "B"), BusinessRuleSpanAttributes.Outcome));
        Assert.Equal(2, batch.GetTagItem(BusinessRuleSpanAttributes.BatchBrokenCount));
    }

    // ---------------------------------------------------------------------
    // The rule span is current while the rule runs
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_ActivityStartedInsideRule_HasRuleSpanAsParent()
    {
        using ActivityCapture capture = new ActivityCapture();
        Activity? childParent = null;
        IAsyncBusinessRule rule = TestRules.Async(BrokenCode, true, onEvaluate: async () =>
        {
            await Task.Delay(1);
            using Activity child = new Activity("repository.query").Start();
            childParent = child.Parent;
        });

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { rule }, new AsyncRuleEvaluationOptions { Observer = new TracingAsyncRuleExecutionObserver(TracingOptionsWithoutMetrics()) });

        Assert.NotNull(childParent);
        Assert.Same(capture.SingleRuleSpan(), childParent);
        Assert.Equal(BrokenCode, ActivityCapture.Tag(childParent!, BusinessRuleSpanAttributes.Code));
    }

    [Fact]
    public async Task EvaluateAllAsync_Parallel_ActivityStartedInsideEachRuleHasItsOwnRuleSpanAsParent()
    {
        using ActivityCapture capture = new ActivityCapture();
        ConcurrentDictionary<string, Activity?> parents = new ConcurrentDictionary<string, Activity?>(StringComparer.Ordinal);
        IAsyncBusinessRule[] rules = new[] { "A", "B", "C" }.Select(code => TestRules.Async(code, true, onEvaluate: async () =>
        {
            await Task.Delay(code == "A" ? 10 : 1);
            using Activity child = new Activity("repository.query").Start();
            parents[code] = child.Parent;
        })).ToArray();

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), rules, new AsyncRuleEvaluationOptions { Observer = new TracingAsyncRuleExecutionObserver(TracingOptionsWithoutMetrics()), RunInParallel = true });

        Assert.Equal(3, parents.Count);
        foreach (KeyValuePair<string, Activity?> pair in parents)
        {
            Assert.NotNull(pair.Value);
            Assert.Equal(BusinessRulesActivitySource.RuleActivityName, pair.Value!.OperationName);
            Assert.Equal(pair.Key, ActivityCapture.Tag(pair.Value, BusinessRuleSpanAttributes.Code));
        }
    }

    // ---------------------------------------------------------------------
    // Message as event
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_MessageAsEvent_AddsBrokenEventWithResultMessage()
    {
        using ActivityCapture capture = new ActivityCapture();
        TracingRuleObserverOptions options = TracingOptionsWithoutMetrics();
        options.MessageAsEvent = true;

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { TestRules.Async(BrokenCode, true, message: "no items") }, new AsyncRuleEvaluationOptions { Observer = new TracingAsyncRuleExecutionObserver(options) });

        ActivityEvent brokenEvent = Assert.Single(capture.SingleRuleSpan().Events);
        Assert.Equal(BusinessRuleSpanAttributes.BrokenEventName, brokenEvent.Name);
        Assert.Equal("no items", brokenEvent.Tags.Single(tag => tag.Key == BusinessRuleSpanAttributes.Message).Value);
    }

    // ---------------------------------------------------------------------
    // Faults and cancellation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_FaultRecorded_SpanIsFaultedWithErrorStatusAndExceptionEvent()
    {
        using ActivityCapture capture = new ActivityCapture();
        InvalidOperationException fault = new InvalidOperationException(TestRules.Boom);

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { TestRules.AsyncThrowing(ThrowingCode, fault) }, new AsyncRuleEvaluationOptions { Observer = new TracingAsyncRuleExecutionObserver(TracingOptionsWithoutMetrics()), OnFault = FaultPolicy.Record });

        Activity span = capture.SingleRuleSpan();
        Assert.Equal(RuleOutcomeTags.Faulted, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Outcome));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        ActivityEvent exceptionEvent = Assert.Single(span.Events);
        Assert.Equal(BusinessRuleSpanAttributes.ExceptionEventName, exceptionEvent.Name);
        Assert.Equal(ActivityStatusCode.Error, capture.SingleBatchSpan().Status);
    }

    [Fact]
    public async Task CheckAllAsync_FaultPropagates_RuleSpanIsClosedButBatchSpanIsNotStopped()
    {
        using ActivityCapture capture = new ActivityCapture();
        AsyncRuleEvaluationOptions options = new AsyncRuleEvaluationOptions { Observer = new TracingAsyncRuleExecutionObserver(TracingOptionsWithoutMetrics()) };

        await Assert.ThrowsAsync<InvalidOperationException>(() => AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), new[] { TestRules.AsyncThrowing(ThrowingCode) }, options));

        Assert.Equal(RuleOutcomeTags.Faulted, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
        Assert.Empty(capture.BatchSpans);
        Assert.Null(Activity.Current);
    }

    [Fact]
    public async Task EvaluateAllAsync_Cancelled_PropagatesWithoutFaultedSpan()
    {
        using ActivityCapture capture = new ActivityCapture();
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();
        AsyncRuleEvaluationOptions options = new AsyncRuleEvaluationOptions { Observer = new TracingAsyncRuleExecutionObserver(TracingOptionsWithoutMetrics()), OnFault = FaultPolicy.Record };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { TestRules.AsyncCancellable(BrokenCode) }, options, source.Token));

        Assert.Empty(capture.RuleSpans);
        Assert.Null(Activity.Current);
    }

    // ---------------------------------------------------------------------
    // Shadow violations
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_ShadowViolation_AddsEventToBatchSpan()
    {
        using ActivityCapture capture = new ActivityCapture();
        IAsyncBusinessRule gated = TestRules.Async(BrokenCode, true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { gated }, new AsyncRuleEvaluationOptions { Observer = new TracingAsyncRuleExecutionObserver(TracingOptionsWithoutMetrics()) });

        Assert.Equal(RuleOutcomeTags.Passed, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
        ActivityEvent shadowEvent = Assert.Single(capture.SingleBatchSpan().Events);
        Assert.Equal(BusinessRuleSpanAttributes.ShadowViolationEventName, shadowEvent.Name);
        Assert.Equal(BrokenCode, shadowEvent.Tags.Single(tag => tag.Key == BusinessRuleSpanAttributes.Code).Value);
    }

    // ---------------------------------------------------------------------
    // Metrics
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_WithMetrics_RecordsEvaluationsDurationsAndBatch()
    {
        using MetricsCapture metrics = new MetricsCapture();
        IAsyncBusinessRule[] rules = { TestRules.Async(BrokenCode, true), TestRules.Async(PassingCode, false), TestRules.AsyncThrowing(ThrowingCode) };

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), rules, new AsyncRuleEvaluationOptions { Observer = new TracingAsyncRuleExecutionObserver(metrics.TracingOptions()), RunInParallel = true, OnFault = FaultPolicy.Record });

        Assert.Equal(3, metrics.Evaluations.Count);
        Assert.Equal(RuleOutcomeTags.Broken, Assert.Single(metrics.Evaluations, m => (string?)m[BusinessRulesMetrics.CodeTag] == BrokenCode)[BusinessRulesMetrics.OutcomeTag]);
        Assert.Equal(RuleOutcomeTags.Passed, Assert.Single(metrics.Evaluations, m => (string?)m[BusinessRulesMetrics.CodeTag] == PassingCode)[BusinessRulesMetrics.OutcomeTag]);
        Assert.Equal(RuleOutcomeTags.Faulted, Assert.Single(metrics.Evaluations, m => (string?)m[BusinessRulesMetrics.CodeTag] == ThrowingCode)[BusinessRulesMetrics.OutcomeTag]);
        Assert.Equal(2, metrics.Durations.Count);
        Assert.Equal(RuleOutcomeTags.Faulted, Assert.Single(metrics.Batches)[BusinessRulesMetrics.OutcomeTag]);
    }

    [Fact]
    public async Task EvaluateAllAsync_RecordMetricsFalse_RecordsNothing()
    {
        using MetricsCapture metrics = new MetricsCapture();

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { TestRules.Async(BrokenCode, true) }, new AsyncRuleEvaluationOptions { Observer = new TracingAsyncRuleExecutionObserver(metrics.TracingOptions(o => o.RecordMetrics = false)) });

        Assert.Empty(metrics.Measurements);
    }

    private static TracingRuleObserverOptions TracingOptionsWithoutMetrics()
    {
        return new TracingRuleObserverOptions { RecordMetrics = false };
    }
}
