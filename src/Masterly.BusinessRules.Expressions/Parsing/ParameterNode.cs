namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// An <c>@name</c> parameter resolved through the rule's declared parameters and <see cref="IRuleParameterProvider"/>s
    /// before falling back to the context entry of the same name.
    /// </summary>
    internal sealed class ParameterNode : RuleExpressionNode
    {
        public ParameterNode(string name, int line, int column) : base(line, column)
        {
            Name = name;
        }

        public string Name { get; }

        public override int Depth => 1;

        public override bool IsPath => true;

        public override TResult Accept<TResult>(IRuleExpressionNodeVisitor<TResult> visitor) => visitor.VisitParameter(this);
    }
}
