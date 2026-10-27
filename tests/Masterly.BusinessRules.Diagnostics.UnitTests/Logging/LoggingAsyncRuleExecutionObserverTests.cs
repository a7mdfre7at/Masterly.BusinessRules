using Masterly.BusinessRules.Diagnostics.UnitTests.Support;
using Microsoft.Extensions.Logging;

namespace Masterly.BusinessRules.Diagnostics.UnitTests.Logging;

public class LoggingAsyncRuleExecutionObserverTests
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
        Assert.Throws<ArgumentNullException>(() => new LoggingAsyncRuleExecutionObserver(null!));
        Assert.Throws<ArgumentNullException>(() => new LoggingAsyncRuleExecutionObserver(null!, new LoggingRuleObserverOptions()));
    }

    [Fact]
    public void Constructor_NullOptions_UsesDefaults()
    {
        LoggingAsyncRuleExecutionObserver observer = new LoggingAsyncRuleExecutionObserver(new CapturingLogger(), null);

        Assert.NotNull(observer.Options);
        Assert.False(observer.Options.LogPassedRules);
    }

    [Fact]
    public async Task Callbacks_NullArguments_Throw()
    {
        LoggingAsyncRuleExecutionObserver observer = new LoggingAsyncRuleExecutionObserver(new CapturingLogger());

        await Assert.ThrowsAsync<ArgumentNullException>(() => observer.OnAfterEvaluateAsync(null!, true, TimeSpan.Zero, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => observer.OnRuleFaultedAsync(null!, new InvalidOperationException(), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => observer.OnRuleFaultedAsync(TestRules.Async(BrokenCode, true), null!, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => observer.OnBatchCompletedAsync(null!, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => observer.OnShadowViolationAsync(null!, new BusinessRuleResult(BrokenCode, "m", RuleSeverity.Error), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => observer.OnShadowViolationAsync(TestRules.Async(BrokenCode, true), null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Callbacks_Always_CompleteSynchronously()
    {
        LoggingAsyncRuleExecutionObserver observer = new LoggingAsyncRuleExecutionObserver(new CapturingLogger());
        IAsyncBusinessRule rule = TestRules.Async(BrokenCode, true);
        CancellationToken cancelled = new CancellationToken(canceled: true);

        Task[] tasks =
        {
            observer.OnBatchStartedAsync(1, cancelled),
            observer.OnBeforeEvaluateAsync(rule, cancelled),
            observer.OnAfterEvaluateAsync(rule, null, cancelled),
            observer.OnAfterEvaluateAsync(rule, true, TimeSpan.Zero, cancelled),
            observer.OnRuleBrokenAsync(rule, new BusinessRuleResult(BrokenCode, "m", RuleSeverity.Error), cancelled),
            observer.OnRuleFaultedAsync(rule, new InvalidOperationException(), cancelled),
            observer.OnShadowViolationAsync(rule, new BusinessRuleResult(BrokenCode, "m", RuleSeverity.Error), cancelled),
            observer.OnBatchCompletedAsync(new AsyncRuleEvaluationResult(Array.Empty<RuleEvaluationEntry>(), TimeSpan.Zero), cancelled)
        };

        Assert.All(tasks, task => Assert.True(task.IsCompletedSuccessfully));
    }

    // ---------------------------------------------------------------------
    // Broken rules
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_BrokenErrorRule_LogsOneWarningWithStructuredFields()
    {
        CapturingLogger logger = new CapturingLogger();
        IAsyncBusinessRule broken = TestRules.Async(BrokenCode, broken: true, message: "Order must contain at least one item.");

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { broken }, new AsyncRuleEvaluationOptions { Observer = new LoggingAsyncRuleExecutionObserver(logger, new LoggingRuleObserverOptions { LogBatches = false }) }, TestContext.Current.CancellationToken);

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
    }

    [Theory]
    [InlineData(RuleSeverity.Info, LogLevel.Debug)]
    [InlineData(RuleSeverity.Warning, LogLevel.Information)]
    [InlineData(RuleSeverity.Error, LogLevel.Warning)]
    public async Task EvaluateAllAsync_BrokenRule_LevelFollowsSeverityMap(RuleSeverity severity, LogLevel expected)
    {
        CapturingLogger logger = new CapturingLogger();

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { TestRules.Async(BrokenCode, true, severity) }, new AsyncRuleEvaluationOptions { Observer = new LoggingAsyncRuleExecutionObserver(logger, new LoggingRuleObserverOptions { LogBatches = false }) }, TestContext.Current.CancellationToken);

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(expected, entry.Level);
    }

    [Fact]
    public async Task EvaluateAllAsync_IncludeMessageFalse_RedactsMessageField()
    {
        CapturingLogger logger = new CapturingLogger();
        LoggingRuleObserverOptions options = new LoggingRuleObserverOptions { LogBatches = false, IncludeMessage = false };

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { TestRules.Async(BrokenCode, true, message: "customer 42 owes 100") }, new AsyncRuleEvaluationOptions { Observer = new LoggingAsyncRuleExecutionObserver(logger, options) }, TestContext.Current.CancellationToken);

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LoggingRuleObserverOptions.RedactedMessage, entry["Message"]);
        Assert.DoesNotContain("customer 42", entry.Message);
    }

    [Fact]
    public async Task CheckAllAsync_BrokenRule_LogsBeforeThrowing()
    {
        CapturingLogger logger = new CapturingLogger();
        AsyncRuleEvaluationOptions options = new AsyncRuleEvaluationOptions { Observer = new LoggingAsyncRuleExecutionObserver(logger, new LoggingRuleObserverOptions { LogBatches = false }) };

        await Assert.ThrowsAsync<BusinessRuleValidationException>(() => AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), new[] { TestRules.Async(BrokenCode, true) }, options, TestContext.Current.CancellationToken));

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(RuleLogEvents.RuleBroken, entry.EventId);
    }

    // ---------------------------------------------------------------------
    // Passed rules
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_PassedRule_NotLoggedByDefault()
    {
        CapturingLogger logger = new CapturingLogger();

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { TestRules.Async(PassingCode, false) }, new AsyncRuleEvaluationOptions { Observer = new LoggingAsyncRuleExecutionObserver(logger, new LoggingRuleObserverOptions { LogBatches = false }) }, TestContext.Current.CancellationToken);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task EvaluateAllAsync_PassedRuleWithLogPassedRules_LogsAtPassedLevel()
    {
        CapturingLogger logger = new CapturingLogger();
        LoggingRuleObserverOptions options = new LoggingRuleObserverOptions { LogBatches = false, LogPassedRules = true, PassedLevel = LogLevel.Information };

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { TestRules.Async(PassingCode, false) }, new AsyncRuleEvaluationOptions { Observer = new LoggingAsyncRuleExecutionObserver(logger, options) }, TestContext.Current.CancellationToken);

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(RuleLogEvents.RulePassed, entry.EventId);
        Assert.Equal(RuleOutcomeTags.Passed, entry["Outcome"]);
    }

    // ---------------------------------------------------------------------
    // Faults
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_FaultRecorded_LogsErrorWithException()
    {
        CapturingLogger logger = new CapturingLogger();
        InvalidOperationException fault = new InvalidOperationException(TestRules.Boom);

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { TestRules.AsyncThrowing(ThrowingCode, fault) }, new AsyncRuleEvaluationOptions { Observer = new LoggingAsyncRuleExecutionObserver(logger, new LoggingRuleObserverOptions { LogBatches = false }), OnFault = FaultPolicy.Record }, TestContext.Current.CancellationToken);

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(RuleLogEvents.RuleFaulted, entry.EventId);
        Assert.Same(fault, entry.Exception);
        Assert.Equal(ThrowingCode, entry["RuleCode"]);
        Assert.Equal(RuleOutcomeTags.Faulted, entry["Outcome"]);
    }

    [Fact]
    public async Task EvaluateAllAsync_FaultTreatedAsBroken_LogsFaultOnce()
    {
        CapturingLogger logger = new CapturingLogger();

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { TestRules.AsyncThrowing(ThrowingCode) }, new AsyncRuleEvaluationOptions { Observer = new LoggingAsyncRuleExecutionObserver(logger, new LoggingRuleObserverOptions { LogBatches = false }), OnFault = FaultPolicy.TreatAsBroken }, TestContext.Current.CancellationToken);

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(RuleLogEvents.RuleFaulted, entry.EventId);
    }

    [Fact]
    public async Task EvaluateAllAsync_Cancelled_LogsNothingForTheRule()
    {
        CapturingLogger logger = new CapturingLogger();
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();
        AsyncRuleEvaluationOptions options = new AsyncRuleEvaluationOptions { Observer = new LoggingAsyncRuleExecutionObserver(logger, new LoggingRuleObserverOptions { LogBatches = false }), OnFault = FaultPolicy.Record };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { TestRules.AsyncCancellable(BrokenCode) }, options, source.Token));

        Assert.Empty(logger.Entries);
    }

    // ---------------------------------------------------------------------
    // Batches
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_ParallelBatch_LogsOneLinePerRuleAndBatchLines()
    {
        CapturingLogger logger = new CapturingLogger();
        IAsyncBusinessRule[] rules =
        {
            TestRules.Async("A", true, onEvaluate: () => Task.Delay(5)),
            TestRules.Async("B", true, onEvaluate: () => Task.Delay(1)),
            TestRules.Async("C", false),
            TestRules.AsyncThrowing("D")
        };

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), rules, new AsyncRuleEvaluationOptions { Observer = new LoggingAsyncRuleExecutionObserver(logger), RunInParallel = true, OnFault = FaultPolicy.Record }, TestContext.Current.CancellationToken);

        Assert.Equal(RuleLogEvents.BatchStarted, logger.Entries.First().EventId);
        Assert.Equal(4, logger.Entries.First()["RuleCount"]);
        LogEntry completed = logger.Entries.Last();
        Assert.Equal(RuleLogEvents.BatchCompleted, completed.EventId);
        Assert.Equal(2, completed["BrokenCount"]);
        Assert.Equal(1, completed["FaultedCount"]);
        Assert.Equal(1, completed["PassedCount"]);
        Assert.Equal(RuleOutcomeTags.Faulted, completed["Outcome"]);
        Assert.Equal(new[] { "A", "B" }, logger.Entries.Where(e => e.EventId == RuleLogEvents.RuleBroken).Select(e => (string?)e["RuleCode"]).OrderBy(code => code));
        Assert.Equal("D", Assert.Single(logger.Entries, e => e.EventId == RuleLogEvents.RuleFaulted)["RuleCode"]);
        Assert.Equal(5, logger.Entries.Count);
    }

    [Fact]
    public async Task EvaluateAllAsync_EmptyBatch_LogsZeroCounts()
    {
        CapturingLogger logger = new CapturingLogger();

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), Array.Empty<IAsyncBusinessRule>(), new AsyncRuleEvaluationOptions { Observer = new LoggingAsyncRuleExecutionObserver(logger) }, TestContext.Current.CancellationToken);

        Assert.Equal(2, logger.Entries.Count);
        Assert.Equal(0, logger.Entries[0]["RuleCount"]);
        Assert.Equal(RuleOutcomeTags.Passed, logger.Entries[1]["Outcome"]);
    }

    // ---------------------------------------------------------------------
    // Shadow violations
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_ShadowViolation_LogsAtShadowLevelWithMessage()
    {
        CapturingLogger logger = new CapturingLogger();
        IAsyncBusinessRule gated = TestRules.Async(BrokenCode, true, message: "would block").GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { gated }, new AsyncRuleEvaluationOptions { Observer = new LoggingAsyncRuleExecutionObserver(logger, new LoggingRuleObserverOptions { LogBatches = false }) }, TestContext.Current.CancellationToken);

        Assert.True(result.AllPassed);
        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(RuleLogEvents.ShadowViolation, entry.EventId);
        Assert.Equal(BrokenCode, entry["RuleCode"]);
        Assert.Equal("would block", entry["Message"]);
        Assert.Equal(RuleOutcomeTags.Shadow, entry["Outcome"]);
    }

    [Fact]
    public async Task EvaluateAllAsync_ShadowViolationWithoutMessage_Redacts()
    {
        CapturingLogger logger = new CapturingLogger();
        IAsyncBusinessRule gated = TestRules.Async(BrokenCode, true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));
        LoggingRuleObserverOptions options = new LoggingRuleObserverOptions { LogBatches = false, IncludeMessage = false, ShadowViolationLevel = LogLevel.Warning };

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { gated }, new AsyncRuleEvaluationOptions { Observer = new LoggingAsyncRuleExecutionObserver(logger, options) }, TestContext.Current.CancellationToken);

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(LoggingRuleObserverOptions.RedactedMessage, entry["Message"]);
    }

    [Fact]
    public async Task EvaluateAllAsync_ShadowViolationBelowTheLoggerLevel_LogsNothing()
    {
        CapturingLogger logger = new CapturingLogger { MinimumLevel = LogLevel.Error };
        IAsyncBusinessRule gated = TestRules.Async(BrokenCode, true).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));
        LoggingRuleObserverOptions options = new LoggingRuleObserverOptions { LogBatches = false, ShadowViolationLevel = LogLevel.Warning };

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { gated }, new AsyncRuleEvaluationOptions { Observer = new LoggingAsyncRuleExecutionObserver(logger, options) }, TestContext.Current.CancellationToken);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task EvaluateAllAsync_SameRuleInstanceTwice_LogsTwoLines()
    {
        CapturingLogger logger = new CapturingLogger();
        IAsyncBusinessRule broken = TestRules.Async(BrokenCode, true);

        await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { broken, broken }, new AsyncRuleEvaluationOptions { Observer = new LoggingAsyncRuleExecutionObserver(logger, new LoggingRuleObserverOptions { LogBatches = false }) }, TestContext.Current.CancellationToken);

        Assert.Equal(2, logger.Entries.Count);
    }
}
