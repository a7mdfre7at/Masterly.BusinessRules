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
    /// <para>The cache is thread-safe and uses a semaphore for async synchronization.</para>
    /// <para>Use <see cref="InvalidateCacheAsync"/> to clear the cache before the duration expires.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// CachedAsyncBusinessRule cachedRule = new CachedAsyncBusinessRule(
    ///     new ExpensiveDatabaseRule(),
    ///     TimeSpan.FromMinutes(5)
    /// );
    /// </code>
    /// </example>
    public sealed class CachedAsyncBusinessRule : IAsyncBusinessRule
    {
        private readonly IAsyncBusinessRule _innerRule;
        private readonly TimeSpan _cacheDuration;
        private bool? _cachedResult;
        private DateTime _cacheExpiry;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedAsyncBusinessRule"/> class.
        /// </summary>
        /// <param name="innerRule">The async rule whose result will be cached.</param>
        /// <param name="cacheDuration">The duration to cache the result.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> is null.</exception>
        public CachedAsyncBusinessRule(IAsyncBusinessRule innerRule, TimeSpan cacheDuration)
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
        public async Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_cachedResult.HasValue && DateTime.UtcNow < _cacheExpiry)
                    return _cachedResult.Value;

                _cachedResult = await _innerRule.IsBrokenAsync(context, cancellationToken);
                _cacheExpiry = DateTime.UtcNow.Add(_cacheDuration);
                return _cachedResult.Value;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            if (await IsBrokenAsync(context, cancellationToken))
                return new BusinessRuleResult(Code, Message, Severity);
            return null;
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the rule is broken.</exception>
        public async Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            if (await IsBrokenAsync(context, cancellationToken))
                throw new BusinessRuleValidationException(new[] { new BusinessRuleResult(Code, Message, Severity) });
        }

        /// <summary>
        /// Invalidates the cached result asynchronously, forcing the next evaluation to re-execute the inner rule.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvalidateCacheAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _cachedResult = null;
                _cacheExpiry = DateTime.MinValue;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
