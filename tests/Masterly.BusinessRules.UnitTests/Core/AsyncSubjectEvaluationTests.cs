namespace Masterly.BusinessRules.UnitTests;

public class AsyncSubjectEvaluationTests
{
    private const string BrokenCode = "BROKEN";
    private const string PassingCode = "PASS";

    // ---------------------------------------------------------------------
    // AsyncSubjectEvaluation<T>
    // ---------------------------------------------------------------------

    [Fact]
    public void Constructor_ExposesIndexSubjectAndResult()
    {
        AsyncRuleEvaluationResult result = Result(broken: true, TimeSpan.FromMilliseconds(3));

        AsyncSubjectEvaluation<string> evaluation = new AsyncSubjectEvaluation<string>(2, "subject", result);

        Assert.Equal(2, evaluation.Index);
        Assert.Equal("subject", evaluation.Subject);
        Assert.Same(result, evaluation.Result);
        Assert.True(evaluation.HasBrokenRules);
        Assert.False(evaluation.AllPassed);
        Assert.Equal(BrokenCode, Assert.Single(evaluation.BrokenRules).Code);
    }

    [Fact]
    public void Constructor_PassingResult_ReportsAllPassed()
    {
        AsyncSubjectEvaluation<int> evaluation = new AsyncSubjectEvaluation<int>(0, 5, Result(broken: false, TimeSpan.Zero));

        Assert.True(evaluation.AllPassed);
        Assert.False(evaluation.HasBrokenRules);
        Assert.Empty(evaluation.BrokenRules);
    }

    [Fact]
    public void Constructor_NegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AsyncSubjectEvaluation<int>(-1, 5, Result(broken: false, TimeSpan.Zero)));
    }

    [Fact]
    public void Constructor_NullResult_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AsyncSubjectEvaluation<int>(0, 5, null!));
    }

    // ---------------------------------------------------------------------
    // AsyncSubjectEvaluationCollection<T>
    // ---------------------------------------------------------------------

    [Fact]
    public void Collection_ExposesItemsByPositionAndAggregates()
    {
        AsyncSubjectEvaluation<int> first = new AsyncSubjectEvaluation<int>(0, 1, Result(broken: true, TimeSpan.FromMilliseconds(2)));
        AsyncSubjectEvaluation<int> second = new AsyncSubjectEvaluation<int>(1, 2, Result(broken: false, TimeSpan.FromMilliseconds(3)));

        AsyncSubjectEvaluationCollection<int> collection = new AsyncSubjectEvaluationCollection<int>(new[] { first, second });

        Assert.Equal(2, collection.Count);
        Assert.Same(first, collection[0]);
        Assert.Same(second, collection[1]);
        Assert.Equal(new[] { first, second }, collection.ToList());
        Assert.False(collection.AllPassed);
        Assert.True(collection.HasBrokenRules);
        Assert.Same(first, Assert.Single(collection.Broken));
        Assert.Same(second, Assert.Single(collection.Passed));
        Assert.Equal(BrokenCode, Assert.Single(collection.BrokenRules).Code);
        Assert.Equal(TimeSpan.FromMilliseconds(5), collection.TotalDuration);
    }

    [Fact]
    public void Collection_Empty_AllPassedAndZeroDuration()
    {
        AsyncSubjectEvaluationCollection<int> collection = new AsyncSubjectEvaluationCollection<int>(Array.Empty<AsyncSubjectEvaluation<int>>());

        Assert.Empty(collection);
        Assert.True(collection.AllPassed);
        Assert.False(collection.HasBrokenRules);
        Assert.Equal(TimeSpan.Zero, collection.TotalDuration);
    }

    [Fact]
    public void Collection_NullEvaluations_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AsyncSubjectEvaluationCollection<int>(null!));
    }

    [Fact]
    public void Collection_NullEvaluationInside_ThrowsArgumentException()
    {
        AsyncSubjectEvaluation<int>[] evaluations = { new AsyncSubjectEvaluation<int>(0, 1, Result(broken: false, TimeSpan.Zero)), null! };

        Assert.Throws<ArgumentException>(() => new AsyncSubjectEvaluationCollection<int>(evaluations));
    }

    private static AsyncRuleEvaluationResult Result(bool broken, TimeSpan duration)
    {
        IAsyncBusinessRule rule = (broken ? RuleTestHelper.CreateBrokenRule(BrokenCode) : RuleTestHelper.CreatePassingRule(PassingCode)).ToAsync();
        RuleEvaluationEntry entry = new RuleEvaluationEntry(0, rule, broken ? RuleOutcome.Broken : RuleOutcome.Passed, broken ? new BusinessRuleResult(BrokenCode, "m", RuleSeverity.Error) : null, null, duration);
        return new AsyncRuleEvaluationResult(new[] { entry }, duration);
    }
}
