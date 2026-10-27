using System.Collections.Generic;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// Membership test <c>value in [a, b, c]</c> (a list of expressions) or <c>value in collection</c> (any expression
    /// yielding an enumerable), using the equality rules of <c>==</c>.
    /// </summary>
    internal sealed class InNode : RuleExpressionNode
    {
        public InNode(RuleExpressionNode value, IReadOnlyList<RuleExpressionNode>? items, RuleExpressionNode? collection, int line, int column)
            : base(line, column)
        {
            Value = value;
            Items = items;
            Collection = collection;
        }

        public RuleExpressionNode Value { get; }

        /// <summary>
        /// Gets the list items when the right-hand side is a literal list, otherwise null.
        /// </summary>
        public IReadOnlyList<RuleExpressionNode>? Items { get; }

        /// <summary>
        /// Gets the collection expression when the right-hand side is not a literal list, otherwise null.
        /// </summary>
        public RuleExpressionNode? Collection { get; }

        public override int Depth
        {
            get
            {
                int deepest = Value.Depth;
                if (Collection != null && Collection.Depth > deepest)
                {
                    deepest = Collection.Depth;
                }

                if (Items != null)
                {
                    foreach (RuleExpressionNode item in Items)
                    {
                        if (item.Depth > deepest)
                        {
                            deepest = item.Depth;
                        }
                    }
                }

                return deepest + 1;
            }
        }

        public override TResult Accept<TResult>(IRuleExpressionNodeVisitor<TResult> visitor) => visitor.VisitIn(this);
    }
}
