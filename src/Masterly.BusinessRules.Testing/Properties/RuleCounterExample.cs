using System;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// The smallest input a sampled law failed for, with the fault it raised and the run statistics the failure message
    /// reports.
    /// </summary>
    internal sealed class RuleCounterExample
    {
        public RuleCounterExample(string input, Exception failure, int seed, int shrinks, int evaluated, int skipped)
        {
            Input = input;
            Failure = failure;
            Seed = seed;
            Shrinks = shrinks;
            Evaluated = evaluated;
            Skipped = skipped;
        }

        /// <summary>
        /// Gets the counter-example rendered with the invariant culture.
        /// </summary>
        public string Input { get; }

        /// <summary>
        /// Gets the exception the law raised for <see cref="Input"/>.
        /// </summary>
        public Exception Failure { get; }

        /// <summary>
        /// Gets the seed that reproduces the run.
        /// </summary>
        public int Seed { get; }

        /// <summary>
        /// Gets how many times a smaller counter-example replaced the first one found.
        /// </summary>
        public int Shrinks { get; }

        /// <summary>
        /// Gets how many inputs were evaluated.
        /// </summary>
        public int Evaluated { get; }

        /// <summary>
        /// Gets how many inputs were skipped because they were not smaller than the counter-example already known.
        /// </summary>
        public int Skipped { get; }
    }
}
