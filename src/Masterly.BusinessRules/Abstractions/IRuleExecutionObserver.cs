using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Observer interface for monitoring synchronous rule execution.
    /// Implement this interface to receive callbacks during rule evaluation.
    /// </summary>
    public interface IRuleExecutionObserver
    {
        /// <summary>
        /// Called before a rule is evaluated.
        /// </summary>
        /// <param name="rule">The rule about to be evaluated.</param>
        void OnBeforeEvaluate(IBusinessRule rule);

        /// <summary>
        /// Called after a rule is evaluated.
        /// </summary>
        /// <param name="rule">The evaluated rule.</param>
        /// <param name="result">The result if the rule was broken; null if the rule passed.</param>
        void OnAfterEvaluate(IBusinessRule rule, BusinessRuleResult? result);

        /// <summary>
        /// Called when a rule is determined to be broken.
        /// </summary>
        /// <param name="rule">The broken rule.</param>
        /// <param name="result">The result containing the rule violation details.</param>
        void OnRuleBroken(IBusinessRule rule, BusinessRuleResult result);
    }

    /// <summary>
    /// Observer interface for monitoring asynchronous rule execution.
    /// Implement this interface to receive callbacks during async rule evaluation.
    /// </summary>
    public interface IAsyncRuleExecutionObserver
    {
        /// <summary>
        /// Called before an async rule is evaluated.
        /// </summary>
        /// <param name="rule">The rule about to be evaluated.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OnBeforeEvaluateAsync(IAsyncBusinessRule rule, CancellationToken cancellationToken = default);

        /// <summary>
        /// Called after an async rule is evaluated.
        /// </summary>
        /// <param name="rule">The evaluated rule.</param>
        /// <param name="result">The result if the rule was broken; null if the rule passed.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OnAfterEvaluateAsync(IAsyncBusinessRule rule, BusinessRuleResult? result, CancellationToken cancellationToken = default);

        /// <summary>
        /// Called when an async rule is determined to be broken.
        /// </summary>
        /// <param name="rule">The broken rule.</param>
        /// <param name="result">The result containing the rule violation details.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OnRuleBrokenAsync(IAsyncBusinessRule rule, BusinessRuleResult result, CancellationToken cancellationToken = default);
    }
}
