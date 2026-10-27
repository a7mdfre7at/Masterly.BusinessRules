using System;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The one-shot evaluation of a decorator's inner rule handed to
    /// <see cref="IExplainableDecoratedBusinessRule.EvaluateDecorated"/>. <see cref="EvaluateInner"/> runs the inner rule
    /// through the library walker at most once: composed and decorated inner rules keep their member details and, when
    /// the walk is explaining, record their own explanation subtree under the decorator's node. Not thread-safe; use it
    /// only within the evaluation it was created for.
    /// </summary>
    public sealed class DecoratedRuleEvaluation
    {
        private readonly RuleExplanationTrace? _trace;
        private BusinessRuleResult? _innerResult;

        /// <summary>
        /// Creates a plain (non-explaining) evaluation, for a decorator implementing its own <c>IsBroken</c> through
        /// <see cref="IExplainableDecoratedBusinessRule.EvaluateDecorated"/>.
        /// </summary>
        /// <param name="innerRule">The decorated rule.</param>
        /// <param name="context">The context to forward, or null to use the parameterless overloads.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> is null.</exception>
        public DecoratedRuleEvaluation(IBusinessRule innerRule, BusinessRuleContext? context)
            : this(innerRule, context, null)
        {
        }

        internal DecoratedRuleEvaluation(IBusinessRule innerRule, BusinessRuleContext? context, RuleExplanationTrace? trace)
        {
            InnerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
            Context = context;
            _trace = trace;
        }

        /// <summary>
        /// Gets the decorated rule.
        /// </summary>
        public IBusinessRule InnerRule { get; }

        /// <summary>
        /// Gets the context forwarded to the inner rule, or null on the parameterless path.
        /// </summary>
        public BusinessRuleContext? Context { get; }

        /// <summary>
        /// Gets whether this evaluation is part of an <c>Explain</c> walk.
        /// </summary>
        public bool IsExplaining => _trace != null;

        /// <summary>
        /// Gets whether <see cref="EvaluateInner"/> has completed.
        /// </summary>
        public bool IsInnerEvaluated { get; private set; }

        /// <summary>
        /// Evaluates the inner rule on the first call and returns the cached outcome on later calls.
        /// </summary>
        /// <returns>The inner rule's broken result, or null when it passed.</returns>
        public BusinessRuleResult? EvaluateInner()
        {
            if (!IsInnerEvaluated)
            {
                _innerResult = BusinessRuleWalker.EvaluateNode(InnerRule, Context, _trace);
                IsInnerEvaluated = true;
            }

            return _innerResult;
        }
    }
}
