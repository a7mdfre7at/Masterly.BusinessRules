namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// Extension methods wrapping a rule in a coverage decorator that reports its outcomes to a <see cref="RuleCoverageSession"/>.
    /// </summary>
    public static class RuleCoverageExtensions
    {
        /// <summary>
        /// Wraps the rule in a <see cref="CoverageBusinessRule"/> recording every verdict into <paramref name="session"/>.
        /// All metadata and the verdict are forwarded unchanged.
        /// </summary>
        /// <param name="rule">The rule to observe.</param>
        /// <param name="session">The session that receives the outcomes.</param>
        /// <returns>The decorated rule.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="rule"/> or <paramref name="session"/> is null.</exception>
        public static IBusinessRule WithCoverage(this IBusinessRule rule, RuleCoverageSession session)
        {
            return new CoverageBusinessRule(rule, session);
        }

        /// <summary>
        /// Wraps the async rule in a <see cref="CoverageAsyncBusinessRule"/> recording every verdict into <paramref name="session"/>.
        /// All metadata and the verdict are forwarded unchanged.
        /// </summary>
        /// <param name="rule">The async rule to observe.</param>
        /// <param name="session">The session that receives the outcomes.</param>
        /// <returns>The decorated rule.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="rule"/> or <paramref name="session"/> is null.</exception>
        public static IAsyncBusinessRule WithCoverage(this IAsyncBusinessRule rule, RuleCoverageSession session)
        {
            return new CoverageAsyncBusinessRule(rule, session);
        }
    }
}
