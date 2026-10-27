using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// A decorator that records every evaluation of a single sync rule outside a checker batch (the <c>rule.Check()</c>
    /// path a DDD aggregate takes) as a <c>BusinessRule.Evaluate</c> span from <see cref="BusinessRulesActivitySource"/>,
    /// a <see cref="BusinessRulesMetrics"/> measurement and, when a logger is supplied, a structured log line. The span is
    /// <see cref="System.Diagnostics.Activity.Current"/> while the inner rule runs. All seven metadata members and the
    /// verdict are forwarded unchanged; <see cref="Check()"/> rethrows the inner rule's own <see cref="BusinessRuleValidationException"/>.
    /// <see cref="Evaluate()"/> stays the pure result factory it is on every sync rule and records nothing.
    /// Created through the <see cref="DiagnosticsBusinessRuleExtensions.Traced(IBusinessRule, TracingRuleObserverOptions?)"/> extensions.
    /// </summary>
    /// <remarks>
    /// The decorator implements <see cref="IExplainableDecoratedBusinessRule"/>, so on every walker path (checker batches,
    /// composition operands, groups, <c>Explain</c>) the inner rule is evaluated through the walker: a composed inner rule
    /// keeps its member <see cref="BusinessRuleResult.Details"/> and its explanation subtree appears under a
    /// <see cref="DiagnosticsText.TracedOperator"/> node, while the span, metric and log line are recorded around it.
    /// Inside a checker batch observed by a <see cref="TracingRuleExecutionObserver"/> the decorator produces a nested
    /// rule span and a second measurement; use the observer for batches and the decorator for single-rule calls.
    /// Cancellation thrown by the inner rule closes the span with the <see cref="RuleOutcomeTags.Cancelled"/> outcome and
    /// is neither counted nor logged.
    /// </remarks>
    /// <example>
    /// <code>
    /// IBusinessRule traced = new OrderMustHaveItemsRule(order).Traced();
    /// traced.Check();   // one span, one evaluations increment, one duration sample
    /// </code>
    /// </example>
    public sealed class DiagnosticsBusinessRule : IExplainableDecoratedBusinessRule
    {
        private readonly IBusinessRule _innerRule;
        private readonly RuleDiagnosticsRecorder _recorder;

        /// <summary>
        /// Initializes a decorator that records spans and metrics.
        /// </summary>
        /// <param name="innerRule">The rule to record.</param>
        /// <param name="options">The tracing options, or null for the defaults.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> is null.</exception>
        public DiagnosticsBusinessRule(IBusinessRule innerRule, TracingRuleObserverOptions? options)
            : this(innerRule, options, null, null)
        {
        }

        /// <summary>
        /// Initializes a decorator that records spans and metrics and writes a log line per evaluation.
        /// </summary>
        /// <param name="innerRule">The rule to record.</param>
        /// <param name="options">The tracing options, or null for the defaults.</param>
        /// <param name="logger">The logger, or null to record spans and metrics only.</param>
        /// <param name="loggingOptions">The logging options, or null for the defaults; ignored without a logger.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> is null.</exception>
        public DiagnosticsBusinessRule(IBusinessRule innerRule, TracingRuleObserverOptions? options, ILogger? logger, LoggingRuleObserverOptions? loggingOptions)
        {
            _innerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
            _recorder = new RuleDiagnosticsRecorder(innerRule, options, logger, loggingOptions);
        }

        /// <inheritdoc />
        public IBusinessRule InnerRule => _innerRule;

        /// <inheritdoc />
        public string ExplanationOperator => DiagnosticsText.TracedOperator;

        /// <summary>
        /// Gets the tracing options the decorator records with.
        /// </summary>
        public TracingRuleObserverOptions Options => _recorder.Options;

        /// <summary>
        /// Gets whether the decorator writes a log line per evaluation.
        /// </summary>
        public bool IsLogging => _recorder.IsLogging;

        /// <inheritdoc />
        public string Code => _innerRule.Code;

        /// <inheritdoc />
        public string Message => _innerRule.Message;

        /// <inheritdoc />
        public RuleSeverity Severity => _innerRule.Severity;

        /// <inheritdoc />
        public string Name => _innerRule.Name;

        /// <inheritdoc />
        public string Description => _innerRule.Description;

        /// <inheritdoc />
        public string Category => _innerRule.Category;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => _innerRule.Tags;

        /// <inheritdoc />
        public bool IsBroken()
        {
            return Record(() => _innerRule.IsBroken());
        }

        /// <inheritdoc />
        public bool IsBroken(BusinessRuleContext context)
        {
            return Record(() => _innerRule.IsBroken(context));
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown by the inner rule when it is broken; rethrown unchanged.</exception>
        public void Check()
        {
            RecordCheck(() => _innerRule.Check());
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown by the inner rule when it is broken; rethrown unchanged.</exception>
        public void Check(BusinessRuleContext context)
        {
            RecordCheck(() => _innerRule.Check(context));
        }

        /// <inheritdoc />
        /// <remarks>A pure factory forwarded to the inner rule; nothing is recorded.</remarks>
        public BusinessRuleResult Evaluate()
        {
            return _innerRule.Evaluate();
        }

        /// <inheritdoc />
        /// <remarks>A pure factory forwarded to the inner rule; nothing is recorded.</remarks>
        public BusinessRuleResult Evaluate(BusinessRuleContext context)
        {
            return _innerRule.Evaluate(context);
        }

        /// <summary>
        /// Evaluates the inner rule once through <paramref name="evaluation"/> (the walker path), recording the span,
        /// metric and log line around it, and forwards the inner result with its member details.
        /// </summary>
        /// <param name="evaluation">The one-shot evaluation of the inner rule supplied by the core walker.</param>
        /// <returns>The inner rule's broken result, or null when it passed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="evaluation"/> is null.</exception>
        public BusinessRuleResult? EvaluateDecorated(DecoratedRuleEvaluation evaluation)
        {
            if (evaluation == null)
            {
                throw new ArgumentNullException(nameof(evaluation));
            }

            RuleEvaluationScope scope = _recorder.Begin();
            BusinessRuleResult? result;
            try
            {
                result = evaluation.EvaluateInner();
            }
            catch (OperationCanceledException)
            {
                _recorder.Cancelled(scope);
                throw;
            }
            catch (Exception exception)
            {
                _recorder.Faulted(scope, exception);
                throw;
            }

            _recorder.Completed(scope, result != null, result != null ? new[] { result.Message } : null);
            return result;
        }

        private bool Record(Func<bool> evaluate)
        {
            RuleEvaluationScope scope = _recorder.Begin();
            bool isBroken;
            try
            {
                isBroken = evaluate();
            }
            catch (OperationCanceledException)
            {
                _recorder.Cancelled(scope);
                throw;
            }
            catch (Exception exception)
            {
                _recorder.Faulted(scope, exception);
                throw;
            }

            _recorder.Completed(scope, isBroken, isBroken ? _recorder.RuleMessage() : null);
            return isBroken;
        }

        private void RecordCheck(Action check)
        {
            RuleEvaluationScope scope = _recorder.Begin();
            try
            {
                check();
            }
            catch (BusinessRuleValidationException broken)
            {
                _recorder.Completed(scope, true, RuleDiagnosticsRecorder.MessagesOf(broken));
                throw;
            }
            catch (OperationCanceledException)
            {
                _recorder.Cancelled(scope);
                throw;
            }
            catch (Exception exception)
            {
                _recorder.Faulted(scope, exception);
                throw;
            }

            _recorder.Completed(scope, false, null);
        }
    }
}
