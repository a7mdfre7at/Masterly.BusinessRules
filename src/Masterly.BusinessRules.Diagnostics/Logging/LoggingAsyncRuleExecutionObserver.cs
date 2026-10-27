using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// An <see cref="IAsyncRuleExecutionObserver"/> that writes one structured log line per rule evaluated by
    /// <see cref="AsyncBusinessRuleChecker"/> (fields <c>RuleCode</c>, <c>RuleName</c>, <c>Category</c>, <c>Severity</c>,
    /// <c>Outcome</c>, <c>ElapsedMs</c> and, for a broken rule, <c>Message</c>) at a level chosen by the rule's severity,
    /// plus batch start/completion, fault and shadow-violation lines. Every callback completes synchronously; in a
    /// parallel batch the lines of different rules interleave. The observer holds no per-rule state and can be shared
    /// by concurrent batches.
    /// </summary>
    /// <example>
    /// <code>
    /// LoggingAsyncRuleExecutionObserver logging = new LoggingAsyncRuleExecutionObserver(logger);
    /// await AsyncBusinessRuleChecker.CheckAllAsync(context, rules, new AsyncRuleEvaluationOptions { Observer = logging }, ct);
    /// </code>
    /// </example>
    public sealed class LoggingAsyncRuleExecutionObserver : IAsyncRuleExecutionObserver
    {
        private readonly RuleLogWriter _writer;

        /// <summary>
        /// Initializes the observer with default options.
        /// </summary>
        /// <param name="logger">The logger to write to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is null.</exception>
        public LoggingAsyncRuleExecutionObserver(ILogger logger)
            : this(logger, null)
        {
        }

        /// <summary>
        /// Initializes the observer.
        /// </summary>
        /// <param name="logger">The logger to write to.</param>
        /// <param name="options">The options, or null for the defaults.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is null.</exception>
        public LoggingAsyncRuleExecutionObserver(ILogger logger, LoggingRuleObserverOptions? options)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _writer = new RuleLogWriter(logger, options ?? new LoggingRuleObserverOptions());
        }

        /// <summary>
        /// Gets the options the observer logs with.
        /// </summary>
        public LoggingRuleObserverOptions Options => _writer.Options;

        /// <summary>
        /// Logs the batch start when <see cref="LoggingRuleObserverOptions.LogBatches"/> is set. Completes synchronously.
        /// </summary>
        /// <param name="ruleCount">The number of rules submitted to the batch.</param>
        /// <param name="cancellationToken">Ignored; the callback never awaits.</param>
        /// <returns>A completed task.</returns>
        public Task OnBatchStartedAsync(int ruleCount, CancellationToken cancellationToken = default)
        {
            _writer.BatchStarted(ruleCount);
            return Task.CompletedTask;
        }

        /// <summary>
        /// No-op: the rule is logged once its outcome and elapsed time are known.
        /// </summary>
        /// <param name="rule">The rule about to be evaluated.</param>
        /// <param name="cancellationToken">Ignored.</param>
        /// <returns>A completed task.</returns>
        public Task OnBeforeEvaluateAsync(IAsyncBusinessRule rule, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// No-op: the rule is logged by <see cref="OnAfterEvaluateAsync(IAsyncBusinessRule, bool, TimeSpan, CancellationToken)"/>.
        /// </summary>
        /// <param name="rule">The evaluated rule.</param>
        /// <param name="result">The result if the rule was broken; null if the rule passed.</param>
        /// <param name="cancellationToken">Ignored.</param>
        /// <returns>A completed task.</returns>
        public Task OnAfterEvaluateAsync(IAsyncBusinessRule rule, BusinessRuleResult? result, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Writes the rule's log line: a broken rule at the level mapped from its severity, a passed rule at
        /// <see cref="LoggingRuleObserverOptions.PassedLevel"/> when <see cref="LoggingRuleObserverOptions.LogPassedRules"/> is set.
        /// Completes synchronously.
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

            _writer.Evaluated(rule, isBroken, elapsed);
            return Task.CompletedTask;
        }

        /// <summary>
        /// No-op: a broken rule is logged once by <see cref="OnAfterEvaluateAsync(IAsyncBusinessRule, bool, TimeSpan, CancellationToken)"/>,
        /// and a faulted rule under <see cref="FaultPolicy.TreatAsBroken"/> once by <see cref="OnRuleFaultedAsync"/>.
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
        /// Logs the fault with its exception at <see cref="LoggingRuleObserverOptions.FaultedLevel"/>. Completes synchronously.
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

            _writer.Faulted(rule, exception);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Logs the batch completion counts when <see cref="LoggingRuleObserverOptions.LogBatches"/> is set. Completes synchronously.
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

            _writer.BatchCompleted(BatchSummary.From(result));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Logs the shadow violation at <see cref="LoggingRuleObserverOptions.ShadowViolationLevel"/>. Completes synchronously.
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

            _writer.ShadowViolation(rule, result);
            return Task.CompletedTask;
        }
    }
}
