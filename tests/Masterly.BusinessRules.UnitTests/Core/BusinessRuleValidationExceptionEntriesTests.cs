namespace Masterly.BusinessRules.UnitTests;

public class BusinessRuleValidationExceptionEntriesTests
{
    private const string ErrorCode = "ERR";
    private const string WarningCode = "WARN";

    [Fact]
    public void LegacyConstructor_HasEmptyCollectedRulesAndEntries()
    {
        BusinessRuleValidationException exception = new BusinessRuleValidationException(
            new[] { new BusinessRuleResult(ErrorCode, ErrorCode, RuleSeverity.Error) });

        Assert.Single(exception.BrokenRules);
        Assert.Empty(exception.CollectedRules);
        Assert.Empty(exception.Entries);
    }

    [Fact]
    public void LegacyConstructor_NullBrokenRules_ThrowsArgumentNullException()
    {
        IEnumerable<BusinessRuleResult> brokenRules = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => new BusinessRuleValidationException(brokenRules));
    }

    [Fact]
    public void LedgerConstructor_SetsBrokenCollectedAndEntries()
    {
        BusinessRuleResult error = new BusinessRuleResult(ErrorCode, ErrorCode, RuleSeverity.Error);
        BusinessRuleResult warning = new BusinessRuleResult(WarningCode, WarningCode, RuleSeverity.Warning);
        RuleEvaluationEntry entry = new RuleEvaluationEntry(0, RuleTestHelper.CreateBrokenRule(ErrorCode), RuleOutcome.Broken, error, null, TimeSpan.Zero);

        BusinessRuleValidationException exception = new BusinessRuleValidationException(new[] { error }, new[] { warning }, new[] { entry });

        Assert.Same(error, Assert.Single(exception.BrokenRules));
        Assert.Same(warning, Assert.Single(exception.CollectedRules));
        Assert.Same(entry, Assert.Single(exception.Entries));
    }

    [Fact]
    public void LedgerConstructor_CopiesInputs()
    {
        List<BusinessRuleResult> broken = new List<BusinessRuleResult> { new BusinessRuleResult(ErrorCode, ErrorCode, RuleSeverity.Error) };
        List<BusinessRuleResult> collected = new List<BusinessRuleResult>();
        List<RuleEvaluationEntry> entries = new List<RuleEvaluationEntry>();

        BusinessRuleValidationException exception = new BusinessRuleValidationException(broken, collected, entries);
        broken.Add(new BusinessRuleResult(WarningCode, WarningCode, RuleSeverity.Warning));
        collected.Add(new BusinessRuleResult(WarningCode, WarningCode, RuleSeverity.Warning));

        Assert.Single(exception.BrokenRules);
        Assert.Empty(exception.CollectedRules);
    }

    [Fact]
    public void LedgerConstructor_NullBrokenRules_ThrowsArgumentNullException()
    {
        IEnumerable<BusinessRuleResult> brokenRules = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => new BusinessRuleValidationException(brokenRules, Array.Empty<BusinessRuleResult>(), Array.Empty<RuleEvaluationEntry>()));
    }

    [Fact]
    public void LedgerConstructor_NullCollectedRules_ThrowsArgumentNullException()
    {
        IEnumerable<BusinessRuleResult> collectedRules = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => new BusinessRuleValidationException(Array.Empty<BusinessRuleResult>(), collectedRules, Array.Empty<RuleEvaluationEntry>()));
    }

    [Fact]
    public void LedgerConstructor_NullEntries_ThrowsArgumentNullException()
    {
        IEnumerable<RuleEvaluationEntry> entries = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => new BusinessRuleValidationException(Array.Empty<BusinessRuleResult>(), Array.Empty<BusinessRuleResult>(), entries));
    }

    [Fact]
    public void Flatten_IgnoresCollectedRules()
    {
        BusinessRuleResult error = new BusinessRuleResult(ErrorCode, ErrorCode, RuleSeverity.Error);
        BusinessRuleResult warning = new BusinessRuleResult(WarningCode, WarningCode, RuleSeverity.Warning);

        BusinessRuleValidationException exception = new BusinessRuleValidationException(new[] { error }, new[] { warning }, Array.Empty<RuleEvaluationEntry>());

        Assert.Equal(new[] { ErrorCode }, exception.Flatten().Select(r => r.Code));
    }
}
