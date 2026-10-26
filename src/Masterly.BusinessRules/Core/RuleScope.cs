using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The scope a rule set or a parameter value applies to: an immutable set of key/value pairs such as
    /// <c>tenant=acme</c> or <c>region=EU</c>. Scopes have value equality (two scopes with the same pairs are equal
    /// regardless of the order the pairs were added in), a <see cref="Specificity"/> equal to their pair count, and a
    /// containment test (<see cref="AppliesTo"/>) that a provider or a registry uses to pick the most specific layer for
    /// a request: <c>RuleScope.Tenant("acme")</c> applies to <c>RuleScope.Tenant("acme").With("region", "EU")</c>,
    /// and <see cref="Global"/> applies to everything. Keys and values are compared ordinally; the pairs are kept
    /// sorted by key so <see cref="Pairs"/> and <see cref="ToString"/> are deterministic.
    /// </summary>
    /// <example>
    /// <code>
    /// RuleScope acme = RuleScope.Tenant("acme");
    /// RuleScope acmeEu = acme.With("region", "EU");            // specificity 2
    /// bool applies = acme.AppliesTo(acmeEu);                    // true: every pair of acme is in acmeEu
    /// context.Set(RuleParameters.ScopeKey, acmeEu);             // the scope of this evaluation
    /// </code>
    /// </example>
    public sealed class RuleScope : IEquatable<RuleScope>
    {
        /// <summary>
        /// The key used by <see cref="Tenant"/> and read by <see cref="TenantId"/>.
        /// </summary>
        public const string TenantKey = "tenant";

        private const string PairSeparator = ";";
        private const string KeyValueSeparator = "=";
        private const string GlobalText = "(global)";
        private const int HashSeed = 17;
        private const int HashMultiplier = 31;

        private static readonly RuleScope GlobalScope = new RuleScope(Array.Empty<KeyValuePair<string, string>>());

        private readonly KeyValuePair<string, string>[] _pairs;
        private readonly ReadOnlyCollection<KeyValuePair<string, string>> _readOnlyPairs;

        private RuleScope(KeyValuePair<string, string>[] pairs)
        {
            _pairs = pairs;
            _readOnlyPairs = new ReadOnlyCollection<KeyValuePair<string, string>>(pairs);
        }

        /// <summary>
        /// Gets the empty scope: it has no pairs, a <see cref="Specificity"/> of zero and applies to every other scope.
        /// </summary>
        public static RuleScope Global => GlobalScope;

        /// <summary>
        /// Gets the key/value pairs of the scope, sorted by key (ordinal).
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, string>> Pairs => _readOnlyPairs;

        /// <summary>
        /// Gets the number of pairs. A more specific scope wins over a less specific one when both apply to a request.
        /// </summary>
        public int Specificity => _pairs.Length;

        /// <summary>
        /// Gets whether the scope has no pairs, that is whether it equals <see cref="Global"/>.
        /// </summary>
        public bool IsGlobal => _pairs.Length == 0;

        /// <summary>
        /// Gets the value stored under <see cref="TenantKey"/>, or null when the scope has no tenant.
        /// </summary>
        public string? TenantId => TryGet(TenantKey, out string tenant) ? tenant : null;

        /// <summary>
        /// Creates a scope holding the single pair <c>tenant=<paramref name="tenantId"/></c>.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <returns>The tenant scope.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tenantId"/> is null.</exception>
        public static RuleScope Tenant(string tenantId)
        {
            return GlobalScope.With(TenantKey, tenantId);
        }

        /// <summary>
        /// Returns a new scope with <paramref name="key"/> set to <paramref name="value"/>. An existing value under the
        /// same key is replaced; this scope is not modified.
        /// </summary>
        /// <param name="key">The key. Compared ordinally.</param>
        /// <param name="value">The value. Compared ordinally; may be empty but not null.</param>
        /// <returns>The new scope.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> or <paramref name="value"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is empty or white space.</exception>
        public RuleScope With(string key, string value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A scope key must not be empty or white space.", nameof(key));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>(_pairs.Length + 1);
            bool replaced = false;

            foreach (KeyValuePair<string, string> pair in _pairs)
            {
                if (string.Equals(pair.Key, key, StringComparison.Ordinal))
                {
                    pairs.Add(new KeyValuePair<string, string>(key, value));
                    replaced = true;
                }
                else
                {
                    pairs.Add(pair);
                }
            }

            if (!replaced)
            {
                pairs.Add(new KeyValuePair<string, string>(key, value));
                pairs.Sort(CompareKeys);
            }

            return new RuleScope(pairs.ToArray());
        }

        /// <summary>
        /// Tries to read the value stored under <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The key. Compared ordinally.</param>
        /// <param name="value">The value, or null when the key is absent.</param>
        /// <returns>True when the scope holds the key.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
        public bool TryGet(string key, out string value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            foreach (KeyValuePair<string, string> pair in _pairs)
            {
                if (string.Equals(pair.Key, key, StringComparison.Ordinal))
                {
                    value = pair.Value;
                    return true;
                }
            }

            value = null!; // the contract of the Try pattern: the out value is unspecified when the method returns false
            return false;
        }

        /// <summary>
        /// Returns whether the scope holds <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The key. Compared ordinally.</param>
        /// <returns>True when the key is present.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
        public bool ContainsKey(string key)
        {
            return TryGet(key, out _);
        }

        /// <summary>
        /// Returns whether this scope applies to <paramref name="requested"/>: every pair of this scope is present, with
        /// the same value, in the requested scope. <see cref="Global"/> applies to every scope; a scope always applies to
        /// itself. This is the test a parameter provider or a rule registry uses to select the layers that match a request.
        /// </summary>
        /// <param name="requested">The scope of the request.</param>
        /// <returns>True when every pair of this scope is in <paramref name="requested"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="requested"/> is null.</exception>
        public bool AppliesTo(RuleScope requested)
        {
            if (requested == null)
            {
                throw new ArgumentNullException(nameof(requested));
            }

            if (_pairs.Length > requested._pairs.Length)
            {
                return false;
            }

            foreach (KeyValuePair<string, string> pair in _pairs)
            {
                if (!requested.TryGet(pair.Key, out string value) || !string.Equals(pair.Value, value, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether two scopes hold the same pairs.
        /// </summary>
        /// <param name="other">The scope to compare with.</param>
        /// <returns>True when both scopes hold the same keys with the same values (ordinal comparison).</returns>
        public bool Equals(RuleScope? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (_pairs.Length != other._pairs.Length)
            {
                return false;
            }

            for (int index = 0; index < _pairs.Length; index++)
            {
                if (!string.Equals(_pairs[index].Key, other._pairs[index].Key, StringComparison.Ordinal)
                    || !string.Equals(_pairs[index].Value, other._pairs[index].Value, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is RuleScope other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = HashSeed;
                foreach (KeyValuePair<string, string> pair in _pairs)
                {
                    hash = hash * HashMultiplier + StringComparer.Ordinal.GetHashCode(pair.Key);
                    hash = hash * HashMultiplier + StringComparer.Ordinal.GetHashCode(pair.Value);
                }

                return hash;
            }
        }

        /// <summary>
        /// Returns the pairs as <c>key=value</c> separated by <c>;</c> in key order, or <c>(global)</c> for the empty scope.
        /// </summary>
        /// <returns>The textual form of the scope.</returns>
        public override string ToString()
        {
            if (_pairs.Length == 0)
            {
                return GlobalText;
            }

            StringBuilder text = new StringBuilder();
            foreach (KeyValuePair<string, string> pair in _pairs)
            {
                if (text.Length > 0)
                {
                    text.Append(PairSeparator);
                }

                text.Append(pair.Key).Append(KeyValueSeparator).Append(pair.Value);
            }

            return text.ToString();
        }

        /// <summary>
        /// Compares two scopes for equality; null equals null.
        /// </summary>
        /// <param name="left">The first scope.</param>
        /// <param name="right">The second scope.</param>
        /// <returns>True when both are null or hold the same pairs.</returns>
        public static bool operator ==(RuleScope? left, RuleScope? right)
        {
            if (left is null)
            {
                return right is null;
            }

            return left.Equals(right);
        }

        /// <summary>
        /// Compares two scopes for inequality.
        /// </summary>
        /// <param name="left">The first scope.</param>
        /// <param name="right">The second scope.</param>
        /// <returns>True when exactly one is null or the pairs differ.</returns>
        public static bool operator !=(RuleScope? left, RuleScope? right)
        {
            return !(left == right);
        }

        private static int CompareKeys(KeyValuePair<string, string> left, KeyValuePair<string, string> right)
        {
            return string.CompareOrdinal(left.Key, right.Key);
        }
    }
}
