using System.Collections.Generic;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// Why an expression evaluated the way it did: the verdict, the ordered <see cref="Steps"/> (every binding and
    /// operation of the one evaluation walk) and a <see cref="Rendered"/> line showing the expression with its bound
    /// values inline, for example <c>amount(12000) &gt; @maxOrderAmount(10000) =&gt; true</c>. Produced by
    /// <see cref="RuleExpression.Explain"/>; the verdict is the same one <see cref="RuleExpression.Evaluate"/> returns
    /// because both come from the same interpreter walk.
    /// </summary>
    public sealed class RuleExpressionExplanation
    {
        internal RuleExpressionExplanation(RuleExpression expression, bool result, IReadOnlyList<RuleExpressionStep> steps, string rendered)
        {
            Expression = expression;
            Result = result;
            Steps = steps;
            Rendered = rendered;
        }

        /// <summary>
        /// Gets the explained expression.
        /// </summary>
        public RuleExpression Expression { get; }

        /// <summary>
        /// Gets the verdict: true when the expression (the broken condition) holds.
        /// </summary>
        public bool Result { get; }

        /// <summary>
        /// Gets the bindings and operations in evaluation order. A branch skipped by <c>&amp;&amp;</c>/<c>||</c>
        /// short-circuiting contributes no steps.
        /// </summary>
        public IReadOnlyList<RuleExpressionStep> Steps { get; }

        /// <summary>
        /// Gets the expression with bound values inline: each path is followed by its value in parentheses, each nested
        /// operation by its result in brackets (<c>[true]</c>, <c>[not evaluated]</c>) and the whole line by
        /// <c>=&gt; result</c>. Values are formatted with <see cref="RuleExpressionOptions.Culture"/> unless a redactor
        /// supplied their text.
        /// </summary>
        public string Rendered { get; }

        /// <summary>
        /// Returns <see cref="Rendered"/>.
        /// </summary>
        public override string ToString() => Rendered;
    }
}
