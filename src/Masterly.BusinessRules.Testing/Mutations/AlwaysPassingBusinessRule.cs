using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// A smoke-mode mutant that never breaks and never evaluates the wrapped rule: the rule has silently stopped
    /// firing. All seven metadata members are forwarded. Created through <see cref="RuleMutationExtensions.AlwaysPass(IBusinessRule)"/>.
    /// </summary>
    public sealed class AlwaysPassingBusinessRule : IExplainableDecoratedBusinessRule
    {
        private readonly IBusinessRule _innerRule;

        /// <summary>
        /// Initializes a new instance of the <see cref="AlwaysPassingBusinessRule"/> class.
        /// </summary>
        /// <param name="innerRule">The rule that is silenced.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> is null.</exception>
        public AlwaysPassingBusinessRule(IBusinessRule innerRule)
        {
            _innerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
        }

        /// <inheritdoc />
        public IBusinessRule InnerRule => _innerRule;

        /// <summary>
        /// Gets the operator label of this decorator's explanation node.
        /// </summary>
        public string ExplanationOperator => RuleMutationText.AlwaysPassingOperator;

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
        /// <remarks>Always false; the inner rule is not evaluated.</remarks>
        public bool IsBroken() => false;

        /// <inheritdoc />
        /// <remarks>Always false; the inner rule is not evaluated.</remarks>
        public bool IsBroken(BusinessRuleContext context) => false;

        /// <inheritdoc />
        /// <remarks>Never throws; the inner rule is not evaluated.</remarks>
        public void Check()
        {
        }

        /// <inheritdoc />
        /// <remarks>Never throws; the inner rule is not evaluated.</remarks>
        public void Check(BusinessRuleContext context)
        {
        }

        /// <inheritdoc />
        public BusinessRuleResult Evaluate() => _innerRule.Evaluate();

        /// <inheritdoc />
        public BusinessRuleResult Evaluate(BusinessRuleContext context) => _innerRule.Evaluate(context);

        /// <summary>
        /// Passes without evaluating the wrapped rule, so <c>Explain</c> records it as not evaluated.
        /// </summary>
        /// <param name="evaluation">The evaluation of the wrapped rule, deliberately left unused.</param>
        /// <returns>Always null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="evaluation"/> is null.</exception>
        public BusinessRuleResult? EvaluateDecorated(DecoratedRuleEvaluation evaluation)
        {
            if (evaluation == null)
            {
                throw new ArgumentNullException(nameof(evaluation));
            }

            return null;
        }
    }
}
