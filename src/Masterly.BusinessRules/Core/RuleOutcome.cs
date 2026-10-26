namespace Masterly.BusinessRules
{
    /// <summary>
    /// Describes what happened to a single rule during an evaluation walk.
    /// Used by <see cref="RuleExplanation"/> to report the state of every node in an explanation tree.
    /// </summary>
    public enum RuleOutcome
    {
        /// <summary>
        /// The rule was evaluated and is not broken.
        /// </summary>
        Passed,

        /// <summary>
        /// The rule was evaluated and is broken.
        /// </summary>
        Broken,

        /// <summary>
        /// The rule was not evaluated because a precondition (for example a <see cref="ConditionalBusinessRule"/> condition) was not met.
        /// The rule counts as passed.
        /// </summary>
        Skipped,

        /// <summary>
        /// The rule was not evaluated because a cached verdict was served (see <see cref="CachedBusinessRule"/>).
        /// Whether the cached verdict is broken is reported by <see cref="RuleExplanation.IsBroken"/>.
        /// </summary>
        CacheHit,

        /// <summary>
        /// The rule was never evaluated because a composed parent short-circuited before reaching it.
        /// </summary>
        NotEvaluated,

        /// <summary>
        /// The rule threw an exception during evaluation. Only reported when an explanation is requested with
        /// <c>captureExceptions: true</c>; the faulted rule is treated as broken for aggregation purposes.
        /// </summary>
        Faulted
    }
}
