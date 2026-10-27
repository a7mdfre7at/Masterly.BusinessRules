using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// Options shared by <see cref="LoggingRuleExecutionObserver"/>, <see cref="LoggingAsyncRuleExecutionObserver"/> and the
    /// logging <c>Traced()</c> overloads. The defaults are conservative: passed rules are not logged, batches are logged at
    /// <see cref="LogLevel.Debug"/>, and a broken <see cref="RuleSeverity.Error"/> rule is a <see cref="LogLevel.Warning"/>
    /// because an expected validation failure is not an application error.
    /// </summary>
    /// <example>
    /// <code>
    /// LoggingRuleObserverOptions options = new LoggingRuleObserverOptions
    /// {
    ///     LogPassedRules = false,
    ///     BrokenLevelBySeverity =
    ///     {
    ///         [RuleSeverity.Info] = LogLevel.Debug,
    ///         [RuleSeverity.Warning] = LogLevel.Information,
    ///         [RuleSeverity.Error] = LogLevel.Warning
    ///     }
    /// };
    /// </code>
    /// </example>
    public sealed class LoggingRuleObserverOptions
    {
        /// <summary>
        /// The value written to the <c>Message</c> field of a broken or shadow-violation line when <see cref="IncludeMessage"/> is false.
        /// </summary>
        public const string RedactedMessage = "[redacted]";

        /// <summary>
        /// Gets or sets whether a rule that passed is logged (at <see cref="PassedLevel"/>). Defaults to false.
        /// </summary>
        public bool LogPassedRules { get; set; }

        /// <summary>
        /// Gets or sets the level a passed rule is logged at when <see cref="LogPassedRules"/> is true. Defaults to <see cref="LogLevel.Debug"/>.
        /// </summary>
        public LogLevel PassedLevel { get; set; } = LogLevel.Debug;

        /// <summary>
        /// Gets the level a broken rule is logged at, by severity. Defaults to <see cref="RuleSeverity.Info"/> →
        /// <see cref="LogLevel.Debug"/>, <see cref="RuleSeverity.Warning"/> → <see cref="LogLevel.Information"/>,
        /// <see cref="RuleSeverity.Error"/> → <see cref="LogLevel.Warning"/>. Map a severity to <see cref="LogLevel.None"/>
        /// to silence it; a severity missing from the map uses <see cref="DefaultBrokenLevel"/>.
        /// </summary>
        public IDictionary<RuleSeverity, LogLevel> BrokenLevelBySeverity { get; } = new Dictionary<RuleSeverity, LogLevel>
        {
            [RuleSeverity.Info] = LogLevel.Debug,
            [RuleSeverity.Warning] = LogLevel.Information,
            [RuleSeverity.Error] = LogLevel.Warning
        };

        /// <summary>
        /// Gets or sets the level used for a broken rule whose severity is not in <see cref="BrokenLevelBySeverity"/>.
        /// Defaults to <see cref="LogLevel.Warning"/>.
        /// </summary>
        public LogLevel DefaultBrokenLevel { get; set; } = LogLevel.Warning;

        /// <summary>
        /// Gets or sets the level a rule that threw is logged at (with the exception). Defaults to <see cref="LogLevel.Error"/>.
        /// </summary>
        public LogLevel FaultedLevel { get; set; } = LogLevel.Error;

        /// <summary>
        /// Gets or sets whether the start and completion of a batch are logged (at <see cref="BatchLevel"/>). Defaults to true.
        /// </summary>
        public bool LogBatches { get; set; } = true;

        /// <summary>
        /// Gets or sets the level batch start and completion are logged at. Defaults to <see cref="LogLevel.Debug"/>.
        /// </summary>
        public LogLevel BatchLevel { get; set; } = LogLevel.Debug;

        /// <summary>
        /// Gets or sets the level a shadow-mode violation is logged at. Defaults to <see cref="LogLevel.Information"/>.
        /// </summary>
        public LogLevel ShadowViolationLevel { get; set; } = LogLevel.Information;

        /// <summary>
        /// Gets or sets whether the rule <c>Message</c> is part of the broken and shadow-violation log lines. Defaults to
        /// true; set it to false when rule messages carry domain data that must not reach the logs, and the <c>Message</c>
        /// field carries <see cref="RedactedMessage"/> instead.
        /// </summary>
        public bool IncludeMessage { get; set; } = true;

        /// <summary>
        /// Returns the level a broken rule of <paramref name="severity"/> is logged at.
        /// </summary>
        /// <param name="severity">The rule severity.</param>
        /// <returns>The mapped level, or <see cref="DefaultBrokenLevel"/> when the severity is not mapped.</returns>
        public LogLevel BrokenLevelFor(RuleSeverity severity)
        {
            return BrokenLevelBySeverity.TryGetValue(severity, out LogLevel level) ? level : DefaultBrokenLevel;
        }
    }
}
