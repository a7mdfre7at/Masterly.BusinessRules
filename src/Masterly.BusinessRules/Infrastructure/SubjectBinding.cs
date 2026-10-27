using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Projects subject-typed rule sets onto the non-generic hierarchies for the typed checker overloads:
    /// every rule becomes a <see cref="BoundBusinessRule{TSubject}"/> / <see cref="BoundAsyncBusinessRule{TSubject}"/>
    /// over the same subject, so the existing evaluation cores run unchanged. A null rule set is rejected with
    /// <see cref="ArgumentNullException"/> and a null rule inside it with the same positional
    /// <see cref="ArgumentException"/> the cores raise for non-generic batches.
    /// </summary>
    internal static class SubjectBinding
    {
        private const string RulesParameterName = CheckerText.RulesParameterName;
        private const string OthersParameterName = "others";

        /// <summary>
        /// Builds the rule set of a params overload: <paramref name="rule"/> followed by <paramref name="others"/>.
        /// Null members are kept so binding reports them by position.
        /// </summary>
        public static List<TRule> Concat<TRule>(TRule rule, TRule[] others)
        {
            if (others == null)
            {
                throw new ArgumentNullException(OthersParameterName);
            }

            List<TRule> rules = new List<TRule>(others.Length + 1) { rule };
            rules.AddRange(others);
            return rules;
        }

        /// <summary>
        /// Binds every sync typed rule to the subject.
        /// </summary>
        public static List<IBusinessRule> Bind<TSubject>(IEnumerable<IBusinessRule<TSubject>> rules, TSubject subject)
        {
            List<IBusinessRule> bound = new List<IBusinessRule>();
            foreach (IBusinessRule<TSubject> rule in Materialize(rules))
            {
                bound.Add(new BoundBusinessRule<TSubject>(rule, subject));
            }

            return bound;
        }

        /// <summary>
        /// Binds every async typed rule to the subject.
        /// </summary>
        public static List<IAsyncBusinessRule> Bind<TSubject>(IEnumerable<IAsyncBusinessRule<TSubject>> rules, TSubject subject)
        {
            List<IAsyncBusinessRule> bound = new List<IAsyncBusinessRule>();
            foreach (IAsyncBusinessRule<TSubject> rule in Materialize(rules))
            {
                bound.Add(new BoundAsyncBusinessRule<TSubject>(rule, subject));
            }

            return bound;
        }

        /// <summary>
        /// Enumerates a sync typed rule set once, rejecting a null set and null members.
        /// </summary>
        public static IReadOnlyList<IBusinessRule<TSubject>> Materialize<TSubject>(IEnumerable<IBusinessRule<TSubject>> rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(RulesParameterName);
            }

            List<IBusinessRule<TSubject>> list = new List<IBusinessRule<TSubject>>();
            foreach (IBusinessRule<TSubject> rule in rules)
            {
                if (rule == null)
                {
                    throw NullRule(list.Count);
                }

                list.Add(rule);
            }

            return list;
        }

        /// <summary>
        /// Enumerates an async typed rule set once, rejecting a null set and null members.
        /// </summary>
        public static IReadOnlyList<IAsyncBusinessRule<TSubject>> Materialize<TSubject>(IEnumerable<IAsyncBusinessRule<TSubject>> rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(RulesParameterName);
            }

            List<IAsyncBusinessRule<TSubject>> list = new List<IAsyncBusinessRule<TSubject>>();
            foreach (IAsyncBusinessRule<TSubject> rule in rules)
            {
                if (rule == null)
                {
                    throw NullRule(list.Count);
                }

                list.Add(rule);
            }

            return list;
        }

        private static ArgumentException NullRule(int position)
        {
            return new ArgumentException(CheckerText.NullRuleAt(position), RulesParameterName);
        }
    }
}
