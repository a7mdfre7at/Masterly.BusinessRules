using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// Records one single-rule evaluation (span, metrics and optional log line) for the <c>Traced()</c> decorators,
    /// which measure the evaluation themselves because no checker does it for them.
    /// </summary>
    internal sealed class RuleDiagnosticsRecorder
    {
        private readonly IBusinessRuleMetadata _rule;
        private readonly TracingRuleObserverOptions _options;
        private readonly RuleLogWriter? _logWriter;

        /// <summary>
        /// Initializes the recorder for <paramref name="rule"/>.
        /// </summary>
        /// <param name="rule">The inner rule whose evaluations are recorded; its metadata is read per evaluation.</param>
        /// <param name="options">The tracing options.</param>
        /// <param name="logger">The logger to write a line per evaluation to, or null for no logging.</param>
        /// <param name="loggingOptions">The logging options; ignored without a logger, defaults when null.</param>
        public RuleDiagnosticsRecorder(IBusinessRuleMetadata rule, TracingRuleObserverOptions? options, ILogger? logger, LoggingRuleObserverOptions? loggingOptions)
        {
            _rule = rule ?? throw new ArgumentNullException(nameof(rule));
            _options = options ?? new TracingRuleObserverOptions();
            _logWriter = logger == null ? null : new RuleLogWriter(logger, loggingOptions ?? new LoggingRuleObserverOptions());
        }

        /// <summary>
        /// Gets the tracing options.
        /// </summary>
        public TracingRuleObserverOptions Options => _options;

        /// <summary>
        /// Gets whether a logger was supplied.
        /// </summary>
        public bool IsLogging => _logWriter != null;

        /// <summary>
        /// Starts the rule span (made current for the rule body) and the timer.
        /// </summary>
        public RuleEvaluationScope Begin()
        {
            RuleMetadataSnapshot metadata = RuleMetadataSnapshot.Capture(_rule);
            string? codeTag = _options.SelectCodeTag(_rule);
            Activity? activity = RuleActivities.StartRule(_rule, metadata, codeTag);
            return new RuleEvaluationScope(activity, DiagnosticsTiming.Start(), metadata, codeTag);
        }

        /// <summary>
        /// Finishes the scope with a verdict. <paramref name="brokenMessages"/> are added as broken events when
        /// <see cref="TracingRuleObserverOptions.MessageAsEvent"/> is set.
        /// </summary>
        public void Completed(in RuleEvaluationScope scope, bool isBroken, IEnumerable<string>? brokenMessages)
        {
            TimeSpan elapsed = DiagnosticsTiming.ElapsedSince(scope.Start);

            if (isBroken && _options.MessageAsEvent && brokenMessages != null)
            {
                foreach (string message in brokenMessages)
                {
                    RuleActivities.AddBrokenEvent(scope.Activity, message);
                }
            }

            string outcome = RuleOutcomeTags.ForVerdict(isBroken);
            RuleActivities.Complete(scope.Activity, outcome);

            if (_options.RecordMetrics)
            {
                _options.Metrics.RecordEvaluation(scope.Metadata, scope.CodeTag, outcome, elapsed);
            }

            _logWriter?.Evaluated(_rule, isBroken, elapsed);
        }

        /// <summary>
        /// Finishes the scope as faulted.
        /// </summary>
        public void Faulted(in RuleEvaluationScope scope, Exception exception)
        {
            TimeSpan elapsed = DiagnosticsTiming.ElapsedSince(scope.Start);
            RuleActivities.Fault(scope.Activity, exception);

            if (_options.RecordMetrics)
            {
                _options.Metrics.RecordEvaluation(scope.Metadata, scope.CodeTag, RuleOutcomeTags.Faulted, elapsed);
            }

            _logWriter?.Faulted(_rule, exception);
        }

        /// <summary>
        /// Finishes the scope as cancelled: the span is closed with the <see cref="RuleOutcomeTags.Cancelled"/> outcome,
        /// nothing is recorded on the metrics and nothing is logged.
        /// </summary>
        public void Cancelled(in RuleEvaluationScope scope)
        {
            RuleActivities.Complete(scope.Activity, RuleOutcomeTags.Cancelled);
        }

        /// <summary>
        /// Returns the message the inner rule would report, for a verdict obtained without a result.
        /// </summary>
        public IEnumerable<string> RuleMessage()
        {
            return new[] { RuleMetadataSnapshot.ReadMessage(_rule) };
        }

        /// <summary>
        /// Returns the messages of the results a <see cref="BusinessRuleValidationException"/> carries.
        /// </summary>
        public static IEnumerable<string> MessagesOf(BusinessRuleValidationException exception)
        {
            List<string> messages = new List<string>(exception.BrokenRules.Count);
            foreach (BusinessRuleResult result in exception.BrokenRules)
            {
                messages.Add(result.Message);
            }

            return messages;
        }
    }

    /// <summary>
    /// The state of one evaluation between <see cref="RuleDiagnosticsRecorder.Begin"/> and its completion.
    /// </summary>
    internal readonly struct RuleEvaluationScope
    {
        /// <summary>
        /// Initializes the scope.
        /// </summary>
        public RuleEvaluationScope(Activity? activity, long start, RuleMetadataSnapshot metadata, string? codeTag)
        {
            Activity = activity;
            Start = start;
            Metadata = metadata;
            CodeTag = codeTag;
        }

        /// <summary>
        /// Gets the rule span, or null when nobody listens.
        /// </summary>
        public Activity? Activity { get; }

        /// <summary>
        /// Gets the timestamp the evaluation started at.
        /// </summary>
        public long Start { get; }

        /// <summary>
        /// Gets the rule identity captured when the evaluation started.
        /// </summary>
        public RuleMetadataSnapshot Metadata { get; }

        /// <summary>
        /// Gets the selected code tag, or null.
        /// </summary>
        public string? CodeTag { get; }
    }
}
