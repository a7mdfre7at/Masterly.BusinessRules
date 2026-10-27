using System;
using CsCheck;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// Entry point for property-based rule testing on top of CsCheck generators: state laws a rule family must obey
    /// (<c>MonotoneIn</c>, <c>ContextPure</c>) and verify them over hundreds of generated inputs; a violation is shrunk
    /// to the smallest counter-example and reported as a <see cref="RuleAssertionException"/> together with the seed
    /// that reproduces it.
    /// </summary>
    /// <example>
    /// <code>
    /// RuleProperties.For&lt;decimal&gt;(amount => new TransactionLimitRule(amount), Gen.Decimal[0m, 100_000m])
    ///     .MonotoneIn(Monotonicity.BrokenAbove)
    ///     .ContextPure()
    ///     .Verify(iterations: 500, seed: 42);
    /// </code>
    /// </example>
    public static class RuleProperties
    {
        /// <summary>
        /// The number of generated inputs each law is checked against when <c>Verify</c> is called without a count.
        /// </summary>
        public const int DefaultIterations = 100;

        /// <summary>
        /// Starts a property set for a synchronous rule family.
        /// </summary>
        /// <typeparam name="T">The input type the generator produces and the factory consumes.</typeparam>
        /// <param name="factory">Builds the rule for one input value.</param>
        /// <param name="generator">The CsCheck generator producing inputs, for example <c>Gen.Decimal[0m, 100_000m]</c>.</param>
        /// <returns>An empty property set; add laws and call <see cref="RuleProperties{T}.Verify"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> or <paramref name="generator"/> is null.</exception>
        public static RuleProperties<T> For<T>(Func<T, IBusinessRule> factory, Gen<T> generator)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (generator == null)
            {
                throw new ArgumentNullException(nameof(generator));
            }

            return new RuleProperties<T>(factory, generator);
        }

        /// <summary>
        /// Starts a property set for an asynchronous rule family.
        /// </summary>
        /// <typeparam name="T">The input type the generator produces and the factory consumes.</typeparam>
        /// <param name="factory">Builds the async rule for one input value.</param>
        /// <param name="generator">The CsCheck generator producing inputs, for example <c>Gen.Int[0, 120]</c>.</param>
        /// <returns>An empty property set; add laws and await <see cref="AsyncRuleProperties{T}.VerifyAsync"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> or <paramref name="generator"/> is null.</exception>
        public static AsyncRuleProperties<T> For<T>(Func<T, IAsyncBusinessRule> factory, Gen<T> generator)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (generator == null)
            {
                throw new ArgumentNullException(nameof(generator));
            }

            return new AsyncRuleProperties<T>(factory, generator);
        }
    }
}
