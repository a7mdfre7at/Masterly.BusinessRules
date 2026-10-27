using System;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// The constants and messages of the boundary finder.
    /// </summary>
    internal static class RuleBoundaryText
    {
        public const string NegativeProbeCount = "The probe count cannot be negative.";
        public const string UndefinedMonotonicity = "The monotonicity is not a defined value.";
        public const string NotFinite = "The value must be finite.";
        public const string StepNotPositive = "The step must be greater than zero.";
        public const string StepTooSmall = "The step is too small to move from the lower bound; from + step must be greater than from.";
        public const string FromNotBelowTo = "The lower bound must be less than the upper bound.";
        public const string SinglePoint = "The range holds a single grid point; the step must be at most to - from.";
        public const string TooManyPoints = "The range holds too many grid points for the step; use a coarser step or a narrower range.";
        public const string SearchNotFinished = "The search has not finished; keep probing until TryNextProbe returns false.";
        public const string NoPendingProbe = "No probe is pending; call TryNextProbe first.";
        public const string Broken = "broken";
        public const string Passing = "passing";

        public static string Format<T>(T value)
        {
            return FormattableString.Invariant($"{value}");
        }

        public static string NullRule<T>(T value)
        {
            return $"The rule factory returned null for the value {Format(value)}.";
        }

        public static string NoBoundary<T>(T from, T to, bool isBroken)
        {
            string outcome = isBroken ? Broken : Passing;
            return $"No boundary found: the rule is {outcome} at both {Format(from)} and {Format(to)}.";
        }

        public static string NonMonotone<T>(Monotonicity monotonicity, T sample, bool sampleIsBroken, T neighbour)
        {
            string sampleOutcome = sampleIsBroken ? Broken : Passing;
            string neighbourOutcome = sampleIsBroken ? Passing : Broken;
            return $"The rule is not monotone ({monotonicity}): it is {sampleOutcome} at {Format(sample)} but {neighbourOutcome} at {Format(neighbour)}.";
        }

        public static string Describe<T>(RuleBoundary<T> boundary) where T : struct
        {
            if (boundary.Monotonicity == Monotonicity.BrokenAbove)
            {
                return $"passes up to {Format(boundary.LastPassing)}, breaks from {Format(boundary.FirstBroken)} ({boundary.ProbeCount} probes)";
            }

            return $"breaks up to {Format(boundary.FirstBroken)}, passes from {Format(boundary.LastPassing)} ({boundary.ProbeCount} probes)";
        }
    }
}
