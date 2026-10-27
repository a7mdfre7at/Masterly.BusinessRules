using System;
using System.Collections.Generic;
using System.Linq;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Subject-typed overloads of <see cref="BusinessRuleChecker"/>. Each one binds every
    /// <see cref="IBusinessRule{TSubject}"/> to the subject with <c>.For(subject)</c> and runs the resulting batch
    /// through the same evaluation core as the non-generic overloads, so the ledger, fault policy, severity threshold
    /// and observer callbacks behave identically; observers receive the <see cref="BoundBusinessRule{TSubject}"/> instances.
    /// <see cref="EvaluateEach{TSubject}(IEnumerable{TSubject}, IEnumerable{IBusinessRule{TSubject}}, BusinessRuleContext)"/>
    /// runs one batch per subject and reports them by position.
    /// </summary>
    /// <example>
    /// <code>
    /// IBusinessRule&lt;Order&gt;[] rules = { new OrderMustHaveItemsRule(), new OrderTotalMustBePositiveRule() };
    ///
    /// BusinessRuleChecker.CheckAll(order, rules, context);
    /// RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(order, rules);
    /// SubjectEvaluationCollection&lt;Order&gt; each = BusinessRuleChecker.EvaluateEach(orders, rules);
    /// </code>
    /// </example>
    public static partial class BusinessRuleChecker
    {
        /// <summary>
        /// Checks all typed rules against the subject and throws if any are broken.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed rules to check.</param>
        /// <param name="context">Optional context containing data for rule evaluation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a rule in <paramref name="rules"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        public static void CheckAll<TSubject>(TSubject subject, IEnumerable<IBusinessRule<TSubject>> rules, BusinessRuleContext? context = null)
        {
            CheckAllCore(SubjectBinding.Bind(rules, subject), context, LegacyOptions(stopOnFirstFailure: false, observer: null));
        }

        /// <summary>
        /// Checks all typed rules against the subject with the supplied options and throws if any broken rule is at
        /// or above <see cref="RuleEvaluationOptions.ThrowOn"/>.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed rules to check.</param>
        /// <param name="options">The evaluation options: fail-fast, fault policy, severity threshold and observer.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when a broken rule is at or above the threshold.</exception>
        public static void CheckAll<TSubject>(TSubject subject, IEnumerable<IBusinessRule<TSubject>> rules, RuleEvaluationOptions options)
        {
            CheckAllCore(SubjectBinding.Bind(rules, subject), null, options);
        }

        /// <summary>
        /// Checks all typed rules against the subject using context with the supplied options and throws if any broken
        /// rule is at or above <see cref="RuleEvaluationOptions.ThrowOn"/>.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed rules to check.</param>
        /// <param name="context">Optional context containing data for rule evaluation.</param>
        /// <param name="options">The evaluation options: fail-fast, fault policy, severity threshold and observer.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when a broken rule is at or above the threshold.</exception>
        public static void CheckAll<TSubject>(TSubject subject, IEnumerable<IBusinessRule<TSubject>> rules, BusinessRuleContext? context, RuleEvaluationOptions options)
        {
            CheckAllCore(SubjectBinding.Bind(rules, subject), context, options);
        }

        /// <summary>
        /// Evaluates all typed rules against the subject and returns detailed results without throwing exceptions.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed rules to evaluate.</param>
        /// <param name="context">Optional context containing data for rule evaluation.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a rule in <paramref name="rules"/> is null.</exception>
        public static RuleEvaluationResult EvaluateAll<TSubject>(TSubject subject, IEnumerable<IBusinessRule<TSubject>> rules, BusinessRuleContext? context = null)
        {
            return EvaluateAllCore(SubjectBinding.Bind(rules, subject), context, new RuleEvaluationOptions(), RuleSeverity.Info);
        }

        /// <summary>
        /// Evaluates all typed rules against the subject with the supplied options. Never throws for broken rules.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed rules to evaluate.</param>
        /// <param name="options">The evaluation options: fail-fast, fault policy and observer.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        public static RuleEvaluationResult EvaluateAll<TSubject>(TSubject subject, IEnumerable<IBusinessRule<TSubject>> rules, RuleEvaluationOptions options)
        {
            return EvaluateAllCore(SubjectBinding.Bind(rules, subject), null, options, RuleSeverity.Info);
        }

        /// <summary>
        /// Evaluates all typed rules against the subject using context with the supplied options. Never throws for broken rules.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed rules to evaluate.</param>
        /// <param name="context">Optional context containing data for rule evaluation.</param>
        /// <param name="options">The evaluation options: fail-fast, fault policy and observer.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        public static RuleEvaluationResult EvaluateAll<TSubject>(TSubject subject, IEnumerable<IBusinessRule<TSubject>> rules, BusinessRuleContext? context, RuleEvaluationOptions options)
        {
            return EvaluateAllCore(SubjectBinding.Bind(rules, subject), context, options, RuleSeverity.Info);
        }

        /// <summary>
        /// Checks the listed typed rules against the subject and throws if any are broken. A params form of
        /// <see cref="CheckAll{TSubject}(TSubject, IEnumerable{IBusinessRule{TSubject}}, BusinessRuleContext)"/> for rules of
        /// different classes: <c>BusinessRuleChecker.CheckAll(order, new OrderIsOverdueRule(), new OrderTooLargeRule())</c>.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rule">The first typed rule to check.</param>
        /// <param name="others">The remaining typed rules to check.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="others"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a rule is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        public static void CheckAll<TSubject>(TSubject subject, IBusinessRule<TSubject> rule, params IBusinessRule<TSubject>[] others)
        {
            CheckAll(subject, SubjectBinding.Concat(rule, others), (BusinessRuleContext?)null);
        }

        /// <summary>
        /// Evaluates the listed typed rules against the subject. Never throws for broken rules. A params form of
        /// <see cref="EvaluateAll{TSubject}(TSubject, IEnumerable{IBusinessRule{TSubject}}, BusinessRuleContext)"/>.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rule">The first typed rule to evaluate.</param>
        /// <param name="others">The remaining typed rules to evaluate.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="others"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a rule is null.</exception>
        public static RuleEvaluationResult EvaluateAll<TSubject>(TSubject subject, IBusinessRule<TSubject> rule, params IBusinessRule<TSubject>[] others)
        {
            return EvaluateAll(subject, SubjectBinding.Concat(rule, others), (BusinessRuleContext?)null);
        }

        /// <summary>
        /// Checks only the typed rules that match the specified severity levels against the subject.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed rules to filter and check.</param>
        /// <param name="severities">The severity levels to include.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any matching rules are broken.</exception>
        public static void CheckBySeverity<TSubject>(TSubject subject, IEnumerable<IBusinessRule<TSubject>> rules, params RuleSeverity[] severities)
        {
            List<IBusinessRule> bound = SubjectBinding.Bind(rules, subject);
            bound.RemoveAll(rule => !severities.Contains(rule.Severity));
            CheckAllCore(bound, null, LegacyOptions(stopOnFirstFailure: false, observer: null));
        }

        /// <summary>
        /// Checks only the typed rules that match the specified category against the subject.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed rules to filter and check.</param>
        /// <param name="category">The category to filter by (ordinal comparison).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any matching rules are broken.</exception>
        public static void CheckByCategory<TSubject>(TSubject subject, IEnumerable<IBusinessRule<TSubject>> rules, string category)
        {
            List<IBusinessRule> bound = SubjectBinding.Bind(rules, subject);
            bound.RemoveAll(rule => rule.Category != category);
            CheckAllCore(bound, null, LegacyOptions(stopOnFirstFailure: false, observer: null));
        }

        /// <summary>
        /// Checks only the typed rules that have any of the specified tags against the subject.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed rules to filter and check.</param>
        /// <param name="tags">The tags to filter by (case-insensitive).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any matching rules are broken.</exception>
        public static void CheckByTags<TSubject>(TSubject subject, IEnumerable<IBusinessRule<TSubject>> rules, params string[] tags)
        {
            HashSet<string> tagSet = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
            List<IBusinessRule> bound = SubjectBinding.Bind(rules, subject);
            bound.RemoveAll(rule => !rule.Tags.Any(tag => tagSet.Contains(tag)));
            CheckAllCore(bound, null, LegacyOptions(stopOnFirstFailure: false, observer: null));
        }

        /// <summary>
        /// Evaluates the typed rule set against every subject, one batch per subject, and returns the results by position.
        /// Every subject is evaluated; the same context instance is shared by all batches.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subjects">The subjects to validate, in order.</param>
        /// <param name="rules">The typed rules to evaluate against each subject.</param>
        /// <param name="context">Optional context containing data for rule evaluation.</param>
        /// <returns>One <see cref="SubjectEvaluation{TSubject}"/> per subject with batch-level aggregates.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="subjects"/> or <paramref name="rules"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a rule in <paramref name="rules"/> is null.</exception>
        public static SubjectEvaluationCollection<TSubject> EvaluateEach<TSubject>(IEnumerable<TSubject> subjects, IEnumerable<IBusinessRule<TSubject>> rules, BusinessRuleContext? context = null)
        {
            return EvaluateEachCore(subjects, rules, context, new RuleEvaluationOptions());
        }

        /// <summary>
        /// Evaluates the typed rule set against every subject with the supplied options, one batch per subject.
        /// Fail-fast applies within a subject's batch, never across subjects; <see cref="RuleEvaluationOptions.ThrowOn"/> is ignored.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subjects">The subjects to validate, in order.</param>
        /// <param name="rules">The typed rules to evaluate against each subject.</param>
        /// <param name="options">The evaluation options applied to each subject's batch.</param>
        /// <returns>One <see cref="SubjectEvaluation{TSubject}"/> per subject with batch-level aggregates.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="subjects"/>, <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        public static SubjectEvaluationCollection<TSubject> EvaluateEach<TSubject>(IEnumerable<TSubject> subjects, IEnumerable<IBusinessRule<TSubject>> rules, RuleEvaluationOptions options)
        {
            return EvaluateEachCore(subjects, rules, null, options);
        }

        /// <summary>
        /// Evaluates the typed rule set against every subject using context with the supplied options, one batch per subject.
        /// Fail-fast applies within a subject's batch, never across subjects; <see cref="RuleEvaluationOptions.ThrowOn"/> is ignored.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subjects">The subjects to validate, in order.</param>
        /// <param name="rules">The typed rules to evaluate against each subject.</param>
        /// <param name="context">Optional context shared by every subject's batch.</param>
        /// <param name="options">The evaluation options applied to each subject's batch.</param>
        /// <returns>One <see cref="SubjectEvaluation{TSubject}"/> per subject with batch-level aggregates.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="subjects"/>, <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        public static SubjectEvaluationCollection<TSubject> EvaluateEach<TSubject>(IEnumerable<TSubject> subjects, IEnumerable<IBusinessRule<TSubject>> rules, BusinessRuleContext? context, RuleEvaluationOptions options)
        {
            return EvaluateEachCore(subjects, rules, context, options);
        }

        private static SubjectEvaluationCollection<TSubject> EvaluateEachCore<TSubject>(
            IEnumerable<TSubject> subjects,
            IEnumerable<IBusinessRule<TSubject>> rules,
            BusinessRuleContext? context,
            RuleEvaluationOptions options)
        {
            if (subjects == null)
            {
                throw new ArgumentNullException(nameof(subjects));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            IReadOnlyList<IBusinessRule<TSubject>> ruleSet = SubjectBinding.Materialize(rules);
            List<SubjectEvaluation<TSubject>> evaluations = new List<SubjectEvaluation<TSubject>>();

            foreach (TSubject subject in subjects)
            {
                RuleEvaluationResult result = EvaluateAllCore(SubjectBinding.Bind(ruleSet, subject), context, options, RuleSeverity.Info);
                evaluations.Add(new SubjectEvaluation<TSubject>(evaluations.Count, subject, result));
            }

            return new SubjectEvaluationCollection<TSubject>(evaluations);
        }
    }
}
