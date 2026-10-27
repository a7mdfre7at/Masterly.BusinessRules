namespace Masterly.BusinessRules.UnitTests;

public class SubjectEvaluationTests
{
    private const string BrokenCode = "BROKEN";
    private const string PassingCode = "PASS";

    // ---------------------------------------------------------------------
    // SubjectEvaluation<T>
    // ---------------------------------------------------------------------

    [Fact]
    public void Constructor_ExposesIndexSubjectAndResult()
    {
        RuleEvaluationResult result = Result(broken: true, TimeSpan.FromMilliseconds(3));

        SubjectEvaluation<string> evaluation = new SubjectEvaluation<string>(2, "subject", result);

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
        SubjectEvaluation<int> evaluation = new SubjectEvaluation<int>(0, 5, Result(broken: false, TimeSpan.Zero));

        Assert.True(evaluation.AllPassed);
        Assert.False(evaluation.HasBrokenRules);
        Assert.Empty(evaluation.BrokenRules);
    }

    [Fact]
    public void Constructor_NegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SubjectEvaluation<int>(-1, 5, Result(broken: false, TimeSpan.Zero)));
    }

    [Fact]
    public void Constructor_NullResult_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SubjectEvaluation<int>(0, 5, null!));
    }

    [Fact]
    public void Constructor_NullSubject_IsAllowed()
    {
        SubjectEvaluation<string?> evaluation = new SubjectEvaluation<string?>(0, null, Result(broken: false, TimeSpan.Zero));

        Assert.Null(evaluation.Subject);
    }

    // ---------------------------------------------------------------------
    // SubjectEvaluationCollection<T>
    // ---------------------------------------------------------------------

    [Fact]
    public void Collection_ExposesItemsByPositionAndAggregates()
    {
        SubjectEvaluation<int> first = new SubjectEvaluation<int>(0, 1, Result(broken: true, TimeSpan.FromMilliseconds(2)));
        SubjectEvaluation<int> second = new SubjectEvaluation<int>(1, 2, Result(broken: false, TimeSpan.FromMilliseconds(3)));

        SubjectEvaluationCollection<int> collection = new SubjectEvaluationCollection<int>(new[] { first, second });

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
        SubjectEvaluationCollection<int> collection = new SubjectEvaluationCollection<int>(Array.Empty<SubjectEvaluation<int>>());

        Assert.Empty(collection);
        Assert.True(collection.AllPassed);
        Assert.False(collection.HasBrokenRules);
        Assert.Empty(collection.Broken);
        Assert.Empty(collection.Passed);
        Assert.Empty(collection.BrokenRules);
        Assert.Equal(TimeSpan.Zero, collection.TotalDuration);
    }

    [Fact]
    public void Collection_NullEvaluations_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SubjectEvaluationCollection<int>(null!));
    }

    [Fact]
    public void Collection_NullEvaluationInside_ThrowsArgumentException()
    {
        SubjectEvaluation<int>[] evaluations = { new SubjectEvaluation<int>(0, 1, Result(broken: false, TimeSpan.Zero)), null! };

        Assert.Throws<ArgumentException>(() => new SubjectEvaluationCollection<int>(evaluations));
    }

    [Fact]
    public void Collection_IsAReadOnlyList()
    {
        SubjectEvaluationCollection<int> collection = new SubjectEvaluationCollection<int>(new[] { new SubjectEvaluation<int>(0, 1, Result(broken: false, TimeSpan.Zero)) });

        IReadOnlyList<SubjectEvaluation<int>> list = collection;

        Assert.Single(list);
        Assert.Equal(1, list[0].Subject);
    }

    private static RuleEvaluationResult Result(bool broken, TimeSpan duration)
    {
        IBusinessRule rule = broken ? RuleTestHelper.CreateBrokenRule(BrokenCode) : RuleTestHelper.CreatePassingRule(PassingCode);
        RuleEvaluationEntry entry = new RuleEvaluationEntry(0, rule, broken ? RuleOutcome.Broken : RuleOutcome.Passed, broken ? rule.Evaluate() : null, null, duration);
        return new RuleEvaluationResult(new[] { entry }, duration);
    }
}
