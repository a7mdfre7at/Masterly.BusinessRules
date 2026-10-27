using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// A decorator that records every evaluation of a single async rule outside a checker batch as a
    /// <c>BusinessRule.Evaluate</c> span from <see cref="BusinessRulesActivitySource"/>, a <see cref="BusinessRulesMetrics"/>
    /// measurement and, when a logger is supplied, a structured log line. The span is <see cref="System.Diagnostics.Activity.Current"/>
    /// while the inner rule runs, including after its awaits. All seven metadata members and the verdict are forwarded
    /// unchanged; <see cref="CheckAsync"/> rethrows the inner rule's own <see cref="BusinessRuleValidationException"/>.
    /// Created through the <see cref="DiagnosticsAsyncBusinessRuleExtensions.Traced(IAsyncBusinessRule, TracingRuleObserverOptions?)"/> extensions.
    /// </summary>
    /// <remarks>
    /// Inside a checker batch observed by a <see cref="TracingAsyncRuleExecutionObserver"/> the decorator produces a nested
    /// rule span and a second measurement; use the observer for batches and the decorator for single-rule calls.
    /// Cancellation closes the span with the <see cref="RuleOutcomeTags.Cancelled"/> outcome and is neither counted nor logged.
    /// </remarks>
    /// <example>
    /// <code>
    /// IAsyncBusinessRule traced = new UniqueEmailRule(email, repository).Traced();
    /// await traced.CheckAsync(context, ct);
    /// </code>
    /// </example>
    public sealed class DiagnosticsAsyncBusinessRule : IDecoratedAsyncBusinessRule
    {
        private readonly IAsyncBusinessRule _innerRule;
        private readonly RuleDiagnosticsRecorder _recorder;

        /// <summary>
        /// Initializes a decorator that records spans and metrics.
        /// </summary>
        /// <param name="innerRule">The async rule to record.</param>
        /// <param name="options">The tracing options, or null for the defaults.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> is null.</exception>
        public DiagnosticsAsyncBusinessRule(IAsyncBusinessRule innerRule, TracingRuleObserverOptions? options)
            : this(innerRule, options, null, null)
        {
        }

        /// <summary>
        /// Initializes a decorator that records spans and metrics and writes a log line per evaluation.
        /// </summary>
        /// <param name="innerRule">The async rule to record.</param>
        /// <param name="options">The tracing options, or null for the defaults.</param>
        /// <param name="logger">The logger, or null to record spans and metrics only.</param>
        /// <param name="loggingOptions">The logging options, or null for the defaults; ignored without a logger.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> is null.</exception>
        public DiagnosticsAsyncBusinessRule(IAsyncBusinessRule innerRule, TracingRuleObserverOptions? options, ILogger? logger, LoggingRuleObserverOptions? loggingOptions)
        {
            _innerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
            _recorder = new RuleDiagnosticsRecorder(innerRule, options, logger, loggingOptions);
        }

        /// <inheritdoc />
        public IAsyncBusinessRule InnerRule => _innerRule;

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
        public async Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            RuleEvaluationScope scope = _recorder.Begin();
            bool isBroken;
            try
            {
                isBroken = await _innerRule.IsBrokenAsync(context, cancellationToken).ConfigureAwait(false);
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

        /// <inheritdoc />
        /// <remarks>The inner rule's result is returned unchanged; its message becomes the broken event when <see cref="TracingRuleObserverOptions.MessageAsEvent"/> is set.</remarks>
        public async Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            RuleEvaluationScope scope = _recorder.Begin();
            BusinessRuleResult? result;
            try
            {
                result = await _innerRule.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
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

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown by the inner rule when it is broken; rethrown unchanged.</exception>
        public async Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            RuleEvaluationScope scope = _recorder.Begin();
            try
            {
                await _innerRule.CheckAsync(context, cancellationToken).ConfigureAwait(false);
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
