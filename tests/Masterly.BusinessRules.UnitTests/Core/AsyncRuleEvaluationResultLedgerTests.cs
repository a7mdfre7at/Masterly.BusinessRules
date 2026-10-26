namespace Masterly.BusinessRules.UnitTests;

public class AsyncRuleEvaluationResultLedgerTests
{
    private const string PassingCode = "PASS";
    private const string BrokenCode = "BROKEN";
    private const string FaultedCode = "FAULTED";
    private const string NotEvaluatedCode = "SKIPPED";

    [Fact]
    public void LegacyConstructor_HasEmptyLedger()
    {
        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(
            new[] { new BusinessRuleResult(BrokenCode, BrokenCode, RuleSeverity.Error) },
            new[] { RuleTestHelper.CreatePassingAsyncRule(PassingCode) });

        Assert.Empty(result.Entries);
        Assert.Empty(result.Faulted);
        Assert.False(result.HasFaults);
        Assert.Equal(TimeSpan.Zero, result.TotalDuration);
        Assert.Null(result.Slowest);
        Assert.True(result.HasBrokenRules);
    }

    [Fact]
    public void LegacyConstructor_NullBrokenRules_ThrowsArgumentNullException()
    {
        IEnumerable<BusinessRuleResult> brokenRules = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => new AsyncRuleEvaluationResult(brokenRules, Array.Empty<IAsyncBusinessRule>()));
    }

    [Fact]
    public void LegacyConstructor_NullPassedRules_ThrowsArgumentNullException()
    {
        IEnumerable<IAsyncBusinessRule> passedRules = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => new AsyncRuleEvaluationResult(Array.Empty<BusinessRuleResult>(), passedRules));
    }

    [Fact]
    public void LedgerConstructor_DerivesBrokenAndPassedRulesFromEntries()
    {
        IAsyncBusinessRule passing = RuleTestHelper.CreatePassingAsyncRule(PassingCode);
        IAsyncBusinessRule broken = RuleTestHelper.CreateBrokenAsyncRule(BrokenCode);
        BusinessRuleResult brokenResult = new BusinessRuleResult(BrokenCode, BrokenCode, RuleSeverity.Error);
        RuleEvaluationEntry[] entries =
        {
            new RuleEvaluationEntry(0, passing, RuleOutcome.Passed, null, null, TimeSpan.FromMilliseconds(1)),
            new RuleEvaluationEntry(1, broken, RuleOutcome.Broken, brokenResult, null, TimeSpan.FromMilliseconds(2))
        };

        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(entries, TimeSpan.FromMilliseconds(5));

        Assert.Equal(2, result.Entries.Count);
        Assert.Same(brokenResult, Assert.Single(result.BrokenRules));
        Assert.Same(passing, Assert.Single(result.PassedRules));
        Assert.Equal(TimeSpan.FromMilliseconds(5), result.TotalDuration);
        Assert.False(result.HasFaults);
    }

    [Fact]
    public void LedgerConstructor_FaultedEntryWithoutResult_IsNeitherBrokenNorPassed()
    {
        IAsyncBusinessRule faulted = RuleTestHelper.CreatePassingAsyncRule(FaultedCode);
        RuleEvaluationEntry[] entries =
        {
            new RuleEvaluationEntry(0, faulted, RuleOutcome.Faulted, null, new InvalidOperationException(), TimeSpan.Zero)
        };

        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(entries, TimeSpan.Zero);

        Assert.Empty(result.BrokenRules);
        Assert.Empty(result.PassedRules);
        Assert.True(result.AllPassed);
        Assert.True(result.HasFaults);
        Assert.Same(entries[0], Assert.Single(result.Faulted));
    }

    [Fact]
    public void LedgerConstructor_FaultedEntryWithResult_CountsAsBroken()
    {
        IAsyncBusinessRule faulted = RuleTestHelper.CreatePassingAsyncRule(FaultedCode);
        BusinessRuleResult synthesised = new BusinessRuleResult(FaultedCode, FaultedCode, RuleSeverity.Error);
        RuleEvaluationEntry[] entries =
        {
            new RuleEvaluationEntry(0, faulted, RuleOutcome.Faulted, synthesised, new InvalidOperationException(), TimeSpan.Zero)
        };

        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(entries, TimeSpan.Zero);

        Assert.Same(synthesised, Assert.Single(result.BrokenRules));
        Assert.Empty(result.PassedRules);
        Assert.True(result.HasFaults);
    }

    [Fact]
    public void LedgerConstructor_NotEvaluatedEntry_IsNeitherBrokenNorPassed()
    {
        IAsyncBusinessRule skipped = RuleTestHelper.CreatePassingAsyncRule(NotEvaluatedCode);
        RuleEvaluationEntry[] entries =
        {
            new RuleEvaluationEntry(0, skipped, RuleOutcome.NotEvaluated, null, null, TimeSpan.Zero)
        };

        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(entries, TimeSpan.Zero);

        Assert.Empty(result.BrokenRules);
        Assert.Empty(result.PassedRules);
        Assert.False(result.HasFaults);
    }

    [Fact]
    public void LedgerConstructor_EmptyEntries_ProducesEmptyResult()
    {
        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(Array.Empty<RuleEvaluationEntry>(), TimeSpan.Zero);

        Assert.Empty(result.Entries);
        Assert.Empty(result.BrokenRules);
        Assert.Empty(result.PassedRules);
        Assert.Null(result.Slowest);
        Assert.True(result.AllPassed);
    }

    [Fact]
    public void LedgerConstructor_NullEntries_ThrowsArgumentNullException()
    {
        IEnumerable<RuleEvaluationEntry> entries = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => new AsyncRuleEvaluationResult(entries, TimeSpan.Zero));
    }

    [Fact]
    public void LedgerConstructor_EntryWithSyncRule_ThrowsArgumentException()
    {
        IBusinessRule syncRule = RuleTestHelper.CreatePassingRule();
        RuleEvaluationEntry[] entries = { new RuleEvaluationEntry(0, syncRule, RuleOutcome.Passed, null, null, TimeSpan.Zero) };

        Assert.Throws<ArgumentException>(() => new AsyncRuleEvaluationResult(entries, TimeSpan.Zero));
    }

    [Fact]
    public void Slowest_ReturnsEntryWithLongestDuration()
    {
        RuleEvaluationEntry[] entries =
        {
            Passed(0, TimeSpan.FromMilliseconds(1)),
            Passed(1, TimeSpan.FromMilliseconds(9)),
            Passed(2, TimeSpan.FromMilliseconds(4))
        };

        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(entries, TimeSpan.FromMilliseconds(14));

        Assert.Same(entries[1], result.Slowest);
    }

    [Fact]
    public void Slowest_OnTie_ReturnsEarliestEntry()
    {
        RuleEvaluationEntry[] entries =
        {
            Passed(0, TimeSpan.FromMilliseconds(5)),
            Passed(1, TimeSpan.FromMilliseconds(5))
        };

        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(entries, TimeSpan.FromMilliseconds(10));

        Assert.Same(entries[0], result.Slowest);
    }

    [Fact]
    public void ThrowIfAtLeast_SplitsThrownAndCollectedAndCarriesEntries()
    {
        RuleEvaluationEntry[] entries =
        {
            Broken(0, "INFO", RuleSeverity.Info),
            Broken(1, "WARN", RuleSeverity.Warning),
            Broken(2, "ERR", RuleSeverity.Error)
        };
        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(entries, TimeSpan.Zero);

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => result.ThrowIfAtLeast(RuleSeverity.Warning));

        Assert.Equal(new[] { "WARN", "ERR" }, exception.BrokenRules.Select(r => r.Code));
        Assert.Equal(new[] { "INFO" }, exception.CollectedRules.Select(r => r.Code));
        Assert.Equal(3, exception.Entries.Count);
        Assert.Same(entries[0], exception.Entries[0]);
    }

    [Fact]
    public void ThrowIfAtLeast_NothingAtThreshold_DoesNotThrow()
    {
        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(new[] { Broken(0, "WARN", RuleSeverity.Warning) }, TimeSpan.Zero);

        result.ThrowIfAtLeast(RuleSeverity.Error);
    }

    [Fact]
    public void ThrowIfAtLeast_Info_ThrowsForEveryBrokenRule()
    {
        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(new[] { Broken(0, "INFO", RuleSeverity.Info) }, TimeSpan.Zero);

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => result.ThrowIfAtLeast(RuleSeverity.Info));

        Assert.Single(exception.BrokenRules);
        Assert.Empty(exception.CollectedRules);
    }

    [Fact]
    public void ThrowIfAtLeast_NoBrokenRules_DoesNotThrow()
    {
        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(new[] { Passed(0, TimeSpan.Zero) }, TimeSpan.Zero);

        result.ThrowIfAtLeast(RuleSeverity.Info);
    }

    [Fact]
    public void ThrowIfBroken_CarriesLedgerEntries()
    {
        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(new[] { Broken(0, "INFO", RuleSeverity.Info) }, TimeSpan.Zero);

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => result.ThrowIfBroken());

        Assert.Single(exception.Entries);
    }

    [Fact]
    public void ThrowIfErrors_CollectsWarningsOnException()
    {
        RuleEvaluationEntry[] entries = { Broken(0, "WARN", RuleSeverity.Warning), Broken(1, "ERR", RuleSeverity.Error) };
        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(entries, TimeSpan.Zero);

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => result.ThrowIfErrors());

        Assert.Equal("ERR", Assert.Single(exception.BrokenRules).Code);
        Assert.Equal("WARN", Assert.Single(exception.CollectedRules).Code);
    }

    private static RuleEvaluationEntry Passed(int order, TimeSpan duration)
    {
        return new RuleEvaluationEntry(order, RuleTestHelper.CreatePassingAsyncRule(PassingCode), RuleOutcome.Passed, null, null, duration);
    }

    private static RuleEvaluationEntry Broken(int order, string code, RuleSeverity severity)
    {
        IAsyncBusinessRule rule = RuleTestHelper.CreateBrokenAsyncRule(code);
        return new RuleEvaluationEntry(order, rule, RuleOutcome.Broken, new BusinessRuleResult(code, code, severity), null, TimeSpan.Zero);
    }
}
