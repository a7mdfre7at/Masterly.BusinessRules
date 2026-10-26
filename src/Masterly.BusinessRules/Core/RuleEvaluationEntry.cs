using System;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// One line of the evaluation ledger produced by <see cref="BusinessRuleChecker"/> and
    /// <see cref="AsyncBusinessRuleChecker"/>: which rule was evaluated at which position, what happened to it,
    /// how long it took and, when it faulted, the exception.
    /// Exactly one of <see cref="Rule"/> (sync batches) and <see cref="AsyncRule"/> (async batches) is set.
    /// </summary>
    public sealed class RuleEvaluationEntry
    {
        /// <summary>
        /// Initializes an entry for a synchronous rule.
        /// </summary>
        /// <param name="order">The zero-based position of the rule in the submitted batch.</param>
        /// <param name="rule">The evaluated rule.</param>
        /// <param name="outcome">What happened to the rule.</param>
        /// <param name="result">The broken result, or null when the rule did not produce one.</param>
        /// <param name="fault">The exception the rule threw, or null.</param>
        /// <param name="duration">The time spent evaluating the rule.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="order"/> is negative.</exception>
        public RuleEvaluationEntry(int order, IBusinessRule rule, RuleOutcome outcome, BusinessRuleResult? result, Exception? fault, TimeSpan duration)
            : this(order, outcome, result, fault, duration)
        {
            Rule = rule ?? throw new ArgumentNullException(nameof(rule));
            Code = rule.Code;
        }

        /// <summary>
        /// Initializes an entry for an asynchronous rule.
        /// </summary>
        /// <param name="order">The zero-based position of the rule in the submitted batch.</param>
        /// <param name="rule">The evaluated async rule.</param>
        /// <param name="outcome">What happened to the rule.</param>
        /// <param name="result">The broken result, or null when the rule did not produce one.</param>
        /// <param name="fault">The exception the rule threw, or null.</param>
        /// <param name="duration">The time spent evaluating the rule.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="order"/> is negative.</exception>
        public RuleEvaluationEntry(int order, IAsyncBusinessRule rule, RuleOutcome outcome, BusinessRuleResult? result, Exception? fault, TimeSpan duration)
            : this(order, outcome, result, fault, duration)
        {
            AsyncRule = rule ?? throw new ArgumentNullException(nameof(rule));
            Code = rule.Code;
        }

        private RuleEvaluationEntry(int order, RuleOutcome outcome, BusinessRuleResult? result, Exception? fault, TimeSpan duration)
        {
            if (order < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(order), order, "The order of a ledger entry cannot be negative.");
            }

            Order = order;
            Outcome = outcome;
            Result = result;
            Fault = fault;
            Duration = duration;
            Code = string.Empty;
        }

        /// <summary>
        /// Gets the zero-based position of the rule in the submitted batch.
        /// </summary>
        public int Order { get; }

        /// <summary>
        /// Gets the evaluated synchronous rule, or null when this entry belongs to an async batch.
        /// </summary>
        public IBusinessRule? Rule { get; }

        /// <summary>
        /// Gets the evaluated asynchronous rule, or null when this entry belongs to a sync batch.
        /// </summary>
        public IAsyncBusinessRule? AsyncRule { get; }

        /// <summary>
        /// Gets the code of the evaluated rule.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets what happened to the rule: <see cref="RuleOutcome.Passed"/>, <see cref="RuleOutcome.Broken"/>,
        /// <see cref="RuleOutcome.Faulted"/>, or <see cref="RuleOutcome.NotEvaluated"/> when a fail-fast batch stopped before it.
        /// </summary>
        public RuleOutcome Outcome { get; }

        /// <summary>
        /// Gets the broken result, or null when the rule passed, was not evaluated, or faulted under <see cref="FaultPolicy.Record"/>.
        /// A faulted rule under <see cref="FaultPolicy.TreatAsBroken"/> carries the synthesised broken result.
        /// </summary>
        public BusinessRuleResult? Result { get; }

        /// <summary>
        /// Gets the exception the rule threw, or null when it did not fault.
        /// </summary>
        public Exception? Fault { get; }

        /// <summary>
        /// Gets the wall-clock time spent evaluating the rule. Zero for a rule that was not evaluated.
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Gets whether the rule contributed a broken result to the batch.
        /// </summary>
        public bool IsBroken => Result != null;

        /// <summary>
        /// Gets whether the rule threw during evaluation.
        /// </summary>
        public bool IsFaulted => Fault != null;
    }
}
