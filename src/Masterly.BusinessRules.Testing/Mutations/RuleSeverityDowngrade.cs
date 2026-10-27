namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// Maps a severity to the next lower one; <see cref="RuleSeverity.Info"/> is the floor.
    /// </summary>
    internal static class RuleSeverityDowngrade
    {
        public static RuleSeverity Of(RuleSeverity severity)
        {
            switch (severity)
            {
                case RuleSeverity.Error:
                    return RuleSeverity.Warning;
                case RuleSeverity.Warning:
                    return RuleSeverity.Info;
                default:
                    return RuleSeverity.Info;
            }
        }
    }
}
