namespace Masterly.BusinessRules
{
    /// <summary>
    /// An asynchronous rule that wraps exactly one other asynchronous rule and forwards its metadata.
    /// Implemented by <see cref="CachedAsyncBusinessRule"/>, <see cref="ConditionalAsyncBusinessRule"/>,
    /// <see cref="SeverityOverrideAsyncBusinessRule"/> and <see cref="FeatureGatedAsyncBusinessRule"/>.
    /// The adapters expose their wrapped rule through their own <c>InnerRule</c> property because the wrapped rule
    /// lives on the other (sync/async) hierarchy.
    /// </summary>
    public interface IDecoratedAsyncBusinessRule : IAsyncBusinessRule
    {
        /// <summary>
        /// Gets the wrapped async rule.
        /// </summary>
        IAsyncBusinessRule InnerRule { get; }
    }
}
