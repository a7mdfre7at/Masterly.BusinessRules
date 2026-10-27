using System;
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Masterly.BusinessRules.FeatureManagement
{
    /// <summary>
    /// The documented convention that turns a <c>Microsoft.FeatureManagement</c> variant into a <see cref="RuleRollout"/>.
    /// A variant is read in two ways, configuration first and name second:
    /// </summary>
    /// <remarks>
    /// <para><b>Configuration.</b> A variant whose <c>configuration_value</c> (or referenced section) has a
    /// <see cref="ModeKey"/> entry maps to that mode; the optional <see cref="PercentKey"/> entry (0..100, default 100)
    /// is the rollout percent. A scalar configuration value is parsed like a variant name.</para>
    /// <para><b>Name.</b> A variant named after a <see cref="RuleRolloutMode"/> (case-insensitive) maps to that mode at
    /// 100%; an optional percent follows one of the separators <c>-</c>, <c>:</c>, <c>_</c>, <c>/</c> or a space and may
    /// carry a trailing <c>%</c>: <c>Shadow</c>, <c>Enforced-10</c>, <c>enforced:25%</c>, <c>Off</c>. An <c>Off</c> mode always
    /// maps to the canonical <see cref="RuleRollout.Off"/> (0%), whatever percent it carries.</para>
    /// <para>The percent is the in-house <see cref="RuleRolloutBucket"/> cohort of the gated rule's stable key. When the
    /// flag already ramps through a <c>Microsoft.Targeting</c> or <c>Microsoft.Percentage</c> filter, leave the variant at
    /// 100% so callers are not bucketed twice.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// RolloutVariantConvention.TryParse("Shadow-10", out RuleRollout rollout);   // Shadow 10%
    /// RolloutVariantConvention.Parse("enforced");                                 // Enforced 100%
    /// </code>
    /// </example>
    public static class RolloutVariantConvention
    {
        /// <summary>
        /// The configuration key holding the <see cref="RuleRolloutMode"/> name.
        /// </summary>
        public const string ModeKey = "Mode";

        /// <summary>
        /// The configuration key holding the rollout percent (0..100); absent means <see cref="RuleRollout.MaxPercent"/>.
        /// </summary>
        public const string PercentKey = "Percent";

        /// <summary>
        /// The suffix a percent in a variant name may carry.
        /// </summary>
        public const char PercentSuffix = '%';

        /// <summary>
        /// The characters that may separate the mode from the percent in a variant name.
        /// </summary>
        public const string Separators = "-:_/ ";

        internal const string Examples = "'Shadow', 'Enforced-10', 'enforced:25%', 'Off'";

        private static readonly char[] SeparatorChars = Separators.ToCharArray();

        /// <summary>
        /// Parses a variant name into a rollout.
        /// </summary>
        /// <param name="variantName">The variant name, for example <c>Shadow</c> or <c>Enforced-10</c>.</param>
        /// <returns>The rollout the name describes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="variantName"/> is null.</exception>
        /// <exception cref="FormatException">Thrown when the name does not follow the convention.</exception>
        public static RuleRollout Parse(string variantName)
        {
            if (variantName == null)
            {
                throw new ArgumentNullException(nameof(variantName));
            }

            if (TryParse(variantName, out RuleRollout rollout))
            {
                return rollout;
            }

            throw new FormatException($"'{variantName}' does not describe a rule rollout; expected a RuleRolloutMode name with an optional percent, for example {Examples}.");
        }

        /// <summary>
        /// Tries to parse a variant name into a rollout.
        /// </summary>
        /// <param name="variantName">The variant name; null, empty and white space are rejected.</param>
        /// <param name="rollout">The rollout, or <see cref="RuleRollout.Off"/> when the name does not follow the convention.</param>
        /// <returns>True when the name follows the convention.</returns>
        public static bool TryParse(string? variantName, out RuleRollout rollout)
        {
            rollout = RuleRollout.Off;

            if (string.IsNullOrWhiteSpace(variantName))
            {
                return false;
            }

            string text = variantName!.Trim(); // non-null: IsNullOrWhiteSpace returned false
            int separatorIndex = text.IndexOfAny(SeparatorChars);
            string modeText = separatorIndex < 0 ? text : text.Substring(0, separatorIndex);
            string? percentText = separatorIndex < 0 ? null : text.Substring(separatorIndex + 1);

            if (!TryParseMode(modeText, out RuleRolloutMode mode))
            {
                return false;
            }

            if (percentText == null)
            {
                rollout = Create(mode, RuleRollout.MaxPercent);
                return true;
            }

            if (!TryParsePercent(percentText, out int percent))
            {
                return false;
            }

            rollout = Create(mode, percent);
            return true;
        }

        /// <summary>
        /// Tries to read a rollout from a variant's configuration section: a <see cref="ModeKey"/> entry with an
        /// optional <see cref="PercentKey"/> entry, or a scalar value parsed like a variant name.
        /// </summary>
        /// <param name="configuration">The variant's configuration, or null.</param>
        /// <param name="rollout">The rollout, or <see cref="RuleRollout.Off"/> when the section describes none.</param>
        /// <returns>True when the section describes a rollout.</returns>
        public static bool TryRead(IConfigurationSection? configuration, out RuleRollout rollout)
        {
            rollout = RuleRollout.Off;

            if (configuration == null)
            {
                return false;
            }

            string? modeText = configuration[ModeKey];

            if (modeText != null)
            {
                return TryReadStructured(modeText, configuration[PercentKey], out rollout);
            }

            return TryParse(configuration.Value, out rollout);
        }

        private static bool TryReadStructured(string modeText, string? percentText, out RuleRollout rollout)
        {
            rollout = RuleRollout.Off;

            if (!TryParseMode(modeText.Trim(), out RuleRolloutMode mode))
            {
                return false;
            }

            int percent = RuleRollout.MaxPercent;

            if (!string.IsNullOrWhiteSpace(percentText) && !TryParsePercent(percentText!, out percent)) // non-null: IsNullOrWhiteSpace returned false
            {
                return false;
            }

            rollout = Create(mode, percent);
            return true;
        }

        /// <summary>
        /// An <see cref="RuleRolloutMode.Off"/> mode is always the canonical <see cref="RuleRollout.Off"/> (0%), whatever
        /// percent the variant carried, so it compares equal to what the core reports for an unknown flag.
        /// </summary>
        private static RuleRollout Create(RuleRolloutMode mode, int percent)
        {
            return mode == RuleRolloutMode.Off ? RuleRollout.Off : new RuleRollout(mode, percent);
        }

        private static bool TryParseMode(string modeText, out RuleRolloutMode mode)
        {
            if (string.Equals(modeText, nameof(RuleRolloutMode.Off), StringComparison.OrdinalIgnoreCase))
            {
                mode = RuleRolloutMode.Off;
                return true;
            }

            if (string.Equals(modeText, nameof(RuleRolloutMode.Shadow), StringComparison.OrdinalIgnoreCase))
            {
                mode = RuleRolloutMode.Shadow;
                return true;
            }

            if (string.Equals(modeText, nameof(RuleRolloutMode.Enforced), StringComparison.OrdinalIgnoreCase))
            {
                mode = RuleRolloutMode.Enforced;
                return true;
            }

            mode = RuleRolloutMode.Off;
            return false;
        }

        private static bool TryParsePercent(string percentText, out int percent)
        {
            string digits = percentText.Trim();

            if (digits.Length > 0 && digits[digits.Length - 1] == PercentSuffix)
            {
                digits = digits.Substring(0, digits.Length - 1).Trim();
            }

            return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out percent)
                && percent >= RuleRollout.MinPercent
                && percent <= RuleRollout.MaxPercent;
        }
    }
}
