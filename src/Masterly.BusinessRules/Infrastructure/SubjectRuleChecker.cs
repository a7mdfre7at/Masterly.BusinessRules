using System;
using System.Collections.Generic;
using System.Linq;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The batch checker for one subject, returned by <see cref="BusinessRuleChecker.For{TSubject}(TSubject)"/>.
    /// Each method binds every <see cref="IBusinessRule{TSubject}"/> to <see cref="Subject"/> with <c>.For(subject)</c>
    /// and runs the resulting batch through the same evaluation core as the non-generic <see cref="BusinessRuleChecker"/>
    /// overloads, so the ledger, fault policy, severity threshold and observer callbacks behave identically; observers
    /// receive the <see cref="BoundBusinessRule{TSubject}"/> instances.
    /// <para>The options overloads are generic in <c>TOptions</c> (inferred from the argument) so that a bare
    /// <c>null</c> or <c>default</c> literal after the rules always means the context, as on <see cref="BusinessRuleChecker"/>.</para>
    /// </summary>
    /// <typeparam name="TSubject">The subject type.</typeparam>
    /// <example>
    /// <code>
    /// IBusinessRule&lt;Order&gt;[] rules = { new OrderMustHaveItemsRule(), new OrderTotalMustBePositiveRule() };
    ///
    /// BusinessRuleChecker.For(order).CheckAll(rules, context);
    /// RuleEvaluationResult result = BusinessRuleChecker.For(order).EvaluateAll(rules, context, new RuleEvaluationOptions { OnFault = FaultPolicy.Record });
    /// BusinessRuleChecker.For(order).CheckAll(new OrderIsOverdueRule(), new OrderTooLargeRule());
    /// </code>
    /// </example>
    public sealed class SubjectRuleChecker<TSubject>
    {
        internal SubjectRuleChecker(TSubject subject)
        {
            Subject = subject;
        }

        /// <summary>
        /// Gets the subject every batch of this checker is evaluated against.
        /// </summary>
        public TSubject Subject { get; }

        /// <summary>
        /// Checks all typed rules against the subject and throws if any are broken.
        /// </summary>
        /// <param name="rules">The typed rules to check.</param>
        /// <param name="context">Optional context containing data for rule evaluation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a rule in <paramref name="rules"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        public void CheckAll(IEnumerable<IBusinessRule<TSubject>> rules, BusinessRuleContext? context = null)
        {
            BusinessRuleChecker.CheckAllCore(SubjectBinding.Bind(rules, Subject), context, BusinessRuleChecker.LegacyOptions(stopOnFirstFailure: false, observer: null));
        }

        /// <summary>
        /// Checks all typed rules against the subject with the supplied options and throws if any broken rule is at
        /// or above <see cref="RuleEvaluationOptions.ThrowOn"/>.
        /// </summary>
        /// <typeparam name="TOptions">Inferred from <paramref name="options"/>; generic so that a bare <c>null</c> or
        /// <c>default</c> literal binds to the context overload instead.</typeparam>
        /// <param name="rules">The typed rules to check.</param>
        /// <param name="options">The evaluation options: fail-fast, fault policy, severity threshold and observer.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when a broken rule is at or above the threshold.</exception>
        public void CheckAll<TOptions>(IEnumerable<IBusinessRule<TSubject>> rules, TOptions options)
            where TOptions : RuleEvaluationOptions
        {
            BusinessRuleChecker.CheckAllCore(SubjectBinding.Bind(rules, Subject), null, options);
        }

        /// <summary>
        /// Checks all typed rules against the subject using context with the supplied options and throws if any broken
        /// rule is at or above <see cref="RuleEvaluationOptions.ThrowOn"/>.
        /// </summary>
        /// <typeparam name="TOptions">Inferred from <paramref name="options"/>; generic so that a bare <c>null</c> or
        /// <c>default</c> literal cannot select this overload.</typeparam>
        /// <param name="rules">The typed rules to check.</param>
        /// <param name="context">Optional context containing data for rule evaluation.</param>
        /// <param name="options">The evaluation options: fail-fast, fault policy, severity threshold and observer.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when a broken rule is at or above the threshold.</exception>
        public void CheckAll<TOptions>(IEnumerable<IBusinessRule<TSubject>> rules, BusinessRuleContext? context, TOptions options)
            where TOptions : RuleEvaluationOptions
        {
            BusinessRuleChecker.CheckAllCore(SubjectBinding.Bind(rules, Subject), context, options);
        }

        /// <summary>
        /// Checks the listed typed rules against the subject and throws if any are broken. A params form of
        /// <see cref="CheckAll(IEnumerable{IBusinessRule{TSubject}}, BusinessRuleContext)"/> for rules of different
        /// classes: <c>BusinessRuleChecker.For(order).CheckAll(new OrderIsOverdueRule(), new OrderTooLargeRule())</c>.
        /// </summary>
        /// <param name="rule">The first typed rule to check.</param>
        /// <param name="others">The remaining typed rules to check.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="others"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a rule is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        public void CheckAll(IBusinessRule<TSubject> rule, params IBusinessRule<TSubject>[] others)
        {
            CheckAll(SubjectBinding.Concat(rule, others), (BusinessRuleContext?)null);
        }

        /// <summary>
        /// Evaluates all typed rules against the subject and returns detailed results without throwing exceptions.
        /// </summary>
        /// <param name="rules">The typed rules to evaluate.</param>
        /// <param name="context">Optional context containing data for rule evaluation.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a rule in <paramref name="rules"/> is null.</exception>
        public RuleEvaluationResult EvaluateAll(IEnumerable<IBusinessRule<TSubject>> rules, BusinessRuleContext? context = null)
        {
            return BusinessRuleChecker.EvaluateAllCore(SubjectBinding.Bind(rules, Subject), context, new RuleEvaluationOptions(), RuleSeverity.Info);
        }

        /// <summary>
        /// Evaluates all typed rules against the subject with the supplied options. Never throws for broken rules.
        /// </summary>
        /// <typeparam name="TOptions">Inferred from <paramref name="options"/>; generic so that a bare <c>null</c> or
        /// <c>default</c> literal binds to the context overload instead.</typeparam>
        /// <param name="rules">The typed rules to evaluate.</param>
        /// <param name="options">The evaluation options: fail-fast, fault policy and observer.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        public RuleEvaluationResult EvaluateAll<TOptions>(IEnumerable<IBusinessRule<TSubject>> rules, TOptions options)
            where TOptions : RuleEvaluationOptions
        {
            return BusinessRuleChecker.EvaluateAllCore(SubjectBinding.Bind(rules, Subject), null, options, RuleSeverity.Info);
        }

        /// <summary>
        /// Evaluates all typed rules against the subject using context with the supplied options. Never throws for broken rules.
        /// </summary>
        /// <typeparam name="TOptions">Inferred from <paramref name="options"/>; generic so that a bare <c>null</c> or
        /// <c>default</c> literal cannot select this overload.</typeparam>
        /// <param name="rules">The typed rules to evaluate.</param>
        /// <param name="context">Optional context containing data for rule evaluation.</param>
        /// <param name="options">The evaluation options: fail-fast, fault policy and observer.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        public RuleEvaluationResult EvaluateAll<TOptions>(IEnumerable<IBusinessRule<TSubject>> rules, BusinessRuleContext? context, TOptions options)
            where TOptions : RuleEvaluationOptions
        {
            return BusinessRuleChecker.EvaluateAllCore(SubjectBinding.Bind(rules, Subject), context, options, RuleSeverity.Info);
        }

        /// <summary>
        /// Evaluates the listed typed rules against the subject. Never throws for broken rules. A params form of
        /// <see cref="EvaluateAll(IEnumerable{IBusinessRule{TSubject}}, BusinessRuleContext)"/>.
        /// </summary>
        /// <param name="rule">The first typed rule to evaluate.</param>
        /// <param name="others">The remaining typed rules to evaluate.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="others"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a rule is null.</exception>
        public RuleEvaluationResult EvaluateAll(IBusinessRule<TSubject> rule, params IBusinessRule<TSubject>[] others)
        {
            return EvaluateAll(SubjectBinding.Concat(rule, others), (BusinessRuleContext?)null);
        }

        /// <summary>
        /// Checks only the typed rules that match the specified severity levels against the subject.
        /// </summary>
        /// <param name="rules">The typed rules to filter and check.</param>
        /// <param name="severities">The severity levels to include.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any matching rules are broken.</exception>
        public void CheckBySeverity(IEnumerable<IBusinessRule<TSubject>> rules, params RuleSeverity[] severities)
        {
            List<IBusinessRule> bound = SubjectBinding.Bind(rules, Subject);
            bound.RemoveAll(rule => !severities.Contains(rule.Severity));
            BusinessRuleChecker.CheckAllCore(bound, null, BusinessRuleChecker.LegacyOptions(stopOnFirstFailure: false, observer: null));
        }

        /// <summary>
        /// Checks only the typed rules that match the specified category against the subject.
        /// </summary>
        /// <param name="rules">The typed rules to filter and check.</param>
        /// <param name="category">The category to filter by (ordinal comparison).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any matching rules are broken.</exception>
        public void CheckByCategory(IEnumerable<IBusinessRule<TSubject>> rules, string category)
        {
            List<IBusinessRule> bound = SubjectBinding.Bind(rules, Subject);
            bound.RemoveAll(rule => rule.Category != category);
            BusinessRuleChecker.CheckAllCore(bound, null, BusinessRuleChecker.LegacyOptions(stopOnFirstFailure: false, observer: null));
        }

        /// <summary>
        /// Checks only the typed rules that have any of the specified tags against the subject.
        /// </summary>
        /// <param name="rules">The typed rules to filter and check.</param>
        /// <param name="tags">The tags to filter by (case-insensitive).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any matching rules are broken.</exception>
        public void CheckByTags(IEnumerable<IBusinessRule<TSubject>> rules, params string[] tags)
        {
            HashSet<string> tagSet = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
            List<IBusinessRule> bound = SubjectBinding.Bind(rules, Subject);
            bound.RemoveAll(rule => !rule.Tags.Any(tag => tagSet.Contains(tag)));
            BusinessRuleChecker.CheckAllCore(bound, null, BusinessRuleChecker.LegacyOptions(stopOnFirstFailure: false, observer: null));
        }
    }
}
