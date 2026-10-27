using System.Collections.Generic;
using System.Text;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// Renders a node back to normalised source text (word operators become symbols, strings are single-quoted,
    /// parentheses appear only where precedence requires them). Used for <see cref="RuleExpressionNode.Text"/>.
    /// </summary>
    internal sealed class RuleExpressionTextRenderer : IRuleExpressionNodeVisitor<string>
    {
        private const int OrPrecedence = 1;
        private const int AndPrecedence = 2;
        private const int NotPrecedence = 3;
        private const int ComparisonPrecedence = 4;
        private const int AdditivePrecedence = 5;
        private const int MultiplicativePrecedence = 6;
        private const int NegatePrecedence = 7;
        private const int PrimaryPrecedence = 8;

        private static readonly RuleExpressionTextRenderer Instance = new RuleExpressionTextRenderer();

        private RuleExpressionTextRenderer()
        {
        }

        public static string Render(RuleExpressionNode node) => node.Accept(Instance);

        /// <summary>
        /// Renders a child, parenthesising it when it binds looser than its parent (or equally on the right-hand side).
        /// </summary>
        public static string RenderOperand(RuleExpressionNode child, int parentPrecedence, bool isRightOperand)
        {
            string text = child.Text;
            int childPrecedence = PrecedenceOf(child);
            bool needsParentheses = childPrecedence < parentPrecedence || (childPrecedence == parentPrecedence && isRightOperand);
            return needsParentheses ? $"({text})" : text;
        }

        public static int PrecedenceOf(RuleExpressionNode node)
        {
            switch (node)
            {
                case BinaryNode binary:
                    return PrecedenceOf(binary.Operator);
                case InNode _:
                    return ComparisonPrecedence;
                case UnaryNode unary:
                    return unary.Operator == UnaryOperator.Not ? NotPrecedence : NegatePrecedence;
                default:
                    return PrimaryPrecedence;
            }
        }

        public static int PrecedenceOf(BinaryOperator op)
        {
            switch (op)
            {
                case BinaryOperator.Or:
                    return OrPrecedence;
                case BinaryOperator.And:
                    return AndPrecedence;
                case BinaryOperator.Add:
                case BinaryOperator.Subtract:
                    return AdditivePrecedence;
                case BinaryOperator.Multiply:
                case BinaryOperator.Divide:
                case BinaryOperator.Modulo:
                    return MultiplicativePrecedence;
                default:
                    return ComparisonPrecedence;
            }
        }

        public static string Symbol(BinaryOperator op)
        {
            switch (op)
            {
                case BinaryOperator.Or:
                    return "||";
                case BinaryOperator.And:
                    return "&&";
                case BinaryOperator.Equal:
                    return "==";
                case BinaryOperator.NotEqual:
                    return "!=";
                case BinaryOperator.EqualIgnoreCase:
                    return "~=";
                case BinaryOperator.Less:
                    return "<";
                case BinaryOperator.LessOrEqual:
                    return "<=";
                case BinaryOperator.Greater:
                    return ">";
                case BinaryOperator.GreaterOrEqual:
                    return ">=";
                case BinaryOperator.Add:
                    return "+";
                case BinaryOperator.Subtract:
                    return "-";
                case BinaryOperator.Multiply:
                    return "*";
                case BinaryOperator.Divide:
                    return "/";
                default:
                    return "%";
            }
        }

        public static string FunctionName(ExpressionFunction function, bool ignoreCase)
        {
            string name;
            switch (function)
            {
                case ExpressionFunction.StartsWith:
                    name = "startsWith";
                    break;
                case ExpressionFunction.EndsWith:
                    name = "endsWith";
                    break;
                default:
                    name = "contains";
                    break;
            }

            return ignoreCase ? name + "/ci" : name;
        }

        public static string QuoteString(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length + 2);
            builder.Append('\'');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '\'':
                        builder.Append("\\'");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(character);
                        break;
                }
            }

            builder.Append('\'');
            return builder.ToString();
        }

        public string VisitLiteral(LiteralNode node)
        {
            switch (node.Value)
            {
                case null:
                    return "null";
                case string text:
                    return QuoteString(text);
                case bool flag:
                    return flag ? "true" : "false";
                default:
                    return node.SourceText;
            }
        }

        public string VisitIdentifier(IdentifierNode node) => node.Name;

        public string VisitParameter(ParameterNode node) => "@" + node.Name;

        public string VisitMember(MemberNode node) => $"{RenderOperand(node.Target, PrimaryPrecedence, false)}.{node.Member}";

        public string VisitMethodCall(MethodCallNode node)
            => $"{RenderOperand(node.Target, PrimaryPrecedence, false)}.{FunctionName(node.Function, node.IgnoreCase)}({node.Argument.Text})";

        public string VisitUnary(UnaryNode node)
        {
            if (node.Operator == UnaryOperator.Not)
            {
                string operand = PrecedenceOf(node.Operand) == PrimaryPrecedence ? node.Operand.Text : $"({node.Operand.Text})";
                return "!" + operand;
            }

            return "-" + RenderOperand(node.Operand, NegatePrecedence, false);
        }

        public string VisitBinary(BinaryNode node)
        {
            int precedence = PrecedenceOf(node.Operator);
            return $"{RenderOperand(node.Left, precedence, false)} {Symbol(node.Operator)} {RenderOperand(node.Right, precedence, true)}";
        }

        public string VisitIn(InNode node)
        {
            string left = RenderOperand(node.Value, ComparisonPrecedence, false);
            if (node.Items != null)
            {
                List<string> items = new List<string>(node.Items.Count);
                foreach (RuleExpressionNode item in node.Items)
                {
                    items.Add(item.Text);
                }

                return $"{left} in [{string.Join(", ", items)}]";
            }

            return $"{left} in {RenderOperand(node.Collection!, ComparisonPrecedence, true)}"; // the parser sets exactly one of Items and Collection
        }
    }
}
