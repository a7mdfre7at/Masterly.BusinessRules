using System;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// The value at which a factory-built rule flips between passing and broken, as found by
    /// <see cref="RuleBoundaryFinder.Find{T}"/> / <see cref="RuleBoundaryFinder.FindAsync{T}"/>.
    /// <see cref="LastPassing"/> and <see cref="FirstBroken"/> are adjacent grid points exactly one <see cref="Step"/> apart;
    /// which of them is the larger one is told by <see cref="Monotonicity"/>.
    /// </summary>
    /// <typeparam name="T">The numeric input type of the rule factory.</typeparam>
    public sealed class RuleBoundary<T> where T : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RuleBoundary{T}"/> class.
        /// </summary>
        /// <param name="lastPassing">The grid point adjacent to the boundary at which the rule passes.</param>
        /// <param name="firstBroken">The grid point adjacent to the boundary at which the rule is broken.</param>
        /// <param name="monotonicity">Whether the broken region lies above or below the boundary.</param>
        /// <param name="probeCount">How many rules the finder built and evaluated.</param>
        /// <param name="step">The grid resolution the boundary was located at.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="probeCount"/> is negative or <paramref name="monotonicity"/> is undefined.</exception>
        public RuleBoundary(T lastPassing, T firstBroken, Monotonicity monotonicity, int probeCount, T step)
        {
            if (probeCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(probeCount), probeCount, RuleBoundaryText.NegativeProbeCount);
            }

            if (!Enum.IsDefined(typeof(Monotonicity), monotonicity))
            {
                throw new ArgumentOutOfRangeException(nameof(monotonicity), monotonicity, RuleBoundaryText.UndefinedMonotonicity);
            }

            LastPassing = lastPassing;
            FirstBroken = firstBroken;
            Monotonicity = monotonicity;
            ProbeCount = probeCount;
            Step = step;
        }

        /// <summary>
        /// Gets the grid point adjacent to the boundary at which the rule passes: the largest passing value for
        /// <see cref="Testing.Monotonicity.BrokenAbove"/>, the smallest passing value for <see cref="Testing.Monotonicity.BrokenBelow"/>.
        /// </summary>
        public T LastPassing { get; }

        /// <summary>
        /// Gets the grid point adjacent to the boundary at which the rule is broken: the smallest broken value for
        /// <see cref="Testing.Monotonicity.BrokenAbove"/>, the largest broken value for <see cref="Testing.Monotonicity.BrokenBelow"/>.
        /// </summary>
        public T FirstBroken { get; }

        /// <summary>
        /// Gets whether the rule breaks above (<see cref="FirstBroken"/> &gt; <see cref="LastPassing"/>) or below the boundary.
        /// </summary>
        public Monotonicity Monotonicity { get; }

        /// <summary>
        /// Gets how many rules the finder built and evaluated, including the two endpoints and the monotonicity samples.
        /// </summary>
        public int ProbeCount { get; }

        /// <summary>
        /// Gets the grid resolution the boundary was located at; the two boundary values are exactly one step apart.
        /// </summary>
        public T Step { get; }

        /// <summary>
        /// Renders the boundary as <c>passes up to 10000.00, breaks from 10000.01 (29 probes)</c> or, for a rule broken
        /// below, <c>breaks up to 17, passes from 18 (9 probes)</c>, formatted with the invariant culture.
        /// </summary>
        /// <returns>The rendered boundary.</returns>
        public override string ToString()
        {
            return RuleBoundaryText.Describe(this);
        }
    }
}
