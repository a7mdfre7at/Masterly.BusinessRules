using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Base class for implementing asynchronous business rules.
    /// Inherit from this class and override <see cref="Code"/>, <see cref="Message"/>, and <see cref="IsBrokenAsync"/>
    /// to create custom async business rules that can perform I/O operations during validation.
    /// </summary>
    /// <example>
    /// <code>
    /// public class UniqueEmailRule : BaseAsyncBusinessRule
    /// {
    ///     private readonly string _email;
    ///     private readonly IUserRepository _repository;
    ///
    ///     public UniqueEmailRule(string email, IUserRepository repository)
    ///     {
    ///         _email = email;
    ///         _repository = repository;
    ///     }
    ///
    ///     public override string Code => "EMAIL.DUPLICATE";
    ///     public override string Message => "Email address is already in use";
    ///
    ///     public override async Task&lt;bool&gt; IsBrokenAsync(BusinessRuleContext context, CancellationToken ct)
    ///         => await _repository.EmailExistsAsync(_email, ct);
    /// }
    /// </code>
    /// </example>
    public abstract class BaseAsyncBusinessRule : IAsyncBusinessRule
    {
        private static readonly IReadOnlyList<string> EmptyTags = Array.Empty<string>();

        /// <summary>
        /// Gets the unique code identifying this rule violation.
        /// </summary>
        public abstract string Code { get; }

        /// <summary>
        /// Gets the human-readable message describing why the rule was broken.
        /// </summary>
        public abstract string Message { get; }

        /// <summary>
        /// Gets the severity level of this rule. Defaults to <see cref="RuleSeverity.Error"/>.
        /// Override this property to specify a different severity level.
        /// </summary>
        public virtual RuleSeverity Severity => RuleSeverity.Error;

        /// <summary>
        /// Optional human-readable name. Defaults to type name.
        /// </summary>
        public virtual string Name => GetType().Name;

        /// <summary>
        /// Optional detailed description. Defaults to empty string.
        /// </summary>
        public virtual string Description => string.Empty;

        /// <summary>
        /// Optional category for grouping. Defaults to empty string.
        /// </summary>
        public virtual string Category => string.Empty;

        /// <summary>
        /// Gets optional tags for filtering rules. Defaults to an empty list.
        /// Override this property to provide tags for rule categorization and filtering.
        /// </summary>
        public virtual IReadOnlyList<string> Tags => EmptyTags;

        /// <summary>
        /// Determines asynchronously whether this business rule is broken.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A task that returns <c>true</c> if the rule is broken (violated); otherwise, <c>false</c>.</returns>
        public abstract Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Evaluates this rule asynchronously and returns a result if the rule is broken.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>
        /// A task that returns a <see cref="BusinessRuleResult"/> if the rule is broken;
        /// otherwise, <c>null</c>.
        /// </returns>
        public virtual async Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            if (await IsBrokenAsync(context, cancellationToken))
                return new BusinessRuleResult(Code, Message, Severity);

            return null;
        }

        /// <summary>
        /// Checks this business rule asynchronously and throws an exception if it is broken.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when the rule is broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            BusinessRuleResult? result = await EvaluateAsync(context, cancellationToken);
            if (result != null)
                throw new BusinessRuleValidationException(new[] { result });
        }
    }
}