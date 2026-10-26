using System;
using System.Collections.Generic;
using System.Linq;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Represents the result of a broken business rule evaluation.
    /// This immutable class carries the rule's identity (<see cref="Code"/>, <see cref="Message"/>, <see cref="Severity"/>,
    /// <see cref="Name"/>, <see cref="Category"/>, <see cref="Tags"/>), the rich violation details a rule attached through
    /// <c>Describe</c> (<see cref="Detail"/>, <see cref="Actual"/>, <see cref="Expected"/>, <see cref="Remediation"/>,
    /// <see cref="Data"/>) and the <see cref="Details"/> of the member rules that broke a composed rule (group, composite, And/Or).
    /// It holds no reference to the rule that produced it, so it can be serialised as is.
    /// </summary>
    public sealed class BusinessRuleResult
    {
        private static readonly IReadOnlyList<BusinessRuleResult> NoDetails = Array.Empty<BusinessRuleResult>();
        private static readonly IReadOnlyList<string> NoTags = Array.Empty<string>();

        /// <summary>
        /// Gets the unique code identifying the broken rule.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the human-readable message describing the rule violation.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the severity level of the rule violation.
        /// </summary>
        public RuleSeverity Severity { get; }

        /// <summary>
        /// Gets the human-readable name of the broken rule. Results built without metadata report the <see cref="Code"/>.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the category of the broken rule. Empty when the rule has no category.
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// Gets the tags of the broken rule. Empty when the rule has no tags.
        /// </summary>
        public IReadOnlyList<string> Tags { get; }

        /// <summary>
        /// Gets the formatted detail sentence attached by the rule, or null when none was attached.
        /// </summary>
        public string? Detail { get; }

        /// <summary>
        /// Gets the actual value that broke the rule, or null when none was attached. Serialised by its runtime type.
        /// </summary>
        public object? Actual { get; }

        /// <summary>
        /// Gets the expected value or range, or null when none was attached. Serialised by its runtime type.
        /// </summary>
        public object? Expected { get; }

        /// <summary>
        /// Gets the remediation hint attached by the rule, or null when none was attached.
        /// </summary>
        public string? Remediation { get; }

        /// <summary>
        /// Gets the free-form data bag attached by the rule. Empty when nothing was attached.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Data { get; }

        /// <summary>
        /// Gets the results of the member rules that caused a composed rule to break, in evaluation order.
        /// Empty for a leaf rule. Use <see cref="Flatten"/> or <see cref="BusinessRuleValidationException.Flatten"/>
        /// to reach the leaf results of a nested tree.
        /// </summary>
        public IReadOnlyList<BusinessRuleResult> Details { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessRuleResult"/> class with no metadata beyond the code,
        /// no rich details and no member details. <see cref="Name"/> reports the code.
        /// </summary>
        /// <param name="code">The unique code identifying the broken rule.</param>
        /// <param name="message">The human-readable message describing the rule violation.</param>
        /// <param name="severity">The severity level of the rule violation.</param>
        public BusinessRuleResult(string code, string message, RuleSeverity severity)
            : this(code, message, severity, code, string.Empty, NoTags, RuleResultDetails.Empty, NoDetails, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessRuleResult"/> class carrying member results.
        /// </summary>
        /// <param name="code">The unique code identifying the broken rule.</param>
        /// <param name="message">The human-readable message describing the rule violation.</param>
        /// <param name="severity">The severity level of the rule violation.</param>
        /// <param name="details">The results of the member rules that caused this rule to break; copied, so later changes to the source are not reflected.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="details"/> is null.</exception>
        public BusinessRuleResult(string code, string message, RuleSeverity severity, IEnumerable<BusinessRuleResult> details)
            : this(code, message, severity, code, string.Empty, NoTags, RuleResultDetails.Empty, details, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessRuleResult"/> class carrying rich violation details.
        /// The detail sentence is formatted with the invariant culture.
        /// </summary>
        /// <param name="code">The unique code identifying the broken rule.</param>
        /// <param name="message">The human-readable message describing the rule violation.</param>
        /// <param name="severity">The severity level of the rule violation.</param>
        /// <param name="details">The rich violation details.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="details"/> is null.</exception>
        public BusinessRuleResult(string code, string message, RuleSeverity severity, RuleResultDetails details)
            : this(code, message, severity, code, string.Empty, NoTags, details, NoDetails, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessRuleResult"/> class carrying the rule's metadata.
        /// </summary>
        /// <param name="code">The unique code identifying the broken rule.</param>
        /// <param name="message">The human-readable message describing the rule violation.</param>
        /// <param name="severity">The severity level of the rule violation.</param>
        /// <param name="name">The human-readable name of the rule.</param>
        /// <param name="category">The category of the rule.</param>
        /// <param name="tags">The tags of the rule; the list is referenced, not copied.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tags"/> is null.</exception>
        public BusinessRuleResult(string code, string message, RuleSeverity severity, string name, string category, IReadOnlyList<string> tags)
            : this(code, message, severity, name, category, tags, RuleResultDetails.Empty, NoDetails, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessRuleResult"/> class carrying the rule's metadata, rich
        /// violation details and member results.
        /// </summary>
        /// <param name="code">The unique code identifying the broken rule.</param>
        /// <param name="message">The human-readable message describing the rule violation.</param>
        /// <param name="severity">The severity level of the rule violation.</param>
        /// <param name="name">The human-readable name of the rule.</param>
        /// <param name="category">The category of the rule.</param>
        /// <param name="tags">The tags of the rule; the list is referenced, not copied.</param>
        /// <param name="details">The rich violation details.</param>
        /// <param name="memberDetails">The results of the member rules that caused this rule to break; copied, so later changes to the source are not reflected.</param>
        /// <param name="formatProvider">The provider used to format the detail sentence; null means the invariant culture.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tags"/>, <paramref name="details"/> or <paramref name="memberDetails"/> is null.</exception>
        public BusinessRuleResult(
            string code,
            string message,
            RuleSeverity severity,
            string name,
            string category,
            IReadOnlyList<string> tags,
            RuleResultDetails details,
            IEnumerable<BusinessRuleResult> memberDetails,
            IFormatProvider? formatProvider)
        {
            if (tags == null)
            {
                throw new ArgumentNullException(nameof(tags));
            }

            if (details == null)
            {
                throw new ArgumentNullException(nameof(details));
            }

            if (memberDetails == null)
            {
                throw new ArgumentNullException(nameof(memberDetails));
            }

            Code = code;
            Message = message;
            Severity = severity;
            Name = name;
            Category = category;
            Tags = tags;
            Detail = details.FormatDetail(formatProvider);
            Actual = details.ActualValue;
            Expected = details.ExpectedValue;
            Remediation = details.RemediationText;
            Data = details.Data;
            Details = ReferenceEquals(memberDetails, NoDetails) ? NoDetails : memberDetails.ToArray();
        }

        private BusinessRuleResult(BusinessRuleResult source, RuleSeverity severity)
        {
            Code = source.Code;
            Message = source.Message;
            Severity = severity;
            Name = source.Name;
            Category = source.Category;
            Tags = source.Tags;
            Detail = source.Detail;
            Actual = source.Actual;
            Expected = source.Expected;
            Remediation = source.Remediation;
            Data = source.Data;
            Details = source.Details;
        }

        /// <summary>
        /// Returns a copy of this result that reports a different severity; every other member, including the rich
        /// details and the member <see cref="Details"/>, is kept.
        /// </summary>
        /// <param name="severity">The severity to report.</param>
        /// <returns>A new result, or this instance when the severity is unchanged.</returns>
        public BusinessRuleResult WithSeverity(RuleSeverity severity)
        {
            return severity == Severity ? this : new BusinessRuleResult(this, severity);
        }

        /// <summary>
        /// Returns whether the broken rule carries the tag, compared case-insensitively.
        /// </summary>
        /// <param name="tag">The tag to look for.</param>
        /// <returns>True when <see cref="Tags"/> contains the tag.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tag"/> is null.</exception>
        public bool HasTag(string tag)
        {
            if (tag == null)
            {
                throw new ArgumentNullException(nameof(tag));
            }

            return Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the leaf results of this result: the result itself when it has no <see cref="Details"/>,
        /// otherwise the leaves of every detail, recursively, in evaluation order.
        /// </summary>
        /// <returns>The leaf results.</returns>
        public IEnumerable<BusinessRuleResult> Flatten()
        {
            if (Details.Count == 0)
            {
                yield return this;
                yield break;
            }

            foreach (BusinessRuleResult detail in Details)
            {
                foreach (BusinessRuleResult leaf in detail.Flatten())
                {
                    yield return leaf;
                }
            }
        }
    }
}
