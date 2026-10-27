using System.Collections.Generic;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// Records what the interpreter bound and computed during one walk: the ordered <see cref="Steps"/> of an
    /// explanation and a per-node lookup for the renderer. Bound values pass through the optional redactor before
    /// they are stored, so a redacted value never reaches the explanation in clear.
    /// </summary>
    internal sealed class RuleExpressionBindingRecorder
    {
        private readonly List<RuleExpressionStep> _steps = new List<RuleExpressionStep>();
        private readonly Dictionary<RuleExpressionNode, RuleExpressionStep> _byNode = new Dictionary<RuleExpressionNode, RuleExpressionStep>();
        private readonly RuleExpressionRedactor? _redact;

        public RuleExpressionBindingRecorder(RuleExpressionRedactor? redact)
        {
            _redact = redact;
        }

        public IReadOnlyList<RuleExpressionStep> Steps => _steps;

        public void RecordBinding(RuleExpressionNode node, object? value)
        {
            object? shown = _redact == null ? value : _redact(node.Text, value);
            Record(node, new RuleExpressionStep(RuleExpressionStepKind.Binding, node.Text, shown, shown));
        }

        public void RecordOperation(RuleExpressionNode node, object? result)
        {
            Record(node, new RuleExpressionStep(RuleExpressionStepKind.Operation, node.Text, null, result));
        }

        public bool TryGetStep(RuleExpressionNode node, out RuleExpressionStep step) => _byNode.TryGetValue(node, out step);

        /// <summary>
        /// Gets whether a redactor is in effect, in which case displayed values are already strings.
        /// </summary>
        public bool IsRedacting => _redact != null;

        private void Record(RuleExpressionNode node, RuleExpressionStep step)
        {
            _steps.Add(step);
            _byNode[node] = step;
        }
    }
}
