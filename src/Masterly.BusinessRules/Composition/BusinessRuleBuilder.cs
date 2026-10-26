using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Fluent builder for creating business rules inline without subclassing.
    /// Provides a convenient way to define rules with full metadata support and, through
    /// <see cref="WithDetail"/>, <see cref="WithActual"/>, <see cref="WithExpected(Func{BusinessRuleContext, object})"/>
    /// and <see cref="WithRemediation(string)"/>, context-aware violation details produced only on the broken path.
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
        private Func<BusinessRuleContext, FormattableString>? _detail;
        private Func<BusinessRuleContext, object?>? _actual;
        private Func<BusinessRuleContext, object?>? _expected;
        private Func<BusinessRuleContext, string>? _remediation;

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
        /// Sets the detail sentence attached to the result when the rule is broken. Write it as an interpolated string;
        /// its placeholders are formatted with <see cref="BusinessRuleContext.FormatProvider"/> (invariant by default).
        /// The function runs only on the broken path; if it throws, the result is built without details.
        /// </summary>
        /// <param name="detail">A function that receives the context and returns the detail sentence.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="detail"/> is null.</exception>
        public BusinessRuleBuilder WithDetail(Func<BusinessRuleContext, FormattableString> detail)
        {
            _detail = detail ?? throw new ArgumentNullException(nameof(detail));
            return this;
        }

        /// <summary>
        /// Sets the actual value attached to the result when the rule is broken.
        /// The function runs only on the broken path; if it throws, the result is built without details.
        /// </summary>
        /// <param name="actual">A function that receives the context and returns the actual value.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> is null.</exception>
        public BusinessRuleBuilder WithActual(Func<BusinessRuleContext, object?> actual)
        {
            _actual = actual ?? throw new ArgumentNullException(nameof(actual));
            return this;
        }

        /// <summary>
        /// Sets the expected value attached to the result when the rule is broken.
        /// The function runs only on the broken path; if it throws, the result is built without details.
        /// </summary>
        /// <param name="expected">A function that receives the context and returns the expected value or range.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="expected"/> is null.</exception>
        public BusinessRuleBuilder WithExpected(Func<BusinessRuleContext, object?> expected)
        {
            _expected = expected ?? throw new ArgumentNullException(nameof(expected));
            return this;
        }

        /// <summary>
        /// Sets a constant expected value attached to the result when the rule is broken.
        /// </summary>
        /// <param name="expected">The expected value or range.</param>
        /// <returns>This builder for method chaining.</returns>
        public BusinessRuleBuilder WithExpected(object? expected)
        {
            _expected = _ => expected;
            return this;
        }

        /// <summary>
        /// Sets a constant remediation hint attached to the result when the rule is broken.
        /// </summary>
        /// <param name="remediation">The hint telling the caller how to satisfy the rule.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="remediation"/> is null.</exception>
        public BusinessRuleBuilder WithRemediation(string remediation)
        {
            if (remediation == null)
            {
                throw new ArgumentNullException(nameof(remediation));
            }

            _remediation = _ => remediation;
            return this;
        }

        /// <summary>
        /// Sets the remediation hint attached to the result when the rule is broken.
        /// The function runs only on the broken path; if it throws, the result is built without details.
        /// </summary>
        /// <param name="remediation">A function that receives the context and returns the hint.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="remediation"/> is null.</exception>
        public BusinessRuleBuilder WithRemediation(Func<BusinessRuleContext, string> remediation)
        {
            _remediation = remediation ?? throw new ArgumentNullException(nameof(remediation));
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
                _isBrokenWithContextFunc,
                new BuiltRuleDescriptor(_detail, _actual, _expected, _remediation));
        }

        private sealed class BuiltBusinessRule : IBusinessRule
        {
            private readonly Func<bool>? _isBrokenFunc;
            private readonly Func<BusinessRuleContext, bool>? _isBrokenWithContextFunc;
            private readonly BuiltRuleDescriptor _descriptor;

            public BuiltBusinessRule(
                string code,
                string message,
                RuleSeverity severity,
                string name,
                string description,
                string category,
                IReadOnlyList<string> tags,
                Func<bool>? isBrokenFunc,
                Func<BusinessRuleContext, bool>? isBrokenWithContextFunc,
                BuiltRuleDescriptor descriptor)
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
                _descriptor = descriptor;
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
                {
                    throw new BusinessRuleValidationException(new[] { Evaluate() });
                }
            }

            public void Check(BusinessRuleContext context)
            {
                if (IsBroken(context))
                {
                    throw new BusinessRuleValidationException(new[] { Evaluate(context) });
                }
            }

            public BusinessRuleResult Evaluate() => _descriptor.CreateResult(this, null);

            public BusinessRuleResult Evaluate(BusinessRuleContext context) => _descriptor.CreateResult(this, context);
        }
    }
}
