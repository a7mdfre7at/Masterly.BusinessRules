using System;
using System.Collections.Generic;
using System.Globalization;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// Recursive-descent parser producing the immutable node tree. Precedence, loosest first: <c>||</c>/<c>or</c>,
    /// <c>&amp;&amp;</c>/<c>and</c>, <c>!</c>/<c>not</c>, comparisons and <c>in</c>, <c>+ -</c>, <c>* / %</c>, unary
    /// <c>-</c>, member access and function calls. Nesting is bounded by <see cref="RuleExpressionOptions.MaxDepth"/>.
    /// </summary>
    internal sealed class RuleExpressionParser
    {
        private const string TrueKeyword = "true";
        private const string FalseKeyword = "false";
        private const string NullKeyword = "null";
        private const string InKeyword = "in";
        private const string AndKeyword = "and";
        private const string OrKeyword = "or";
        private const string NotKeyword = "not";
        private const string CaseInsensitiveSuffix = "ci";
        private const string StartsWithFunction = "startsWith";
        private const string EndsWithFunction = "endsWith";
        private const string ContainsFunction = "contains";

        private readonly IReadOnlyList<RuleExpressionToken> _tokens;
        private readonly RuleExpressionOptions _options;
        private int _position;
        private int _nesting;

        private RuleExpressionParser(IReadOnlyList<RuleExpressionToken> tokens, RuleExpressionOptions options)
        {
            _tokens = tokens;
            _options = options;
        }

        public static RuleExpressionNode Parse(string text, RuleExpressionOptions options)
        {
            if (text.Length > options.MaxLength)
            {
                throw new RuleExpressionParseException($"The expression is {text.Length} characters long; the limit is {options.MaxLength}", 1, options.MaxLength + 1);
            }

            RuleExpressionParser parser = new RuleExpressionParser(RuleExpressionTokenizer.Tokenize(text), options);
            RuleExpressionNode root = parser.ParseRoot();
            if (root.Depth > options.MaxDepth)
            {
                throw new RuleExpressionParseException($"The expression nests {root.Depth} levels deep; the limit is {options.MaxDepth}", root.Line, root.Column);
            }

            return root;
        }

        private RuleExpressionToken Current => _tokens[_position];

        private RuleExpressionNode ParseRoot()
        {
            if (Current.Kind == RuleExpressionTokenKind.End)
            {
                throw new RuleExpressionParseException("The expression is empty", Current.Line, Current.Column);
            }

            RuleExpressionNode root = ParseOr();
            if (Current.Kind != RuleExpressionTokenKind.End)
            {
                throw Unexpected(Current);
            }

            return root;
        }

        private RuleExpressionNode ParseOr()
        {
            RuleExpressionNode left = ParseAnd();
            while (Current.Kind == RuleExpressionTokenKind.Or || IsKeyword(Current, OrKeyword))
            {
                RuleExpressionToken op = Advance();
                RuleExpressionNode right = ParseAnd();
                left = new BinaryNode(BinaryOperator.Or, left, right, op.Line, op.Column);
            }

            return left;
        }

        private RuleExpressionNode ParseAnd()
        {
            RuleExpressionNode left = ParseNot();
            while (Current.Kind == RuleExpressionTokenKind.And || IsKeyword(Current, AndKeyword))
            {
                RuleExpressionToken op = Advance();
                RuleExpressionNode right = ParseNot();
                left = new BinaryNode(BinaryOperator.And, left, right, op.Line, op.Column);
            }

            return left;
        }

        private RuleExpressionNode ParseNot()
        {
            if (Current.Kind == RuleExpressionTokenKind.Not || IsKeyword(Current, NotKeyword))
            {
                RuleExpressionToken op = Advance();
                EnterNesting(op);
                RuleExpressionNode operand = ParseNot();
                LeaveNesting();
                return new UnaryNode(UnaryOperator.Not, operand, op.Line, op.Column);
            }

            return ParseComparison();
        }

        private RuleExpressionNode ParseComparison()
        {
            RuleExpressionNode left = ParseAdditive();
            if (TryComparisonOperator(Current.Kind, out BinaryOperator op))
            {
                RuleExpressionToken token = Advance();
                RuleExpressionNode right = ParseAdditive();
                return new BinaryNode(op, left, right, token.Line, token.Column);
            }

            if (IsKeyword(Current, InKeyword))
            {
                RuleExpressionToken token = Advance();
                if (Current.Kind == RuleExpressionTokenKind.LeftBracket)
                {
                    return new InNode(left, ParseList(), null, token.Line, token.Column);
                }

                return new InNode(left, null, ParseAdditive(), token.Line, token.Column);
            }

            return left;
        }

        private IReadOnlyList<RuleExpressionNode> ParseList()
        {
            RuleExpressionToken open = Expect(RuleExpressionTokenKind.LeftBracket);
            EnterNesting(open);
            List<RuleExpressionNode> items = new List<RuleExpressionNode>();
            if (Current.Kind != RuleExpressionTokenKind.RightBracket)
            {
                items.Add(ParseOr());
                while (Current.Kind == RuleExpressionTokenKind.Comma)
                {
                    Advance();
                    items.Add(ParseOr());
                }
            }

            Expect(RuleExpressionTokenKind.RightBracket);
            LeaveNesting();
            return items;
        }

        private RuleExpressionNode ParseAdditive()
        {
            RuleExpressionNode left = ParseMultiplicative();
            while (Current.Kind == RuleExpressionTokenKind.Plus || Current.Kind == RuleExpressionTokenKind.Minus)
            {
                RuleExpressionToken op = Advance();
                BinaryOperator binary = op.Kind == RuleExpressionTokenKind.Plus ? BinaryOperator.Add : BinaryOperator.Subtract;
                RuleExpressionNode right = ParseMultiplicative();
                left = new BinaryNode(binary, left, right, op.Line, op.Column);
            }

            return left;
        }

        private RuleExpressionNode ParseMultiplicative()
        {
            RuleExpressionNode left = ParseUnary();
            while (TryMultiplicativeOperator(Current.Kind, out BinaryOperator op))
            {
                RuleExpressionToken token = Advance();
                RuleExpressionNode right = ParseUnary();
                left = new BinaryNode(op, left, right, token.Line, token.Column);
            }

            return left;
        }

        private RuleExpressionNode ParseUnary()
        {
            if (Current.Kind == RuleExpressionTokenKind.Minus)
            {
                RuleExpressionToken op = Advance();
                EnterNesting(op);
                RuleExpressionNode operand = ParseUnary();
                LeaveNesting();
                return new UnaryNode(UnaryOperator.Negate, operand, op.Line, op.Column);
            }

            return ParsePostfix();
        }

        private RuleExpressionNode ParsePostfix()
        {
            RuleExpressionNode node = ParsePrimary();
            while (Current.Kind == RuleExpressionTokenKind.Dot)
            {
                Advance();
                RuleExpressionToken name = Expect(RuleExpressionTokenKind.Identifier);
                bool ignoreCase = TryConsumeCaseInsensitiveSuffix(name);
                if (Current.Kind == RuleExpressionTokenKind.LeftParenthesis)
                {
                    node = ParseMethodCall(node, name, ignoreCase);
                }
                else if (ignoreCase)
                {
                    throw new RuleExpressionParseException("'/ci' is only valid on a function call such as startsWith/ci('a')", Current.Line, Current.Column);
                }
                else
                {
                    node = new MemberNode(node, name.Text, name.Line, name.Column);
                }
            }

            return node;
        }

        /// <summary>
        /// Consumes a <c>/ci</c> suffix written directly after a function name (no whitespace), so <c>x.Total / ci</c>
        /// with spaces stays a division by the identifier <c>ci</c>.
        /// </summary>
        private bool TryConsumeCaseInsensitiveSuffix(RuleExpressionToken name)
        {
            RuleExpressionToken slash = Current;
            bool isSuffix = slash.Kind == RuleExpressionTokenKind.Slash
                && _position + 1 < _tokens.Count
                && IsKeyword(_tokens[_position + 1], CaseInsensitiveSuffix)
                && IsAdjacent(name, slash)
                && IsAdjacent(slash, _tokens[_position + 1]);
            if (!isSuffix)
            {
                return false;
            }

            Advance();
            Advance();
            return true;
        }

        private static bool IsAdjacent(RuleExpressionToken first, RuleExpressionToken second)
            => first.Line == second.Line && first.Column + first.Text.Length == second.Column;

        private RuleExpressionNode ParseMethodCall(RuleExpressionNode target, RuleExpressionToken name, bool ignoreCase)
        {
            ExpressionFunction function = ResolveFunction(name);
            RuleExpressionToken open = Expect(RuleExpressionTokenKind.LeftParenthesis);
            EnterNesting(open);
            RuleExpressionNode argument = ParseOr();
            Expect(RuleExpressionTokenKind.RightParenthesis);
            LeaveNesting();
            return new MethodCallNode(target, function, ignoreCase, argument, name.Line, name.Column);
        }

        private static ExpressionFunction ResolveFunction(RuleExpressionToken name)
        {
            if (IsKeyword(name, StartsWithFunction))
            {
                return ExpressionFunction.StartsWith;
            }

            if (IsKeyword(name, EndsWithFunction))
            {
                return ExpressionFunction.EndsWith;
            }

            if (IsKeyword(name, ContainsFunction))
            {
                return ExpressionFunction.Contains;
            }

            throw new RuleExpressionParseException($"Unknown function '{name.Text}'; supported functions are {ContainsFunction}, {EndsWithFunction} and {StartsWithFunction}", name.Line, name.Column);
        }

        private RuleExpressionNode ParsePrimary()
        {
            RuleExpressionToken token = Current;
            switch (token.Kind)
            {
                case RuleExpressionTokenKind.Number:
                    Advance();
                    return new LiteralNode(decimal.Parse(token.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture), token.Text, token.Line, token.Column);
                case RuleExpressionTokenKind.String:
                    Advance();
                    return new LiteralNode(token.Text, token.Text, token.Line, token.Column);
                case RuleExpressionTokenKind.Parameter:
                    Advance();
                    return new ParameterNode(token.Text, token.Line, token.Column);
                case RuleExpressionTokenKind.Identifier:
                    return ParseIdentifierOrKeyword();
                case RuleExpressionTokenKind.LeftParenthesis:
                    return ParseParenthesised();
                case RuleExpressionTokenKind.LeftBracket:
                    throw new RuleExpressionParseException("A list is only valid on the right-hand side of 'in'", token.Line, token.Column);
                default:
                    throw Unexpected(token);
            }
        }

        private RuleExpressionNode ParseIdentifierOrKeyword()
        {
            RuleExpressionToken token = Advance();
            if (IsKeyword(token, TrueKeyword))
            {
                return new LiteralNode(true, TrueKeyword, token.Line, token.Column);
            }

            if (IsKeyword(token, FalseKeyword))
            {
                return new LiteralNode(false, FalseKeyword, token.Line, token.Column);
            }

            if (IsKeyword(token, NullKeyword))
            {
                return new LiteralNode(null, NullKeyword, token.Line, token.Column);
            }

            if (IsKeyword(token, InKeyword) || IsKeyword(token, AndKeyword) || IsKeyword(token, OrKeyword) || IsKeyword(token, NotKeyword))
            {
                throw new RuleExpressionParseException($"Unexpected keyword '{token.Text}'", token.Line, token.Column);
            }

            return new IdentifierNode(token.Text, token.Line, token.Column);
        }

        private RuleExpressionNode ParseParenthesised()
        {
            RuleExpressionToken open = Advance();
            EnterNesting(open);
            RuleExpressionNode inner = ParseOr();
            Expect(RuleExpressionTokenKind.RightParenthesis);
            LeaveNesting();
            return inner;
        }

        private void EnterNesting(RuleExpressionToken token)
        {
            _nesting++;
            if (_nesting > _options.MaxDepth)
            {
                throw new RuleExpressionParseException($"The expression nests more than {_options.MaxDepth} levels deep", token.Line, token.Column);
            }
        }

        private void LeaveNesting() => _nesting--;

        private RuleExpressionToken Advance()
        {
            RuleExpressionToken token = Current;
            if (token.Kind != RuleExpressionTokenKind.End)
            {
                _position++;
            }

            return token;
        }

        private RuleExpressionToken Expect(RuleExpressionTokenKind kind)
        {
            if (Current.Kind != kind)
            {
                throw Unexpected(Current, kind);
            }

            return Advance();
        }

        private static bool IsKeyword(RuleExpressionToken token, string keyword)
            => token.Kind == RuleExpressionTokenKind.Identifier && string.Equals(token.Text, keyword, StringComparison.OrdinalIgnoreCase);

        private static bool TryComparisonOperator(RuleExpressionTokenKind kind, out BinaryOperator op)
        {
            switch (kind)
            {
                case RuleExpressionTokenKind.Equal:
                    op = BinaryOperator.Equal;
                    return true;
                case RuleExpressionTokenKind.NotEqual:
                    op = BinaryOperator.NotEqual;
                    return true;
                case RuleExpressionTokenKind.EqualIgnoreCase:
                    op = BinaryOperator.EqualIgnoreCase;
                    return true;
                case RuleExpressionTokenKind.Less:
                    op = BinaryOperator.Less;
                    return true;
                case RuleExpressionTokenKind.LessOrEqual:
                    op = BinaryOperator.LessOrEqual;
                    return true;
                case RuleExpressionTokenKind.Greater:
                    op = BinaryOperator.Greater;
                    return true;
                case RuleExpressionTokenKind.GreaterOrEqual:
                    op = BinaryOperator.GreaterOrEqual;
                    return true;
                default:
                    op = default;
                    return false;
            }
        }

        private static bool TryMultiplicativeOperator(RuleExpressionTokenKind kind, out BinaryOperator op)
        {
            switch (kind)
            {
                case RuleExpressionTokenKind.Star:
                    op = BinaryOperator.Multiply;
                    return true;
                case RuleExpressionTokenKind.Slash:
                    op = BinaryOperator.Divide;
                    return true;
                case RuleExpressionTokenKind.Percent:
                    op = BinaryOperator.Modulo;
                    return true;
                default:
                    op = default;
                    return false;
            }
        }

        private static RuleExpressionParseException Unexpected(RuleExpressionToken token)
        {
            string message = token.Kind == RuleExpressionTokenKind.End ? "Unexpected end of expression" : $"Unexpected token '{token.Text}'";
            return new RuleExpressionParseException(message, token.Line, token.Column);
        }

        private static RuleExpressionParseException Unexpected(RuleExpressionToken token, RuleExpressionTokenKind expected)
        {
            string found = token.Kind == RuleExpressionTokenKind.End ? "end of expression" : $"'{token.Text}'";
            return new RuleExpressionParseException($"Expected {Describe(expected)} but found {found}", token.Line, token.Column);
        }

        private static string Describe(RuleExpressionTokenKind kind)
        {
            switch (kind)
            {
                case RuleExpressionTokenKind.Identifier:
                    return "an identifier";
                case RuleExpressionTokenKind.LeftParenthesis:
                    return "'('";
                case RuleExpressionTokenKind.RightParenthesis:
                    return "')'";
                case RuleExpressionTokenKind.LeftBracket:
                    return "'['";
                case RuleExpressionTokenKind.RightBracket:
                    return "']'";
                default:
                    return kind.ToString();
            }
        }
    }
}
