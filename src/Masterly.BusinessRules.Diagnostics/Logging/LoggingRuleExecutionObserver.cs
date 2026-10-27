using System;
using Microsoft.Extensions.Logging;

namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// An <see cref="IRuleExecutionObserver"/> that writes one structured log line per rule evaluated by
    /// <see cref="BusinessRuleChecker"/> (fields <c>RuleCode</c>, <c>RuleName</c>, <c>Category</c>, <c>Severity</c>,
    /// <c>Outcome</c>, <c>ElapsedMs</c> and, for a broken rule, <c>Message</c>) at a level chosen by the rule's severity,
    /// plus batch start/completion, fault and shadow-violation lines. Messages are generated with
    /// <see cref="LoggerMessageAttribute"/>, so a disabled level allocates nothing. The observer holds no per-rule state
    /// and can be shared by concurrent batches.
    /// </summary>
    /// <example>
    /// <code>
    /// LoggingRuleExecutionObserver logging = new LoggingRuleExecutionObserver(logger, new LoggingRuleObserverOptions
    /// {
    ///     LogPassedRules = false,
    ///     BrokenLevelBySeverity = { [RuleSeverity.Error] = LogLevel.Warning }
    /// });
    /// BusinessRuleChecker.CheckAll(rules, context, new RuleEvaluationOptions { Observer = logging });
    /// </code>
    /// </example>
    public sealed class LoggingRuleExecutionObserver : IRuleExecutionObserver
    {
        private readonly RuleLogWriter _writer;

        /// <summary>
        /// Initializes the observer with default options.
        /// </summary>
        /// <param name="logger">The logger to write to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is null.</exception>
        public LoggingRuleExecutionObserver(ILogger logger)
            : this(logger, null)
        {
        }

        /// <summary>
        /// Initializes the observer.
        /// </summary>
        /// <param name="logger">The logger to write to.</param>
        /// <param name="options">The options, or null for the defaults.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is null.</exception>
        public LoggingRuleExecutionObserver(ILogger logger, LoggingRuleObserverOptions? options)
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
        /// Logs the batch start when <see cref="LoggingRuleObserverOptions.LogBatches"/> is set.
        /// </summary>
        /// <param name="ruleCount">The number of rules submitted to the batch.</param>
        public void OnBatchStarted(int ruleCount)
        {
            _writer.BatchStarted(ruleCount);
        }

        /// <summary>
        /// No-op: the rule is logged once its outcome and elapsed time are known.
        /// </summary>
        /// <param name="rule">The rule about to be evaluated.</param>
        public void OnBeforeEvaluate(IBusinessRule rule)
        {
        }

        /// <summary>
        /// No-op: the rule is logged by <see cref="OnAfterEvaluate(IBusinessRule, bool, TimeSpan)"/>, which carries the elapsed time.
        /// </summary>
        /// <param name="rule">The evaluated rule.</param>
        /// <param name="result">The result if the rule was broken; null if the rule passed.</param>
        public void OnAfterEvaluate(IBusinessRule rule, BusinessRuleResult? result)
        {
        }

        /// <summary>
        /// Writes the rule's log line: a broken rule at the level mapped from its severity, a passed rule at
        /// <see cref="LoggingRuleObserverOptions.PassedLevel"/> when <see cref="LoggingRuleObserverOptions.LogPassedRules"/> is set.
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

            _writer.Evaluated(rule, isBroken, elapsed);
        }

        /// <summary>
        /// No-op: a broken rule is logged once by <see cref="OnAfterEvaluate(IBusinessRule, bool, TimeSpan)"/>, and a faulted
        /// rule under <see cref="FaultPolicy.TreatAsBroken"/> once by <see cref="OnRuleFaulted"/>.
        /// </summary>
        /// <param name="rule">The broken rule.</param>
        /// <param name="result">The result containing the rule violation details.</param>
        public void OnRuleBroken(IBusinessRule rule, BusinessRuleResult result)
        {
        }

        /// <summary>
        /// Logs the fault with its exception at <see cref="LoggingRuleObserverOptions.FaultedLevel"/>.
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

            _writer.Faulted(rule, exception);
        }

        /// <summary>
        /// Logs the batch completion counts when <see cref="LoggingRuleObserverOptions.LogBatches"/> is set.
        /// </summary>
        /// <param name="result">The result of the batch, including its ledger.</param>
        public void OnBatchCompleted(RuleEvaluationResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            _writer.BatchCompleted(BatchSummary.From(result));
        }

        /// <summary>
        /// Logs the shadow violation at <see cref="LoggingRuleObserverOptions.ShadowViolationLevel"/>.
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

            _writer.ShadowViolation(rule, result);
        }
    }
}
