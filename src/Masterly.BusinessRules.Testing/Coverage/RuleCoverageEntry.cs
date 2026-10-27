using System;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// The outcome coverage of one rule code: how often it was seen broken and how often it was seen passing.
    /// A rule is fully covered when both outcomes were observed at least once.
    /// </summary>
    public sealed class RuleCoverageEntry
    {
        /// <summary>
        /// The number of distinct outcomes a rule can produce (broken, passed).
        /// </summary>
        public const int OutcomesPerRule = 2;

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleCoverageEntry"/> class.
        /// </summary>
        /// <param name="code">The rule code.</param>
        /// <param name="name">The rule name recorded at registration.</param>
        /// <param name="brokenCount">How often the rule was seen broken.</param>
        /// <param name="passedCount">How often the rule was seen passing.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/> or <paramref name="name"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a count is negative.</exception>
        public RuleCoverageEntry(string code, string name, int brokenCount, int passedCount)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Name = name ?? throw new ArgumentNullException(nameof(name));

            if (brokenCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(brokenCount), brokenCount, RuleCoverageText.NegativeCount);
            }

            if (passedCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(passedCount), passedCount, RuleCoverageText.NegativeCount);
            }

            BrokenCount = brokenCount;
            PassedCount = passedCount;
        }

        /// <summary>
        /// Gets the rule code the outcomes were recorded under.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the rule name recorded when the code was first registered.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets how often the rule was seen broken.
        /// </summary>
        public int BrokenCount { get; }

        /// <summary>
        /// Gets how often the rule was seen passing.
        /// </summary>
        public int PassedCount { get; }

        /// <summary>
        /// Gets a value indicating whether the rule was seen broken at least once.
        /// </summary>
        public bool SeenBroken => BrokenCount > 0;

        /// <summary>
        /// Gets a value indicating whether the rule was seen passing at least once.
        /// </summary>
        public bool SeenPassed => PassedCount > 0;

        /// <summary>
        /// Gets a value indicating whether the rule was evaluated at all.
        /// </summary>
        public bool WasEvaluated => SeenBroken || SeenPassed;

        /// <summary>
        /// Gets a value indicating whether both outcomes were observed.
        /// </summary>
        public bool IsFullyCovered => SeenBroken && SeenPassed;

        /// <summary>
        /// Gets the number of distinct outcomes observed: 0, 1 or <see cref="OutcomesPerRule"/>.
        /// </summary>
        public int CoveredOutcomes => (SeenBroken ? 1 : 0) + (SeenPassed ? 1 : 0);

        /// <inheritdoc />
        public override string ToString()
        {
            return RuleCoverageText.FormatEntry(this, Code.Length);
        }
    }
}
