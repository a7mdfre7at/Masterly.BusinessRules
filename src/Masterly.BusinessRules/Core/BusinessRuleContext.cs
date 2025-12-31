using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Context for passing runtime data to business rules.
    /// </summary>
    public class BusinessRuleContext
    {
        private readonly Dictionary<string, object> _items = new Dictionary<string, object>();

        /// <summary>
        /// Gets a value from the context by key.
        /// </summary>
        /// <exception cref="KeyNotFoundException">Thrown if the key does not exist.</exception>
        public T Get<T>(string key) => (T)_items[key];

        /// <summary>
        /// Tries to get a value from the context by key.
        /// </summary>
        public bool TryGet<T>(string key, out T value)
        {
            if (_items.TryGetValue(key, out object? val) && val is T typed)
            {
                value = typed;
                return true;
            }

            value = default!;
            return false;
        }

        /// <summary>
        /// Sets a value in the context.
        /// </summary>
        public void Set<T>(string key, T value) => _items[key] = value!;

        /// <summary>
        /// Checks if a key exists in the context.
        /// </summary>
        public bool ContainsKey(string key) => _items.ContainsKey(key);

        /// <summary>
        /// Removes a key from the context.
        /// </summary>
        public bool Remove(string key) => _items.Remove(key);

        /// <summary>
        /// Clears all items from the context.
        /// </summary>
        public void Clear() => _items.Clear();

        /// <summary>
        /// Gets the number of items in the context.
        /// </summary>
        public int Count => _items.Count;
    }
}