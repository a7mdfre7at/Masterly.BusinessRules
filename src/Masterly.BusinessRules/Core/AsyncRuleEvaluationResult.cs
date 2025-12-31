using System.Collections.Generic;
using System.Linq;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Represents the result of evaluating a collection of asynchronous business rules.
    /// Provides access to both broken and passed rules, along with filtering capabilities.
    /// </summary>
    public sealed class AsyncRuleEvaluationResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncRuleEvaluationResult"/> class.
        /// </summary>
        /// <param name="brokenRules">The collection of broken rule results.</param>
        /// <param name="passedRules">The collection of async rules that passed evaluation.</param>
        public AsyncRuleEvaluationResult(
            IEnumerable<BusinessRuleResult> brokenRules,
            IEnumerable<IAsyncBusinessRule> passedRules)
        {
            BrokenRules = brokenRules.ToList().AsReadOnly();
            PassedRules = passedRules.ToList().AsReadOnly();
        }

        /// <summary>
        /// Gets all broken rule results.
        /// </summary>
        public IReadOnlyCollection<BusinessRuleResult> BrokenRules { get; }

        /// <summary>
        /// Gets all rules that passed evaluation.
        /// </summary>
        public IReadOnlyCollection<IAsyncBusinessRule> PassedRules { get; }

        /// <summary>
        /// Returns true if any rules were broken.
        /// </summary>
        public bool HasBrokenRules => BrokenRules.Count > 0;

        /// <summary>
        /// Returns true if all rules passed.
        /// </summary>
        public bool AllPassed => BrokenRules.Count == 0;

        /// <summary>
        /// Gets broken rules filtered by the specified severity level.
        /// </summary>
        /// <param name="severity">The severity level to filter by.</param>
        /// <returns>An enumerable of broken rule results matching the specified severity.</returns>
        public IEnumerable<BusinessRuleResult> GetBySeverity(RuleSeverity severity)
            => BrokenRules.Where(r => r.Severity == severity);

        /// <summary>
        /// Gets only error-level broken rules.
        /// </summary>
        public IEnumerable<BusinessRuleResult> Errors
            => GetBySeverity(RuleSeverity.Error);

        /// <summary>
        /// Gets only warning-level broken rules.
        /// </summary>
        public IEnumerable<BusinessRuleResult> Warnings
            => GetBySeverity(RuleSeverity.Warning);

        /// <summary>
        /// Gets only info-level broken rules.
        /// </summary>
        public IEnumerable<BusinessRuleResult> Infos
            => GetBySeverity(RuleSeverity.Info);

        /// <summary>
        /// Returns true if there are any error-level broken rules.
        /// </summary>
        public bool HasErrors => Errors.Any();

        /// <summary>
        /// Returns true if there are any warning-level broken rules.
        /// </summary>
        public bool HasWarnings => Warnings.Any();

        /// <summary>
        /// Throws a <see cref="BusinessRuleValidationException"/> if any rules are broken.
        /// </summary>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        public void ThrowIfBroken()
        {
            if (HasBrokenRules)
                throw new BusinessRuleValidationException(BrokenRules);
        }

        /// <summary>
        /// Throws a <see cref="BusinessRuleValidationException"/> only if there are error-level broken rules.
        /// Warnings and info-level violations are ignored.
        /// </summary>
        /// <exception cref="BusinessRuleValidationException">Thrown when there are error-level broken rules.</exception>
        public void ThrowIfErrors()
        {
            List<BusinessRuleResult> errors = Errors.ToList();
            if (errors.Any())
                throw new BusinessRuleValidationException(errors);
        }
    }
}
