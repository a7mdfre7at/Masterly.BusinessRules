using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// A wrapper that conditionally executes an async rule based on a predicate.
    /// When the condition returns false, the rule is considered not broken (passes).
    /// </summary>
    /// <remarks>
    /// The condition predicate receives the <see cref="BusinessRuleContext"/> allowing
    /// for context-aware conditional logic.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Only validate if feature flag is enabled
    /// ConditionalAsyncBusinessRule rule = new ConditionalAsyncBusinessRule(
    ///     new AsyncValidationRule(),
    ///     ctx => ctx.Get&lt;bool&gt;("featureEnabled")
    /// );
    /// </code>
    /// </example>
    public sealed class ConditionalAsyncBusinessRule : IAsyncBusinessRule
    {
        private readonly IAsyncBusinessRule _innerRule;
        private readonly Func<BusinessRuleContext, bool> _condition;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConditionalAsyncBusinessRule"/> class.
        /// </summary>
        /// <param name="innerRule">The async rule to conditionally execute.</param>
        /// <param name="condition">The condition that must be true for the rule to be evaluated. Receives the context.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> or <paramref name="condition"/> is null.</exception>
        public ConditionalAsyncBusinessRule(IAsyncBusinessRule innerRule, Func<BusinessRuleContext, bool> condition)
        {
            _innerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        /// <inheritdoc />
        public string Code => _innerRule.Code;

        /// <inheritdoc />
        public string Message => _innerRule.Message;

        /// <inheritdoc />
        public RuleSeverity Severity => _innerRule.Severity;

        /// <inheritdoc />
        public string Name => _innerRule.Name;

        /// <inheritdoc />
        public string Description => _innerRule.Description;

        /// <inheritdoc />
        public string Category => _innerRule.Category;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => _innerRule.Tags;

        /// <inheritdoc />
        /// <remarks>Returns false (not broken) if the condition is false.</remarks>
        public async Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            if (!_condition(context))
                return false;

            return await _innerRule.IsBrokenAsync(context, cancellationToken);
        }

        /// <inheritdoc />
        /// <remarks>Returns null if the condition is false.</remarks>
        public async Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            if (!_condition(context))
                return null;

            return await _innerRule.EvaluateAsync(context, cancellationToken);
        }

        /// <inheritdoc />
        /// <remarks>Does nothing if the condition is false.</remarks>
        /// <exception cref="BusinessRuleValidationException">Thrown when condition is true and the inner rule is broken.</exception>
        public async Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            if (_condition(context))
                await _innerRule.CheckAsync(context, cancellationToken);
        }
    }
}
