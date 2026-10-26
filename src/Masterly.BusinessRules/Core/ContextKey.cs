using System;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// A strongly-typed key for <see cref="BusinessRuleContext"/>. The key carries the name under which the value is
    /// stored and the type of the value as one symbol, so <c>context.Get(OrderKeys.Order)</c> infers the value type
    /// with no cast and no string literal. Declare keys once as <c>static readonly</c> fields and share them between the
    /// code that writes the context and the rules that read it.
    /// </summary>
    /// <typeparam name="T">The type of the value stored under the key.</typeparam>
    /// <example>
    /// <code>
    /// public static class OrderKeys
    /// {
    ///     public static readonly ContextKey&lt;Order&gt; Order = new ContextKey&lt;Order&gt;("order");
    ///     public static readonly ContextKey&lt;decimal&gt; Limit = new ContextKey&lt;decimal&gt;("limit");
    /// }
    ///
    /// context.Set(OrderKeys.Order, order);
    /// Order order = context.Get(OrderKeys.Order);
    /// </code>
    /// </example>
    public readonly struct ContextKey<T> : IEquatable<ContextKey<T>>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContextKey{T}"/> struct.
        /// </summary>
        /// <param name="name">The name under which the value is stored in the context. Compared ordinally.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or white space.</exception>
        public ContextKey(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A context key name must not be empty or white space.", nameof(name));
            }

            Name = name;
        }

        /// <summary>
        /// Gets the name under which the value is stored in the context. Null only for the default instance of the struct.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the type of the value stored under this key.
        /// </summary>
        public Type ValueType => typeof(T);

        /// <summary>
        /// Determines whether two keys have the same name.
        /// </summary>
        /// <param name="other">The key to compare with.</param>
        /// <returns><c>true</c> when both names are equal (ordinal comparison).</returns>
        public bool Equals(ContextKey<T> other) => string.Equals(Name, other.Name, StringComparison.Ordinal);

        /// <summary>
        /// Determines whether the object is a <see cref="ContextKey{T}"/> with the same name.
        /// </summary>
        /// <param name="obj">The object to compare with.</param>
        /// <returns><c>true</c> when <paramref name="obj"/> is a key of the same value type with the same name.</returns>
        public override bool Equals(object? obj) => obj is ContextKey<T> other && Equals(other);

        /// <summary>
        /// Returns a hash code derived from the ordinal hash of the name.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode() => Name == null ? 0 : StringComparer.Ordinal.GetHashCode(Name);

        /// <summary>
        /// Returns the key name.
        /// </summary>
        /// <returns>The name, or an empty string for the default instance.</returns>
        public override string ToString() => Name ?? string.Empty;

        /// <summary>
        /// Determines whether two keys have the same name.
        /// </summary>
        /// <param name="left">The first key.</param>
        /// <param name="right">The second key.</param>
        /// <returns><c>true</c> when both names are equal.</returns>
        public static bool operator ==(ContextKey<T> left, ContextKey<T> right) => left.Equals(right);

        /// <summary>
        /// Determines whether two keys have different names.
        /// </summary>
        /// <param name="left">The first key.</param>
        /// <param name="right">The second key.</param>
        /// <returns><c>true</c> when the names differ.</returns>
        public static bool operator !=(ContextKey<T> left, ContextKey<T> right) => !left.Equals(right);
    }
}
