using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// A wrapper that caches the result of an async rule evaluation for a specified duration.
    /// Useful for expensive async rule evaluations that don't change frequently.
    /// </summary>
    /// <remarks>
    /// <para>The cache is thread-safe. A semaphore guards only the cache lookups and stores: the inner rule is awaited
    /// outside it, so a slow evaluation for one key never blocks cache hits or evaluations for other keys (concurrent
    /// misses for the same key may each evaluate the inner rule; the last verdict stored wins). A verdict whose
    /// evaluation was invalidated while it ran is not stored.</para>
    /// <para>A verdict that rests on a fault captured by <c>ExplainAsync(captureExceptions: true)</c> is returned but
    /// never cached, so a diagnostic explanation cannot poison the verdict later plain evaluations read.</para>
    /// <para>By default one verdict is cached per instance, whatever the context. A <c>cacheKeySelector</c> turns the
    /// cache into one verdict per key (for example per tenant: <c>ctx =&gt; ctx.Get(RuleParameters.ScopeKey).ToString()</c>),
    /// which is required when the inner rule reads scoped parameters or any other per-context value. Expired entries
    /// are purged whenever a new key is stored, so the cache holds at most one live entry per key seen within the
    /// duration; keep the key space bounded (tenants, regions), not per-request.</para>
    /// <para>Use <see cref="InvalidateCacheAsync(CancellationToken)"/> to clear every entry before the duration expires, or
    /// <see cref="InvalidateCacheAsync(string, CancellationToken)"/> to clear one key.</para>
    /// <para>Only the broken verdict is cached: a fresh evaluation reports the inner rule's result including its
    /// <see cref="BusinessRuleResult.Details"/> and <c>Describe</c> details, while a served cache hit reports a result
    /// carrying the forwarded metadata only, because an async rule has no result factory to call without evaluating.</para>
    /// <para>A cancelled evaluation never populates the cache. In an explanation tree a served cache entry is reported as
    /// <see cref="RuleOutcome.CacheHit"/> with the inner rule as a <see cref="RuleOutcome.NotEvaluated"/> child.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// CachedAsyncBusinessRule cachedRule = new CachedAsyncBusinessRule(
    ///     new ExpensiveDatabaseRule(),
    ///     TimeSpan.FromMinutes(5)
    /// );
    ///
    /// CachedAsyncBusinessRule perTenant = new CachedAsyncBusinessRule(
    ///     scopedRule,
    ///     TimeSpan.FromMinutes(5),
    ///     ctx => ctx.Get(RuleParameters.ScopeKey).ToString()
    /// );
    /// </code>
    /// </example>
    public sealed class CachedAsyncBusinessRule : IDecoratedAsyncBusinessRule, IExplainableAsyncBusinessRule
    {
        private readonly IAsyncBusinessRule _innerRule;
        private readonly TimeSpan _cacheDuration;
        private readonly Func<BusinessRuleContext, string>? _cacheKeySelector;
        private readonly Dictionary<string, CacheEntry>? _entries;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private CacheEntry? _sharedEntry;
        private long _generation;

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedAsyncBusinessRule"/> class caching one verdict per instance.
        /// </summary>
        /// <param name="innerRule">The async rule whose result will be cached.</param>
        /// <param name="cacheDuration">The duration to cache the result.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> is null.</exception>
        public CachedAsyncBusinessRule(IAsyncBusinessRule innerRule, TimeSpan cacheDuration)
            : this(innerRule, cacheDuration, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedAsyncBusinessRule"/> class caching one verdict per cache key.
        /// </summary>
        /// <param name="innerRule">The async rule whose result will be cached.</param>
        /// <param name="cacheDuration">The duration to cache each result.</param>
        /// <param name="cacheKeySelector">Selects the cache key from the evaluation context (for example the tenant); null caches one verdict per instance.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> is null.</exception>
        public CachedAsyncBusinessRule(IAsyncBusinessRule innerRule, TimeSpan cacheDuration, Func<BusinessRuleContext, string>? cacheKeySelector)
        {
            _innerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
            _cacheDuration = cacheDuration;
            _cacheKeySelector = cacheKeySelector;
            _entries = cacheKeySelector != null ? new Dictionary<string, CacheEntry>(StringComparer.Ordinal) : null;
        }

        /// <inheritdoc />
        public IAsyncBusinessRule InnerRule => _innerRule;

        /// <summary>
        /// Gets whether a cache key selector was supplied, so verdicts are cached per key rather than per instance.
        /// </summary>
        public bool CachesPerKey => _cacheKeySelector != null;

        /// <inheritdoc />
        public string Code => _innerRule.Code;

        /// <inheritdoc />
        public string Message => _innerRule.Message;

        /// <inheritdoc />
        public RuleSeverity Severity => _innerRule.Severity;

        /// <inheritdoc />
        public string Name => _innerRule.Name;

        /// <inheritdoc />
        public string Description => _innerRule.Description;

        /// <inheritdoc />
        public string Category => _innerRule.Category;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => _innerRule.Tags;

        /// <inheritdoc />
        /// <remarks>Returns cached result if available and not expired; otherwise evaluates and caches.</remarks>
        /// <exception cref="RuleConfigurationException">Thrown when the cache key selector returns null.</exception>
        public async Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            return await EvaluateCoreAsync(context, null, cancellationToken) != null;
        }

        /// <inheritdoc />
        /// <remarks>
        /// When the inner rule is evaluated its result (including <see cref="BusinessRuleResult.Details"/> of a composed
        /// inner rule) is returned as is; a served cache hit returns a result without details because only the verdict is cached.
        /// </remarks>
        /// <exception cref="RuleConfigurationException">Thrown when the cache key selector returns null.</exception>
        public Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            return EvaluateCoreAsync(context, null, cancellationToken);
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the rule is broken.</exception>
        /// <exception cref="RuleConfigurationException">Thrown when the cache key selector returns null.</exception>
        public async Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            BusinessRuleResult? result = await EvaluateCoreAsync(context, null, cancellationToken);
            if (result != null)
            {
                throw new BusinessRuleValidationException(new[] { result });
            }
        }

        /// <summary>
        /// Invalidates every cached result (the shared entry and all per-key entries) asynchronously, forcing the next
        /// evaluation to re-execute the inner rule.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvalidateCacheAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _sharedEntry = null;
                _entries?.Clear();
                _generation++;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Invalidates the cached result of one cache key asynchronously, forcing the next evaluation for that key to
        /// re-execute the inner rule. Other keys and the shared entry are untouched; unknown keys are ignored.
        /// </summary>
        /// <param name="cacheKey">The key as returned by the cache key selector.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cacheKey"/> is null.</exception>
        public async Task InvalidateCacheAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            if (cacheKey == null)
            {
                throw new ArgumentNullException(nameof(cacheKey));
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _entries?.Remove(cacheKey);
                _generation++;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        Task<BusinessRuleResult?> IExplainableAsyncBusinessRule.EvaluateCoreAsync(BusinessRuleContext context, RuleExplanationTrace? trace, CancellationToken cancellationToken)
            => EvaluateCoreAsync(context, trace, cancellationToken);

        private async Task<BusinessRuleResult?> EvaluateCoreAsync(BusinessRuleContext context, RuleExplanationTrace? trace, CancellationToken cancellationToken)
        {
            string? cacheKey = SelectCacheKey(context);
            bool isHit;
            bool cachedIsBroken;
            long generation;

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                isHit = TryGetFresh(cacheKey, out cachedIsBroken);
                generation = _generation;
            }
            finally
            {
                _semaphore.Release();
            }

            if (isHit)
            {
                trace?.Add(AsyncBusinessRuleWalker.CacheHit(this, _innerRule, cachedIsBroken));
                return cachedIsBroken ? RuleResultFactory.Create(this) : null;
            }

            RuleExplanationTrace? children = trace?.Branch();
            long start = trace != null ? RuleExplanationTrace.StartTiming() : 0L;

            BusinessRuleResult? result = await AsyncBusinessRuleWalker.EvaluateNodeAsync(_innerRule, context, children, cancellationToken);
            if (children == null || !children.HasCapturedFault)
            {
                await StoreIfCurrentAsync(cacheKey, result != null, generation);
            }

            if (trace != null && children != null)
            {
                trace.Add(AsyncBusinessRuleWalker.Evaluated(this, result != null, RuleExplanationText.CachedOperator, RuleExplanationTrace.ElapsedSince(start), children.Nodes));
            }

            return result;
        }

        private async Task StoreIfCurrentAsync(string? cacheKey, bool isBroken, long generation)
        {
            // The verdict is already computed: storing it is not cancellable, so a late cancellation cannot lose it.
            await _semaphore.WaitAsync(CancellationToken.None);
            try
            {
                if (generation == _generation)
                {
                    Store(cacheKey, isBroken);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private string? SelectCacheKey(BusinessRuleContext? context)
        {
            if (_cacheKeySelector == null || context == null)
            {
                return null;
            }

            return _cacheKeySelector(context) ?? throw new RuleConfigurationException(Code, $"The cache key selector of rule {Code} returned null; a cache key is required to cache per context.");
        }

        private bool TryGetFresh(string? cacheKey, out bool cachedIsBroken)
        {
            CacheEntry? entry;
            if (cacheKey == null)
            {
                entry = _sharedEntry;
            }
            else
            {
                entry = _entries!.TryGetValue(cacheKey, out CacheEntry stored) ? stored : (CacheEntry?)null; // _entries is non-null whenever a key was selected
            }

            if (entry.HasValue && DateTime.UtcNow < entry.Value.Expiry)
            {
                cachedIsBroken = entry.Value.IsBroken;
                return true;
            }

            cachedIsBroken = false;
            return false;
        }

        private void Store(string? cacheKey, bool isBroken)
        {
            CacheEntry entry = new CacheEntry(isBroken, DateTime.UtcNow.Add(_cacheDuration));
            if (cacheKey == null)
            {
                _sharedEntry = entry;
                return;
            }

            PurgeExpired();
            _entries![cacheKey] = entry; // _entries is non-null whenever a key was selected
        }

        private void PurgeExpired()
        {
            DateTime now = DateTime.UtcNow;
            List<string>? expired = null;
            foreach (KeyValuePair<string, CacheEntry> pair in _entries!) // called only from Store with a key, so _entries exists
            {
                if (now >= pair.Value.Expiry)
                {
                    expired ??= new List<string>();
                    expired.Add(pair.Key);
                }
            }

            if (expired == null)
            {
                return;
            }

            foreach (string key in expired)
            {
                _entries.Remove(key);
            }
        }

        private readonly struct CacheEntry
        {
            public CacheEntry(bool isBroken, DateTime expiry)
            {
                IsBroken = isBroken;
                Expiry = expiry;
            }

            public bool IsBroken { get; }

            public DateTime Expiry { get; }
        }
    }
}
