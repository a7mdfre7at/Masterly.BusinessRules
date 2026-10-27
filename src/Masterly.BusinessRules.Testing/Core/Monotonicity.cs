namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// The direction in which a rule's broken region lies on an ordered input, as proven by
    /// <see cref="RuleProperties{T}.MonotoneIn(Monotonicity, System.Collections.Generic.IComparer{T})"/> and reported by
    /// <see cref="RuleBoundary{T}.Monotonicity"/>.
    /// </summary>
    public enum Monotonicity
    {
        /// <summary>
        /// Once the rule is broken for a value it is broken for every larger value: a limit or maximum rule
        /// (for example "amount must not exceed 10 000").
        /// </summary>
        BrokenAbove,

        /// <summary>
        /// Once the rule is broken for a value it is broken for every smaller value: a minimum rule
        /// (for example "customer must be at least 18").
        /// </summary>
        BrokenBelow
    }
}
