namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// One recorded step of an explanation: a binding (<c>amount</c> bound to <c>12000</c>) or an operation
    /// (<c>amount &gt; @maxOrderAmount</c> evaluated to <c>true</c>). Steps appear in evaluation order; a branch skipped
    /// by short-circuiting has no steps. When a redactor was supplied, <see cref="BoundValue"/> and <see cref="Result"/>
    /// of a binding hold the redacted text, never the raw value.
    /// </summary>
    public sealed class RuleExpressionStep
    {
        /// <summary>
        /// Creates a step.
        /// </summary>
        /// <param name="kind">Whether the step is a binding or an operation.</param>
        /// <param name="nodeText">The source text of the node.</param>
        /// <param name="boundValue">The bound value of a binding step; null for an operation.</param>
        /// <param name="result">The value the node evaluated to.</param>
        public RuleExpressionStep(RuleExpressionStepKind kind, string nodeText, object? boundValue, object? result)
        {
            Kind = kind;
            NodeText = nodeText;
            BoundValue = boundValue;
            Result = result;
        }

        /// <summary>
        /// Gets whether the step is a binding or an operation.
        /// </summary>
        public RuleExpressionStepKind Kind { get; }

        /// <summary>
        /// Gets the source text of the node (<c>amount</c>, <c>customer.Tier</c>, <c>amount &gt; @maxOrderAmount</c>).
        /// </summary>
        public string NodeText { get; }

        /// <summary>
        /// Gets the value a binding step bound (possibly redacted); null for an operation step.
        /// </summary>
        public object? BoundValue { get; }

        /// <summary>
        /// Gets the value the node evaluated to: the bound value for a binding, the operator's result for an operation.
        /// </summary>
        public object? Result { get; }

        /// <summary>
        /// Returns <c>text = value</c> for a binding and <c>text => result</c> for an operation.
        /// </summary>
        public override string ToString()
            => Kind == RuleExpressionStepKind.Binding ? $"{NodeText} = {Result ?? "null"}" : $"{NodeText} => {Result ?? "null"}";
    }
}
