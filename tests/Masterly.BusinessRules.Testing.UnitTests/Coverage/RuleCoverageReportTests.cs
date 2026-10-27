namespace Masterly.BusinessRules.Testing.UnitTests;

public class RuleCoverageReportTests
{
    private const string AdultCode = "CUSTOMER.ADULT";
    private const string InactiveCode = "CUSTOMER.INACTIVE";
    private const string NeverCode = "CUSTOMER.NEVER";
    private const string Covered = "✔";
    private const string Uncovered = "✘";

    [Fact]
    public void Entry_DerivesCoverageFromCounts()
    {
        RuleCoverageEntry full = new RuleCoverageEntry(AdultCode, "Adult", 2, 3);
        RuleCoverageEntry brokenOnly = new RuleCoverageEntry(InactiveCode, "Inactive", 1, 0);
        RuleCoverageEntry never = new RuleCoverageEntry(NeverCode, "Never", 0, 0);

        Assert.True(full.IsFullyCovered);
        Assert.Equal(RuleCoverageEntry.OutcomesPerRule, full.CoveredOutcomes);
        Assert.True(brokenOnly.SeenBroken);
        Assert.False(brokenOnly.SeenPassed);
        Assert.Equal(1, brokenOnly.CoveredOutcomes);
        Assert.False(never.WasEvaluated);
        Assert.Equal(0, never.CoveredOutcomes);
        Assert.Equal("Adult", full.Name);
    }

