using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Subject-typed async node produced by <see cref="TypedAsyncBusinessRuleExtensions.Not{TSubject}"/>: broken when its
    /// operand passes for the subject, and passing when the operand is broken. The subject, the context and the
    /// cancellation token are forwarded to the operand.
    /// </summary>
    /// <typeparam name="TSubject">The subject type.</typeparam>
    public sealed class NotAsyncBusinessRule<TSubject> : IAsyncBusinessRule<TSubject>
    {
        private static readonly IReadOnlyList<string> EmptyTags = Array.Empty<string>();

        private readonly IAsyncBusinessRule<TSubject> _operand;
        private readonly IReadOnlyList<IAsyncBusinessRule<TSubject>> _operands;

        /// <summary>
        /// Initializes a new <see cref="NotAsyncBusinessRule{TSubject}"/> over one operand.
        /// </summary>
        /// <param name="operand">The async rule to invert.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="operand"/> is null.</exception>
        public NotAsyncBusinessRule(IAsyncBusinessRule<TSubject> operand)
        {
            _operand = operand ?? throw new ArgumentNullException(nameof(operand));
            _operands = new[] { _operand };
        }

        /// <summary>
        /// Gets the synthesised code <c>!A</c>.
        /// </summary>
        public string Code => $"{RuleCompositionText.NotCodePrefix}{_operand.Code}";

        /// <summary>
        /// Gets the synthesised message describing the inverted operand.
        /// </summary>
        public string Message => $"{RuleCompositionText.NotMessagePrefix}{_operand.Message}";

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Error;

        /// <inheritdoc />
        public string Name => nameof(NotAsyncBusinessRule<TSubject>);

        /// <inheritdoc />
        public string Description => RuleCompositionText.NotDescription;

        /// <inheritdoc />
        public string Category => string.Empty;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => EmptyTags;

        /// <summary>
        /// Gets the operator that combines the operands' broken states.
        /// </summary>
        public RuleOperator Operator => RuleOperator.Not;

        /// <summary>
        /// Gets the single operand.
        /// </summary>
        public IReadOnlyList<IAsyncBusinessRule<TSubject>> Operands => _operands;

        /// <inheritdoc />
        public async Task<bool> IsBrokenAsync(TSubject subject, BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            return !await _operand.IsBrokenAsync(subject, context, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<BusinessRuleResult?> EvaluateAsync(TSubject subject, BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            return await IsBrokenAsync(subject, context, cancellationToken) ? RuleResultFactory.Create(this) : null;
        }
    }
}
