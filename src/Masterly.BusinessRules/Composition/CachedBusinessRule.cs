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
    /// <para>Only the broken verdict is cached: a fresh evaluation reports the inner rule's result including its
    /// <see cref="BusinessRuleResult.Details"/>, while a served cache hit reports a result without details.</para>
    /// <para>In an explanation tree a served cache entry is reported as <see cref="RuleOutcome.CacheHit"/> with the
    /// inner rule as a <see cref="RuleOutcome.NotEvaluated"/> child.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// CachedBusinessRule cachedRule = new CachedBusinessRule(
    ///     new ExpensiveValidationRule(),
    ///     TimeSpan.FromMinutes(5)
    /// );
    /// </code>
    /// </example>
    public sealed class CachedBusinessRule : IDecoratedBusinessRule, IExplainableBusinessRule
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
        public IBusinessRule InnerRule => _innerRule;

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
            return EvaluateCore(null, null) != null;
        }

        /// <inheritdoc />
        /// <remarks>Returns cached result if available and not expired; otherwise evaluates and caches.</remarks>
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
        public void Check(BusinessRuleContext context)
        {
            ThrowIfBroken(EvaluateCore(context, null));
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

        BusinessRuleResult? IExplainableBusinessRule.EvaluateCore(BusinessRuleContext? context, RuleExplanationTrace? trace)
            => EvaluateCore(context, trace);

        private static void ThrowIfBroken(BusinessRuleResult? result)
        {
            if (result != null)
            {
                throw new BusinessRuleValidationException(new[] { result });
            }
        }

        private BusinessRuleResult? EvaluateCore(BusinessRuleContext? context, RuleExplanationTrace? trace)
        {
            lock (_lock)
            {
                if (_cachedResult.HasValue && DateTime.UtcNow < _cacheExpiry)
                {
                    bool cachedIsBroken = _cachedResult.Value;
                    trace?.Add(BusinessRuleWalker.CacheHit(this, _innerRule, cachedIsBroken));
                    return cachedIsBroken ? Evaluate() : null;
                }

                RuleExplanationTrace? children = trace?.Branch();
                long start = trace != null ? RuleExplanationTrace.StartTiming() : 0L;

                BusinessRuleResult? result = BusinessRuleWalker.EvaluateNode(_innerRule, context, children);
                _cachedResult = result != null;
                _cacheExpiry = DateTime.UtcNow.Add(_cacheDuration);

                if (trace != null && children != null)
                {
                    trace.Add(BusinessRuleWalker.Evaluated(this, result != null, RuleExplanationText.CachedOperator, RuleExplanationTrace.ElapsedSince(start), children.Nodes));
                }

                return result;
            }
        }
    }
}
