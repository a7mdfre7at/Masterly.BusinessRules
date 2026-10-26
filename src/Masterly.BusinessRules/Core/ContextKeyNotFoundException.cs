using System;
using System.Collections.Generic;
using System.Linq;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Thrown by <see cref="BusinessRuleContext.Get{T}(string)"/> when the requested key is not present. The message names
    /// the missing key and lists the keys the context does hold (sorted ordinally), so a typo such as <c>"ordr"</c> is
    /// visible at a glance. Derives from <see cref="KeyNotFoundException"/>, so existing <c>catch</c> blocks keep working.
    /// </summary>
    /// <example>
    /// <code>
    /// // Context has no key 'ordr'. Available keys: customer, limit, order
    /// </code>
    /// </example>
    public sealed class ContextKeyNotFoundException : KeyNotFoundException
    {
        private const string EmptyContextSuffix = "The context is empty.";
        private const string AvailableKeysPrefix = "Available keys: ";
        private const string KeySeparator = ", ";

        /// <summary>
        /// Initializes a new instance of the <see cref="ContextKeyNotFoundException"/> class.
        /// </summary>
        /// <param name="key">The key that was requested but not found.</param>
        /// <param name="availableKeys">The keys the context holds at the time of the failed read.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> or <paramref name="availableKeys"/> is null.</exception>
        public ContextKeyNotFoundException(string key, IEnumerable<string> availableKeys)
            : base(FormatMessage(key, availableKeys, out IReadOnlyList<string> snapshot))
        {
            Key = key;
            AvailableKeys = snapshot;
        }

        /// <summary>
        /// Gets the key that was requested but not found.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the keys the context held when the read failed, sorted ordinally.
        /// </summary>
        public IReadOnlyList<string> AvailableKeys { get; }

        private static string FormatMessage(string key, IEnumerable<string> availableKeys, out IReadOnlyList<string> snapshot)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (availableKeys == null)
            {
                throw new ArgumentNullException(nameof(availableKeys));
            }

            List<string> sorted = availableKeys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            snapshot = sorted.AsReadOnly();

            string suffix = sorted.Count == 0
                ? EmptyContextSuffix
                : $"{AvailableKeysPrefix}{string.Join(KeySeparator, sorted)}";

            return $"Context has no key '{key}'. {suffix}";
        }
    }
}
