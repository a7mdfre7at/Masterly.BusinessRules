using System.Diagnostics;
using Masterly.BusinessRules.Diagnostics.UnitTests.Support;
using Microsoft.Extensions.Logging;

namespace Masterly.BusinessRules.Diagnostics.UnitTests.Composition;

[Collection(DiagnosticsCollections.Activities)]
public class DiagnosticsAsyncBusinessRuleTests
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
        IAsyncBusinessRule inner = TestRules.Async(BrokenCode, true);

        DiagnosticsAsyncBusinessRule traced = inner.Traced();

        Assert.Same(inner, traced.InnerRule);
        Assert.IsAssignableFrom<IDecoratedAsyncBusinessRule>(traced);
        Assert.False(traced.IsLogging);
        Assert.NotNull(traced.Options);
    }

    [Fact]
    public void Traced_WithOptionsAndLogger_KeepsThem()
    {
        TracingRuleObserverOptions options = new TracingRuleObserverOptions { MessageAsEvent = true };

        DiagnosticsAsyncBusinessRule traced = TestRules.Async(BrokenCode, true).Traced(new CapturingLogger(), options);

        Assert.Same(options, traced.Options);
        Assert.True(traced.IsLogging);
    }

    [Fact]
    public void Constructor_NullInnerRule_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DiagnosticsAsyncBusinessRule(null!, null));
        Assert.Throws<ArgumentNullException>(() => ((IAsyncBusinessRule)null!).Traced());
    }

    [Fact]
    public void Traced_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TestRules.Async(BrokenCode, true).Traced((ILogger)null!));
    }

    [Fact]
    public void Decorator_ForwardsAllSevenMetadataMembers()
    {
        IAsyncBusinessRule inner = AsyncBusinessRuleBuilder.Create(BrokenCode)
            .WithMessage("message")
            .WithSeverity(RuleSeverity.Warning)
            .WithName("name")
            .WithDescription("description")
            .WithCategory("category")
            .WithTags("t1", "t2")
            .WhenAsync((ctx, ct) => Task.FromResult(true))
            .Build();

        DiagnosticsAsyncBusinessRule traced = inner.Traced();

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
    public async Task IsBrokenAsync_BrokenRule_ReturnsTrueAndRecordsBrokenSpan()
    {
        using ActivityCapture capture = new ActivityCapture();
        DiagnosticsAsyncBusinessRule traced = TestRules.Async(BrokenCode, true, RuleSeverity.Warning, onEvaluate: () => Task.Delay(1)).Traced(WithoutMetrics());

        bool isBroken = await traced.IsBrokenAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken);

        Assert.True(isBroken);
        Activity span = capture.SingleRuleSpan();
        Assert.Equal(BrokenCode, span.DisplayName);
        Assert.Equal(BrokenCode, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Code));
        Assert.Equal(TestRules.Category, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Category));
        Assert.Equal(RuleSeverityTags.Warning, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Severity));
        Assert.Equal(RuleOutcomeTags.Broken, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Outcome));
        Assert.Null(Activity.Current);
    }

    [Fact]
    public async Task IsBrokenAsync_PassingRule_ReturnsFalseAndRecordsPassedSpan()
    {
        using ActivityCapture capture = new ActivityCapture();

        bool isBroken = await TestRules.Async(PassingCode, false).Traced(WithoutMetrics()).IsBrokenAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken);

        Assert.False(isBroken);
        Assert.Equal(RuleOutcomeTags.Passed, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
    }

    [Fact]
    public async Task IsBrokenAsync_WithContext_ForwardsContextToInnerRule()
    {
        IAsyncBusinessRule inner = AsyncBusinessRuleBuilder.Create(BrokenCode).WithMessage("m").WhenAsync(ctx => Task.FromResult(ctx.ContainsKey(ContextKey))).Build();
        DiagnosticsAsyncBusinessRule traced = inner.Traced(WithoutMetrics());

        Assert.True(await traced.IsBrokenAsync(RuleTestHelper.CreateContext((ContextKey, true)), TestContext.Current.CancellationToken));
        Assert.False(await traced.IsBrokenAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EvaluateAsync_BrokenRule_ReturnsInnerResultAndUsesItsMessageAsEvent()
    {
        using ActivityCapture capture = new ActivityCapture();
        DiagnosticsAsyncBusinessRule traced = TestRules.Async(BrokenCode, true, message: "no items").Traced(new TracingRuleObserverOptions { RecordMetrics = false, MessageAsEvent = true });

        BusinessRuleResult? result = await traced.EvaluateAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(BrokenCode, result!.Code);
        Activity span = capture.SingleRuleSpan();
        Assert.Equal(RuleOutcomeTags.Broken, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Outcome));
        Assert.Equal("no items", Assert.Single(span.Events).Tags.Single(tag => tag.Key == BusinessRuleSpanAttributes.Message).Value);
    }

    [Fact]
    public async Task EvaluateAsync_PassingRule_ReturnsNullAndRecordsPassedSpan()
    {
        using ActivityCapture capture = new ActivityCapture();

        BusinessRuleResult? result = await TestRules.Async(PassingCode, false).Traced(WithoutMetrics()).EvaluateAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(RuleOutcomeTags.Passed, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
    }

    [Fact]
    public async Task CheckAsync_BrokenRule_RethrowsInnerExceptionAndRecordsBrokenSpan()
    {
        using ActivityCapture capture = new ActivityCapture();
        DiagnosticsAsyncBusinessRule traced = TestRules.Async(BrokenCode, true).Traced(WithoutMetrics());

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(() => traced.CheckAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken));

        Assert.Equal(BrokenCode, Assert.Single(exception.BrokenRules).Code);
        Assert.Equal(RuleOutcomeTags.Broken, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
    }

    [Fact]
    public async Task CheckAsync_PassingRule_DoesNotThrowAndRecordsPassedSpan()
    {
        using ActivityCapture capture = new ActivityCapture();

        await TestRules.Async(PassingCode, false).Traced(WithoutMetrics()).CheckAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken);

        Assert.Equal(RuleOutcomeTags.Passed, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
    }

    [Fact]
    public async Task CheckAsync_ComposedRuleWithMessageAsEvent_AddsOneEventPerBrokenResult()
    {
        using ActivityCapture capture = new ActivityCapture();
        IAsyncBusinessRule composed = TestRules.Async("A", true, message: "a broke").And(TestRules.Async("B", true, message: "b broke"));
        DiagnosticsAsyncBusinessRule traced = composed.Traced(new TracingRuleObserverOptions { RecordMetrics = false, MessageAsEvent = true });

        await Assert.ThrowsAsync<BusinessRuleValidationException>(() => traced.CheckAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken));

        Activity span = capture.SingleRuleSpan();
        Assert.Null(span.GetTagItem(BusinessRuleSpanAttributes.Code));
        Assert.Single(span.Events);
    }

    [Fact]
    public async Task IsBrokenAsync_ActivityStartedInsideRuleAfterAwait_HasDecoratorSpanAsParent()
    {
        using ActivityCapture capture = new ActivityCapture();
        Activity? childParent = null;
        DiagnosticsAsyncBusinessRule traced = TestRules.Async(BrokenCode, true, onEvaluate: async () =>
        {
            await Task.Delay(1);
            using Activity child = new Activity("repository.query").Start();
            childParent = child.Parent;
        }).Traced(WithoutMetrics());

        await traced.IsBrokenAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken);

        Assert.Same(capture.SingleRuleSpan(), childParent);
    }

    [Fact]
    public async Task IsBrokenAsync_InsideAnExistingActivity_SpanIsItsChildAndCurrentIsRestored()
    {
        using ActivityCapture capture = new ActivityCapture();
        using Activity request = new Activity("http.request").Start();

        await TestRules.Async(PassingCode, false).Traced(WithoutMetrics()).IsBrokenAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken);

        Assert.Same(request, capture.SingleRuleSpan().Parent);
        Assert.Same(request, Activity.Current);
    }

    // ---------------------------------------------------------------------
    // Faults and cancellation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task IsBrokenAsync_ThrowingRule_RethrowsSameExceptionAndRecordsFaultedSpan()
    {
        using ActivityCapture capture = new ActivityCapture();
        InvalidOperationException fault = new InvalidOperationException(TestRules.Boom);

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => TestRules.AsyncThrowing(ThrowingCode, fault).Traced(WithoutMetrics()).IsBrokenAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken));

        Assert.Same(fault, thrown);
        Activity span = capture.SingleRuleSpan();
        Assert.Equal(RuleOutcomeTags.Faulted, ActivityCapture.Tag(span, BusinessRuleSpanAttributes.Outcome));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(BusinessRuleSpanAttributes.ExceptionEventName, Assert.Single(span.Events).Name);
    }

    [Fact]
    public async Task EvaluateAsync_ThrowingRule_RethrowsAndRecordsFaultedSpan()
    {
        using ActivityCapture capture = new ActivityCapture();

        await Assert.ThrowsAsync<InvalidOperationException>(() => TestRules.AsyncThrowing(ThrowingCode).Traced(WithoutMetrics()).EvaluateAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken));

        Assert.Equal(RuleOutcomeTags.Faulted, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
    }

    [Fact]
    public async Task CheckAsync_ThrowingRule_RethrowsAndRecordsFaultedSpan()
    {
        using ActivityCapture capture = new ActivityCapture();

        await Assert.ThrowsAsync<InvalidOperationException>(() => TestRules.AsyncThrowing(ThrowingCode).Traced(WithoutMetrics()).CheckAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken));

        Assert.Equal(RuleOutcomeTags.Faulted, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
    }

    [Fact]
    public async Task IsBrokenAsync_CancelledToken_ClosesSpanAsCancelledWithoutMetricsOrLog()
    {
        using ActivityCapture capture = new ActivityCapture();
        using MetricsCapture metrics = new MetricsCapture();
        CapturingLogger logger = new CapturingLogger();
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();
        DiagnosticsAsyncBusinessRule traced = TestRules.AsyncCancellable(BrokenCode).Traced(logger, metrics.TracingOptions());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => traced.IsBrokenAsync(new BusinessRuleContext(), source.Token));

        Assert.Equal(RuleOutcomeTags.Cancelled, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
        Assert.Empty(metrics.Measurements);
        Assert.Empty(logger.Entries);
        Assert.Null(Activity.Current);
    }

    [Fact]
    public async Task CheckAsync_CancelledToken_ClosesSpanAsCancelled()
    {
        using ActivityCapture capture = new ActivityCapture();
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TestRules.AsyncCancellable(BrokenCode).Traced(WithoutMetrics()).CheckAsync(new BusinessRuleContext(), source.Token));

        Assert.Equal(RuleOutcomeTags.Cancelled, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
    }

    // ---------------------------------------------------------------------
    // Metrics and logging
    // ---------------------------------------------------------------------

    [Fact]
    public async Task IsBrokenAsync_WithMetrics_RecordsEvaluationAndDurationWithCode()
    {
        using MetricsCapture metrics = new MetricsCapture();

        await TestRules.Async(BrokenCode, true).Traced(metrics.TracingOptions()).IsBrokenAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken);

        Measurement evaluation = Assert.Single(metrics.Evaluations);
        Assert.Equal(BrokenCode, evaluation[BusinessRulesMetrics.CodeTag]);
        Assert.Equal(RuleOutcomeTags.Broken, evaluation[BusinessRulesMetrics.OutcomeTag]);
        Assert.Equal(RuleOutcomeTags.Broken, Assert.Single(metrics.Durations)[BusinessRulesMetrics.OutcomeTag]);
        Assert.Empty(metrics.Batches);
    }

    [Fact]
    public async Task EvaluateAsync_ThrowingRuleWithMetrics_RecordsFaultedEvaluationWithDuration()
    {
        using MetricsCapture metrics = new MetricsCapture();

        await Assert.ThrowsAsync<InvalidOperationException>(() => TestRules.AsyncThrowing(ThrowingCode).Traced(metrics.TracingOptions()).EvaluateAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken));

        Assert.Equal(RuleOutcomeTags.Faulted, Assert.Single(metrics.Evaluations)[BusinessRulesMetrics.OutcomeTag]);
        Assert.Single(metrics.Durations);
    }

    [Fact]
    public async Task CheckAsync_WithLogger_LogsBrokenLineAtMappedLevel()
    {
        CapturingLogger logger = new CapturingLogger();
        DiagnosticsAsyncBusinessRule traced = TestRules.Async(BrokenCode, true, RuleSeverity.Info, message: "no items").Traced(logger, WithoutMetrics());

        await Assert.ThrowsAsync<BusinessRuleValidationException>(() => traced.CheckAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken));

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Equal(RuleLogEvents.RuleBroken, entry.EventId);
        Assert.Equal(BrokenCode, entry["RuleCode"]);
        Assert.Equal("no items", entry["Message"]);
    }

    [Fact]
    public async Task IsBrokenAsync_WithLoggerAndThrowingRule_LogsFaultWithException()
    {
        CapturingLogger logger = new CapturingLogger();
        InvalidOperationException fault = new InvalidOperationException(TestRules.Boom);

        await Assert.ThrowsAsync<InvalidOperationException>(() => TestRules.AsyncThrowing(ThrowingCode, fault).Traced(logger, WithoutMetrics()).IsBrokenAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken));

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(RuleLogEvents.RuleFaulted, entry.EventId);
        Assert.Same(fault, entry.Exception);
    }

    [Fact]
    public async Task IsBrokenAsync_NoListener_StillReturnsVerdict()
    {
        bool isBroken = await TestRules.Async(BrokenCode, true).Traced(WithoutMetrics()).IsBrokenAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken);

        Assert.True(isBroken);
        Assert.Null(Activity.Current);
    }

    // ---------------------------------------------------------------------
    // Walker paths: details and explanation subtree survive the decorator
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CheckAllAsync_TracedGroup_KeepsTheMemberDetails()
    {
        using ActivityCapture capture = new ActivityCapture();
        IAsyncBusinessRule group = AsyncRuleGroup.All("KYC", "KYC failed", TestRules.Async("A", true), TestRules.Async("B", true));

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), new[] { group.Traced(WithoutMetrics()) }, new AsyncRuleEvaluationOptions(), TestContext.Current.CancellationToken));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(new[] { "A", "B" }, result.Details.Select(detail => detail.Code));
        Assert.Equal(RuleOutcomeTags.Broken, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
    }

    [Fact]
    public async Task ExplainAsync_TracedGroup_RecordsTheGroupSubtreeUnderATracedNode()
    {
        IAsyncBusinessRule group = AsyncRuleGroup.All("KYC", "KYC failed", TestRules.Async("A", true), TestRules.Async("B", false));

        RuleExplanation explanation = await group.Traced(WithoutMetrics()).ExplainAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken);

        Assert.Equal(DiagnosticsText.TracedOperator, explanation.Operator);
        Assert.Equal(RuleOutcome.Broken, explanation.Outcome);
        RuleExplanation groupNode = Assert.Single(explanation.Children);
        Assert.Equal("KYC", groupNode.Code);
        Assert.Equal(new[] { "A", "B" }, groupNode.Children.Select(child => child.Code));
    }

    [Fact]
    public void ExplanationOperator_IsTraced()
    {
        DiagnosticsAsyncBusinessRule traced = TestRules.Async(BrokenCode, true).Traced();

        Assert.Equal("TRACED", traced.ExplanationOperator);
        Assert.IsAssignableFrom<IExplainableDecoratedAsyncBusinessRule>(traced);
    }

    [Fact]
    public async Task EvaluateDecoratedAsync_NullEvaluation_Throws()
    {
        DiagnosticsAsyncBusinessRule traced = TestRules.Async(BrokenCode, true).Traced();

        await Assert.ThrowsAsync<ArgumentNullException>(() => traced.EvaluateDecoratedAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CheckAllAsync_TracedRuleThrows_RecordsAFaultedDecoratorSpan()
    {
        using ActivityCapture capture = new ActivityCapture();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), new[] { TestRules.AsyncThrowing(ThrowingCode).Traced(WithoutMetrics()) }, new AsyncRuleEvaluationOptions(), TestContext.Current.CancellationToken));

        Assert.Equal(RuleOutcomeTags.Faulted, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
        Assert.Null(Activity.Current);
    }

    [Fact]
    public async Task CheckAllAsync_TracedRuleCancelled_ClosesTheDecoratorSpanAsCancelledWithoutMetrics()
    {
        using ActivityCapture capture = new ActivityCapture();
        using MetricsCapture metrics = new MetricsCapture();
        IAsyncBusinessRule cancelling = TestRules.AsyncThrowing(BrokenCode, new OperationCanceledException()).Traced(metrics.TracingOptions());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), new[] { cancelling }, new AsyncRuleEvaluationOptions(), TestContext.Current.CancellationToken));

        Assert.Equal(RuleOutcomeTags.Cancelled, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
        Assert.Empty(metrics.Measurements);
        Assert.Null(Activity.Current);
    }

    [Fact]
    public async Task EvaluateAsync_CancelledToken_ClosesSpanAsCancelledWithoutMetricsOrLog()
    {
        using ActivityCapture capture = new ActivityCapture();
        using MetricsCapture metrics = new MetricsCapture();
        CapturingLogger logger = new CapturingLogger();
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();
        DiagnosticsAsyncBusinessRule traced = TestRules.AsyncCancellable(BrokenCode).Traced(logger, metrics.TracingOptions());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => traced.EvaluateAsync(new BusinessRuleContext(), source.Token));

        Assert.Equal(RuleOutcomeTags.Cancelled, ActivityCapture.Tag(capture.SingleRuleSpan(), BusinessRuleSpanAttributes.Outcome));
        Assert.Empty(metrics.Measurements);
        Assert.Empty(logger.Entries);
        Assert.Null(Activity.Current);
    }

    private static TracingRuleObserverOptions WithoutMetrics()
    {
        return new TracingRuleObserverOptions { RecordMetrics = false };
    }
}
