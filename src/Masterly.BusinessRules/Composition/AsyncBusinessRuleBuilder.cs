using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Fluent builder for creating asynchronous business rules inline without subclassing.
    /// Provides a convenient way to define async rules with full metadata support.
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
    public sealed class AsyncBusinessRuleBuilder
    {
        private string _code = "ASYNC_RULE";
        private string _message = "Async business rule violated.";
        private RuleSeverity _severity = RuleSeverity.Error;
        private string _name = string.Empty;
        private string _description = string.Empty;
        private string _category = string.Empty;
        private List<string> _tags = new List<string>();
        private Func<BusinessRuleContext, CancellationToken, Task<bool>>? _isBrokenFunc;

        private AsyncBusinessRuleBuilder() { }

        /// <summary>
        /// Creates a new async rule builder with the specified code.
        /// </summary>
        /// <param name="code">The unique code identifying this rule.</param>
        /// <returns>A new builder instance.</returns>
        public static AsyncBusinessRuleBuilder Create(string code)
        {
            return new AsyncBusinessRuleBuilder { _code = code };
        }

        /// <summary>
        /// Sets the human-readable message describing why the rule is broken.
        /// </summary>
        /// <param name="message">The violation message.</param>
        /// <returns>This builder for method chaining.</returns>
        public AsyncBusinessRuleBuilder WithMessage(string message)
        {
            _message = message;
            return this;
        }

        /// <summary>
        /// Sets the severity level of the rule.
        /// </summary>
        /// <param name="severity">The severity level.</param>
        /// <returns>This builder for method chaining.</returns>
        public AsyncBusinessRuleBuilder WithSeverity(RuleSeverity severity)
        {
            _severity = severity;
            return this;
        }

        /// <summary>
        /// Sets the human-readable name for the rule.
        /// </summary>
        /// <param name="name">The rule name.</param>
        /// <returns>This builder for method chaining.</returns>
        public AsyncBusinessRuleBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        /// <summary>
        /// Sets a detailed description of what the rule validates.
        /// </summary>
        /// <param name="description">The rule description.</param>
        /// <returns>This builder for method chaining.</returns>
        public AsyncBusinessRuleBuilder WithDescription(string description)
        {
            _description = description;
            return this;
        }

        /// <summary>
        /// Sets the category for grouping related rules.
        /// </summary>
        /// <param name="category">The category name.</param>
        /// <returns>This builder for method chaining.</returns>
        public AsyncBusinessRuleBuilder WithCategory(string category)
        {
            _category = category;
            return this;
        }

        /// <summary>
        /// Adds tags for filtering and categorizing rules.
        /// </summary>
        /// <param name="tags">The tags to add.</param>
        /// <returns>This builder for method chaining.</returns>
        public AsyncBusinessRuleBuilder WithTags(params string[] tags)
        {
            _tags.AddRange(tags);
            return this;
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
                throw new InvalidOperationException("Must specify a condition using WhenAsync()");

            return new BuiltAsyncBusinessRule(
                _code,
                _message,
                _severity,
                _name,
                _description,
                _category,
                _tags.AsReadOnly(),
                _isBrokenFunc);
        }

        private sealed class BuiltAsyncBusinessRule : IAsyncBusinessRule
        {
            private readonly Func<BusinessRuleContext, CancellationToken, Task<bool>> _isBrokenFunc;

            public BuiltAsyncBusinessRule(
                string code,
                string message,
                RuleSeverity severity,
                string name,
                string description,
                string category,
                IReadOnlyList<string> tags,
                Func<BusinessRuleContext, CancellationToken, Task<bool>> isBrokenFunc)
            {
                Code = code;
                Message = message;
                Severity = severity;
                Name = string.IsNullOrEmpty(name) ? code : name;
                Description = description;
                Category = category;
                Tags = tags;
                _isBrokenFunc = isBrokenFunc;
            }

            public string Code { get; }
            public string Message { get; }
            public RuleSeverity Severity { get; }
            public string Name { get; }
            public string Description { get; }
            public string Category { get; }
            public IReadOnlyList<string> Tags { get; }

            public Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            {
                return _isBrokenFunc(context, cancellationToken);
            }

            public async Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            {
                if (await IsBrokenAsync(context, cancellationToken))
                    return new BusinessRuleResult(Code, Message, Severity);
                return null;
            }

            public async Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            {
                if (await IsBrokenAsync(context, cancellationToken))
                    throw new BusinessRuleValidationException(new[] { new BusinessRuleResult(Code, Message, Severity) });
            }
        }
    }
}
