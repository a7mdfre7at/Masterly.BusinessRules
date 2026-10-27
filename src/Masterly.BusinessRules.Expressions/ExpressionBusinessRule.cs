using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// A synchronous rule whose broken condition is a <see cref="RuleExpression"/>. The expression needs a context to
    /// bind its identifiers, so the parameterless <see cref="IsBroken()"/> (and therefore <c>Check()</c>) throws
    /// <see cref="InvalidOperationException"/>; evaluate it with a context, through the checkers or under a composition
    /// that forwards the context. <see cref="Description"/> reports the expression text unless a description was configured.
    /// </summary>
    /// <remarks>
    /// Created either directly (<see cref="RuleExpression.ToRule"/> or the constructor) or by
    /// <c>BusinessRuleBuilder...WhenExpression(...).Build()</c>. A builder-built rule carries the builder's name,
    /// description, category, tags and violation details, and its <c>@name</c> parameters resolve through the
    /// builder's <c>WithParameter&lt;T&gt;</c> declarations first, then the provider under
    /// <see cref="RuleParameters.ContextKey"/>, then the context bag.
    /// </remarks>
    /// <example>
    /// <code>
    /// ExpressionBusinessRule rule = RuleExpression.Parse("amount > @maxOrderAmount").ToRule("ORDER.TOO_LARGE", "Order exceeds the limit", parameters);
    ///
    /// rule.Check(context);
    /// RuleExpressionExplanation why = rule.ExplainExpression(context);
    /// </code>
    /// </example>
    public sealed class ExpressionBusinessRule : BaseBusinessRule
    {
        /// <summary>
        /// The message of the <see cref="InvalidOperationException"/> thrown by the context-less <see cref="IsBroken()"/>.
        /// </summary>
        public const string ContextRequiredMessage = "An expression rule needs a BusinessRuleContext to bind its identifiers; call IsBroken(context) or Check(context).";

        private readonly string _code;
        private readonly string _message;
        private readonly RuleSeverity _severity;
        private readonly BuiltRuleDefinition? _definition;

        /// <summary>
        /// Creates the rule.
        /// </summary>
        /// <param name="expression">The broken-condition expression.</param>
        /// <param name="code">The rule code.</param>
        /// <param name="message">The violation message.</param>
        /// <param name="parameters">An optional provider for <c>@parameters</c>.</param>
        /// <param name="severity">The rule severity; <see cref="RuleSeverity.Error"/> by default.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="expression"/>, <paramref name="code"/> or <paramref name="message"/> is null.</exception>
        public ExpressionBusinessRule(RuleExpression expression, string code, string message, IRuleParameterProvider? parameters = null, RuleSeverity severity = RuleSeverity.Error)
        {
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            _code = code ?? throw new ArgumentNullException(nameof(code));
            _message = message ?? throw new ArgumentNullException(nameof(message));
            Parameters = parameters;
            _severity = severity;
        }

        /// <summary>
        /// Creates the rule from a builder's configuration (the <c>WhenExpression</c> path).
        /// </summary>
        internal ExpressionBusinessRule(RuleExpression expression, BuiltRuleDefinition definition)
        {
            Expression = expression;
            _definition = definition;
            _code = definition.Code;
            _message = definition.Message;
            _severity = definition.Severity;
        }

        /// <summary>
        /// Gets the broken-condition expression.
        /// </summary>
        public RuleExpression Expression { get; }

        /// <summary>
        /// Gets the provider for <c>@parameters</c>, or null when only context-stored providers, builder-declared
        /// parameters and the context bag are used.
        /// </summary>
        public IRuleParameterProvider? Parameters { get; }

        /// <inheritdoc />
        public override string Code => _code;

        /// <inheritdoc />
        public override string Message => _message;

        /// <inheritdoc />
        public override RuleSeverity Severity => _severity;

        /// <summary>
        /// Gets the builder-configured name, or the type name for a directly created rule.
        /// </summary>
        public override string Name => _definition != null ? _definition.Name : base.Name;

        /// <summary>
        /// Gets the builder-configured description when one was set; otherwise the expression text.
        /// </summary>
        public override string Description => _definition != null && _definition.Description.Length > 0 ? _definition.Description : Expression.Text;

        /// <summary>
        /// Gets the builder-configured category; empty for a directly created rule.
        /// </summary>
        public override string Category => _definition != null ? _definition.Category : base.Category;

        /// <summary>
        /// Gets the builder-configured tags; empty for a directly created rule.
        /// </summary>
        public override IReadOnlyList<string> Tags => _definition != null ? _definition.Tags : base.Tags;

        /// <summary>
        /// Not supported: the expression needs a context.
        /// </summary>
        /// <returns>Never returns.</returns>
        /// <exception cref="InvalidOperationException">Always thrown; see <see cref="ContextRequiredMessage"/>.</exception>
        public override bool IsBroken() => throw new InvalidOperationException(ContextRequiredMessage);

        /// <summary>
        /// Evaluates the expression against <paramref name="context"/>.
        /// </summary>
        /// <param name="context">The context whose entries the identifiers bind to.</param>
        /// <returns>True when the broken condition holds.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
        /// <exception cref="RuleExpressionBindingException">Thrown when an identifier, parameter or member cannot be bound.</exception>
        /// <exception cref="RuleExpressionEvaluationException">Thrown when an operator meets operands it cannot work on.</exception>
        public override bool IsBroken(BusinessRuleContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return Expression.EvaluateCore(context, CreateParameterSource(context));
        }

        /// <summary>
        /// Builds the broken result; a builder-built rule attaches the builder's violation details.
        /// </summary>
        /// <returns>The result.</returns>
        public override BusinessRuleResult Evaluate() => _definition != null ? _definition.CreateResult(this, null) : base.Evaluate();

        /// <summary>
        /// Builds the broken result for <paramref name="context"/>; a builder-built rule attaches the builder's violation details.
        /// </summary>
        /// <param name="context">The evaluation context.</param>
        /// <returns>The result.</returns>
        public override BusinessRuleResult Evaluate(BusinessRuleContext context) => _definition != null ? _definition.CreateResult(this, context) : base.Evaluate(context);

        /// <summary>
        /// Evaluates the expression once against <paramref name="context"/> and explains the verdict with the bound
        /// values. Named <c>ExplainExpression</c> so it does not shadow the core <c>Explain</c> extension that returns a <c>RuleExplanation</c> tree.
        /// </summary>
        /// <param name="context">The context whose entries the identifiers bind to.</param>
        /// <param name="redact">An optional delegate deciding how each bound value is displayed.</param>
        /// <returns>The explanation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
        public RuleExpressionExplanation ExplainExpression(BusinessRuleContext context, RuleExpressionRedactor? redact = null)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return Expression.ExplainCore(context, CreateParameterSource(context), redact);
        }

        private RuleExpressionParameterSource CreateParameterSource(BusinessRuleContext context)
        {
            if (_definition == null)
            {
                return RuleExpressionParameterSource.ForSync(context, null, Parameters);
            }

            return RuleExpressionParameterSource.ForSync(context, _definition.ResolveParameters(context), null);
        }
    }
}
