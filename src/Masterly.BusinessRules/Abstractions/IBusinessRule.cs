namespace Masterly.BusinessRules
{
    /// <summary>
    /// Defines a synchronous business rule that can be evaluated to determine if it is broken.
    /// The metadata members (<c>Code</c>, <c>Message</c>, <c>Severity</c>, <c>Name</c>, <c>Description</c>,
    /// <c>Category</c>, <c>Tags</c>) come from <see cref="IBusinessRuleMetadata"/>.
    /// </summary>
    public interface IBusinessRule : IBusinessRuleMetadata
    {
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
