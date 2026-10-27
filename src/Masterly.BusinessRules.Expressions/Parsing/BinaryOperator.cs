namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// The infix operators, grouped by family so the interpreter and the compiler share one dispatch.
    /// </summary>
    internal enum BinaryOperator
    {
        Or,
        And,
        Equal,
        NotEqual,
        EqualIgnoreCase,
        Less,
        LessOrEqual,
        Greater,
        GreaterOrEqual,
        Add,
        Subtract,
        Multiply,
        Divide,
        Modulo,
    }
}
