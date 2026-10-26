using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Shared single-evaluation logic for the binary <see cref="AndBusinessRule"/> and <see cref="OrBusinessRule"/> nodes.
    /// <c>And</c> is broken only when both operands are broken and short-circuits when the first operand passes;
    /// <c>Or</c> is broken when either operand is broken and short-circuits when the first operand is broken.
    /// </summary>
    internal static class BinaryRuleEvaluation
    {
        private const int FirstOperandIndex = 0;
        private const int SecondOperandIndex = 1;

        /// <summary>
        /// Evaluates a binary node once, forwarding the context to its operands.
        /// </summary>
        public static BusinessRuleResult? Evaluate(IComposedBusinessRule node, BusinessRuleContext? context, RuleExplanationTrace? trace)
        {
            bool isOr = node.Operator == RuleOperator.Or;
            IBusinessRule first = node.Operands[FirstOperandIndex];
            IBusinessRule second = node.Operands[SecondOperandIndex];

            RuleExplanationTrace? children = trace?.Branch();
            long start = trace != null ? RuleExplanationTrace.StartTiming() : 0L;

            BusinessRuleResult? firstResult = BusinessRuleWalker.EvaluateNode(first, context, children);
            bool firstBroken = firstResult != null;
            bool decidedByFirst = firstBroken == isOr;

            BusinessRuleResult? secondResult = decidedByFirst ? null : BusinessRuleWalker.EvaluateNode(second, context, children);
            bool secondBroken = secondResult != null;
            bool isBroken = isOr ? firstBroken || secondBroken : firstBroken && secondBroken;

            if (trace != null && children != null)
            {
                if (decidedByFirst)
                {
                    children.Add(BusinessRuleWalker.NotEvaluated(second, RuleExplanationText.ShortCircuitReason));
                }

                string operatorName = RuleExplanationText.OperatorLabel(node.Operator, 0);
                trace.Add(BusinessRuleWalker.Evaluated(node, isBroken, operatorName, RuleExplanationTrace.ElapsedSince(start), children.Nodes));
            }

            return isBroken
                ? RuleResultFactory.Create(node, CollectDetails(firstResult, secondResult))
                : null;
        }

        /// <summary>
        /// Returns the non-null operand results in operand order.
        /// </summary>
        public static IReadOnlyList<BusinessRuleResult> CollectDetails(BusinessRuleResult? firstResult, BusinessRuleResult? secondResult)
        {
            List<BusinessRuleResult> details = new List<BusinessRuleResult>(2);
            if (firstResult != null)
            {
                details.Add(firstResult);
            }

            if (secondResult != null)
            {
                details.Add(secondResult);
            }

            return details;
        }
    }
}
