using Masterly.BusinessRules.Diagnostics.UnitTests.Support;
using Microsoft.Extensions.Logging;

namespace Masterly.BusinessRules.Diagnostics.UnitTests.Logging;

public class LoggingRuleExecutionObserverTests
{
    private const string BrokenCode = "ORDER.NO_ITEMS";
    private const string PassingCode = "ORDER.OK";
    private const string ThrowingCode = "ORDER.THROWS";
    private const string Flag = "shadow-flag";

    // ---------------------------------------------------------------------
    // Construction
    // ---------------------------------------------------------------------

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new LoggingRuleExecutionObserver(null!));
        Assert.Throws<ArgumentNullException>(() => new LoggingRuleExecutionObserver(null!, new LoggingRuleObserverOptions()));
    }

    [Fact]
    public void Constructor_NullOptions_UsesDefaults()
    {
        LoggingRuleExecutionObserver observer = new LoggingRuleExecutionObserver(new CapturingLogger(), null);

        Assert.NotNull(observer.Options);
        Assert.False(observer.Options.LogPassedRules);
    }

    [Fact]
    public void Callbacks_NullArguments_Throw()
    {
        LoggingRuleExecutionObserver observer = new LoggingRuleExecutionObserver(new CapturingLogger());

        Assert.Throws<ArgumentNullException>(() => observer.OnAfterEvaluate(null!, true, TimeSpan.Zero));
        Assert.Throws<ArgumentNullException>(() => observer.OnRuleFaulted(null!, new InvalidOperationException()));
        Assert.Throws<ArgumentNullException>(() => observer.OnRuleFaulted(TestRules.Sync(BrokenCode, true), null!));
        Assert.Throws<ArgumentNullException>(() => observer.OnBatchCompleted(null!));
        Assert.Throws<ArgumentNullException>(() => observer.OnShadowViolation(null!, new BusinessRuleResult(BrokenCode, "m", RuleSeverity.Error)));
        Assert.Throws<ArgumentNullException>(() => observer.OnShadowViolation(TestRules.Sync(BrokenCode, true), null!));
    }

    // ---------------------------------------------------------------------
    // Broken rules
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_BrokenErrorRule_LogsOneWarningWithStructuredFields()
    {
        CapturingLogger logger = new CapturingLogger();
        IBusinessRule broken = TestRules.Sync(BrokenCode, broken: true, message: "Order must contain at least one item.");

        BusinessRuleChecker.EvaluateAll(new[] { broken }, new RuleEvaluationOptions { Observer = new LoggingRuleExecutionObserver(logger, new LoggingRuleObserverOptions { LogBatches = false }) });

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(RuleLogEvents.RuleBroken, entry.EventId);
        Assert.Equal(BrokenCode, entry["RuleCode"]);
        Assert.Equal($"{BrokenCode} name", entry["RuleName"]);
        Assert.Equal(TestRules.Category, entry["Category"]);
        Assert.Equal(RuleSeverity.Error, entry["Severity"]);
        Assert.Equal(RuleOutcomeTags.Broken, entry["Outcome"]);
        Assert.Equal("Order must contain at least one item.", entry["Message"]);
        Assert.IsType<double>(entry["ElapsedMs"]);
        Assert.True((double)entry["ElapsedMs"]! >= 0);
        Assert.Contains(BrokenCode, entry.Message);
        Assert.Contains("Order must contain at least one item.", entry.Message);
    }

    [Theory]
    [InlineData(RuleSeverity.Info, LogLevel.Debug)]
    [InlineData(RuleSeverity.Warning, LogLevel.Information)]
    [InlineData(RuleSeverity.Error, LogLevel.Warning)]
    public void EvaluateAll_BrokenRule_LevelFollowsSeverityMap(RuleSeverity severity, LogLevel expected)
    {
        CapturingLogger logger = new CapturingLogger();
        IBusinessRule broken = TestRules.Sync(BrokenCode, broken: true, severity: severity);

        BusinessRuleChecker.EvaluateAll(new[] { broken }, new RuleEvaluationOptions { Observer = new LoggingRuleExecutionObserver(logger, new LoggingRuleObserverOptions { LogBatches = false }) });

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(expected, entry.Level);
        Assert.Equal(severity, entry["Severity"]);
    }

    [Fact]
    public void EvaluateAll_BrokenRuleMappedToNone_LogsNothing()
    {
        CapturingLogger logger = new CapturingLogger();
        LoggingRuleObserverOptions options = new LoggingRuleObserverOptions { LogBatches = false, BrokenLevelBySeverity = { [RuleSeverity.Error] = LogLevel.None } };

        BusinessRuleChecker.EvaluateAll(new[] { TestRules.Sync(BrokenCode, true) }, new RuleEvaluationOptions { Observer = new LoggingRuleExecutionObserver(logger, options) });

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void EvaluateAll_IncludeMessageFalse_RedactsMessageField()
    {
        CapturingLogger logger = new CapturingLogger();
        LoggingRuleObserverOptions options = new LoggingRuleObserverOptions { LogBatches = false, IncludeMessage = false };

        BusinessRuleChecker.EvaluateAll(new[] { TestRules.Sync(BrokenCode, true, message: "customer 42 owes 100") }, new RuleEvaluationOptions { Observer = new LoggingRuleExecutionObserver(logger, options) });

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LoggingRuleObserverOptions.RedactedMessage, entry["Message"]);
        Assert.DoesNotContain("customer 42", entry.Message);
        Assert.Equal(BrokenCode, entry["RuleCode"]);
    }

    [Fact]
    public void CheckAll_BrokenRule_LogsBeforeThrowing()
    {
        CapturingLogger logger = new CapturingLogger();
        RuleEvaluationOptions options = new RuleEvaluationOptions { Observer = new LoggingRuleExecutionObserver(logger, new LoggingRuleObserverOptions { LogBatches = false }) };

        Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.CheckAll(new[] { TestRules.Sync(BrokenCode, true) }, options));

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(RuleLogEvents.RuleBroken, entry.EventId);
    }

    // ---------------------------------------------------------------------
    // Passed rules
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_PassedRule_NotLoggedByDefault()
    {
        CapturingLogger logger = new CapturingLogger();

        BusinessRuleChecker.EvaluateAll(new[] { TestRules.Sync(PassingCode, false) }, new RuleEvaluationOptions { Observer = new LoggingRuleExecutionObserver(logger, new LoggingRuleObserverOptions { LogBatches = false }) });

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void EvaluateAll_PassedRuleWithLogPassedRules_LogsAtPassedLevel()
    {
        CapturingLogger logger = new CapturingLogger();
        LoggingRuleObserverOptions options = new LoggingRuleObserverOptions { LogBatches = false, LogPassedRules = true, PassedLevel = LogLevel.Trace };

        BusinessRuleChecker.EvaluateAll(new[] { TestRules.Sync(PassingCode, false) }, new RuleEvaluationOptions { Observer = new LoggingRuleExecutionObserver(logger, options) });

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Trace, entry.Level);
        Assert.Equal(RuleLogEvents.RulePassed, entry.EventId);
        Assert.Equal(PassingCode, entry["RuleCode"]);
        Assert.Equal(RuleOutcomeTags.Passed, entry["Outcome"]);
        Assert.False(entry.Fields.ContainsKey("Message"));
    }

    [Fact]
    public void EvaluateAll_LoggerDisabledForLevel_WritesNothing()
    {
        CapturingLogger logger = new CapturingLogger { MinimumLevel = LogLevel.Error };

        BusinessRuleChecker.EvaluateAll(new[] { TestRules.Sync(BrokenCode, true), TestRules.Sync(PassingCode, false) }, new RuleEvaluationOptions { Observer = new LoggingRuleExecutionObserver(logger, new LoggingRuleObserverOptions { LogPassedRules = true }) });

        Assert.Empty(logger.Entries);
    }

    // ---------------------------------------------------------------------
    // Faults
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_FaultRecorded_LogsErrorWithException()
    {
        CapturingLogger logger = new CapturingLogger();
        InvalidOperationException fault = new InvalidOperationException(TestRules.Boom);

        BusinessRuleChecker.EvaluateAll(new[] { TestRules.SyncThrowing(ThrowingCode, fault) }, new RuleEvaluationOptions { Observer = new LoggingRuleExecutionObserver(logger, new LoggingRuleObserverOptions { LogBatches = false }), OnFault = FaultPolicy.Record });

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(RuleLogEvents.RuleFaulted, entry.EventId);
        Assert.Same(fault, entry.Exception);
        Assert.Equal(ThrowingCode, entry["RuleCode"]);
        Assert.Equal(RuleOutcomeTags.Faulted, entry["Outcome"]);
    }

    [Fact]
    public void EvaluateAll_FaultTreatedAsBroken_LogsFaultOnceNotBrokenTwice()
    {
        CapturingLogger logger = new CapturingLogger();

        BusinessRuleChecker.EvaluateAll(new[] { TestRules.SyncThrowing(ThrowingCode) }, new RuleEvaluationOptions { Observer = new LoggingRuleExecutionObserver(logger, new LoggingRuleObserverOptions { LogBatches = false }), OnFault = FaultPolicy.TreatAsBroken });

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(RuleLogEvents.RuleFaulted, entry.EventId);
    }

    [Fact]
    public void CheckAll_FaultPropagates_LogsFaultThenRethrows()
    {
        CapturingLogger logger = new CapturingLogger();
        RuleEvaluationOptions options = new RuleEvaluationOptions { Observer = new LoggingRuleExecutionObserver(logger, new LoggingRuleObserverOptions { LogBatches = false }) };

        Assert.Throws<InvalidOperationException>(() => BusinessRuleChecker.CheckAll(new[] { TestRules.SyncThrowing(ThrowingCode) }, options));

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(RuleLogEvents.RuleFaulted, entry.EventId);
    }

    [Fact]
    public void EvaluateAll_FaultedLevelNone_LogsNothing()
    {
        CapturingLogger logger = new CapturingLogger();
        LoggingRuleObserverOptions options = new LoggingRuleObserverOptions { LogBatches = false, FaultedLevel = LogLevel.None };

        BusinessRuleChecker.EvaluateAll(new[] { TestRules.SyncThrowing(ThrowingCode) }, new RuleEvaluationOptions { Observer = new LoggingRuleExecutionObserver(logger, options), OnFault = FaultPolicy.Record });

        Assert.Empty(logger.Entries);
    }

    // ---------------------------------------------------------------------
    // Batches
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_Batch_LogsStartAndCompletionAtBatchLevel()
    {
        CapturingLogger logger = new CapturingLogger();
        IBusinessRule[] rules = { TestRules.Sync(BrokenCode, true), TestRules.Sync(PassingCode, false), TestRules.SyncThrowing(ThrowingCode) };

        BusinessRuleChecker.EvaluateAll(rules, new RuleEvaluationOptions { Observer = new LoggingRuleExecutionObserver(logger, new LoggingRuleObserverOptions { BatchLevel = LogLevel.Information }), OnFault = FaultPolicy.Record });

        LogEntry started = logger.Entries.First();
        LogEntry completed = logger.Entries.Last();
        Assert.Equal(RuleLogEvents.BatchStarted, started.EventId);
        Assert.Equal(LogLevel.Information, started.Level);
        Assert.Equal(3, started["RuleCount"]);
        Assert.Equal(RuleLogEvents.BatchCompleted, completed.EventId);
        Assert.Equal(LogLevel.Information, completed.Level);
        Assert.Equal(3, completed["RuleCount"]);
        Assert.Equal(1, completed["BrokenCount"]);
        Assert.Equal(1, completed["FaultedCount"]);
        Assert.Equal(1, completed["PassedCount"]);
        Assert.Equal(RuleOutcomeTags.Faulted, completed["Outcome"]);
        Assert.IsType<double>(completed["ElapsedMs"]);
        Assert.Equal(4, logger.Entries.Count);
    }

    [Fact]
    public void EvaluateAll_EmptyBatch_LogsZeroCounts()
    {
        CapturingLogger logger = new CapturingLogger();

        BusinessRuleChecker.EvaluateAll(Array.Empty<IBusinessRule>(), new RuleEvaluationOptions { Observer = new LoggingRuleExecutionObserver(logger) });

        Assert.Equal(2, logger.Entries.Count);
        Assert.Equal(0, logger.Entries[0]["RuleCount"]);
        Assert.Equal(RuleOutcomeTags.Passed, logger.Entries[1]["Outcome"]);
        Assert.Equal(0, logger.Entries[1]["PassedCount"]);
    }

    [Fact]
    public void EvaluateAll_LogBatchesFalse_LogsNoBatchLines()
    {
        CapturingLogger logger = new CapturingLogger();

        BusinessRuleChecker.EvaluateAll(new[] { TestRules.Sync(PassingCode, false) }, new RuleEvaluationOptions { Observer = new LoggingRuleExecutionObserver(logger, new LoggingRuleObserverOptions { LogBatches = false }) });

        Assert.Empty(logger.Entries);
    }

    // ---------------------------------------------------------------------
    // Shadow violations
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_ShadowViolation_LogsAtShadowLevelWithMessage()
    {
        CapturingLogger logger = new CapturingLogger();
        IBusinessRule gated = TestRules.Sync(BrokenCode, true, message: "would block").GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { gated }, new BusinessRuleContext(), new RuleEvaluationOptions { Observer = new LoggingRuleExecutionObserver(logger, new LoggingRuleObserverOptions { LogBatches = false }) });

        Assert.True(result.AllPassed);
        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(RuleLogEvents.ShadowViolation, entry.EventId);
        Assert.Equal(BrokenCode, entry["RuleCode"]);
        Assert.Equal("would block", entry["Message"]);
        Assert.Equal(RuleOutcomeTags.Shadow, entry["Outcome"]);
    }

    [Fact]
    public void EvaluateAll_ShadowViolationWithoutMessage_Redacts()
    {
        CapturingLogger logger = new CapturingLogger();
        IBusinessRule gated = TestRules.Sync(BrokenCode, true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));
        LoggingRuleObserverOptions options = new LoggingRuleObserverOptions { LogBatches = false, IncludeMessage = false, ShadowViolationLevel = LogLevel.Warning };

        BusinessRuleChecker.EvaluateAll(new[] { gated }, new BusinessRuleContext(), new RuleEvaluationOptions { Observer = new LoggingRuleExecutionObserver(logger, options) });

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(LoggingRuleObserverOptions.RedactedMessage, entry["Message"]);
    }

    // ---------------------------------------------------------------------
    // Statelessness
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_SameRuleInstanceTwice_LogsTwoLines()
    {
        CapturingLogger logger = new CapturingLogger();
        IBusinessRule broken = TestRules.Sync(BrokenCode, true);

        BusinessRuleChecker.EvaluateAll(new[] { broken, broken }, new RuleEvaluationOptions { Observer = new LoggingRuleExecutionObserver(logger, new LoggingRuleObserverOptions { LogBatches = false }) });

        Assert.Equal(2, logger.Entries.Count);
        Assert.All(logger.Entries, entry => Assert.Equal(BrokenCode, entry["RuleCode"]));
    }

    [Fact]
    public void EvaluateAll_PassingRuleWithThrowingMetadataGetters_DegradesToTypeName()
    {
        // A broken rule with a throwing Code getter never reaches the observer (the core's Evaluate() throws first),
        // so the degradation is observable on the passed path, where only the observer reads the metadata.
        CapturingLogger logger = new CapturingLogger();
        IBusinessRule rule = new ThrowingMetadataRule();
        LoggingRuleObserverOptions options = new LoggingRuleObserverOptions { LogBatches = false, LogPassedRules = true, PassedLevel = LogLevel.Information };

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { rule }, new RuleEvaluationOptions { Observer = new LoggingRuleExecutionObserver(logger, options) });

        Assert.True(result.AllPassed);
        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(RuleLogEvents.RulePassed, entry.EventId);
        Assert.Equal(nameof(ThrowingMetadataRule), entry["RuleCode"]);
        Assert.Equal(nameof(ThrowingMetadataRule), entry["RuleName"]);
        Assert.Equal(string.Empty, entry["Category"]);
        Assert.Equal(RuleSeverity.Error, entry["Severity"]);
    }

    private sealed class ThrowingMetadataRule : BaseBusinessRule
    {
        public override string Code => throw new InvalidOperationException("code");

        public override string Message => "message";

        public override string Name => throw new InvalidOperationException("name");

        public override string Category => throw new InvalidOperationException("category");

        public override RuleSeverity Severity => throw new InvalidOperationException("severity");

        public override bool IsBroken()
        {
            return false;
        }
    }
}
