using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The subject-typed entry points of <see cref="BusinessRuleChecker"/>. <see cref="For{TSubject}(TSubject)"/>
    /// returns a <see cref="SubjectRuleChecker{TSubject}"/> whose batch methods bind every
    /// <see cref="IBusinessRule{TSubject}"/> to that subject, mirroring <c>rule.For(subject)</c>.
    /// <c>EvaluateEach</c> runs one batch per subject and reports them by position.
    /// <para>The typed batch methods live behind <c>For(subject)</c> rather than beside the 2.x overloads on this
    /// class: a subject parameter accepts any argument by identity, so a 2.x-shaped call such as
    /// <c>CheckAll(rules, default)</c> would otherwise bind the rule array as the subject and <c>default</c> as a
    /// null rule set.</para>
    /// </summary>
    /// <example>
    /// <code>
    /// IBusinessRule&lt;Order&gt;[] rules = { new OrderMustHaveItemsRule(), new OrderTotalMustBePositiveRule() };
    ///
    /// BusinessRuleChecker.For(order).CheckAll(rules, context);
    /// RuleEvaluationResult result = BusinessRuleChecker.For(order).EvaluateAll(rules);
    /// SubjectEvaluationCollection&lt;Order&gt; each = BusinessRuleChecker.EvaluateEach(orders, rules);
    /// </code>
    /// </example>
    public static partial class BusinessRuleChecker
    {
        /// <summary>
        /// Returns the batch checker for one subject: its methods bind every typed rule to <paramref name="subject"/>
        /// and run the batch through the same evaluation core as the non-generic overloads.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subject">The subject to validate.</param>
        /// <returns>A <see cref="SubjectRuleChecker{TSubject}"/> bound to <paramref name="subject"/>.</returns>
        public static SubjectRuleChecker<TSubject> For<TSubject>(TSubject subject)
        {
            return new SubjectRuleChecker<TSubject>(subject);
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
