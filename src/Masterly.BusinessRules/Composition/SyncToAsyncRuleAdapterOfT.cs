using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Adapts a subject-typed synchronous rule (<see cref="IBusinessRule{TSubject}"/>) to the subject-typed async
    /// hierarchy (<see cref="IAsyncBusinessRule{TSubject}"/>), so it can join typed async compositions, typed async
    /// batches and <c>AsyncBusinessRuleChecker</c> calls without being bound to one subject first. Created through
    /// <see cref="TypedBusinessRuleExtensions.ToAsync{TSubject}(IBusinessRule{TSubject})"/>. All seven metadata members
    /// are forwarded; evaluation completes synchronously.
    /// </summary>
    /// <typeparam name="TSubject">The subject type.</typeparam>
    public sealed class SyncToAsyncRuleAdapter<TSubject> : IAsyncBusinessRule<TSubject>
    {
        private readonly IBusinessRule<TSubject> _syncRule;

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncToAsyncRuleAdapter{TSubject}"/> class.
        /// </summary>
        /// <param name="syncRule">The synchronous typed rule to adapt.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="syncRule"/> is null.</exception>
        public SyncToAsyncRuleAdapter(IBusinessRule<TSubject> syncRule)
        {
            _syncRule = syncRule ?? throw new ArgumentNullException(nameof(syncRule));
        }

        /// <summary>
        /// Gets the wrapped synchronous typed rule.
        /// </summary>
        public IBusinessRule<TSubject> InnerRule => _syncRule;

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
        /// <remarks>Completes synchronously with the wrapped rule's verdict; honours a cancelled token before evaluating.</remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
        public Task<bool> IsBrokenAsync(TSubject subject, BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_syncRule.IsBroken(subject, context));
        }

        /// <inheritdoc />
        /// <remarks>Returns the wrapped rule's <c>Evaluate(subject, context)</c> result when broken and null otherwise.</remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
        public Task<BusinessRuleResult?> EvaluateAsync(TSubject subject, BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            cancellationToken.ThrowIfCancellationRequested();
            BusinessRuleResult? result = _syncRule.IsBroken(subject, context) ? _syncRule.Evaluate(subject, context) : null;
            return Task.FromResult(result);
        }
    }
}
