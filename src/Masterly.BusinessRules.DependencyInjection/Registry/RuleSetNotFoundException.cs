using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Masterly.BusinessRules.DependencyInjection
{
    /// <summary>
    /// Thrown by <see cref="IRuleRegistry"/> when a set name was never registered. Derives from
    /// <see cref="KeyNotFoundException"/>; the message names the missing set and lists the registered ones so a typo
    /// is visible at a glance.
    /// </summary>
    /// <example>
    /// <code>
    /// // Rule registry has no set 'checkuot'. Available sets: checkout, refund
    /// </code>
    /// </example>
    public sealed class RuleSetNotFoundException : KeyNotFoundException
    {
        private const string Separator = ", ";

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleSetNotFoundException"/> class.
        /// </summary>
        /// <param name="setName">The set name that was requested.</param>
        /// <param name="availableSets">The names the registry knows.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> or <paramref name="availableSets"/> is null.</exception>
        public RuleSetNotFoundException(string setName, IEnumerable<string> availableSets)
            : base(BuildMessage(setName, Sort(availableSets)))
        {
            SetName = setName;
            AvailableSets = new ReadOnlyCollection<string>(Sort(availableSets));
        }

        /// <summary>
        /// Gets the set name that was requested.
        /// </summary>
        public string SetName { get; }

        /// <summary>
        /// Gets the registered set names, sorted ordinally.
        /// </summary>
        public IReadOnlyList<string> AvailableSets { get; }

        private static List<string> Sort(IEnumerable<string> availableSets)
        {
            if (availableSets == null)
            {
                throw new ArgumentNullException(nameof(availableSets));
            }

            return availableSets.OrderBy(name => name, StringComparer.Ordinal).ToList();
        }

        private static string BuildMessage(string setName, List<string> availableSets)
        {
            if (setName == null)
            {
                throw new ArgumentNullException(nameof(setName));
            }

            if (availableSets.Count == 0)
            {
                return $"Rule registry has no set '{setName}'. The registry is empty.";
            }

            return $"Rule registry has no set '{setName}'. Available sets: {string.Join(Separator, availableSets)}";
        }
    }
}
