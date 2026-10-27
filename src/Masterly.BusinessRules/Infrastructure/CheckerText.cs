namespace Masterly.BusinessRules
{
    /// <summary>
    /// The parameter names and messages the checkers, the subject binding and the shadow sink share.
    /// </summary>
    internal static class CheckerText
    {
        /// <summary>
        /// The name of the rules parameter every checker overload takes, reported by argument exceptions.
        /// </summary>
        public const string RulesParameterName = "rules";

        /// <summary>
        /// The message of a shadow violation that carries a rule of neither hierarchy.
        /// </summary>
        public const string ShadowViolationWithoutRule = "The shadow violation carries no rule.";

        /// <summary>
        /// Returns the message of the argument exception thrown for a null rule in a batch.
        /// </summary>
        /// <param name="position">The zero-based position of the null rule.</param>
        /// <returns>The message.</returns>
        public static string NullRuleAt(int position)
        {
            return $"The rule at position {position} is null.";
        }
    }
}
