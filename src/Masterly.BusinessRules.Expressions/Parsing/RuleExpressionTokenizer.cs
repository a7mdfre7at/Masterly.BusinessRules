using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// Splits expression text into tokens, tracking 1-based line and column positions. String literals accept single or
    /// double quotes with <c>\'</c>, <c>\"</c>, <c>\\</c>, <c>\n</c>, <c>\r</c> and <c>\t</c> escapes; numbers are
    /// digits with an optional fraction and are always parsed with the invariant culture.
    /// </summary>
    internal sealed class RuleExpressionTokenizer
    {
        private const char ParameterPrefix = '@';
        private const char SingleQuote = '\'';
        private const char DoubleQuote = '"';
        private const char Escape = '\\';
        private const char DecimalSeparator = '.';
        private const char Underscore = '_';

        private readonly string _text;
        private int _index;
        private int _line = 1;
        private int _column = 1;

        private RuleExpressionTokenizer(string text)
        {
            _text = text;
        }

        public static IReadOnlyList<RuleExpressionToken> Tokenize(string text)
        {
            RuleExpressionTokenizer tokenizer = new RuleExpressionTokenizer(text);
            List<RuleExpressionToken> tokens = new List<RuleExpressionToken>();
            RuleExpressionToken token;
            do
            {
                token = tokenizer.Next();
                tokens.Add(token);
            }
            while (token.Kind != RuleExpressionTokenKind.End);

            return tokens;
        }

        private RuleExpressionToken Next()
        {
            SkipWhiteSpace();
            if (_index >= _text.Length)
            {
                return new RuleExpressionToken(RuleExpressionTokenKind.End, string.Empty, _line, _column);
            }

            char current = _text[_index];
            int line = _line;
            int column = _column;

            if (char.IsLetter(current) || current == Underscore)
            {
                return new RuleExpressionToken(RuleExpressionTokenKind.Identifier, ReadIdentifier(), line, column);
            }

            if (char.IsDigit(current))
            {
                return new RuleExpressionToken(RuleExpressionTokenKind.Number, ReadNumber(line, column), line, column);
            }

            switch (current)
            {
                case ParameterPrefix:
                    return ReadParameter(line, column);
                case SingleQuote:
                case DoubleQuote:
                    return new RuleExpressionToken(RuleExpressionTokenKind.String, ReadString(current, line, column), line, column);
                case '(':
                    return Single(RuleExpressionTokenKind.LeftParenthesis, line, column);
                case ')':
                    return Single(RuleExpressionTokenKind.RightParenthesis, line, column);
                case '[':
                    return Single(RuleExpressionTokenKind.LeftBracket, line, column);
                case ']':
                    return Single(RuleExpressionTokenKind.RightBracket, line, column);
                case ',':
                    return Single(RuleExpressionTokenKind.Comma, line, column);
                case DecimalSeparator:
                    return Single(RuleExpressionTokenKind.Dot, line, column);
                case '+':
                    return Single(RuleExpressionTokenKind.Plus, line, column);
                case '-':
                    return Single(RuleExpressionTokenKind.Minus, line, column);
                case '*':
                    return Single(RuleExpressionTokenKind.Star, line, column);
                case '/':
                    return Single(RuleExpressionTokenKind.Slash, line, column);
                case '%':
                    return Single(RuleExpressionTokenKind.Percent, line, column);
                case '!':
                    return PeekIs('=') ? Double(RuleExpressionTokenKind.NotEqual, line, column) : Single(RuleExpressionTokenKind.Not, line, column);
                case '=':
                    if (PeekIs('='))
                    {
                        return Double(RuleExpressionTokenKind.Equal, line, column);
                    }

                    throw Unexpected("Unexpected '='; use '==' for equality", line, column);
                case '~':
                    if (PeekIs('='))
                    {
                        return Double(RuleExpressionTokenKind.EqualIgnoreCase, line, column);
                    }

                    throw Unexpected("Unexpected '~'; use '~=' for case-insensitive equality", line, column);
                case '<':
                    return PeekIs('=') ? Double(RuleExpressionTokenKind.LessOrEqual, line, column) : Single(RuleExpressionTokenKind.Less, line, column);
                case '>':
                    return PeekIs('=') ? Double(RuleExpressionTokenKind.GreaterOrEqual, line, column) : Single(RuleExpressionTokenKind.Greater, line, column);
                case '&':
                    if (PeekIs('&'))
                    {
                        return Double(RuleExpressionTokenKind.And, line, column);
                    }

                    throw Unexpected("Unexpected '&'; use '&&' or 'and'", line, column);
                case '|':
                    if (PeekIs('|'))
                    {
                        return Double(RuleExpressionTokenKind.Or, line, column);
                    }

                    throw Unexpected("Unexpected '|'; use '||' or 'or'", line, column);
                default:
                    throw Unexpected($"Unexpected character '{current}'", line, column);
            }
        }

        private void SkipWhiteSpace()
        {
            while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
            {
                Advance();
            }
        }

        private string ReadIdentifier()
        {
            int start = _index;
            while (_index < _text.Length && (char.IsLetterOrDigit(_text[_index]) || _text[_index] == Underscore))
            {
                Advance();
            }

            return _text.Substring(start, _index - start);
        }

        private string ReadNumber(int line, int column)
        {
            int start = _index;
            while (_index < _text.Length && char.IsDigit(_text[_index]))
            {
                Advance();
            }

            if (_index < _text.Length && _text[_index] == DecimalSeparator && _index + 1 < _text.Length && char.IsDigit(_text[_index + 1]))
            {
                Advance();
                while (_index < _text.Length && char.IsDigit(_text[_index]))
                {
                    Advance();
                }
            }

            if (_index < _text.Length && (char.IsLetter(_text[_index]) || _text[_index] == Underscore))
            {
                throw Unexpected("A number cannot be followed directly by a letter", line, column);
            }

            string text = _text.Substring(start, _index - start);
            if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal _))
            {
                throw Unexpected($"The number '{text}' is out of range", line, column);
            }

            return text;
        }

        private RuleExpressionToken ReadParameter(int line, int column)
        {
            Advance();
            if (_index >= _text.Length || !(char.IsLetter(_text[_index]) || _text[_index] == Underscore))
            {
                throw Unexpected("A parameter name must follow '@'", line, column);
            }

            return new RuleExpressionToken(RuleExpressionTokenKind.Parameter, ReadIdentifier(), line, column);
        }

        private string ReadString(char quote, int line, int column)
        {
            Advance();
            StringBuilder value = new StringBuilder();
            while (true)
            {
                if (_index >= _text.Length)
                {
                    throw Unexpected("Unterminated string literal", line, column);
                }

                char current = _text[_index];
                if (current == quote)
                {
                    Advance();
                    return value.ToString();
                }

                if (current == Escape)
                {
                    value.Append(ReadEscape(line, column));
                    continue;
                }

                value.Append(current);
                Advance();
            }
        }

        private char ReadEscape(int line, int column)
        {
            Advance();
            if (_index >= _text.Length)
            {
                throw Unexpected("Unterminated string literal", line, column);
            }

            char escaped = _text[_index];
            Advance();
            switch (escaped)
            {
                case SingleQuote:
                case DoubleQuote:
                case Escape:
                    return escaped;
                case 'n':
                    return '\n';
                case 'r':
                    return '\r';
                case 't':
                    return '\t';
                default:
                    throw Unexpected($"Unknown escape sequence '\\{escaped}'", _line, _column - 2);
            }
        }

        private RuleExpressionToken Single(RuleExpressionTokenKind kind, int line, int column)
        {
            string text = _text.Substring(_index, 1);
            Advance();
            return new RuleExpressionToken(kind, text, line, column);
        }

        private RuleExpressionToken Double(RuleExpressionTokenKind kind, int line, int column)
        {
            string text = _text.Substring(_index, 2);
            Advance();
            Advance();
            return new RuleExpressionToken(kind, text, line, column);
        }

        private bool PeekIs(char expected) => _index + 1 < _text.Length && _text[_index + 1] == expected;

        private void Advance()
        {
            if (_text[_index] == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }

            _index++;
        }

        private static RuleExpressionParseException Unexpected(string message, int line, int column) => new RuleExpressionParseException(message, line, column);
    }
}
