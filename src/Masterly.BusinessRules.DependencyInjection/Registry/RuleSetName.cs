using System;

namespace Masterly.BusinessRules.DependencyInjection
{
    /// <summary>
    /// Validation shared by every entry point that takes a rule set name: the <c>AddSet</c> methods of
    /// <see cref="RuleRegistryBuilder"/>, the <c>AddRuleSet</c> methods of <see cref="BusinessRuleOptions"/> and the
    /// <see cref="IRuleRegistry"/> lookups. Names are compared ordinally everywhere.
    /// </summary>
    internal static class RuleSetName
    {
        private const string EmptyNameMessage = "A rule set name must not be empty or white space.";

        /// <summary>
        /// Rejects a null, empty or white-space set name.
        /// </summary>
        /// <param name="name">The set name.</param>
        /// <param name="parameterName">The name of the caller's parameter, for the exception.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or white space.</exception>
        public static void Require(string name, string parameterName)
        {
            if (name == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(EmptyNameMessage, parameterName);
            }
        }
    }
}
