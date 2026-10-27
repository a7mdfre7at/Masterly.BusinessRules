using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Fluent builder for creating subject-typed business rules inline without subclassing. The condition receives
    /// the subject (<see cref="When(Func{TSubject, bool})"/>) or the subject and the context
    /// (<see cref="When(Func{TSubject, BusinessRuleContext, bool})"/>). The metadata and context-only detail methods
    /// are inherited from <see cref="BusinessRuleMetadataBuilder{TBuilder}"/>; the subject-aware
    /// <see cref="WithDetail(Func{TSubject, BusinessRuleContext, FormattableString})"/>,
    /// <see cref="WithActual(Func{TSubject, BusinessRuleContext, object})"/>,
    /// <see cref="WithExpected(Func{TSubject, BusinessRuleContext, object})"/> and
    /// <see cref="WithRemediation(Func{TSubject, BusinessRuleContext, string})"/> overloads take precedence over them.
    /// </summary>
    /// <typeparam name="TSubject">The type of the subject the rule validates.</typeparam>
    /// <example>
    /// <code>
    /// IBusinessRule&lt;Order&gt; rule = BusinessRuleBuilder&lt;Order&gt;.Create("ORDER.TOO_LARGE")
    ///     .WithMessage("Order has too many items.")
    ///     .WithActual((order, ctx) => order.Items.Count)
    ///     .WithExpected(100)
    ///     .When(order => order.Items.Count &gt; 100)
    ///     .Build();
    /// </code>
    /// </example>
    public sealed class BusinessRuleBuilder<TSubject> : BusinessRuleMetadataBuilder<BusinessRuleBuilder<TSubject>>
    {
        private const string DefaultMessage = "Business rule violated.";
        private const string MissingConditionMessage = "Must specify a condition using When()";

        private Func<TSubject, bool>? _isBrokenFunc;
        private Func<TSubject, BusinessRuleContext, bool>? _isBrokenWithContextFunc;
        private Func<TSubject, BusinessRuleContext, RuleParameters, bool>? _isBrokenWithParametersFunc;
        private Func<TSubject, BusinessRuleContext, FormattableString>? _detail;
        private Func<TSubject, BusinessRuleContext, object?>? _actual;
        private Func<TSubject, BusinessRuleContext, object?>? _expected;
        private Func<TSubject, BusinessRuleContext, string>? _remediation;

        private BusinessRuleBuilder(string code) : base(code, DefaultMessage)
        {
        }

        /// <summary>
        /// Creates a new typed rule builder with the specified code.
        /// </summary>
        /// <param name="code">The unique code identifying this rule.</param>
        /// <returns>A new builder instance.</returns>
        public static BusinessRuleBuilder<TSubject> Create(string code)
        {
            return new BusinessRuleBuilder<TSubject>(code);
        }

        /// <summary>
        /// Sets the detail sentence from the subject and the context. Takes precedence over the context-only overload.
        /// The function runs only on the broken path; if it throws, the result is built without details.
        /// </summary>
        /// <param name="detail">A function that receives the subject and the context and returns the detail sentence.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="detail"/> is null.</exception>
        public BusinessRuleBuilder<TSubject> WithDetail(Func<TSubject, BusinessRuleContext, FormattableString> detail)
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
        public BusinessRuleBuilder<TSubject> WithActual(Func<TSubject, BusinessRuleContext, object?> actual)
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
        public BusinessRuleBuilder<TSubject> WithExpected(Func<TSubject, BusinessRuleContext, object?> expected)
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
        public BusinessRuleBuilder<TSubject> WithRemediation(Func<TSubject, BusinessRuleContext, string> remediation)
        {
            _remediation = remediation ?? throw new ArgumentNullException(nameof(remediation));
            return this;
        }

        /// <summary>
        /// Sets the condition on the subject that determines if the rule is broken. It replaces an earlier subject
        /// condition and any parameters condition; a context condition set with
        /// <see cref="When(Func{TSubject, BusinessRuleContext, bool})"/> is kept and still decides the context overloads.
        /// </summary>
        /// <param name="isBrokenCondition">A function that receives the subject and returns true when the rule is broken.</param>
        /// <returns>This builder for method chaining.</returns>
        public BusinessRuleBuilder<TSubject> When(Func<TSubject, bool> isBrokenCondition)
        {
            _isBrokenFunc = isBrokenCondition;
            _isBrokenWithParametersFunc = null;
            return this;
        }

        /// <summary>
        /// Sets the condition on the subject and the context that determines if the rule is broken. It replaces an
        /// earlier context condition and any parameters condition; a subject condition set with
        /// <see cref="When(Func{TSubject, bool})"/> is kept and still decides the context-less overloads.
        /// </summary>
        /// <param name="isBrokenCondition">A function that receives the subject and the context and returns true when the rule is broken.</param>
        /// <returns>This builder for method chaining.</returns>
        public BusinessRuleBuilder<TSubject> When(Func<TSubject, BusinessRuleContext, bool> isBrokenCondition)
        {
            _isBrokenWithContextFunc = isBrokenCondition;
            _isBrokenWithParametersFunc = null;
            return this;
        }

        /// <summary>
        /// Sets the condition on the subject, the context and the resolved parameters that determines if the rule is
        /// broken. The <see cref="RuleParameters"/> hold every parameter declared with
        /// <see cref="BusinessRuleMetadataBuilder{TBuilder}.WithParameter{T}"/>, resolved on every evaluation through the
        /// provider from <see cref="BusinessRuleMetadataBuilder{TBuilder}.WithParameters"/> (or <see cref="RuleParameters.ContextKey"/>)
        /// for the scope under <see cref="RuleParameters.ScopeKey"/>. It replaces every condition set before, and a
        /// later <c>When</c> call replaces it.
        /// </summary>
        /// <param name="isBrokenCondition">A function that receives the subject, the context and the parameters and returns true when the rule is broken.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="isBrokenCondition"/> is null.</exception>
        public BusinessRuleBuilder<TSubject> When(Func<TSubject, BusinessRuleContext, RuleParameters, bool> isBrokenCondition)
        {
            _isBrokenWithParametersFunc = isBrokenCondition ?? throw new ArgumentNullException(nameof(isBrokenCondition));
            _isBrokenFunc = null;
            _isBrokenWithContextFunc = null;
            return this;
        }

        /// <summary>
        /// Builds and returns the configured subject-typed business rule.
        /// </summary>
        /// <returns>A new typed rule instance with the configured settings.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no condition has been specified via <see cref="When(Func{TSubject, bool})"/>, <see cref="When(Func{TSubject, BusinessRuleContext, bool})"/> or <see cref="When(Func{TSubject, BusinessRuleContext, RuleParameters, bool})"/>.</exception>
        public IBusinessRule<TSubject> Build()
        {
            if (_isBrokenFunc == null && _isBrokenWithContextFunc == null && _isBrokenWithParametersFunc == null)
            {
                throw new InvalidOperationException(MissingConditionMessage);
            }

            SubjectRuleDescriptor<TSubject> descriptor = new SubjectRuleDescriptor<TSubject>(BuildDescriptor(), _detail, _actual, _expected, _remediation);
            return new BuiltBusinessRule(BuildMetadata(), _isBrokenFunc, _isBrokenWithContextFunc, _isBrokenWithParametersFunc, BuildParameterBinding(), descriptor);
        }

        private sealed class BuiltBusinessRule : IBusinessRule<TSubject>
        {
            private readonly BuiltRuleMetadata _metadata;
            private readonly Func<TSubject, bool>? _isBrokenFunc;
            private readonly Func<TSubject, BusinessRuleContext, bool>? _isBrokenWithContextFunc;
            private readonly Func<TSubject, BusinessRuleContext, RuleParameters, bool>? _isBrokenWithParametersFunc;
            private readonly RuleParameterBinding _parameters;
            private readonly SubjectRuleDescriptor<TSubject> _descriptor;

            public BuiltBusinessRule(
                BuiltRuleMetadata metadata,
                Func<TSubject, bool>? isBrokenFunc,
                Func<TSubject, BusinessRuleContext, bool>? isBrokenWithContextFunc,
                Func<TSubject, BusinessRuleContext, RuleParameters, bool>? isBrokenWithParametersFunc,
                RuleParameterBinding parameters,
                SubjectRuleDescriptor<TSubject> descriptor)
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

            public bool IsBroken(TSubject subject)
            {
                if (_isBrokenFunc != null)
                {
                    return _isBrokenFunc(subject);
                }

                if (_isBrokenWithContextFunc != null)
                {
                    return _isBrokenWithContextFunc(subject, new BusinessRuleContext());
                }

                if (_isBrokenWithParametersFunc != null)
                {
                    BusinessRuleContext context = new BusinessRuleContext();
                    return _isBrokenWithParametersFunc(subject, context, _parameters.Resolve(context));
                }

                return false;
            }

            public bool IsBroken(TSubject subject, BusinessRuleContext context)
            {
                if (_isBrokenWithParametersFunc != null)
                {
                    return _isBrokenWithParametersFunc(subject, context, _parameters.Resolve(context));
                }

                if (_isBrokenWithContextFunc != null)
                {
                    return _isBrokenWithContextFunc(subject, context);
                }

                if (_isBrokenFunc != null)
                {
                    return _isBrokenFunc(subject);
                }

                return false;
            }

            public BusinessRuleResult Evaluate(TSubject subject) => _descriptor.CreateResult(this, subject, null);

            public BusinessRuleResult Evaluate(TSubject subject, BusinessRuleContext context) => _descriptor.CreateResult(this, subject, context);
        }
    }
}
