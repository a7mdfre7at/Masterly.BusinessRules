using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Fluent builder for creating business rules inline without subclassing.
    /// Provides a convenient way to define rules with full metadata support and, through
    /// <c>WithDetail</c>, <c>WithActual</c>, <c>WithExpected</c> and <c>WithRemediation</c> (inherited from
    /// <see cref="BusinessRuleMetadataBuilder{TBuilder}"/>), context-aware violation details produced only on the broken path.
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
    public sealed class BusinessRuleBuilder : BusinessRuleMetadataBuilder<BusinessRuleBuilder>
    {
        private const string DefaultMessage = "Business rule violated.";
        private const string MissingConditionMessage = "Must specify a condition using When()";

        private Func<bool>? _isBrokenFunc;
        private Func<BusinessRuleContext, bool>? _isBrokenWithContextFunc;

        private BusinessRuleBuilder(string code) : base(code, DefaultMessage)
        {
        }

        /// <summary>
        /// Creates a new rule builder with the specified code.
        /// </summary>
        /// <param name="code">The unique code identifying this rule.</param>
        /// <returns>A new builder instance.</returns>
        public static BusinessRuleBuilder Create(string code)
        {
            return new BusinessRuleBuilder(code);
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
            {
                throw new InvalidOperationException(MissingConditionMessage);
            }

            return new BuiltBusinessRule(BuildMetadata(), _isBrokenFunc, _isBrokenWithContextFunc, BuildDescriptor());
        }

        private sealed class BuiltBusinessRule : IBusinessRule
        {
            private readonly BuiltRuleMetadata _metadata;
            private readonly Func<bool>? _isBrokenFunc;
            private readonly Func<BusinessRuleContext, bool>? _isBrokenWithContextFunc;
            private readonly BuiltRuleDescriptor _descriptor;

            public BuiltBusinessRule(
                BuiltRuleMetadata metadata,
                Func<bool>? isBrokenFunc,
                Func<BusinessRuleContext, bool>? isBrokenWithContextFunc,
                BuiltRuleDescriptor descriptor)
            {
                _metadata = metadata;
                _isBrokenFunc = isBrokenFunc;
                _isBrokenWithContextFunc = isBrokenWithContextFunc;
                _descriptor = descriptor;
            }

            public string Code => _metadata.Code;
            public string Message => _metadata.Message;
            public RuleSeverity Severity => _metadata.Severity;
            public string Name => _metadata.Name;
            public string Description => _metadata.Description;
            public string Category => _metadata.Category;
            public IReadOnlyList<string> Tags => _metadata.Tags;

            public bool IsBroken()
            {
                if (_isBrokenFunc != null)
                {
                    return _isBrokenFunc();
                }

                if (_isBrokenWithContextFunc != null)
                {
                    return _isBrokenWithContextFunc(new BusinessRuleContext());
                }

                return false;
            }

            public bool IsBroken(BusinessRuleContext context)
            {
                if (_isBrokenWithContextFunc != null)
                {
                    return _isBrokenWithContextFunc(context);
                }

                if (_isBrokenFunc != null)
                {
                    return _isBrokenFunc();
                }

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
