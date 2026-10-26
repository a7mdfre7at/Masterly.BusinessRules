using System;
using System.Text;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Maps a stable key to one of <see cref="BucketCount"/> buckets (0..99) with the 32-bit FNV-1a hash, kept
    /// in-house so the library stays dependency-free. The same flag and key always land in the same bucket, so a
    /// customer who is inside a 10% cohort stays inside it when the rollout grows to 20%; the flag name is mixed into
    /// the hash so that different flags ramp over different cohorts instead of always hitting the same customers first.
    /// </summary>
    /// <example>
    /// <code>
    /// int bucket = RuleRolloutBucket.Compute("credit-score-v2", customerId);   // 0..99
    /// bool inCohort = RuleRollout.Enforced(percent: 10).Includes(bucket);
    /// </code>
    /// </example>
    public static class RuleRolloutBucket
    {
        /// <summary>
        /// The number of buckets. A <see cref="RuleRollout.Percent"/> of <c>n</c> includes buckets <c>0</c> to <c>n - 1</c>.
        /// </summary>
        public const int BucketCount = 100;

        /// <summary>
        /// The separator placed between the flag and the stable key before hashing.
        /// </summary>
        public const char Separator = ':';

        private const uint FnvOffsetBasis = 2166136261;
        private const uint FnvPrime = 16777619;

        /// <summary>
        /// Computes the bucket of <paramref name="stableKey"/> for <paramref name="flag"/>:
        /// <c>Hash(flag + ":" + stableKey) % 100</c>.
        /// </summary>
        /// <param name="flag">The feature flag the rollout belongs to.</param>
        /// <param name="stableKey">The stable key identifying the caller, for example a customer id.</param>
        /// <returns>A bucket from 0 to <see cref="BucketCount"/> - 1.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="flag"/> or <paramref name="stableKey"/> is null.</exception>
        public static int Compute(string flag, string stableKey)
        {
            if (flag == null)
            {
                throw new ArgumentNullException(nameof(flag));
            }

            if (stableKey == null)
            {
                throw new ArgumentNullException(nameof(stableKey));
            }

            return (int)(Hash($"{flag}{Separator}{stableKey}") % BucketCount);
        }

        /// <summary>
        /// Computes the 32-bit FNV-1a hash of the UTF-8 bytes of <paramref name="text"/>.
        /// </summary>
        /// <param name="text">The text to hash.</param>
        /// <returns>The FNV-1a hash.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public static uint Hash(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            byte[] bytes = Encoding.UTF8.GetBytes(text);
            uint hash = FnvOffsetBasis;

            foreach (byte value in bytes)
            {
                unchecked
                {
                    hash ^= value;
                    hash *= FnvPrime;
                }
            }

            return hash;
        }
    }
}
