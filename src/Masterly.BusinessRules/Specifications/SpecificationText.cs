namespace Masterly.BusinessRules
{
    /// <summary>
    /// String constants shared by the specification combinators and guards so that a composed specification reports
    /// the same synthesised code, message and description wherever it was built.
    /// </summary>
    internal static class SpecificationText
    {
        public const string AllOfCodePrefix = "ALL(";
        public const string AllOfMessagePrefix = "All rules must be satisfied: ";
        public const string AllOfMessageSeparator = " AND ";
        public const string AllOfDescription = "Satisfied only when every operand is satisfied; broken when any operand is broken.";

        public const string AnyOfCodePrefix = "ANY(";
        public const string AnyOfMessagePrefix = "At least one rule must be satisfied: ";
        public const string AnyOfMessageSeparator = " OR ";
        public const string AnyOfDescription = "Satisfied when any operand is satisfied; broken only when every operand is broken.";

        public const string NotDescription = "Satisfied when the operand is broken; broken when the operand is satisfied.";

        public const string CodeSeparator = ",";
        public const string CodeSuffix = ")";

        public const string MissingBrokenWhen = "BrokenWhen returned null; a specification rule must supply its broken predicate as an expression.";
        public const string MissingBrokenExpression = "BrokenExpression returned null; a specification rule must expose its broken predicate as an expression.";
        public const string MissingSatisfiedExpression = "SatisfiedExpression returned null; a specification rule must expose its satisfied predicate as an expression.";
        public const string EmptyOperands = "A specification combinator needs at least one operand.";
        public const string NullOperandFormat = "The operand at position {0} is null.";
    }
}
