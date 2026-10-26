using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Base class for implementing synchronous business rules.
    /// Inherit from this class and override <see cref="Code"/>, <see cref="Message"/>, and <see cref="IsBroken()"/>
    /// to create custom business rules. Override <see cref="Describe"/> to attach rich violation details
    /// (<c>Detail</c>, <c>Actual</c>, <c>Expected</c>, <c>Remediation</c>, <c>Data</c>) to the result; it runs only on
    /// the broken path and an exception inside it degrades to <see cref="RuleResultDetails.Empty"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// public class AgeValidationRule : BaseBusinessRule
    /// {
    ///     private readonly int _age;
    ///
    ///     public AgeValidationRule(int age) => _age = age;
    ///
    ///     public override string Code => "AGE.INVALID";
    ///     public override string Message => "Age must be 18 or older";
    ///     public override bool IsBroken() => _age &lt; 18;
    /// }
    /// </code>
    /// </example>
    public abstract class BaseBusinessRule : IBusinessRule
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
        /// Determines whether this business rule is broken.
        /// </summary>
        /// <returns><c>true</c> if the rule is broken (violated); otherwise, <c>false</c>.</returns>
        public abstract bool IsBroken();

        /// <summary>
        /// Determines whether this business rule is broken using context data.
        /// Override this method to access context data during rule evaluation.
        /// The default implementation ignores the context and calls <see cref="IsBroken()"/>.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <returns><c>true</c> if the rule is broken (violated); otherwise, <c>false</c>.</returns>
        public virtual bool IsBroken(BusinessRuleContext context) => IsBroken();

        /// <summary>
        /// Checks this business rule and throws an exception if it is broken.
        /// </summary>
        /// <exception cref="BusinessRuleValidationException">Thrown when the rule is broken.</exception>
        public void Check()
        {
            if (IsBroken())
            {
                throw new BusinessRuleValidationException(AsEnumerable(Evaluate()));
            }
        }

        /// <summary>
        /// Checks this business rule using context data and throws an exception if it is broken.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when the rule is broken.</exception>
        public void Check(BusinessRuleContext context)
        {
            if (IsBroken(context))
            {
                throw new BusinessRuleValidationException(AsEnumerable(Evaluate(context)));
            }
        }

        /// <summary>
        /// Creates the result describing this rule as broken, carrying its metadata and the details returned by
        /// <see cref="Describe"/> called with a null context. This is a factory, not an evaluation.
        /// </summary>
        /// <returns>A result containing the rule's code, message, severity, metadata and details.</returns>
        public virtual BusinessRuleResult Evaluate() => CreateResult(null);

        /// <summary>
        /// Creates the result describing this rule as broken, carrying its metadata and the details returned by
        /// <see cref="Describe"/> called with <paramref name="context"/>. The detail sentence is formatted with
        /// <see cref="BusinessRuleContext.FormatProvider"/> when set. This is a factory, not an evaluation.
        /// </summary>
        /// <param name="context">The context the rule was evaluated with.</param>
        /// <returns>A result containing the rule's code, message, severity, metadata and details.</returns>
        public virtual BusinessRuleResult Evaluate(BusinessRuleContext context) => CreateResult(context);

        /// <summary>
        /// Describes a violation of this rule. Called only when the rule is broken, with the evaluation context or
        /// null for the parameterless path. Override it to attach <c>Detail</c>, <c>Actual</c>, <c>Expected</c>,
        /// <c>Remediation</c> and <c>Data</c> to the result. An exception thrown here is swallowed and the result is
        /// built without details, so a faulty description never masks the violation. Defaults to <see cref="RuleResultDetails.Empty"/>.
        /// </summary>
        /// <param name="context">The context the rule was evaluated with, or null.</param>
        /// <returns>The details to attach to the result.</returns>
        protected virtual RuleResultDetails Describe(BusinessRuleContext? context) => RuleResultDetails.Empty;

        private BusinessRuleResult CreateResult(BusinessRuleContext? context)
        {
            RuleResultDetails details = RuleResultFactory.DescribeGuarded(() => Describe(context));
            return RuleResultFactory.Create(this, details, context?.FormatProvider);
        }

        /// <summary>
        /// Converts a single result to an enumerable collection.
        /// </summary>
        private static IEnumerable<BusinessRuleResult> AsEnumerable(BusinessRuleResult result) => new List<BusinessRuleResult> { result };
    }
}