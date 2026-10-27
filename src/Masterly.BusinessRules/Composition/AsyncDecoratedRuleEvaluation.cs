using System;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The one-shot evaluation of an async decorator's inner rule handed to
    /// <see cref="IExplainableDecoratedAsyncBusinessRule.EvaluateDecoratedAsync"/>. <see cref="EvaluateInnerAsync"/> runs the
    /// inner rule through the library walker at most once: composed and decorated inner rules keep their member details
    /// and, when the walk is explaining, record their own explanation subtree under the decorator's node. Not
    /// thread-safe; await it only within the evaluation it was created for.
    /// </summary>
    public sealed class AsyncDecoratedRuleEvaluation
    {
        private readonly RuleExplanationTrace? _trace;
        private BusinessRuleResult? _innerResult;

        /// <summary>
        /// Creates a plain (non-explaining) evaluation, for a decorator implementing its own <c>IsBrokenAsync</c> through
        /// <see cref="IExplainableDecoratedAsyncBusinessRule.EvaluateDecoratedAsync"/>.
        /// </summary>
        /// <param name="innerRule">The decorated rule.</param>
        /// <param name="context">The context to forward to the inner rule.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> is null.</exception>
        public AsyncDecoratedRuleEvaluation(IAsyncBusinessRule innerRule, BusinessRuleContext context)
            : this(innerRule, context, null)
        {
        }

        internal AsyncDecoratedRuleEvaluation(IAsyncBusinessRule innerRule, BusinessRuleContext context, RuleExplanationTrace? trace)
        {
            InnerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
            Context = context;
            _trace = trace;
        }

        /// <summary>
        /// Gets the decorated rule.
        /// </summary>
        public IAsyncBusinessRule InnerRule { get; }

        /// <summary>
        /// Gets the context forwarded to the inner rule.
        /// </summary>
        public BusinessRuleContext Context { get; }

        /// <summary>
        /// Gets whether this evaluation is part of an <c>ExplainAsync</c> walk.
        /// </summary>
        public bool IsExplaining => _trace != null;

        /// <summary>
        /// Gets whether <see cref="EvaluateInnerAsync"/> has completed.
        /// </summary>
        public bool IsInnerEvaluated { get; private set; }

        /// <summary>
        /// Evaluates the inner rule on the first call and returns the cached outcome on later calls.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The inner rule's broken result, or null when it passed.</returns>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled before the inner rule completes.</exception>
        public async Task<BusinessRuleResult?> EvaluateInnerAsync(CancellationToken cancellationToken = default)
        {
            if (!IsInnerEvaluated)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _innerResult = await AsyncBusinessRuleWalker.EvaluateNodeAsync(InnerRule, Context, _trace, cancellationToken);
                IsInnerEvaluated = true;
            }

            return _innerResult;
        }
    }
}
