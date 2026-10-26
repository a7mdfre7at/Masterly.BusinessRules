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
    /// With child rules it is the <see cref="RuleOperator.All"/> node of the composition model and evaluates every
    /// child (no short-circuit) so that every broken child is reported; in delegate mode it has no operands and
    /// behaves as a leaf.
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
    public class CompositeAsyncBusinessRule : IComposedAsyncBusinessRule, IExplainableAsyncBusinessRule
    {
        private const string CompositeCode = "CompositeAsyncRule";
        private const string CompositeMessage = "One or more async business rules failed.";
        private static readonly IReadOnlyList<string> EmptyTags = Array.Empty<string>();
        private static readonly IReadOnlyList<IAsyncBusinessRule> NoOperands = Array.Empty<IAsyncBusinessRule>();
        private static readonly IReadOnlyList<BusinessRuleResult> NoResults = Array.Empty<BusinessRuleResult>();
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
            Code = CompositeCode;
            Message = CompositeMessage;
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
        public RuleOperator Operator => RuleOperator.All;

        /// <inheritdoc />
        /// <remarks>Empty when the composite was created from a delegate function.</remarks>
        public IReadOnlyList<IAsyncBusinessRule> Operands => Rules ?? NoOperands;

        /// <inheritdoc />
        public async Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            return await EvaluateCoreAsync(context, null, cancellationToken) != null;
        }

        /// <inheritdoc />
        /// <remarks>With child rules the returned result carries each broken child in <see cref="BusinessRuleResult.Details"/>.</remarks>
        public Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            return EvaluateCoreAsync(context, null, cancellationToken);
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
            BusinessRuleResult? result = await EvaluateCoreAsync(context, null, cancellationToken);
            if (result == null)
            {
                return NoResults;
            }

            return _rules == null ? new[] { result } : result.Details;
        }

        Task<BusinessRuleResult?> IExplainableAsyncBusinessRule.EvaluateCoreAsync(BusinessRuleContext context, RuleExplanationTrace? trace, CancellationToken cancellationToken)
            => EvaluateCoreAsync(context, trace, cancellationToken);

        private Task<BusinessRuleResult?> EvaluateCoreAsync(BusinessRuleContext context, RuleExplanationTrace? trace, CancellationToken cancellationToken)
        {
            if (_rules == null)
            {
                return AsyncBusinessRuleWalker.EvaluateLeafAsync(this, () => EvaluateDelegateAsync(context, cancellationToken), trace);
            }

            return EvaluateRulesAsync(_rules, context, trace, cancellationToken);
        }

        private async Task<BusinessRuleResult?> EvaluateDelegateAsync(BusinessRuleContext context, CancellationToken cancellationToken)
        {
            if (_isBrokenFunc != null && await _isBrokenFunc(context, cancellationToken))
            {
                return new BusinessRuleResult(Code, Message, Severity);
            }

            return null;
        }

        private async Task<BusinessRuleResult?> EvaluateRulesAsync(
            List<IAsyncBusinessRule> rules,
            BusinessRuleContext context,
            RuleExplanationTrace? trace,
            CancellationToken cancellationToken)
        {
            RuleExplanationTrace? children = trace?.Branch();
            long start = trace != null ? RuleExplanationTrace.StartTiming() : 0L;

            List<BusinessRuleResult>? details = null;
            foreach (IAsyncBusinessRule rule in rules)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BusinessRuleResult? result = await AsyncBusinessRuleWalker.EvaluateNodeAsync(rule, context, children, cancellationToken);
                if (result != null)
                {
                    details = details ?? new List<BusinessRuleResult>();
                    details.Add(result);
                }
            }

            bool isBroken = details != null;

            if (trace != null && children != null)
            {
                trace.Add(AsyncBusinessRuleWalker.Evaluated(this, isBroken, RuleExplanationText.AllOperator, RuleExplanationTrace.ElapsedSince(start), children.Nodes));
            }

            return isBroken ? new BusinessRuleResult(Code, Message, Severity, details ?? NoResults) : null;
        }
    }
}
