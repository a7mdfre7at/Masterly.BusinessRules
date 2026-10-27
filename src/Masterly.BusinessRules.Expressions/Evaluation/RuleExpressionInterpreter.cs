using System.Collections.Generic;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// The one evaluator of the untyped path. It walks the tree against a <see cref="BusinessRuleContext"/> and, when a
    /// <see cref="RuleExpressionBindingRecorder"/> is attached, records every binding and operation as it happens, so
    /// the verdict and the explanation come from the same walk by construction. The interpreter needs no dynamic code
    /// generation, which keeps this path AOT- and trimming-friendly.
    /// </summary>
    internal sealed class RuleExpressionInterpreter : IRuleExpressionNodeVisitor<object?>
    {
        private readonly BusinessRuleContext _context;
        private readonly RuleExpressionParameterSource _parameters;
        private readonly RuleExpressionOptions _options;
        private readonly RuleExpressionBindingRecorder? _recorder;

        public RuleExpressionInterpreter(BusinessRuleContext context, RuleExpressionParameterSource parameters, RuleExpressionOptions options, RuleExpressionBindingRecorder? recorder)
        {
            _context = context;
            _parameters = parameters;
            _options = options;
            _recorder = recorder;
        }

        /// <summary>
        /// Evaluates the root and coerces the result to a boolean.
        /// </summary>
        public bool EvaluateRoot(RuleExpressionNode root)
        {
            object? result = Evaluate(root);
            return RuleExpressionValues.ToBoolean(result, root.Text);
        }

        public object? VisitLiteral(LiteralNode node) => node.Value;

        public object? VisitIdentifier(IdentifierNode node)
        {
            if (node.IsData && _context.ContainsKey(RuleExpression.TypedDataKey))
            {
                return _context.Get<object>(RuleExpression.TypedDataKey);
            }

            if (_context.ContainsKey(node.Name))
            {
                return _context.Get<object>(node.Name);
            }

            throw new RuleExpressionBindingException(node.Name, _context.Keys);
        }

        public object? VisitParameter(ParameterNode node)
        {
            if (_parameters.TryResolve(node.Name, out object? value))
            {
                return value;
            }

            if (_context.ContainsKey(node.Name))
            {
                return _context.Get<object>(node.Name);
            }

            throw new RuleExpressionBindingException(node.Text, _context.Keys);
        }

        public object? VisitMember(MemberNode node)
        {
            object? target = node.Target.Accept(this);
            return RuleExpressionMemberAccessor.GetValue(target, node.Member, _options, node.Text);
        }

        public object? VisitMethodCall(MethodCallNode node)
        {
            object? target = Evaluate(node.Target);
            object? argument = Evaluate(node.Argument);
            return RuleExpressionValues.InvokeFunction(node.Function, target, argument, node.IgnoreCase, node.Text);
        }

        public object? VisitUnary(UnaryNode node)
        {
            object? operand = Evaluate(node.Operand);
            if (node.Operator == UnaryOperator.Not)
            {
                return !RuleExpressionValues.ToBoolean(operand, node.Text);
            }

            return RuleExpressionValues.Negate(operand, node.Text);
        }

        public object? VisitBinary(BinaryNode node)
        {
            if (node.IsLogical)
            {
                return EvaluateLogical(node);
            }

            object? left = Evaluate(node.Left);
            object? right = Evaluate(node.Right);
            switch (node.Operator)
            {
                case BinaryOperator.Equal:
                    return RuleExpressionValues.AreEqual(left, right, false);
                case BinaryOperator.NotEqual:
                    return !RuleExpressionValues.AreEqual(left, right, false);
                case BinaryOperator.EqualIgnoreCase:
                    return RuleExpressionValues.AreEqual(left, right, true);
                case BinaryOperator.Less:
                case BinaryOperator.LessOrEqual:
                case BinaryOperator.Greater:
                case BinaryOperator.GreaterOrEqual:
                    return RuleExpressionValues.Compare(node.Operator, left, right, node.Text);
                default:
                    return RuleExpressionValues.Arithmetic(node.Operator, left, right, node.Text);
            }
        }

        public object? VisitIn(InNode node)
        {
            object? value = Evaluate(node.Value);
            if (node.Items == null)
            {
                object? collection = Evaluate(node.Collection!); // the parser sets exactly one of Items and Collection
                return RuleExpressionValues.IsInCollection(value, collection, node.Text);
            }

            foreach (RuleExpressionNode item in node.Items)
            {
                if (RuleExpressionValues.AreEqual(value, Evaluate(item), false))
                {
                    return true;
                }
            }

            return false;
        }

        private bool EvaluateLogical(BinaryNode node)
        {
            bool left = RuleExpressionValues.ToBoolean(Evaluate(node.Left), node.Left.Text);
            if (node.Operator == BinaryOperator.And && !left)
            {
                return false;
            }

            if (node.Operator == BinaryOperator.Or && left)
            {
                return true;
            }

            return RuleExpressionValues.ToBoolean(Evaluate(node.Right), node.Right.Text);
        }

        /// <summary>
        /// Evaluates a node and records it: a path node as a binding, any other non-literal node as an operation.
        /// Member targets bypass this method so only the full path is recorded.
        /// </summary>
        private object? Evaluate(RuleExpressionNode node)
        {
            object? value = node.Accept(this);
            if (_recorder != null)
            {
                if (node.IsPath)
                {
                    _recorder.RecordBinding(node, value);
                }
                else if (!(node is LiteralNode))
                {
                    _recorder.RecordOperation(node, value);
                }
            }

            return value;
        }
    }
}
