using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Defines a synchronous business rule that can be evaluated to determine if it is broken.
    /// </summary>
    public interface IBusinessRule
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
        /// Evaluates whether the business rule is broken.
        /// </summary>
        /// <returns>True if the rule is broken; otherwise, false.</returns>
        bool IsBroken();

        /// <summary>
        /// Evaluates whether the business rule is broken using the provided context.
        /// </summary>
        /// <param name="context">The context containing runtime data for rule evaluation.</param>
        /// <returns>True if the rule is broken; otherwise, false.</returns>
        bool IsBroken(BusinessRuleContext context);

        /// <summary>
        /// Checks the rule and throws <see cref="BusinessRuleValidationException"/> if broken.
        /// </summary>
        /// <exception cref="BusinessRuleValidationException">Thrown when the rule is broken.</exception>
        void Check();

        /// <summary>
        /// Checks the rule using the provided context and throws <see cref="BusinessRuleValidationException"/> if broken.
        /// </summary>
        /// <param name="context">The context containing runtime data for rule evaluation.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when the rule is broken.</exception>
        void Check(BusinessRuleContext context);

        /// <summary>
        /// Creates the result describing this rule as broken: its code, message, severity and metadata.
        /// This is a factory, not an evaluation; the library calls it only after <see cref="IsBroken()"/> returned true.
        /// </summary>
        /// <returns>A <see cref="BusinessRuleResult"/> representing the violation.</returns>
        BusinessRuleResult Evaluate();

        /// <summary>
        /// Creates the result describing this rule as broken using the provided context, so that context-aware
        /// details (<see cref="BusinessRuleResult.Detail"/>, <see cref="BusinessRuleResult.Actual"/>, ...) can be attached.
        /// The default implementation ignores the context and returns <see cref="Evaluate()"/>.
        /// This is a factory, not an evaluation; the library calls it only after <see cref="IsBroken(BusinessRuleContext)"/> returned true.
        /// </summary>
        /// <param name="context">The context the rule was evaluated with.</param>
        /// <returns>A <see cref="BusinessRuleResult"/> representing the violation.</returns>
        BusinessRuleResult Evaluate(BusinessRuleContext context) => Evaluate();
    }
}