using Microsoft.Extensions.Logging;

namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// The <see cref="EventId"/>s of the log messages written by this package, for filtering and alerting.
    /// </summary>
    public static class RuleLogEvents
    {
        /// <summary>
        /// A rule was evaluated and is broken (fields <c>RuleCode</c>, <c>RuleName</c>, <c>Category</c>, <c>Severity</c>,
        /// <c>Outcome</c>, <c>ElapsedMs</c> and, unless disabled, <c>Message</c>).
        /// </summary>
        public static readonly EventId RuleBroken = new EventId(1, nameof(RuleBroken));

        /// <summary>
        /// A rule was evaluated and passed.
        /// </summary>
        public static readonly EventId RulePassed = new EventId(2, nameof(RulePassed));

        /// <summary>
        /// A rule threw during evaluation; the exception is attached.
        /// </summary>
        public static readonly EventId RuleFaulted = new EventId(3, nameof(RuleFaulted));

        /// <summary>
        /// A checker batch started (<c>RuleCount</c>).
        /// </summary>
        public static readonly EventId BatchStarted = new EventId(4, nameof(BatchStarted));

        /// <summary>
        /// A checker batch completed (<c>RuleCount</c>, <c>BrokenCount</c>, <c>FaultedCount</c>, <c>PassedCount</c>, <c>ElapsedMs</c>, <c>Outcome</c>).
        /// </summary>
        public static readonly EventId BatchCompleted = new EventId(5, nameof(BatchCompleted));

        /// <summary>
        /// A shadow-mode gated rule observed a violation it did not report as broken.
        /// </summary>
        public static readonly EventId ShadowViolation = new EventId(6, nameof(ShadowViolation));
    }
}
