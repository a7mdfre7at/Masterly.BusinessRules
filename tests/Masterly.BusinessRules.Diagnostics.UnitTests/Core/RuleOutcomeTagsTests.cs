namespace Masterly.BusinessRules.Diagnostics.UnitTests.Core;

public class RuleOutcomeTagsTests
{
    [Theory]
    [InlineData(true, RuleOutcomeTags.Broken)]
    [InlineData(false, RuleOutcomeTags.Passed)]
    public void ForVerdict_Verdict_ReturnsMatchingTag(bool isBroken, string expected)
    {
        string outcome = RuleOutcomeTags.ForVerdict(isBroken);

        Assert.Equal(expected, outcome);
    }

    [Theory]
    [InlineData(true, true, RuleOutcomeTags.Faulted)]
    [InlineData(true, false, RuleOutcomeTags.Faulted)]
    [InlineData(false, true, RuleOutcomeTags.Broken)]
    [InlineData(false, false, RuleOutcomeTags.Passed)]
    public void ForBatch_Counts_FaultOutranksBrokenOutranksPassed(bool hasFaults, bool hasBroken, string expected)
    {
        string outcome = RuleOutcomeTags.ForBatch(hasFaults, hasBroken);

        Assert.Equal(expected, outcome);
    }

    [Fact]
    public void Tags_AllValues_AreLowerCase()
    {
        string[] values = { RuleOutcomeTags.Passed, RuleOutcomeTags.Broken, RuleOutcomeTags.Faulted, RuleOutcomeTags.Shadow, RuleOutcomeTags.Cancelled };

        Assert.All(values, value => Assert.Equal(value.ToLowerInvariant(), value));
        Assert.Equal(values.Length, values.Distinct().Count());
    }
}

public class RuleSeverityTagsTests
{
    [Theory]
    [InlineData(RuleSeverity.Info, RuleSeverityTags.Info)]
    [InlineData(RuleSeverity.Warning, RuleSeverityTags.Warning)]
    [InlineData(RuleSeverity.Error, RuleSeverityTags.Error)]
    public void From_DefinedSeverity_ReturnsLowerCaseName(RuleSeverity severity, string expected)
    {
        string tag = RuleSeverityTags.From(severity);

        Assert.Equal(expected, tag);
    }

    [Fact]
    public void From_UndefinedSeverity_FallsBackToLowerCaseNumber()
    {
        RuleSeverity undefined = (RuleSeverity)42;

        string tag = RuleSeverityTags.From(undefined);

        Assert.Equal("42", tag);
    }
}
