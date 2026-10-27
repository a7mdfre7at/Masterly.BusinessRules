namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// A root identifier bound to a context entry of the same name; the reserved name <see cref="RuleExpression.DataIdentifier"/>
    /// binds to the typed data of a <see cref="BusinessRuleContext{T}"/>.
    /// </summary>
    internal sealed class IdentifierNode : RuleExpressionNode
    {
        public IdentifierNode(string name, int line, int column) : base(line, column)
        {
            Name = name;
        }

        public string Name { get; }

        public bool IsData => string.Equals(Name, RuleExpression.DataIdentifier, System.StringComparison.Ordinal);

        public override int Depth => 1;

        public override bool IsPath => true;

        public override TResult Accept<TResult>(IRuleExpressionNodeVisitor<TResult> visitor) => visitor.VisitIdentifier(this);
    }
}
