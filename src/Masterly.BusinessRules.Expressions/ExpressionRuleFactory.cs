namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// The core builder seam behind <c>WhenExpression</c>: makes <c>Build()</c> return an <see cref="ExpressionBusinessRule"/>
    /// or <see cref="ExpressionAsyncBusinessRule"/> carrying the builder's configuration.
    /// </summary>
    internal sealed class ExpressionRuleFactory : IBuiltRuleFactory, IAsyncBuiltRuleFactory
    {
        private readonly RuleExpression _expression;

        /// <summary>
        /// Creates the factory for one parsed expression.
        /// </summary>
        public ExpressionRuleFactory(RuleExpression expression)
        {
            _expression = expression;
        }

        /// <summary>
        /// Creates the synchronous expression rule.
        /// </summary>
        IBusinessRule IBuiltRuleFactory.Create(BuiltRuleDefinition definition) => new ExpressionBusinessRule(_expression, definition);

        /// <summary>
        /// Creates the asynchronous expression rule.
        /// </summary>
        IAsyncBusinessRule IAsyncBuiltRuleFactory.Create(BuiltRuleDefinition definition) => new ExpressionAsyncBusinessRule(_expression, definition);
    }
}
