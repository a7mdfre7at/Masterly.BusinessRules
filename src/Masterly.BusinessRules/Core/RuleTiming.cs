using System;
using System.Diagnostics;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Allocation-free high-resolution timing based on <see cref="Stopwatch.GetTimestamp"/> tick math.
    /// Used by the explanation walk and by the batch checkers' evaluation ledger; no <see cref="Stopwatch"/>
    /// instance is ever allocated.
    /// </summary>
    internal static class RuleTiming
    {
        private static readonly double TimeSpanTicksPerStopwatchTick = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

        /// <summary>
        /// Starts a high-resolution timing measurement.
        /// </summary>
        /// <returns>The current <see cref="Stopwatch"/> timestamp.</returns>
        public static long Start()
        {
            return Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Returns the time elapsed since <paramref name="start"/> was taken with <see cref="Start"/>.
        /// </summary>
        /// <param name="start">The timestamp returned by <see cref="Start"/>.</param>
        /// <returns>The elapsed time.</returns>
        public static TimeSpan ElapsedSince(long start)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - start;
            return TimeSpan.FromTicks((long)(elapsedTicks * TimeSpanTicksPerStopwatchTick));
        }
    }
}
