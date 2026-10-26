using System.Linq.Expressions;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Expression visitor that substitutes one lambda parameter with another expression, so that the bodies of two
    /// predicates over different parameters can be spliced into a single lambda. Splicing bodies (instead of wrapping
    /// the operands in <see cref="InvocationExpression"/> nodes) keeps the combined predicate translatable by every
    /// LINQ provider.
    /// </summary>
    internal sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _source;
        private readonly Expression _target;

        private ParameterReplacer(ParameterExpression source, Expression target)
        {
            _source = source;
            _target = target;
        }

        /// <summary>
        /// Returns <paramref name="body"/> with every reference to <paramref name="source"/> replaced by <paramref name="target"/>.
        /// </summary>
        /// <param name="body">The expression to rewrite.</param>
        /// <param name="source">The parameter to replace.</param>
        /// <param name="target">The expression that takes its place.</param>
        /// <returns>The rewritten expression; the same instance when the parameter does not occur.</returns>
        public static Expression Replace(Expression body, ParameterExpression source, Expression target)
        {
            return new ParameterReplacer(source, target).Visit(body);
        }

        /// <inheritdoc />
        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _source ? _target : base.VisitParameter(node);
        }
    }
}
