using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// An asynchronous rule whose broken state is computed from other async rules (its operands).
    /// Implemented by <see cref="AndAsyncBusinessRule"/>, <see cref="OrAsyncBusinessRule"/>, <see cref="NotAsyncBusinessRule"/>,
    /// <see cref="GroupAsyncBusinessRule"/> and <see cref="CompositeAsyncBusinessRule"/>, so that tooling can walk a rule tree
    /// without knowing the concrete node types.
    /// </summary>
    public interface IComposedAsyncBusinessRule : IAsyncBusinessRule
    {
        /// <summary>
        /// Gets the operator that combines the operands' broken states.
        /// </summary>
        RuleOperator Operator { get; }

        /// <summary>
        /// Gets the operand rules in declaration order. Never null; empty for a delegate-based
        /// <see cref="CompositeAsyncBusinessRule"/>, which behaves as a leaf.
        /// </summary>
        IReadOnlyList<IAsyncBusinessRule> Operands { get; }
    }
}
