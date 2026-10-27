using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// Compiles a tree to <c>Func&lt;BusinessRuleContext&lt;T&gt;, bool&gt;</c>. Paths rooted at <c>Data</c> become real,
    /// statically resolved <see cref="MemberExpression"/> chains (null-safe, so a null segment yields null), and a
    /// member that does not exist on the subject type fails here with <see cref="RuleExpressionParseException"/>.
    /// Everything else (context identifiers, parameters, operators) calls the shared <see cref="RuleExpressionRuntime"/>
    /// helpers, so the compiled verdict matches the interpreter's.
    /// </summary>
    internal sealed class RuleExpressionTypedCompiler<T> : IRuleExpressionNodeVisitor<Expression> where T : class
    {
        private static readonly Type ObjectType = typeof(object);
        private static readonly Type RuntimeType = typeof(RuleExpressionRuntime);

        private readonly RuleExpressionOptions _options;
        private readonly ParameterExpression _context = Expression.Parameter(typeof(BusinessRuleContext<T>), "context");
        private readonly ParameterExpression _parameterSource = Expression.Variable(typeof(RuleExpressionParameterSource), "parameters");
        private readonly ConstantExpression _provider;

        private RuleExpressionTypedCompiler(RuleExpressionOptions options, IRuleParameterProvider? parameters)
        {
            _options = options;
            _provider = Expression.Constant(parameters, typeof(IRuleParameterProvider));
        }

        public static Func<BusinessRuleContext<T>, bool> Compile(RuleExpressionNode root, RuleExpressionOptions options, IRuleParameterProvider? parameters, bool hasParameters)
        {
            RuleExpressionTypedCompiler<T> compiler = new RuleExpressionTypedCompiler<T>(options, parameters);
            Expression body = compiler.ToBoolean(root.Accept(compiler), root.Text);
            if (hasParameters)
            {
                Expression createSource = Expression.Call(Runtime(nameof(RuleExpressionRuntime.CreateParameterSource)), compiler._context, compiler._provider);
                body = Expression.Block(typeof(bool), new[] { compiler._parameterSource }, Expression.Assign(compiler._parameterSource, createSource), body);
            }

            return Expression.Lambda<Func<BusinessRuleContext<T>, bool>>(body, compiler._context).Compile();
        }

        public Expression VisitLiteral(LiteralNode node) => Expression.Constant(node.Value, ObjectType);

        public Expression VisitIdentifier(IdentifierNode node)
        {
            if (node.IsData)
            {
                return CompileStaticPath(new List<MemberNode>());
            }

            return Expression.Call(Runtime(nameof(RuleExpressionRuntime.BindIdentifier)), _context, Expression.Constant(node.Name));
        }

        public Expression VisitParameter(ParameterNode node)
            => Expression.Call(Runtime(nameof(RuleExpressionRuntime.BindParameter)), _parameterSource, _context, Expression.Constant(node.Name), Expression.Constant(node.Text));

        public Expression VisitMember(MemberNode node)
        {
            List<MemberNode>? chain = TryCollectDataChain(node);
            if (chain != null)
            {
                return CompileStaticPath(chain);
            }

            Expression target = node.Target.Accept(this);
            return Expression.Call(Runtime(nameof(RuleExpressionRuntime.GetMember)), target, Expression.Constant(node.Member), Expression.Constant(_options), Expression.Constant(node.Text));
        }

        public Expression VisitMethodCall(MethodCallNode node)
        {
            Expression target = node.Target.Accept(this);
            Expression argument = node.Argument.Accept(this);
            return Expression.Call(
                Runtime(nameof(RuleExpressionRuntime.InvokeFunction)),
                Expression.Constant(node.Function),
                target,
                argument,
                Expression.Constant(node.IgnoreCase),
                Expression.Constant(node.Text));
        }

        public Expression VisitUnary(UnaryNode node)
        {
            Expression operand = node.Operand.Accept(this);
            string helper = node.Operator == UnaryOperator.Not ? nameof(RuleExpressionRuntime.Not) : nameof(RuleExpressionRuntime.Negate);
            return Expression.Call(Runtime(helper), operand, Expression.Constant(node.Text));
        }

        public Expression VisitBinary(BinaryNode node)
        {
            if (node.IsLogical)
            {
                Expression left = ToBoolean(node.Left.Accept(this), node.Left.Text);
                Expression right = ToBoolean(node.Right.Accept(this), node.Right.Text);
                Expression logical = node.Operator == BinaryOperator.And ? Expression.AndAlso(left, right) : Expression.OrElse(left, right);
                return Expression.Convert(logical, ObjectType);
            }

            Expression leftValue = node.Left.Accept(this);
            Expression rightValue = node.Right.Accept(this);
            switch (node.Operator)
            {
                case BinaryOperator.Equal:
                    return Expression.Call(Runtime(nameof(RuleExpressionRuntime.AreEqual)), leftValue, rightValue, Expression.Constant(false));
                case BinaryOperator.EqualIgnoreCase:
                    return Expression.Call(Runtime(nameof(RuleExpressionRuntime.AreEqual)), leftValue, rightValue, Expression.Constant(true));
                case BinaryOperator.NotEqual:
                    return Expression.Call(Runtime(nameof(RuleExpressionRuntime.AreNotEqual)), leftValue, rightValue);
                case BinaryOperator.Less:
                case BinaryOperator.LessOrEqual:
                case BinaryOperator.Greater:
                case BinaryOperator.GreaterOrEqual:
                    return Expression.Call(Runtime(nameof(RuleExpressionRuntime.Compare)), Expression.Constant(node.Operator), leftValue, rightValue, Expression.Constant(node.Text));
                default:
                    return Expression.Call(Runtime(nameof(RuleExpressionRuntime.Arithmetic)), Expression.Constant(node.Operator), leftValue, rightValue, Expression.Constant(node.Text));
            }
        }

        public Expression VisitIn(InNode node)
        {
            Expression value = node.Value.Accept(this);
            if (node.Items == null)
            {
                Expression collection = node.Collection!.Accept(this); // the parser sets exactly one of Items and Collection
                return Expression.Call(Runtime(nameof(RuleExpressionRuntime.IsInCollection)), value, collection, Expression.Constant(node.Text));
            }

            // value == item0 || value == item1 || ...: the value is computed once and the items after a match are never
            // evaluated, exactly like the interpreter.
            ParameterExpression local = Expression.Variable(ObjectType);
            Expression match = Expression.Constant(false);
            for (int index = node.Items.Count - 1; index >= 0; index--)
            {
                Expression isItem = Expression.Call(Runtime(nameof(RuleExpressionRuntime.IsItem)), local, node.Items[index].Accept(this));
                match = index == node.Items.Count - 1 ? isItem : Expression.OrElse(isItem, match);
            }

            return Expression.Block(ObjectType, new[] { local }, Expression.Assign(local, value), Expression.Convert(match, ObjectType));
        }

        private Expression ToBoolean(Expression value, string nodeText)
            => Expression.Call(Runtime(nameof(RuleExpressionRuntime.ToBoolean)), value, Expression.Constant(nodeText));

        /// <summary>
        /// Returns the member chain (outermost last) when the path is rooted at <c>Data</c>, otherwise null.
        /// </summary>
        private static List<MemberNode>? TryCollectDataChain(MemberNode node)
        {
            List<MemberNode> chain = new List<MemberNode>();
            RuleExpressionNode current = node;
            while (current is MemberNode member)
            {
                chain.Insert(0, member);
                current = member.Target;
            }

            return current is IdentifierNode root && root.IsData ? chain : null;
        }

        private Expression CompileStaticPath(List<MemberNode> chain)
        {
            Expression data = Expression.Property(_context, nameof(BusinessRuleContext<T>.Data));
            return BuildSegment(data, chain, 0);
        }

        private Expression BuildSegment(Expression current, List<MemberNode> chain, int index)
        {
            bool canBeNull = !current.Type.IsValueType || Nullable.GetUnderlyingType(current.Type) != null;
            if (!canBeNull)
            {
                return index == chain.Count ? Expression.Convert(current, ObjectType) : BuildSegment(AccessMember(current, chain[index]), chain, index + 1);
            }

            ParameterExpression local = Expression.Variable(current.Type);
            Expression rest = index == chain.Count ? Expression.Convert(local, ObjectType) : BuildSegment(AccessMember(local, chain[index]), chain, index + 1);
            Expression isNull = Expression.Equal(local, Expression.Constant(null, local.Type));
            return Expression.Block(ObjectType, new[] { local }, Expression.Assign(local, current), Expression.Condition(isNull, Expression.Constant(null, ObjectType), rest));
        }

        private Expression AccessMember(Expression target, MemberNode node)
        {
            Type inspected = Nullable.GetUnderlyingType(target.Type) ?? target.Type;
            Expression source = inspected != target.Type ? Expression.Property(target, nameof(Nullable<int>.Value)) : target;

            PropertyInfo? property = RuleExpressionMemberAccessor.FindProperty(inspected, node.Member);
            if (property != null && _options.IsAccessible(property))
            {
                return Expression.Property(source, property);
            }

            if (RuleExpressionMemberAccessor.IsCountOrLength(node.Member))
            {
                if (inspected == typeof(string))
                {
                    return Expression.Property(source, nameof(string.Length));
                }

                if (typeof(ICollection).IsAssignableFrom(inspected))
                {
                    return Expression.Property(Expression.Convert(source, typeof(ICollection)), nameof(ICollection.Count));
                }
            }

            string members = string.Join(", ", RuleExpressionMemberAccessor.AccessibleMembers(inspected, _options));
            string available = members.Length == 0 ? "no members are accessible" : $"accessible members: {members}";
            throw new RuleExpressionParseException($"Member '{node.Member}' does not exist on type '{inspected.Name}' ({available})", node.Line, node.Column);
        }

        private static MethodInfo Runtime(string name) => RuntimeType.GetMethod(name) ?? throw new MissingMethodException(RuntimeType.FullName, name);
    }
}
