using System;
using System.Diagnostics;

namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// Allocation-free wall-clock timing over <see cref="Stopwatch.GetTimestamp"/>, mirroring the tick math the core
    /// checkers use so the durations the <c>Traced()</c> decorators record are comparable with the batch ledger.
    /// </summary>
    internal static class DiagnosticsTiming
    {
        private static readonly double TicksPerTimestampTick = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;

        /// <summary>
        /// Takes a high-resolution timestamp.
        /// </summary>
        public static long Start()
        {
            return Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Returns the time elapsed since <paramref name="start"/> was taken with <see cref="Start"/>.
        /// </summary>
        public static TimeSpan ElapsedSince(long start)
        {
            long elapsedTimestampTicks = Stopwatch.GetTimestamp() - start;
            return TimeSpan.FromTicks((long)(elapsedTimestampTicks * TicksPerTimestampTick));
        }
    }
}
