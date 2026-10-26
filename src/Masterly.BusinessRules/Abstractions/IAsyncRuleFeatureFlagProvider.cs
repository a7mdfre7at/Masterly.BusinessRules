using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Supplies the <see cref="RuleRollout"/> of a feature flag to asynchronous feature-gated rules
    /// (<see cref="FeatureGatedAsyncBusinessRule"/>). The rollout is read on every evaluation, so a change in the
    /// provider's configuration takes effect on the next call without rebuilding the rule.
    /// </summary>
    /// <example>
    /// <code>
    /// public sealed class FeatureManagerFlagProvider : IAsyncRuleFeatureFlagProvider
    /// {
    ///     public async Task&lt;RuleRollout&gt; GetRolloutAsync(string flag, BusinessRuleContext context, CancellationToken ct = default)
    ///         => await _featureManager.IsEnabledAsync(flag) ? RuleRollout.Enforced() : RuleRollout.Off;
    /// }
    /// </code>
    /// </example>
    public interface IAsyncRuleFeatureFlagProvider
    {
        /// <summary>
        /// Returns the rollout of <paramref name="flag"/>. An unknown flag should be reported as <see cref="RuleRollout.Off"/>.
        /// </summary>
        /// <param name="flag">The feature flag the gated rule is bound to.</param>
        /// <param name="context">The context of the evaluation; providers may use it for targeting.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task producing the rollout of the flag.</returns>
        Task<RuleRollout> GetRolloutAsync(string flag, BusinessRuleContext context, CancellationToken cancellationToken = default);
    }
}
