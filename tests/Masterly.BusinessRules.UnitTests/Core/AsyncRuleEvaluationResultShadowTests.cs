namespace Masterly.BusinessRules.UnitTests;

public class AsyncRuleEvaluationResultShadowTests
{
    private const string PassingCode = "PASS";
    private const string ShadowCode = "SHADOW";

    [Fact]
    public void Constructor_WithShadowResults_ExposesThemInOrder()
    {
        IAsyncBusinessRule passing = RuleTestHelper.CreatePassingAsyncRule(PassingCode);
        RuleEvaluationEntry entry = new RuleEvaluationEntry(0, passing, RuleOutcome.Passed, null, null, TimeSpan.Zero);
        BusinessRuleResult first = new BusinessRuleResult(ShadowCode, "first", RuleSeverity.Error);
        BusinessRuleResult second = new BusinessRuleResult(ShadowCode, "second", RuleSeverity.Warning);

        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(new[] { entry }, TimeSpan.FromMilliseconds(1), new[] { first, second });

        Assert.Equal(new[] { first, second }, result.ShadowResults);
        Assert.True(result.HasShadowResults);
    }

    [Fact]
    public void Constructor_WithShadowResults_DoesNotCountThemAsBroken()
    {
        IAsyncBusinessRule passing = RuleTestHelper.CreatePassingAsyncRule(PassingCode);
        RuleEvaluationEntry entry = new RuleEvaluationEntry(0, passing, RuleOutcome.Passed, null, null, TimeSpan.Zero);
        BusinessRuleResult shadow = new BusinessRuleResult(ShadowCode, "shadow", RuleSeverity.Error);

        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(new[] { entry }, TimeSpan.Zero, new[] { shadow });

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

        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(Array.Empty<RuleEvaluationEntry>(), TimeSpan.Zero, shadows);
        shadows.Clear();

        Assert.Single(result.ShadowResults);
    }

    [Fact]
    public void Constructor_NullShadowResults_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AsyncRuleEvaluationResult(Array.Empty<RuleEvaluationEntry>(), TimeSpan.Zero, null!));
    }

    [Fact]
    public void Constructor_FromLedgerWithoutShadowResults_HasNone()
    {
        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(Array.Empty<RuleEvaluationEntry>(), TimeSpan.Zero);

        Assert.Empty(result.ShadowResults);
        Assert.False(result.HasShadowResults);
    }

    [Fact]
    public void Constructor_FromBrokenAndPassedCollections_HasNoShadowResults()
    {
        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(Array.Empty<BusinessRuleResult>(), Array.Empty<IAsyncBusinessRule>());

        Assert.Empty(result.ShadowResults);
        Assert.False(result.HasShadowResults);
    }
}
