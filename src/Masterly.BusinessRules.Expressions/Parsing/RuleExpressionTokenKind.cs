namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// The kinds of token the tokenizer produces. Word operators (<c>and</c>, <c>or</c>, <c>not</c>, <c>in</c>) and the
    /// literal keywords (<c>true</c>, <c>false</c>, <c>null</c>) are reported as <see cref="Identifier"/> and recognised by the parser.
    /// </summary>
    internal enum RuleExpressionTokenKind
    {
        End,
        Identifier,
        Parameter,
        Number,
        String,
        LeftParenthesis,
        RightParenthesis,
        LeftBracket,
        RightBracket,
        Comma,
        Dot,
        Not,
        And,
        Or,
        Equal,
        NotEqual,
        EqualIgnoreCase,
        Less,
        LessOrEqual,
        Greater,
        GreaterOrEqual,
        Plus,
        Minus,
        Star,
        Slash,
        Percent,
    }
}
