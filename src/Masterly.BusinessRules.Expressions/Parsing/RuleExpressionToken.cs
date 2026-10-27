namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// One token of an expression: its kind, its source text (the unescaped value for a string, the name without
    /// <c>@</c> for a parameter) and its 1-based position.
    /// </summary>
    internal readonly struct RuleExpressionToken
    {
        public RuleExpressionToken(RuleExpressionTokenKind kind, string text, int line, int column)
        {
            Kind = kind;
            Text = text;
            Line = line;
            Column = column;
        }

        public RuleExpressionTokenKind Kind { get; }

        public string Text { get; }

        public int Line { get; }

        public int Column { get; }
    }
}
