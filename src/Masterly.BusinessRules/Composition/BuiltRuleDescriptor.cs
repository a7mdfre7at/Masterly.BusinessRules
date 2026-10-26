using System;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The context-aware detail functions captured by <see cref="BusinessRuleBuilder"/> and
    /// <see cref="AsyncBusinessRuleBuilder"/> (<c>WithDetail</c>, <c>WithActual</c>, <c>WithExpected</c>,
    /// <c>WithRemediation</c>). It plays the role of the <c>Describe</c> hook for built rules: the functions run only
    /// on the broken path and, like an overridden <c>Describe</c>, an exception inside any of them degrades the whole
    /// description to <see cref="RuleResultDetails.Empty"/>.
    /// </summary>
    internal sealed class BuiltRuleDescriptor
    {
        private readonly Func<BusinessRuleContext, FormattableString>? _detail;
        private readonly Func<BusinessRuleContext, object?>? _actual;
        private readonly Func<BusinessRuleContext, object?>? _expected;
        private readonly Func<BusinessRuleContext, string>? _remediation;

        /// <summary>
        /// Initializes a descriptor from the builder's optional functions.
        /// </summary>
        public BuiltRuleDescriptor(
            Func<BusinessRuleContext, FormattableString>? detail,
            Func<BusinessRuleContext, object?>? actual,
            Func<BusinessRuleContext, object?>? expected,
            Func<BusinessRuleContext, string>? remediation)
        {
            _detail = detail;
            _actual = actual;
            _expected = expected;
            _remediation = remediation;
        }

        /// <summary>
        /// Gets whether no detail function was configured, in which case no function is ever invoked.
        /// </summary>
        public bool IsEmpty => _detail == null && _actual == null && _expected == null && _remediation == null;

        /// <summary>
        /// Builds the result for a built synchronous rule. A null context (the parameterless <c>Evaluate()</c> path)
        /// is replaced by an empty context for the detail functions, mirroring how built rules evaluate their
        /// context condition without a context.
        /// </summary>
        public BusinessRuleResult CreateResult(IBusinessRule rule, BusinessRuleContext? context)
        {
            if (IsEmpty)
            {
                return RuleResultFactory.Create(rule);
            }

            return RuleResultFactory.Create(rule, DescribeGuarded(context), context?.FormatProvider);
        }

        /// <summary>
        /// Builds the result for a built asynchronous rule.
        /// </summary>
        public BusinessRuleResult CreateResult(IAsyncBusinessRule rule, BusinessRuleContext? context)
        {
            if (IsEmpty)
            {
                return RuleResultFactory.Create(rule);
            }

            return RuleResultFactory.Create(rule, DescribeGuarded(context), context?.FormatProvider);
        }

        private RuleResultDetails DescribeGuarded(BusinessRuleContext? context)
        {
            BusinessRuleContext effectiveContext = context ?? new BusinessRuleContext();
            return RuleResultFactory.DescribeGuarded(() => Describe(effectiveContext));
        }

        private RuleResultDetails Describe(BusinessRuleContext context)
        {
            RuleResultDetails details = _detail != null ? RuleResultDetails.Detail(_detail(context)) : RuleResultDetails.Empty;

            if (_actual != null)
            {
                details = details.Actual(_actual(context));
            }

            if (_expected != null)
            {
                details = details.Expected(_expected(context));
            }

            if (_remediation != null)
            {
                details = details.Remediation(_remediation(context));
            }

            return details;
        }
    }
}
