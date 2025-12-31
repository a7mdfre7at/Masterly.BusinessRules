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
    /// <para>Use <see cref="InvalidateCache"/> to clear the cache before the duration expires.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// CachedBusinessRule cachedRule = new CachedBusinessRule(
    ///     new ExpensiveValidationRule(),
    ///     TimeSpan.FromMinutes(5)
    /// );
    /// </code>
    /// </example>
    public sealed class CachedBusinessRule : IBusinessRule
    {
        private readonly IBusinessRule _innerRule;
        private readonly TimeSpan _cacheDuration;
        private bool? _cachedResult;
        private DateTime _cacheExpiry;
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedBusinessRule"/> class.
        /// </summary>
        /// <param name="innerRule">The rule whose result will be cached.</param>
        /// <param name="cacheDuration">The duration to cache the result.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> is null.</exception>
        public CachedBusinessRule(IBusinessRule innerRule, TimeSpan cacheDuration)
        {
            _innerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
            _cacheDuration = cacheDuration;
            _cacheExpiry = DateTime.MinValue;
        }

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
        public bool IsBroken()
        {
            lock (_lock)
            {
                if (_cachedResult.HasValue && DateTime.UtcNow < _cacheExpiry)
                    return _cachedResult.Value;

                _cachedResult = _innerRule.IsBroken();
                _cacheExpiry = DateTime.UtcNow.Add(_cacheDuration);
                return _cachedResult.Value;
            }
        }

        /// <inheritdoc />
        /// <remarks>Returns cached result if available and not expired; otherwise evaluates and caches.</remarks>
        public bool IsBroken(BusinessRuleContext context)
        {
            lock (_lock)
            {
                if (_cachedResult.HasValue && DateTime.UtcNow < _cacheExpiry)
                    return _cachedResult.Value;

                _cachedResult = _innerRule.IsBroken(context);
                _cacheExpiry = DateTime.UtcNow.Add(_cacheDuration);
                return _cachedResult.Value;
            }
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the rule is broken.</exception>
        public void Check()
        {
            if (IsBroken())
                throw new BusinessRuleValidationException(new[] { Evaluate() });
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the rule is broken.</exception>
        public void Check(BusinessRuleContext context)
        {
            if (IsBroken(context))
                throw new BusinessRuleValidationException(new[] { Evaluate() });
        }

        /// <inheritdoc />
        public BusinessRuleResult Evaluate() => new BusinessRuleResult(Code, Message, Severity);

        /// <summary>
        /// Invalidates the cached result, forcing the next evaluation to re-execute the inner rule.
        /// </summary>
        public void InvalidateCache()
        {
            lock (_lock)
            {
                _cachedResult = null;
                _cacheExpiry = DateTime.MinValue;
            }
        }
    }
}
