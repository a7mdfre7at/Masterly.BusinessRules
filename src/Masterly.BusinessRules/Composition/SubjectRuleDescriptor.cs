using System;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The detail functions captured by the subject-typed builders. Subject-aware functions
    /// (<c>(subject, context) => ...</c>) take precedence over the context-only functions inherited from
    /// <see cref="BusinessRuleMetadataBuilder{TBuilder}"/>; on the broken path the subject is bound and the merged
    /// <see cref="BuiltRuleDescriptor"/> builds the result with the usual guard.
    /// </summary>
    /// <typeparam name="TSubject">The subject type of the built rule.</typeparam>
    internal sealed class SubjectRuleDescriptor<TSubject>
    {
        private readonly BuiltRuleDescriptor _contextDescriptor;
        private readonly Func<TSubject, BusinessRuleContext, FormattableString>? _detail;
        private readonly Func<TSubject, BusinessRuleContext, object?>? _actual;
        private readonly Func<TSubject, BusinessRuleContext, object?>? _expected;
        private readonly Func<TSubject, BusinessRuleContext, string>? _remediation;

        /// <summary>
        /// Initializes the descriptor from the context-only descriptor and the optional subject-aware functions.
        /// </summary>
        public SubjectRuleDescriptor(
            BuiltRuleDescriptor contextDescriptor,
            Func<TSubject, BusinessRuleContext, FormattableString>? detail,
            Func<TSubject, BusinessRuleContext, object?>? actual,
            Func<TSubject, BusinessRuleContext, object?>? expected,
            Func<TSubject, BusinessRuleContext, string>? remediation)
        {
            _contextDescriptor = contextDescriptor;
            _detail = detail;
            _actual = actual;
            _expected = expected;
            _remediation = remediation;
        }

        private bool HasSubjectFunctions => _detail != null || _actual != null || _expected != null || _remediation != null;

        /// <summary>
        /// Builds the result for a built subject-typed rule.
        /// </summary>
        public BusinessRuleResult CreateResult(IBusinessRuleMetadata rule, TSubject subject, BusinessRuleContext? context)
        {
            if (!HasSubjectFunctions)
            {
                return _contextDescriptor.CreateResult(rule, context);
            }

            BuiltRuleDescriptor bound = _contextDescriptor.Override(
                Bind(_detail, subject),
                Bind(_actual, subject),
                Bind(_expected, subject),
                Bind(_remediation, subject));

            return bound.CreateResult(rule, context);
        }

        private static Func<BusinessRuleContext, TResult>? Bind<TResult>(Func<TSubject, BusinessRuleContext, TResult>? function, TSubject subject)
        {
            if (function == null)
            {
                return null;
            }

            return context => function(subject, context);
        }
    }
}
