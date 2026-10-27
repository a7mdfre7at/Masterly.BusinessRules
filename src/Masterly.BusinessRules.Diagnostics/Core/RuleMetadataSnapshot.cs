using System;

namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// The identity of a rule read once for a diagnostic record. Every getter is guarded: a metadata getter that throws
    /// (the same case the core's <c>TreatAsBroken</c> stand-in guards against) degrades to the rule's type name, an
    /// empty category or <see cref="RuleSeverity.Error"/> instead of masking the evaluation that is being reported.
    /// <see cref="IBusinessRuleMetadata.Message"/> is deliberately not part of the snapshot: it may carry domain data
    /// and is read only where an option asks for it.
    /// </summary>
    internal readonly struct RuleMetadataSnapshot
    {
        private RuleMetadataSnapshot(string code, string name, string category, RuleSeverity severity)
        {
            Code = code;
            Name = name;
            Category = category;
            Severity = severity;
        }

        /// <summary>
        /// Gets the rule code, or the rule's type name when the getter threw.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the rule name, or the rule's type name when the getter threw.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the rule category, or an empty string when the getter threw.
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// Gets the rule severity, or <see cref="RuleSeverity.Error"/> when the getter threw.
        /// </summary>
        public RuleSeverity Severity { get; }

        /// <summary>
        /// Reads the four identity members of <paramref name="rule"/>.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <returns>The snapshot.</returns>
        public static RuleMetadataSnapshot Capture(IBusinessRuleMetadata rule)
        {
            string typeName = rule.GetType().Name;
            string code = Read(() => rule.Code, typeName);
            string name = Read(() => rule.Name, typeName);
            string category = Read(() => rule.Category, string.Empty);
            RuleSeverity severity = ReadSeverity(rule);
            return new RuleMetadataSnapshot(code, name, category, severity);
        }

        /// <summary>
        /// Reads <see cref="IBusinessRuleMetadata.Message"/>, degrading to an empty string when the getter throws.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <returns>The message.</returns>
        public static string ReadMessage(IBusinessRuleMetadata rule)
        {
            return Read(() => rule.Message, string.Empty);
        }

        private static string Read(Func<string> getter, string fallback)
        {
            try
            {
                return getter() ?? fallback;
            }
            catch (Exception)
            {
                // A throwing metadata getter must not hide the evaluation being reported; the fault itself is
                // reported by the checker through OnRuleFaulted with the original exception.
                return fallback;
            }
        }

        private static RuleSeverity ReadSeverity(IBusinessRuleMetadata rule)
        {
            try
            {
                return rule.Severity;
            }
            catch (Exception)
            {
                return RuleSeverity.Error;
            }
        }
    }
}
