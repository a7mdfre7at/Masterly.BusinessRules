using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// Renders an evaluated tree with its bound values inline, reading what the recorder captured: paths become
    /// <c>path(value)</c>, nested operations gain <c>[result]</c> (or <c>[not evaluated]</c> when short-circuited) and the
    /// root gains <c>=&gt; result</c>.
    /// </summary>
    internal sealed class RuleExpressionExplanationRenderer : IRuleExpressionNodeVisitor<string>
    {
        private const string NotEvaluated = "not evaluated";
        private const string NullText = "null";

        private readonly RuleExpressionBindingRecorder _recorder;
        private readonly CultureInfo _culture;
        private readonly RuleExpressionNode _root;

        private RuleExpressionExplanationRenderer(RuleExpressionNode root, RuleExpressionBindingRecorder recorder, CultureInfo culture)
        {
            _root = root;
            _recorder = recorder;
            _culture = culture;
        }

        public static string Render(RuleExpressionNode root, bool result, RuleExpressionBindingRecorder recorder, CultureInfo culture)
        {
            RuleExpressionExplanationRenderer renderer = new RuleExpressionExplanationRenderer(root, recorder, culture);
            string body = renderer.RenderBody(root);
            return $"{body} => {FormatValue(result, culture)}";
        }

        public static string FormatValue(object? value, CultureInfo culture)
        {
            switch (value)
            {
                case null:
                    return NullText;
                case string text:
                    return RuleExpressionTextRenderer.QuoteString(text);
                case bool flag:
                    return flag ? "true" : "false";
                case Enum enumeration:
                    return enumeration.ToString();
                case IFormattable formattable:
                    return formattable.ToString(null, culture);
                case IEnumerable items:
                    return FormatCollection(items, culture);
                default:
                    return value.ToString() ?? NullText;
            }
        }

        public string VisitLiteral(LiteralNode node) => node.Text;

        public string VisitIdentifier(IdentifierNode node) => RenderPath(node);

        public string VisitParameter(ParameterNode node) => RenderPath(node);

        public string VisitMember(MemberNode node) => RenderPath(node);

        public string VisitMethodCall(MethodCallNode node)
            => Annotate(node, $"{RenderOperand(node.Target)}.{RuleExpressionTextRenderer.FunctionName(node.Function, node.IgnoreCase)}({RenderBody(node.Argument)})");

        public string VisitUnary(UnaryNode node)
        {
            string operand = node.Operand is LiteralNode || node.Operand.IsPath ? RenderBody(node.Operand) : $"({RenderBody(node.Operand)})";
            string symbol = node.Operator == UnaryOperator.Not ? "!" : "-";
            return Annotate(node, symbol + operand);
        }

        public string VisitBinary(BinaryNode node)
        {
            string body = $"{RenderOperand(node.Left)} {RuleExpressionTextRenderer.Symbol(node.Operator)} {RenderOperand(node.Right)}";
            return Annotate(node, body);
        }

        public string VisitIn(InNode node)
        {
            string right;
            if (node.Items != null)
            {
                List<string> items = new List<string>(node.Items.Count);
                foreach (RuleExpressionNode item in node.Items)
                {
                    items.Add(RenderBody(item));
                }

                right = $"[{string.Join(", ", items)}]";
            }
            else
            {
                right = RenderOperand(node.Collection!); // the parser sets exactly one of Items and Collection
            }

            return Annotate(node, $"{RenderOperand(node.Value)} in {right}");
        }

        private string RenderBody(RuleExpressionNode node) => node.Accept(this);

        /// <summary>
        /// Renders an operand, parenthesising nested operators so their bracketed results read unambiguously.
        /// </summary>
        private string RenderOperand(RuleExpressionNode node)
        {
            string text = RenderBody(node);
            bool isOperator = node is BinaryNode || node is InNode;
            return isOperator ? $"({text})" : text;
        }

        private string RenderPath(RuleExpressionNode node)
        {
            if (!_recorder.TryGetStep(node, out RuleExpressionStep step))
            {
                return node.Text;
            }

            string shown = _recorder.IsRedacting ? (step.BoundValue as string) ?? NullText : FormatValue(step.BoundValue, _culture);
            return $"{node.Text}({shown})";
        }

        /// <summary>
        /// Appends the node's bracketed result; the root is left bare because its verdict follows the arrow.
        /// </summary>
        private string Annotate(RuleExpressionNode node, string body)
        {
            if (ReferenceEquals(node, _root))
            {
                return body;
            }

            string annotation = _recorder.TryGetStep(node, out RuleExpressionStep step) ? FormatValue(step.Result, _culture) : NotEvaluated;
            return $"{body} [{annotation}]";
        }

        private static string FormatCollection(IEnumerable items, CultureInfo culture)
        {
            List<string> parts = new List<string>();
            foreach (object? item in items)
            {
                parts.Add(FormatValue(item, culture));
            }

            return $"[{string.Join(", ", parts)}]";
        }
    }
}
