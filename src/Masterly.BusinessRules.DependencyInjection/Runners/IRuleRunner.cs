using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules.DependencyInjection
{
    /// <summary>
    /// The injectable entry point for the subject-typed rules of one subject type: every <see cref="IBusinessRule{TSubject}"/>
    /// and <see cref="IAsyncBusinessRule{TSubject}"/> registered in the container (by <c>AddBusinessRules</c> scanning or by
    /// hand) is resolved and run through the typed <see cref="BusinessRuleChecker"/> / <see cref="AsyncBusinessRuleChecker"/>
    /// overloads, so the ledger, fault policy, severity threshold and observers behave exactly as for a direct call.
    /// The async methods evaluate <b>both</b> lists (the sync rules adapted with <c>.For(subject).ToAsync()</c>, then the
    /// async rules); the sync methods evaluate the sync rules only and throw <see cref="System.InvalidOperationException"/>
    /// when async rules are registered, because a synchronous batch cannot run them without blocking.
    /// </summary>
    /// <typeparam name="TSubject">The subject type the rules validate.</typeparam>
    /// <example>
    /// <code>
    /// public sealed class PlaceOrderHandler
    /// {
    ///     private readonly IRuleRunner&lt;Order&gt; _rules;
    ///     public PlaceOrderHandler(IRuleRunner&lt;Order&gt; rules) => _rules = rules;
    ///     public Task Handle(Order order, CancellationToken ct) => _rules.CheckAllAsync(order, ct);
    /// }
    /// </code>
    /// </example>
    public interface IRuleRunner<TSubject>
    {
        /// <summary>
        /// Gets the sync rules registered for <typeparamref name="TSubject"/>, in registration order.
        /// </summary>
        IReadOnlyList<IBusinessRule<TSubject>> Rules { get; }

        /// <summary>
        /// Gets the async rules registered for <typeparamref name="TSubject"/>, in registration order.
        /// </summary>
        IReadOnlyList<IAsyncBusinessRule<TSubject>> AsyncRules { get; }

        /// <summary>
        /// Checks the sync rules against the subject and throws if any are broken.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="context">Optional context containing data for rule evaluation.</param>
        /// <exception cref="System.InvalidOperationException">Thrown when async rules are registered for the subject type.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rule is broken.</exception>
        void CheckAll(TSubject subject, BusinessRuleContext? context = null);

        /// <summary>
        /// Checks the sync rules against the subject with the supplied options.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="context">Optional context containing data for rule evaluation.</param>
        /// <param name="options">The evaluation options: fail-fast, fault policy, severity threshold and observer.</param>
        /// <exception cref="System.InvalidOperationException">Thrown when async rules are registered for the subject type.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when a broken rule is at or above <see cref="RuleEvaluationOptions.ThrowOn"/>.</exception>
        void CheckAll(TSubject subject, BusinessRuleContext? context, RuleEvaluationOptions options);

        /// <summary>
        /// Evaluates the sync rules against the subject without throwing for broken rules.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="context">Optional context containing data for rule evaluation.</param>
        /// <returns>The batch result.</returns>
        /// <exception cref="System.InvalidOperationException">Thrown when async rules are registered for the subject type.</exception>
        RuleEvaluationResult EvaluateAll(TSubject subject, BusinessRuleContext? context = null);

        /// <summary>
        /// Evaluates the sync rules against the subject with the supplied options.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="context">Optional context containing data for rule evaluation.</param>
        /// <param name="options">The evaluation options.</param>
        /// <returns>The batch result with its ledger.</returns>
        /// <exception cref="System.InvalidOperationException">Thrown when async rules are registered for the subject type.</exception>
        RuleEvaluationResult EvaluateAll(TSubject subject, BusinessRuleContext? context, RuleEvaluationOptions options);

        /// <summary>
        /// Checks every rule (sync rules adapted, then async rules) against the subject with a fresh context and throws if any are broken.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rule is broken.</exception>
        Task CheckAllAsync(TSubject subject, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks every rule (sync rules adapted, then async rules) against the subject and throws if any are broken.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="context">The context containing data for rule evaluation, or null for a fresh context.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rule is broken.</exception>
        Task CheckAllAsync(TSubject subject, BusinessRuleContext? context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks every rule (sync rules adapted, then async rules) against the subject with the supplied options.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="context">The context containing data for rule evaluation, or null for a fresh context.</param>
        /// <param name="options">The evaluation options: fail-fast, parallel, fault policy, severity threshold and observer.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="BusinessRuleValidationException">Thrown when a broken rule is at or above <see cref="AsyncRuleEvaluationOptions.ThrowOn"/>.</exception>
        Task CheckAllAsync(TSubject subject, BusinessRuleContext? context, AsyncRuleEvaluationOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Evaluates every rule (sync rules adapted, then async rules) against the subject with a fresh context, without throwing for broken rules.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task producing the batch result.</returns>
        Task<AsyncRuleEvaluationResult> EvaluateAllAsync(TSubject subject, CancellationToken cancellationToken = default);

        /// <summary>
        /// Evaluates every rule (sync rules adapted, then async rules) against the subject without throwing for broken rules.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="context">The context containing data for rule evaluation, or null for a fresh context.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task producing the batch result.</returns>
        Task<AsyncRuleEvaluationResult> EvaluateAllAsync(TSubject subject, BusinessRuleContext? context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Evaluates every rule (sync rules adapted, then async rules) against the subject with the supplied options.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="context">The context containing data for rule evaluation, or null for a fresh context.</param>
        /// <param name="options">The evaluation options.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task producing the batch result with its ledger.</returns>
        Task<AsyncRuleEvaluationResult> EvaluateAllAsync(TSubject subject, BusinessRuleContext? context, AsyncRuleEvaluationOptions options, CancellationToken cancellationToken = default);
    }
}
