using System;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Severity-related extension methods for synchronous business rules: re-severity a rule without subclassing it
    /// and check a single rule against a severity threshold.
    /// </summary>
    public static class BusinessRuleSeverityExtensions
    {
        /// <summary>
        /// Wraps the rule in a <see cref="SeverityOverrideBusinessRule"/> that reports <paramref name="severity"/>
        /// instead of the rule's own severity. All other metadata and the verdict are forwarded unchanged.
        /// </summary>
        /// <param name="rule">The rule to re-severity.</param>
        /// <param name="severity">The severity to report.</param>
        /// <returns>A decorated rule reporting the given severity.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public static IBusinessRule WithSeverity(this IBusinessRule rule, RuleSeverity severity)
        {
            return new SeverityOverrideBusinessRule(rule, severity);
        }

        /// <summary>
        /// Evaluates the rule once and throws only when it is broken with a severity at or above <paramref name="throwOn"/>.
        /// A broken rule below the threshold returns silently.
        /// </summary>
        /// <param name="rule">The rule to check.</param>
        /// <param name="throwOn">The lowest severity that throws.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when the rule is broken at or above the threshold.</exception>
        public static void Check(this IBusinessRule rule, RuleSeverity throwOn)
        {
            CheckCore(rule, null, throwOn);
        }

        /// <summary>
        /// Evaluates the rule once with the given context and throws only when it is broken with a severity at or above
        /// <paramref name="throwOn"/>. A broken rule below the threshold returns silently.
        /// </summary>
        /// <param name="rule">The rule to check.</param>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="throwOn">The lowest severity that throws.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> or <paramref name="context"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when the rule is broken at or above the threshold.</exception>
        public static void Check(this IBusinessRule rule, BusinessRuleContext context, RuleSeverity throwOn)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            CheckCore(rule, context, throwOn);
        }

        private static void CheckCore(IBusinessRule rule, BusinessRuleContext? context, RuleSeverity throwOn)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            BusinessRuleResult? result = BusinessRuleWalker.EvaluateNode(rule, context, null);

            if (result != null && RuleSeverityThreshold.IsAtLeast(result.Severity, throwOn))
            {
                throw new BusinessRuleValidationException(new[] { result });
            }
        }
    }
}
