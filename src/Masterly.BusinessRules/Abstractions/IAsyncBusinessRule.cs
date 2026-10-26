using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Defines an asynchronous business rule that can be evaluated to determine if it is broken.
    /// The metadata members (<c>Code</c>, <c>Message</c>, <c>Severity</c>, <c>Name</c>, <c>Description</c>,
    /// <c>Category</c>, <c>Tags</c>) come from <see cref="IBusinessRuleMetadata"/>.
    /// </summary>
    public interface IAsyncBusinessRule : IBusinessRuleMetadata
    {
        /// <summary>
        /// Asynchronously evaluates whether the business rule is broken.
        /// </summary>
        /// <param name="context">The context containing runtime data for rule evaluation.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. True if the rule is broken; otherwise, false.</returns>
        Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously evaluates the rule and returns a result if broken.
        /// </summary>
        /// <param name="context">The context containing runtime data for rule evaluation.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task containing the <see cref="BusinessRuleResult"/> if broken; otherwise, null.</returns>
        Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously checks the rule and throws <see cref="BusinessRuleValidationException"/> if broken.
        /// </summary>
        /// <param name="context">The context containing runtime data for rule evaluation.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when the rule is broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default);
    }
}
