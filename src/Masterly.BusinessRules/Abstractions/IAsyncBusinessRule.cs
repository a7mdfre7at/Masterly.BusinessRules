using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Defines an asynchronous business rule that can be evaluated to determine if it is broken.
    /// </summary>
    public interface IAsyncBusinessRule
    {
        /// <summary>
        /// Gets the unique code identifying this rule.
        /// </summary>
        string Code { get; }

        /// <summary>
        /// Gets the human-readable message describing why this rule is broken.
        /// </summary>
        string Message { get; }

        /// <summary>
        /// Gets the severity level of this rule when broken. Defaults to <see cref="RuleSeverity.Error"/>.
        /// </summary>
        RuleSeverity Severity => RuleSeverity.Error;

        /// <summary>
        /// Gets the optional human-readable name for the rule. Defaults to the type name.
        /// </summary>
        string Name => GetType().Name;

        /// <summary>
        /// Gets the optional detailed description of what this rule validates. Defaults to empty string.
        /// </summary>
        string Description => string.Empty;

        /// <summary>
        /// Gets the optional category for grouping related rules. Defaults to empty string.
        /// </summary>
        string Category => string.Empty;

        /// <summary>
        /// Gets the optional tags for filtering and organizing rules. Defaults to empty collection.
        /// </summary>
        IReadOnlyList<string> Tags => Array.Empty<string>();

        /// <summary>
        /// Asynchronously evaluates whether the business rule is broken.
        /// </summary>
        /// <param name="context">The context containing runtime data for rule evaluation.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. True if the rule is broken; otherwise, false.</returns>
        Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously evaluates the rule and returns a result if broken.
        /// </summary>
        /// <param name="context">The context containing runtime data for rule evaluation.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task containing the <see cref="BusinessRuleResult"/> if broken; otherwise, null.</returns>
        Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously checks the rule and throws <see cref="BusinessRuleValidationException"/> if broken.
        /// </summary>
        /// <param name="context">The context containing runtime data for rule evaluation.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when the rule is broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default);
    }
}