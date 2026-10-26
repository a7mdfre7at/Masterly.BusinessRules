namespace Masterly.BusinessRules
{
    /// <summary>
    /// Describes what happened to a single rule during an evaluation walk.
    /// Used by <see cref="RuleExplanation"/> to report the state of every node in an explanation tree and by
    /// <see cref="RuleEvaluationEntry"/> to report the state of every rule in a batch ledger.
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
        /// The rule was never evaluated because a composed parent short-circuited before reaching it, or because a
        /// fail-fast batch stopped before reaching it.
        /// </summary>
        NotEvaluated,

        /// <summary>
        /// The rule threw an exception during evaluation. Reported by an explanation requested with
        /// <c>captureExceptions: true</c> (where the faulted rule is treated as broken) and by the batch checkers'
        /// evaluation ledger under <see cref="FaultPolicy.Record"/> or <see cref="FaultPolicy.TreatAsBroken"/>.
        /// </summary>
        Faulted
    }
}
