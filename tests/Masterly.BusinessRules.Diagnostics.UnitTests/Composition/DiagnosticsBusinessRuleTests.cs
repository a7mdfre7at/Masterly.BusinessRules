using System.Diagnostics;
using Masterly.BusinessRules.Diagnostics.UnitTests.Support;
using Microsoft.Extensions.Logging;

namespace Masterly.BusinessRules.Diagnostics.UnitTests.Composition;

[Collection(DiagnosticsCollections.Activities)]
public class DiagnosticsBusinessRuleTests
{
    private const string BrokenCode = "ORDER.NO_ITEMS";
    private const string PassingCode = "ORDER.OK";
    private const string ThrowingCode = "ORDER.THROWS";
    private const string ContextKey = "flag";

    // ---------------------------------------------------------------------
    // Construction and metadata
    // ---------------------------------------------------------------------

    [Fact]
    public void Traced_Rule_ReturnsDecoratorOverTheRule()
    {
        IBusinessRule inner = TestRules.Sync(BrokenCode, true);

        DiagnosticsBusinessRule traced = inner.Traced();

        Assert.Same(inner, traced.InnerRule);
        Assert.IsAssignableFrom<IDecoratedBusinessRule>(traced);
        Assert.False(traced.IsLogging);
        Assert.NotNull(traced.Options);
    }

    [Fact]
    public void Traced_WithOptionsAndLogger_KeepsThem()
    {
        TracingRuleObserverOptions options = new TracingRuleObserverOptions { MessageAsEvent = true };

        DiagnosticsBusinessRule traced = TestRules.Sync(BrokenCode, true).Traced(new CapturingLogger(), options);

        Assert.Same(options, traced.Options);
        Assert.True(traced.IsLogging);
    }

