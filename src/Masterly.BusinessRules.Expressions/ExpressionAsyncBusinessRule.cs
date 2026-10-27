using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// An asynchronous rule whose broken condition is a <see cref="RuleExpression"/>; the async twin of
    /// <see cref="ExpressionBusinessRule"/>. <c>@parameters</c> resolve through asynchronous providers first
    /// (<see cref="RuleParameters.AsyncContextKey"/>, then <see cref="AsyncParameters"/>) and synchronous ones second
    /// (<see cref="RuleParameters.ContextKey"/>, then <see cref="Parameters"/>) before the context bag. The interpreter
    /// itself is synchronous: parameter values are resolved up front, then the tree is walked once.
    /// </summary>
    /// <remarks>
    /// Created either directly (<see cref="RuleExpression.ToAsyncRule"/> or the constructor) or by
    /// <c>AsyncBusinessRuleBuilder...WhenExpression(...).Build()</c>. A builder-built rule carries the builder's name,
    /// description, category, tags and violation details, and its <c>@name</c> parameters resolve through the
    /// builder's <c>WithParameter&lt;T&gt;</c> declarations (and its injected providers) first.
    /// </remarks>
    /// <example>
    /// <code>
    /// ExpressionAsyncBusinessRule rule = RuleExpression.Parse("amount > @maxOrderAmount").ToAsyncRule("ORDER.TOO_LARGE", "Order exceeds the limit", remoteParameters);
    ///
    /// await rule.CheckAsync(context, ct);
    /// RuleExpressionExplanation why = await rule.ExplainExpressionAsync(context, cancellationToken: ct);
    /// </code>
    /// </example>
    public sealed class ExpressionAsyncBusinessRule : BaseAsyncBusinessRule
    {
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
        /// <param name="asyncParameters">An optional asynchronous provider for <c>@parameters</c>.</param>
        /// <param name="parameters">An optional synchronous provider for <c>@parameters</c>.</param>
        /// <param name="severity">The rule severity; <see cref="RuleSeverity.Error"/> by default.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="expression"/>, <paramref name="code"/> or <paramref name="message"/> is null.</exception>
        public ExpressionAsyncBusinessRule(
            RuleExpression expression,
            string code,
            string message,
            IAsyncRuleParameterProvider? asyncParameters = null,
            IRuleParameterProvider? parameters = null,
            RuleSeverity severity = RuleSeverity.Error)
        {
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            _code = code ?? throw new ArgumentNullException(nameof(code));
            _message = message ?? throw new ArgumentNullException(nameof(message));
            AsyncParameters = asyncParameters;
            Parameters = parameters;
            _severity = severity;
        }

        /// <summary>
        /// Creates the rule from an async builder's configuration (the <c>WhenExpression</c> path).
        /// </summary>
        internal ExpressionAsyncBusinessRule(RuleExpression expression, BuiltRuleDefinition definition)
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
        /// Gets the asynchronous provider for <c>@parameters</c>, or null.
        /// </summary>
        public IAsyncRuleParameterProvider? AsyncParameters { get; }

        /// <summary>
        /// Gets the synchronous provider for <c>@parameters</c>, or null.
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
        /// Evaluates the expression against <paramref name="context"/>.
        /// </summary>
        /// <param name="context">The context whose entries the identifiers bind to.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task producing true when the broken condition holds.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
        /// <exception cref="RuleExpressionBindingException">Thrown when an identifier, parameter or member cannot be bound.</exception>
        /// <exception cref="RuleExpressionEvaluationException">Thrown when an operator meets operands it cannot work on.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public override Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            if (_definition == null)
            {
                return Expression.EvaluateAsync(context, AsyncParameters, Parameters, cancellationToken);
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return IsBrokenThroughDefinitionAsync(_definition, context, cancellationToken);
        }

        /// <summary>
        /// Evaluates the rule and returns its broken result, or null when it passed; a builder-built rule attaches the
        /// builder's violation details.
        /// </summary>
        /// <param name="context">The context whose entries the identifiers bind to.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task producing the broken result, or null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public override async Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            if (_definition == null)
            {
                return await base.EvaluateAsync(context, cancellationToken);
            }

            return await IsBrokenAsync(context, cancellationToken) ? _definition.CreateResult(this, context) : null;
        }

        /// <summary>
        /// Evaluates the expression once against <paramref name="context"/> and explains the verdict with the bound
        /// values. Named <c>ExplainExpressionAsync</c> so it does not shadow the core <c>ExplainAsync</c> extension that returns a <c>RuleExplanation</c> tree.
        /// </summary>
        /// <param name="context">The context whose entries the identifiers bind to.</param>
        /// <param name="redact">An optional delegate deciding how each bound value is displayed.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task producing the explanation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public Task<RuleExpressionExplanation> ExplainExpressionAsync(BusinessRuleContext context, RuleExpressionRedactor? redact = null, CancellationToken cancellationToken = default)
        {
            if (_definition == null)
            {
                return Expression.ExplainAsync(context, redact, AsyncParameters, Parameters, cancellationToken);
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return ExplainThroughDefinitionAsync(_definition, context, redact, cancellationToken);
        }

        private async Task<bool> IsBrokenThroughDefinitionAsync(BuiltRuleDefinition definition, BusinessRuleContext context, CancellationToken cancellationToken)
        {
            RuleParameters declared = await definition.ResolveParametersAsync(context, cancellationToken);
            return await Expression.EvaluateAsyncCore(context, declared, null, null, cancellationToken);
        }

        private async Task<RuleExpressionExplanation> ExplainThroughDefinitionAsync(
            BuiltRuleDefinition definition,
            BusinessRuleContext context,
            RuleExpressionRedactor? redact,
            CancellationToken cancellationToken)
        {
            RuleParameters declared = await definition.ResolveParametersAsync(context, cancellationToken);
            return await Expression.ExplainAsyncCore(context, declared, null, null, redact, cancellationToken);
        }
    }
}
