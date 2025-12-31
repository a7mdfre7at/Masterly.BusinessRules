using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// A composite async business rule that can either contain multiple child rules
    /// or use a delegate function for evaluation.
    /// </summary>
    /// <example>
    /// <code>
    /// // From rules collection
    /// CompositeAsyncBusinessRule composite = new CompositeAsyncBusinessRule(new[]
    /// {
    ///     new UniqueEmailRule(email, repo),
    ///     new ValidDomainRule(email)
    /// });
    ///
    /// // From delegate
    /// CompositeAsyncBusinessRule delegateRule = new CompositeAsyncBusinessRule(
    ///     async (ctx, ct) => await CheckSomethingAsync(ct),
    ///     "Check failed",
    ///     "CHECK.FAILED"
    /// );
    /// </code>
    /// </example>
    public class CompositeAsyncBusinessRule : IAsyncBusinessRule
    {
        private static readonly IReadOnlyList<string> EmptyTags = Array.Empty<string>();
        private readonly Func<BusinessRuleContext, CancellationToken, Task<bool>>? _isBrokenFunc;
        private readonly List<IAsyncBusinessRule>? _rules;

        /// <summary>
        /// Initializes a new instance using a delegate function to determine if the rule is broken.
        /// </summary>
        /// <param name="isBrokenFunc">An async function that returns <c>true</c> when the rule is broken.</param>
        /// <param name="message">The message describing why the rule is broken.</param>
        /// <param name="code">The unique code identifying this rule.</param>
        /// <param name="severity">The severity level of this rule. Defaults to <see cref="RuleSeverity.Error"/>.</param>
        public CompositeAsyncBusinessRule(
            Func<BusinessRuleContext, CancellationToken, Task<bool>> isBrokenFunc,
            string message,
            string code,
            RuleSeverity severity = RuleSeverity.Error)
        {
            _isBrokenFunc = isBrokenFunc;
            Message = message;
            Code = code;
            Severity = severity;
        }

        /// <summary>
        /// Initializes a new instance from a collection of async rules.
        /// The composite is considered broken if any child rule is broken.
        /// </summary>
        /// <param name="rules">The collection of async rules to include in this composite.</param>
        public CompositeAsyncBusinessRule(IEnumerable<IAsyncBusinessRule> rules)
        {
            _rules = rules.ToList();
            Code = "CompositeAsyncRule";
            Message = "One or more async business rules failed.";
            Severity = RuleSeverity.Error;
        }

        /// <inheritdoc />
        public string Code { get; }

        /// <inheritdoc />
        public string Message { get; }

        /// <inheritdoc />
        public RuleSeverity Severity { get; }

        /// <inheritdoc />
        public string Name => "CompositeAsyncBusinessRule";

        /// <inheritdoc />
        public string Description => "A composite rule that evaluates multiple async business rules.";

        /// <inheritdoc />
        public string Category => string.Empty;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => EmptyTags;

        /// <summary>
        /// Gets the underlying rules in this composite.
        /// Returns <c>null</c> if the composite was created from a delegate function.
        /// </summary>
        public IReadOnlyList<IAsyncBusinessRule>? Rules => _rules?.AsReadOnly();

        /// <inheritdoc />
        public async Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            if (_isBrokenFunc != null)
            {
                return await _isBrokenFunc(context, cancellationToken);
            }

            if (_rules != null)
            {
                foreach (IAsyncBusinessRule rule in _rules)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (await rule.IsBrokenAsync(context, cancellationToken))
                        return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public async Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            return await IsBrokenAsync(context, cancellationToken)
                ? new BusinessRuleResult(Code, Message, Severity)
                : null;
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the rule is broken.</exception>
        public async Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            BusinessRuleResult? result = await EvaluateAsync(context, cancellationToken);
            if (result != null)
                throw new BusinessRuleValidationException(new[] { result });
        }

        /// <summary>
        /// Evaluates all child rules and returns results for all broken rules.
        /// When created from a delegate, returns a single result if broken.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A collection of results for all broken child rules.</returns>
        public async Task<IReadOnlyCollection<BusinessRuleResult>> EvaluateAllAsync(
            BusinessRuleContext context,
            CancellationToken cancellationToken = default)
        {
            if (_rules == null)
            {
                BusinessRuleResult? singleResult = await EvaluateAsync(context, cancellationToken);
                return singleResult != null
                    ? new[] { singleResult }
                    : Array.Empty<BusinessRuleResult>();
            }

            List<BusinessRuleResult> brokenRules = new List<BusinessRuleResult>();
            foreach (IAsyncBusinessRule rule in _rules)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BusinessRuleResult? result = await rule.EvaluateAsync(context, cancellationToken);
                if (result != null)
                    brokenRules.Add(result);
            }

            return brokenRules.AsReadOnly();
        }
    }
}