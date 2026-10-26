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
        private Func<BusinessRuleContext, RuleParameters, bool>? _isBrokenWithParametersFunc;

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
        /// Sets the condition with context and resolved parameters that determines if the rule is broken. The
        /// <see cref="RuleParameters"/> hold every parameter declared with <see cref="BusinessRuleMetadataBuilder{TBuilder}.WithParameter{T}"/>,
        /// resolved on every evaluation through the provider from <see cref="BusinessRuleMetadataBuilder{TBuilder}.WithParameters"/>
        /// (or <see cref="RuleParameters.ContextKey"/>) for the scope under <see cref="RuleParameters.ScopeKey"/>.
        /// </summary>
        /// <param name="isBrokenCondition">A function that receives the context and the parameters and returns true when the rule is broken.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="isBrokenCondition"/> is null.</exception>
        public BusinessRuleBuilder When(Func<BusinessRuleContext, RuleParameters, bool> isBrokenCondition)
        {
            _isBrokenWithParametersFunc = isBrokenCondition ?? throw new ArgumentNullException(nameof(isBrokenCondition));
            return this;
        }

        /// <summary>
        /// Sets the condition from a textual expression compiled by <paramref name="compiler"/> (the seam an expression
        /// package implements). The expression is compiled now, so a malformed expression fails here rather than at evaluation time.
        /// </summary>
        /// <param name="compiler">The compiler that turns the expression into a broken-condition delegate.</param>
        /// <param name="expression">The condition text.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="compiler"/> or <paramref name="expression"/> is null, or the compiler returned null.</exception>
        public BusinessRuleBuilder When(IRuleConditionCompiler compiler, string expression)
        {
            if (compiler == null)
            {
                throw new ArgumentNullException(nameof(compiler));
            }

            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            _isBrokenWithContextFunc = compiler.Compile(expression) ?? throw new ArgumentNullException(nameof(compiler), "The condition compiler returned a null delegate.");
            return this;
        }

        /// <summary>
        /// Builds and returns the configured business rule.
        /// </summary>
        /// <returns>A new business rule instance with the configured settings.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no condition has been specified via <see cref="When(Func{bool})"/>, <see cref="When(Func{BusinessRuleContext, bool})"/> or <see cref="When(Func{BusinessRuleContext, RuleParameters, bool})"/>.</exception>
        public IBusinessRule Build()
        {
            if (_isBrokenFunc == null && _isBrokenWithContextFunc == null && _isBrokenWithParametersFunc == null)
            {
                throw new InvalidOperationException(MissingConditionMessage);
            }

            return new BuiltBusinessRule(BuildMetadata(), _isBrokenFunc, _isBrokenWithContextFunc, _isBrokenWithParametersFunc, BuildParameterBinding(), BuildDescriptor());
        }

        private sealed class BuiltBusinessRule : IBusinessRule
        {
            private readonly BuiltRuleMetadata _metadata;
            private readonly Func<bool>? _isBrokenFunc;
            private readonly Func<BusinessRuleContext, bool>? _isBrokenWithContextFunc;
            private readonly Func<BusinessRuleContext, RuleParameters, bool>? _isBrokenWithParametersFunc;
            private readonly RuleParameterBinding _parameters;
            private readonly BuiltRuleDescriptor _descriptor;

            public BuiltBusinessRule(
                BuiltRuleMetadata metadata,
                Func<bool>? isBrokenFunc,
                Func<BusinessRuleContext, bool>? isBrokenWithContextFunc,
                Func<BusinessRuleContext, RuleParameters, bool>? isBrokenWithParametersFunc,
                RuleParameterBinding parameters,
                BuiltRuleDescriptor descriptor)
            {
                _metadata = metadata;
                _isBrokenFunc = isBrokenFunc;
                _isBrokenWithContextFunc = isBrokenWithContextFunc;
                _isBrokenWithParametersFunc = isBrokenWithParametersFunc;
                _parameters = parameters;
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

                if (_isBrokenWithParametersFunc != null)
                {
                    BusinessRuleContext context = new BusinessRuleContext();
                    return _isBrokenWithParametersFunc(context, _parameters.Resolve(context));
                }

                return false;
            }

            public bool IsBroken(BusinessRuleContext context)
            {
                if (_isBrokenWithParametersFunc != null)
                {
                    return _isBrokenWithParametersFunc(context, _parameters.Resolve(context));
                }

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
