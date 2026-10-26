using System;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Thrown by <see cref="BusinessRuleChecker"/> and <see cref="AsyncBusinessRuleChecker"/> when a rule in the batch
    /// fails to read the context (<see cref="ContextKeyNotFoundException"/> or <see cref="ContextValueTypeMismatchException"/>).
    /// The message names the failing rule's <c>Code</c> and repeats the context error; the original exception is preserved as
    /// <see cref="Exception.InnerException"/>. Under <see cref="FaultPolicy.Record"/> and <see cref="FaultPolicy.TreatAsBroken"/>
    /// the wrapped exception is what the ledger entry's <see cref="RuleEvaluationEntry.Fault"/> and the observer receive.
    /// </summary>
    /// <example>
    /// <code>
    /// // Rule LIMIT.EXCEEDED failed to read context: Context has no key 'limit'. Available keys: order
    /// </code>
    /// </example>
    public sealed class BusinessRuleContextException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessRuleContextException"/> class.
        /// </summary>
        /// <param name="ruleCode">The <c>Code</c> of the rule whose context read failed.</param>
        /// <param name="key">The context key the rule tried to read.</param>
        /// <param name="innerException">The context exception that was thrown.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public BusinessRuleContextException(string ruleCode, string key, Exception innerException)
            : base(FormatMessage(ruleCode, key, innerException), innerException)
        {
            RuleCode = ruleCode;
            Key = key;
        }

        /// <summary>
        /// Gets the <c>Code</c> of the rule whose context read failed.
        /// </summary>
        public string RuleCode { get; }

        /// <summary>
        /// Gets the context key the rule tried to read.
        /// </summary>
        public string Key { get; }

        private static string FormatMessage(string ruleCode, string key, Exception innerException)
        {
            if (ruleCode == null)
            {
                throw new ArgumentNullException(nameof(ruleCode));
            }

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (innerException == null)
            {
                throw new ArgumentNullException(nameof(innerException));
            }

            return $"Rule {ruleCode} failed to read context: {innerException.Message}";
        }
    }
}
