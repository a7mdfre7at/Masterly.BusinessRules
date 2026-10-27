using System;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// Marks a property, or every property of a type, as readable by rule expressions when
    /// <see cref="RuleExpressionOptions.RequireRuleAccessibleAttribute"/> is on. Use it to publish a deliberate subset of
    /// a domain model to analyst-edited expressions instead of every public property.
    /// </summary>
    /// <example>
    /// <code>
    /// public sealed class Customer
    /// {
    ///     [RuleAccessible]
    ///     public string Tier { get; set; }
    ///
    ///     public string Ssn { get; set; }   // not readable from an expression
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
    public sealed class RuleAccessibleAttribute : Attribute
    {
    }
}
