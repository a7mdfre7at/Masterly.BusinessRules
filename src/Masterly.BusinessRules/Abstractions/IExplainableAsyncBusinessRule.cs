using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Internal contract implemented by every in-library composed and decorated asynchronous rule.
    /// <see cref="EvaluateCoreAsync"/> performs the single real evaluation of the node; when a trace is supplied the node's
    /// <see cref="RuleExplanation"/> is recorded as a by-product, so <c>ExplainAsync</c> never evaluates a rule twice.
    /// </summary>
    internal interface IExplainableAsyncBusinessRule
    {
        /// <summary>
        /// Evaluates this node exactly once.
        /// </summary>
        /// <param name="context">The context to forward to operands.</param>
        /// <param name="trace">The trace to record this node's explanation in, or null on the plain evaluation path.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The broken result (with <see cref="BusinessRuleResult.Details"/> for member failures), or null when the node passed.</returns>
        Task<BusinessRuleResult?> EvaluateCoreAsync(BusinessRuleContext context, RuleExplanationTrace? trace, CancellationToken cancellationToken);
    }
}
