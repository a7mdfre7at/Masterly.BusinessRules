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
