using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// A decorator that puts an async rule behind a feature flag whose <see cref="RuleRollout"/> is read from an
    /// <see cref="IAsyncRuleFeatureFlagProvider"/> (or an <see cref="IRuleFeatureFlagProvider"/>) on every evaluation,
    /// so a rule can ship dark and be ramped from the flag provider without touching the rule class. All seven metadata
    /// members are forwarded from the inner rule.
    /// </summary>
    /// <remarks>
    /// <para><see cref="RuleRolloutMode.Off"/>, or a caller outside the rollout cohort: the inner rule is not evaluated
    /// and the gated rule passes. <see cref="RuleRolloutMode.Shadow"/>: the inner rule is evaluated, the gated rule still
    /// passes, and a violation is handed to the checker's <see cref="IAsyncRuleExecutionObserver.OnShadowViolationAsync"/> and
    /// <see cref="AsyncRuleEvaluationResult.ShadowResults"/>. <see cref="RuleRolloutMode.Enforced"/>: the inner verdict is reported as is.</para>
    /// <para>The provider is resolved per evaluation: <see cref="RuleFeatureFlags.AsyncContextKey"/>, then
    /// <see cref="RuleFeatureFlags.ContextKey"/>, then the injected async provider, then the injected sync provider; when
    /// none exists the evaluation throws <see cref="RuleConfigurationException"/>. A partial rollout without a stable key
    /// selector also throws <see cref="RuleConfigurationException"/>.</para>
    /// <para>Place a cache inside the gate (<c>rule.Cached(ttl).GatedBy(...)</c>), never outside it: a cache around the
    /// gate would freeze the rollout decision for the cache duration.</para>
    /// <para>The explanation tree mirrors <see cref="FeatureGatedBusinessRule"/>: a closed gate is a <see cref="RuleOutcome.Skipped"/>
    /// <c>GATED</c> node, an enforced evaluation a <c>GATED</c> node, a shadow evaluation a <c>SHADOW</c> node.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// IAsyncBusinessRule ramped = new SanctionsCheckRule(api)
    ///     .GatedBy("sanctions-check", asyncFlagProvider, stableKey: ctx => ctx.Get&lt;string&gt;("customerId"));
    /// </code>
    /// </example>
    public sealed class FeatureGatedAsyncBusinessRule : IDecoratedAsyncBusinessRule, IExplainableAsyncBusinessRule
    {
        private readonly IAsyncBusinessRule _innerRule;
        private readonly string _flag;
        private readonly IAsyncRuleFeatureFlagProvider? _asyncProvider;
        private readonly IRuleFeatureFlagProvider? _provider;
        private readonly Func<BusinessRuleContext, string>? _stableKey;

        /// <summary>
        /// Initializes a gated async rule whose provider is supplied through <see cref="RuleFeatureFlags.AsyncContextKey"/>
        /// or <see cref="RuleFeatureFlags.ContextKey"/> at evaluation time.
        /// </summary>
        /// <param name="innerRule">The async rule to gate.</param>
        /// <param name="flag">The feature flag the rule is bound to.</param>
        /// <param name="stableKey">Selects the stable key (for example a customer id) used to bucket partial rollouts; null when the flag is only ever at 0% or 100%.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> or <paramref name="flag"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="flag"/> is empty or white space.</exception>
        public FeatureGatedAsyncBusinessRule(IAsyncBusinessRule innerRule, string flag, Func<BusinessRuleContext, string>? stableKey = null)
            : this(innerRule, flag, null, null, stableKey)
        {
        }

        /// <summary>
        /// Initializes a gated async rule with injected providers. The async provider is tried first, then the sync one.
        /// </summary>
        /// <param name="innerRule">The async rule to gate.</param>
        /// <param name="flag">The feature flag the rule is bound to.</param>
        /// <param name="asyncProvider">The async provider that supplies the flag's rollout, or null.</param>
        /// <param name="provider">The sync provider used when <paramref name="asyncProvider"/> is null, or null to rely on the context keys.</param>
        /// <param name="stableKey">Selects the stable key (for example a customer id) used to bucket partial rollouts; null when the flag is only ever at 0% or 100%.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> or <paramref name="flag"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="flag"/> is empty or white space.</exception>
        public FeatureGatedAsyncBusinessRule(
            IAsyncBusinessRule innerRule,
            string flag,
            IAsyncRuleFeatureFlagProvider? asyncProvider,
            IRuleFeatureFlagProvider? provider,
            Func<BusinessRuleContext, string>? stableKey = null)
        {
            _innerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
            _flag = flag ?? throw new ArgumentNullException(nameof(flag));

            if (string.IsNullOrWhiteSpace(flag))
            {
                throw new ArgumentException("The feature flag name cannot be empty or white space.", nameof(flag));
            }

            _asyncProvider = asyncProvider;
            _provider = provider;
            _stableKey = stableKey;
        }

        /// <inheritdoc />
        public IAsyncBusinessRule InnerRule => _innerRule;

        /// <summary>
        /// Gets the feature flag the rule is bound to.
        /// </summary>
        public string Flag => _flag;

        /// <summary>
        /// Gets the injected async provider, or null.
        /// </summary>
        public IAsyncRuleFeatureFlagProvider? AsyncProvider => _asyncProvider;

        /// <summary>
        /// Gets the injected sync provider, or null.
        /// </summary>
        public IRuleFeatureFlagProvider? Provider => _provider;

        /// <summary>
        /// Gets whether a stable key selector was supplied, so partial rollouts can be bucketed.
        /// </summary>
        public bool HasStableKey => _stableKey != null;

        /// <inheritdoc />
        public string Code => _innerRule.Code;

        /// <inheritdoc />
        public string Message => _innerRule.Message;

        /// <inheritdoc />
        public RuleSeverity Severity => _innerRule.Severity;

        /// <inheritdoc />
        public string Name => _innerRule.Name;

        /// <inheritdoc />
        public string Description => _innerRule.Description;

        /// <inheritdoc />
        public string Category => _innerRule.Category;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => _innerRule.Tags;

        /// <inheritdoc />
        /// <remarks>Applies the gate: passes when the flag is off, the caller is outside the cohort or the mode is shadow.</remarks>
        /// <exception cref="RuleConfigurationException">Thrown when no provider is available or a partial rollout has no stable key.</exception>
        public async Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            return await EvaluateCoreAsync(RequireContext(context), null, cancellationToken) != null;
        }

        /// <inheritdoc />
        /// <remarks>Null when the gate is closed, the mode is shadow or the inner rule passed; otherwise the inner rule's result.</remarks>
        /// <exception cref="RuleConfigurationException">Thrown when no provider is available or a partial rollout has no stable key.</exception>
        public Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            return EvaluateCoreAsync(RequireContext(context), null, cancellationToken);
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the gate is enforced and the inner rule is broken.</exception>
        /// <exception cref="RuleConfigurationException">Thrown when no provider is available or a partial rollout has no stable key.</exception>
        public async Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            BusinessRuleResult? result = await EvaluateCoreAsync(RequireContext(context), null, cancellationToken);
            if (result != null)
            {
                throw new BusinessRuleValidationException(new[] { result });
            }
        }

        Task<BusinessRuleResult?> IExplainableAsyncBusinessRule.EvaluateCoreAsync(BusinessRuleContext context, RuleExplanationTrace? trace, CancellationToken cancellationToken)
            => EvaluateCoreAsync(context, trace, cancellationToken);

        private static BusinessRuleContext RequireContext(BusinessRuleContext context)
        {
            return context ?? throw new ArgumentNullException(nameof(context));
        }

        private async Task<BusinessRuleResult?> EvaluateCoreAsync(BusinessRuleContext context, RuleExplanationTrace? trace, CancellationToken cancellationToken)
        {
            RuleRollout rollout = await RuleGate.GetRolloutAsync(this, _flag, context, _asyncProvider, _provider, cancellationToken);

            if (!RuleGate.IsOpen(this, _flag, rollout, context, _stableKey, out string closedReason))
            {
                trace?.Add(AsyncBusinessRuleWalker.Skipped(this, _innerRule, RuleExplanationText.GateOperator, closedReason));
                return null;
            }

            RuleExplanationTrace? children = trace?.Branch();
            long start = trace != null ? RuleExplanationTrace.StartTiming() : 0L;

            BusinessRuleResult? result = await AsyncBusinessRuleWalker.EvaluateNodeAsync(_innerRule, context, children, cancellationToken);
            bool shadow = rollout.Mode == RuleRolloutMode.Shadow;

            if (shadow && result != null)
            {
                ShadowViolationSink.Report(context, this, result);
            }

            if (trace != null && children != null)
            {
                string operatorName = shadow ? RuleExplanationText.ShadowOperator : RuleExplanationText.GateOperator;
                string nodeReason = shadow && result != null ? RuleExplanationText.ShadowViolationReason : RuleExplanationText.NoReason;
                trace.Add(AsyncBusinessRuleWalker.Evaluated(this, !shadow && result != null, operatorName, nodeReason, RuleExplanationTrace.ElapsedSince(start), children.Nodes));
            }

            return shadow ? null : result;
        }
    }
}
