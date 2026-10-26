namespace Masterly.BusinessRules
{
    /// <summary>
    /// String constants shared by the And/Or/Not nodes of every hierarchy (sync, async and subject-typed) so that
    /// a composed rule reports the same synthesised code, message and description wherever it was built.
    /// </summary>
    internal static class RuleCompositionText
    {
        public const string AndCodeSeparator = "+";
        public const string AndMessagePrefix = "Both rules must be satisfied: ";
        public const string AndMessageSeparator = " AND ";
        public const string AndDescription = "Broken only when both operands are broken.";

        public const string OrCodeSeparator = "|";
        public const string OrMessagePrefix = "At least one rule must be satisfied: ";
        public const string OrMessageSeparator = " OR ";
        public const string OrDescription = "Broken when either operand is broken.";

        public const string NotCodePrefix = "!";
        public const string NotMessagePrefix = "NOT: ";
        public const string NotDescription = "Broken when the operand passes.";
    }
}
