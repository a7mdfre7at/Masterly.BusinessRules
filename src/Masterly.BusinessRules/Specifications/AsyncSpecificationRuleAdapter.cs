using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Adapts an <see cref="ISpecificationRule{TSubject}"/> to the subject-typed async hierarchy
    /// (<see cref="IAsyncBusinessRule{TSubject}"/>) so it can join async batches, async groups and
    /// <c>AsyncBusinessRuleChecker</c> calls. Created through
    /// <see cref="SpecificationRuleExtensions.ToAsync{TSubject}"/>. All seven metadata members and both expressions are
    /// forwarded; evaluation completes synchronously because an expression rule never awaits anything. This adapter is
    /// the whole async side of specification rules: an expression has no asynchronous form, so there is deliberately no
    /// <c>IAsyncSpecificationRule</c>.
    /// </summary>
    /// <typeparam name="TSubject">The subject type.</typeparam>
    public sealed class AsyncSpecificationRuleAdapter<TSubject> : IAsyncBusinessRule<TSubject>
    {
        private readonly ISpecificationRule<TSubject> _rule;

        /// <summary>
        /// Initializes a new <see cref="AsyncSpecificationRuleAdapter{TSubject}"/> over a specification rule.
        /// </summary>
        /// <param name="rule">The specification rule to adapt.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public AsyncSpecificationRuleAdapter(ISpecificationRule<TSubject> rule)
        {
            _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        }

        /// <summary>
        /// Gets the adapted specification rule.
        /// </summary>
        public ISpecificationRule<TSubject> InnerRule => _rule;

        /// <summary>
        /// Gets the adapted rule's <see cref="ISpecificationRule{TSubject}.BrokenExpression"/>.
        /// </summary>
        public Expression<Func<TSubject, bool>> BrokenExpression => _rule.BrokenExpression;

        /// <summary>
        /// Gets the adapted rule's <see cref="ISpecificationRule{TSubject}.SatisfiedExpression"/>.
        /// </summary>
        public Expression<Func<TSubject, bool>> SatisfiedExpression => _rule.SatisfiedExpression;

        /// <inheritdoc />
        public string Code => _rule.Code;

        /// <inheritdoc />
        public string Message => _rule.Message;

        /// <inheritdoc />
        public RuleSeverity Severity => _rule.Severity;

        /// <inheritdoc />
        public string Name => _rule.Name;

        /// <inheritdoc />
        public string Description => _rule.Description;

        /// <inheritdoc />
        public string Category => _rule.Category;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => _rule.Tags;

        /// <inheritdoc />
        /// <remarks>Completes synchronously with the adapted rule's verdict; honours a cancelled token before evaluating.</remarks>
        public Task<bool> IsBrokenAsync(TSubject subject, BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_rule.IsBroken(subject, context));
        }

        /// <inheritdoc />
        /// <remarks>Returns the adapted rule's <c>Evaluate(subject, context)</c> result when broken and null otherwise.</remarks>
        public Task<BusinessRuleResult?> EvaluateAsync(TSubject subject, BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BusinessRuleResult? result = _rule.IsBroken(subject, context) ? _rule.Evaluate(subject, context) : null;
            return Task.FromResult(result);
        }
    }
}
