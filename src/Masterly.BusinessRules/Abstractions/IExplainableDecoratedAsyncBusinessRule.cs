using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The public seam that lets an asynchronous decorator written outside the library take part in <c>ExplainAsync</c>
    /// and in the composed-node evaluation path. The walker never treats such a decorator as a leaf: it hands
    /// <see cref="EvaluateDecoratedAsync"/> an <see cref="AsyncDecoratedRuleEvaluation"/> whose
    /// <see cref="AsyncDecoratedRuleEvaluation.EvaluateInnerAsync"/> evaluates <see cref="IDecoratedAsyncBusinessRule.InnerRule"/>
    /// once (recording its explanation subtree when explaining), and records the decorator as a node labelled
    /// <see cref="ExplanationOperator"/> with the inner rule as its child. When the decorator never calls
    /// <c>EvaluateInnerAsync</c>, the child is recorded as not evaluated.
    /// </summary>
    /// <remarks>
    /// <see cref="EvaluateDecoratedAsync"/> is the decorator's verdict on every walker path (composition operands,
    /// groups, <c>ExplainAsync</c>), so its own <c>IsBrokenAsync</c>/<c>EvaluateAsync</c> must agree with it; the
    /// simplest way is to implement them through <c>EvaluateDecoratedAsync(new AsyncDecoratedRuleEvaluation(InnerRule, context), cancellationToken)</c>.
    /// </remarks>
    public interface IExplainableDecoratedAsyncBusinessRule : IDecoratedAsyncBusinessRule
    {
        /// <summary>
        /// Gets the operator label the decorator's explanation node carries (for example <c>COVERAGE</c>).
        /// </summary>
        string ExplanationOperator { get; }

        /// <summary>
        /// Decides the decorator's verdict.
        /// </summary>
        /// <param name="evaluation">The evaluation of the inner rule, run at most once.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The broken result, or null when the decorator passed.</returns>
        Task<BusinessRuleResult?> EvaluateDecoratedAsync(AsyncDecoratedRuleEvaluation evaluation, CancellationToken cancellationToken);
    }
}
