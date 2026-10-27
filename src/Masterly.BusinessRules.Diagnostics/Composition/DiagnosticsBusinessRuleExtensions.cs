using System;
using Microsoft.Extensions.Logging;

namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// The <c>Traced()</c> extensions wrapping a sync rule in a <see cref="DiagnosticsBusinessRule"/>.
    /// </summary>
    public static class DiagnosticsBusinessRuleExtensions
    {
        /// <summary>
        /// Wraps the rule so that every <c>IsBroken</c>/<c>Check</c> call is recorded as a span and a metric measurement.
        /// </summary>
        /// <param name="rule">The rule to record.</param>
        /// <param name="options">The tracing options, or null for the defaults.</param>
        /// <returns>The decorator.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public static DiagnosticsBusinessRule Traced(this IBusinessRule rule, TracingRuleObserverOptions? options = null)
        {
            return new DiagnosticsBusinessRule(rule, options);
        }

        /// <summary>
        /// Wraps the rule so that every <c>IsBroken</c>/<c>Check</c> call is recorded as a span, a metric measurement and a
        /// structured log line.
        /// </summary>
        /// <param name="rule">The rule to record.</param>
        /// <param name="logger">The logger to write to.</param>
        /// <param name="options">The tracing options, or null for the defaults.</param>
        /// <param name="loggingOptions">The logging options, or null for the defaults.</param>
        /// <returns>The decorator.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> or <paramref name="logger"/> is null.</exception>
        public static DiagnosticsBusinessRule Traced(this IBusinessRule rule, ILogger logger, TracingRuleObserverOptions? options = null, LoggingRuleObserverOptions? loggingOptions = null)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            return new DiagnosticsBusinessRule(rule, options, logger, loggingOptions);
        }
    }
}
