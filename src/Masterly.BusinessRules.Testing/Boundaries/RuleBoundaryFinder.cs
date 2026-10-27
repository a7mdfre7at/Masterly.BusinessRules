using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// Locates the exact value at which a factory-built rule flips between passing and broken by bisecting the grid
    /// <c>from, from + step, …, to</c>. The step is mandatory because it defines the resolution of the answer
    /// (<c>passes up to 10000.00, breaks from 10000.01</c>). The finder takes a factory rather than a context key
    /// because rules usually receive their data through the constructor. After bisection it probes
    /// <see cref="MonotonicitySampleCount"/> evenly spread values and throws <see cref="RuleAssertionException"/> naming the
    /// contradicting pair when one disagrees with the located boundary, so a non-monotone rule is reported instead of
    /// a meaningless boundary.
    /// </summary>
    /// <example>
    /// <code>
    /// RuleBoundary&lt;decimal&gt; boundary = RuleBoundaryFinder.Find&lt;decimal&gt;(
    ///     amount => new TransactionLimitRule(amount), from: 0m, to: 1_000_000m, step: 0.01m);
    /// // boundary.LastPassing == 10000.00m, boundary.FirstBroken == 10000.01m, boundary.Monotonicity == BrokenAbove
    /// </code>
    /// </example>
    public static class RuleBoundaryFinder
    {
        /// <summary>
        /// How many evenly spread grid points are probed after bisection to guard against a non-monotone rule.
        /// </summary>
        public const int MonotonicitySampleCount = 32;

        /// <summary>
        /// Finds the boundary of a synchronous rule family. Each probe builds a fresh rule with <paramref name="factory"/>
        /// and evaluates its parameterless <see cref="IBusinessRule.IsBroken()"/>.
        /// </summary>
        /// <typeparam name="T">The numeric input type (<see cref="decimal"/>, <see cref="int"/>, <see cref="double"/>, …).</typeparam>
        /// <param name="factory">Builds the rule for one input value.</param>
        /// <param name="from">The lower bound of the search, inclusive.</param>
        /// <param name="to">The upper bound of the search, inclusive.</param>
        /// <param name="step">The resolution of the answer; the boundary values are exactly one step apart.</param>
        /// <returns>The located boundary.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a bound is not finite, <paramref name="from"/> is not below <paramref name="to"/>, or the range holds fewer than two or too many grid points.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="step"/> is not positive or too small to move the lower bound.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the factory returns null.</exception>
        /// <exception cref="RuleAssertionException">Thrown when the rule has the same verdict at both bounds, or a sample contradicts the located boundary.</exception>
        public static RuleBoundary<T> Find<T>(Func<T, IBusinessRule> factory, T from, T to, T step) where T : struct, INumber<T>
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            BoundaryGrid<T> grid = new BoundaryGrid<T>(from, to, step);
            BoundarySearch<T> search = new BoundarySearch<T>(grid, MonotonicitySampleCount);

            while (search.TryNextProbe(out long index))
            {
                T value = grid.ValueAt(index);
                IBusinessRule rule = factory(value) ?? throw new InvalidOperationException(RuleBoundaryText.NullRule(value));
                search.Record(rule.IsBroken());
            }

            return search.Result();
        }

        /// <summary>
        /// Finds the boundary of an asynchronous rule family. Each probe builds a fresh rule with <paramref name="factory"/>
        /// and evaluates <see cref="IAsyncBusinessRule.IsBrokenAsync"/> with one empty context shared by the whole search.
        /// </summary>
        /// <typeparam name="T">The numeric input type (<see cref="decimal"/>, <see cref="int"/>, <see cref="double"/>, …).</typeparam>
        /// <param name="factory">Builds the async rule for one input value.</param>
        /// <param name="from">The lower bound of the search, inclusive.</param>
        /// <param name="to">The upper bound of the search, inclusive.</param>
        /// <param name="step">The resolution of the answer; the boundary values are exactly one step apart.</param>
        /// <param name="cancellationToken">A token to cancel the search; checked before every probe.</param>
        /// <returns>A task producing the located boundary.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a bound is not finite, <paramref name="from"/> is not below <paramref name="to"/>, or the range holds fewer than two or too many grid points.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="step"/> is not positive or too small to move the lower bound.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the factory returns null.</exception>
        /// <exception cref="RuleAssertionException">Thrown when the rule has the same verdict at both bounds, or a sample contradicts the located boundary.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public static async Task<RuleBoundary<T>> FindAsync<T>(
            Func<T, IAsyncBusinessRule> factory,
            T from,
            T to,
            T step,
            CancellationToken cancellationToken = default) where T : struct, INumber<T>
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            BoundaryGrid<T> grid = new BoundaryGrid<T>(from, to, step);
            BoundarySearch<T> search = new BoundarySearch<T>(grid, MonotonicitySampleCount);
            BusinessRuleContext context = new BusinessRuleContext();

            while (search.TryNextProbe(out long index))
            {
                cancellationToken.ThrowIfCancellationRequested();

                T value = grid.ValueAt(index);
                IAsyncBusinessRule rule = factory(value) ?? throw new InvalidOperationException(RuleBoundaryText.NullRule(value));
                search.Record(await rule.IsBrokenAsync(context, cancellationToken));
            }

            return search.Result();
        }
    }
}
