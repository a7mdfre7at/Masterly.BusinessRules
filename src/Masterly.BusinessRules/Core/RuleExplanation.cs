using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// A node in the explanation tree produced by <see cref="BusinessRuleExplanationExtensions.Explain(IBusinessRule, BusinessRuleContext?, bool)"/>
    /// and <see cref="AsyncBusinessRuleExplanationExtensions.ExplainAsync(IAsyncBusinessRule, BusinessRuleContext, System.Threading.CancellationToken)"/>.
    /// Every node records the rule, its <see cref="RuleOutcome"/>, the operator that produced it, an optional reason,
    /// the time spent evaluating it and its child nodes. The tree is produced by the one real evaluation of the rule;
    /// no rule is evaluated a second time to build it.
    /// </summary>
    public sealed class RuleExplanation
    {
        private const string ChildBranch = " ├─ ";
        private const string LastChildBranch = " └─ ";
        private const string ChildContinuation = " │  ";
        private const string LastChildContinuation = "    ";
        private const string ColumnSeparator = "  ";
        private const string ReasonPrefix = "reason: ";
        private const string DurationFormat = "0.0";
        private const string DurationUnit = " ms";
        private const char Quote = '"';
        private static readonly IReadOnlyList<RuleExplanation> NoChildren = Array.Empty<RuleExplanation>();

        /// <summary>
        /// Initializes an explanation node for a synchronous rule.
        /// </summary>
        /// <param name="rule">The explained rule.</param>
        /// <param name="outcome">What happened to the rule.</param>
        /// <param name="isBroken">Whether the rule counts as broken for its parent.</param>
        /// <param name="operatorName">The operator label of a composed or decorated node; empty for a leaf.</param>
        /// <param name="reason">Why the outcome occurred (for example <c>short-circuit</c>); empty when not applicable.</param>
        /// <param name="duration">The time spent evaluating this node including its children.</param>
        /// <param name="fault">The exception captured when <paramref name="outcome"/> is <see cref="RuleOutcome.Faulted"/>; otherwise null.</param>
        /// <param name="children">The child nodes in evaluation order; null for none.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public RuleExplanation(
            IBusinessRule rule,
            RuleOutcome outcome,
            bool isBroken,
            string operatorName,
            string reason,
            TimeSpan duration,
            Exception? fault,
            IEnumerable<RuleExplanation>? children)
            : this(
                rule ?? throw new ArgumentNullException(nameof(rule)),
                null,
                rule.Code,
                rule.Message,
                rule.Severity,
                outcome,
                isBroken,
                operatorName,
                reason,
                duration,
                fault,
                children)
        {
        }

        /// <summary>
        /// Initializes an explanation node for an asynchronous rule.
        /// </summary>
        /// <param name="rule">The explained async rule.</param>
        /// <param name="outcome">What happened to the rule.</param>
        /// <param name="isBroken">Whether the rule counts as broken for its parent.</param>
        /// <param name="operatorName">The operator label of a composed or decorated node; empty for a leaf.</param>
        /// <param name="reason">Why the outcome occurred (for example <c>short-circuit</c>); empty when not applicable.</param>
        /// <param name="duration">The time spent evaluating this node including its children.</param>
        /// <param name="fault">The exception captured when <paramref name="outcome"/> is <see cref="RuleOutcome.Faulted"/>; otherwise null.</param>
        /// <param name="children">The child nodes in evaluation order; null for none.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public RuleExplanation(
            IAsyncBusinessRule rule,
            RuleOutcome outcome,
            bool isBroken,
            string operatorName,
            string reason,
            TimeSpan duration,
            Exception? fault,
            IEnumerable<RuleExplanation>? children)
            : this(
                null,
                rule ?? throw new ArgumentNullException(nameof(rule)),
                rule.Code,
                rule.Message,
                rule.Severity,
                outcome,
                isBroken,
                operatorName,
                reason,
                duration,
                fault,
                children)
        {
        }

        private RuleExplanation(
            IBusinessRule? rule,
            IAsyncBusinessRule? asyncRule,
            string code,
            string message,
            RuleSeverity severity,
            RuleOutcome outcome,
            bool isBroken,
            string operatorName,
            string reason,
            TimeSpan duration,
            Exception? fault,
            IEnumerable<RuleExplanation>? children)
        {
            Rule = rule;
            AsyncRule = asyncRule;
            Code = code;
            Message = message;
            Severity = severity;
            Outcome = outcome;
            IsBroken = isBroken;
            Operator = operatorName ?? RuleExplanationText.LeafOperator;
            Reason = reason ?? RuleExplanationText.NoReason;
            Duration = duration;
            Fault = fault;
            Children = children == null ? NoChildren : children.ToArray();
        }

        /// <summary>
        /// Gets the explained synchronous rule, or null when this node explains an async rule (see <see cref="AsyncRule"/>).
        /// </summary>
        public IBusinessRule? Rule { get; }

        /// <summary>
        /// Gets the explained asynchronous rule, or null when this node explains a sync rule (see <see cref="Rule"/>).
        /// </summary>
        public IAsyncBusinessRule? AsyncRule { get; }

        /// <summary>
        /// Gets the code of the explained rule.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the message of the explained rule.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the severity of the explained rule.
        /// </summary>
        public RuleSeverity Severity { get; }

        /// <summary>
        /// Gets what happened to the rule during the walk.
        /// </summary>
        public RuleOutcome Outcome { get; }

        /// <summary>
        /// Gets whether the rule counted as broken for its parent. True for <see cref="RuleOutcome.Broken"/> and
        /// <see cref="RuleOutcome.Faulted"/>, false for <see cref="RuleOutcome.Passed"/>, <see cref="RuleOutcome.Skipped"/>
        /// and <see cref="RuleOutcome.NotEvaluated"/>, and the cached verdict for <see cref="RuleOutcome.CacheHit"/>.
        /// </summary>
        public bool IsBroken { get; }

        /// <summary>
        /// Gets the operator label of a composed or decorated node (for example <c>ALL</c>, <c>AND</c>, <c>AT LEAST 2</c>, <c>CACHED</c>);
        /// empty for a leaf rule.
        /// </summary>
        public string Operator { get; }

        /// <summary>
        /// Gets the reason for the outcome (for example <c>short-circuit</c>, <c>precondition not met</c>, <c>cache hit</c>
        /// or the captured exception); empty when not applicable.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Gets the time spent evaluating this node including its children. Zero for nodes that were not evaluated.
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Gets the exception captured for a <see cref="RuleOutcome.Faulted"/> node; otherwise null.
        /// </summary>
        public Exception? Fault { get; }

        /// <summary>
        /// Gets the child nodes in evaluation order. Empty for a leaf rule.
        /// </summary>
        public IReadOnlyList<RuleExplanation> Children { get; }

        /// <summary>
        /// Returns the deepest broken nodes of the tree: the broken nodes that have no broken child.
        /// For a leaf rule that is the node itself; for a group it is the broken members; for <c>Not</c> it is the <c>Not</c> node.
        /// </summary>
        /// <returns>The broken leaves in evaluation order; empty when this node is not broken.</returns>
        public IEnumerable<RuleExplanation> BrokenLeaves()
        {
            if (!IsBroken)
            {
                yield break;
            }

            bool anyBrokenChild = false;
            foreach (RuleExplanation child in Children)
            {
                if (!child.IsBroken)
                {
                    continue;
                }

                anyBrokenChild = true;
                foreach (RuleExplanation leaf in child.BrokenLeaves())
                {
                    yield return leaf;
                }
            }

            if (!anyBrokenChild)
            {
                yield return this;
            }
        }

        /// <summary>
        /// Renders the tree as indented ASCII text, one node per line, in the form
        /// <c>[OUTCOME] OPERATOR  CODE  "message"  (duration)  reason: ...</c>.
        /// </summary>
        /// <returns>The rendered tree without a trailing newline.</returns>
        public string ToText()
        {
            List<string> lines = new List<string>();
            CollectLines(lines, string.Empty, isRoot: true, isLast: true);
            return string.Join(Environment.NewLine, lines);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return ToText();
        }

        private void CollectLines(List<string> lines, string prefix, bool isRoot, bool isLast)
        {
            string branch = isRoot ? string.Empty : prefix + (isLast ? LastChildBranch : ChildBranch);
            lines.Add(branch + FormatLine());

            string childPrefix = isRoot ? string.Empty : prefix + (isLast ? LastChildContinuation : ChildContinuation);
            for (int index = 0; index < Children.Count; index++)
            {
                Children[index].CollectLines(lines, childPrefix, isRoot: false, isLast: index == Children.Count - 1);
            }
        }

        private string FormatLine()
        {
            StringBuilder line = new StringBuilder();
            line.Append('[').Append(RuleExplanationText.OutcomeLabel(Outcome)).Append(']');

            if (Operator.Length > 0)
            {
                line.Append(' ').Append(Operator);
            }

            line.Append(ColumnSeparator).Append(Code);

            if (IsBroken && Outcome != RuleOutcome.NotEvaluated)
            {
                line.Append(ColumnSeparator).Append(Quote).Append(Message).Append(Quote);
            }

            if (WasEvaluated(Outcome))
            {
                line.Append(ColumnSeparator)
                    .Append('(')
                    .Append(Duration.TotalMilliseconds.ToString(DurationFormat, CultureInfo.InvariantCulture))
                    .Append(DurationUnit)
                    .Append(')');
            }

            if (Reason.Length > 0)
            {
                line.Append(ColumnSeparator).Append(ReasonPrefix).Append(Reason);
            }

            return line.ToString();
        }

        private static bool WasEvaluated(RuleOutcome outcome)
        {
            return outcome == RuleOutcome.Passed || outcome == RuleOutcome.Broken || outcome == RuleOutcome.Faulted;
        }
    }
}
