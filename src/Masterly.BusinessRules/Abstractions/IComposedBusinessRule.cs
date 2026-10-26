using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// A synchronous rule whose broken state is computed from other rules (its operands).
    /// Implemented by <see cref="AndBusinessRule"/>, <see cref="OrBusinessRule"/>, <see cref="NotBusinessRule"/>,
    /// <see cref="GroupBusinessRule"/> and <see cref="CompositeBusinessRule"/>, so that tooling can walk a rule tree
    /// without knowing the concrete node types.
    /// </summary>
    public interface IComposedBusinessRule : IBusinessRule
    {
        /// <summary>
        /// Gets the operator that combines the operands' broken states.
        /// </summary>
        RuleOperator Operator { get; }

        /// <summary>
        /// Gets the operand rules in declaration order. Never null; a single element for <see cref="RuleOperator.Not"/>.
        /// </summary>
        IReadOnlyList<IBusinessRule> Operands { get; }
    }
}
