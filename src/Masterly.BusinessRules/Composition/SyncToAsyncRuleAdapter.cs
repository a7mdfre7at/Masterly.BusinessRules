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
    /// or when using the <see cref="AsyncBusinessRuleChecker"/>.
    /// </remarks>
    public sealed class SyncToAsyncRuleAdapter : IAsyncBusinessRule
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
            return Task.FromResult(_syncRule.IsBroken(context));
        }

        /// <inheritdoc />
        public Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_syncRule.IsBroken(context))
                return Task.FromResult<BusinessRuleResult?>(new BusinessRuleResult(Code, Message, Severity));
            return Task.FromResult<BusinessRuleResult?>(null);
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the wrapped rule is broken.</exception>
        public Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _syncRule.Check(context);
            return Task.CompletedTask;
        }
    }
}
