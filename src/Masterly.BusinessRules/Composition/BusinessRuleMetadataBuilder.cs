using System;
using System.Collections.Generic;
using System.Linq;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The shared fluent surface of every rule builder: the seven metadata members (<c>WithMessage</c>,
    /// <c>WithSeverity</c>, <c>WithName</c>, <c>WithDescription</c>, <c>WithCategory</c>, <c>WithTags</c>) and the
    /// context-aware violation details (<c>WithDetail</c>, <c>WithActual</c>, <c>WithExpected</c>, <c>WithRemediation</c>)
    /// produced only on the broken path. <see cref="BusinessRuleBuilder"/>, <see cref="AsyncBusinessRuleBuilder"/>,
    /// <see cref="BusinessRuleBuilder{TSubject}"/> and <see cref="AsyncBusinessRuleBuilder{TSubject}"/> derive from it
    /// and add only their condition (<c>When</c>/<c>WhenAsync</c>) and <c>Build</c>. Each method returns the concrete
    /// builder so chains keep their type. This class cannot be derived from outside the library.
    /// </summary>
    /// <typeparam name="TBuilder">The concrete builder type returned by every fluent method.</typeparam>
    public abstract class BusinessRuleMetadataBuilder<TBuilder> where TBuilder : BusinessRuleMetadataBuilder<TBuilder>
    {
        private readonly string _code;
        private readonly List<string> _tags = new List<string>();
        private string _message;
        private RuleSeverity _severity = RuleSeverity.Error;
        private string _name = string.Empty;
        private string _description = string.Empty;
        private string _category = string.Empty;
        private Func<BusinessRuleContext, FormattableString>? _detail;
        private Func<BusinessRuleContext, object?>? _actual;
        private Func<BusinessRuleContext, object?>? _expected;
        private Func<BusinessRuleContext, string>? _remediation;

        /// <summary>
        /// Initializes the builder with the rule code and the message used when <see cref="WithMessage"/> is never called.
        /// </summary>
        /// <param name="code">The unique code identifying the rule.</param>
        /// <param name="defaultMessage">The violation message used until <see cref="WithMessage"/> replaces it.</param>
        private protected BusinessRuleMetadataBuilder(string code, string defaultMessage)
        {
            _code = code;
            _message = defaultMessage;
        }

        /// <summary>
        /// Gets this builder typed as the concrete builder, for fluent returns.
        /// </summary>
        protected TBuilder Self => (TBuilder)this;

        /// <summary>
        /// Sets the human-readable message describing why the rule is broken.
        /// </summary>
        /// <param name="message">The violation message.</param>
        /// <returns>This builder for method chaining.</returns>
        public TBuilder WithMessage(string message)
        {
            _message = message;
            return Self;
        }

        /// <summary>
        /// Sets the severity level of the rule.
        /// </summary>
        /// <param name="severity">The severity level.</param>
        /// <returns>This builder for method chaining.</returns>
        public TBuilder WithSeverity(RuleSeverity severity)
        {
            _severity = severity;
            return Self;
        }

        /// <summary>
        /// Sets the human-readable name for the rule. An empty name defaults to the code.
        /// </summary>
        /// <param name="name">The rule name.</param>
        /// <returns>This builder for method chaining.</returns>
        public TBuilder WithName(string name)
        {
            _name = name;
            return Self;
        }

        /// <summary>
        /// Sets a detailed description of what the rule validates.
        /// </summary>
        /// <param name="description">The rule description.</param>
        /// <returns>This builder for method chaining.</returns>
        public TBuilder WithDescription(string description)
        {
            _description = description;
            return Self;
        }

        /// <summary>
        /// Sets the category for grouping related rules.
        /// </summary>
        /// <param name="category">The category name.</param>
        /// <returns>This builder for method chaining.</returns>
        public TBuilder WithCategory(string category)
        {
            _category = category;
            return Self;
        }

        /// <summary>
        /// Adds tags for filtering and categorizing rules.
        /// </summary>
        /// <param name="tags">The tags to add.</param>
        /// <returns>This builder for method chaining.</returns>
        public TBuilder WithTags(params string[] tags)
        {
            _tags.AddRange(tags);
            return Self;
        }

        /// <summary>
        /// Sets the detail sentence attached to the result when the rule is broken. Write it as an interpolated string;
        /// its placeholders are formatted with <see cref="BusinessRuleContext.FormatProvider"/> (invariant by default).
        /// The function runs only on the broken path; if it throws, the result is built without details.
        /// </summary>
        /// <param name="detail">A function that receives the context and returns the detail sentence.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="detail"/> is null.</exception>
        public TBuilder WithDetail(Func<BusinessRuleContext, FormattableString> detail)
        {
            _detail = detail ?? throw new ArgumentNullException(nameof(detail));
            return Self;
        }

        /// <summary>
        /// Sets the actual value attached to the result when the rule is broken.
        /// The function runs only on the broken path; if it throws, the result is built without details.
        /// </summary>
        /// <param name="actual">A function that receives the context and returns the actual value.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> is null.</exception>
        public TBuilder WithActual(Func<BusinessRuleContext, object?> actual)
        {
            _actual = actual ?? throw new ArgumentNullException(nameof(actual));
            return Self;
        }

        /// <summary>
        /// Sets the expected value attached to the result when the rule is broken.
        /// The function runs only on the broken path; if it throws, the result is built without details.
        /// </summary>
        /// <param name="expected">A function that receives the context and returns the expected value or range.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="expected"/> is null.</exception>
        public TBuilder WithExpected(Func<BusinessRuleContext, object?> expected)
        {
            _expected = expected ?? throw new ArgumentNullException(nameof(expected));
            return Self;
        }

        /// <summary>
        /// Sets a constant expected value attached to the result when the rule is broken.
        /// </summary>
        /// <param name="expected">The expected value or range.</param>
        /// <returns>This builder for method chaining.</returns>
        public TBuilder WithExpected(object? expected)
        {
            _expected = _ => expected;
            return Self;
        }

        /// <summary>
        /// Sets a constant remediation hint attached to the result when the rule is broken.
        /// </summary>
        /// <param name="remediation">The hint telling the caller how to satisfy the rule.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="remediation"/> is null.</exception>
        public TBuilder WithRemediation(string remediation)
        {
            if (remediation == null)
            {
                throw new ArgumentNullException(nameof(remediation));
            }

            _remediation = _ => remediation;
            return Self;
        }

        /// <summary>
        /// Sets the remediation hint attached to the result when the rule is broken.
        /// The function runs only on the broken path; if it throws, the result is built without details.
        /// </summary>
        /// <param name="remediation">A function that receives the context and returns the hint.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="remediation"/> is null.</exception>
        public TBuilder WithRemediation(Func<BusinessRuleContext, string> remediation)
        {
            _remediation = remediation ?? throw new ArgumentNullException(nameof(remediation));
            return Self;
        }

        /// <summary>
        /// Snapshots the configured metadata for the rule being built. The tags are copied, so a rule built here is
        /// unaffected by later <see cref="WithTags"/> calls on the same builder.
        /// </summary>
        internal BuiltRuleMetadata BuildMetadata()
        {
            return new BuiltRuleMetadata(_code, _message, _severity, _name, _description, _category, _tags.ToArray());
        }

        /// <summary>
        /// Snapshots the configured context-aware detail functions for the rule being built.
        /// </summary>
        internal BuiltRuleDescriptor BuildDescriptor()
        {
            return new BuiltRuleDescriptor(_detail, _actual, _expected, _remediation);
        }
    }
}
