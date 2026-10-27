using System;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// Smoke-mode mutants: wrap a rule so it misbehaves in a known way and run the test suite against it. A suite that
    /// still passes has not pinned the rule down. This is deliberately a smoke test, not mutation testing; the real
    /// rule bugs live inside <c>IsBroken</c>, which only a source-level mutator such as Stryker.NET reaches.
    /// Every mutant keeps the rule's <c>Code</c>, <c>Message</c> and the rest of its metadata so assertions that look a
    /// rule up by code keep working.
    /// </summary>
    /// <example>
    /// <code>
    /// IBusinessRule mutant = new CustomerIsAdultRule(customer).Invert();
    /// Assert.Throws&lt;BusinessRuleValidationException&gt;(() => BusinessRuleChecker.CheckAll(new[] { mutant }));   // the suite must notice
    /// </code>
    /// </example>
    public static class RuleMutationExtensions
    {
        /// <summary>
        /// Wraps the rule so it is broken exactly when the original passes.
        /// </summary>
        /// <param name="rule">The rule to invert.</param>
        /// <returns>An <see cref="InvertedBusinessRule"/> over the rule.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public static IBusinessRule Invert(this IBusinessRule rule)
        {
            return new InvertedBusinessRule(rule);
        }

        /// <summary>
        /// Wraps the async rule so it is broken exactly when the original passes.
        /// </summary>
        /// <param name="rule">The async rule to invert.</param>
        /// <returns>An <see cref="InvertedAsyncBusinessRule"/> over the rule.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public static IAsyncBusinessRule Invert(this IAsyncBusinessRule rule)
        {
            return new InvertedAsyncBusinessRule(rule);
        }

        /// <summary>
        /// Wraps the rule so it never breaks and never evaluates the original: the rule has silently stopped firing.
        /// </summary>
        /// <param name="rule">The rule to silence.</param>
        /// <returns>An <see cref="AlwaysPassingBusinessRule"/> over the rule.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public static IBusinessRule AlwaysPass(this IBusinessRule rule)
        {
            return new AlwaysPassingBusinessRule(rule);
        }

        /// <summary>
        /// Wraps the async rule so it never breaks and never evaluates the original: the rule has silently stopped firing.
        /// </summary>
        /// <param name="rule">The async rule to silence.</param>
        /// <returns>An <see cref="AlwaysPassingAsyncBusinessRule"/> over the rule.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public static IAsyncBusinessRule AlwaysPass(this IAsyncBusinessRule rule)
        {
            return new AlwaysPassingAsyncBusinessRule(rule);
        }

        /// <summary>
        /// Wraps the rule in a <see cref="SeverityOverrideBusinessRule"/> one level below its own severity
        /// (<c>Error</c> → <c>Warning</c> → <c>Info</c>). An <c>Info</c> rule has nowhere to go and stays <c>Info</c>, so the
        /// mutant is then indistinguishable from the original.
        /// </summary>
        /// <param name="rule">The rule to soften.</param>
        /// <returns>The severity-override decorator over the rule.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public static IBusinessRule DowngradeSeverity(this IBusinessRule rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            return new SeverityOverrideBusinessRule(rule, RuleSeverityDowngrade.Of(rule.Severity));
        }

        /// <summary>
        /// Wraps the async rule in a <see cref="SeverityOverrideAsyncBusinessRule"/> one level below its own severity
        /// (<c>Error</c> → <c>Warning</c> → <c>Info</c>). An <c>Info</c> rule has nowhere to go and stays <c>Info</c>, so the
        /// mutant is then indistinguishable from the original.
        /// </summary>
        /// <param name="rule">The async rule to soften.</param>
        /// <returns>The severity-override decorator over the rule.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public static IAsyncBusinessRule DowngradeSeverity(this IAsyncBusinessRule rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            return new SeverityOverrideAsyncBusinessRule(rule, RuleSeverityDowngrade.Of(rule.Severity));
        }
    }
}
