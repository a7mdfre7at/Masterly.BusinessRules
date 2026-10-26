using System;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Thrown when a rule is wired up in a way that cannot be evaluated: a feature-gated rule that has no
    /// <see cref="IRuleFeatureFlagProvider"/> (neither injected nor supplied through <see cref="RuleFeatureFlags.ContextKey"/>),
    /// or a partial rollout without a stable key selector. The message names the rule's <c>Code</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// // Rule CREDIT.SCORE is gated by flag 'credit-score-v2' but no feature flag provider was supplied: ...
    /// </code>
    /// </example>
    public class RuleConfigurationException : InvalidOperationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RuleConfigurationException"/> class.
        /// </summary>
        /// <param name="ruleCode">The <c>Code</c> of the misconfigured rule.</param>
        /// <param name="message">The message describing the misconfiguration.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="ruleCode"/> or <paramref name="message"/> is null.</exception>
        public RuleConfigurationException(string ruleCode, string message)
            : this(ruleCode, message, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleConfigurationException"/> class with an inner exception.
        /// </summary>
        /// <param name="ruleCode">The <c>Code</c> of the misconfigured rule.</param>
        /// <param name="message">The message describing the misconfiguration.</param>
        /// <param name="innerException">The exception that revealed the misconfiguration, or null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="ruleCode"/> or <paramref name="message"/> is null.</exception>
        public RuleConfigurationException(string ruleCode, string message, Exception? innerException)
            : base(message ?? throw new ArgumentNullException(nameof(message)), innerException)
        {
            RuleCode = ruleCode ?? throw new ArgumentNullException(nameof(ruleCode));
        }

        /// <summary>
        /// Gets the <c>Code</c> of the misconfigured rule.
        /// </summary>
        public string RuleCode { get; }
    }
}
