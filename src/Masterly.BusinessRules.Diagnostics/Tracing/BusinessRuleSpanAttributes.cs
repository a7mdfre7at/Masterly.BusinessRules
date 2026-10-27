namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// The attribute, event and tag names written on the spans started from <see cref="BusinessRulesActivitySource"/>.
    /// </summary>
    public static class BusinessRuleSpanAttributes
    {
        /// <summary>
        /// The rule code, present only when the <see cref="TracingRuleObserverOptions.CodeTagSelector"/> returned one.
        /// </summary>
        public const string Code = "business_rule.code";

        /// <summary>
        /// The rule name.
        /// </summary>
        public const string Name = "business_rule.name";

        /// <summary>
        /// The rule category (an empty string for an uncategorised rule).
        /// </summary>
        public const string Category = "business_rule.category";

        /// <summary>
        /// The rule severity as one of <see cref="RuleSeverityTags"/>.
        /// </summary>
        public const string Severity = "business_rule.severity";

        /// <summary>
        /// The evaluation outcome as one of <see cref="RuleOutcomeTags"/>.
        /// </summary>
        public const string Outcome = "business_rule.outcome";

        /// <summary>
        /// The broken-rule message, written on the <see cref="BrokenEventName"/> and <see cref="ShadowViolationEventName"/>
        /// events only when <see cref="TracingRuleObserverOptions.MessageAsEvent"/> is true.
        /// </summary>
        public const string Message = "business_rule.message";

        /// <summary>
        /// The name of the event added to a rule span when the rule is broken.
        /// </summary>
        public const string BrokenEventName = "business_rule.broken";

        /// <summary>
        /// The name of the event added to the enclosing span when a shadow-mode gated rule observed a violation.
        /// </summary>
        public const string ShadowViolationEventName = "business_rule.shadow_violation";

        /// <summary>
        /// The OpenTelemetry exception event name added to a faulted rule span.
        /// </summary>
        public const string ExceptionEventName = "exception";

        /// <summary>
        /// The OpenTelemetry exception type attribute on the <see cref="ExceptionEventName"/> event.
        /// </summary>
        public const string ExceptionType = "exception.type";

        /// <summary>
        /// The OpenTelemetry exception message attribute on the <see cref="ExceptionEventName"/> event.
        /// </summary>
        public const string ExceptionMessage = "exception.message";

        /// <summary>
        /// The number of rules submitted to a batch, written on the batch span when it starts.
        /// </summary>
        public const string BatchRuleCount = "business_rules.batch.rule_count";

        /// <summary>
        /// The number of broken rules of a batch, written on the batch span when it completes.
        /// </summary>
        public const string BatchBrokenCount = "business_rules.batch.broken_count";

        /// <summary>
        /// The number of faulted rules of a batch, written on the batch span when it completes.
        /// </summary>
        public const string BatchFaultedCount = "business_rules.batch.faulted_count";

        /// <summary>
        /// The batch outcome as one of <see cref="RuleOutcomeTags"/> (see <see cref="RuleOutcomeTags.ForBatch"/>).
        /// </summary>
        public const string BatchOutcome = "business_rules.batch.outcome";
    }
}
