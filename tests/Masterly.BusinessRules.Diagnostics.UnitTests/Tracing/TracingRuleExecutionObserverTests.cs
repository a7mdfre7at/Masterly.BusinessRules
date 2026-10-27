using System.Diagnostics;
using Masterly.BusinessRules.Diagnostics.UnitTests.Support;

namespace Masterly.BusinessRules.Diagnostics.UnitTests.Tracing;

[Collection(DiagnosticsCollections.Activities)]
public class TracingRuleExecutionObserverTests
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
        TracingRuleExecutionObserver observer = new TracingRuleExecutionObserver(null);

        Assert.NotNull(observer.Options);
        Assert.False(observer.Options.MessageAsEvent);
        Assert.NotNull(new TracingRuleExecutionObserver().Options);
    }

    [Fact]
    public void Callbacks_NullArguments_Throw()
    {
        TracingRuleExecutionObserver observer = new TracingRuleExecutionObserver();

        Assert.Throws<ArgumentNullException>(() => observer.OnBeforeEvaluate(null!));
        Assert.Throws<ArgumentNullException>(() => observer.OnAfterEvaluate(null!, true, TimeSpan.Zero));
        Assert.Throws<ArgumentNullException>(() => observer.OnRuleFaulted(null!, new InvalidOperationException()));
        Assert.Throws<ArgumentNullException>(() => observer.OnRuleFaulted(TestRules.Sync(BrokenCode, true), null!));
        Assert.Throws<ArgumentNullException>(() => observer.OnBatchCompleted(null!));
        Assert.Throws<ArgumentNullException>(() => observer.OnShadowViolation(null!, new BusinessRuleResult(BrokenCode, "m", RuleSeverity.Error)));
        Assert.Throws<ArgumentNullException>(() => observer.OnShadowViolation(TestRules.Sync(BrokenCode, true), null!));
    }

    [Fact]
    public void ActivitySource_Name_IsMasterlyBusinessRules()
    {
        Assert.Equal("Masterly.BusinessRules", BusinessRulesActivitySource.Name);
        Assert.Equal(BusinessRulesActivitySource.Name, BusinessRulesActivitySource.Source.Name);
    }

    // ---------------------------------------------------------------------
    // Span structure
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_TwoRules_ProducesBatchSpanWithOneChildPerRule()
    {
        using ActivityCapture capture = new ActivityCapture();
        IBusinessRule[] rules = { TestRules.Sync(BrokenCode, true), TestRules.Sync(PassingCode, false) };

        BusinessRuleChecker.EvaluateAll(rules, new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(TracingOptionsWithoutMetrics()) });

        Activity batch = capture.SingleBatchSpan();
        Assert.Equal(2, capture.RuleSpans.Count);
        Assert.All(capture.RuleSpans, span => Assert.Same(batch, span.Parent));
        Assert.Equal(2, batch.GetTagItem(BusinessRuleSpanAttributes.BatchRuleCount));
        Assert.Equal(1, batch.GetTagItem(BusinessRuleSpanAttributes.BatchBrokenCount));
        Assert.Equal(0, batch.GetTagItem(BusinessRuleSpanAttributes.BatchFaultedCount));
        Assert.Equal(RuleOutcomeTags.Broken, ActivityCapture.Tag(batch, BusinessRuleSpanAttributes.BatchOutcome));
        Assert.Equal(ActivityStatusCode.Unset, batch.Status);
        Assert.Null(Activity.Current);
    }

    [Fact]
    public void EvaluateAll_BrokenRule_SpanCarriesAttributesAndCodeAsDisplayName()
    {
        using ActivityCapture capture = new ActivityCapture();
        IBusinessRule broken = TestRules.Sync(BrokenCode, true, RuleSeverity.Warning);

        BusinessRuleChecker.EvaluateAll(new[] { broken }, new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(TracingOptionsWithoutMetrics()) });

        Activity span = capture.SingleRuleSpan();
        Assert.Equal(BusinessRulesActivitySource.RuleActivityName, span.OperationName);
        Assert.Equal(BrokenCode, span.DisplayName);
        Assert.Equal(ActivityKind.Internal, span.Kind);
        Assert.Equal(BrokenCode, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Code));
        Assert.Equal($"{BrokenCode} name", ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Name));
        Assert.Equal(TestRules.Category, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Category));
        Assert.Equal(RuleSeverityTags.Warning, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Severity));
        Assert.Equal(RuleOutcomeTags.Broken, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Outcome));
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
        Assert.Empty(span.Events);
        Assert.True(span.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public void EvaluateAll_PassedRule_SpanOutcomeIsPassed()
    {
        using ActivityCapture capture = new ActivityCapture();

        BusinessRuleChecker.EvaluateAll(new[] { TestRules.Sync(PassingCode, false) }, new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(TracingOptionsWithoutMetrics()) });

        Assert.Equal(RuleOutcomeTags.Passed, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
    }

    [Fact]
    public void EvaluateAll_ComposedRule_SpanHasNoCodeTagAndKeepsOperationName()
    {
        using ActivityCapture capture = new ActivityCapture();
        IBusinessRule composed = TestRules.Sync("A", true).And(TestRules.Sync("B", true));

        BusinessRuleChecker.EvaluateAll(new[] { composed }, new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(TracingOptionsWithoutMetrics()) });

        Activity span = capture.SingleRuleSpan();
        Assert.Null(span.GetTagItem(BusinessRuleSpanAttributes.Code));
        Assert.Equal(BusinessRulesActivitySource.RuleActivityName, span.DisplayName);
        Assert.Equal(RuleOutcomeTags.Broken, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Outcome));
    }

    [Fact]
    public void EvaluateAll_CustomCodeTagSelector_IsUsed()
    {
        using ActivityCapture capture = new ActivityCapture();
        TracingRuleObserverOptions options = TracingOptionsWithoutMetrics();
        options.CodeTagSelector = rule => $"custom:{rule.Code}";

        BusinessRuleChecker.EvaluateAll(new[] { TestRules.Sync(BrokenCode, true) }, new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(options) });

        Assert.Equal($"custom:{BrokenCode}", ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Code));
    }

    [Fact]
    public void EvaluateAll_ThrowingCodeTagSelector_LeavesSpanUntagged()
    {
        using ActivityCapture capture = new ActivityCapture();
        TracingRuleObserverOptions options = TracingOptionsWithoutMetrics();
        options.CodeTagSelector = rule => throw new InvalidOperationException("selector");

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { TestRules.Sync(BrokenCode, true) }, new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(options) });

        Assert.True(result.HasBrokenRules);
        Assert.Null(capture.SingleRuleSpan().GetTagItem(BusinessRuleSpanAttributes.Code));
    }

    [Fact]
    public void EvaluateAll_SameRuleInstanceTwice_ProducesTwoSpans()
    {
        using ActivityCapture capture = new ActivityCapture();
        IBusinessRule broken = TestRules.Sync(BrokenCode, true);

        BusinessRuleChecker.EvaluateAll(new[] { broken, broken }, new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(TracingOptionsWithoutMetrics()) });

        Assert.Equal(2, capture.RuleSpans.Count);
        Assert.All(capture.RuleSpans, span => Assert.Equal(RuleOutcomeTags.Broken, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Outcome)));
        Assert.Single(capture.BatchSpans);
    }

    [Fact]
    public void EvaluateAll_EmptyBatch_ProducesBatchSpanOnly()
    {
        using ActivityCapture capture = new ActivityCapture();

        BusinessRuleChecker.EvaluateAll(Array.Empty<IBusinessRule>(), new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(TracingOptionsWithoutMetrics()) });

        Activity batch = capture.SingleBatchSpan();
        Assert.Empty(capture.RuleSpans);
        Assert.Equal(0, batch.GetTagItem(BusinessRuleSpanAttributes.BatchRuleCount));
        Assert.Equal(RuleOutcomeTags.Passed, ActivityCapture.Tag(batch, BusinessRuleSpanAttributes.BatchOutcome));
    }

    [Fact]
    public void EvaluateAll_OutsideAnExistingActivity_BatchSpanIsItsChildAndCurrentIsRestored()
    {
        using ActivityCapture capture = new ActivityCapture();
        using Activity request = new Activity("http.request").Start();

        BusinessRuleChecker.EvaluateAll(new[] { TestRules.Sync(PassingCode, false) }, new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(TracingOptionsWithoutMetrics()) });

        Assert.Same(request, capture.SingleBatchSpan().Parent);
        Assert.Same(request, Activity.Current);
    }

    // ---------------------------------------------------------------------
    // The rule span is current while the rule runs
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_ActivityStartedInsideRule_HasRuleSpanAsParent()
    {
        using ActivityCapture capture = new ActivityCapture();
        Activity? childParent = null;
        IBusinessRule rule = TestRules.Sync(BrokenCode, true, onEvaluate: () =>
        {
            using Activity child = new Activity("repository.query").Start();
            childParent = child.Parent;
        });

        BusinessRuleChecker.EvaluateAll(new[] { rule }, new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(TracingOptionsWithoutMetrics()) });

        Activity ruleSpan = capture.SingleRuleSpan();
        Assert.NotNull(childParent);
        Assert.Same(ruleSpan, childParent);
        Assert.Equal(BrokenCode, ActivityCapture.Tag(childParent!, BusinessRuleSpanAttributes.Code));
    }

    // ---------------------------------------------------------------------
    // Message as event
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_MessageAsEvent_AddsBrokenEventWithResultMessage()
    {
        using ActivityCapture capture = new ActivityCapture();
        TracingRuleObserverOptions options = TracingOptionsWithoutMetrics();
        options.MessageAsEvent = true;

        BusinessRuleChecker.EvaluateAll(new[] { TestRules.Sync(BrokenCode, true, message: "no items"), TestRules.Sync(PassingCode, false) }, new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(options) });

        Activity brokenSpan = Assert.Single(capture.RuleSpans, span => ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Code) == BrokenCode);
        Activity passedSpan = Assert.Single(capture.RuleSpans, span => ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Code) == PassingCode);
        ActivityEvent brokenEvent = Assert.Single(brokenSpan.Events);
        Assert.Equal(BusinessRuleSpanAttributes.BrokenEventName, brokenEvent.Name);
        Assert.Equal("no items", brokenEvent.Tags.Single(tag => tag.Key == BusinessRuleSpanAttributes.Message).Value);
        Assert.Empty(passedSpan.Events);
    }

    // ---------------------------------------------------------------------
    // Faults
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_FaultRecorded_SpanIsFaultedWithErrorStatusAndExceptionEvent()
    {
        using ActivityCapture capture = new ActivityCapture();
        InvalidOperationException fault = new InvalidOperationException(TestRules.Boom);

        BusinessRuleChecker.EvaluateAll(new[] { TestRules.SyncThrowing(ThrowingCode, fault) }, new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(TracingOptionsWithoutMetrics()), OnFault = FaultPolicy.Record });

        Activity span = capture.SingleRuleSpan();
        Assert.Equal(RuleOutcomeTags.Faulted, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Outcome));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(TestRules.Boom, span.StatusDescription);
        ActivityEvent exceptionEvent = Assert.Single(span.Events);
        Assert.Equal(BusinessRuleSpanAttributes.ExceptionEventName, exceptionEvent.Name);
        Assert.Equal(typeof(InvalidOperationException).FullName, exceptionEvent.Tags.Single(tag => tag.Key == BusinessRuleSpanAttributes.ExceptionType).Value);
        Assert.Equal(TestRules.Boom, exceptionEvent.Tags.Single(tag => tag.Key == BusinessRuleSpanAttributes.ExceptionMessage).Value);

        Activity batch = capture.SingleBatchSpan();
        Assert.Equal(1, batch.GetTagItem(BusinessRuleSpanAttributes.BatchFaultedCount));
        Assert.Equal(RuleOutcomeTags.Faulted, ActivityCapture.Tag(batch, BusinessRuleSpanAttributes.BatchOutcome));
        Assert.Equal(ActivityStatusCode.Error, batch.Status);
    }

    [Fact]
    public void CheckAll_FaultPropagates_RuleSpanIsClosedButBatchSpanStaysOpen()
    {
        using ActivityCapture capture = new ActivityCapture();
        RuleEvaluationOptions options = new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(TracingOptionsWithoutMetrics()) };

        Assert.Throws<InvalidOperationException>(() => BusinessRuleChecker.CheckAll(new[] { TestRules.SyncThrowing(ThrowingCode) }, options));

        Assert.Equal(RuleOutcomeTags.Faulted, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
        Assert.Single(capture.Started, activity => activity.OperationName == BusinessRulesActivitySource.BatchActivityName);
        Assert.Empty(capture.BatchSpans);
    }

    // ---------------------------------------------------------------------
    // Shadow violations
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_ShadowViolation_AddsEventToBatchSpan()
    {
        using ActivityCapture capture = new ActivityCapture();
        TracingRuleObserverOptions options = TracingOptionsWithoutMetrics();
        options.MessageAsEvent = true;
        IBusinessRule gated = TestRules.Sync(BrokenCode, true, message: "would block").GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));

        BusinessRuleChecker.EvaluateAll(new[] { gated }, new BusinessRuleContext(), new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(options) });

        Assert.Equal(RuleOutcomeTags.Passed, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
        ActivityEvent shadowEvent = Assert.Single(capture.SingleBatchSpan().Events);
        Assert.Equal(BusinessRuleSpanAttributes.ShadowViolationEventName, shadowEvent.Name);
        Assert.Equal(BrokenCode, shadowEvent.Tags.Single(tag => tag.Key == BusinessRuleSpanAttributes.Code).Value);
        Assert.Equal("would block", shadowEvent.Tags.Single(tag => tag.Key == BusinessRuleSpanAttributes.Message).Value);
    }

    [Fact]
    public void EvaluateAll_ShadowViolationWithoutMessageAsEvent_EventCarriesCodeOnly()
    {
        using ActivityCapture capture = new ActivityCapture();
        IBusinessRule gated = TestRules.Sync(BrokenCode, true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));

        BusinessRuleChecker.EvaluateAll(new[] { gated }, new BusinessRuleContext(), new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(TracingOptionsWithoutMetrics()) });

        ActivityEvent shadowEvent = Assert.Single(capture.SingleBatchSpan().Events);
        Assert.DoesNotContain(shadowEvent.Tags, tag => tag.Key == BusinessRuleSpanAttributes.Message);
    }

    // ---------------------------------------------------------------------
    // Metrics
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_WithMetrics_RecordsEvaluationsDurationsAndBatch()
    {
        using MetricsCapture metrics = new MetricsCapture();
        IBusinessRule[] rules = { TestRules.Sync(BrokenCode, true), TestRules.Sync(PassingCode, false), TestRules.Sync("A", true).And(TestRules.Sync("B", true)) };

        BusinessRuleChecker.EvaluateAll(rules, new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(metrics.TracingOptions()) });

        Assert.Equal(3, metrics.Evaluations.Count);
        Measurement broken = Assert.Single(metrics.Evaluations, m => (string?)m[BusinessRulesMetrics.CodeTag] == BrokenCode);
        Assert.Equal(RuleOutcomeTags.Broken, broken[BusinessRulesMetrics.OutcomeTag]);
        Assert.Equal(TestRules.Category, broken[BusinessRulesMetrics.CategoryTag]);
        Assert.Equal(RuleSeverityTags.Error, broken[BusinessRulesMetrics.SeverityTag]);
        Measurement passed = Assert.Single(metrics.Evaluations, m => (string?)m[BusinessRulesMetrics.CodeTag] == PassingCode);
        Assert.Equal(RuleOutcomeTags.Passed, passed[BusinessRulesMetrics.OutcomeTag]);
        Measurement composed = Assert.Single(metrics.Evaluations, m => !m.Tags.ContainsKey(BusinessRulesMetrics.CodeTag));
        Assert.Equal(RuleOutcomeTags.Broken, composed[BusinessRulesMetrics.OutcomeTag]);

        Assert.Equal(3, metrics.Durations.Count);
        Assert.All(metrics.Durations, duration => Assert.Single(duration.Tags));
        Assert.All(metrics.Durations, duration => Assert.True(duration.Value >= 0));

        Measurement batch = Assert.Single(metrics.Batches);
        Assert.Equal(RuleOutcomeTags.Broken, batch[BusinessRulesMetrics.OutcomeTag]);
    }

    [Fact]
    public void EvaluateAll_FaultWithoutListener_RecordsFaultedEvaluationWithoutDuration()
    {
        using MetricsCapture metrics = new MetricsCapture();

        BusinessRuleChecker.EvaluateAll(new[] { TestRules.SyncThrowing(ThrowingCode) }, new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(metrics.TracingOptions()), OnFault = FaultPolicy.Record });

        Measurement faulted = Assert.Single(metrics.Evaluations);
        Assert.Equal(RuleOutcomeTags.Faulted, faulted[BusinessRulesMetrics.OutcomeTag]);
        Assert.Empty(metrics.Durations);
        Assert.Equal(RuleOutcomeTags.Faulted, Assert.Single(metrics.Batches)[BusinessRulesMetrics.OutcomeTag]);
    }

    [Fact]
    public void EvaluateAll_FaultWithListener_RecordsDurationFromSpan()
    {
        using ActivityCapture capture = new ActivityCapture();
        using MetricsCapture metrics = new MetricsCapture();

        BusinessRuleChecker.EvaluateAll(new[] { TestRules.SyncThrowing(ThrowingCode) }, new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(metrics.TracingOptions()), OnFault = FaultPolicy.Record });

        Measurement duration = Assert.Single(metrics.Durations);
        Assert.Equal(RuleOutcomeTags.Faulted, duration[BusinessRulesMetrics.OutcomeTag]);
    }

    [Fact]
    public void EvaluateAll_ShadowViolationWithMetrics_RecordsShadowOutcomeInAdditionToPassed()
    {
        using MetricsCapture metrics = new MetricsCapture();
        IBusinessRule gated = TestRules.Sync(BrokenCode, true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));

        BusinessRuleChecker.EvaluateAll(new[] { gated }, new BusinessRuleContext(), new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(metrics.TracingOptions()) });

        Assert.Equal(2, metrics.Evaluations.Count);
        Assert.Single(metrics.Evaluations, m => (string?)m[BusinessRulesMetrics.OutcomeTag] == RuleOutcomeTags.Passed);
        Measurement shadow = Assert.Single(metrics.Evaluations, m => (string?)m[BusinessRulesMetrics.OutcomeTag] == RuleOutcomeTags.Shadow);
        Assert.Equal(BrokenCode, shadow[BusinessRulesMetrics.CodeTag]);
        Assert.Single(metrics.Durations);
    }

    [Fact]
    public void EvaluateAll_RecordMetricsFalse_RecordsNothing()
    {
        using MetricsCapture metrics = new MetricsCapture();

        BusinessRuleChecker.EvaluateAll(new[] { TestRules.Sync(BrokenCode, true) }, new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(metrics.TracingOptions(o => o.RecordMetrics = false)) });

        Assert.Empty(metrics.Measurements);
    }

    [Fact]
    public void EvaluateAll_NoListenerNoMetrics_StillReturnsVerdict()
    {
        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { TestRules.Sync(BrokenCode, true) }, new RuleEvaluationOptions { Observer = new TracingRuleExecutionObserver(TracingOptionsWithoutMetrics()) });

        Assert.True(result.HasBrokenRules);
        Assert.Null(Activity.Current);
    }

    private static TracingRuleObserverOptions TracingOptionsWithoutMetrics()
    {
        return new TracingRuleObserverOptions { RecordMetrics = false };
    }
}
