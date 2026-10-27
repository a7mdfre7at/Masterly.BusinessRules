namespace Masterly.BusinessRules
{
    /// <summary>
    /// The seam that lets a sibling package decide which rule type <see cref="AsyncBusinessRuleBuilder.Build"/> returns.
    /// A factory set with <see cref="AsyncBusinessRuleBuilder.WithRuleFactory"/> replaces the builder's condition: at
    /// <c>Build()</c> it receives a <see cref="BuiltRuleDefinition"/> holding the configured metadata, details and
    /// parameters (including the injected async provider), and returns the rule to hand back to the caller. The
    /// expression package uses it so that <c>WhenExpression(...).Build()</c> returns an <c>ExpressionAsyncBusinessRule</c>.
    /// </summary>
    public interface IAsyncBuiltRuleFactory
    {
        /// <summary>
        /// Creates the rule an async builder returns from <c>Build()</c>.
        /// </summary>
        /// <param name="definition">The snapshot of the builder's configuration.</param>
        /// <returns>The built rule; never null.</returns>
        IAsyncBusinessRule Create(BuiltRuleDefinition definition);
    }
}
