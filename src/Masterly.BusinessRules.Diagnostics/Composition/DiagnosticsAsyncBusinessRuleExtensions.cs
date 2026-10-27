using System;
using Microsoft.Extensions.Logging;

namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// The <c>Traced()</c> extensions wrapping an async rule in a <see cref="DiagnosticsAsyncBusinessRule"/>.
    /// </summary>
    public static class DiagnosticsAsyncBusinessRuleExtensions
    {
        /// <summary>
        /// Wraps the async rule so that every <c>IsBrokenAsync</c>/<c>EvaluateAsync</c>/<c>CheckAsync</c> call is recorded as a
        /// span and a metric measurement.
        /// </summary>
        /// <param name="rule">The async rule to record.</param>
        /// <param name="options">The tracing options, or null for the defaults.</param>
        /// <returns>The decorator.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public static DiagnosticsAsyncBusinessRule Traced(this IAsyncBusinessRule rule, TracingRuleObserverOptions? options = null)
        {
            return new DiagnosticsAsyncBusinessRule(rule, options);
        }

        /// <summary>
        /// Wraps the async rule so that every <c>IsBrokenAsync</c>/<c>EvaluateAsync</c>/<c>CheckAsync</c> call is recorded as a
        /// span, a metric measurement and a structured log line.
        /// </summary>
        /// <param name="rule">The async rule to record.</param>
        /// <param name="logger">The logger to write to.</param>
        /// <param name="options">The tracing options, or null for the defaults.</param>
        /// <param name="loggingOptions">The logging options, or null for the defaults.</param>
        /// <returns>The decorator.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> or <paramref name="logger"/> is null.</exception>
        public static DiagnosticsAsyncBusinessRule Traced(this IAsyncBusinessRule rule, ILogger logger, TracingRuleObserverOptions? options = null, LoggingRuleObserverOptions? loggingOptions = null)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            return new DiagnosticsAsyncBusinessRule(rule, options, logger, loggingOptions);
        }
    }
}
