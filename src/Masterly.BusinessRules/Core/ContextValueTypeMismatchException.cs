using System;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Thrown by <see cref="BusinessRuleContext.Get{T}(string)"/> when the key exists but its value is not of the requested
    /// type (or is null while a non-nullable value type was requested). The message names the key, the stored type and the
    /// requested type. Derives from <see cref="InvalidCastException"/>, so existing <c>catch</c> blocks keep working.
    /// </summary>
    /// <example>
    /// <code>
    /// // Key 'order' holds Int32, requested Order
    /// </code>
    /// </example>
    public sealed class ContextValueTypeMismatchException : InvalidCastException
    {
        private const string NullTypeName = "null";

        /// <summary>
        /// Initializes a new instance of the <see cref="ContextValueTypeMismatchException"/> class.
        /// </summary>
        /// <param name="key">The key whose value could not be returned as the requested type.</param>
        /// <param name="actualType">The runtime type of the stored value, or null when the stored value is null.</param>
        /// <param name="requestedType">The type the caller asked for.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> or <paramref name="requestedType"/> is null.</exception>
        public ContextValueTypeMismatchException(string key, Type? actualType, Type requestedType)
            : base(FormatMessage(key, actualType, requestedType))
        {
            Key = key;
            ActualType = actualType;
            RequestedType = requestedType;
        }

        /// <summary>
        /// Gets the key whose value could not be returned as the requested type.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the runtime type of the stored value, or null when the stored value is null.
        /// </summary>
        public Type? ActualType { get; }

        /// <summary>
        /// Gets the type the caller asked for.
        /// </summary>
        public Type RequestedType { get; }

        private static string FormatMessage(string key, Type? actualType, Type requestedType)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (requestedType == null)
            {
                throw new ArgumentNullException(nameof(requestedType));
            }

            string actualName = actualType == null ? NullTypeName : actualType.Name;
            return $"Key '{key}' holds {actualName}, requested {requestedType.Name}";
        }
    }
}
