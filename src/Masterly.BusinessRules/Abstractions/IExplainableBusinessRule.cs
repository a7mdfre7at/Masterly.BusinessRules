namespace Masterly.BusinessRules
{
    /// <summary>
    /// Internal contract implemented by every in-library composed and decorated synchronous rule.
    /// <see cref="EvaluateCore"/> performs the single real evaluation of the node; when a trace is supplied the node's
    /// <see cref="RuleExplanation"/> is recorded as a by-product, so <c>Explain</c> never evaluates a rule twice.
    /// </summary>
    internal interface IExplainableBusinessRule
    {
        /// <summary>
        /// Evaluates this node exactly once.
        /// </summary>
        /// <param name="context">The context to forward to operands, or null to use the parameterless <see cref="IBusinessRule.IsBroken()"/> overload.</param>
        /// <param name="trace">The trace to record this node's explanation in, or null on the plain evaluation path.</param>
        /// <returns>The broken result (with <see cref="BusinessRuleResult.Details"/> for member failures), or null when the node passed.</returns>
        BusinessRuleResult? EvaluateCore(BusinessRuleContext? context, RuleExplanationTrace? trace);
    }
}
