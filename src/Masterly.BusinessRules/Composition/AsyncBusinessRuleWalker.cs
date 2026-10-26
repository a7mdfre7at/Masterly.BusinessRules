using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Evaluates an asynchronous rule node exactly once and, when a trace is supplied, records its explanation.
    /// In-library composed and decorated rules are routed through <see cref="IExplainableAsyncBusinessRule.EvaluateCoreAsync"/>;
    /// every other rule is treated as a leaf and evaluated with <see cref="IAsyncBusinessRule.EvaluateAsync"/>.
    /// </summary>
    internal static class AsyncBusinessRuleWalker
    {
        private static readonly IReadOnlyList<RuleExplanation> NoChildren = Array.Empty<RuleExplanation>();

        /// <summary>
        /// Evaluates <paramref name="rule"/> once and returns its broken result, or null when it passed.
        /// </summary>
        public static async Task<BusinessRuleResult?> EvaluateNodeAsync(
            IAsyncBusinessRule rule,
            BusinessRuleContext context,
            RuleExplanationTrace? trace,
            CancellationToken cancellationToken)
        {
            if (trace == null)
            {
                return await EvaluatePlainAsync(rule, context, cancellationToken);
            }

            long start = RuleExplanationTrace.StartTiming();
            try
            {
                return await EvaluateTracedAsync(rule, context, trace, cancellationToken);
            }
            catch (Exception exception) when (trace.ShouldCapture(exception))
            {
                trace.Add(Faulted(rule, exception, RuleExplanationTrace.ElapsedSince(start)));
                return new BusinessRuleResult(rule.Code, rule.Message, rule.Severity);
            }
        }

        /// <summary>
        /// Evaluates a leaf through the supplied delegate and records a leaf node when a trace is supplied.
        /// Used by in-library nodes that behave as leaves (for example a delegate-based composite).
        /// </summary>
        public static async Task<BusinessRuleResult?> EvaluateLeafAsync(
            IAsyncBusinessRule rule,
            Func<Task<BusinessRuleResult?>> evaluate,
            RuleExplanationTrace? trace)
        {
            if (trace == null)
            {
                return await evaluate();
            }

            long start = RuleExplanationTrace.StartTiming();
            BusinessRuleResult? result = await evaluate();
            trace.Add(Leaf(rule, result, RuleExplanationTrace.ElapsedSince(start)));
            return result;
        }

        /// <summary>
        /// Builds the node for an evaluated composed or decorated rule.
        /// </summary>
        public static RuleExplanation Evaluated(IAsyncBusinessRule rule, bool isBroken, string operatorName, TimeSpan duration, IEnumerable<RuleExplanation> children)
        {
            return new RuleExplanation(
                rule,
                isBroken ? RuleOutcome.Broken : RuleOutcome.Passed,
                isBroken,
                operatorName,
                RuleExplanationText.NoReason,
                duration,
                null,
                children);
        }

        /// <summary>
        /// Builds the node for a rule that a short-circuiting parent never reached.
        /// </summary>
        public static RuleExplanation NotEvaluated(IAsyncBusinessRule rule, string reason)
        {
            return new RuleExplanation(rule, RuleOutcome.NotEvaluated, false, RuleExplanationText.LeafOperator, reason, TimeSpan.Zero, null, NoChildren);
        }

        /// <summary>
        /// Builds the node for a decorator whose precondition was not met; the inner rule is recorded as not evaluated.
        /// </summary>
        public static RuleExplanation Skipped(IAsyncBusinessRule rule, IAsyncBusinessRule innerRule, string operatorName, string reason)
        {
            RuleExplanation[] children = new[] { NotEvaluated(innerRule, reason) };
            return new RuleExplanation(rule, RuleOutcome.Skipped, false, operatorName, reason, TimeSpan.Zero, null, children);
        }

        /// <summary>
        /// Builds the node for a cached decorator that served its verdict from the cache.
        /// </summary>
        public static RuleExplanation CacheHit(IAsyncBusinessRule rule, IAsyncBusinessRule innerRule, bool cachedIsBroken)
        {
            RuleExplanation[] children = new[] { NotEvaluated(innerRule, RuleExplanationText.CacheHitReason) };
            return new RuleExplanation(rule, RuleOutcome.CacheHit, cachedIsBroken, RuleExplanationText.CachedOperator, RuleExplanationText.CacheHitReason, TimeSpan.Zero, null, children);
        }

        /// <summary>
        /// Records a not-evaluated node for every operand from <paramref name="fromIndex"/> onwards.
        /// </summary>
        public static void AddNotEvaluated(RuleExplanationTrace trace, IReadOnlyList<IAsyncBusinessRule> operands, int fromIndex, string reason)
        {
            for (int index = fromIndex; index < operands.Count; index++)
            {
                trace.Add(NotEvaluated(operands[index], reason));
            }
        }

        private static Task<BusinessRuleResult?> EvaluatePlainAsync(IAsyncBusinessRule rule, BusinessRuleContext context, CancellationToken cancellationToken)
        {
            if (rule is IExplainableAsyncBusinessRule explainable)
            {
                return explainable.EvaluateCoreAsync(context, null, cancellationToken);
            }

            return rule.EvaluateAsync(context, cancellationToken);
        }

        private static async Task<BusinessRuleResult?> EvaluateTracedAsync(
            IAsyncBusinessRule rule,
            BusinessRuleContext context,
            RuleExplanationTrace trace,
            CancellationToken cancellationToken)
        {
            if (rule is IExplainableAsyncBusinessRule explainable)
            {
                return await explainable.EvaluateCoreAsync(context, trace, cancellationToken);
            }

            long start = RuleExplanationTrace.StartTiming();
            BusinessRuleResult? result = await rule.EvaluateAsync(context, cancellationToken);
            trace.Add(Leaf(rule, result, RuleExplanationTrace.ElapsedSince(start)));
            return result;
        }

        private static RuleExplanation Leaf(IAsyncBusinessRule rule, BusinessRuleResult? result, TimeSpan duration)
        {
            bool isBroken = result != null;
            return new RuleExplanation(
                rule,
                isBroken ? RuleOutcome.Broken : RuleOutcome.Passed,
                isBroken,
                RuleExplanationText.LeafOperator,
                RuleExplanationText.NoReason,
                duration,
                null,
                NoChildren);
        }

        private static RuleExplanation Faulted(IAsyncBusinessRule rule, Exception exception, TimeSpan duration)
        {
            return new RuleExplanation(
                rule,
                RuleOutcome.Faulted,
                true,
                RuleExplanationText.LeafOperator,
                RuleExplanationText.FaultReason(exception),
                duration,
                exception,
                NoChildren);
        }
    }
}
