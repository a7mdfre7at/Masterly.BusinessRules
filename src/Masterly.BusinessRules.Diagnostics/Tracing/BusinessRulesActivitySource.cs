using System.Diagnostics;
using System.Reflection;

namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// The single <see cref="ActivitySource"/> every span of this package is started from. Subscribe to it with plain
    /// OpenTelemetry (<c>tracing.AddSource(BusinessRulesActivitySource.Name)</c>) or an <see cref="ActivityListener"/>;
    /// when nobody listens, <see cref="ActivitySource.StartActivity(string, ActivityKind)"/> returns null and the
    /// observers and decorators allocate nothing for tracing.
    /// </summary>
    public static class BusinessRulesActivitySource
    {
        /// <summary>
        /// The source name: <c>Masterly.BusinessRules</c>.
        /// </summary>
        public const string Name = "Masterly.BusinessRules";

        /// <summary>
        /// The operation name of the span wrapping one rule evaluation. Its <see cref="Activity.DisplayName"/> is the
        /// rule code when the <see cref="TracingRuleObserverOptions.CodeTagSelector"/> returns one.
        /// </summary>
        public const string RuleActivityName = "BusinessRule.Evaluate";

        /// <summary>
        /// The operation name of the span wrapping one checker batch; the rule spans of the batch are its children.
        /// </summary>
        public const string BatchActivityName = "BusinessRules.Batch";

        /// <summary>
        /// Gets the shared source. Its version is the package assembly version.
        /// </summary>
        public static ActivitySource Source { get; } = new ActivitySource(Name, ReadVersion());

        private static string ReadVersion()
        {
            return typeof(BusinessRulesActivitySource).GetTypeInfo().Assembly.GetName().Version?.ToString() ?? string.Empty;
        }
    }
}
