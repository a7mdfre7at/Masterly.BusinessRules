using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// A decorator that puts a rule behind a feature flag whose <see cref="RuleRollout"/> is read from an
    /// <see cref="IRuleFeatureFlagProvider"/> on every evaluation, so a rule can ship dark and be ramped from the
    /// flag provider without touching the rule class. All seven metadata members are forwarded from the inner rule.
    /// </summary>
    /// <remarks>
    /// <para><see cref="RuleRolloutMode.Off"/>, or a caller outside the rollout cohort: the inner rule is not evaluated
    /// and the gated rule passes. <see cref="RuleRolloutMode.Shadow"/>: the inner rule is evaluated, the gated rule still
    /// passes, and a violation is handed to the checker's <see cref="IRuleExecutionObserver.OnShadowViolation"/> and
    /// <see cref="RuleEvaluationResult.ShadowResults"/>. Shadow mode never blocks: an exception the inner rule throws
    /// (other than cancellation) is caught, a stand-in result goes to <see cref="RuleEvaluationResult.ShadowResults"/>
    /// and the exception to <see cref="IRuleExecutionObserver.OnShadowFault"/>. <see cref="RuleRolloutMode.Enforced"/>:
    /// the inner verdict is reported as is and faults propagate.</para>
    /// <para>The cohort is decided by <see cref="RuleRolloutBucket"/> over the stable key selected from the context
    /// (for example a customer id); a rollout at 100% needs no stable key, a partial rollout without one throws
    /// <see cref="RuleConfigurationException"/>.</para>
    /// <para>The provider stored under <see cref="RuleFeatureFlags.ContextKey"/> overrides the injected one; when
    /// neither exists the evaluation throws <see cref="RuleConfigurationException"/>.</para>
    /// <para>The gate needs a context. The parameterless <see cref="IsBroken()"/> and <see cref="Check()"/>, and therefore
    /// a context-less checker call or an <c>And</c>/<c>Or</c> composition evaluated without a context, evaluate the
    /// inner rule un-gated and never consult the provider, so composition can never throw for a missing provider.</para>
    /// <para>Place a cache inside the gate (<c>rule.Cached(ttl).GatedBy(...)</c>), never outside it: a cache around the
    /// gate would freeze the rollout decision for the cache duration.</para>
    /// <para>In an explanation tree a closed gate is a <see cref="RuleOutcome.Skipped"/> <c>GATED</c> node with the reason
    /// (flag off, or bucket outside the cohort); an enforced evaluation is a <c>GATED</c> node; a shadow evaluation is a
    /// <c>SHADOW</c> node that reports <see cref="RuleOutcome.Passed"/> with the reason <c>shadow violation</c> when the
    /// inner rule was broken.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// IBusinessRule gated = new NewCreditScoreRule(customer)
    ///     .GatedBy("credit-score-v2", flagProvider, stableKey: ctx => ctx.Get&lt;string&gt;("customerId"));
    /// </code>
    /// </example>
    public sealed class FeatureGatedBusinessRule : IDecoratedBusinessRule, IExplainableBusinessRule
    {
        private readonly IBusinessRule _innerRule;
        private readonly string _flag;
        private readonly IRuleFeatureFlagProvider? _provider;
        private readonly Func<BusinessRuleContext, string>? _stableKey;

        /// <summary>
        /// Initializes a gated rule whose provider is supplied through <see cref="RuleFeatureFlags.ContextKey"/> at evaluation time.
        /// </summary>
        /// <param name="innerRule">The rule to gate.</param>
        /// <param name="flag">The feature flag the rule is bound to.</param>
        /// <param name="stableKey">Selects the stable key (for example a customer id) used to bucket partial rollouts; null when the flag is only ever at 0% or 100%.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> or <paramref name="flag"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="flag"/> is empty or white space.</exception>
        public FeatureGatedBusinessRule(IBusinessRule innerRule, string flag, Func<BusinessRuleContext, string>? stableKey = null)
            : this(innerRule, flag, null, stableKey)
        {
        }

        /// <summary>
        /// Initializes a gated rule with an injected provider.
        /// </summary>
        /// <param name="innerRule">The rule to gate.</param>
        /// <param name="flag">The feature flag the rule is bound to.</param>
        /// <param name="provider">The provider that supplies the flag's rollout, or null to rely on <see cref="RuleFeatureFlags.ContextKey"/>.</param>
        /// <param name="stableKey">Selects the stable key (for example a customer id) used to bucket partial rollouts; null when the flag is only ever at 0% or 100%.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> or <paramref name="flag"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="flag"/> is empty or white space.</exception>
        public FeatureGatedBusinessRule(IBusinessRule innerRule, string flag, IRuleFeatureFlagProvider? provider, Func<BusinessRuleContext, string>? stableKey = null)
        {
            _innerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
            _flag = flag ?? throw new ArgumentNullException(nameof(flag));

            if (string.IsNullOrWhiteSpace(flag))
            {
                throw new ArgumentException("The feature flag name cannot be empty or white space.", nameof(flag));
            }

            _provider = provider;
            _stableKey = stableKey;
        }

        /// <inheritdoc />
        public IBusinessRule InnerRule => _innerRule;

        /// <summary>
        /// Gets the feature flag the rule is bound to.
        /// </summary>
        public string Flag => _flag;

        /// <summary>
        /// Gets the injected provider, or null when the provider comes from <see cref="RuleFeatureFlags.ContextKey"/>.
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
        /// <remarks>Evaluates the inner rule un-gated: without a context there is no caller to bucket and no provider override to consult.</remarks>
        public bool IsBroken()
        {
            return EvaluateCore(null, null) != null;
        }

        /// <inheritdoc />
        /// <remarks>Applies the gate: passes when the flag is off, the caller is outside the cohort or the mode is shadow.</remarks>
        /// <exception cref="RuleConfigurationException">Thrown when no provider is available or a partial rollout has no stable key.</exception>
        public bool IsBroken(BusinessRuleContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return EvaluateCore(context, null) != null;
        }

        /// <inheritdoc />
        /// <remarks>Evaluates the inner rule un-gated, like <see cref="IsBroken()"/>.</remarks>
        /// <exception cref="BusinessRuleValidationException">Thrown when the inner rule is broken.</exception>
        public void Check()
        {
            ThrowIfBroken(EvaluateCore(null, null));
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the gate is enforced and the inner rule is broken.</exception>
        /// <exception cref="RuleConfigurationException">Thrown when no provider is available or a partial rollout has no stable key.</exception>
        public void Check(BusinessRuleContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ThrowIfBroken(EvaluateCore(context, null));
        }

        /// <inheritdoc />
        /// <remarks>Forwards to the inner rule's factory; the gate is not consulted.</remarks>
        public BusinessRuleResult Evaluate() => _innerRule.Evaluate();

        /// <inheritdoc />
        /// <remarks>Forwards to the inner rule's context-aware factory; the gate is not consulted.</remarks>
        public BusinessRuleResult Evaluate(BusinessRuleContext context) => _innerRule.Evaluate(context);

        BusinessRuleResult? IExplainableBusinessRule.EvaluateCore(BusinessRuleContext? context, RuleExplanationTrace? trace)
            => EvaluateCore(context, trace);

        private static void ThrowIfBroken(BusinessRuleResult? result)
        {
            if (result != null)
            {
                throw new BusinessRuleValidationException(new[] { result });
            }
        }

        private BusinessRuleResult? EvaluateCore(BusinessRuleContext? context, RuleExplanationTrace? trace)
        {
            if (context == null)
            {
                return EvaluateInner(null, trace, RuleRolloutMode.Enforced, RuleExplanationText.UngatedReason);
            }

            IRuleFeatureFlagProvider provider = RuleGate.ResolveProvider(this, _flag, context, _provider);
            RuleRollout rollout = provider.GetRollout(_flag, context);

            if (!RuleGate.IsOpen(this, _flag, rollout, context, _stableKey, out string closedReason))
            {
                trace?.Add(BusinessRuleWalker.Skipped(this, _innerRule, RuleExplanationText.GateOperator, closedReason));
                return null;
            }

            return EvaluateInner(context, trace, rollout.Mode, RuleExplanationText.NoReason);
        }

        private BusinessRuleResult? EvaluateInner(BusinessRuleContext? context, RuleExplanationTrace? trace, RuleRolloutMode mode, string reason)
        {
            RuleExplanationTrace? children = trace?.Branch();
            long start = trace != null ? RuleExplanationTrace.StartTiming() : 0L;
            bool shadow = mode == RuleRolloutMode.Shadow && context != null;

            BusinessRuleResult? result = shadow
                ? EvaluateShadow(context!, children) // shadow is only reached with a context (checked above)
                : BusinessRuleWalker.EvaluateNode(_innerRule, context, children);

            if (trace != null && children != null)
            {
                string operatorName = shadow ? RuleExplanationText.ShadowOperator : RuleExplanationText.GateOperator;
                string nodeReason = shadow && result != null ? RuleExplanationText.ShadowViolationReason : reason;
                trace.Add(BusinessRuleWalker.Evaluated(this, !shadow && result != null, operatorName, nodeReason, RuleExplanationTrace.ElapsedSince(start), children.Nodes));
            }

            return shadow ? null : result;
        }

        /// <summary>
        /// Evaluates the inner rule in shadow mode and reports what it would have failed. A fault (other than
        /// cancellation) is caught, recorded as a faulted child node and reported as a shadow fault, so shadow mode never
        /// blocks; the returned stand-in result is only used to label the explanation node.
        /// </summary>
        private BusinessRuleResult? EvaluateShadow(BusinessRuleContext context, RuleExplanationTrace? children)
        {
            long start = RuleTiming.Start();
            try
            {
                BusinessRuleResult? result = BusinessRuleWalker.EvaluateNode(_innerRule, context, children);
                if (result != null)
                {
                    ShadowViolationSink.Report(context, this, result);
                }

                return result;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                Exception fault = ContextReadFault.Wrap(this, exception);
                children?.Add(BusinessRuleWalker.Faulted(_innerRule, fault, RuleTiming.ElapsedSince(start)));
                ShadowViolationSink.ReportFault(context, this, fault);
                return RuleFaultResult.Create(this, fault);
            }
        }
    }
}
