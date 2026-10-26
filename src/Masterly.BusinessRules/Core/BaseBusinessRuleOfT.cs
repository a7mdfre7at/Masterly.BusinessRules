using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Base class for implementing synchronous business rules that receive their subject at evaluation time.
    /// Inherit from this class and override <see cref="Code"/>, <see cref="Message"/> and <see cref="IsBroken(TSubject)"/>.
    /// Override <see cref="Describe"/> to attach rich violation details (<c>Detail</c>, <c>Actual</c>, <c>Expected</c>,
    /// <c>Remediation</c>, <c>Data</c>) to the result; it runs only on the broken path and an exception inside it
    /// degrades to <see cref="RuleResultDetails.Empty"/>. The rule holds no subject state, so a single instance can be
    /// shared and bound to any subject with <c>.For(subject)</c>.
    /// </summary>
    /// <typeparam name="TSubject">The type of the subject the rule validates. Value types are supported.</typeparam>
    /// <example>
    /// <code>
    /// public sealed class OrderMustHaveItemsRule : BaseBusinessRule&lt;Order&gt;
    /// {
    ///     public override string Code => "ORDER.NO_ITEMS";
    ///     public override string Message => "Order must contain at least one item.";
    ///     public override bool IsBroken(Order order) => order.Items.Count == 0;
    /// }
    /// </code>
    /// </example>
    public abstract class BaseBusinessRule<TSubject> : IBusinessRule<TSubject>
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
        /// Determines whether this business rule is broken for the given subject.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <returns><c>true</c> if the rule is broken (violated); otherwise, <c>false</c>.</returns>
        public abstract bool IsBroken(TSubject subject);

        /// <summary>
        /// Determines whether this business rule is broken for the given subject using context data.
        /// The default implementation ignores the context and calls <see cref="IsBroken(TSubject)"/>.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <returns><c>true</c> if the rule is broken (violated); otherwise, <c>false</c>.</returns>
        public virtual bool IsBroken(TSubject subject, BusinessRuleContext context) => IsBroken(subject);

        /// <summary>
        /// Creates the result describing this rule as broken for the given subject, carrying its metadata and the
        /// details returned by <see cref="Describe"/> called with a null context. This is a factory, not an evaluation.
        /// </summary>
        /// <param name="subject">The subject the rule was evaluated against.</param>
        /// <returns>A result containing the rule's code, message, severity, metadata and details.</returns>
        public virtual BusinessRuleResult Evaluate(TSubject subject) => CreateResult(subject, null);

        /// <summary>
        /// Creates the result describing this rule as broken for the given subject, carrying its metadata and the
        /// details returned by <see cref="Describe"/> called with <paramref name="context"/>. The detail sentence is
        /// formatted with <see cref="BusinessRuleContext.FormatProvider"/> when set. This is a factory, not an evaluation.
        /// </summary>
        /// <param name="subject">The subject the rule was evaluated against.</param>
        /// <param name="context">The context the rule was evaluated with.</param>
        /// <returns>A result containing the rule's code, message, severity, metadata and details.</returns>
        public virtual BusinessRuleResult Evaluate(TSubject subject, BusinessRuleContext context) => CreateResult(subject, context);

        /// <summary>
        /// Describes a violation of this rule for the given subject. Called only when the rule is broken, with the
        /// evaluation context or null for the context-less path. Override it to attach <c>Detail</c>, <c>Actual</c>,
        /// <c>Expected</c>, <c>Remediation</c> and <c>Data</c> to the result. An exception thrown here is swallowed and
        /// the result is built without details. Defaults to <see cref="RuleResultDetails.Empty"/>.
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
