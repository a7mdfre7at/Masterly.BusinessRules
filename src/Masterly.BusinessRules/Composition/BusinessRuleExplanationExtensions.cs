using System;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Produces navigable, printable explanation trees for synchronous rules.
    /// </summary>
    public static class BusinessRuleExplanationExtensions
    {
        /// <summary>
        /// Evaluates the rule <b>exactly once</b> and returns the explanation tree produced by that evaluation.
        /// Composed nodes (<see cref="IComposedBusinessRule"/>) and decorators (<see cref="IDecoratedBusinessRule"/>, the adapters)
        /// expand into child nodes; any other rule, including third-party implementations, is a single leaf.
        /// </summary>
        /// <param name="rule">The rule to explain.</param>
        /// <param name="context">The context forwarded to every rule, or null to evaluate with the parameterless <see cref="IBusinessRule.IsBroken()"/>.</param>
        /// <param name="captureExceptions">
        /// When true, a node that throws is recorded as <see cref="RuleOutcome.Faulted"/> (and treated as broken) instead of
        /// propagating; cancellation is never captured. When false, the exception propagates and no tree is produced.
        /// </param>
        /// <returns>The root explanation node for <paramref name="rule"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        /// <example>
        /// <code>
        /// RuleExplanation why = kycRule.Explain(context);
        /// Console.WriteLine(why.ToText());
        /// // [BROKEN] ALL  KYC.INCOMPLETE  "All KYC checks must pass"  (0.9 ms)
        /// //  ├─ [PASSED] KYC.ID  (0.0 ms)
        /// //  ├─ [BROKEN] KYC.ADDRESS  "Address must be verified"  (0.0 ms)
        /// //  └─ [NOT EVALUATED] KYC.SANCTIONS  reason: short-circuit
        /// </code>
        /// </example>
        public static RuleExplanation Explain(this IBusinessRule rule, BusinessRuleContext? context = null, bool captureExceptions = false)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            RuleExplanationTrace trace = new RuleExplanationTrace(captureExceptions);
            BusinessRuleWalker.EvaluateNode(rule, context, trace);
            return trace.Nodes[0];
        }
    }
}
