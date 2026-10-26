using System;
using System.Globalization;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The rollout state of one feature flag as returned by an <see cref="IRuleFeatureFlagProvider"/>: the
    /// <see cref="Mode"/> the gated rule runs in and the <see cref="Percent"/> of stable keys that are inside the
    /// cohort. A caller whose bucket (see <see cref="RuleRolloutBucket"/>) is below <see cref="Percent"/> is in the
    /// cohort; everybody else sees the gate closed. The default value is <see cref="Off"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// RuleRollout dark = RuleRollout.Shadow(percent: 10);      // observe for 10% of customers, block nobody
    /// RuleRollout live = RuleRollout.Enforced();                // enforce for everybody
    /// RuleRollout none = RuleRollout.Off;
    /// </code>
    /// </example>
    public readonly struct RuleRollout : IEquatable<RuleRollout>
    {
        /// <summary>
        /// The lowest valid <see cref="Percent"/>: nobody is in the cohort.
        /// </summary>
        public const int MinPercent = 0;

        /// <summary>
        /// The highest valid <see cref="Percent"/>: everybody is in the cohort and no stable key is needed.
        /// </summary>
        public const int MaxPercent = 100;

        private const string PercentSuffix = "%";

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleRollout"/> struct.
        /// </summary>
        /// <param name="mode">The mode the gated rule runs in for callers inside the cohort.</param>
        /// <param name="percent">The percentage of stable keys inside the cohort, from <see cref="MinPercent"/> to <see cref="MaxPercent"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mode"/> is not a defined <see cref="RuleRolloutMode"/> or <paramref name="percent"/> is outside 0..100.</exception>
        public RuleRollout(RuleRolloutMode mode, int percent)
        {
            if (!Enum.IsDefined(typeof(RuleRolloutMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "The rollout mode is not a defined RuleRolloutMode value.");
            }

            if (percent < MinPercent || percent > MaxPercent)
            {
                throw new ArgumentOutOfRangeException(nameof(percent), percent, $"The rollout percent must be between {MinPercent} and {MaxPercent}.");
            }

            Mode = mode;
            Percent = percent;
        }

        /// <summary>
        /// Gets the mode the gated rule runs in for callers inside the cohort.
        /// </summary>
        public RuleRolloutMode Mode { get; }

        /// <summary>
        /// Gets the percentage of stable keys inside the cohort (0..100).
        /// </summary>
        public int Percent { get; }

        /// <summary>
        /// Gets the closed gate: <see cref="RuleRolloutMode.Off"/> at 0%. Equal to <c>default(RuleRollout)</c>.
        /// </summary>
        public static RuleRollout Off => default;

        /// <summary>
        /// Gets whether the gate is closed for everybody: the mode is <see cref="RuleRolloutMode.Off"/> or the
        /// percent is <see cref="MinPercent"/>.
        /// </summary>
        public bool IsOff => Mode == RuleRolloutMode.Off || Percent == MinPercent;

        /// <summary>
        /// Gets whether the whole population is inside the cohort, so no stable key is required to decide membership.
        /// </summary>
        public bool IsComplete => Percent == MaxPercent;

        /// <summary>
        /// Creates a <see cref="RuleRolloutMode.Shadow"/> rollout for the given percentage of stable keys.
        /// </summary>
        /// <param name="percent">The percentage of stable keys inside the cohort. Defaults to everybody.</param>
        /// <returns>The rollout.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="percent"/> is outside 0..100.</exception>
        public static RuleRollout Shadow(int percent = MaxPercent)
        {
            return new RuleRollout(RuleRolloutMode.Shadow, percent);
        }

        /// <summary>
        /// Creates an <see cref="RuleRolloutMode.Enforced"/> rollout for the given percentage of stable keys.
        /// </summary>
        /// <param name="percent">The percentage of stable keys inside the cohort. Defaults to everybody.</param>
        /// <returns>The rollout.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="percent"/> is outside 0..100.</exception>
        public static RuleRollout Enforced(int percent = MaxPercent)
        {
            return new RuleRollout(RuleRolloutMode.Enforced, percent);
        }

        /// <summary>
        /// Returns whether a stable key that hashes to <paramref name="bucket"/> is inside the cohort: the gate is
        /// not <see cref="IsOff"/> and the bucket is below <see cref="Percent"/>.
        /// </summary>
        /// <param name="bucket">The bucket of the stable key, from 0 to <see cref="RuleRolloutBucket.BucketCount"/> - 1.</param>
        /// <returns>True when the bucket is inside the cohort.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bucket"/> is outside 0..99.</exception>
        public bool Includes(int bucket)
        {
            if (bucket < 0 || bucket >= RuleRolloutBucket.BucketCount)
            {
                throw new ArgumentOutOfRangeException(nameof(bucket), bucket, $"The bucket must be between 0 and {RuleRolloutBucket.BucketCount - 1}.");
            }

            return !IsOff && bucket < Percent;
        }

        /// <inheritdoc />
        public bool Equals(RuleRollout other)
        {
            return Mode == other.Mode && Percent == other.Percent;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is RuleRollout other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Mode * 397) ^ Percent;
            }
        }

        /// <summary>
        /// Returns the mode and percent, for example <c>Shadow 10%</c>.
        /// </summary>
        /// <returns>The textual form of the rollout.</returns>
        public override string ToString()
        {
            return $"{Mode} {Percent.ToString(CultureInfo.InvariantCulture)}{PercentSuffix}";
        }

        /// <summary>
        /// Compares two rollouts for equality.
        /// </summary>
        /// <param name="left">The first rollout.</param>
        /// <param name="right">The second rollout.</param>
        /// <returns>True when both have the same mode and percent.</returns>
        public static bool operator ==(RuleRollout left, RuleRollout right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two rollouts for inequality.
        /// </summary>
        /// <param name="left">The first rollout.</param>
        /// <param name="right">The second rollout.</param>
        /// <returns>True when the mode or the percent differ.</returns>
        public static bool operator !=(RuleRollout left, RuleRollout right)
        {
            return !left.Equals(right);
        }
    }
}
