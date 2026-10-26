namespace Masterly.BusinessRules
{
    /// <summary>
    /// The quantifier a <see cref="GroupBusinessRule"/> or <see cref="GroupAsyncBusinessRule"/> applies to its members.
    /// A group counts how many members pass and compares that count against <see cref="GroupBusinessRule.Threshold"/>.
    /// </summary>
    public enum RuleGroupQuantifier
    {
        /// <summary>
        /// Every member must pass; the group is broken when any member is broken.
        /// </summary>
        All,

        /// <summary>
        /// At least one member must pass; the group is broken only when every member is broken.
        /// </summary>
        Any,

        /// <summary>
        /// At least the threshold number of members must pass.
        /// </summary>
        AtLeast,

        /// <summary>
        /// Exactly the threshold number of members must pass.
        /// </summary>
        Exactly
    }
}
