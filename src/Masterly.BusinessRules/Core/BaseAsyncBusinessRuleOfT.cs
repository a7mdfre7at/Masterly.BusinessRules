using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Base class for implementing asynchronous business rules that receive their subject at evaluation time.
    /// Inherit from this class and override <see cref="Code"/>, <see cref="Message"/> and <see cref="IsBrokenAsync"/>.
    /// Override <see cref="Describe"/> to attach rich violation details (<c>Detail</c>, <c>Actual</c>, <c>Expected</c>,
    /// <c>Remediation</c>, <c>Data</c>) to the result; it runs only on the broken path and an exception inside it
    /// degrades to <see cref="RuleResultDetails.Empty"/>. The rule holds no subject state, so a single instance can be
    /// shared (for example as a DI singleton with injected repositories) and bound to any subject with <c>.For(subject)</c>.
    /// </summary>
    /// <typeparam name="TSubject">The type of the subject the rule validates. Value types are supported.</typeparam>
    /// <example>
    /// <code>
    /// public sealed class EmailMustBeUniqueRule : BaseAsyncBusinessRule&lt;User&gt;
    /// {
    ///     private readonly IUserRepository _repository;
    ///
    ///     public EmailMustBeUniqueRule(IUserRepository repository) => _repository = repository;
    ///
    ///     public override string Code => "EMAIL.DUPLICATE";
    ///     public override string Message => "Email address is already in use";
    ///
    ///     public override Task&lt;bool&gt; IsBrokenAsync(User user, BusinessRuleContext context, CancellationToken ct)
    ///         => _repository.EmailExistsAsync(user.Email, ct);
    /// }
    /// </code>
    /// </example>
    public abstract class BaseAsyncBusinessRule<TSubject> : IAsyncBusinessRule<TSubject>
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
        /// </summary>
        public virtual IReadOnlyList<string> Tags => EmptyTags;

        /// <summary>
        /// Determines asynchronously whether this business rule is broken for the given subject.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A task that returns <c>true</c> if the rule is broken (violated); otherwise, <c>false</c>.</returns>
        public abstract Task<bool> IsBrokenAsync(TSubject subject, BusinessRuleContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Evaluates this rule asynchronously for the given subject and returns a result if the rule is broken.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A task that returns a <see cref="BusinessRuleResult"/> if the rule is broken; otherwise, <c>null</c>.</returns>
        public virtual async Task<BusinessRuleResult?> EvaluateAsync(TSubject subject, BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            if (await IsBrokenAsync(subject, context, cancellationToken))
            {
                return CreateResult(subject, context);
            }

            return null;
        }

        /// <summary>
        /// Describes a violation of this rule for the given subject. Called only when the rule is broken, with the
        /// evaluation context. Override it to attach <c>Detail</c>, <c>Actual</c>, <c>Expected</c>, <c>Remediation</c>
        /// and <c>Data</c> to the result. An exception thrown here is swallowed and the result is built without
        /// details. Defaults to <see cref="RuleResultDetails.Empty"/>.
        /// </summary>
        /// <param name="subject">The subject the rule was evaluated against.</param>
        /// <param name="context">The context the rule was evaluated with, or null.</param>
        /// <returns>The details to attach to the result.</returns>
        protected virtual RuleResultDetails Describe(TSubject subject, BusinessRuleContext? context) => RuleResultDetails.Empty;

        private BusinessRuleResult CreateResult(TSubject subject, BusinessRuleContext? context)
        {
            RuleResultDetails details = RuleResultFactory.DescribeGuarded(() => Describe(subject, context));
            return RuleResultFactory.Create(this, details, context?.FormatProvider);
        }
    }
}
