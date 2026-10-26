using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Context for passing runtime data to business rules. Values are stored under string keys; the
    /// <see cref="ContextKey{T}"/> overloads bind the key name and the value type into one symbol so reads need no cast
    /// and no string literal. A failed <see cref="Get{T}(string)"/> throws <see cref="ContextKeyNotFoundException"/> (listing
    /// the available keys) or <see cref="ContextValueTypeMismatchException"/> (naming the stored and requested types).
    /// The context is not thread-safe.
    /// </summary>
    public class BusinessRuleContext
    {
        private readonly Dictionary<string, object> _items = new Dictionary<string, object>();

        /// <summary>
        /// Gets a value from the context by key.
        /// </summary>
        /// <typeparam name="T">The type to return the value as.</typeparam>
        /// <param name="key">The key the value is stored under.</param>
        /// <returns>The stored value. A stored null is returned as null when <typeparamref name="T"/> allows null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
        /// <exception cref="ContextKeyNotFoundException">Thrown when the key does not exist (a <see cref="KeyNotFoundException"/>).</exception>
        /// <exception cref="ContextValueTypeMismatchException">Thrown when the stored value is not a <typeparamref name="T"/> (an <see cref="InvalidCastException"/>).</exception>
        public T Get<T>(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (!_items.TryGetValue(key, out object? stored))
            {
                throw new ContextKeyNotFoundException(key, _items.Keys);
            }

            if (stored is T typed)
            {
                return typed;
            }

            if (stored == null && default(T) == null)
            {
                // T is a reference or nullable type and the caller stored null: null is the faithful answer.
                return default!;
            }

            throw new ContextValueTypeMismatchException(key, stored?.GetType(), typeof(T));
        }

        /// <summary>
        /// Gets a value from the context by typed key.
        /// </summary>
        /// <typeparam name="T">The type of the value, inferred from the key.</typeparam>
        /// <param name="key">The typed key.</param>
        /// <returns>The stored value.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is the default instance (no name).</exception>
        /// <exception cref="ContextKeyNotFoundException">Thrown when the key does not exist.</exception>
        /// <exception cref="ContextValueTypeMismatchException">Thrown when the stored value is not a <typeparamref name="T"/>.</exception>
        public T Get<T>(ContextKey<T> key) => Get<T>(key.Name);

        /// <summary>
        /// Tries to get a value from the context by key. Returns <c>false</c> when the key is missing or the stored
        /// value is not a <typeparamref name="T"/>; never throws for a missing key or a type mismatch.
        /// </summary>
        /// <typeparam name="T">The type to return the value as.</typeparam>
        /// <param name="key">The key the value is stored under.</param>
        /// <param name="value">The stored value, or <c>default</c> when the method returns <c>false</c>.</param>
        /// <returns><c>true</c> when a value of type <typeparamref name="T"/> is stored under the key.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
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
        /// Tries to get a value from the context by typed key.
        /// </summary>
        /// <typeparam name="T">The type of the value, inferred from the key.</typeparam>
        /// <param name="key">The typed key.</param>
        /// <param name="value">The stored value, or <c>default</c> when the method returns <c>false</c>.</param>
        /// <returns><c>true</c> when a value of type <typeparamref name="T"/> is stored under the key.</returns>
        public bool TryGet<T>(ContextKey<T> key, out T value) => TryGet(key.Name, out value);

        /// <summary>
        /// Sets a value in the context, replacing any existing value under the same key.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="key">The key to store the value under.</param>
        /// <param name="value">The value to store.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
        public void Set<T>(string key, T value) => _items[key] = value!;

        /// <summary>
        /// Sets a value in the context by typed key, replacing any existing value under the same key.
        /// </summary>
        /// <typeparam name="T">The type of the value, inferred from the key.</typeparam>
        /// <param name="key">The typed key.</param>
        /// <param name="value">The value to store.</param>
        public void Set<T>(ContextKey<T> key, T value) => Set(key.Name, value);

        /// <summary>
        /// Checks if a key exists in the context.
        /// </summary>
        /// <param name="key">The key to look for.</param>
        /// <returns><c>true</c> when the key is present.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
        public bool ContainsKey(string key) => _items.ContainsKey(key);

        /// <summary>
        /// Checks if a typed key exists in the context. Only the name is checked, not the type of the stored value.
        /// </summary>
        /// <typeparam name="T">The type of the value, inferred from the key.</typeparam>
        /// <param name="key">The typed key.</param>
        /// <returns><c>true</c> when the key is present.</returns>
        public bool ContainsKey<T>(ContextKey<T> key) => ContainsKey(key.Name);

        /// <summary>
        /// Removes a key from the context.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <returns><c>true</c> when the key was present and has been removed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
        public bool Remove(string key) => _items.Remove(key);

        /// <summary>
        /// Removes a typed key from the context.
        /// </summary>
        /// <typeparam name="T">The type of the value, inferred from the key.</typeparam>
        /// <param name="key">The typed key.</param>
        /// <returns><c>true</c> when the key was present and has been removed.</returns>
        public bool Remove<T>(ContextKey<T> key) => Remove(key.Name);

        /// <summary>
        /// Clears all items from the context.
        /// </summary>
        public void Clear() => _items.Clear();

        /// <summary>
        /// Gets the number of items in the context.
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// Gets the keys currently stored in the context. The collection is a read-only live view: it reflects later
        /// <see cref="Set{T}(string, T)"/>, <see cref="Remove(string)"/> and <see cref="Clear"/> calls.
        /// </summary>
        public IReadOnlyCollection<string> Keys => _items.Keys;

        /// <summary>
        /// Gets or sets the provider used to format the <c>Detail</c> sentence of results produced for this context
        /// (see <see cref="RuleResultDetails"/>). Null, the default, means the invariant culture.
        /// </summary>
        public IFormatProvider? FormatProvider { get; set; }
    }
}
