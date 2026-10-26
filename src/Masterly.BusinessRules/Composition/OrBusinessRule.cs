using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Structural node produced by <see cref="BusinessRuleExtensions.Or"/>: broken when <b>either</b> operand is broken.
    /// The first operand is evaluated first; when it is broken the second operand is not evaluated (short-circuit).
    /// The context passed to <see cref="IsBroken(BusinessRuleContext)"/> is forwarded to both operands.
    /// </summary>
    public sealed class OrBusinessRule : IComposedBusinessRule, IExplainableBusinessRule
    {
        private static readonly IReadOnlyList<string> EmptyTags = Array.Empty<string>();

        private readonly IBusinessRule _first;
        private readonly IBusinessRule _second;
        private readonly IReadOnlyList<IBusinessRule> _operands;

        /// <summary>
        /// Initializes a new <see cref="OrBusinessRule"/> over two operands.
        /// </summary>
        /// <param name="first">The first operand.</param>
        /// <param name="second">The second operand.</param>
        /// <exception cref="ArgumentNullException">Thrown when either operand is null.</exception>
        public OrBusinessRule(IBusinessRule first, IBusinessRule second)
        {
            _first = first ?? throw new ArgumentNullException(nameof(first));
            _second = second ?? throw new ArgumentNullException(nameof(second));
            _operands = new[] { _first, _second };
        }

        /// <summary>
        /// Gets the synthesised code <c>A|B</c>.
        /// </summary>
        public string Code => $"{_first.Code}{RuleCompositionText.OrCodeSeparator}{_second.Code}";

        /// <summary>
        /// Gets the synthesised message describing both operands.
        /// </summary>
        public string Message => $"{RuleCompositionText.OrMessagePrefix}{_first.Message}{RuleCompositionText.OrMessageSeparator}{_second.Message}";

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Error;

        /// <inheritdoc />
        public string Name => GetType().Name;

        /// <inheritdoc />
        public string Description => RuleCompositionText.OrDescription;

        /// <inheritdoc />
        public string Category => string.Empty;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => EmptyTags;

        /// <inheritdoc />
        public RuleOperator Operator => RuleOperator.Or;

        /// <inheritdoc />
        public IReadOnlyList<IBusinessRule> Operands => _operands;

        /// <inheritdoc />
        public bool IsBroken() => EvaluateCore(null, null) != null;

        /// <inheritdoc />
        public bool IsBroken(BusinessRuleContext context) => EvaluateCore(context, null) != null;

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when either operand is broken.</exception>
        public void Check() => ThrowIfBroken(EvaluateCore(null, null));

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when either operand is broken.</exception>
        public void Check(BusinessRuleContext context) => ThrowIfBroken(EvaluateCore(context, null));

        /// <inheritdoc />
        /// <remarks>A factory carrying the node's metadata; the broken members are reported by <c>Check</c>, the checkers and <c>Explain</c>.</remarks>
        public BusinessRuleResult Evaluate() => RuleResultFactory.Create(this);

        /// <inheritdoc />
        /// <remarks>The context is not used: a structural node attaches no context-aware details of its own.</remarks>
        public BusinessRuleResult Evaluate(BusinessRuleContext context) => Evaluate();

        BusinessRuleResult? IExplainableBusinessRule.EvaluateCore(BusinessRuleContext? context, RuleExplanationTrace? trace)
            => EvaluateCore(context, trace);

        private BusinessRuleResult? EvaluateCore(BusinessRuleContext? context, RuleExplanationTrace? trace)
            => BinaryRuleEvaluation.Evaluate(this, context, trace);

        private static void ThrowIfBroken(BusinessRuleResult? result)
        {
            if (result != null)
            {
                throw new BusinessRuleValidationException(new[] { result });
            }
        }
    }
}
