using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Fluent builder for creating asynchronous business rules inline without subclassing.
    /// Provides a convenient way to define async rules with full metadata support and, through
    /// <c>WithDetail</c>, <c>WithActual</c>, <c>WithExpected</c> and <c>WithRemediation</c> (inherited from
    /// <see cref="BusinessRuleMetadataBuilder{TBuilder}"/>), context-aware violation details produced only on the broken path.
    /// </summary>
    /// <example>
    /// <code>
    /// IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create("EMAIL.DUPLICATE")
    ///     .WithMessage("Email already exists")
    ///     .WithSeverity(RuleSeverity.Error)
    ///     .WithCategory("Validation")
    ///     .WhenAsync(async (ctx, ct) => await repo.EmailExistsAsync(email, ct))
    ///     .Build();
    /// </code>
    /// </example>
    public sealed class AsyncBusinessRuleBuilder : BusinessRuleMetadataBuilder<AsyncBusinessRuleBuilder>
    {
        private const string DefaultMessage = "Async business rule violated.";
        private const string MissingConditionMessage = "Must specify a condition using WhenAsync()";
        private const string NullFactoryRuleMessage = "The rule factory returned a null rule.";

        private Func<BusinessRuleContext, CancellationToken, Task<bool>>? _isBrokenFunc;
        private Func<BusinessRuleContext, RuleParameters, CancellationToken, Task<bool>>? _isBrokenWithParametersFunc;
        private IAsyncRuleParameterProvider? _asyncParameterProvider;
        private IAsyncBuiltRuleFactory? _ruleFactory;

        private AsyncBusinessRuleBuilder(string code) : base(code, DefaultMessage)
        {
        }

        /// <summary>
        /// Creates a new async rule builder with the specified code.
        /// </summary>
        /// <param name="code">The unique code identifying this rule.</param>
        /// <returns>A new builder instance.</returns>
        public static AsyncBusinessRuleBuilder Create(string code)
        {
            return new AsyncBusinessRuleBuilder(code);
        }

        /// <summary>
        /// Sets the async condition that determines if the rule is broken.
        /// </summary>
        /// <param name="isBrokenCondition">An async function that receives context and cancellation token, returns true when broken.</param>
        /// <returns>This builder for method chaining.</returns>
        public AsyncBusinessRuleBuilder WhenAsync(Func<BusinessRuleContext, CancellationToken, Task<bool>> isBrokenCondition)
        {
            _isBrokenFunc = isBrokenCondition;
            _isBrokenWithParametersFunc = null;
            _ruleFactory = null;
            return this;
        }

        /// <summary>
        /// Sets the async condition (without cancellation token) that determines if the rule is broken.
        /// </summary>
        /// <param name="isBrokenCondition">An async function that receives context and returns true when broken.</param>
        /// <returns>This builder for method chaining.</returns>
        public AsyncBusinessRuleBuilder WhenAsync(Func<BusinessRuleContext, Task<bool>> isBrokenCondition)
        {
            _isBrokenFunc = (ctx, _) => isBrokenCondition(ctx);
            _isBrokenWithParametersFunc = null;
            _ruleFactory = null;
            return this;
        }

        /// <summary>
        /// Sets the async condition with context, resolved parameters and cancellation token that determines if the
        /// rule is broken. The <see cref="RuleParameters"/> hold every parameter declared with
        /// <see cref="BusinessRuleMetadataBuilder{TBuilder}.WithParameter{T}"/>, resolved on every evaluation through the
        /// provider from <see cref="WithParameters(IAsyncRuleParameterProvider)"/> or
        /// <see cref="BusinessRuleMetadataBuilder{TBuilder}.WithParameters"/> (a provider under
        /// <see cref="RuleParameters.AsyncContextKey"/> / <see cref="RuleParameters.ContextKey"/> overrides them) for the
        /// scope under <see cref="RuleParameters.ScopeKey"/>. The token is part of the signature because a two-parameter
        /// <c>(ctx, p)</c> lambda would be ambiguous with <c>(ctx, ct)</c>.
        /// </summary>
        /// <param name="isBrokenCondition">An async function that receives the context, the parameters and the cancellation token and returns true when broken.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="isBrokenCondition"/> is null.</exception>
        public AsyncBusinessRuleBuilder WhenAsync(Func<BusinessRuleContext, RuleParameters, CancellationToken, Task<bool>> isBrokenCondition)
        {
            _isBrokenWithParametersFunc = isBrokenCondition ?? throw new ArgumentNullException(nameof(isBrokenCondition));
            _isBrokenFunc = null;
            _ruleFactory = null;
            return this;
        }

        /// <summary>
        /// Sets the async condition from a textual expression compiled by <paramref name="compiler"/> (the seam an
        /// expression package implements). The expression is compiled now, so a malformed expression fails here rather than at evaluation time.
        /// </summary>
        /// <param name="compiler">The compiler that turns the expression into an async broken-condition delegate.</param>
        /// <param name="expression">The condition text.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="compiler"/> or <paramref name="expression"/> is null, or the compiler returned null.</exception>
        public AsyncBusinessRuleBuilder WhenAsync(IAsyncRuleConditionCompiler compiler, string expression)
        {
            if (compiler == null)
            {
                throw new ArgumentNullException(nameof(compiler));
            }

            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            _isBrokenFunc = compiler.Compile(expression) ?? throw new ArgumentNullException(nameof(compiler), "The condition compiler returned a null delegate.");
            _isBrokenWithParametersFunc = null;
            _ruleFactory = null;
            return this;
        }

        /// <summary>
        /// Injects an asynchronous provider for the parameters declared with <see cref="BusinessRuleMetadataBuilder{TBuilder}.WithParameter{T}"/>.
        /// It is preferred over a sync provider injected with <see cref="BusinessRuleMetadataBuilder{TBuilder}.WithParameters"/>;
        /// a provider implementing both interfaces is used through its async side.
        /// </summary>
        /// <param name="provider">The async parameter provider.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
        public AsyncBusinessRuleBuilder WithParameters(IAsyncRuleParameterProvider provider)
        {
            _asyncParameterProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            return this;
        }

        /// <summary>
        /// Hands rule creation to <paramref name="factory"/>: <see cref="Build"/> passes it a <see cref="BuiltRuleDefinition"/>
        /// with the configured metadata, details and parameters (including the async provider) and returns the rule it
        /// creates. The factory replaces any condition set so far, and a later <c>WhenAsync</c> call replaces the factory.
        /// This is the seam a sibling package uses to return its own rule type from <c>Build()</c>, for example
        /// <c>WhenExpression</c> returning an expression rule.
        /// </summary>
        /// <param name="factory">The factory that creates the built rule.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
        public AsyncBusinessRuleBuilder WithRuleFactory(IAsyncBuiltRuleFactory factory)
        {
            _ruleFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            _isBrokenFunc = null;
            _isBrokenWithParametersFunc = null;
            return this;
        }

        /// <summary>
        /// Builds and returns the configured async business rule.
        /// </summary>
        /// <returns>A new async business rule instance with the configured settings.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no condition has been specified via <c>WhenAsync</c> and no factory via <see cref="WithRuleFactory"/>, or when the factory returned null.</exception>
        public IAsyncBusinessRule Build()
        {
            if (_ruleFactory != null)
            {
                return _ruleFactory.Create(BuildDefinition(_asyncParameterProvider)) ?? throw new InvalidOperationException(NullFactoryRuleMessage);
            }

            if (_isBrokenFunc == null && _isBrokenWithParametersFunc == null)
            {
                throw new InvalidOperationException(MissingConditionMessage);
            }

            return new BuiltAsyncBusinessRule(BuildMetadata(), _isBrokenFunc, _isBrokenWithParametersFunc, BuildParameterBinding(_asyncParameterProvider), BuildDescriptor());
        }

        private sealed class BuiltAsyncBusinessRule : IAsyncBusinessRule
        {
            private readonly BuiltRuleMetadata _metadata;
            private readonly Func<BusinessRuleContext, CancellationToken, Task<bool>>? _isBrokenFunc;
            private readonly Func<BusinessRuleContext, RuleParameters, CancellationToken, Task<bool>>? _isBrokenWithParametersFunc;
            private readonly RuleParameterBinding _parameters;
            private readonly BuiltRuleDescriptor _descriptor;

            public BuiltAsyncBusinessRule(
                BuiltRuleMetadata metadata,
                Func<BusinessRuleContext, CancellationToken, Task<bool>>? isBrokenFunc,
                Func<BusinessRuleContext, RuleParameters, CancellationToken, Task<bool>>? isBrokenWithParametersFunc,
                RuleParameterBinding parameters,
                BuiltRuleDescriptor descriptor)
            {
                _metadata = metadata;
                _isBrokenFunc = isBrokenFunc;
                _isBrokenWithParametersFunc = isBrokenWithParametersFunc;
                _parameters = parameters;
                _descriptor = descriptor;
            }

            public string Code => _metadata.Code;
            public string Message => _metadata.Message;
            public RuleSeverity Severity => _metadata.Severity;
            public string Name => _metadata.Name;
            public string Description => _metadata.Description;
            public string Category => _metadata.Category;
            public IReadOnlyList<string> Tags => _metadata.Tags;

            public Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            {
                if (_isBrokenWithParametersFunc != null)
                {
                    return IsBrokenWithParametersAsync(context, cancellationToken);
                }

                return _isBrokenFunc!(context, cancellationToken); // Build() guarantees one of the two conditions is set
            }

            private async Task<bool> IsBrokenWithParametersAsync(BusinessRuleContext context, CancellationToken cancellationToken)
            {
                RuleParameters parameters = await _parameters.ResolveAsync(context, cancellationToken);
                return await _isBrokenWithParametersFunc!(context, parameters, cancellationToken); // only called when the parameters condition is set
            }

            public async Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            {
                if (await IsBrokenAsync(context, cancellationToken))
                {
                    return _descriptor.CreateResult(this, context);
                }

                return null;
            }

            public async Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            {
                BusinessRuleResult? result = await EvaluateAsync(context, cancellationToken);
                if (result != null)
                {
                    throw new BusinessRuleValidationException(new[] { result });
                }
            }
        }
    }
}
