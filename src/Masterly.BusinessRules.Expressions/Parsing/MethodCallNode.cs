using System;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// A built-in function applied to a target with method syntax, for example <c>customer.Name.startsWith('A')</c>.
    /// </summary>
    internal sealed class MethodCallNode : RuleExpressionNode
    {
        public MethodCallNode(RuleExpressionNode target, ExpressionFunction function, bool ignoreCase, RuleExpressionNode argument, int line, int column)
            : base(line, column)
        {
            Target = target;
            Function = function;
            IgnoreCase = ignoreCase;
            Argument = argument;
        }

        public RuleExpressionNode Target { get; }

        public ExpressionFunction Function { get; }

        public bool IgnoreCase { get; }

        public RuleExpressionNode Argument { get; }

        public override int Depth => Math.Max(Target.Depth, Argument.Depth) + 1;

        public override TResult Accept<TResult>(IRuleExpressionNodeVisitor<TResult> visitor) => visitor.VisitMethodCall(this);
    }
}
