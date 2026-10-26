namespace Masterly.BusinessRules.UnitTests;

public class RuleEvaluationResultShadowTests
{
    private const string PassingCode = "PASS";
    private const string ShadowCode = "SHADOW";

    [Fact]
    public void Constructor_WithShadowResults_ExposesThemInOrder()
    {
        IBusinessRule passing = RuleTestHelper.CreatePassingRule(PassingCode);
        RuleEvaluationEntry entry = new RuleEvaluationEntry(0, passing, RuleOutcome.Passed, null, null, TimeSpan.Zero);
        BusinessRuleResult first = new BusinessRuleResult(ShadowCode, "first", RuleSeverity.Error);
        BusinessRuleResult second = new BusinessRuleResult(ShadowCode, "second", RuleSeverity.Warning);

        RuleEvaluationResult result = new RuleEvaluationResult(new[] { entry }, TimeSpan.FromMilliseconds(1), new[] { first, second });

        Assert.Equal(new[] { first, second }, result.ShadowResults);
        Assert.True(result.HasShadowResults);
    }

    [Fact]
    public void Constructor_WithShadowResults_DoesNotCountThemAsBroken()
    {
        IBusinessRule passing = RuleTestHelper.CreatePassingRule(PassingCode);
        RuleEvaluationEntry entry = new RuleEvaluationEntry(0, passing, RuleOutcome.Passed, null, null, TimeSpan.Zero);
        BusinessRuleResult shadow = new BusinessRuleResult(ShadowCode, "shadow", RuleSeverity.Error);

        RuleEvaluationResult result = new RuleEvaluationResult(new[] { entry }, TimeSpan.Zero, new[] { shadow });

        Assert.Empty(result.BrokenRules);
        Assert.True(result.AllPassed);
        Assert.False(result.HasErrors);
        result.ThrowIfBroken();
        result.ThrowIfAtLeast(RuleSeverity.Info);
    }

    [Fact]
    public void Constructor_WithShadowResults_SnapshotsTheCollection()
    {
        List<BusinessRuleResult> shadows = new List<BusinessRuleResult> { new BusinessRuleResult(ShadowCode, "shadow", RuleSeverity.Error) };

        RuleEvaluationResult result = new RuleEvaluationResult(Array.Empty<RuleEvaluationEntry>(), TimeSpan.Zero, shadows);
        shadows.Clear();

        Assert.Single(result.ShadowResults);
    }

    [Fact]
    public void Constructor_NullShadowResults_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RuleEvaluationResult(Array.Empty<RuleEvaluationEntry>(), TimeSpan.Zero, null!));
    }

    [Fact]
    public void Constructor_FromLedgerWithoutShadowResults_HasNone()
    {
        RuleEvaluationResult result = new RuleEvaluationResult(Array.Empty<RuleEvaluationEntry>(), TimeSpan.Zero);

        Assert.Empty(result.ShadowResults);
        Assert.False(result.HasShadowResults);
    }

    [Fact]
    public void Constructor_FromBrokenAndPassedCollections_HasNoShadowResults()
    {
        RuleEvaluationResult result = new RuleEvaluationResult(Array.Empty<BusinessRuleResult>(), Array.Empty<IBusinessRule>());

        Assert.Empty(result.ShadowResults);
        Assert.False(result.HasShadowResults);
    }
}
