using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Fluent builder for creating business rules inline without subclassing.
    /// Provides a convenient way to define rules with full metadata support.
    /// </summary>
    /// <example>
    /// <code>
    /// IBusinessRule rule = BusinessRuleBuilder.Create("AGE.INVALID")
    ///     .WithMessage("Age must be 18 or older")
    ///     .WithSeverity(RuleSeverity.Error)
    ///     .WithCategory("Validation")
    ///     .WithTags("user", "age")
    ///     .When(() => age &lt; 18)
    ///     .Build();
    /// </code>
    /// </example>
    public sealed class BusinessRuleBuilder
    {
        private string _code = "RULE";
        private string _message = "Business rule violated.";
        private RuleSeverity _severity = RuleSeverity.Error;
        private string _name = string.Empty;
        private string _description = string.Empty;
        private string _category = string.Empty;
        private List<string> _tags = new List<string>();
        private Func<bool>? _isBrokenFunc;
        private Func<BusinessRuleContext, bool>? _isBrokenWithContextFunc;

        private BusinessRuleBuilder() { }

        /// <summary>
        /// Creates a new rule builder with the specified code.
        /// </summary>
        /// <param name="code">The unique code identifying this rule.</param>
        /// <returns>A new builder instance.</returns>
        public static BusinessRuleBuilder Create(string code)
        {
            return new BusinessRuleBuilder { _code = code };
        }

        /// <summary>
        /// Sets the human-readable message describing why the rule is broken.
        /// </summary>
        /// <param name="message">The violation message.</param>
        /// <returns>This builder for method chaining.</returns>
        public BusinessRuleBuilder WithMessage(string message)
        {
            _message = message;
            return this;
        }

        /// <summary>
        /// Sets the severity level of the rule.
        /// </summary>
        /// <param name="severity">The severity level.</param>
        /// <returns>This builder for method chaining.</returns>
        public BusinessRuleBuilder WithSeverity(RuleSeverity severity)
        {
            _severity = severity;
            return this;
        }

        /// <summary>
        /// Sets the human-readable name for the rule.
        /// </summary>
        /// <param name="name">The rule name.</param>
        /// <returns>This builder for method chaining.</returns>
        public BusinessRuleBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        /// <summary>
        /// Sets a detailed description of what the rule validates.
        /// </summary>
        /// <param name="description">The rule description.</param>
        /// <returns>This builder for method chaining.</returns>
        public BusinessRuleBuilder WithDescription(string description)
        {
            _description = description;
            return this;
        }

        /// <summary>
        /// Sets the category for grouping related rules.
        /// </summary>
        /// <param name="category">The category name.</param>
        /// <returns>This builder for method chaining.</returns>
        public BusinessRuleBuilder WithCategory(string category)
        {
            _category = category;
            return this;
        }

        /// <summary>
        /// Adds tags for filtering and categorizing rules.
        /// </summary>
        /// <param name="tags">The tags to add.</param>
        /// <returns>This builder for method chaining.</returns>
        public BusinessRuleBuilder WithTags(params string[] tags)
        {
            _tags.AddRange(tags);
            return this;
        }

        /// <summary>
        /// Sets the condition that determines if the rule is broken.
        /// </summary>
        /// <param name="isBrokenCondition">A function that returns true when the rule is broken.</param>
        /// <returns>This builder for method chaining.</returns>
        public BusinessRuleBuilder When(Func<bool> isBrokenCondition)
        {
            _isBrokenFunc = isBrokenCondition;
            return this;
        }

        /// <summary>
        /// Sets the condition with context that determines if the rule is broken.
        /// </summary>
        /// <param name="isBrokenCondition">A function that receives context and returns true when the rule is broken.</param>
        /// <returns>This builder for method chaining.</returns>
        public BusinessRuleBuilder When(Func<BusinessRuleContext, bool> isBrokenCondition)
        {
            _isBrokenWithContextFunc = isBrokenCondition;
            return this;
        }

        /// <summary>
        /// Builds and returns the configured business rule.
        /// </summary>
        /// <returns>A new business rule instance with the configured settings.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no condition has been specified via <see cref="When(Func{bool})"/> or <see cref="When(Func{BusinessRuleContext, bool})"/>.</exception>
        public IBusinessRule Build()
        {
            if (_isBrokenFunc == null && _isBrokenWithContextFunc == null)
                throw new InvalidOperationException("Must specify a condition using When()");

            return new BuiltBusinessRule(
                _code,
                _message,
                _severity,
                _name,
                _description,
                _category,
                _tags.AsReadOnly(),
                _isBrokenFunc,
                _isBrokenWithContextFunc);
        }

        private sealed class BuiltBusinessRule : IBusinessRule
        {
            private readonly Func<bool>? _isBrokenFunc;
            private readonly Func<BusinessRuleContext, bool>? _isBrokenWithContextFunc;

            public BuiltBusinessRule(
                string code,
                string message,
                RuleSeverity severity,
                string name,
                string description,
                string category,
                IReadOnlyList<string> tags,
                Func<bool>? isBrokenFunc,
                Func<BusinessRuleContext, bool>? isBrokenWithContextFunc)
            {
                Code = code;
                Message = message;
                Severity = severity;
                Name = string.IsNullOrEmpty(name) ? code : name;
                Description = description;
                Category = category;
                Tags = tags;
                _isBrokenFunc = isBrokenFunc;
                _isBrokenWithContextFunc = isBrokenWithContextFunc;
            }

            public string Code { get; }
            public string Message { get; }
            public RuleSeverity Severity { get; }
            public string Name { get; }
            public string Description { get; }
            public string Category { get; }
            public IReadOnlyList<string> Tags { get; }

            public bool IsBroken()
            {
                if (_isBrokenFunc != null)
                    return _isBrokenFunc();
                if (_isBrokenWithContextFunc != null)
                    return _isBrokenWithContextFunc(new BusinessRuleContext());
                return false;
            }

            public bool IsBroken(BusinessRuleContext context)
            {
                if (_isBrokenWithContextFunc != null)
                    return _isBrokenWithContextFunc(context);
                if (_isBrokenFunc != null)
                    return _isBrokenFunc();
                return false;
            }

            public void Check()
            {
                if (IsBroken())
                    throw new BusinessRuleValidationException(new[] { Evaluate() });
            }

            public void Check(BusinessRuleContext context)
            {
                if (IsBroken(context))
                    throw new BusinessRuleValidationException(new[] { Evaluate() });
            }

            public BusinessRuleResult Evaluate() => new BusinessRuleResult(Code, Message, Severity);
        }
    }
}
