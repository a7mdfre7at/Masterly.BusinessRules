using System;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// <c>WhenExpression</c> for the core builders: set a rule's broken condition from expression text. The expression is
    /// parsed when the method is called, so a malformed expression fails at build time. It is installed through the core
    /// <c>WithRuleFactory</c> seam, so <c>Build()</c> returns an <see cref="ExpressionBusinessRule"/> (or
    /// <see cref="ExpressionAsyncBusinessRule"/>) carrying the builder's metadata and violation details. The built rule
    /// evaluates through the builder's parameter machinery: a parameter declared with <c>WithParameter&lt;T&gt;(name, fallback)</c>
    /// and supplied by <c>WithParameters(provider)</c> is what <c>@name</c> binds to, before any provider stored on the
    /// context and before the context bag. Like every expression rule, the built rule needs a context: its
    /// parameterless <c>IsBroken()</c> throws <see cref="InvalidOperationException"/>. A later <c>When</c> call on
    /// the same builder replaces the expression.
    /// </summary>
    /// <example>
    /// <code>
    /// IBusinessRule rule = BusinessRuleBuilder.Create("ORDER.TOO_LARGE")   // an ExpressionBusinessRule at runtime
    ///     .WithMessage("Order exceeds the limit")
    ///     .WithParameters(parameters)
    ///     .WithParameter&lt;decimal&gt;("maxOrderAmount", fallback: 10_000m)
    ///     .WhenExpression("amount > @maxOrderAmount")
    ///     .Build();
    /// </code>
    /// </example>
    public static class RuleExpressionBuilderExtensions
    {
        /// <summary>
        /// Sets the broken condition from expression text parsed with <see cref="RuleExpressionOptions.Default"/>.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="expression">The condition text.</param>
        /// <returns>The builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="expression"/> is null.</exception>
        /// <exception cref="RuleExpressionParseException">Thrown when the text is not a valid expression.</exception>
        public static BusinessRuleBuilder WhenExpression(this BusinessRuleBuilder builder, string expression)
            => WhenExpression(builder, expression, RuleExpressionOptions.Default);

        /// <summary>
        /// Sets the broken condition from expression text parsed with <paramref name="options"/>.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="expression">The condition text.</param>
        /// <param name="options">The parse and evaluation options.</param>
        /// <returns>The builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/>, <paramref name="expression"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="RuleExpressionParseException">Thrown when the text is not a valid expression.</exception>
        public static BusinessRuleBuilder WhenExpression(this BusinessRuleBuilder builder, string expression, RuleExpressionOptions options)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return WhenExpression(builder, RuleExpression.Parse(expression, options));
        }

        /// <summary>
        /// Sets the broken condition from a parsed expression.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="expression">The parsed expression.</param>
        /// <returns>The builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="expression"/> is null.</exception>
        public static BusinessRuleBuilder WhenExpression(this BusinessRuleBuilder builder, RuleExpression expression)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            return builder.WithRuleFactory(new ExpressionRuleFactory(expression));
        }

        /// <summary>
        /// Sets the broken condition from expression text parsed with <see cref="RuleExpressionOptions.Default"/>.
        /// </summary>
        /// <param name="builder">The async builder.</param>
        /// <param name="expression">The condition text.</param>
        /// <returns>The builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="expression"/> is null.</exception>
        /// <exception cref="RuleExpressionParseException">Thrown when the text is not a valid expression.</exception>
        public static AsyncBusinessRuleBuilder WhenExpression(this AsyncBusinessRuleBuilder builder, string expression)
            => WhenExpression(builder, expression, RuleExpressionOptions.Default);

        /// <summary>
        /// Sets the broken condition from expression text parsed with <paramref name="options"/>.
        /// </summary>
        /// <param name="builder">The async builder.</param>
        /// <param name="expression">The condition text.</param>
        /// <param name="options">The parse and evaluation options.</param>
        /// <returns>The builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/>, <paramref name="expression"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="RuleExpressionParseException">Thrown when the text is not a valid expression.</exception>
        public static AsyncBusinessRuleBuilder WhenExpression(this AsyncBusinessRuleBuilder builder, string expression, RuleExpressionOptions options)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return WhenExpression(builder, RuleExpression.Parse(expression, options));
        }

        /// <summary>
        /// Sets the broken condition from a parsed expression. Declared parameters resolve through the builder's
        /// providers (async preferred); undeclared <c>@names</c> fall back to providers stored on the context and the context bag.
        /// </summary>
        /// <param name="builder">The async builder.</param>
        /// <param name="expression">The parsed expression.</param>
        /// <returns>The builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="expression"/> is null.</exception>
        public static AsyncBusinessRuleBuilder WhenExpression(this AsyncBusinessRuleBuilder builder, RuleExpression expression)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            return builder.WithRuleFactory(new ExpressionRuleFactory(expression));
        }
    }
}
