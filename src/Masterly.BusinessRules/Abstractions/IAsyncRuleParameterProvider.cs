using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Supplies named parameter values to asynchronous rules, optionally layered per <see cref="RuleScope"/>; the
    /// asynchronous twin of <see cref="IRuleParameterProvider"/> for providers backed by a store that must be awaited.
    /// Because an <c>out</c> parameter cannot cross an <c>await</c>, the lookup is reported as a
    /// <see cref="RuleParameterLookup{T}"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// public sealed class RemoteParameterProvider : IAsyncRuleParameterProvider
    /// {
    ///     public async Task&lt;RuleParameterLookup&lt;T&gt;&gt; TryGetAsync&lt;T&gt;(string name, RuleScope? scope, CancellationToken ct = default)
    ///     {
    ///         T? value = await _client.GetAsync&lt;T&gt;(name, scope?.TenantId, ct);
    ///         return value is null ? RuleParameterLookup&lt;T&gt;.Missing : RuleParameterLookup&lt;T&gt;.Of(value);
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IAsyncRuleParameterProvider
    {
        /// <summary>
        /// Tries to resolve the value of <paramref name="name"/> for <paramref name="scope"/>. A provider that layers
        /// values should return the most specific value whose scope applies to the requested one (see
        /// <see cref="RuleScope.AppliesTo"/>) and fall back to its global value; an unknown parameter is reported as
        /// <see cref="RuleParameterLookup{T}.Missing"/> so the rule's declared fallback is used.
        /// </summary>
        /// <typeparam name="T">The type the rule declared for the parameter.</typeparam>
        /// <param name="name">The parameter name.</param>
        /// <param name="scope">The scope of the evaluation, or null for the global scope.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task producing the lookup: found with its value, or missing.</returns>
        Task<RuleParameterLookup<T>> TryGetAsync<T>(string name, RuleScope? scope, CancellationToken cancellationToken = default);
    }
}
