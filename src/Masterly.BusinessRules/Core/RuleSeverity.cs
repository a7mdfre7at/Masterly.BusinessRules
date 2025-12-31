namespace Masterly.BusinessRules
{
    /// <summary>
    /// Defines the severity levels for business rule violations.
    /// </summary>
    public enum RuleSeverity
    {
        /// <summary>
        /// Informational level - the rule violation is for information purposes only.
        /// </summary>
        Info,

        /// <summary>
        /// Warning level - the rule violation should be noted but may not block the operation.
        /// </summary>
        Warning,

        /// <summary>
        /// Error level - the rule violation indicates a serious issue that should block the operation.
        /// </summary>
        Error
    }
}