using System;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Severity-related extension methods for asynchronous business rules: re-severity a rule without subclassing it
    /// and check a single rule against a severity threshold.
    /// </summary>
    public static class AsyncBusinessRuleSeverityExtensions
    {
        /// <summary>
        /// Wraps the async rule in a <see cref="SeverityOverrideAsyncBusinessRule"/> that reports <paramref name="severity"/>
        /// instead of the rule's own severity. All other metadata and the verdict are forwarded unchanged.
        /// </summary>
        /// <param name="rule">The async rule to re-severity.</param>
        /// <param name="severity">The severity to report.</param>
        /// <returns>A decorated async rule reporting the given severity.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public static IAsyncBusinessRule WithSeverity(this IAsyncBusinessRule rule, RuleSeverity severity)
        {
            return new SeverityOverrideAsyncBusinessRule(rule, severity);
        }

        /// <summary>
        /// Evaluates the async rule once with the given context and throws only when it is broken with a severity at or
        /// above <paramref name="throwOn"/>. A broken rule below the threshold completes silently.
        /// </summary>
        /// <param name="rule">The async rule to check.</param>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="throwOn">The lowest severity that throws.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> or <paramref name="context"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when the rule is broken at or above the threshold.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task CheckAsync(this IAsyncBusinessRule rule, BusinessRuleContext context, RuleSeverity throwOn, CancellationToken cancellationToken = default)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            BusinessRuleResult? result = await AsyncBusinessRuleWalker.EvaluateNodeAsync(rule, context, null, cancellationToken);

            if (result != null && RuleSeverityThreshold.IsAtLeast(result.Severity, throwOn))
            {
                throw new BusinessRuleValidationException(new[] { result });
            }
        }
    }
}
