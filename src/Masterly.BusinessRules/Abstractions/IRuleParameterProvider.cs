namespace Masterly.BusinessRules
{
    /// <summary>
    /// Supplies named parameter values (<c>maxOrderAmount</c>, <c>minimumAge</c>) to synchronous rules, optionally
    /// layered per <see cref="RuleScope"/> so one rule serves every tenant with its own limits. A rule declares its
    /// parameters with a fallback (<c>WithParameter&lt;T&gt;(name, fallback)</c>) and reads the resolved values from
    /// <see cref="RuleParameters"/>; the provider is consulted on every evaluation, so a changed value takes effect on the
    /// next call without rebuilding the rule.
    /// </summary>
    /// <example>
    /// <code>
    /// public sealed class ConfigurationParameterProvider : IRuleParameterProvider
    /// {
    ///     public bool TryGet&lt;T&gt;(string name, RuleScope? scope, out T value)
    ///         => _settings.TryResolve(name, scope?.TenantId, out value);
    /// }
    /// </code>
    /// </example>
    public interface IRuleParameterProvider
    {
        /// <summary>
        /// Tries to resolve the value of <paramref name="name"/> for <paramref name="scope"/>. A provider that layers
        /// values should return the most specific value whose scope applies to the requested one (see
        /// <see cref="RuleScope.AppliesTo"/>) and fall back to its global value; an unknown parameter returns false so
        /// the rule's declared fallback is used.
        /// </summary>
        /// <typeparam name="T">The type the rule declared for the parameter.</typeparam>
        /// <param name="name">The parameter name.</param>
        /// <param name="scope">The scope of the evaluation, or null for the global scope.</param>
        /// <param name="value">The resolved value, or <c>default</c> when the method returns false.</param>
        /// <returns>True when a value was resolved.</returns>
        bool TryGet<T>(string name, RuleScope? scope, out T value);
    }
}
