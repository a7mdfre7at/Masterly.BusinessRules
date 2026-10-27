namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// The built-in functions callable with method syntax on a value (<c>name.startsWith('A')</c>); the <c>/ci</c>
    /// suffix (<c>name.startsWith/ci('a')</c>) makes the comparison case-insensitive.
    /// </summary>
    internal enum ExpressionFunction
    {
        StartsWith,
        EndsWith,
        Contains,
    }
}
