using System;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// Thrown by <see cref="RuleExpression.Parse(string)"/> when the text is not a valid rule expression: a malformed
    /// token, an unexpected token, an unknown function, a parse limit (<see cref="RuleExpressionOptions.MaxLength"/>,
    /// <see cref="RuleExpressionOptions.MaxDepth"/>) or, on the typed <see cref="RuleExpression.Compile{T}()"/> path, a
    /// member that does not exist on the subject type. <see cref="Line"/> and <see cref="Column"/> are 1-based and point at
    /// the offending token.
    /// </summary>
    /// <example>
    /// <code>
    /// RuleExpressionParseException error = Assert.Throws&lt;RuleExpressionParseException&gt;(() => RuleExpression.Parse("amount >"));
    /// // error.Line == 1, error.Column == 9
    /// </code>
    /// </example>
    public sealed class RuleExpressionParseException : Exception
    {
        /// <summary>
        /// Creates the exception.
        /// </summary>
        /// <param name="message">The reason the text could not be parsed.</param>
        /// <param name="line">The 1-based line of the offending token.</param>
        /// <param name="column">The 1-based column of the offending token.</param>
        public RuleExpressionParseException(string message, int line, int column)
            : base(FormatMessage(message, line, column))
        {
            Line = line;
            Column = column;
        }

        /// <summary>
        /// Gets the 1-based line of the offending token.
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// Gets the 1-based column of the offending token.
        /// </summary>
        public int Column { get; }

        private static string FormatMessage(string message, int line, int column) => $"{message} (line {line}, column {column})";
    }
}
