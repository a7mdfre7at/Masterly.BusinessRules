namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// The lower-case values written to the <c>business_rule.severity</c> span attribute and the <c>severity</c> metric tag.
    /// </summary>
    public static class RuleSeverityTags
    {
        /// <summary>
        /// The value for <see cref="RuleSeverity.Info"/>.
        /// </summary>
        public const string Info = "info";

        /// <summary>
        /// The value for <see cref="RuleSeverity.Warning"/>.
        /// </summary>
        public const string Warning = "warning";

        /// <summary>
        /// The value for <see cref="RuleSeverity.Error"/>.
        /// </summary>
        public const string Error = "error";

        /// <summary>
        /// Returns the tag value for a severity. An undefined enum value falls back to its lower-case name.
        /// </summary>
        /// <param name="severity">The severity.</param>
        /// <returns>The tag value.</returns>
        public static string From(RuleSeverity severity)
        {
            switch (severity)
            {
                case RuleSeverity.Info:
                    return Info;
                case RuleSeverity.Warning:
                    return Warning;
                case RuleSeverity.Error:
                    return Error;
                default:
                    return severity.ToString().ToLowerInvariant();
            }
        }
    }
}
