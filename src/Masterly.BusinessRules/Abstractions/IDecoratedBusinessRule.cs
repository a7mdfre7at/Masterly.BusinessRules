namespace Masterly.BusinessRules
{
    /// <summary>
    /// A synchronous rule that wraps exactly one other synchronous rule and forwards its metadata.
    /// Implemented by <see cref="CachedBusinessRule"/> and <see cref="ConditionalBusinessRule"/>.
    /// The adapters expose their wrapped rule through their own <c>InnerRule</c> property because the wrapped rule
    /// lives on the other (sync/async) hierarchy.
    /// </summary>
    public interface IDecoratedBusinessRule : IBusinessRule
    {
        /// <summary>
        /// Gets the wrapped rule.
        /// </summary>
        IBusinessRule InnerRule { get; }
    }
}
