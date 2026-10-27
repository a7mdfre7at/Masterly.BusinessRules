namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// A null-safe member access <c>target.Member</c>: a null target yields null instead of throwing.
    /// </summary>
    internal sealed class MemberNode : RuleExpressionNode
    {
        public MemberNode(RuleExpressionNode target, string member, int line, int column) : base(line, column)
        {
            Target = target;
            Member = member;
        }

        public RuleExpressionNode Target { get; }

        public string Member { get; }

        public override int Depth => Target.Depth + 1;

        public override bool IsPath => true;

        public override TResult Accept<TResult>(IRuleExpressionNodeVisitor<TResult> visitor) => visitor.VisitMember(this);
    }
}
