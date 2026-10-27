using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// Collects the distinct root identifiers and parameter names of a tree, in order of first appearance.
    /// </summary>
    internal sealed class RuleExpressionNameCollector : IRuleExpressionNodeVisitor<bool>
    {
        private readonly List<string> _identifiers = new List<string>();
        private readonly List<string> _parameters = new List<string>();
        private readonly HashSet<string> _seenIdentifiers = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _seenParameters = new HashSet<string>(StringComparer.Ordinal);

        private RuleExpressionNameCollector()
        {
        }

        public static void Collect(RuleExpressionNode root, out IReadOnlyList<string> identifiers, out IReadOnlyList<string> parameters)
        {
            RuleExpressionNameCollector collector = new RuleExpressionNameCollector();
            root.Accept(collector);
            identifiers = collector._identifiers.ToArray();
            parameters = collector._parameters.ToArray();
        }

        public bool VisitLiteral(LiteralNode node) => true;

        public bool VisitIdentifier(IdentifierNode node)
        {
            if (_seenIdentifiers.Add(node.Name))
            {
                _identifiers.Add(node.Name);
            }

            return true;
        }

        public bool VisitParameter(ParameterNode node)
        {
            if (_seenParameters.Add(node.Name))
            {
                _parameters.Add(node.Name);
            }

            return true;
        }

        public bool VisitMember(MemberNode node) => node.Target.Accept(this);

        public bool VisitMethodCall(MethodCallNode node) => node.Target.Accept(this) && node.Argument.Accept(this);

        public bool VisitUnary(UnaryNode node) => node.Operand.Accept(this);

        public bool VisitBinary(BinaryNode node) => node.Left.Accept(this) && node.Right.Accept(this);

        public bool VisitIn(InNode node)
        {
            node.Value.Accept(this);
            if (node.Items != null)
            {
                foreach (RuleExpressionNode item in node.Items)
                {
                    item.Accept(this);
                }
            }

            node.Collection?.Accept(this);
            return true;
        }
    }
}
