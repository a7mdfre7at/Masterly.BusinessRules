namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// A node of the immutable rule expression tree. Nodes carry their 1-based source position, their nesting
    /// <see cref="Depth"/> and a normalised <see cref="Text"/> rendering used in explanations and error messages.
    /// </summary>
    internal abstract class RuleExpressionNode
    {
        private string? _text;

        protected RuleExpressionNode(int line, int column)
        {
            Line = line;
            Column = column;
        }

        public int Line { get; }

        public int Column { get; }

        /// <summary>
        /// Gets the number of nodes on the longest path from this node to a leaf, this node included.
        /// </summary>
        public abstract int Depth { get; }

        /// <summary>
        /// Gets the normalised source text of the node.
        /// </summary>
        public string Text => _text ??= RuleExpressionTextRenderer.Render(this);

        /// <summary>
        /// Gets whether the node is a value path (identifier, parameter or member chain) whose bound value an
        /// explanation displays.
        /// </summary>
        public virtual bool IsPath => false;

        public abstract TResult Accept<TResult>(IRuleExpressionNodeVisitor<TResult> visitor);

        public override string ToString() => Text;
    }
}
