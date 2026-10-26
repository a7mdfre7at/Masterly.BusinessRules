using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Adapts a synchronous business rule to work as an async rule.
    /// This allows sync rules to be used in async contexts without blocking.
    /// </summary>
    /// <remarks>
    /// Use this adapter when you need to include a synchronous rule in an async rule collection
    /// or when using the <see cref="AsyncBusinessRuleChecker"/>. In an explanation tree the adapter is a node with
    /// operator <c>ADAPTER</c> whose single child is the explanation of the wrapped sync rule.
    /// </remarks>
    public sealed class SyncToAsyncRuleAdapter : IAsyncBusinessRule, IExplainableAsyncBusinessRule
    {
        private readonly IBusinessRule _syncRule;

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncToAsyncRuleAdapter"/> class.
        /// </summary>
        /// <param name="syncRule">The synchronous rule to adapt.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="syncRule"/> is null.</exception>
        public SyncToAsyncRuleAdapter(IBusinessRule syncRule)
        {
            _syncRule = syncRule ?? throw new ArgumentNullException(nameof(syncRule));
        }

        /// <summary>
        /// Gets the wrapped synchronous rule.
        /// </summary>
        public IBusinessRule InnerRule => _syncRule;

        /// <inheritdoc />
        public string Code => _syncRule.Code;

        /// <inheritdoc />
        public string Message => _syncRule.Message;

        /// <inheritdoc />
        public RuleSeverity Severity => _syncRule.Severity;

        /// <inheritdoc />
        public string Name => _syncRule.Name;

        /// <inheritdoc />
        public string Description => _syncRule.Description;

        /// <inheritdoc />
        public string Category => _syncRule.Category;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => _syncRule.Tags;

        /// <inheritdoc />
        public Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(EvaluateCore(context, null) != null);
        }

        /// <inheritdoc />
        public Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(EvaluateCore(context, null));
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the wrapped rule is broken.</exception>
        public Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _syncRule.Check(context);
            return Task.CompletedTask;
        }

        Task<BusinessRuleResult?> IExplainableAsyncBusinessRule.EvaluateCoreAsync(BusinessRuleContext context, RuleExplanationTrace? trace, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(EvaluateCore(context, trace));
        }

        private BusinessRuleResult? EvaluateCore(BusinessRuleContext context, RuleExplanationTrace? trace)
        {
            if (trace == null)
            {
                return BusinessRuleWalker.EvaluateNode(_syncRule, context, null);
            }

            RuleExplanationTrace children = trace.Branch();
            long start = RuleExplanationTrace.StartTiming();
            BusinessRuleResult? result = BusinessRuleWalker.EvaluateNode(_syncRule, context, children);
            trace.Add(AsyncBusinessRuleWalker.Evaluated(this, result != null, RuleExplanationText.AdapterOperator, RuleExplanationTrace.ElapsedSince(start), children.Nodes));
            return result;
        }
    }
}
