namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// A prefix operator applied to one operand.
    /// </summary>
    internal sealed class UnaryNode : RuleExpressionNode
    {
        public UnaryNode(UnaryOperator op, RuleExpressionNode operand, int line, int column) : base(line, column)
        {
            Operator = op;
            Operand = operand;
        }

        public UnaryOperator Operator { get; }

        public RuleExpressionNode Operand { get; }

        public override int Depth => Operand.Depth + 1;

        public override TResult Accept<TResult>(IRuleExpressionNodeVisitor<TResult> visitor) => visitor.VisitUnary(this);
    }
}
