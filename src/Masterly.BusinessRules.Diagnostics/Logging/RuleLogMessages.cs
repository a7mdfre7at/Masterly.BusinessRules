using System;
using Microsoft.Extensions.Logging;

namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// The structured log messages of this package, implemented by the <c>Microsoft.Extensions.Logging</c>
    /// <see cref="LoggerMessageAttribute"/> source generator so a disabled level costs one <c>IsEnabled</c> call and no
    /// allocation. The event ids match <see cref="RuleLogEvents"/>.
    /// The generated partial methods carry no access modifier because C# 8 (the language version of a netstandard2.1
    /// project) only allows the classic, implicitly private form; the <c>Log*</c> wrappers are the callable surface.
    /// </summary>
    internal static partial class RuleLogMessages
    {
        private const int RuleBrokenEventId = 1;
        private const int RulePassedEventId = 2;
        private const int RuleFaultedEventId = 3;
        private const int BatchStartedEventId = 4;
        private const int BatchCompletedEventId = 5;
        private const int ShadowViolationEventId = 6;

        /// <summary>
        /// Logs that a rule was evaluated and is broken.
        /// </summary>
        public static void LogRuleBroken(ILogger logger, LogLevel level, string ruleCode, string ruleName, string message, string category, RuleSeverity severity, string outcome, double elapsedMs)
        {
            RuleBroken(logger, level, ruleCode, ruleName, message, category, severity, outcome, elapsedMs);
        }

        /// <summary>
        /// Logs that a rule was evaluated and passed.
        /// </summary>
        public static void LogRulePassed(ILogger logger, LogLevel level, string ruleCode, string ruleName, string category, RuleSeverity severity, string outcome, double elapsedMs)
        {
            RulePassed(logger, level, ruleCode, ruleName, category, severity, outcome, elapsedMs);
        }

        /// <summary>
        /// Logs that a rule threw during evaluation.
        /// </summary>
        public static void LogRuleFaulted(ILogger logger, LogLevel level, Exception exception, string ruleCode, string ruleName, string category, RuleSeverity severity, string outcome)
        {
            RuleFaulted(logger, level, exception, ruleCode, ruleName, category, severity, outcome);
        }

        /// <summary>
        /// Logs that a checker batch started.
        /// </summary>
        public static void LogBatchStarted(ILogger logger, LogLevel level, int ruleCount)
        {
            BatchStarted(logger, level, ruleCount);
        }

        /// <summary>
        /// Logs that a checker batch completed.
        /// </summary>
        public static void LogBatchCompleted(ILogger logger, LogLevel level, int ruleCount, int brokenCount, int faultedCount, int passedCount, string outcome, double elapsedMs)
        {
            BatchCompleted(logger, level, ruleCount, brokenCount, faultedCount, passedCount, outcome, elapsedMs);
        }

        /// <summary>
        /// Logs that a shadow-mode gated rule observed a violation.
        /// </summary>
        public static void LogShadowViolation(ILogger logger, LogLevel level, string ruleCode, string ruleName, string message, string category, RuleSeverity severity, string outcome)
        {
            ShadowViolation(logger, level, ruleCode, ruleName, message, category, severity, outcome);
        }

        [LoggerMessage(EventId = RuleBrokenEventId, EventName = nameof(RuleLogEvents.RuleBroken),
            Message = "Business rule {RuleCode} ({RuleName}) is broken: {Message} [Category={Category} Severity={Severity} Outcome={Outcome} ElapsedMs={ElapsedMs:F3}]")]
        static partial void RuleBroken(ILogger logger, LogLevel level, string ruleCode, string ruleName, string message, string category, RuleSeverity severity, string outcome, double elapsedMs);

        [LoggerMessage(EventId = RulePassedEventId, EventName = nameof(RuleLogEvents.RulePassed),
            Message = "Business rule {RuleCode} ({RuleName}) passed [Category={Category} Severity={Severity} Outcome={Outcome} ElapsedMs={ElapsedMs:F3}]")]
        static partial void RulePassed(ILogger logger, LogLevel level, string ruleCode, string ruleName, string category, RuleSeverity severity, string outcome, double elapsedMs);

        [LoggerMessage(EventId = RuleFaultedEventId, EventName = nameof(RuleLogEvents.RuleFaulted),
            Message = "Business rule {RuleCode} ({RuleName}) threw during evaluation [Category={Category} Severity={Severity} Outcome={Outcome}]")]
        static partial void RuleFaulted(ILogger logger, LogLevel level, Exception exception, string ruleCode, string ruleName, string category, RuleSeverity severity, string outcome);

        [LoggerMessage(EventId = BatchStartedEventId, EventName = nameof(RuleLogEvents.BatchStarted),
            Message = "Evaluating a batch of {RuleCount} business rules")]
        static partial void BatchStarted(ILogger logger, LogLevel level, int ruleCount);

        [LoggerMessage(EventId = BatchCompletedEventId, EventName = nameof(RuleLogEvents.BatchCompleted),
            Message = "Business rule batch completed: {BrokenCount} broken, {FaultedCount} faulted, {PassedCount} passed of {RuleCount} [Outcome={Outcome} ElapsedMs={ElapsedMs:F3}]")]
        static partial void BatchCompleted(ILogger logger, LogLevel level, int ruleCount, int brokenCount, int faultedCount, int passedCount, string outcome, double elapsedMs);

        [LoggerMessage(EventId = ShadowViolationEventId, EventName = nameof(RuleLogEvents.ShadowViolation),
            Message = "Business rule {RuleCode} ({RuleName}) would be broken if enforced: {Message} [Category={Category} Severity={Severity} Outcome={Outcome}]")]
        static partial void ShadowViolation(ILogger logger, LogLevel level, string ruleCode, string ruleName, string message, string category, RuleSeverity severity, string outcome);
    }
}
