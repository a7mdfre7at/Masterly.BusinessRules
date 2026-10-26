using System;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The decisions shared by <see cref="FeatureGatedBusinessRule"/> and <see cref="FeatureGatedAsyncBusinessRule"/>:
    /// which provider answers for a flag (a context override wins over the injected provider, and the async decorator
    /// prefers an async provider over a sync one) and whether the gate is open for the caller described by the context.
    /// </summary>
    internal static class RuleGate
    {
        /// <summary>
        /// Resolves the sync provider for one evaluation: <see cref="RuleFeatureFlags.ContextKey"/> first, then the injected provider.
        /// </summary>
        /// <exception cref="RuleConfigurationException">Thrown when neither source supplies a provider.</exception>
        public static IRuleFeatureFlagProvider ResolveProvider(IBusinessRuleMetadata rule, string flag, BusinessRuleContext context, IRuleFeatureFlagProvider? injected)
        {
            if (context.TryGet(RuleFeatureFlags.ContextKey, out IRuleFeatureFlagProvider fromContext))
            {
                return fromContext;
            }

            if (injected != null)
            {
                return injected;
            }

            throw MissingProvider(rule, flag);
        }

        /// <summary>
        /// Reads the rollout for one async evaluation: <see cref="RuleFeatureFlags.AsyncContextKey"/>, then
        /// <see cref="RuleFeatureFlags.ContextKey"/>, then the injected async provider, then the injected sync provider.
        /// </summary>
        /// <exception cref="RuleConfigurationException">Thrown when no source supplies a provider.</exception>
        public static Task<RuleRollout> GetRolloutAsync(
            IBusinessRuleMetadata rule,
            string flag,
            BusinessRuleContext context,
            IAsyncRuleFeatureFlagProvider? injectedAsync,
            IRuleFeatureFlagProvider? injected,
            CancellationToken cancellationToken)
        {
            if (context.TryGet(RuleFeatureFlags.AsyncContextKey, out IAsyncRuleFeatureFlagProvider asyncFromContext))
            {
                return asyncFromContext.GetRolloutAsync(flag, context, cancellationToken);
            }

            if (context.TryGet(RuleFeatureFlags.ContextKey, out IRuleFeatureFlagProvider fromContext))
            {
                return Task.FromResult(fromContext.GetRollout(flag, context));
            }

            if (injectedAsync != null)
            {
                return injectedAsync.GetRolloutAsync(flag, context, cancellationToken);
            }

            if (injected != null)
            {
                return Task.FromResult(injected.GetRollout(flag, context));
            }

            throw MissingProvider(rule, flag);
        }

        /// <summary>
        /// Decides whether the gate is open for the caller described by <paramref name="context"/>. A closed gate
        /// carries the reason recorded in the explanation tree; an open gate carries <see cref="RuleExplanationText.NoReason"/>.
        /// </summary>
        /// <exception cref="RuleConfigurationException">Thrown for a partial rollout without a stable key selector, or when the selector returns null.</exception>
        public static bool IsOpen(
            IBusinessRuleMetadata rule,
            string flag,
            RuleRollout rollout,
            BusinessRuleContext context,
            Func<BusinessRuleContext, string>? stableKey,
            out string closedReason)
        {
            if (rollout.IsOff)
            {
                closedReason = RuleExplanationText.FlagOffReason(flag);
                return false;
            }

            if (rollout.IsComplete)
            {
                closedReason = RuleExplanationText.NoReason;
                return true;
            }

            if (stableKey == null)
            {
                throw MissingStableKey(rule, flag, rollout.Percent);
            }

            string key = stableKey(context) ?? throw NullStableKey(rule, flag);
            int bucket = RuleRolloutBucket.Compute(flag, key);

            if (rollout.Includes(bucket))
            {
                closedReason = RuleExplanationText.NoReason;
                return true;
            }

            closedReason = RuleExplanationText.OutsideCohortReason(flag, bucket, rollout.Percent);
            return false;
        }

        private static RuleConfigurationException MissingProvider(IBusinessRuleMetadata rule, string flag)
        {
            return new RuleConfigurationException(
                rule.Code,
                $"Rule {rule.Code} is gated by flag '{flag}' but no feature flag provider was supplied: pass one to GatedBy(...) or store one under RuleFeatureFlags.ContextKey / RuleFeatureFlags.AsyncContextKey on the context.");
        }

        private static RuleConfigurationException MissingStableKey(IBusinessRuleMetadata rule, string flag, int percent)
        {
            return new RuleConfigurationException(
                rule.Code,
                $"Rule {rule.Code} is gated by flag '{flag}' rolled out to {percent}% but has no stable key selector: pass stableKey to GatedBy(...) or roll the flag out to {RuleRollout.MinPercent}% or {RuleRollout.MaxPercent}%.");
        }

        private static RuleConfigurationException NullStableKey(IBusinessRuleMetadata rule, string flag)
        {
            return new RuleConfigurationException(
                rule.Code,
                $"The stable key selector of rule {rule.Code} for flag '{flag}' returned null; a stable key is required to bucket a partial rollout.");
        }
    }
}
