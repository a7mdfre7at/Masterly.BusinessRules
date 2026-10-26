using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Collects the violations that <see cref="RuleRolloutMode.Shadow"/> gated rules observe while a checker batch
    /// runs. The checkers attach a sink to the context's internal <see cref="BusinessRuleContext.ShadowSink"/> slot
    /// (never to its key/value store, so <c>Keys</c> and <c>Count</c> are untouched) for the duration of the batch and
    /// drain it after every rule, so a gated rule nested anywhere in the tree (inside a group, a cache, an adapter)
    /// reaches the observer and <c>ShadowResults</c>. Reporting without an attached sink is a no-op. The sink is
    /// thread-safe because parallel async batches share one context.
    /// </summary>
    internal sealed class ShadowViolationSink
    {
        private static readonly IReadOnlyList<ShadowViolation> NoViolations = Array.Empty<ShadowViolation>();

        private readonly object _lock = new object();
        private List<ShadowViolation> _violations = new List<ShadowViolation>();

        /// <summary>
        /// Returns the sink attached to <paramref name="context"/>, attaching a new one when none is present.
        /// Returns null for a null context (the sync checker's context-less path, where gated rules evaluate un-gated).
        /// </summary>
        /// <param name="context">The batch context, or null.</param>
        /// <param name="attached">True when this call attached the sink and the caller must <see cref="Detach"/> it.</param>
        public static ShadowViolationSink? Attach(BusinessRuleContext? context, out bool attached)
        {
            attached = false;

            if (context == null)
            {
                return null;
            }

            ShadowViolationSink? existing = context.ShadowSink;
            if (existing != null)
            {
                return existing;
            }

            ShadowViolationSink sink = new ShadowViolationSink();
            context.ShadowSink = sink;
            attached = true;
            return sink;
        }

        /// <summary>
        /// Removes the sink from the context. A null context is ignored.
        /// </summary>
        public static void Detach(BusinessRuleContext? context)
        {
            if (context != null)
            {
                context.ShadowSink = null;
            }
        }

        /// <summary>
        /// Records a shadow violation of a sync gated rule when a sink is attached to the context.
        /// </summary>
        public static void Report(BusinessRuleContext context, IBusinessRule rule, BusinessRuleResult result)
        {
            context.ShadowSink?.Add(new ShadowViolation(rule, result));
        }

        /// <summary>
        /// Records a shadow violation of an async gated rule when a sink is attached to the context.
        /// </summary>
        public static void Report(BusinessRuleContext context, IAsyncBusinessRule rule, BusinessRuleResult result)
        {
            context.ShadowSink?.Add(new ShadowViolation(rule, result));
        }

        /// <summary>
        /// Removes and returns every violation recorded so far, in reporting order.
        /// </summary>
        public IReadOnlyList<ShadowViolation> Drain()
        {
            lock (_lock)
            {
                if (_violations.Count == 0)
                {
                    return NoViolations;
                }

                List<ShadowViolation> drained = _violations;
                _violations = new List<ShadowViolation>();
                return drained;
            }
        }

        private void Add(ShadowViolation violation)
        {
            lock (_lock)
            {
                _violations.Add(violation);
            }
        }
    }

    /// <summary>
    /// One violation observed by a <see cref="RuleRolloutMode.Shadow"/> gated rule: the gated rule (of either
    /// hierarchy) and the result its inner rule produced. The reporting checker asks for the rule in its own hierarchy
    /// and receives the other side through an adapter.
    /// </summary>
    internal sealed class ShadowViolation
    {
        private readonly IBusinessRule? _rule;
        private readonly IAsyncBusinessRule? _asyncRule;

        /// <summary>
        /// Initializes a violation of a sync gated rule.
        /// </summary>
        public ShadowViolation(IBusinessRule rule, BusinessRuleResult result)
        {
            _rule = rule ?? throw new ArgumentNullException(nameof(rule));
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        /// <summary>
        /// Initializes a violation of an async gated rule.
        /// </summary>
        public ShadowViolation(IAsyncBusinessRule rule, BusinessRuleResult result)
        {
            _asyncRule = rule ?? throw new ArgumentNullException(nameof(rule));
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        /// <summary>
        /// Gets the result the inner rule produced: what would have failed had the gate been enforced.
        /// </summary>
        public BusinessRuleResult Result { get; }

        /// <summary>
        /// Gets the gated rule as a sync rule; an async gated rule is wrapped in an <see cref="AsyncToSyncRuleAdapter"/>.
        /// </summary>
        public IBusinessRule AsSyncRule()
        {
            if (_rule != null)
            {
                return _rule;
            }

            return new AsyncToSyncRuleAdapter(RequireAsyncRule());
        }

        /// <summary>
        /// Gets the gated rule as an async rule; a sync gated rule is wrapped in a <see cref="SyncToAsyncRuleAdapter"/>.
        /// </summary>
        public IAsyncBusinessRule AsAsyncRule()
        {
            if (_asyncRule != null)
            {
                return _asyncRule;
            }

            return new SyncToAsyncRuleAdapter(RequireSyncRule());
        }

        private IBusinessRule RequireSyncRule()
        {
            return _rule ?? throw new InvalidOperationException("The shadow violation carries no rule.");
        }

        private IAsyncBusinessRule RequireAsyncRule()
        {
            return _asyncRule ?? throw new InvalidOperationException("The shadow violation carries no rule.");
        }
    }
}
