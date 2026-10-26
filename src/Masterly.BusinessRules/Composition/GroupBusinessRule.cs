using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// An immutable N-ary quantifier over member rules, created through <see cref="RuleGroup"/>.
    /// The group counts how many members pass and compares the count with <see cref="Threshold"/> according to
    /// <see cref="Quantifier"/>. By default every member is evaluated so that <see cref="BusinessRuleResult.Details"/>
    /// lists every broken member; <see cref="WithShortCircuit"/> returns a copy that stops as soon as the verdict is decided.
    /// The context passed to <see cref="IsBroken(BusinessRuleContext)"/> is forwarded to every member.
    /// </summary>
    /// <example>
    /// <code>
    /// IBusinessRule kyc = RuleGroup.All("KYC.INCOMPLETE", "All KYC checks must pass", idRule, addressRule).WithShortCircuit();
    /// IBusinessRule payment = RuleGroup.Any("PAYMENT.NONE_VALID", "At least one payment method must be valid", card, bank);
    /// </code>
    /// </example>
    public sealed class GroupBusinessRule : IComposedBusinessRule, IExplainableBusinessRule
    {
        private static readonly IReadOnlyList<string> EmptyTags = Array.Empty<string>();
        private static readonly IReadOnlyList<BusinessRuleResult> NoDetails = Array.Empty<BusinessRuleResult>();

        private readonly IReadOnlyList<IBusinessRule> _members;

        internal GroupBusinessRule(
            RuleGroupQuantifier quantifier,
            int threshold,
            string code,
            string message,
            RuleSeverity severity,
            IReadOnlyList<IBusinessRule> members,
            bool shortCircuit)
        {
            Quantifier = quantifier;
            Threshold = threshold;
            Code = code;
            Message = message;
            Severity = severity;
            _members = members;
            ShortCircuit = shortCircuit;
        }

        /// <inheritdoc />
        public string Code { get; }

        /// <inheritdoc />
        public string Message { get; }

        /// <inheritdoc />
        public RuleSeverity Severity { get; }

        /// <inheritdoc />
        public string Name => GetType().Name;

        /// <inheritdoc />
        public string Description => RuleGroupQuantifierLogic.Describe(Quantifier, Threshold, _members.Count);

        /// <inheritdoc />
        public string Category => string.Empty;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => EmptyTags;

        /// <summary>
        /// Gets the quantifier applied to the members.
        /// </summary>
        public RuleGroupQuantifier Quantifier { get; }

        /// <summary>
        /// Gets the number of members that must pass: the member count for <see cref="RuleGroupQuantifier.All"/>,
        /// one for <see cref="RuleGroupQuantifier.Any"/>, and the requested count for <c>AtLeast</c>/<c>Exactly</c>.
        /// </summary>
        public int Threshold { get; }

        /// <summary>
        /// Gets whether evaluation stops as soon as the verdict is decided.
        /// </summary>
        public bool ShortCircuit { get; }

        /// <inheritdoc />
        public RuleOperator Operator => RuleGroupQuantifierLogic.ToOperator(Quantifier);

        /// <inheritdoc />
        public IReadOnlyList<IBusinessRule> Operands => _members;

        /// <summary>
        /// Returns a copy of this group that stops evaluating members as soon as the verdict is decided.
        /// Members that were not reached are reported as <see cref="RuleOutcome.NotEvaluated"/> by <c>Explain</c>.
        /// </summary>
        /// <returns>A new short-circuiting group; this instance is unchanged.</returns>
        public GroupBusinessRule WithShortCircuit()
        {
            return new GroupBusinessRule(Quantifier, Threshold, Code, Message, Severity, _members, shortCircuit: true);
        }

        /// <summary>
        /// Returns a copy of this group with a different severity.
        /// </summary>
        /// <param name="severity">The severity reported when the group is broken.</param>
        /// <returns>A new group; this instance is unchanged.</returns>
        public GroupBusinessRule WithSeverity(RuleSeverity severity)
        {
            return new GroupBusinessRule(Quantifier, Threshold, Code, Message, severity, _members, ShortCircuit);
        }

        /// <inheritdoc />
        public bool IsBroken() => EvaluateCore(null, null) != null;

        /// <inheritdoc />
        public bool IsBroken(BusinessRuleContext context) => EvaluateCore(context, null) != null;

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the group is broken; the single result carries the broken members in <see cref="BusinessRuleResult.Details"/>.</exception>
        public void Check() => ThrowIfBroken(EvaluateCore(null, null));

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the group is broken; the single result carries the broken members in <see cref="BusinessRuleResult.Details"/>.</exception>
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
        {
            RuleExplanationTrace? children = trace?.Branch();
            long start = trace != null ? RuleExplanationTrace.StartTiming() : 0L;

            int passedCount = 0;
            int brokenCount = 0;
            int evaluatedCount = 0;
            List<BusinessRuleResult>? details = null;

            for (int index = 0; index < _members.Count; index++)
            {
                BusinessRuleResult? memberResult = BusinessRuleWalker.EvaluateNode(_members[index], context, children);
                evaluatedCount++;

                if (memberResult != null)
                {
                    brokenCount++;
                    details = details ?? new List<BusinessRuleResult>();
                    details.Add(memberResult);
                }
                else
                {
                    passedCount++;
                }

                if (ShortCircuit && RuleGroupQuantifierLogic.IsDecided(Quantifier, Threshold, passedCount, brokenCount, _members.Count))
                {
                    break;
                }
            }

            bool isBroken = RuleGroupQuantifierLogic.IsBroken(Quantifier, Threshold, passedCount);

            if (trace != null && children != null)
            {
                BusinessRuleWalker.AddNotEvaluated(children, _members, evaluatedCount, RuleExplanationText.ShortCircuitReason);
                string operatorName = RuleExplanationText.OperatorLabel(Operator, Threshold);
                trace.Add(BusinessRuleWalker.Evaluated(this, isBroken, operatorName, RuleExplanationTrace.ElapsedSince(start), children.Nodes));
            }

            return isBroken ? RuleResultFactory.Create(this, details ?? NoDetails) : null;
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
