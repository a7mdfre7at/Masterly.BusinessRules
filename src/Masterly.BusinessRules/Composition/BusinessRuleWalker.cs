using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Evaluates a synchronous rule node exactly once and, when a trace is supplied, records its explanation.
    /// In-library composed and decorated rules are routed through <see cref="IExplainableBusinessRule.EvaluateCore"/>,
    /// decorators from outside the library through <see cref="IExplainableDecoratedBusinessRule.EvaluateDecorated"/>;
    /// every other rule is treated as a leaf and evaluated with <see cref="IBusinessRule.IsBroken(BusinessRuleContext)"/>
    /// followed by <see cref="IBusinessRule.Evaluate(BusinessRuleContext)"/> (or the parameterless overloads when no
    /// context is supplied), so context-aware <c>Describe</c> details reach the result.
    /// </summary>
    internal static class BusinessRuleWalker
    {
        private static readonly IReadOnlyList<RuleExplanation> NoChildren = Array.Empty<RuleExplanation>();

        /// <summary>
        /// Evaluates <paramref name="rule"/> once and returns its broken result, or null when it passed.
        /// </summary>
        public static BusinessRuleResult? EvaluateNode(IBusinessRule rule, BusinessRuleContext? context, RuleExplanationTrace? trace)
        {
            if (trace == null)
            {
                return EvaluatePlain(rule, context);
            }

            long start = RuleExplanationTrace.StartTiming();
            try
            {
                return EvaluateTraced(rule, context, trace);
            }
            catch (Exception exception) when (trace.ShouldCapture(exception))
            {
                trace.Add(Faulted(rule, exception, RuleExplanationTrace.ElapsedSince(start)));
                return RuleResultFactory.Create(rule);
            }
        }

        /// <summary>
        /// Builds the node for an evaluated composed or decorated rule.
        /// </summary>
        public static RuleExplanation Evaluated(IBusinessRule rule, bool isBroken, string operatorName, TimeSpan duration, IEnumerable<RuleExplanation> children)
        {
            return Evaluated(rule, isBroken, operatorName, RuleExplanationText.NoReason, duration, children);
        }

        /// <summary>
        /// Builds the node for an evaluated composed or decorated rule with a reason (for example a shadow violation).
        /// </summary>
        public static RuleExplanation Evaluated(IBusinessRule rule, bool isBroken, string operatorName, string reason, TimeSpan duration, IEnumerable<RuleExplanation> children)
        {
            return new RuleExplanation(
                rule,
                isBroken ? RuleOutcome.Broken : RuleOutcome.Passed,
                isBroken,
                operatorName,
                reason,
                duration,
                null,
                children);
        }

        /// <summary>
        /// Builds the node for a rule that a short-circuiting parent never reached.
        /// </summary>
        public static RuleExplanation NotEvaluated(IBusinessRule rule, string reason)
        {
            return new RuleExplanation(rule, RuleOutcome.NotEvaluated, false, RuleExplanationText.LeafOperator, reason, TimeSpan.Zero, null, NoChildren);
        }

        /// <summary>
        /// Builds the node for a decorator whose precondition was not met; the inner rule is recorded as not evaluated.
        /// </summary>
        public static RuleExplanation Skipped(IBusinessRule rule, IBusinessRule innerRule, string operatorName, string reason)
        {
            RuleExplanation[] children = new[] { NotEvaluated(innerRule, reason) };
            return new RuleExplanation(rule, RuleOutcome.Skipped, false, operatorName, reason, TimeSpan.Zero, null, children);
        }

        /// <summary>
        /// Builds the node for a cached decorator that served its verdict from the cache.
        /// </summary>
        public static RuleExplanation CacheHit(IBusinessRule rule, IBusinessRule innerRule, bool cachedIsBroken)
        {
            RuleExplanation[] children = new[] { NotEvaluated(innerRule, RuleExplanationText.CacheHitReason) };
            return new RuleExplanation(rule, RuleOutcome.CacheHit, cachedIsBroken, RuleExplanationText.CachedOperator, RuleExplanationText.CacheHitReason, TimeSpan.Zero, null, children);
        }

        /// <summary>
        /// Records a not-evaluated node for every operand from <paramref name="fromIndex"/> onwards.
        /// </summary>
        public static void AddNotEvaluated(RuleExplanationTrace trace, IReadOnlyList<IBusinessRule> operands, int fromIndex, string reason)
        {
            for (int index = fromIndex; index < operands.Count; index++)
            {
                trace.Add(NotEvaluated(operands[index], reason));
            }
        }

        private static BusinessRuleResult? EvaluatePlain(IBusinessRule rule, BusinessRuleContext? context)
        {
            if (rule is IExplainableBusinessRule explainable)
            {
                return explainable.EvaluateCore(context, null);
            }

            if (rule is IExplainableDecoratedBusinessRule decorated)
            {
                return decorated.EvaluateDecorated(new DecoratedRuleEvaluation(decorated.InnerRule, context, null));
            }

            return EvaluateLeaf(rule, context);
        }

        private static BusinessRuleResult? EvaluateTraced(IBusinessRule rule, BusinessRuleContext? context, RuleExplanationTrace trace)
        {
            if (rule is IExplainableBusinessRule explainable)
            {
                return explainable.EvaluateCore(context, trace);
            }

            if (rule is IExplainableDecoratedBusinessRule decorated)
            {
                return EvaluateDecoratedTraced(decorated, context, trace);
            }

            long start = RuleExplanationTrace.StartTiming();
            BusinessRuleResult? result = EvaluateLeaf(rule, context);
            trace.Add(Leaf(rule, result, RuleExplanationTrace.ElapsedSince(start)));
            return result;
        }

        private static BusinessRuleResult? EvaluateDecoratedTraced(IExplainableDecoratedBusinessRule rule, BusinessRuleContext? context, RuleExplanationTrace trace)
        {
            RuleExplanationTrace children = trace.Branch();
            long start = RuleExplanationTrace.StartTiming();
            DecoratedRuleEvaluation evaluation = new DecoratedRuleEvaluation(rule.InnerRule, context, children);
            BusinessRuleResult? result = rule.EvaluateDecorated(evaluation);

            IReadOnlyList<RuleExplanation> innerNodes = evaluation.IsInnerEvaluated
                ? children.Nodes
                : new[] { NotEvaluated(evaluation.InnerRule, RuleExplanationText.DecoratorSkippedReason) };
            trace.Add(Evaluated(rule, result != null, rule.ExplanationOperator, RuleExplanationTrace.ElapsedSince(start), innerNodes));
            return result;
        }

        private static BusinessRuleResult? EvaluateLeaf(IBusinessRule rule, BusinessRuleContext? context)
        {
            if (context != null)
            {
                return rule.IsBroken(context) ? rule.Evaluate(context) : null;
            }

            return rule.IsBroken() ? rule.Evaluate() : null;
        }

        private static RuleExplanation Leaf(IBusinessRule rule, BusinessRuleResult? result, TimeSpan duration)
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

        private static RuleExplanation Faulted(IBusinessRule rule, Exception exception, TimeSpan duration)
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
