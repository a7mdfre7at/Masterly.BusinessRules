using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Defines an asynchronous business rule evaluated against a subject supplied at evaluation time rather than
    /// captured by the constructor. A subject-typed rule is stateless, so one instance can be registered as a
    /// singleton and reused for many subjects. It does <b>not</b> derive from <see cref="IAsyncBusinessRule"/>: no
    /// member can be called without a subject, so none ever throws for the lack of one. Bind a subject with
    /// <see cref="TypedAsyncBusinessRuleExtensions.For{TSubject}(IAsyncBusinessRule{TSubject}, TSubject)"/> to obtain
    /// an <see cref="IAsyncBusinessRule"/> that works with every decorator and checker, or pass the subject to the
    /// typed <see cref="AsyncBusinessRuleChecker"/> overloads.
    /// </summary>
    /// <typeparam name="TSubject">The type of the subject the rule validates. Value types are supported.</typeparam>
    public interface IAsyncBusinessRule<in TSubject> : IBusinessRuleMetadata
    {
        /// <summary>
        /// Asynchronously evaluates whether the business rule is broken for the given subject.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="context">The context containing runtime data for rule evaluation.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. True if the rule is broken; otherwise, false.</returns>
        Task<bool> IsBrokenAsync(TSubject subject, BusinessRuleContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously evaluates the rule for the given subject and returns a result if broken.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="context">The context containing runtime data for rule evaluation.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task containing the <see cref="BusinessRuleResult"/> if broken; otherwise, null.</returns>
        Task<BusinessRuleResult?> EvaluateAsync(TSubject subject, BusinessRuleContext context, CancellationToken cancellationToken = default);
    }
}
