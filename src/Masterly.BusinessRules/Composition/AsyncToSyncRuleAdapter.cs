using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Adapts an asynchronous business rule to work as a synchronous rule.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>WARNING:</strong> This adapter blocks the calling thread using <c>GetAwaiter().GetResult()</c>.
    /// Use with caution to avoid deadlocks, especially in UI applications or ASP.NET contexts.
    /// </para>
    /// <para>
    /// Prefer using async rules natively with <see cref="AsyncBusinessRuleChecker"/> when possible.
    /// </para>
    /// <para>
    /// In an explanation tree the adapter is a node with operator <c>ADAPTER</c> whose single child is the explanation
    /// of the wrapped async rule.
    /// </para>
    /// </remarks>
    public sealed class AsyncToSyncRuleAdapter : IBusinessRule, IExplainableBusinessRule
    {
        private readonly IAsyncBusinessRule _asyncRule;
        private readonly BusinessRuleContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncToSyncRuleAdapter"/> class.
        /// </summary>
        /// <param name="asyncRule">The asynchronous rule to adapt.</param>
        /// <param name="context">Optional context to use for parameterless evaluations. Defaults to an empty context.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="asyncRule"/> is null.</exception>
        public AsyncToSyncRuleAdapter(IAsyncBusinessRule asyncRule, BusinessRuleContext? context = null)
        {
            _asyncRule = asyncRule ?? throw new ArgumentNullException(nameof(asyncRule));
            _context = context ?? new BusinessRuleContext();
        }

        /// <summary>
        /// Gets the wrapped asynchronous rule.
        /// </summary>
        public IAsyncBusinessRule InnerRule => _asyncRule;

        /// <inheritdoc />
        public string Code => _asyncRule.Code;

        /// <inheritdoc />
        public string Message => _asyncRule.Message;

        /// <inheritdoc />
        public RuleSeverity Severity => _asyncRule.Severity;

        /// <inheritdoc />
        public string Name => _asyncRule.Name;

        /// <inheritdoc />
        public string Description => _asyncRule.Description;

        /// <inheritdoc />
        public string Category => _asyncRule.Category;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => _asyncRule.Tags;

        /// <inheritdoc />
        /// <remarks>This method blocks the calling thread.</remarks>
        public bool IsBroken()
        {
            return EvaluateCore(null, null) != null;
        }

        /// <inheritdoc />
        /// <remarks>This method blocks the calling thread.</remarks>
        public bool IsBroken(BusinessRuleContext context)
        {
            return EvaluateCore(context, null) != null;
        }

        /// <inheritdoc />
        /// <remarks>A factory carrying the wrapped rule's metadata; an async rule has no factory of its own to forward to.</remarks>
        public BusinessRuleResult Evaluate()
        {
            return RuleResultFactory.Create(this);
        }

        /// <inheritdoc />
        /// <remarks>The context is not used: the wrapped async rule attaches its details when it is evaluated, not here.</remarks>
        public BusinessRuleResult Evaluate(BusinessRuleContext context)
        {
            return Evaluate();
        }

        /// <inheritdoc />
        /// <remarks>This method blocks the calling thread.</remarks>
        /// <exception cref="BusinessRuleValidationException">Thrown when the wrapped rule is broken.</exception>
        public void Check()
        {
            _asyncRule.CheckAsync(_context).GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        /// <remarks>This method blocks the calling thread.</remarks>
        /// <exception cref="BusinessRuleValidationException">Thrown when the wrapped rule is broken.</exception>
        public void Check(BusinessRuleContext context)
        {
            _asyncRule.CheckAsync(context).GetAwaiter().GetResult();
        }

        BusinessRuleResult? IExplainableBusinessRule.EvaluateCore(BusinessRuleContext? context, RuleExplanationTrace? trace)
            => EvaluateCore(context, trace);

        private BusinessRuleResult? EvaluateCore(BusinessRuleContext? context, RuleExplanationTrace? trace)
        {
            BusinessRuleContext effectiveContext = context ?? _context;

            if (trace == null)
            {
                return AsyncBusinessRuleWalker.EvaluateNodeAsync(_asyncRule, effectiveContext, null, default).GetAwaiter().GetResult();
            }

            RuleExplanationTrace children = trace.Branch();
            long start = RuleExplanationTrace.StartTiming();
            BusinessRuleResult? result = AsyncBusinessRuleWalker.EvaluateNodeAsync(_asyncRule, effectiveContext, children, default).GetAwaiter().GetResult();
            trace.Add(BusinessRuleWalker.Evaluated(this, result != null, RuleExplanationText.AdapterOperator, RuleExplanationTrace.ElapsedSince(start), children.Nodes));
            return result;
        }
    }
}
