namespace Masterly.BusinessRules.UnitTests;

public class RuleEvaluationEntryTests
{
    private const string Code = "ENTRY.CODE";
    private const string Message = "Entry message";

    [Fact]
    public void Constructor_SyncRule_SetsAllProperties()
    {
        IBusinessRule rule = RuleTestHelper.CreateBrokenRule(Code, Message);
        BusinessRuleResult result = new BusinessRuleResult(Code, Message, RuleSeverity.Warning);
        TimeSpan duration = TimeSpan.FromMilliseconds(3);

        RuleEvaluationEntry entry = new RuleEvaluationEntry(2, rule, RuleOutcome.Broken, result, null, duration);

        Assert.Equal(2, entry.Order);
        Assert.Same(rule, entry.Rule);
        Assert.Null(entry.AsyncRule);
        Assert.Equal(Code, entry.Code);
        Assert.Equal(RuleOutcome.Broken, entry.Outcome);
        Assert.Same(result, entry.Result);
        Assert.Null(entry.Fault);
        Assert.Equal(duration, entry.Duration);
        Assert.True(entry.IsBroken);
        Assert.False(entry.IsFaulted);
    }

    [Fact]
    public void Constructor_AsyncRule_SetsAsyncRuleAndLeavesSyncRuleNull()
    {
        IAsyncBusinessRule rule = RuleTestHelper.CreatePassingAsyncRule(Code, Message);

        RuleEvaluationEntry entry = new RuleEvaluationEntry(0, rule, RuleOutcome.Passed, null, null, TimeSpan.Zero);

        Assert.Null(entry.Rule);
        Assert.Same(rule, entry.AsyncRule);
        Assert.Equal(Code, entry.Code);
        Assert.Equal(RuleOutcome.Passed, entry.Outcome);
        Assert.False(entry.IsBroken);
        Assert.False(entry.IsFaulted);
    }

    [Fact]
    public void Constructor_WithFault_ReportsIsFaulted()
    {
        IBusinessRule rule = RuleTestHelper.CreatePassingRule(Code, Message);
        InvalidOperationException fault = new InvalidOperationException("boom");

        RuleEvaluationEntry entry = new RuleEvaluationEntry(0, rule, RuleOutcome.Faulted, null, fault, TimeSpan.Zero);

        Assert.True(entry.IsFaulted);
        Assert.Same(fault, entry.Fault);
        Assert.False(entry.IsBroken);
    }

    [Fact]
    public void Constructor_FaultWithTreatAsBrokenResult_IsBothBrokenAndFaulted()
    {
        IBusinessRule rule = RuleTestHelper.CreatePassingRule(Code, Message);
        BusinessRuleResult result = new BusinessRuleResult(Code, Message, RuleSeverity.Error);

        RuleEvaluationEntry entry = new RuleEvaluationEntry(0, rule, RuleOutcome.Faulted, result, new InvalidOperationException(), TimeSpan.Zero);

        Assert.True(entry.IsFaulted);
        Assert.True(entry.IsBroken);
    }

    [Fact]
    public void Constructor_NullSyncRule_ThrowsArgumentNullException()
    {
        IBusinessRule rule = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => new RuleEvaluationEntry(0, rule, RuleOutcome.Passed, null, null, TimeSpan.Zero));
    }

    [Fact]
    public void Constructor_NullAsyncRule_ThrowsArgumentNullException()
    {
        IAsyncBusinessRule rule = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => new RuleEvaluationEntry(0, rule, RuleOutcome.Passed, null, null, TimeSpan.Zero));
    }

    [Fact]
    public void Constructor_NegativeOrder_ThrowsArgumentOutOfRangeException()
    {
        IBusinessRule rule = RuleTestHelper.CreatePassingRule(Code, Message);

        Assert.Throws<ArgumentOutOfRangeException>(() => new RuleEvaluationEntry(-1, rule, RuleOutcome.Passed, null, null, TimeSpan.Zero));
    }
}
