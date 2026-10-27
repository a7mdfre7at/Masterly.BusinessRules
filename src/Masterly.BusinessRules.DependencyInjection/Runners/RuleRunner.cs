using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules.DependencyInjection
{
    /// <summary>
    /// The default <see cref="IRuleRunner{TSubject}"/>: a thin projection of the registered typed rules onto the typed
    /// checker overloads. Registered as an open generic by <c>AddBusinessRules</c> with the configured lifetime; it can
    /// also be constructed directly from two rule collections, which is how tests and non-container code use it.
    /// </summary>
    /// <typeparam name="TSubject">The subject type the rules validate.</typeparam>
    /// <example>
    /// <code>
    /// IRuleRunner&lt;Order&gt; runner = new RuleRunner&lt;Order&gt;(new[] { new OrderMustHaveItemsRule() }, Array.Empty&lt;IAsyncBusinessRule&lt;Order&gt;&gt;());
    /// runner.CheckAll(order);
    /// </code>
    /// </example>
    public sealed class RuleRunner<TSubject> : IRuleRunner<TSubject>
    {
        private const string RulesParameterName = "rules";
        private const string AsyncRulesParameterName = "asyncRules";
        private const string CodeSeparator = ", ";

        private readonly ReadOnlyCollection<IBusinessRule<TSubject>> _rules;
        private readonly ReadOnlyCollection<IAsyncBusinessRule<TSubject>> _asyncRules;

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleRunner{TSubject}"/> class over the given rules. Both
        /// collections are enumerated once; the container supplies them as <c>IEnumerable&lt;IBusinessRule&lt;T&gt;&gt;</c>
        /// and <c>IEnumerable&lt;IAsyncBusinessRule&lt;T&gt;&gt;</c>, empty when nothing is registered.
        /// </summary>
        /// <param name="rules">The sync rules for the subject type.</param>
        /// <param name="asyncRules">The async rules for the subject type.</param>
        /// <exception cref="ArgumentNullException">Thrown when either collection is null.</exception>
        /// <exception cref="ArgumentException">Thrown when either collection contains a null rule.</exception>
        public RuleRunner(IEnumerable<IBusinessRule<TSubject>> rules, IEnumerable<IAsyncBusinessRule<TSubject>> asyncRules)
        {
            _rules = Materialize(rules, RulesParameterName);
            _asyncRules = Materialize(asyncRules, AsyncRulesParameterName);
        }

        /// <inheritdoc />
        public IReadOnlyList<IBusinessRule<TSubject>> Rules => _rules;

        /// <inheritdoc />
        public IReadOnlyList<IAsyncBusinessRule<TSubject>> AsyncRules => _asyncRules;

        /// <summary>
        /// Gets whether any async rule is registered, in which case the sync <see cref="CheckAll(TSubject, BusinessRuleContext)"/>
        /// and <see cref="EvaluateAll(TSubject, BusinessRuleContext)"/> throw.
        /// </summary>
        public bool HasAsyncRules => _asyncRules.Count > 0;

        /// <inheritdoc />
        public void CheckAll(TSubject subject, BusinessRuleContext? context = null)
        {
            RequireSynchronousOnly();
            BusinessRuleChecker.CheckAll(subject, _rules, context);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        public void CheckAll(TSubject subject, BusinessRuleContext? context, RuleEvaluationOptions options)
        {
            RequireOptions(options);
            RequireSynchronousOnly();
            BusinessRuleChecker.CheckAll(subject, _rules, context, options);
        }

        /// <inheritdoc />
        public RuleEvaluationResult EvaluateAll(TSubject subject, BusinessRuleContext? context = null)
        {
            RequireSynchronousOnly();
            return BusinessRuleChecker.EvaluateAll(subject, _rules, context);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        public RuleEvaluationResult EvaluateAll(TSubject subject, BusinessRuleContext? context, RuleEvaluationOptions options)
        {
            RequireOptions(options);
            RequireSynchronousOnly();
            return BusinessRuleChecker.EvaluateAll(subject, _rules, context, options);
        }

        /// <inheritdoc />
        public Task CheckAllAsync(TSubject subject, CancellationToken cancellationToken = default)
        {
            return CheckAllAsync(subject, null, new AsyncRuleEvaluationOptions(), cancellationToken);
        }

        /// <inheritdoc />
        public Task CheckAllAsync(TSubject subject, BusinessRuleContext? context, CancellationToken cancellationToken = default)
        {
            return CheckAllAsync(subject, context, new AsyncRuleEvaluationOptions(), cancellationToken);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        public Task CheckAllAsync(TSubject subject, BusinessRuleContext? context, AsyncRuleEvaluationOptions options, CancellationToken cancellationToken = default)
        {
            RequireOptions(options);
            return AsyncBusinessRuleChecker.CheckAllAsync(context ?? new BusinessRuleContext(), Bind(subject), options, cancellationToken);
        }

        /// <inheritdoc />
        public Task<AsyncRuleEvaluationResult> EvaluateAllAsync(TSubject subject, CancellationToken cancellationToken = default)
        {
            return EvaluateAllAsync(subject, null, new AsyncRuleEvaluationOptions(), cancellationToken);
        }

        /// <inheritdoc />
        public Task<AsyncRuleEvaluationResult> EvaluateAllAsync(TSubject subject, BusinessRuleContext? context, CancellationToken cancellationToken = default)
        {
            return EvaluateAllAsync(subject, context, new AsyncRuleEvaluationOptions(), cancellationToken);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        public Task<AsyncRuleEvaluationResult> EvaluateAllAsync(TSubject subject, BusinessRuleContext? context, AsyncRuleEvaluationOptions options, CancellationToken cancellationToken = default)
        {
            RequireOptions(options);
            return AsyncBusinessRuleChecker.EvaluateAllAsync(context ?? new BusinessRuleContext(), Bind(subject), options, cancellationToken);
        }

        private List<IAsyncBusinessRule> Bind(TSubject subject)
        {
            List<IAsyncBusinessRule> bound = new List<IAsyncBusinessRule>(_rules.Count + _asyncRules.Count);
            foreach (IBusinessRule<TSubject> rule in _rules)
            {
                bound.Add(rule.For(subject).ToAsync());
            }

            foreach (IAsyncBusinessRule<TSubject> rule in _asyncRules)
            {
                bound.Add(rule.For(subject));
            }

            return bound;
        }

        private void RequireSynchronousOnly()
        {
            if (_asyncRules.Count == 0)
            {
                return;
            }

            string codes = string.Join(CodeSeparator, _asyncRules.Select(rule => rule.Code));
            throw new InvalidOperationException(
                $"IRuleRunner<{typeof(TSubject).Name}> has {_asyncRules.Count} asynchronous rule(s) ({codes}) that a synchronous batch cannot evaluate without blocking. Call CheckAllAsync/EvaluateAllAsync, or run the Rules collection through BusinessRuleChecker directly.");
        }

        private static ReadOnlyCollection<TRule> Materialize<TRule>(IEnumerable<TRule> rules, string parameterName)
            where TRule : class
        {
            if (rules == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            List<TRule> list = new List<TRule>();
            foreach (TRule rule in rules)
            {
                if (rule == null)
                {
                    throw new ArgumentException($"The rule at position {list.Count} is null.", parameterName);
                }

                list.Add(rule);
            }

            return new ReadOnlyCollection<TRule>(list);
        }

        private static void RequireOptions(object options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
        }
    }
}
