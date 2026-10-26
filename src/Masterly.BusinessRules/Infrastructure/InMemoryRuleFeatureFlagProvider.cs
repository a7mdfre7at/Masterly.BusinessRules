using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// A thread-safe, in-memory <see cref="IRuleFeatureFlagProvider"/> and <see cref="IAsyncRuleFeatureFlagProvider"/>
    /// for tests, local development and small deployments. Flags are set with <see cref="Set(string, RuleRollout)"/>
    /// and can be flipped at any time; a flag that was never set reports <see cref="DefaultRollout"/>
    /// (<see cref="RuleRollout.Off"/> unless configured otherwise). Flag names are compared ordinally.
    /// </summary>
    /// <example>
    /// <code>
    /// InMemoryRuleFeatureFlagProvider flags = new InMemoryRuleFeatureFlagProvider()
    ///     .Set("credit-score-v2", RuleRolloutMode.Shadow)
    ///     .Set("sanctions-check", RuleRolloutMode.Enforced, percent: 10);
    ///
    /// flags.Set("credit-score-v2", RuleRolloutMode.Enforced);   // ramp without touching the rule
    /// </code>
    /// </example>
    public sealed class InMemoryRuleFeatureFlagProvider : IRuleFeatureFlagProvider, IAsyncRuleFeatureFlagProvider
    {
        private readonly ConcurrentDictionary<string, RuleRollout> _rollouts = new ConcurrentDictionary<string, RuleRollout>(StringComparer.Ordinal);

        /// <summary>
        /// Initializes an empty provider that reports <see cref="RuleRollout.Off"/> for unknown flags.
        /// </summary>
        public InMemoryRuleFeatureFlagProvider()
            : this(RuleRollout.Off)
        {
        }

        /// <summary>
        /// Initializes an empty provider that reports <paramref name="defaultRollout"/> for unknown flags.
        /// </summary>
        /// <param name="defaultRollout">The rollout reported for flags that were never set.</param>
        public InMemoryRuleFeatureFlagProvider(RuleRollout defaultRollout)
        {
            DefaultRollout = defaultRollout;
        }

        /// <summary>
        /// Gets the rollout reported for flags that were never set.
        /// </summary>
        public RuleRollout DefaultRollout { get; }

        /// <summary>
        /// Gets the number of flags that have been set.
        /// </summary>
        public int Count => _rollouts.Count;

        /// <summary>
        /// Sets or replaces the rollout of a flag.
        /// </summary>
        /// <param name="flag">The feature flag.</param>
        /// <param name="rollout">The rollout to report for it.</param>
        /// <returns>This provider, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="flag"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="flag"/> is empty or white space.</exception>
        public InMemoryRuleFeatureFlagProvider Set(string flag, RuleRollout rollout)
        {
            _rollouts[RequireFlag(flag)] = rollout;
            return this;
        }

        /// <summary>
        /// Sets or replaces the rollout of a flag from a mode and a percentage.
        /// </summary>
        /// <param name="flag">The feature flag.</param>
        /// <param name="mode">The mode the gated rule runs in for callers inside the cohort.</param>
        /// <param name="percent">The percentage of stable keys inside the cohort. Defaults to everybody.</param>
        /// <returns>This provider, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="flag"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="flag"/> is empty or white space.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mode"/> is undefined or <paramref name="percent"/> is outside 0..100.</exception>
        public InMemoryRuleFeatureFlagProvider Set(string flag, RuleRolloutMode mode, int percent = RuleRollout.MaxPercent)
        {
            return Set(flag, new RuleRollout(mode, percent));
        }

        /// <summary>
        /// Removes a flag, so it reports <see cref="DefaultRollout"/> again.
        /// </summary>
        /// <param name="flag">The feature flag.</param>
        /// <returns>True when the flag was set and has been removed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="flag"/> is null.</exception>
        public bool Remove(string flag)
        {
            if (flag == null)
            {
                throw new ArgumentNullException(nameof(flag));
            }

            return _rollouts.TryRemove(flag, out _);
        }

        /// <summary>
        /// Tries to read the rollout that was explicitly set for a flag.
        /// </summary>
        /// <param name="flag">The feature flag.</param>
        /// <param name="rollout">The rollout, or <c>default</c> when the flag was never set.</param>
        /// <returns>True when the flag was set.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="flag"/> is null.</exception>
        public bool TryGet(string flag, out RuleRollout rollout)
        {
            if (flag == null)
            {
                throw new ArgumentNullException(nameof(flag));
            }

            return _rollouts.TryGetValue(flag, out rollout);
        }

        /// <inheritdoc />
        /// <remarks>The context is not used; the provider has no targeting beyond the rollout percentage.</remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="flag"/> is null.</exception>
        public RuleRollout GetRollout(string flag, BusinessRuleContext context)
        {
            if (flag == null)
            {
                throw new ArgumentNullException(nameof(flag));
            }

            return _rollouts.TryGetValue(flag, out RuleRollout rollout) ? rollout : DefaultRollout;
        }

        /// <inheritdoc />
        /// <remarks>Completes synchronously with <see cref="GetRollout"/>; a cancelled token throws before the lookup.</remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="flag"/> is null.</exception>
        public Task<RuleRollout> GetRolloutAsync(string flag, BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(GetRollout(flag, context));
        }

        private static string RequireFlag(string flag)
        {
            if (flag == null)
            {
                throw new ArgumentNullException(nameof(flag));
            }

            if (string.IsNullOrWhiteSpace(flag))
            {
                throw new ArgumentException("The feature flag name cannot be empty or white space.", nameof(flag));
            }

            return flag;
        }
    }
}
