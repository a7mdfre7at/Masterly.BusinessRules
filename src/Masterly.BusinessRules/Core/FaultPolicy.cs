namespace Masterly.BusinessRules
{
    /// <summary>
    /// Defines what a batch checker does when a rule throws an exception during evaluation.
    /// There is deliberately no "treat as passed" policy: a fault is never silently turned into a pass.
    /// Cancellation (<see cref="System.OperationCanceledException"/>) is never subject to a fault policy and always propagates.
    /// </summary>
    public enum FaultPolicy
    {
        /// <summary>
        /// The exception propagates out of the checker after <c>OnRuleFaulted</c> has been raised on the observer.
        /// The batch is abandoned and no result or ledger is produced. This is the default and the pre-3.0 behaviour.
        /// </summary>
        Throw,

        /// <summary>
        /// The fault is recorded in the ledger as a <see cref="RuleOutcome.Faulted"/> entry carrying the exception,
        /// the rule is counted as neither broken nor passed and the batch continues. Faults are visible through
        /// <c>result.Faulted</c>/<c>result.HasFaults</c>, <c>exception.Entries</c> and the observer.
        /// </summary>
        Record,

        /// <summary>
        /// The fault is recorded as in <see cref="Record"/> and, in addition, a broken result built from the rule's
        /// <c>Code</c>, <c>Message</c> and <c>Severity</c> is added to the broken rules, so the fault blocks like a violation.
        /// </summary>
        TreatAsBroken
    }
}
