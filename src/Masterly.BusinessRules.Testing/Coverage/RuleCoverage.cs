namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// Entry point for outcome coverage: which rules has a test suite seen both broken <em>and</em> passing?
    /// Begin a <see cref="RuleCoverageSession"/>, wrap the rules under test with
    /// <see cref="RuleCoverageExtensions.WithCoverage(IBusinessRule, RuleCoverageSession)"/> (or the async twin), run the
    /// suite and read <see cref="RuleCoverageSession.Report"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// using RuleCoverageSession coverage = RuleCoverage.Begin();
    /// IBusinessRule adult = new CustomerIsAdultRule(customer).WithCoverage(coverage);
    /// IBusinessRule active = new CustomerIsActiveRule(customer).WithCoverage(coverage);
    /// BusinessRuleChecker.EvaluateAll(adult.And(active));
    ///
    /// RuleCoverageReport report = coverage.Report();
    /// Console.WriteLine(report.ToText());
    /// report.AssertMinimum(outcomeCoverage: 1.0);
    /// </code>
    /// </example>
    public static class RuleCoverage
    {
        /// <summary>
        /// Starts a new, empty coverage session. Dispose it to stop recording; the report stays readable afterwards.
        /// </summary>
        /// <returns>A new session.</returns>
        public static RuleCoverageSession Begin()
        {
            return new RuleCoverageSession();
        }
    }
}
