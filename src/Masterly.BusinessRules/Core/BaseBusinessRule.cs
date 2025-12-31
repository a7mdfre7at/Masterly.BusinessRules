using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Base class for implementing synchronous business rules.
    /// Inherit from this class and override <see cref="Code"/>, <see cref="Message"/>, and <see cref="IsBroken()"/>
    /// to create custom business rules.
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
                throw new BusinessRuleValidationException(AsEnumerable(Evaluate()));
        }

        /// <summary>
        /// Checks this business rule using context data and throws an exception if it is broken.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when the rule is broken.</exception>
        public void Check(BusinessRuleContext context)
        {
            if (IsBroken(context))
                throw new BusinessRuleValidationException(AsEnumerable(Evaluate()));
        }

        /// <summary>
        /// Creates a <see cref="BusinessRuleResult"/> for this rule.
        /// </summary>
        /// <returns>A result containing the rule's code, message, and severity.</returns>
        public BusinessRuleResult Evaluate() => new BusinessRuleResult(Code, Message, Severity);

        /// <summary>
        /// Converts a single result to an enumerable collection.
        /// </summary>
        private static IEnumerable<BusinessRuleResult> AsEnumerable(BusinessRuleResult result) => new List<BusinessRuleResult> { result };
    }
}