using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// An <see cref="IAsyncRuleExecutionObserver"/> that turns an <see cref="AsyncBusinessRuleChecker"/> batch into one
    /// <c>BusinessRules.Batch</c> span with a <c>BusinessRule.Evaluate</c> child span per rule and records the
    /// <see cref="BusinessRulesMetrics"/> instruments. Every callback completes synchronously (it returns
    /// <see cref="Task.CompletedTask"/> and is deliberately not an <c>async</c> method), so the span it starts is
    /// <see cref="Activity.Current"/> in the checker's evaluation flow when the rule body runs; in a parallel batch each
    /// rule runs in its own flow and sees its own span as current.
    /// The observer holds no per-rule state and can be shared by concurrent batches.
    /// </summary>
    /// <remarks>
    /// Under <see cref="FaultPolicy.Throw"/> the checker lets the fault propagate without raising
    /// <see cref="IAsyncRuleExecutionObserver.OnBatchCompletedAsync"/>; the rule span is still finished from
    /// <see cref="OnRuleFaultedAsync"/>, but the batch span is never stopped and is therefore never exported. Prefer
    /// <see cref="FaultPolicy.Record"/> or <see cref="FaultPolicy.TreatAsBroken"/> for complete traces.
    /// </remarks>
    /// <example>
    /// <code>
    /// TracingAsyncRuleExecutionObserver tracing = new TracingAsyncRuleExecutionObserver(new TracingRuleObserverOptions
    /// {
    ///     MessageAsEvent = false,
    ///     CodeTagSelector = rule => rule is IComposedBusinessRule ? null : rule.Code
    /// });
    /// await AsyncBusinessRuleChecker.EvaluateAllAsync(context, rules, new AsyncRuleEvaluationOptions { Observer = tracing }, ct);
    /// </code>
    /// </example>
    public sealed class TracingAsyncRuleExecutionObserver : IAsyncRuleExecutionObserver
    {
        private readonly TracingRuleObserverOptions _options;

        /// <summary>
        /// Initializes the observer with default options.
        /// </summary>
        public TracingAsyncRuleExecutionObserver()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes the observer.
        /// </summary>
        /// <param name="options">The options, or null for the defaults.</param>
        public TracingAsyncRuleExecutionObserver(TracingRuleObserverOptions? options)
        {
            _options = options ?? new TracingRuleObserverOptions();
        }

        /// <summary>
        /// Gets the options the observer records with.
        /// </summary>
        public TracingRuleObserverOptions Options => _options;

        /// <summary>
        /// Starts the batch span. Completes synchronously.
        /// </summary>
        /// <param name="ruleCount">The number of rules submitted to the batch.</param>
        /// <param name="cancellationToken">Ignored; the callback never awaits.</param>
        /// <returns>A completed task.</returns>
        public Task OnBatchStartedAsync(int ruleCount, CancellationToken cancellationToken = default)
        {
            RuleActivities.StartBatch(ruleCount);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Starts the rule span and makes it current. Completes synchronously.
        /// </summary>
        /// <param name="rule">The rule about to be evaluated.</param>
        /// <param name="cancellationToken">Ignored; the callback never awaits.</param>
        /// <returns>A completed task.</returns>
        public Task OnBeforeEvaluateAsync(IAsyncBusinessRule rule, CancellationToken cancellationToken = default)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            RuleActivities.StartRule(rule, RuleMetadataSnapshot.Capture(rule), _options.SelectCodeTag(rule));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Adds the broken event with the result message when <see cref="TracingRuleObserverOptions.MessageAsEvent"/> is set.
        /// Completes synchronously.
        /// </summary>
        /// <param name="rule">The evaluated rule.</param>
        /// <param name="result">The result if the rule was broken; null if the rule passed.</param>
        /// <param name="cancellationToken">Ignored; the callback never awaits.</param>
        /// <returns>A completed task.</returns>
        public Task OnAfterEvaluateAsync(IAsyncBusinessRule rule, BusinessRuleResult? result, CancellationToken cancellationToken = default)
        {
            if (result != null && _options.MessageAsEvent)
            {
                RuleActivities.AddBrokenEvent(RuleActivities.CurrentRule(), result.Message);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Writes the outcome, stops the rule span and records the evaluation on the metrics. Completes synchronously.
        /// </summary>
        /// <param name="rule">The evaluated rule.</param>
        /// <param name="isBroken">True when the rule is broken.</param>
        /// <param name="elapsed">The wall-clock time the checker measured.</param>
        /// <param name="cancellationToken">Ignored; the callback never awaits.</param>
        /// <returns>A completed task.</returns>
        public Task OnAfterEvaluateAsync(IAsyncBusinessRule rule, bool isBroken, TimeSpan elapsed, CancellationToken cancellationToken = default)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            string outcome = RuleOutcomeTags.ForVerdict(isBroken);
            RuleActivities.Complete(RuleActivities.CurrentRule(), outcome);
            RecordEvaluation(rule, outcome, elapsed);
            return Task.CompletedTask;
        }

        /// <summary>
        /// No-op: the verdict is recorded by <see cref="OnAfterEvaluateAsync(IAsyncBusinessRule, bool, TimeSpan, CancellationToken)"/>.
        /// </summary>
        /// <param name="rule">The broken rule.</param>
        /// <param name="result">The result containing the rule violation details.</param>
        /// <param name="cancellationToken">Ignored.</param>
        /// <returns>A completed task.</returns>
        public Task OnRuleBrokenAsync(IAsyncBusinessRule rule, BusinessRuleResult result, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Marks the rule span as faulted, stops it and records the evaluation with the <see cref="RuleOutcomeTags.Faulted"/>
        /// outcome. The duration is taken from the span and is therefore recorded only when the span was sampled.
        /// Completes synchronously.
        /// </summary>
        /// <param name="rule">The rule that threw.</param>
        /// <param name="exception">The exception the rule threw.</param>
        /// <param name="cancellationToken">Ignored; the callback never awaits.</param>
        /// <returns>A completed task.</returns>
        public Task OnRuleFaultedAsync(IAsyncBusinessRule rule, Exception exception, CancellationToken cancellationToken = default)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            Activity? activity = RuleActivities.CurrentRule();
            RuleActivities.Fault(activity, exception);
            RecordEvaluation(rule, RuleOutcomeTags.Faulted, activity?.Duration);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Writes the batch counts, stops the batch span and records the batch on the metrics. Completes synchronously.
        /// </summary>
        /// <param name="result">The result of the batch, including its ledger.</param>
        /// <param name="cancellationToken">Ignored; the callback never awaits.</param>
        /// <returns>A completed task.</returns>
        public Task OnBatchCompletedAsync(AsyncRuleEvaluationResult result, CancellationToken cancellationToken = default)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            BatchSummary summary = BatchSummary.From(result);
            RuleActivities.CompleteBatch(RuleActivities.CurrentBatch(), summary);

            if (_options.RecordMetrics)
            {
                _options.Metrics.RecordBatch(summary.Outcome);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Adds the shadow-violation event to the enclosing span and records the violation with the
        /// <see cref="RuleOutcomeTags.Shadow"/> outcome (in addition to the gated rule's own passed measurement).
        /// Completes synchronously.
        /// </summary>
        /// <param name="rule">The gated rule that would have failed.</param>
        /// <param name="result">The result the inner rule produced.</param>
        /// <param name="cancellationToken">Ignored; the callback never awaits.</param>
        /// <returns>A completed task.</returns>
        public Task OnShadowViolationAsync(IAsyncBusinessRule rule, BusinessRuleResult result, CancellationToken cancellationToken = default)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            string? codeTag = _options.SelectCodeTag(rule);
            RuleActivities.AddShadowEvent(RuleActivities.CurrentOwned(), codeTag, _options.MessageAsEvent ? result.Message : null);

            if (_options.RecordMetrics)
            {
                _options.Metrics.RecordEvaluation(RuleMetadataSnapshot.Capture(rule), codeTag, RuleOutcomeTags.Shadow, null);
            }

            return Task.CompletedTask;
        }

        private void RecordEvaluation(IAsyncBusinessRule rule, string outcome, TimeSpan? elapsed)
        {
            if (!_options.RecordMetrics)
            {
                return;
            }

            _options.Metrics.RecordEvaluation(RuleMetadataSnapshot.Capture(rule), _options.SelectCodeTag(rule), outcome, elapsed);
        }
    }
}
