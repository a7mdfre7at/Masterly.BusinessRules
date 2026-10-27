namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// A literal: a <see cref="decimal"/> number, a string, a boolean or null. Numbers keep their source text so
    /// <c>12.50</c> renders as written.
    /// </summary>
    internal sealed class LiteralNode : RuleExpressionNode
    {
        public LiteralNode(object? value, string sourceText, int line, int column) : base(line, column)
        {
            Value = value;
            SourceText = sourceText;
        }

        public object? Value { get; }

        public string SourceText { get; }

        public override int Depth => 1;

        public override TResult Accept<TResult>(IRuleExpressionNodeVisitor<TResult> visitor) => visitor.VisitLiteral(this);
    }
}