    [Fact]
    public void Entry_Constructor_GuardsArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new RuleCoverageEntry(null!, "n", 0, 0));
        Assert.Throws<ArgumentNullException>(() => new RuleCoverageEntry(AdultCode, null!, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuleCoverageEntry(AdultCode, "n", -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuleCoverageEntry(AdultCode, "n", 0, -1));
    }

    [Fact]
    public void Report_Constructor_GuardsArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new RuleCoverageReport(null!));
        Assert.Throws<ArgumentException>(() => new RuleCoverageReport(new RuleCoverageEntry[] { null! }));
        Assert.Throws<ArgumentException>(() => new RuleCoverageReport(new[] { Entry(AdultCode, 1, 1), Entry(AdultCode, 0, 1) }));
    }

    [Fact]
    public void Report_SortsEntriesOrdinallyByCode()
    {
        RuleCoverageReport report = new RuleCoverageReport(new[] { Entry("b", 1, 1), Entry("B", 1, 1), Entry("a", 1, 1) });

        Assert.Equal(new[] { "B", "a", "b" }, report.Entries.Select(entry => entry.Code));
    }

    [Fact]
    public void Report_ComputesOutcomeCoverageAndLists()
    {
        RuleCoverageReport report = new RuleCoverageReport(new[]
        {
            Entry(AdultCode, 1, 1),
            Entry(InactiveCode, 1, 0),
            Entry(NeverCode, 0, 0),
            Entry("CUSTOMER.PASSONLY", 0, 4),
        });

        Assert.Equal(4, report.RuleCount);
        Assert.Equal(8, report.TotalOutcomes);
        Assert.Equal(4, report.CoveredOutcomes);
        Assert.Equal(0.5, report.OutcomeCoverage);
        Assert.Equal(1, report.FullyCoveredCount);
        Assert.False(report.IsComplete);
        Assert.Equal(new[] { "CUSTOMER.PASSONLY" }, report.NeverBroken);
        Assert.Equal(new[] { InactiveCode }, report.NeverPassed);
        Assert.Equal(new[] { NeverCode }, report.NeverEvaluated);
    }

    [Fact]
    public void Report_WhenEveryRuleCovered_IsComplete()
    {
        RuleCoverageReport report = new RuleCoverageReport(new[] { Entry(AdultCode, 1, 1), Entry(InactiveCode, 2, 2) });

        Assert.True(report.IsComplete);
        Assert.Equal(1.0, report.OutcomeCoverage);
        Assert.Empty(report.NeverBroken);
        Assert.Empty(report.NeverPassed);
        Assert.Empty(report.NeverEvaluated);
    }

    [Fact]
    public void Report_WhenEmpty_HasZeroCoverage()
    {
        RuleCoverageReport report = new RuleCoverageReport(Array.Empty<RuleCoverageEntry>());

        Assert.Equal(0, report.RuleCount);
        Assert.Equal(0.0, report.OutcomeCoverage);
        Assert.False(report.IsComplete);
        Assert.Equal("No rules were wrapped with coverage.", report.ToText());
    }

    [Fact]
    public void Find_ReturnsEntryOrNull()
    {
        RuleCoverageReport report = new RuleCoverageReport(new[] { Entry(AdultCode, 1, 1) });

        Assert.NotNull(report.Find(AdultCode));
        Assert.Null(report.Find(InactiveCode));
        Assert.Null(report.Find(AdultCode.ToLowerInvariant()));
        Assert.Throws<ArgumentNullException>(() => report.Find(null!));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void AssertMinimum_OutOfRange_ThrowsArgumentOutOfRangeException(double minimum)
    {
        RuleCoverageReport report = new RuleCoverageReport(new[] { Entry(AdultCode, 1, 1) });

        Assert.Throws<ArgumentOutOfRangeException>(() => report.AssertMinimum(minimum));
    }

    [Fact]
    public void AssertMinimum_WhenBelow_ThrowsRuleAssertionExceptionNamingUncoveredRules()
    {
        RuleCoverageReport report = new RuleCoverageReport(new[] { Entry(AdultCode, 1, 1), Entry(InactiveCode, 1, 0), Entry(NeverCode, 0, 0) });

        RuleAssertionException exception = Assert.Throws<RuleAssertionException>(() => report.AssertMinimum(1.0));

        Assert.Contains("50% is below the required 100%", exception.Message);
        Assert.Contains($"Never passed: {InactiveCode}.", exception.Message);
        Assert.Contains($"Never evaluated: {NeverCode}.", exception.Message);
        Assert.DoesNotContain("Never broken", exception.Message);
        Assert.Contains(report.ToText(), exception.Message);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    public void AssertMinimum_WhenAtOrAbove_DoesNotThrow(double minimum)
    {
        RuleCoverageReport report = new RuleCoverageReport(new[] { Entry(AdultCode, 1, 1), Entry(InactiveCode, 1, 0) });

        report.AssertMinimum(minimum);
    }

    [Fact]
    public void AssertMinimum_OnEmptyReport_FailsForAnyPositiveMinimum()
    {
        RuleCoverageReport report = new RuleCoverageReport(Array.Empty<RuleCoverageEntry>());

        report.AssertMinimum(0.0);
        RuleAssertionException exception = Assert.Throws<RuleAssertionException>(() => report.AssertMinimum(0.01));

        Assert.Contains("No rules were wrapped with coverage.", exception.Message);
    }

    [Fact]
    public void ToText_RendersOneAlignedLinePerRuleWithAnnotations()
    {
        RuleCoverageReport report = new RuleCoverageReport(new[] { Entry(AdultCode, 1, 1), Entry(InactiveCode, 1, 0), Entry(NeverCode, 0, 0), Entry("CUSTOMER.PASSONLY", 0, 1) });

        string[] lines = report.ToText().Split(Environment.NewLine);

        Assert.Equal(5, lines.Length);
        Assert.Equal($"{AdultCode}     broken {Covered}  passed {Covered}", lines[0]);
        Assert.Equal($"{InactiveCode}  broken {Covered}  passed {Uncovered}   <- never exercised on the happy path", lines[1]);
        Assert.Equal($"{NeverCode}     broken {Uncovered}  passed {Uncovered}   <- never evaluated", lines[2]);
        Assert.Equal($"CUSTOMER.PASSONLY  broken {Uncovered}  passed {Covered}   <- never exercised on the broken path", lines[3]);
        Assert.Equal("outcome coverage 50% (4 of 8 outcomes, 1 of 4 rules fully covered)", lines[4]);
    }

    [Fact]
    public void ToText_FormatsFractionalPercentWithInvariantCulture()
    {
        RuleCoverageReport report = new RuleCoverageReport(new[] { Entry(AdultCode, 1, 1), Entry(InactiveCode, 1, 0), Entry(NeverCode, 1, 0) });

        Assert.EndsWith("outcome coverage 66.7% (4 of 6 outcomes, 1 of 3 rules fully covered)", report.ToText());
    }

    [Fact]
    public void ToString_EqualsToText()
    {
        RuleCoverageReport report = new RuleCoverageReport(new[] { Entry(AdultCode, 1, 1) });

        Assert.Equal(report.ToText(), report.ToString());
        Assert.Equal($"{AdultCode}  broken {Covered}  passed {Covered}", report.Entries[0].ToString());
    }

    private static RuleCoverageEntry Entry(string code, int brokenCount, int passedCount)
    {
        return new RuleCoverageEntry(code, code, brokenCount, passedCount);
    }
}
