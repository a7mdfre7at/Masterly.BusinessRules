using System;
using System.Numerics;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// The evenly spaced probe values <c>from, from + step, from + 2·step, …</c> up to <c>to</c>, addressed by index so
    /// the search never accumulates rounding error. Validates the range and the mandatory step.
    /// </summary>
    /// <typeparam name="T">The numeric input type.</typeparam>
    internal sealed class BoundaryGrid<T> where T : struct, INumber<T>
    {
        /// <summary>
        /// The most grid points a search accepts; the quotient <c>(to - from) / step</c> must not exceed it.
        /// </summary>
        public const long MaxPoints = long.MaxValue / 4;

        public BoundaryGrid(T from, T to, T step)
        {
            if (!T.IsFinite(from))
            {
                throw new ArgumentException(RuleBoundaryText.NotFinite, nameof(from));
            }

            if (!T.IsFinite(to))
            {
                throw new ArgumentException(RuleBoundaryText.NotFinite, nameof(to));
            }

            if (!T.IsFinite(step))
            {
                throw new ArgumentException(RuleBoundaryText.NotFinite, nameof(step));
            }

            if (step <= T.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(step), step, RuleBoundaryText.StepNotPositive);
            }

            if (from >= to)
            {
                throw new ArgumentException(RuleBoundaryText.FromNotBelowTo, nameof(from));
            }

            if (from + step <= from)
            {
                throw new ArgumentOutOfRangeException(nameof(step), step, RuleBoundaryText.StepTooSmall);
            }

            T quotient = (to - from) / step;
            if (quotient < T.One)
            {
                throw new ArgumentException(RuleBoundaryText.SinglePoint, nameof(step));
            }

            if (quotient > T.CreateSaturating(MaxPoints))
            {
                throw new ArgumentException(RuleBoundaryText.TooManyPoints, nameof(step));
            }

            From = from;
            To = to;
            Step = step;
            LastIndex = IncludeTruncatedEndpoint(long.CreateTruncating(quotient));
        }

        public T From { get; }

        public T To { get; }

        public T Step { get; }

        /// <summary>
        /// The index of the last grid point; the grid has <c>LastIndex + 1</c> points and at least two.
        /// </summary>
        public long LastIndex { get; }

        public T ValueAt(long index)
        {
            return From + Step * T.CreateChecked(index);
        }

        /// <summary>
        /// Floating-point division can land one step short of <paramref name="lastIndex"/>'s true value; accept the next
        /// point when it still lies inside the range and the addition did not wrap around.
        /// </summary>
        private long IncludeTruncatedEndpoint(long lastIndex)
        {
            T candidate = ValueAt(lastIndex + 1);
            return candidate <= To && candidate > ValueAt(lastIndex) ? lastIndex + 1 : lastIndex;
        }
    }
}
