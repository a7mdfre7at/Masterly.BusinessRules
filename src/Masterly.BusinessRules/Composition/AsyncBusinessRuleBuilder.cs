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

        private Func<BusinessRuleContext, CancellationToken, Task<bool>>? _isBrokenFunc;

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
            return this;
        }

        /// <summary>
        /// Builds and returns the configured async business rule.
        /// </summary>
        /// <returns>A new async business rule instance with the configured settings.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no condition has been specified via <see cref="WhenAsync(Func{BusinessRuleContext, CancellationToken, Task{bool}})"/>.</exception>
        public IAsyncBusinessRule Build()
        {
            if (_isBrokenFunc == null)
            {
                throw new InvalidOperationException(MissingConditionMessage);
            }

            return new BuiltAsyncBusinessRule(BuildMetadata(), _isBrokenFunc, BuildDescriptor());
        }

        private sealed class BuiltAsyncBusinessRule : IAsyncBusinessRule
        {
            private readonly BuiltRuleMetadata _metadata;
            private readonly Func<BusinessRuleContext, CancellationToken, Task<bool>> _isBrokenFunc;
            private readonly BuiltRuleDescriptor _descriptor;

            public BuiltAsyncBusinessRule(
                BuiltRuleMetadata metadata,
                Func<BusinessRuleContext, CancellationToken, Task<bool>> isBrokenFunc,
                BuiltRuleDescriptor descriptor)
            {
                _metadata = metadata;
                _isBrokenFunc = isBrokenFunc;
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
                return _isBrokenFunc(context, cancellationToken);
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
