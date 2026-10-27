using System;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// An infix operator applied to two operands. <c>&amp;&amp;</c> and <c>||</c> short-circuit.
    /// </summary>
    internal sealed class BinaryNode : RuleExpressionNode
    {
        public BinaryNode(BinaryOperator op, RuleExpressionNode left, RuleExpressionNode right, int line, int column) : base(line, column)
        {
            Operator = op;
            Left = left;
            Right = right;
        }

        public BinaryOperator Operator { get; }

        public RuleExpressionNode Left { get; }

        public RuleExpressionNode Right { get; }

        public bool IsLogical => Operator == BinaryOperator.And || Operator == BinaryOperator.Or;

        public bool IsComparison => Operator >= BinaryOperator.Equal && Operator <= BinaryOperator.GreaterOrEqual;

        public bool IsArithmetic => Operator >= BinaryOperator.Add;

        public override int Depth => Math.Max(Left.Depth, Right.Depth) + 1;

        public override TResult Accept<TResult>(IRuleExpressionNodeVisitor<TResult> visitor) => visitor.VisitBinary(this);
    }
}
