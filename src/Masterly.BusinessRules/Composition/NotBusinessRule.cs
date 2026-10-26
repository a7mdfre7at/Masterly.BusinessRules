using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Structural node produced by <see cref="BusinessRuleExtensions.Not"/>: broken when its operand passes and vice versa.
    /// The context passed to <see cref="IsBroken(BusinessRuleContext)"/> is forwarded to the operand.
    /// </summary>
    public sealed class NotBusinessRule : IComposedBusinessRule, IExplainableBusinessRule
    {
        private const string CodePrefix = "!";
        private const string MessagePrefix = "NOT: ";
        private static readonly IReadOnlyList<string> EmptyTags = Array.Empty<string>();

        private readonly IBusinessRule _operand;
        private readonly IReadOnlyList<IBusinessRule> _operands;

        /// <summary>
        /// Initializes a new <see cref="NotBusinessRule"/> over one operand.
        /// </summary>
        /// <param name="operand">The rule to invert.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="operand"/> is null.</exception>
        public NotBusinessRule(IBusinessRule operand)
        {
            _operand = operand ?? throw new ArgumentNullException(nameof(operand));
            _operands = new[] { _operand };
        }

        /// <summary>
        /// Gets the synthesised code <c>!A</c>.
        /// </summary>
        public string Code => $"{CodePrefix}{_operand.Code}";

        /// <summary>
        /// Gets the synthesised message <c>NOT: ...</c>.
        /// </summary>
        public string Message => $"{MessagePrefix}{_operand.Message}";

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Error;

        /// <inheritdoc />
        public string Name => GetType().Name;

        /// <inheritdoc />
        public string Description => "Broken when the operand passes.";

        /// <inheritdoc />
        public string Category => string.Empty;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => EmptyTags;

        /// <inheritdoc />
        public RuleOperator Operator => RuleOperator.Not;

        /// <inheritdoc />
        public IReadOnlyList<IBusinessRule> Operands => _operands;

        /// <inheritdoc />
        public bool IsBroken() => EvaluateCore(null, null) != null;

        /// <inheritdoc />
        public bool IsBroken(BusinessRuleContext context) => EvaluateCore(context, null) != null;

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the operand passes.</exception>
        public void Check() => ThrowIfBroken(EvaluateCore(null, null));

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the operand passes.</exception>
        public void Check(BusinessRuleContext context) => ThrowIfBroken(EvaluateCore(context, null));

        /// <inheritdoc />
        public BusinessRuleResult Evaluate() => new BusinessRuleResult(Code, Message, Severity);

        BusinessRuleResult? IExplainableBusinessRule.EvaluateCore(BusinessRuleContext? context, RuleExplanationTrace? trace)
            => EvaluateCore(context, trace);

        private BusinessRuleResult? EvaluateCore(BusinessRuleContext? context, RuleExplanationTrace? trace)
        {
            RuleExplanationTrace? children = trace?.Branch();
            long start = trace != null ? RuleExplanationTrace.StartTiming() : 0L;

            BusinessRuleResult? operandResult = BusinessRuleWalker.EvaluateNode(_operand, context, children);
            bool isBroken = operandResult == null;

            if (trace != null && children != null)
            {
                trace.Add(BusinessRuleWalker.Evaluated(this, isBroken, RuleExplanationText.NotOperator, RuleExplanationTrace.ElapsedSince(start), children.Nodes));
            }

            return isBroken ? Evaluate() : null;
        }

        private static void ThrowIfBroken(BusinessRuleResult? result)
        {
            if (result != null)
            {
                throw new BusinessRuleValidationException(new[] { result });
            }
        }
    }
}
