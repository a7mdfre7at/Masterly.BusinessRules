using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules.DependencyInjection
{
    /// <summary>
    /// Named rule sets layered per <see cref="RuleScope"/>: register the checkout rules once, override them for one
    /// tenant (disable a code, add a rule, replace a rule by code), and resolve the merged set for the scope of a
    /// request. The scope is an explicit argument, never read from ambient state. Resolution merges the applicable
    /// layers from the least to the most specific (<see cref="RuleScope.Specificity"/>), equally specific layers in
    /// declaration order, so the result is deterministic. Nothing is cached: merging a handful of layers is cheap and a
    /// cache keyed by high-cardinality scopes would be unbounded. Build one with <see cref="RuleRegistry.Create"/> or
    /// let <c>services.AddBusinessRules(o =&gt; o.AddRuleSet(...))</c> register it as a singleton.
    /// <para>Scope and context: the requested scope (null meaning <see cref="RuleScope.Global"/>) chooses the layers and is
    /// the scope parameterized rules resolve their values for. The batch methods store it under
    /// <see cref="RuleParameters.ScopeKey"/> for the duration of the call, replacing whatever the context held, and put
    /// the previous entry back afterwards, so one context can be reused across scopes without leaking a scope.
    /// <see cref="Resolve"/> and <see cref="ResolveAsyncRules"/> store it and leave it there, because the returned rules
    /// are evaluated later against that context.</para>
    /// <para>Sync and async rules: <c>CheckAllAsync</c>/<c>EvaluateAllAsync</c> evaluate the whole set, the sync rules
    /// (adapted with <c>ToAsync()</c>) followed by the async ones, like <c>IRuleRunner&lt;T&gt;</c>. <c>CheckAll</c>/<c>EvaluateAll</c>
    /// throw <see cref="System.InvalidOperationException"/> naming the codes when the resolved set has async rules, rather
    /// than silently skipping them.</para>
    /// </summary>
    /// <example>
    /// <code>
    /// IReadOnlyList&lt;IBusinessRule&gt; rules = registry.Resolve("checkout", RuleScope.Tenant("acme").With("region", "EU"), context);
    /// registry.CheckAll("checkout", RuleScope.Tenant("acme"), context);
    /// await registry.CheckAllAsync("checkout", RuleScope.Tenant("acme"), context, cancellationToken);
    /// </code>
    /// </example>
    public interface IRuleRegistry
    {
        /// <summary>
        /// Gets the registered set names, sorted ordinally.
        /// </summary>
        IReadOnlyCollection<string> SetNames { get; }

        /// <summary>
        /// Returns whether a set with <paramref name="setName"/> was registered (ordinal comparison).
        /// </summary>
        /// <param name="setName">The set name.</param>
        /// <returns>True when at least one layer was declared under the name.</returns>
        bool Contains(string setName);

        /// <summary>
        /// Creates the sync rules of a set for a scope: the applicable layers are merged (least specific first, declaration
        /// order on ties), each layer removing the codes it disables from the rules merged so far and appending its own.
        /// Every factory receives <paramref name="context"/>; the requested scope is stored under
        /// <see cref="RuleParameters.ScopeKey"/> first (replacing any value), so the rules, evaluated later with this
        /// context, resolve their parameters for the same scope their layers were chosen for.
        /// </summary>
        /// <param name="setName">The set name.</param>
        /// <param name="scope">The scope of the request, or null for <see cref="RuleScope.Global"/>.</param>
        /// <param name="context">The evaluation context handed to the factories, or null for a fresh context.</param>
        /// <returns>The resolved rules, in merge order. Empty when no applicable layer adds a sync rule.</returns>
        /// <exception cref="RuleSetNotFoundException">Thrown when the set was never registered.</exception>
        IReadOnlyList<IBusinessRule> Resolve(string setName, RuleScope? scope = null, BusinessRuleContext? context = null);

        /// <summary>
        /// Creates the async rules of a set for a scope, with the same merge and scope semantics as <see cref="Resolve"/>.
        /// </summary>
        /// <param name="setName">The set name.</param>
        /// <param name="scope">The scope of the request, or null for <see cref="RuleScope.Global"/>.</param>
        /// <param name="context">The evaluation context handed to the factories, or null for a fresh context.</param>
        /// <returns>The resolved async rules, in merge order. Empty when no applicable layer adds an async rule.</returns>
        /// <exception cref="RuleSetNotFoundException">Thrown when the set was never registered.</exception>
        IReadOnlyList<IAsyncBusinessRule> ResolveAsyncRules(string setName, RuleScope? scope = null, BusinessRuleContext? context = null);

        /// <summary>
        /// Resolves the sync rules of the set and checks them with <see cref="BusinessRuleChecker"/>, throwing if any are broken.
        /// </summary>
        /// <param name="setName">The set name.</param>
        /// <param name="scope">The scope of the request, or null for <see cref="RuleScope.Global"/>.</param>
        /// <param name="context">The evaluation context, or null for a fresh context.</param>
        /// <exception cref="RuleSetNotFoundException">Thrown when the set was never registered.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the resolved set also has async rules; call <c>CheckAllAsync</c>.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any resolved rule is broken.</exception>
        void CheckAll(string setName, RuleScope? scope = null, BusinessRuleContext? context = null);

        /// <summary>
        /// Resolves the sync rules of the set and checks them with the supplied options.
        /// </summary>
        /// <param name="setName">The set name.</param>
        /// <param name="scope">The scope of the request, or null for <see cref="RuleScope.Global"/>.</param>
        /// <param name="context">The evaluation context, or null for a fresh context.</param>
        /// <param name="options">The evaluation options: fail-fast, fault policy, severity threshold and observer.</param>
        /// <exception cref="RuleSetNotFoundException">Thrown when the set was never registered.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when a broken rule is at or above <see cref="RuleEvaluationOptions.ThrowOn"/>.</exception>
        void CheckAll(string setName, RuleScope? scope, BusinessRuleContext? context, RuleEvaluationOptions options);

        /// <summary>
        /// Resolves the sync rules of the set and evaluates them without throwing for broken rules.
        /// </summary>
        /// <param name="setName">The set name.</param>
        /// <param name="scope">The scope of the request, or null for <see cref="RuleScope.Global"/>.</param>
        /// <param name="context">The evaluation context, or null for a fresh context.</param>
        /// <returns>The batch result.</returns>
        /// <exception cref="RuleSetNotFoundException">Thrown when the set was never registered.</exception>
        RuleEvaluationResult EvaluateAll(string setName, RuleScope? scope = null, BusinessRuleContext? context = null);

        /// <summary>
        /// Resolves the sync rules of the set and evaluates them with the supplied options.
        /// </summary>
        /// <param name="setName">The set name.</param>
        /// <param name="scope">The scope of the request, or null for <see cref="RuleScope.Global"/>.</param>
        /// <param name="context">The evaluation context, or null for a fresh context.</param>
        /// <param name="options">The evaluation options.</param>
        /// <returns>The batch result with its ledger.</returns>
        /// <exception cref="RuleSetNotFoundException">Thrown when the set was never registered.</exception>
        RuleEvaluationResult EvaluateAll(string setName, RuleScope? scope, BusinessRuleContext? context, RuleEvaluationOptions options);

        /// <summary>
        /// Resolves the whole set (sync rules adapted with <c>ToAsync()</c>, then async rules) and checks it with
        /// <see cref="AsyncBusinessRuleChecker"/>, throwing if any are broken.
        /// </summary>
        /// <param name="setName">The set name.</param>
        /// <param name="scope">The scope of the request, or null for <see cref="RuleScope.Global"/>.</param>
        /// <param name="context">The evaluation context, or null for a fresh context.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="RuleSetNotFoundException">Thrown when the set was never registered.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any resolved rule is broken.</exception>
        Task CheckAllAsync(string setName, RuleScope? scope = null, BusinessRuleContext? context = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves the whole set (sync rules adapted with <c>ToAsync()</c>, then async rules) and checks it with the supplied options.
        /// </summary>
        /// <param name="setName">The set name.</param>
        /// <param name="scope">The scope of the request, or null for <see cref="RuleScope.Global"/>.</param>
        /// <param name="context">The evaluation context, or null for a fresh context.</param>
        /// <param name="options">The evaluation options: fail-fast, parallel, fault policy, severity threshold and observer.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="RuleSetNotFoundException">Thrown when the set was never registered.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when a broken rule is at or above <see cref="AsyncRuleEvaluationOptions.ThrowOn"/>.</exception>
        Task CheckAllAsync(string setName, RuleScope? scope, BusinessRuleContext? context, AsyncRuleEvaluationOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves the whole set (sync rules adapted with <c>ToAsync()</c>, then async rules) and evaluates it without
        /// throwing for broken rules.
        /// </summary>
        /// <param name="setName">The set name.</param>
        /// <param name="scope">The scope of the request, or null for <see cref="RuleScope.Global"/>.</param>
        /// <param name="context">The evaluation context, or null for a fresh context.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task producing the batch result.</returns>
        /// <exception cref="RuleSetNotFoundException">Thrown when the set was never registered.</exception>
        Task<AsyncRuleEvaluationResult> EvaluateAllAsync(string setName, RuleScope? scope = null, BusinessRuleContext? context = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves the whole set (sync rules adapted with <c>ToAsync()</c>, then async rules) and evaluates it with the supplied options.
        /// </summary>
        /// <param name="setName">The set name.</param>
        /// <param name="scope">The scope of the request, or null for <see cref="RuleScope.Global"/>.</param>
        /// <param name="context">The evaluation context, or null for a fresh context.</param>
        /// <param name="options">The evaluation options.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task producing the batch result with its ledger.</returns>
        /// <exception cref="RuleSetNotFoundException">Thrown when the set was never registered.</exception>
        Task<AsyncRuleEvaluationResult> EvaluateAllAsync(string setName, RuleScope? scope, BusinessRuleContext? context, AsyncRuleEvaluationOptions options, CancellationToken cancellationToken = default);
    }
}
