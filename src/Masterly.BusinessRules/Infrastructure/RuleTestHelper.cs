using System;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Helper utilities for testing business rules in unit tests.
    /// Provides assertion methods and factory methods for creating test rules.
    /// </summary>
    /// <example>
    /// <code>
    /// // Assert a rule is broken
    /// RuleTestHelper.AssertBroken(new AgeValidationRule(age: 15));
    ///
    /// // Assert with context
    /// BusinessRuleContext ctx = RuleTestHelper.CreateContext(("userId", 123));
    /// RuleTestHelper.AssertNotBroken(myRule, ctx);
    ///
    /// // Typed keys of different value types in one call
    /// BusinessRuleContext typed = RuleTestHelper.CreateContext((OrderKeys.Order, order), (OrderKeys.Limit, 1000m));
    ///
    /// // Create test rules
    /// IBusinessRule brokenRule = RuleTestHelper.CreateBrokenRule("CUSTOM.CODE", "Custom message");
    /// IBusinessRule passingRule = RuleTestHelper.CreatePassingRule();
    /// </code>
    /// </example>
    public static class RuleTestHelper
    {
        /// <summary>
        /// Asserts that the rule is broken.
        /// </summary>
        /// <param name="rule">The rule to check.</param>
        /// <exception cref="RuleAssertionException">Thrown if the rule is not broken.</exception>
        public static void AssertBroken(IBusinessRule rule)
        {
            if (!rule.IsBroken())
                throw new RuleAssertionException($"Expected rule '{rule.Code}' to be broken, but it was not.");
        }

        /// <summary>
        /// Asserts that the rule is broken with the given context.
        /// </summary>
        /// <param name="rule">The rule to check.</param>
        /// <param name="context">The context for rule evaluation.</param>
        /// <exception cref="RuleAssertionException">Thrown if the rule is not broken.</exception>
        public static void AssertBroken(IBusinessRule rule, BusinessRuleContext context)
        {
            if (!rule.IsBroken(context))
                throw new RuleAssertionException($"Expected rule '{rule.Code}' to be broken, but it was not.");
        }

        /// <summary>
        /// Asserts that the rule is not broken (passes).
        /// </summary>
        /// <param name="rule">The rule to check.</param>
        /// <exception cref="RuleAssertionException">Thrown if the rule is broken.</exception>
        public static void AssertNotBroken(IBusinessRule rule)
        {
            if (rule.IsBroken())
                throw new RuleAssertionException($"Expected rule '{rule.Code}' to pass, but it was broken: {rule.Message}");
        }

        /// <summary>
        /// Asserts that the rule is not broken with the given context.
        /// </summary>
        /// <param name="rule">The rule to check.</param>
        /// <param name="context">The context for rule evaluation.</param>
        /// <exception cref="RuleAssertionException">Thrown if the rule is broken.</exception>
        public static void AssertNotBroken(IBusinessRule rule, BusinessRuleContext context)
        {
            if (rule.IsBroken(context))
                throw new RuleAssertionException($"Expected rule '{rule.Code}' to pass, but it was broken: {rule.Message}");
        }

        /// <summary>
        /// Asserts that the async rule is broken.
        /// </summary>
        /// <param name="rule">The async rule to check.</param>
        /// <param name="context">Optional context for rule evaluation. Defaults to empty context.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <exception cref="RuleAssertionException">Thrown if the rule is not broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task AssertBrokenAsync(
            IAsyncBusinessRule rule,
            BusinessRuleContext? context = null,
            CancellationToken cancellationToken = default)
        {
            BusinessRuleContext ctx = context ?? new BusinessRuleContext();
            if (!await rule.IsBrokenAsync(ctx, cancellationToken))
                throw new RuleAssertionException($"Expected async rule '{rule.Code}' to be broken, but it was not.");
        }

        /// <summary>
        /// Asserts that the async rule is not broken (passes).
        /// </summary>
        /// <param name="rule">The async rule to check.</param>
        /// <param name="context">Optional context for rule evaluation. Defaults to empty context.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <exception cref="RuleAssertionException">Thrown if the rule is broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task AssertNotBrokenAsync(
            IAsyncBusinessRule rule,
            BusinessRuleContext? context = null,
            CancellationToken cancellationToken = default)
        {
            BusinessRuleContext ctx = context ?? new BusinessRuleContext();
            if (await rule.IsBrokenAsync(ctx, cancellationToken))
                throw new RuleAssertionException($"Expected async rule '{rule.Code}' to pass, but it was broken: {rule.Message}");
        }

        /// <summary>
        /// Creates a context with the specified key-value pairs.
        /// </summary>
        /// <param name="items">Key-value pairs to add to the context.</param>
        /// <returns>A new context populated with the specified values.</returns>
        public static BusinessRuleContext CreateContext(params (string key, object value)[] items)
        {
            BusinessRuleContext context = new BusinessRuleContext();
            foreach ((string key, object value) in items)
            {
                context.Set(key, value);
            }
            return context;
        }

        /// <summary>
        /// Creates a context holding one typed key/value pair. For more than four pairs, call <see cref="BusinessRuleContext.Set{T}(ContextKey{T}, T)"/>.
        /// </summary>
        /// <typeparam name="T1">The value type of the pair, inferred from the key.</typeparam>
        /// <param name="first">The typed key and the value to store under it.</param>
        /// <returns>A new context populated with the pair.</returns>
        public static BusinessRuleContext CreateContext<T1>((ContextKey<T1> key, T1 value) first)
        {
            BusinessRuleContext context = new BusinessRuleContext();
            context.Set(first.key, first.value);
            return context;
        }

        /// <summary>
        /// Creates a context holding two typed key/value pairs.
        /// </summary>
        /// <typeparam name="T1">The value type of the first pair, inferred from its key.</typeparam>
        /// <typeparam name="T2">The value type of the second pair, inferred from its key.</typeparam>
        /// <param name="first">The first typed key and value.</param>
        /// <param name="second">The second typed key and value.</param>
        /// <returns>A new context populated with the pairs.</returns>
        public static BusinessRuleContext CreateContext<T1, T2>(
            (ContextKey<T1> key, T1 value) first,
            (ContextKey<T2> key, T2 value) second)
        {
            BusinessRuleContext context = CreateContext(first);
            context.Set(second.key, second.value);
            return context;
        }

        /// <summary>
        /// Creates a context holding three typed key/value pairs.
        /// </summary>
        /// <typeparam name="T1">The value type of the first pair, inferred from its key.</typeparam>
        /// <typeparam name="T2">The value type of the second pair, inferred from its key.</typeparam>
        /// <typeparam name="T3">The value type of the third pair, inferred from its key.</typeparam>
        /// <param name="first">The first typed key and value.</param>
        /// <param name="second">The second typed key and value.</param>
        /// <param name="third">The third typed key and value.</param>
        /// <returns>A new context populated with the pairs.</returns>
        public static BusinessRuleContext CreateContext<T1, T2, T3>(
            (ContextKey<T1> key, T1 value) first,
            (ContextKey<T2> key, T2 value) second,
            (ContextKey<T3> key, T3 value) third)
        {
            BusinessRuleContext context = CreateContext(first, second);
            context.Set(third.key, third.value);
            return context;
        }

        /// <summary>
        /// Creates a context holding four typed key/value pairs.
        /// </summary>
        /// <typeparam name="T1">The value type of the first pair, inferred from its key.</typeparam>
        /// <typeparam name="T2">The value type of the second pair, inferred from its key.</typeparam>
        /// <typeparam name="T3">The value type of the third pair, inferred from its key.</typeparam>
        /// <typeparam name="T4">The value type of the fourth pair, inferred from its key.</typeparam>
        /// <param name="first">The first typed key and value.</param>
        /// <param name="second">The second typed key and value.</param>
        /// <param name="third">The third typed key and value.</param>
        /// <param name="fourth">The fourth typed key and value.</param>
        /// <returns>A new context populated with the pairs.</returns>
        public static BusinessRuleContext CreateContext<T1, T2, T3, T4>(
            (ContextKey<T1> key, T1 value) first,
            (ContextKey<T2> key, T2 value) second,
            (ContextKey<T3> key, T3 value) third,
            (ContextKey<T4> key, T4 value) fourth)
        {
            BusinessRuleContext context = CreateContext(first, second, third);
            context.Set(fourth.key, fourth.value);
            return context;
        }

        /// <summary>
        /// Creates a typed context with the specified data object.
        /// </summary>
        /// <typeparam name="T">The type of the data object.</typeparam>
        /// <param name="data">The data object to wrap in the context.</param>
        /// <returns>A new typed context containing the data object.</returns>
        public static BusinessRuleContext<T> CreateContext<T>(T data) where T : class
        {
            return new BusinessRuleContext<T>(data);
        }

        /// <summary>
        /// Creates an always-broken test rule.
        /// </summary>
        /// <param name="code">The rule code. Defaults to "TEST_BROKEN".</param>
        /// <param name="message">The rule message. Defaults to "Test broken rule".</param>
        /// <returns>A rule that always returns true from IsBroken().</returns>
        public static IBusinessRule CreateBrokenRule(string code = "TEST_BROKEN", string message = "Test broken rule")
        {
            return BusinessRuleBuilder.Create(code)
                .WithMessage(message)
                .When(() => true)
                .Build();
        }

        /// <summary>
        /// Creates an always-passing test rule.
        /// </summary>
        /// <param name="code">The rule code. Defaults to "TEST_PASSING".</param>
        /// <param name="message">The rule message. Defaults to "Test passing rule".</param>
        /// <returns>A rule that always returns false from IsBroken().</returns>
        public static IBusinessRule CreatePassingRule(string code = "TEST_PASSING", string message = "Test passing rule")
        {
            return BusinessRuleBuilder.Create(code)
                .WithMessage(message)
                .When(() => false)
                .Build();
        }

        /// <summary>
        /// Creates an always-broken async test rule.
        /// </summary>
        /// <param name="code">The rule code. Defaults to "TEST_BROKEN_ASYNC".</param>
        /// <param name="message">The rule message. Defaults to "Test broken async rule".</param>
        /// <returns>An async rule that always returns true from IsBrokenAsync().</returns>
        public static IAsyncBusinessRule CreateBrokenAsyncRule(string code = "TEST_BROKEN_ASYNC", string message = "Test broken async rule")
        {
            return AsyncBusinessRuleBuilder.Create(code)
                .WithMessage(message)
                .WhenAsync((ctx, ct) => Task.FromResult(true))
                .Build();
        }

        /// <summary>
        /// Creates an always-passing async test rule.
        /// </summary>
        /// <param name="code">The rule code. Defaults to "TEST_PASSING_ASYNC".</param>
        /// <param name="message">The rule message. Defaults to "Test passing async rule".</param>
        /// <returns>An async rule that always returns false from IsBrokenAsync().</returns>
        public static IAsyncBusinessRule CreatePassingAsyncRule(string code = "TEST_PASSING_ASYNC", string message = "Test passing async rule")
        {
            return AsyncBusinessRuleBuilder.Create(code)
                .WithMessage(message)
                .WhenAsync((ctx, ct) => Task.FromResult(false))
                .Build();
        }

        /// <summary>
        /// Creates an in-memory feature flag provider (usable by sync and async gated rules) with one flag set.
        /// Set further flags with <see cref="InMemoryRuleFeatureFlagProvider.Set(string, RuleRolloutMode, int)"/>;
        /// flags that were never set report <see cref="RuleRollout.Off"/>.
        /// </summary>
        /// <param name="flag">The feature flag to set.</param>
        /// <param name="mode">The mode the gated rule runs in for callers inside the cohort.</param>
        /// <param name="percent">The percentage of stable keys inside the cohort. Defaults to everybody.</param>
        /// <returns>The provider.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="flag"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="flag"/> is empty or white space.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mode"/> is undefined or <paramref name="percent"/> is outside 0..100.</exception>
        public static InMemoryRuleFeatureFlagProvider CreateFlagProvider(string flag, RuleRolloutMode mode, int percent = RuleRollout.MaxPercent)
        {
            return new InMemoryRuleFeatureFlagProvider().Set(flag, mode, percent);
        }

        /// <summary>
        /// Creates an in-memory parameter provider (usable by sync and async built rules) with one value set.
        /// Set further values with <see cref="InMemoryRuleParameterProvider.Set{T}(string, T, RuleScope)"/>;
        /// parameters that were never set resolve to the rule's declared fallback.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The value.</param>
        /// <param name="scope">The scope the value applies to; null means <see cref="RuleScope.Global"/>.</param>
        /// <returns>The provider.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or white space.</exception>
        public static InMemoryRuleParameterProvider CreateParameterProvider<T>(string name, T value, RuleScope? scope = null)
        {
            return new InMemoryRuleParameterProvider().Set(name, value, scope ?? RuleScope.Global);
        }
    }

    /// <summary>
    /// Exception thrown when a rule assertion fails in unit tests.
    /// </summary>
    public sealed class RuleAssertionException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RuleAssertionException"/> class.
        /// </summary>
        /// <param name="message">The message describing the assertion failure.</param>
        public RuleAssertionException(string message) : base(message) { }
    }
}
