using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// Records, per rule <see cref="IBusinessRuleMetadata.Code"/>, how often the rule was seen broken and how often it
    /// was seen passing. Rules are registered when they are wrapped with <c>WithCoverage</c>, so a rule the suite never
    /// evaluated still appears in the report. Recording is thread-safe so parallel async batches can share a session.
    /// Disposing the session ends it: later registrations and outcomes are ignored, <see cref="Report"/> keeps working.
    /// </summary>
    public sealed class RuleCoverageSession : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, RuleOutcomeTally> _tallies = new Dictionary<string, RuleOutcomeTally>(StringComparer.Ordinal);
        private bool _ended;

        internal RuleCoverageSession()
        {
        }

        /// <summary>
        /// Gets a value indicating whether the session has been disposed and no longer records.
        /// </summary>
        public bool IsEnded
        {
            get
            {
                lock (_gate)
                {
                    return _ended;
                }
            }
        }

        /// <summary>
        /// Gets the number of distinct rule codes registered so far.
        /// </summary>
        public int RuleCount
        {
            get
            {
                lock (_gate)
                {
                    return _tallies.Count;
                }
            }
        }

        /// <summary>
        /// Registers a rule so it appears in the report even when it is never evaluated. Registering the same code
        /// twice is harmless. Ignored once the session has ended.
        /// </summary>
        /// <param name="rule">The rule to register.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public void Register(IBusinessRuleMetadata rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            lock (_gate)
            {
                if (_ended)
                {
                    return;
                }

                GetOrAddTally(rule);
            }
        }

        /// <summary>
        /// Records one evaluation outcome for a rule, registering it when needed. Ignored once the session has ended.
        /// </summary>
        /// <param name="rule">The evaluated rule.</param>
        /// <param name="isBroken">True when the evaluation found the rule broken; false when it passed.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public void Record(IBusinessRuleMetadata rule, bool isBroken)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            lock (_gate)
            {
                if (_ended)
                {
                    return;
                }

                RuleOutcomeTally tally = GetOrAddTally(rule);
                if (isBroken)
                {
                    tally.BrokenCount++;
                }
                else
                {
                    tally.PassedCount++;
                }
            }
        }

        /// <summary>
        /// Takes an immutable snapshot of the outcomes recorded so far.
        /// </summary>
        /// <returns>The report, with one entry per registered code sorted ordinally.</returns>
        public RuleCoverageReport Report()
        {
            List<RuleCoverageEntry> entries = new List<RuleCoverageEntry>();

            lock (_gate)
            {
                foreach (RuleOutcomeTally tally in _tallies.Values)
                {
                    entries.Add(new RuleCoverageEntry(tally.Code, tally.Name, tally.BrokenCount, tally.PassedCount));
                }
            }

            return new RuleCoverageReport(entries);
        }

        /// <summary>
        /// Forgets every registered rule and recorded outcome. The session stays open.
        /// </summary>
        public void Reset()
        {
            lock (_gate)
            {
                _tallies.Clear();
            }
        }

        /// <summary>
        /// Ends the session: later registrations and outcomes are ignored. Idempotent.
        /// </summary>
        public void Dispose()
        {
            lock (_gate)
            {
                _ended = true;
            }
        }

        private RuleOutcomeTally GetOrAddTally(IBusinessRuleMetadata rule)
        {
            string code = rule.Code ?? string.Empty;

            if (!_tallies.TryGetValue(code, out RuleOutcomeTally? tally))
            {
                tally = new RuleOutcomeTally(code, rule.Name ?? string.Empty);
                _tallies.Add(code, tally);
            }

            return tally;
        }

        /// <summary>
        /// The mutable per-code counters behind a session; snapshotted into <see cref="RuleCoverageEntry"/> by <see cref="Report"/>.
        /// </summary>
        private sealed class RuleOutcomeTally
        {
            public RuleOutcomeTally(string code, string name)
            {
                Code = code;
                Name = name;
            }

            public string Code { get; }

            public string Name { get; }

            public int BrokenCount { get; set; }

            public int PassedCount { get; set; }
        }
    }
}
