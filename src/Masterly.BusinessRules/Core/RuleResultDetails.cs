using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Immutable bag of rich violation details that a rule attaches to its <see cref="BusinessRuleResult"/> through
    /// <c>Describe(context)</c>: a formatted <c>Detail</c> sentence, the <c>Actual</c> value that broke the rule, the
    /// <c>Expected</c> value or range, a <c>Remediation</c> hint and a free-form <see cref="Data"/> bag.
    /// Start a chain with <see cref="Detail(FormattableString)"/> or <see cref="Empty"/>; every instance method returns a
    /// new instance, so a shared <see cref="Empty"/> is never mutated.
    /// </summary>
    /// <remarks>
    /// The detail is kept as a <see cref="FormattableString"/> and formatted when the result is built, with the
    /// <see cref="IFormatProvider"/> of the <see cref="BusinessRuleContext"/> when one is set and
    /// <see cref="CultureInfo.InvariantCulture"/> otherwise. <see cref="ActualValue"/> and <see cref="ExpectedValue"/> are
    /// kept as <see cref="object"/> and are serialised by their runtime type.
    /// </remarks>
    /// <example>
    /// <code>
    /// protected override RuleResultDetails Describe(BusinessRuleContext? context) => RuleResultDetails
    ///     .Detail($"Amount {_amount:C} exceeds limit {_limit:C}")
    ///     .Actual(_amount)
    ///     .Expected($"&lt;= {_limit}")
    ///     .Remediation("Reduce the amount or request a limit increase.")
    ///     .With("currency", "USD");
    /// </code>
    /// </example>
    public sealed class RuleResultDetails
    {
        private static readonly IReadOnlyDictionary<string, object?> NoData =
            new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(0));

        private readonly FormattableString? _detail;
        private readonly object? _actual;
        private readonly object? _expected;
        private readonly string? _remediation;
        private readonly IReadOnlyDictionary<string, object?> _data;

        private RuleResultDetails(
            FormattableString? detail,
            object? actual,
            object? expected,
            string? remediation,
            IReadOnlyDictionary<string, object?> data)
        {
            _detail = detail;
            _actual = actual;
            _expected = expected;
            _remediation = remediation;
            _data = data;
        }

        /// <summary>
        /// Gets the details instance that carries nothing: the default returned by <c>Describe</c>.
        /// </summary>
        public static RuleResultDetails Empty { get; } = new RuleResultDetails(null, null, null, null, NoData);

        /// <summary>
        /// Gets the detail sentence as the <see cref="FormattableString"/> it was written as, or null when none was set.
        /// Use <see cref="FormatDetail"/> to obtain the finished text.
        /// </summary>
        public FormattableString? DetailFormat => _detail;

        /// <summary>
        /// Gets the actual value that broke the rule, or null when none was set.
        /// </summary>
        public object? ActualValue => _actual;

        /// <summary>
        /// Gets the expected value or range, or null when none was set.
        /// </summary>
        public object? ExpectedValue => _expected;

        /// <summary>
        /// Gets the remediation hint, or null when none was set.
        /// </summary>
        public string? RemediationText => _remediation;

        /// <summary>
        /// Gets the free-form data bag added through <see cref="With"/>. Empty when nothing was added.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Data => _data;

        /// <summary>
        /// Gets whether this instance carries no detail, actual, expected, remediation or data.
        /// </summary>
        public bool IsEmpty => _detail == null && _actual == null && _expected == null && _remediation == null && _data.Count == 0;

        /// <summary>
        /// Starts a details chain with a detail sentence written as an interpolated string.
        /// </summary>
        /// <param name="detail">The detail sentence; its placeholders are formatted when the result is built.</param>
        /// <returns>A new details instance carrying only the detail.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="detail"/> is null.</exception>
        public static RuleResultDetails Detail(FormattableString detail)
        {
            if (detail == null)
            {
                throw new ArgumentNullException(nameof(detail));
            }

            return new RuleResultDetails(detail, null, null, null, NoData);
        }

        /// <summary>
        /// Returns a copy of this instance carrying the actual value that broke the rule.
        /// </summary>
        /// <param name="actual">The actual value; may be null to clear it.</param>
        /// <returns>A new details instance.</returns>
        public RuleResultDetails Actual(object? actual)
        {
            return new RuleResultDetails(_detail, actual, _expected, _remediation, _data);
        }

        /// <summary>
        /// Returns a copy of this instance carrying the expected value or range.
        /// </summary>
        /// <param name="expected">The expected value; may be null to clear it.</param>
        /// <returns>A new details instance.</returns>
        public RuleResultDetails Expected(object? expected)
        {
            return new RuleResultDetails(_detail, _actual, expected, _remediation, _data);
        }

        /// <summary>
        /// Returns a copy of this instance carrying a remediation hint.
        /// </summary>
        /// <param name="remediation">The hint telling the caller how to satisfy the rule.</param>
        /// <returns>A new details instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="remediation"/> is null.</exception>
        public RuleResultDetails Remediation(string remediation)
        {
            if (remediation == null)
            {
                throw new ArgumentNullException(nameof(remediation));
            }

            return new RuleResultDetails(_detail, _actual, _expected, remediation, _data);
        }

        /// <summary>
        /// Returns a copy of this instance with one more entry in the <see cref="Data"/> bag; an existing key is replaced.
        /// </summary>
        /// <param name="key">The entry key.</param>
        /// <param name="value">The entry value; may be null.</param>
        /// <returns>A new details instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
        public RuleResultDetails With(string key, object? value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            Dictionary<string, object?> copy = new Dictionary<string, object?>(_data.Count + 1);
            foreach (KeyValuePair<string, object?> entry in _data)
            {
                copy[entry.Key] = entry.Value;
            }

            copy[key] = value;
            return new RuleResultDetails(_detail, _actual, _expected, _remediation, new ReadOnlyDictionary<string, object?>(copy));
        }

        /// <summary>
        /// Formats the detail sentence with the supplied provider.
        /// </summary>
        /// <param name="formatProvider">The provider to format placeholders with; null means <see cref="CultureInfo.InvariantCulture"/>.</param>
        /// <returns>The finished detail text, or null when no detail was set.</returns>
        public string? FormatDetail(IFormatProvider? formatProvider)
        {
            return _detail?.ToString(formatProvider ?? CultureInfo.InvariantCulture);
        }
    }
}
