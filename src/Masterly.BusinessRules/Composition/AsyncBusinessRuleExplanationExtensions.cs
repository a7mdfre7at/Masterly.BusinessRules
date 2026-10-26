using System;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Produces navigable, printable explanation trees for asynchronous rules.
    /// </summary>
    public static class AsyncBusinessRuleExplanationExtensions
    {
        /// <summary>
        /// Evaluates the async rule <b>exactly once</b> and returns the explanation tree produced by that evaluation.
        /// Composed nodes (<see cref="IComposedAsyncBusinessRule"/>) and decorators (<see cref="IDecoratedAsyncBusinessRule"/>, the adapters)
        /// expand into child nodes; any other rule, including third-party implementations, is a single leaf.
        /// Exceptions propagate; use <see cref="ExplainAsync(IAsyncBusinessRule, BusinessRuleContext, bool, CancellationToken)"/> to capture them.
        /// </summary>
        /// <param name="rule">The async rule to explain.</param>
        /// <param name="context">The context forwarded to every rule.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task producing the root explanation node for <paramref name="rule"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> or <paramref name="context"/> is null.</exception>
        public static Task<RuleExplanation> ExplainAsync(this IAsyncBusinessRule rule, BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            return ExplainAsync(rule, context, captureExceptions: false, cancellationToken);
        }

        /// <summary>
        /// Evaluates the async rule <b>exactly once</b> and returns the explanation tree produced by that evaluation.
        /// </summary>
        /// <param name="rule">The async rule to explain.</param>
        /// <param name="context">The context forwarded to every rule.</param>
        /// <param name="captureExceptions">
        /// When true, a node that throws is recorded as <see cref="RuleOutcome.Faulted"/> (and treated as broken) instead of
        /// propagating; cancellation is never captured. When false, the exception propagates and no tree is produced.
        /// </param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task producing the root explanation node for <paramref name="rule"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> or <paramref name="context"/> is null.</exception>
        public static async Task<RuleExplanation> ExplainAsync(
            this IAsyncBusinessRule rule,
            BusinessRuleContext context,
            bool captureExceptions,
            CancellationToken cancellationToken = default)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            RuleExplanationTrace trace = new RuleExplanationTrace(captureExceptions);
            await AsyncBusinessRuleWalker.EvaluateNodeAsync(rule, context, trace, cancellationToken);
            return trace.Nodes[0];
        }
    }
}