    [Fact]
    public void Constructor_NullInnerRule_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DiagnosticsBusinessRule(null!, null));
        Assert.Throws<ArgumentNullException>(() => ((IBusinessRule)null!).Traced());
    }

    [Fact]
    public void Traced_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TestRules.Sync(BrokenCode, true).Traced((ILogger)null!));
    }

    [Fact]
    public void Decorator_ForwardsAllSevenMetadataMembers()
    {
        IBusinessRule inner = BusinessRuleBuilder.Create(BrokenCode)
            .WithMessage("message")
            .WithSeverity(RuleSeverity.Warning)
            .WithName("name")
            .WithDescription("description")
            .WithCategory("category")
            .WithTags("t1", "t2")
            .When(() => true)
            .Build();

        DiagnosticsBusinessRule traced = inner.Traced();

        Assert.Equal(inner.Code, traced.Code);
        Assert.Equal(inner.Message, traced.Message);
        Assert.Equal(inner.Severity, traced.Severity);
        Assert.Equal(inner.Name, traced.Name);
        Assert.Equal(inner.Description, traced.Description);
        Assert.Equal(inner.Category, traced.Category);
        Assert.Equal(inner.Tags, traced.Tags);
    }

    // ---------------------------------------------------------------------
    // Verdicts and spans
    // ---------------------------------------------------------------------

    [Fact]
    public void IsBroken_BrokenRule_ReturnsTrueAndRecordsBrokenSpan()
    {
        using ActivityCapture capture = new ActivityCapture();
        DiagnosticsBusinessRule traced = TestRules.Sync(BrokenCode, true, RuleSeverity.Warning).Traced(WithoutMetrics());

        bool isBroken = traced.IsBroken();

        Assert.True(isBroken);
        Activity span = capture.SingleRuleSpan();
        Assert.Equal(BrokenCode, span.DisplayName);
        Assert.Equal(BrokenCode, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Code));
        Assert.Equal($"{BrokenCode} name", ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Name));
        Assert.Equal(TestRules.Category, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Category));
        Assert.Equal(RuleSeverityTags.Warning, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Severity));
        Assert.Equal(RuleOutcomeTags.Broken, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Outcome));
        Assert.Empty(capture.BatchSpans);
        Assert.Null(Activity.Current);
    }

    [Fact]
    public void IsBroken_PassingRule_ReturnsFalseAndRecordsPassedSpan()
    {
        using ActivityCapture capture = new ActivityCapture();

        bool isBroken = TestRules.Sync(PassingCode, false).Traced(WithoutMetrics()).IsBroken();

        Assert.False(isBroken);
        Assert.Equal(RuleOutcomeTags.Passed, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
    }

    [Fact]
    public void IsBroken_WithContext_ForwardsContextToInnerRule()
    {
        using ActivityCapture capture = new ActivityCapture();
        IBusinessRule inner = BusinessRuleBuilder.Create(BrokenCode).WithMessage("m").When(ctx => ctx.ContainsKey(ContextKey)).Build();
        DiagnosticsBusinessRule traced = inner.Traced(WithoutMetrics());

        Assert.True(traced.IsBroken(RuleTestHelper.CreateContext((ContextKey, true))));
        Assert.False(traced.IsBroken(new BusinessRuleContext()));
        Assert.Equal(2, capture.RuleSpans.Count);
    }

    [Fact]
    public void Check_BrokenRule_RethrowsInnerExceptionAndRecordsBrokenSpan()
    {
        using ActivityCapture capture = new ActivityCapture();
        DiagnosticsBusinessRule traced = TestRules.Sync(BrokenCode, true).Traced(WithoutMetrics());

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => traced.Check());

        Assert.Equal(BrokenCode, Assert.Single(exception.BrokenRules).Code);
        Assert.Equal(RuleOutcomeTags.Broken, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
    }

    [Fact]
    public void Check_PassingRuleWithContext_DoesNotThrowAndRecordsPassedSpan()
    {
        using ActivityCapture capture = new ActivityCapture();

        TestRules.Sync(PassingCode, false).Traced(WithoutMetrics()).Check(new BusinessRuleContext());

        Assert.Equal(RuleOutcomeTags.Passed, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
    }

    [Fact]
    public void Check_CompositeRule_RethrowsEveryMemberAndAddsOneEventPerMember()
    {
        using ActivityCapture capture = new ActivityCapture();
        IBusinessRule composite = new CompositeBusinessRule(new[] { TestRules.Sync("A", true, message: "a broke"), TestRules.Sync("B", true, message: "b broke") });
        DiagnosticsBusinessRule traced = composite.Traced(new TracingRuleObserverOptions { RecordMetrics = false, MessageAsEvent = true });

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => traced.Check());

        Assert.Equal(2, exception.BrokenRules.Count);
        Activity span = capture.SingleRuleSpan();
        Assert.Null(span.GetTagItem(BusinessRuleSpanAttributes.Code));
        Assert.Equal(new[] { "a broke", "b broke" }, span.Events.Select(e => (string?)e.Tags.Single(t => t.Key == BusinessRuleSpanAttributes.Message).Value));
    }

    [Fact]
    public void Evaluate_IsAPureFactory_ForwardsWithoutRecording()
    {
        using ActivityCapture capture = new ActivityCapture();
        DiagnosticsBusinessRule traced = TestRules.Sync(BrokenCode, true).Traced(WithoutMetrics());

        BusinessRuleResult result = traced.Evaluate();
        BusinessRuleResult contextResult = traced.Evaluate(new BusinessRuleContext());

        Assert.Equal(BrokenCode, result.Code);
        Assert.Equal(BrokenCode, contextResult.Code);
        Assert.Empty(capture.Started);
    }

    [Fact]
    public void IsBroken_MessageAsEvent_AddsBrokenEventWithRuleMessage()
    {
        using ActivityCapture capture = new ActivityCapture();
        DiagnosticsBusinessRule traced = TestRules.Sync(BrokenCode, true, message: "no items").Traced(new TracingRuleObserverOptions { RecordMetrics = false, MessageAsEvent = true });

        traced.IsBroken();

        ActivityEvent brokenEvent = Assert.Single(capture.SingleRuleSpan().Events);
        Assert.Equal(BusinessRuleSpanAttributes.BrokenEventName, brokenEvent.Name);
        Assert.Equal("no items", brokenEvent.Tags.Single(tag => tag.Key == BusinessRuleSpanAttributes.Message).Value);
    }

    [Fact]
    public void IsBroken_ActivityStartedInsideRule_HasDecoratorSpanAsParent()
    {
        using ActivityCapture capture = new ActivityCapture();
        Activity? childParent = null;
        DiagnosticsBusinessRule traced = TestRules.Sync(BrokenCode, true, onEvaluate: () =>
        {
            using Activity child = new Activity("repository.query").Start();
            childParent = child.Parent;
        }).Traced(WithoutMetrics());

        traced.IsBroken();

        Assert.Same(capture.SingleRuleSpan(), childParent);
    }

    [Fact]
    public void IsBroken_InsideAnExistingActivity_SpanIsItsChildAndCurrentIsRestored()
    {
        using ActivityCapture capture = new ActivityCapture();
        using Activity request = new Activity("http.request").Start();

        TestRules.Sync(PassingCode, false).Traced(WithoutMetrics()).IsBroken();

        Assert.Same(request, capture.SingleRuleSpan().Parent);
        Assert.Same(request, Activity.Current);
    }

    // ---------------------------------------------------------------------
    // Faults
    // ---------------------------------------------------------------------

    [Fact]
    public void IsBroken_ThrowingRule_RethrowsSameExceptionAndRecordsFaultedSpan()
    {
        using ActivityCapture capture = new ActivityCapture();
        InvalidOperationException fault = new InvalidOperationException(TestRules.Boom);
        DiagnosticsBusinessRule traced = TestRules.SyncThrowing(ThrowingCode, fault).Traced(WithoutMetrics());

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() => traced.IsBroken());

        Assert.Same(fault, thrown);
        Activity span = capture.SingleRuleSpan();
        Assert.Equal(RuleOutcomeTags.Faulted, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Outcome));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(BusinessRuleSpanAttributes.ExceptionEventName, Assert.Single(span.Events).Name);
        Assert.Null(Activity.Current);
    }

    [Fact]
    public void Check_ThrowingRule_RethrowsSameExceptionAndRecordsFaultedSpan()
    {
        using ActivityCapture capture = new ActivityCapture();
        InvalidOperationException fault = new InvalidOperationException(TestRules.Boom);

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() => TestRules.SyncThrowing(ThrowingCode, fault).Traced(WithoutMetrics()).Check());

        Assert.Same(fault, thrown);
        Assert.Equal(RuleOutcomeTags.Faulted, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
    }

    [Fact]
    public void IsBroken_CancelledRule_ClosesSpanAsCancelledWithoutMetricsOrLog()
    {
        using ActivityCapture capture = new ActivityCapture();
        using MetricsCapture metrics = new MetricsCapture();
        CapturingLogger logger = new CapturingLogger();
        IBusinessRule cancelling = BusinessRuleBuilder.Create(BrokenCode).WithMessage("m").When(() => throw new OperationCanceledException()).Build();

        Assert.Throws<OperationCanceledException>(() => cancelling.Traced(logger, metrics.TracingOptions()).IsBroken());

        Assert.Equal(RuleOutcomeTags.Cancelled, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
        Assert.Empty(metrics.Measurements);
        Assert.Empty(logger.Entries);
        Assert.Null(Activity.Current);
    }

    // ---------------------------------------------------------------------
    // Metrics and logging
    // ---------------------------------------------------------------------

    [Fact]
    public void IsBroken_WithMetrics_RecordsEvaluationAndDurationWithCode()
    {
        using MetricsCapture metrics = new MetricsCapture();

        TestRules.Sync(BrokenCode, true).Traced(metrics.TracingOptions()).IsBroken();

        Measurement evaluation = Assert.Single(metrics.Evaluations);
        Assert.Equal(BrokenCode, evaluation[BusinessRulesMetrics.CodeTag]);
        Assert.Equal(RuleOutcomeTags.Broken, evaluation[BusinessRulesMetrics.OutcomeTag]);
        Measurement duration = Assert.Single(metrics.Durations);
        Assert.Equal(RuleOutcomeTags.Broken, duration[BusinessRulesMetrics.OutcomeTag]);
        Assert.Empty(metrics.Batches);
    }

    [Fact]
    public void IsBroken_ThrowingRuleWithMetrics_RecordsFaultedEvaluationWithDuration()
    {
        using MetricsCapture metrics = new MetricsCapture();

        Assert.Throws<InvalidOperationException>(() => TestRules.SyncThrowing(ThrowingCode).Traced(metrics.TracingOptions()).IsBroken());

        Assert.Equal(RuleOutcomeTags.Faulted, Assert.Single(metrics.Evaluations)[BusinessRulesMetrics.OutcomeTag]);
        Assert.Equal(RuleOutcomeTags.Faulted, Assert.Single(metrics.Durations)[BusinessRulesMetrics.OutcomeTag]);
    }

    [Fact]
    public void Check_WithLogger_LogsBrokenLineAtMappedLevel()
    {
        CapturingLogger logger = new CapturingLogger();
        DiagnosticsBusinessRule traced = TestRules.Sync(BrokenCode, true, RuleSeverity.Error, message: "no items").Traced(logger, WithoutMetrics());

        Assert.Throws<BusinessRuleValidationException>(() => traced.Check());

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(RuleLogEvents.RuleBroken, entry.EventId);
        Assert.Equal(BrokenCode, entry["RuleCode"]);
        Assert.Equal("no items", entry["Message"]);
        Assert.Equal(RuleOutcomeTags.Broken, entry["Outcome"]);
    }

    [Fact]
    public void IsBroken_WithLoggerAndPassedRule_LogsOnlyWhenEnabled()
    {
        CapturingLogger logger = new CapturingLogger();
        IBusinessRule passing = TestRules.Sync(PassingCode, false);

        passing.Traced(logger, WithoutMetrics()).IsBroken();
        passing.Traced(logger, WithoutMetrics(), new LoggingRuleObserverOptions { LogPassedRules = true }).IsBroken();

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(RuleLogEvents.RulePassed, entry.EventId);
        Assert.Equal(PassingCode, entry["RuleCode"]);
    }

    [Fact]
    public void IsBroken_WithLoggerAndThrowingRule_LogsFaultWithException()
    {
        CapturingLogger logger = new CapturingLogger();
        InvalidOperationException fault = new InvalidOperationException(TestRules.Boom);

        Assert.Throws<InvalidOperationException>(() => TestRules.SyncThrowing(ThrowingCode, fault).Traced(logger, WithoutMetrics()).IsBroken());

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(RuleLogEvents.RuleFaulted, entry.EventId);
        Assert.Same(fault, entry.Exception);
    }

    [Fact]
    public void IsBroken_NoListener_StillReturnsVerdict()
    {
        bool isBroken = TestRules.Sync(BrokenCode, true).Traced(WithoutMetrics()).IsBroken();

        Assert.True(isBroken);
        Assert.Null(Activity.Current);
    }

    private static TracingRuleObserverOptions WithoutMetrics()
    {
        return new TracingRuleObserverOptions { RecordMetrics = false };
    }
}
