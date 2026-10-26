namespace Masterly.BusinessRules
{
    /// <summary>
    /// Identifies how a composed rule (<see cref="IComposedBusinessRule"/> / <see cref="IComposedAsyncBusinessRule"/>)
    /// combines the broken state of its operands.
    /// </summary>
    public enum RuleOperator
    {
        /// <summary>
        /// Binary legacy operator: broken only when both operands are broken (<see cref="AndBusinessRule"/>).
        /// </summary>
        And,

        /// <summary>
        /// Binary legacy operator: broken when either operand is broken (<see cref="OrBusinessRule"/>).
        /// </summary>
        Or,

        /// <summary>
        /// Unary operator: inverts the broken state of its single operand (<see cref="NotBusinessRule"/>).
        /// </summary>
        Not,

        /// <summary>
        /// N-ary quantifier: broken when any member is broken, i.e. every member must pass
        /// (<see cref="RuleGroup.All(string, string, IBusinessRule[])"/>, <see cref="CompositeBusinessRule"/>).
        /// </summary>
        All,

        /// <summary>
        /// N-ary quantifier: broken only when every member is broken, i.e. at least one member must pass
        /// (<see cref="RuleGroup.Any(string, string, IBusinessRule[])"/>).
        /// </summary>
        Any,

        /// <summary>
        /// N-ary quantifier: broken when fewer than the required number of members pass
        /// (<see cref="RuleGroup.AtLeast(int, string, string, IBusinessRule[])"/>).
        /// </summary>
        AtLeast,

        /// <summary>
        /// N-ary quantifier: broken unless exactly the required number of members pass
        /// (<see cref="RuleGroup.Exactly(int, string, string, IBusinessRule[])"/>).
        /// </summary>
        Exactly
    }
}
