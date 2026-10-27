namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// What a <see cref="RuleExpressionStep"/> records.
    /// </summary>
    public enum RuleExpressionStepKind
    {
        /// <summary>
        /// A value path (identifier, parameter or member chain) was bound to a value.
        /// </summary>
        Binding,

        /// <summary>
        /// An operator, function call or membership test produced a result.
        /// </summary>
        Operation,
    }
}
