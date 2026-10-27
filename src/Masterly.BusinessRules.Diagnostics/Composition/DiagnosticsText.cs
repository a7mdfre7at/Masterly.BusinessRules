namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// The labels the <c>Traced()</c> decorators contribute to explanation trees.
    /// </summary>
    public static class DiagnosticsText
    {
        /// <summary>
        /// The operator of a <see cref="DiagnosticsBusinessRule"/> / <see cref="DiagnosticsAsyncBusinessRule"/> node in an
        /// explanation tree; its single child is the explanation of the traced rule.
        /// </summary>
        public const string TracedOperator = "TRACED";
    }
}
