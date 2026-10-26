using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Fluent builder for creating subject-typed asynchronous business rules inline without subclassing. The condition
    /// receives the subject, the context and the cancellation token (or fewer of them). The metadata and context-only
    /// detail methods are inherited from <see cref="BusinessRuleMetadataBuilder{TBuilder}"/>; the subject-aware
    /// <see cref="WithDetail(Func{TSubject, BusinessRuleContext, FormattableString})"/>,
    /// <see cref="WithActual(Func{TSubject, BusinessRuleContext, object})"/>,
    /// <see cref="WithExpected(Func{TSubject, BusinessRuleContext, object})"/> and
    /// <see cref="WithRemediation(Func{TSubject, BusinessRuleContext, string})"/> overloads take precedence over them.
    /// </summary>
    /// <typeparam name="TSubject">The type of the subject the rule validates.</typeparam>
    /// <example>
    /// <code>
    /// IAsyncBusinessRule&lt;User&gt; rule = AsyncBusinessRuleBuilder&lt;User&gt;.Create("EMAIL.DUPLICATE")
    ///     .WithMessage("Email already exists")
    ///     .WhenAsync((user, ctx, ct) => repository.EmailExistsAsync(user.Email, ct))
    ///     .Build();
    /// </code>
    /// </example>
    public sealed class AsyncBusinessRuleBuilder<TSubject> : BusinessRuleMetadataBuilder<AsyncBusinessRuleBuilder<TSubject>>
    {
        private const string DefaultMessage = "Async business rule violated.";
        private const string MissingConditionMessage = "Must specify a condition using WhenAsync()";

        private Func<TSubject, BusinessRuleContext, CancellationToken, Task<bool>>? _isBrokenFunc;
        private Func<TSubject, BusinessRuleContext, FormattableString>? _detail;
        private Func<TSubject, BusinessRuleContext, object?>? _actual;
        private Func<TSubject, BusinessRuleContext, object?>? _expected;
        private Func<TSubject, BusinessRuleContext, string>? _remediation;

        private AsyncBusinessRuleBuilder(string code) : base(code, DefaultMessage)
        {
        }

        /// <summary>
        /// Creates a new typed async rule builder with the specified code.
        /// </summary>
        /// <param name="code">The unique code identifying this rule.</param>
        /// <returns>A new builder instance.</returns>
        public static AsyncBusinessRuleBuilder<TSubject> Create(string code)
        {
            return new AsyncBusinessRuleBuilder<TSubject>(code);
        }

        /// <summary>
        /// Sets the detail sentence from the subject and the context. Takes precedence over the context-only overload.
        /// The function runs only on the broken path; if it throws, the result is built without details.
        /// </summary>
        /// <param name="detail">A function that receives the subject and the context and returns the detail sentence.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="detail"/> is null.</exception>
        public AsyncBusinessRuleBuilder<TSubject> WithDetail(Func<TSubject, BusinessRuleContext, FormattableString> detail)
        {
            _detail = detail ?? throw new ArgumentNullException(nameof(detail));
            return this;
        }

        /// <summary>
        /// Sets the actual value from the subject and the context. Takes precedence over the context-only overload.
        /// The function runs only on the broken path; if it throws, the result is built without details.
        /// </summary>
        /// <param name="actual">A function that receives the subject and the context and returns the actual value.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> is null.</exception>
        public AsyncBusinessRuleBuilder<TSubject> WithActual(Func<TSubject, BusinessRuleContext, object?> actual)
        {
            _actual = actual ?? throw new ArgumentNullException(nameof(actual));
            return this;
        }

        /// <summary>
        /// Sets the expected value from the subject and the context. Takes precedence over the context-only overloads.
        /// The function runs only on the broken path; if it throws, the result is built without details.
        /// </summary>
        /// <param name="expected">A function that receives the subject and the context and returns the expected value or range.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="expected"/> is null.</exception>
        public AsyncBusinessRuleBuilder<TSubject> WithExpected(Func<TSubject, BusinessRuleContext, object?> expected)
        {
            _expected = expected ?? throw new ArgumentNullException(nameof(expected));
            return this;
        }

        /// <summary>
        /// Sets the remediation hint from the subject and the context. Takes precedence over the context-only overloads.
        /// The function runs only on the broken path; if it throws, the result is built without details.
        /// </summary>
        /// <param name="remediation">A function that receives the subject and the context and returns the hint.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="remediation"/> is null.</exception>
        public AsyncBusinessRuleBuilder<TSubject> WithRemediation(Func<TSubject, BusinessRuleContext, string> remediation)
        {
            _remediation = remediation ?? throw new ArgumentNullException(nameof(remediation));
            return this;
        }

        /// <summary>
        /// Sets the async condition on the subject, the context and the cancellation token that determines if the rule is broken.
        /// </summary>
        /// <param name="isBrokenCondition">An async function that receives the subject, the context and the cancellation token and returns true when broken.</param>
        /// <returns>This builder for method chaining.</returns>
        public AsyncBusinessRuleBuilder<TSubject> WhenAsync(Func<TSubject, BusinessRuleContext, CancellationToken, Task<bool>> isBrokenCondition)
        {
            _isBrokenFunc = isBrokenCondition;
            return this;
        }

        /// <summary>
        /// Sets the async condition on the subject and the context (without cancellation token) that determines if the rule is broken.
        /// </summary>
        /// <param name="isBrokenCondition">An async function that receives the subject and the context and returns true when broken.</param>
        /// <returns>This builder for method chaining.</returns>
        public AsyncBusinessRuleBuilder<TSubject> WhenAsync(Func<TSubject, BusinessRuleContext, Task<bool>> isBrokenCondition)
        {
            _isBrokenFunc = (subject, ctx, _) => isBrokenCondition(subject, ctx);
            return this;
        }

        /// <summary>
        /// Sets the async condition on the subject alone that determines if the rule is broken.
        /// </summary>
        /// <param name="isBrokenCondition">An async function that receives the subject and returns true when broken.</param>
        /// <returns>This builder for method chaining.</returns>
        public AsyncBusinessRuleBuilder<TSubject> WhenAsync(Func<TSubject, Task<bool>> isBrokenCondition)
        {
            _isBrokenFunc = (subject, _, __) => isBrokenCondition(subject);
            return this;
        }

        /// <summary>
        /// Builds and returns the configured subject-typed async business rule.
        /// </summary>
        /// <returns>A new typed async rule instance with the configured settings.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no condition has been specified via <c>WhenAsync</c>.</exception>
        public IAsyncBusinessRule<TSubject> Build()
        {
            if (_isBrokenFunc == null)
            {
                throw new InvalidOperationException(MissingConditionMessage);
            }

            SubjectRuleDescriptor<TSubject> descriptor = new SubjectRuleDescriptor<TSubject>(BuildDescriptor(), _detail, _actual, _expected, _remediation);
            return new BuiltAsyncBusinessRule(BuildMetadata(), _isBrokenFunc, descriptor);
        }

        private sealed class BuiltAsyncBusinessRule : IAsyncBusinessRule<TSubject>
        {
            private readonly BuiltRuleMetadata _metadata;
            private readonly Func<TSubject, BusinessRuleContext, CancellationToken, Task<bool>> _isBrokenFunc;
            private readonly SubjectRuleDescriptor<TSubject> _descriptor;

            public BuiltAsyncBusinessRule(
                BuiltRuleMetadata metadata,
                Func<TSubject, BusinessRuleContext, CancellationToken, Task<bool>> isBrokenFunc,
                SubjectRuleDescriptor<TSubject> descriptor)
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

            public Task<bool> IsBrokenAsync(TSubject subject, BusinessRuleContext context, CancellationToken cancellationToken = default)
            {
                return _isBrokenFunc(subject, context, cancellationToken);
            }

            public async Task<BusinessRuleResult?> EvaluateAsync(TSubject subject, BusinessRuleContext context, CancellationToken cancellationToken = default)
            {
                if (await IsBrokenAsync(subject, context, cancellationToken))
                {
                    return _descriptor.CreateResult(this, subject, context);
                }

                return null;
            }
        }
    }
}
