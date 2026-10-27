using System;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// Thrown during evaluation when an operator meets operands it cannot work on: a relational comparison between a
    /// string and a number, arithmetic on a boolean, <c>!</c> on a non-boolean, <c>in</c> on something that is not a
    /// collection, or a division by zero. Mismatched operands of <c>==</c>/<c>!=</c> are never an error (they are simply
    /// not equal). <see cref="NodeText"/> is the offending sub-expression.
    /// </summary>
    public sealed class RuleExpressionEvaluationException : Exception
    {
        /// <summary>
        /// Creates the exception.
        /// </summary>
        /// <param name="nodeText">The source text of the sub-expression that failed.</param>
        /// <param name="message">What went wrong.</param>
        public RuleExpressionEvaluationException(string nodeText, string message)
            : this(nodeText, message, null)
        {
        }

        /// <summary>
        /// Creates the exception with an inner exception.
        /// </summary>
        /// <param name="nodeText">The source text of the sub-expression that failed.</param>
        /// <param name="message">What went wrong.</param>
        /// <param name="innerException">The underlying failure, if any.</param>
        public RuleExpressionEvaluationException(string nodeText, string message, Exception? innerException)
            : base($"{message} in '{nodeText}'.", innerException)
        {
            NodeText = nodeText;
        }

        /// <summary>
        /// Gets the source text of the sub-expression that failed.
        /// </summary>
        public string NodeText { get; }
    }
}
