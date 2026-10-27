using System;
using Microsoft.Extensions.Logging;

namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// Applies <see cref="LoggingRuleObserverOptions"/> to the generated <see cref="RuleLogMessages"/>: picks the level,
    /// honours <see cref="LogLevel.None"/> and the include-message switch. Shared by both logging observers and the
    /// logging <c>Traced()</c> decorators.
    /// </summary>
    internal sealed class RuleLogWriter
    {
        private readonly ILogger _logger;
        private readonly LoggingRuleObserverOptions _options;

        /// <summary>
        /// Initializes the writer.
        /// </summary>
        public RuleLogWriter(ILogger logger, LoggingRuleObserverOptions options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Gets the options the writer applies.
        /// </summary>
        public LoggingRuleObserverOptions Options => _options;

        /// <summary>
        /// Logs an evaluated rule: broken at the severity-mapped level, passed at <see cref="LoggingRuleObserverOptions.PassedLevel"/>
        /// when enabled.
        /// </summary>
        public void Evaluated(IBusinessRuleMetadata rule, bool isBroken, TimeSpan elapsed)
        {
            RuleMetadataSnapshot metadata = RuleMetadataSnapshot.Capture(rule);
            string outcome = RuleOutcomeTags.ForVerdict(isBroken);

            if (isBroken)
            {
                LogLevel level = _options.BrokenLevelFor(metadata.Severity);
                if (!ShouldLog(level))
                {
                    return;
                }

                string message = _options.IncludeMessage ? RuleMetadataSnapshot.ReadMessage(rule) : LoggingRuleObserverOptions.RedactedMessage;
                RuleLogMessages.LogRuleBroken(_logger, level, metadata.Code, metadata.Name, message, metadata.Category, metadata.Severity, outcome, elapsed.TotalMilliseconds);
                return;
            }

            if (_options.LogPassedRules && ShouldLog(_options.PassedLevel))
            {
                RuleLogMessages.LogRulePassed(_logger, _options.PassedLevel, metadata.Code, metadata.Name, metadata.Category, metadata.Severity, outcome, elapsed.TotalMilliseconds);
            }
        }

        /// <summary>
        /// Logs a rule that threw, with the exception.
        /// </summary>
        public void Faulted(IBusinessRuleMetadata rule, Exception exception)
        {
            if (!ShouldLog(_options.FaultedLevel))
            {
                return;
            }

            RuleMetadataSnapshot metadata = RuleMetadataSnapshot.Capture(rule);
            RuleLogMessages.LogRuleFaulted(_logger, _options.FaultedLevel, exception, metadata.Code, metadata.Name, metadata.Category, metadata.Severity, RuleOutcomeTags.Faulted);
        }

        /// <summary>
        /// Logs the start of a batch when batch logging is enabled.
        /// </summary>
        public void BatchStarted(int ruleCount)
        {
            if (_options.LogBatches && ShouldLog(_options.BatchLevel))
            {
                RuleLogMessages.LogBatchStarted(_logger, _options.BatchLevel, ruleCount);
            }
        }

        /// <summary>
        /// Logs the completion of a batch when batch logging is enabled.
        /// </summary>
        public void BatchCompleted(BatchSummary summary)
        {
            if (_options.LogBatches && ShouldLog(_options.BatchLevel))
            {
                RuleLogMessages.LogBatchCompleted(_logger, _options.BatchLevel, summary.RuleCount, summary.BrokenCount, summary.FaultedCount, summary.PassedCount, summary.Outcome, summary.TotalDuration.TotalMilliseconds);
            }
        }

        /// <summary>
        /// Logs a shadow-mode violation.
        /// </summary>
        public void ShadowViolation(IBusinessRuleMetadata rule, BusinessRuleResult result)
        {
            LogLevel level = _options.ShadowViolationLevel;
            if (!ShouldLog(level))
            {
                return;
            }

            RuleMetadataSnapshot metadata = RuleMetadataSnapshot.Capture(rule);
            string message = _options.IncludeMessage ? result.Message : LoggingRuleObserverOptions.RedactedMessage;
            RuleLogMessages.LogShadowViolation(_logger, level, metadata.Code, metadata.Name, message, metadata.Category, metadata.Severity, RuleOutcomeTags.Shadow);
        }

        private bool ShouldLog(LogLevel level)
        {
            return level != LogLevel.None && _logger.IsEnabled(level);
        }
    }
}
