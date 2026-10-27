namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// Decides how a bound value is shown in an explanation. Receives the path text of the binding (<c>amount</c>,
    /// <c>customer.Ssn</c>, <c>@limit</c>) and the raw value; returns the text to display (null displays as <c>null</c>).
    /// Use it to mask PII before an explanation is logged or shown to a user.
    /// </summary>
    /// <param name="name">The path text of the bound node.</param>
    /// <param name="value">The raw bound value.</param>
    /// <returns>The text to display in place of the value.</returns>
    /// <example>
    /// <code>
    /// RuleExpressionExplanation why = expression.Explain(context, redact: (name, value) => name == "customer.Ssn" ? "***" : value?.ToString());
    /// </code>
    /// </example>
    public delegate string? RuleExpressionRedactor(string name, object? value);
}
