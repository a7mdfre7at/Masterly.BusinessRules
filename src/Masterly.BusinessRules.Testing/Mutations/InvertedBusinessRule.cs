using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// A smoke-mode mutant that is broken exactly when the wrapped rule passes. Unlike <c>Not()</c> it keeps the rule's
    /// <see cref="Code"/>, <see cref="Message"/> and every other metadata member, so the test suite sees the same rule
    /// identity misbehaving. Created through <see cref="RuleMutationExtensions.Invert(IBusinessRule)"/>.
    /// </summary>
    public sealed class InvertedBusinessRule : IExplainableDecoratedBusinessRule
    {
        private readonly IBusinessRule _innerRule;

        /// <summary>
        /// Initializes a new instance of the <see cref="InvertedBusinessRule"/> class.
        /// </summary>
        /// <param name="innerRule">The rule whose verdict is inverted.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> is null.</exception>
        public InvertedBusinessRule(IBusinessRule innerRule)
        {
            _innerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
        }

        /// <inheritdoc />
        public IBusinessRule InnerRule => _innerRule;

        /// <summary>
        /// Gets the operator label of this decorator's explanation node.
        /// </summary>
        public string ExplanationOperator => RuleMutationText.InvertedOperator;

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
        public bool IsBroken() => !_innerRule.IsBroken();

        /// <inheritdoc />
        public bool IsBroken(BusinessRuleContext context) => !_innerRule.IsBroken(context);

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the inner rule passes.</exception>
        public void Check()
        {
            if (IsBroken())
            {
                throw new BusinessRuleValidationException(new[] { Evaluate() });
            }
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the inner rule passes.</exception>
        public void Check(BusinessRuleContext context)
        {
            if (IsBroken(context))
            {
                throw new BusinessRuleValidationException(new[] { Evaluate(context) });
            }
        }

        /// <inheritdoc />
        public BusinessRuleResult Evaluate() => _innerRule.Evaluate();

        /// <inheritdoc />
        public BusinessRuleResult Evaluate(BusinessRuleContext context) => _innerRule.Evaluate(context);

        /// <summary>
        /// Evaluates the wrapped rule once through <paramref name="evaluation"/> and inverts its verdict.
        /// </summary>
        /// <param name="evaluation">The one-shot evaluation of the wrapped rule supplied by the core walker.</param>
        /// <returns>The mutant's broken result when the wrapped rule passed; null when it was broken.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="evaluation"/> is null.</exception>
        public BusinessRuleResult? EvaluateDecorated(DecoratedRuleEvaluation evaluation)
        {
            if (evaluation == null)
            {
                throw new ArgumentNullException(nameof(evaluation));
            }

            if (evaluation.EvaluateInner() != null)
            {
                return null;
            }

            return evaluation.Context != null ? Evaluate(evaluation.Context) : Evaluate();
        }
    }
}
