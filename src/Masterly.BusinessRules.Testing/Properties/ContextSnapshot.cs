using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// The keys and values of a <see cref="BusinessRuleContext"/> at one moment, taken through
    /// <see cref="BusinessRuleContext.Keys"/>, so two snapshots can tell whether an evaluation wrote to the context.
    /// </summary>
    internal sealed class ContextSnapshot
    {
        private readonly Dictionary<string, object?> _values;

        private ContextSnapshot(Dictionary<string, object?> values)
        {
            _values = values;
        }

        public int Count => _values.Count;

        public static ContextSnapshot Of(BusinessRuleContext context)
        {
            Dictionary<string, object?> values = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (string key in new List<string>(context.Keys))
            {
                values[key] = context.Get<object?>(key);
            }

            return new ContextSnapshot(values);
        }

        public Difference DiffTo(ContextSnapshot after)
        {
            List<string> added = new List<string>();
            List<string> removed = new List<string>();
            List<string> changed = new List<string>();

            foreach (KeyValuePair<string, object?> pair in after._values)
            {
                if (!_values.TryGetValue(pair.Key, out object? previous))
                {
                    added.Add(pair.Key);
                }
                else if (!Equals(previous, pair.Value))
                {
                    changed.Add(pair.Key);
                }
            }

            foreach (string key in _values.Keys)
            {
                if (!after._values.ContainsKey(key))
                {
                    removed.Add(key);
                }
            }

            added.Sort(StringComparer.Ordinal);
            removed.Sort(StringComparer.Ordinal);
            changed.Sort(StringComparer.Ordinal);
            return new Difference(added, removed, changed);
        }

        /// <summary>
        /// The keys that differ between two snapshots.
        /// </summary>
        public sealed class Difference
        {
            public Difference(IReadOnlyList<string> added, IReadOnlyList<string> removed, IReadOnlyList<string> changed)
            {
                Added = added;
                Removed = removed;
                Changed = changed;
            }

            public IReadOnlyList<string> Added { get; }

            public IReadOnlyList<string> Removed { get; }

            public IReadOnlyList<string> Changed { get; }

            public bool IsEmpty => Added.Count == 0 && Removed.Count == 0 && Changed.Count == 0;
        }
    }
}
