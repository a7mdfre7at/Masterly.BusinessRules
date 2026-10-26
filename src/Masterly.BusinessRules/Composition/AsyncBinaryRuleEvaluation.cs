using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Shared single-evaluation logic for the binary <see cref="AndAsyncBusinessRule"/> and <see cref="OrAsyncBusinessRule"/> nodes.
    /// <c>And</c> is broken only when both operands are broken and short-circuits when the first operand passes;
    /// <c>Or</c> is broken when either operand is broken and short-circuits when the first operand is broken.
    /// </summary>
    internal static class AsyncBinaryRuleEvaluation
    {
        private const int FirstOperandIndex = 0;
        private const int SecondOperandIndex = 1;

        /// <summary>
        /// Evaluates a binary node once, forwarding the context and cancellation token to its operands.
        /// </summary>
        public static async Task<BusinessRuleResult?> EvaluateAsync(
            IComposedAsyncBusinessRule node,
            BusinessRuleContext context,
            RuleExplanationTrace? trace,
            CancellationToken cancellationToken)
        {
            bool isOr = node.Operator == RuleOperator.Or;
            IAsyncBusinessRule first = node.Operands[FirstOperandIndex];
            IAsyncBusinessRule second = node.Operands[SecondOperandIndex];

            RuleExplanationTrace? children = trace?.Branch();
            long start = trace != null ? RuleExplanationTrace.StartTiming() : 0L;

            cancellationToken.ThrowIfCancellationRequested();
            BusinessRuleResult? firstResult = await AsyncBusinessRuleWalker.EvaluateNodeAsync(first, context, children, cancellationToken);
            bool firstBroken = firstResult != null;
            bool decidedByFirst = firstBroken == isOr;

            BusinessRuleResult? secondResult = null;
            if (!decidedByFirst)
            {
                cancellationToken.ThrowIfCancellationRequested();
                secondResult = await AsyncBusinessRuleWalker.EvaluateNodeAsync(second, context, children, cancellationToken);
            }

            bool secondBroken = secondResult != null;
            bool isBroken = isOr ? firstBroken || secondBroken : firstBroken && secondBroken;

            if (trace != null && children != null)
            {
                if (decidedByFirst)
                {
                    children.Add(AsyncBusinessRuleWalker.NotEvaluated(second, RuleExplanationText.ShortCircuitReason));
                }

                string operatorName = RuleExplanationText.OperatorLabel(node.Operator, 0);
                trace.Add(AsyncBusinessRuleWalker.Evaluated(node, isBroken, operatorName, RuleExplanationTrace.ElapsedSince(start), children.Nodes));
            }

            return isBroken
                ? RuleResultFactory.Create(node, BinaryRuleEvaluation.CollectDetails(firstResult, secondResult))
                : null;
        }
    }
}
