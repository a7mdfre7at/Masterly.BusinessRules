using System;
using System.Diagnostics;

namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// An <see cref="IRuleExecutionObserver"/> that turns a <see cref="BusinessRuleChecker"/> batch into one
    /// <c>BusinessRules.Batch</c> span with a <c>BusinessRule.Evaluate</c> child span per rule (attributes
    /// <c>business_rule.code</c>, <c>.name</c>, <c>.category</c>, <c>.severity</c>, <c>.outcome</c>) and records the
    /// <see cref="BusinessRulesMetrics"/> instruments. The rule span is <see cref="Activity.Current"/> while the rule
    /// body runs, so any activity a rule starts itself becomes a child of its rule span.
    /// The observer holds no per-rule state and can be shared by concurrent batches.
    /// </summary>
    /// <remarks>
    /// Under <see cref="FaultPolicy.Throw"/> the checker lets the fault propagate without raising
    /// <see cref="IRuleExecutionObserver.OnBatchCompleted"/>; the rule span is still finished from
    /// <see cref="OnRuleFaulted"/>, but the batch span stays open and current on the calling thread. Prefer
    /// <see cref="FaultPolicy.Record"/> or <see cref="FaultPolicy.TreatAsBroken"/> for complete traces.
    /// </remarks>
    /// <example>
    /// <code>
    /// TracingRuleExecutionObserver tracing = new TracingRuleExecutionObserver();
    /// BusinessRuleChecker.CheckAll(rules, context, new RuleEvaluationOptions { Observer = tracing });
    /// </code>
    /// </example>
    public sealed class TracingRuleExecutionObserver : IRuleExecutionObserver
    {
        private readonly TracingRuleObserverOptions _options;

        /// <summary>
        /// Initializes the observer with default options.
        /// </summary>
        public TracingRuleExecutionObserver()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes the observer.
        /// </summary>
        /// <param name="options">The options, or null for the defaults.</param>
        public TracingRuleExecutionObserver(TracingRuleObserverOptions? options)
        {
            _options = options ?? new TracingRuleObserverOptions();
        }

        /// <summary>
        /// Gets the options the observer records with.
        /// </summary>
        public TracingRuleObserverOptions Options => _options;

        /// <summary>
        /// Starts the batch span.
        /// </summary>
        /// <param name="ruleCount">The number of rules submitted to the batch.</param>
        public void OnBatchStarted(int ruleCount)
        {
            RuleActivities.StartBatch(ruleCount);
        }

        /// <summary>
        /// Starts the rule span and makes it current.
        /// </summary>
        /// <param name="rule">The rule about to be evaluated.</param>
        public void OnBeforeEvaluate(IBusinessRule rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            RuleActivities.StartRule(rule, RuleMetadataSnapshot.Capture(rule), _options.SelectCodeTag(rule));
        }

        /// <summary>
        /// Adds the broken event with the result message when <see cref="TracingRuleObserverOptions.MessageAsEvent"/> is set.
        /// </summary>
        /// <param name="rule">The evaluated rule.</param>
        /// <param name="result">The result if the rule was broken; null if the rule passed.</param>
        public void OnAfterEvaluate(IBusinessRule rule, BusinessRuleResult? result)
        {
            if (result != null && _options.MessageAsEvent)
            {
                RuleActivities.AddBrokenEvent(RuleActivities.CurrentRule(), result.Message);
            }
        }

        /// <summary>
        /// Writes the outcome, stops the rule span and records the evaluation on the metrics.
        /// </summary>
        /// <param name="rule">The evaluated rule.</param>
        /// <param name="isBroken">True when the rule is broken.</param>
        /// <param name="elapsed">The wall-clock time the checker measured.</param>
        public void OnAfterEvaluate(IBusinessRule rule, bool isBroken, TimeSpan elapsed)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            string outcome = RuleOutcomeTags.ForVerdict(isBroken);
            RuleActivities.Complete(RuleActivities.CurrentRule(), outcome);
            RecordEvaluation(rule, outcome, elapsed);
        }

        /// <summary>
        /// No-op: the verdict is recorded by <see cref="OnAfterEvaluate(IBusinessRule, bool, TimeSpan)"/>.
        /// </summary>
        /// <param name="rule">The broken rule.</param>
        /// <param name="result">The result containing the rule violation details.</param>
        public void OnRuleBroken(IBusinessRule rule, BusinessRuleResult result)
        {
        }

        /// <summary>
        /// Marks the rule span as faulted, stops it and records the evaluation with the <see cref="RuleOutcomeTags.Faulted"/>
        /// outcome. The duration is taken from the span and is therefore recorded only when the span was sampled.
        /// </summary>
        /// <param name="rule">The rule that threw.</param>
        /// <param name="exception">The exception the rule threw.</param>
        public void OnRuleFaulted(IBusinessRule rule, Exception exception)
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
        }

        /// <summary>
        /// Writes the batch counts, stops the batch span and records the batch on the metrics.
        /// </summary>
        /// <param name="result">The result of the batch, including its ledger.</param>
        public void OnBatchCompleted(RuleEvaluationResult result)
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
        }

        /// <summary>
        /// Adds the shadow-violation event to the enclosing span and records the violation with the
        /// <see cref="RuleOutcomeTags.Shadow"/> outcome (in addition to the gated rule's own passed measurement).
        /// </summary>
        /// <param name="rule">The gated rule that would have failed.</param>
        /// <param name="result">The result the inner rule produced.</param>
        public void OnShadowViolation(IBusinessRule rule, BusinessRuleResult result)
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
        }

        private void RecordEvaluation(IBusinessRule rule, string outcome, TimeSpan? elapsed)
        {
            if (!_options.RecordMetrics)
            {
                return;
            }

            _options.Metrics.RecordEvaluation(RuleMetadataSnapshot.Capture(rule), _options.SelectCodeTag(rule), outcome, elapsed);
        }
    }
}
