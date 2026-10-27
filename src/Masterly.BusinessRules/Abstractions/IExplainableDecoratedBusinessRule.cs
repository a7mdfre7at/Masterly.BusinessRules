namespace Masterly.BusinessRules
{
    /// <summary>
    /// The public seam that lets a synchronous decorator written outside the library take part in <c>Explain</c> and in
    /// the composed-node evaluation path. The walker never treats such a decorator as a leaf: it hands
    /// <see cref="EvaluateDecorated"/> a <see cref="DecoratedRuleEvaluation"/> whose <see cref="DecoratedRuleEvaluation.EvaluateInner"/>
    /// evaluates <see cref="IDecoratedBusinessRule.InnerRule"/> once (recording its explanation subtree when explaining),
    /// and records the decorator as a node labelled <see cref="ExplanationOperator"/> with the inner rule as its child.
    /// When the decorator never calls <c>EvaluateInner</c>, the child is recorded as not evaluated.
    /// </summary>
    /// <remarks>
    /// <see cref="EvaluateDecorated"/> is the decorator's verdict on every walker path (composition operands, groups,
    /// <c>Explain</c>), so its own <c>IsBroken</c> overloads must agree with it; the simplest way is to implement them as
    /// <c>EvaluateDecorated(new DecoratedRuleEvaluation(InnerRule, context)) != null</c>.
    /// </remarks>
    public interface IExplainableDecoratedBusinessRule : IDecoratedBusinessRule
    {
        /// <summary>
        /// Gets the operator label the decorator's explanation node carries (for example <c>COVERAGE</c>).
        /// </summary>
        string ExplanationOperator { get; }

        /// <summary>
        /// Decides the decorator's verdict.
        /// </summary>
        /// <param name="evaluation">The evaluation of the inner rule, run at most once.</param>
        /// <returns>The broken result, or null when the decorator passed.</returns>
        BusinessRuleResult? EvaluateDecorated(DecoratedRuleEvaluation evaluation);
    }
}
