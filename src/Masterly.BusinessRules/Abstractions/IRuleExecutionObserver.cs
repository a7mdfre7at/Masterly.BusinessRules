using System;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Observer interface for monitoring synchronous rule execution.
    /// Implement this interface to receive callbacks during rule evaluation.
    /// The batch lifecycle hooks (<see cref="OnBatchStarted"/>, <see cref="OnRuleFaulted"/>,
    /// <see cref="OnAfterEvaluate(IBusinessRule, bool, TimeSpan)"/>, <see cref="OnBatchCompleted"/>) have empty default
    /// implementations, so existing observers keep compiling.
    /// </summary>
    /// <remarks>
    /// Per rule the checker calls <see cref="OnBeforeEvaluate"/>, then either
    /// <see cref="OnAfterEvaluate(IBusinessRule, BusinessRuleResult)"/> followed by
    /// <see cref="OnAfterEvaluate(IBusinessRule, bool, TimeSpan)"/> (and <see cref="OnRuleBroken"/> when broken), or
    /// <see cref="OnRuleFaulted"/> when the rule threw (followed by <see cref="OnRuleBroken"/> under
    /// <see cref="FaultPolicy.TreatAsBroken"/>). <see cref="OnBatchCompleted"/> is raised before a batch throws;
    /// it is not raised when a fault propagates under <see cref="FaultPolicy.Throw"/>.
    /// </remarks>
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

        /// <summary>
        /// Called once before the first rule of a batch is evaluated.
        /// </summary>
        /// <param name="ruleCount">The number of rules submitted to the batch.</param>
        void OnBatchStarted(int ruleCount)
        {
        }

        /// <summary>
        /// Called when a rule throws during evaluation, before the <see cref="FaultPolicy"/> is applied.
        /// Cancellation is never reported here.
        /// </summary>
        /// <param name="rule">The rule that threw.</param>
        /// <param name="exception">The exception the rule threw.</param>
        void OnRuleFaulted(IBusinessRule rule, Exception exception)
        {
        }

        /// <summary>
        /// Called after a rule is evaluated, with the time the checker measured for it. Not called for a faulted rule.
        /// </summary>
        /// <param name="rule">The evaluated rule.</param>
        /// <param name="isBroken">True when the rule is broken.</param>
        /// <param name="elapsed">The wall-clock time spent evaluating the rule.</param>
        void OnAfterEvaluate(IBusinessRule rule, bool isBroken, TimeSpan elapsed)
        {
        }

        /// <summary>
        /// Called once after the last rule of a batch is evaluated, before <c>CheckAll</c> throws.
        /// </summary>
        /// <param name="result">The result of the batch, including its ledger.</param>
        void OnBatchCompleted(RuleEvaluationResult result)
        {
        }
    }

    /// <summary>
    /// Observer interface for monitoring asynchronous rule execution.
    /// Implement this interface to receive callbacks during async rule evaluation.
    /// The batch lifecycle hooks (<see cref="OnBatchStartedAsync"/>, <see cref="OnRuleFaultedAsync"/>,
    /// <see cref="OnAfterEvaluateAsync(IAsyncBusinessRule, bool, TimeSpan, CancellationToken)"/>,
    /// <see cref="OnBatchCompletedAsync"/>) have completed-task default implementations, so existing observers keep compiling.
    /// </summary>
    /// <remarks>
    /// The callback order per rule mirrors <see cref="IRuleExecutionObserver"/>. In a parallel batch the callbacks of
    /// different rules interleave; the ledger handed to <see cref="OnBatchCompletedAsync"/> is in submission order.
    /// </remarks>
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

        /// <summary>
        /// Called once before the first rule of a batch is evaluated.
        /// </summary>
        /// <param name="ruleCount">The number of rules submitted to the batch.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OnBatchStartedAsync(int ruleCount, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when an async rule throws during evaluation, before the <see cref="FaultPolicy"/> is applied.
        /// Cancellation is never reported here.
        /// </summary>
        /// <param name="rule">The rule that threw.</param>
        /// <param name="exception">The exception the rule threw.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OnRuleFaultedAsync(IAsyncBusinessRule rule, Exception exception, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called after an async rule is evaluated, with the time the checker measured for it. Not called for a faulted rule.
        /// </summary>
        /// <param name="rule">The evaluated rule.</param>
        /// <param name="isBroken">True when the rule is broken.</param>
        /// <param name="elapsed">The wall-clock time spent evaluating the rule.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OnAfterEvaluateAsync(IAsyncBusinessRule rule, bool isBroken, TimeSpan elapsed, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called once after the last rule of a batch is evaluated, before <c>CheckAllAsync</c> throws.
        /// </summary>
        /// <param name="result">The result of the batch, including its ledger.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OnBatchCompletedAsync(AsyncRuleEvaluationResult result, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
