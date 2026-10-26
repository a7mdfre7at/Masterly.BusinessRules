using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// A wrapper that caches the result of a rule evaluation for a specified duration.
    /// Useful for expensive rule evaluations that don't change frequently.
    /// </summary>
    /// <remarks>
    /// <para>The cache is thread-safe and uses a lock for synchronization.</para>
    /// <para>By default one verdict is cached per instance, whatever the context. A <c>cacheKeySelector</c> turns the
    /// cache into one verdict per key (for example per tenant: <c>ctx =&gt; ctx.Get(RuleParameters.ScopeKey).ToString()</c>),
    /// which is required when the inner rule reads scoped parameters or any other per-context value. The parameterless
    /// <see cref="IsBroken()"/> / <see cref="Check()"/> have no context to select a key from and always use a dedicated
    /// shared entry. Expired entries are purged whenever a new key is stored, so the cache holds at most one live entry
    /// per key seen within the duration; keep the key space bounded (tenants, regions), not per-request.</para>
    /// <para>Use <see cref="InvalidateCache()"/> to clear every entry before the duration expires, or
    /// <see cref="InvalidateCache(string)"/> to clear one key.</para>
    /// <para>Only the broken verdict is cached: a fresh evaluation reports the inner rule's result including its
    /// <see cref="BusinessRuleResult.Details"/>, while a served cache hit reports the inner rule's
    /// <see cref="IBusinessRule.Evaluate(BusinessRuleContext)"/> factory result, which carries its metadata and
    /// <c>Describe</c> details but no member details.</para>
    /// <para>In an explanation tree a served cache entry is reported as <see cref="RuleOutcome.CacheHit"/> with the
    /// inner rule as a <see cref="RuleOutcome.NotEvaluated"/> child.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// CachedBusinessRule cachedRule = new CachedBusinessRule(
    ///     new ExpensiveValidationRule(),
    ///     TimeSpan.FromMinutes(5)
    /// );
    ///
    /// CachedBusinessRule perTenant = new CachedBusinessRule(
    ///     scopedRule,
    ///     TimeSpan.FromMinutes(5),
    ///     ctx => ctx.Get(RuleParameters.ScopeKey).ToString()
    /// );
    /// </code>
    /// </example>
    public sealed class CachedBusinessRule : IDecoratedBusinessRule, IExplainableBusinessRule
    {
        private readonly IBusinessRule _innerRule;
        private readonly TimeSpan _cacheDuration;
        private readonly Func<BusinessRuleContext, string>? _cacheKeySelector;
        private readonly Dictionary<string, CacheEntry>? _entries;
        private readonly object _lock = new object();
        private CacheEntry? _sharedEntry;

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedBusinessRule"/> class caching one verdict per instance.
        /// </summary>
        /// <param name="innerRule">The rule whose result will be cached.</param>
        /// <param name="cacheDuration">The duration to cache the result.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> is null.</exception>
        public CachedBusinessRule(IBusinessRule innerRule, TimeSpan cacheDuration)
            : this(innerRule, cacheDuration, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedBusinessRule"/> class caching one verdict per cache key.
        /// </summary>
        /// <param name="innerRule">The rule whose result will be cached.</param>
        /// <param name="cacheDuration">The duration to cache each result.</param>
        /// <param name="cacheKeySelector">Selects the cache key from the evaluation context (for example the tenant); null caches one verdict per instance.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> is null.</exception>
        public CachedBusinessRule(IBusinessRule innerRule, TimeSpan cacheDuration, Func<BusinessRuleContext, string>? cacheKeySelector)
        {
            _innerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
            _cacheDuration = cacheDuration;
            _cacheKeySelector = cacheKeySelector;
            _entries = cacheKeySelector != null ? new Dictionary<string, CacheEntry>(StringComparer.Ordinal) : null;
        }

        /// <inheritdoc />
        public IBusinessRule InnerRule => _innerRule;

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
        /// <remarks>Returns cached result if available and not expired; otherwise evaluates and caches. Uses the shared entry, never a per-key one.</remarks>
        public bool IsBroken()
        {
            return EvaluateCore(null, null) != null;
        }

        /// <inheritdoc />
        /// <remarks>Returns cached result if available and not expired; otherwise evaluates and caches.</remarks>
        /// <exception cref="RuleConfigurationException">Thrown when the cache key selector returns null.</exception>
        public bool IsBroken(BusinessRuleContext context)
        {
            return EvaluateCore(context, null) != null;
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the rule is broken; a fresh evaluation carries the inner rule's <see cref="BusinessRuleResult.Details"/>.</exception>
        public void Check()
        {
            ThrowIfBroken(EvaluateCore(null, null));
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the rule is broken; a fresh evaluation carries the inner rule's <see cref="BusinessRuleResult.Details"/>.</exception>
        /// <exception cref="RuleConfigurationException">Thrown when the cache key selector returns null.</exception>
        public void Check(BusinessRuleContext context)
        {
            ThrowIfBroken(EvaluateCore(context, null));
        }

        /// <inheritdoc />
        /// <remarks>Forwards to the inner rule, so its metadata and <c>Describe</c> details are kept.</remarks>
        public BusinessRuleResult Evaluate() => _innerRule.Evaluate();

        /// <inheritdoc />
        /// <remarks>Forwards to the inner rule, so its metadata and context-aware <c>Describe</c> details are kept.</remarks>
        public BusinessRuleResult Evaluate(BusinessRuleContext context) => _innerRule.Evaluate(context);

        /// <summary>
        /// Invalidates every cached result (the shared entry and all per-key entries), forcing the next evaluation to
        /// re-execute the inner rule.
        /// </summary>
        public void InvalidateCache()
        {
            lock (_lock)
            {
                _sharedEntry = null;
                _entries?.Clear();
            }
        }

        /// <summary>
        /// Invalidates the cached result of one cache key, forcing the next evaluation for that key to re-execute the
        /// inner rule. Other keys and the shared entry are untouched; unknown keys are ignored.
        /// </summary>
        /// <param name="cacheKey">The key as returned by the cache key selector.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cacheKey"/> is null.</exception>
        public void InvalidateCache(string cacheKey)
        {
            if (cacheKey == null)
            {
                throw new ArgumentNullException(nameof(cacheKey));
            }

            lock (_lock)
            {
                _entries?.Remove(cacheKey);
            }
        }

        BusinessRuleResult? IExplainableBusinessRule.EvaluateCore(BusinessRuleContext? context, RuleExplanationTrace? trace)
            => EvaluateCore(context, trace);

        private static void ThrowIfBroken(BusinessRuleResult? result)
        {
            if (result != null)
            {
                throw new BusinessRuleValidationException(new[] { result });
            }
        }

        private BusinessRuleResult CachedResult(BusinessRuleContext? context)
        {
            return context != null ? _innerRule.Evaluate(context) : _innerRule.Evaluate();
        }

        private BusinessRuleResult? EvaluateCore(BusinessRuleContext? context, RuleExplanationTrace? trace)
        {
            string? cacheKey = SelectCacheKey(context);

            lock (_lock)
            {
                if (TryGetFresh(cacheKey, out bool cachedIsBroken))
                {
                    trace?.Add(BusinessRuleWalker.CacheHit(this, _innerRule, cachedIsBroken));
                    return cachedIsBroken ? CachedResult(context) : null;
                }

                RuleExplanationTrace? children = trace?.Branch();
                long start = trace != null ? RuleExplanationTrace.StartTiming() : 0L;

                BusinessRuleResult? result = BusinessRuleWalker.EvaluateNode(_innerRule, context, children);
                Store(cacheKey, result != null);

                if (trace != null && children != null)
                {
                    trace.Add(BusinessRuleWalker.Evaluated(this, result != null, RuleExplanationText.CachedOperator, RuleExplanationTrace.ElapsedSince(start), children.Nodes));
                }

                return result;
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
