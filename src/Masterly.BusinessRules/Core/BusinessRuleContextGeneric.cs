namespace Masterly.BusinessRules
{
    /// <summary>
    /// Strongly-typed business rule context that wraps a data object.
    /// Provides type-safe access to context data without string keys.
    /// </summary>
    /// <typeparam name="T">The type of the context data.</typeparam>
    public sealed class BusinessRuleContext<T> : BusinessRuleContext where T : class
    {
        /// <summary>
        /// Creates a new typed context with the specified data.
        /// </summary>
        /// <param name="data">The context data object.</param>
        public BusinessRuleContext(T data)
        {
            Data = data;
            Set("__typedData", data);
        }

        /// <summary>
        /// Gets the strongly-typed context data.
        /// </summary>
        public T Data { get; }
    }
}
