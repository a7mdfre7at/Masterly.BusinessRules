namespace Masterly.BusinessRules.UnitTests;

public class BusinessRuleCheckerLedgerTests
{
    private const string PassingCode = "PASS";
    private const string BrokenCode = "BROKEN";
    private const string ThrowingCode = "THROWS";
    private const string FlagKey = "flag";
    private const string Boom = "boom";

    private static readonly TimeSpan SlowRuleDuration = TimeSpan.FromMilliseconds(100);

    // ---------------------------------------------------------------------
    // Ledger shape
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_WithOptions_RecordsOneEntryPerRuleInSubmissionOrder()
    {
        IBusinessRule first = CreateRule("A", broken: false);
        IBusinessRule second = CreateRule("B", broken: true);
        IBusinessRule third = CreateRule("C", broken: false);

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { first, second, third }, new RuleEvaluationOptions());

        Assert.Equal(3, result.Entries.Count);
        Assert.Equal(new[] { 0, 1, 2 }, result.Entries.Select(e => e.Order));
        Assert.Equal(new[] { "A", "B", "C" }, result.Entries.Select(e => e.Code));
        Assert.Equal(new[] { RuleOutcome.Passed, RuleOutcome.Broken, RuleOutcome.Passed }, result.Entries.Select(e => e.Outcome));
        Assert.Same(second, result.Entries[1].Rule);
        Assert.NotNull(result.Entries[1].Result);
        Assert.Null(result.Entries[0].Result);
        Assert.All(result.Entries, e => Assert.Null(e.Fault));
    }

    [Fact]
    public void EvaluateAll_WithOptions_DerivesBrokenAndPassedRulesFromLedger()
    {
        IBusinessRule passing = CreateRule(PassingCode, broken: false);
        IBusinessRule broken = CreateRule(BrokenCode, broken: true);

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { passing, broken }, new RuleEvaluationOptions());

        Assert.Equal(BrokenCode, Assert.Single(result.BrokenRules).Code);
        Assert.Same(passing, Assert.Single(result.PassedRules));
    }

    [Fact]
    public void EvaluateAll_WithOptions_EmptyRules_ReturnsEmptyLedger()
    {
        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(Array.Empty<IBusinessRule>(), new RuleEvaluationOptions());

        Assert.Empty(result.Entries);
        Assert.Null(result.Slowest);
        Assert.True(result.AllPassed);
        Assert.False(result.HasFaults);
        Assert.True(result.TotalDuration >= TimeSpan.Zero);
    }

    [Fact]
    public void EvaluateAll_WithOptions_TotalDurationCoversEveryEntry()
    {
        IBusinessRule slow = CreateSlowRule("SLOW", SlowRuleDuration);
        IBusinessRule fast = CreateRule("FAST", broken: false);

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { slow, fast }, new RuleEvaluationOptions());

        Assert.NotNull(result.Slowest);
        Assert.True(result.TotalDuration >= result.Slowest!.Duration);
        Assert.All(result.Entries, e => Assert.True(result.TotalDuration >= e.Duration));
    }

    [Fact]
    public void EvaluateAll_WithOptions_SlowestIsTheSlowRule()
    {
        IBusinessRule fast = CreateRule("FAST", broken: false);
        IBusinessRule slow = CreateSlowRule("SLOW", SlowRuleDuration);

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { fast, slow }, new RuleEvaluationOptions());

        Assert.Equal("SLOW", result.Slowest!.Code);
        Assert.True(result.Slowest.Duration >= TimeSpan.FromMilliseconds(5));
    }

    [Fact]
    public void EvaluateAll_LegacyOverload_AlsoRecordsLedger()
    {
        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(CreateRule("A", broken: false), CreateRule("B", broken: true));

        Assert.Equal(2, result.Entries.Count);
        Assert.Equal(RuleOutcome.Broken, result.Entries[1].Outcome);
    }

    [Fact]
    public void EvaluateAll_WithOptions_ComposedRule_SurfacesMemberDetails()
    {
        IBusinessRule group = RuleGroup.All("GROUP", "group", CreateRule("M1", broken: true), CreateRule("M2", broken: false));

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { group }, new RuleEvaluationOptions());

        RuleEvaluationEntry entry = Assert.Single(result.Entries);
        Assert.Equal(RuleOutcome.Broken, entry.Outcome);
        Assert.Equal("M1", Assert.Single(entry.Result!.Details).Code);
    }

    // ---------------------------------------------------------------------
    // Context forwarding
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_WithOptionsAndContext_ForwardsContextToRules()
    {
        IBusinessRule contextRule = BusinessRuleBuilder.Create("CTX").WithMessage("ctx").When(ctx => ctx.Get<bool>(FlagKey)).Build();
        BusinessRuleContext context = RuleTestHelper.CreateContext((FlagKey, true));

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { contextRule }, context, new RuleEvaluationOptions());

        Assert.True(result.HasBrokenRules);
    }

    [Fact]
    public void EvaluateAll_WithOptionsAndNullContext_UsesParameterlessOverload()
    {
        ContextRecordingRule rule = new ContextRecordingRule();

        BusinessRuleChecker.EvaluateAll(new IBusinessRule[] { rule }, (BusinessRuleContext?)null, new RuleEvaluationOptions());

        Assert.False(rule.ContextOverloadCalled);
        Assert.True(rule.ParameterlessOverloadCalled);
    }

    [Fact]
    public void EvaluateAll_WithOptionsAndContext_UsesContextOverload()
    {
        ContextRecordingRule rule = new ContextRecordingRule();

        BusinessRuleChecker.EvaluateAll(new IBusinessRule[] { rule }, new BusinessRuleContext(), new RuleEvaluationOptions());

        Assert.True(rule.ContextOverloadCalled);
    }

    // ---------------------------------------------------------------------
    // Fault policy
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_FaultPolicyThrow_PropagatesTheException()
    {
        IBusinessRule[] rules = { CreateThrowingRule(ThrowingCode), CreateRule(PassingCode, broken: false) };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => BusinessRuleChecker.EvaluateAll(rules, new RuleEvaluationOptions { OnFault = FaultPolicy.Throw }));

        Assert.Equal(Boom, exception.Message);
    }

    [Fact]
    public void EvaluateAll_FaultPolicyThrow_RaisesOnRuleFaultedButNotOnBatchCompleted()
    {
        RecordingObserver observer = new RecordingObserver();
        IBusinessRule throwing = CreateThrowingRule(ThrowingCode);

        Assert.Throws<InvalidOperationException>(
            () => BusinessRuleChecker.EvaluateAll(new[] { throwing }, new RuleEvaluationOptions { Observer = observer }));

        Assert.Same(throwing, Assert.Single(observer.Faulted).rule);
        Assert.IsType<InvalidOperationException>(observer.Faulted[0].exception);
        Assert.Null(observer.CompletedResult);
        Assert.Equal(0, observer.AfterCount);
    }

    [Fact]
    public void EvaluateAll_FaultPolicyRecord_RecordsFaultAndContinues()
    {
        IBusinessRule throwing = CreateThrowingRule(ThrowingCode);
        IBusinessRule broken = CreateRule(BrokenCode, broken: true);
        IBusinessRule passing = CreateRule(PassingCode, broken: false);

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { throwing, broken, passing }, new RuleEvaluationOptions { OnFault = FaultPolicy.Record });

        Assert.Equal(3, result.Entries.Count);
        RuleEvaluationEntry faulted = result.Entries[0];
        Assert.Equal(RuleOutcome.Faulted, faulted.Outcome);
        Assert.IsType<InvalidOperationException>(faulted.Fault);
        Assert.Null(faulted.Result);
        Assert.True(faulted.IsFaulted);
        Assert.True(result.HasFaults);
        Assert.Same(faulted, Assert.Single(result.Faulted));
        Assert.Equal(BrokenCode, Assert.Single(result.BrokenRules).Code);
        Assert.Same(passing, Assert.Single(result.PassedRules));
    }

    [Fact]
    public void EvaluateAll_FaultPolicyRecord_RaisesOnRuleFaultedAndSkipsAfterEvaluate()
    {
        RecordingObserver observer = new RecordingObserver();
        IBusinessRule throwing = CreateThrowingRule(ThrowingCode);

        BusinessRuleChecker.EvaluateAll(new[] { throwing }, new RuleEvaluationOptions { OnFault = FaultPolicy.Record, Observer = observer });

        Assert.Single(observer.Faulted);
        Assert.Equal(0, observer.AfterCount);
        Assert.Equal(0, observer.ElapsedCount);
        Assert.Equal(0, observer.BrokenCount);
        Assert.NotNull(observer.CompletedResult);
    }

    [Fact]
    public void EvaluateAll_FaultPolicyTreatAsBroken_AddsResultWithRuleMetadata()
    {
        IBusinessRule throwing = BusinessRuleBuilder.Create(ThrowingCode)
            .WithMessage("Throwing rule")
            .WithSeverity(RuleSeverity.Warning)
            .When(() => throw new InvalidOperationException(Boom))
            .Build();

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { throwing }, new RuleEvaluationOptions { OnFault = FaultPolicy.TreatAsBroken });

        RuleEvaluationEntry entry = Assert.Single(result.Entries);
        Assert.Equal(RuleOutcome.Faulted, entry.Outcome);
        Assert.True(entry.IsFaulted);
        Assert.True(entry.IsBroken);
        BusinessRuleResult brokenResult = Assert.Single(result.BrokenRules);
        Assert.Same(entry.Result, brokenResult);
        Assert.Equal(ThrowingCode, brokenResult.Code);
        Assert.Equal("Throwing rule", brokenResult.Message);
        Assert.Equal(RuleSeverity.Warning, brokenResult.Severity);
        Assert.True(result.HasFaults);
        Assert.Empty(result.PassedRules);
    }

    [Fact]
    public void EvaluateAll_FaultPolicyTreatAsBroken_RaisesOnRuleBroken()
    {
        RecordingObserver observer = new RecordingObserver();

        BusinessRuleChecker.EvaluateAll(new[] { CreateThrowingRule(ThrowingCode) }, new RuleEvaluationOptions { OnFault = FaultPolicy.TreatAsBroken, Observer = observer });

        Assert.Single(observer.Faulted);
        Assert.Equal(1, observer.BrokenCount);
        Assert.Equal(ThrowingCode, observer.LastBrokenResult!.Code);
    }

    [Fact]
    public void EvaluateAll_FaultPolicyTreatAsBroken_ThrowingMessageGetter_FallsBackWithoutMaskingTheFault()
    {
        ThrowingMessageRule rule = new ThrowingMessageRule();

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new IBusinessRule[] { rule }, new RuleEvaluationOptions { OnFault = FaultPolicy.TreatAsBroken });

        RuleEvaluationEntry entry = Assert.Single(result.Entries);
        Assert.IsType<InvalidOperationException>(entry.Fault);
        Assert.Equal(Boom, entry.Fault!.Message);
        Assert.Equal(nameof(ThrowingMessageRule), entry.Result!.Code);
        Assert.Contains("Rule evaluation faulted", entry.Result.Message);
        Assert.Contains(Boom, entry.Result.Message);
        Assert.Equal(RuleSeverity.Error, entry.Result.Severity);
    }

    [Theory]
    [InlineData(FaultPolicy.Record)]
    [InlineData(FaultPolicy.TreatAsBroken)]
    public void EvaluateAll_ThrowingCodeGetter_RecordsTheFaultWithoutMaskingIt(FaultPolicy policy)
    {
        ThrowingCodeRule rule = new ThrowingCodeRule();

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new IBusinessRule[] { rule }, new RuleEvaluationOptions { OnFault = policy });

        RuleEvaluationEntry entry = Assert.Single(result.Entries);
        Assert.Equal(RuleOutcome.Faulted, entry.Outcome);
        Assert.IsType<InvalidOperationException>(entry.Fault);
        Assert.Equal(Boom, entry.Fault!.Message);
        Assert.Throws<NotSupportedException>(() => entry.Code);
    }

    [Fact]
    public void EvaluateAll_FaultPolicyTreatAsBroken_ThrowingCodeGetter_FallsBackToTypeName()
    {
        ThrowingCodeRule rule = new ThrowingCodeRule();

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new IBusinessRule[] { rule }, new RuleEvaluationOptions { OnFault = FaultPolicy.TreatAsBroken });

        Assert.Equal(nameof(ThrowingCodeRule), Assert.Single(result.BrokenRules).Code);
    }

    [Theory]
    [InlineData(FaultPolicy.Throw)]
    [InlineData(FaultPolicy.Record)]
    [InlineData(FaultPolicy.TreatAsBroken)]
    public void EvaluateAll_RuleThrowsOperationCanceled_AlwaysPropagates(FaultPolicy policy)
    {
        IBusinessRule cancelling = BusinessRuleBuilder.Create("CANCEL").WithMessage("cancel").When(() => throw new OperationCanceledException()).Build();

        Assert.Throws<OperationCanceledException>(
            () => BusinessRuleChecker.EvaluateAll(new[] { cancelling }, new RuleEvaluationOptions { OnFault = policy }));
    }

    // ---------------------------------------------------------------------
    // Fail-fast
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_StopOnFirstFailure_MarksRemainingRulesNotEvaluated()
    {
        int thirdEvaluations = 0;
        IBusinessRule first = CreateRule("A", broken: false);
        IBusinessRule second = CreateRule("B", broken: true);
        IBusinessRule third = BusinessRuleBuilder.Create("C").WithMessage("c").When(() => { thirdEvaluations++; return false; }).Build();

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { first, second, third }, new RuleEvaluationOptions { StopOnFirstFailure = true });

        Assert.Equal(3, result.Entries.Count);
        Assert.Equal(RuleOutcome.NotEvaluated, result.Entries[2].Outcome);
        Assert.Equal(TimeSpan.Zero, result.Entries[2].Duration);
        Assert.Null(result.Entries[2].Result);
        Assert.Equal(2, result.Entries[2].Order);
        Assert.Equal(0, thirdEvaluations);
        Assert.Same(first, Assert.Single(result.PassedRules));
        Assert.Single(result.BrokenRules);
    }

    [Fact]
    public void EvaluateAll_StopOnFirstFailure_StopsAtAnySeverityBecauseThrowOnIsIgnored()
    {
        int secondEvaluations = 0;
        IBusinessRule warning = CreateRule("WARN", broken: true, RuleSeverity.Warning);
        IBusinessRule error = BusinessRuleBuilder.Create("ERR").WithMessage("err").When(() => { secondEvaluations++; return true; }).Build();

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { warning, error }, new RuleEvaluationOptions { StopOnFirstFailure = true, ThrowOn = RuleSeverity.Error });

        Assert.Equal(0, secondEvaluations);
        Assert.Equal(RuleOutcome.NotEvaluated, result.Entries[1].Outcome);
    }

    [Fact]
    public void EvaluateAll_WithOptions_NeverThrowsForBrokenRulesRegardlessOfThrowOn()
    {
        IBusinessRule error = CreateRule("ERR", broken: true, RuleSeverity.Error);

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { error }, new RuleEvaluationOptions { ThrowOn = RuleSeverity.Error });

        Assert.True(result.HasErrors);
    }

    // ---------------------------------------------------------------------
    // Observers from EvaluateAll
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_WithOptions_FiresEveryObserverCallback()
    {
        RecordingObserver observer = new RecordingObserver();
        IBusinessRule[] rules = { CreateRule("A", broken: true), CreateRule("B", broken: false), CreateRule("C", broken: false) };

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rules, new RuleEvaluationOptions { Observer = observer });

        Assert.Equal(3, observer.BatchStartedCount);
        Assert.Equal(3, observer.BeforeCount);
        Assert.Equal(3, observer.AfterCount);
        Assert.Equal(3, observer.ElapsedCount);
        Assert.Equal(1, observer.BrokenCount);
        Assert.Same(result, observer.CompletedResult);
        Assert.All(observer.Elapsed, elapsed => Assert.True(elapsed >= TimeSpan.Zero));
    }

    [Fact]
    public void EvaluateAll_WithOptions_ObserverCallbacksArriveInDocumentedOrder()
    {
        RecordingObserver observer = new RecordingObserver();
        IBusinessRule[] rules = { CreateRule("A", broken: true), CreateRule("B", broken: false) };

        BusinessRuleChecker.EvaluateAll(rules, new RuleEvaluationOptions { Observer = observer });

        Assert.Equal(
            new[] { "started:2", "before:A", "after:A", "elapsed:A", "broken:A", "before:B", "after:B", "elapsed:B", "completed" },
            observer.Sequence);
    }

    [Fact]
    public void EvaluateAll_WithOptions_ObserverElapsedMatchesLedgerDuration()
    {
        RecordingObserver observer = new RecordingObserver();

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { CreateSlowRule("SLOW", TimeSpan.FromMilliseconds(2)) }, new RuleEvaluationOptions { Observer = observer });

        Assert.Equal(result.Entries[0].Duration, Assert.Single(observer.Elapsed));
    }

    [Fact]
    public void EvaluateAll_WithOptions_ReportsPassedRuleWithIsBrokenFalse()
    {
        RecordingObserver observer = new RecordingObserver();

        BusinessRuleChecker.EvaluateAll(new[] { CreateRule("A", broken: false) }, new RuleEvaluationOptions { Observer = observer });

        Assert.False(Assert.Single(observer.IsBrokenFlags));
    }

    [Fact]
    public void CheckAll_LegacyObserverOverload_ReceivesBatchHooks()
    {
        RecordingObserver observer = new RecordingObserver();

        BusinessRuleChecker.CheckAll(new[] { CreateRule("A", broken: false) }, observer: observer);

        Assert.Equal(1, observer.BatchStartedCount);
        Assert.NotNull(observer.CompletedResult);
        Assert.Equal(1, observer.ElapsedCount);
    }

    // ---------------------------------------------------------------------
    // CheckAll with ThrowOn
    // ---------------------------------------------------------------------

    [Fact]
    public void CheckAll_ThrowOnError_ThrowsErrorsAndCollectsLowerSeverities()
    {
        IBusinessRule[] rules =
        {
            CreateRule("WARN", broken: true, RuleSeverity.Warning),
            CreateRule("ERR", broken: true, RuleSeverity.Error),
            CreateRule("INFO", broken: true, RuleSeverity.Info)
        };

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckAll(rules, new RuleEvaluationOptions { ThrowOn = RuleSeverity.Error }));

        Assert.Equal("ERR", Assert.Single(exception.BrokenRules).Code);
        Assert.Equal(new[] { "WARN", "INFO" }, exception.CollectedRules.Select(r => r.Code));
        Assert.Equal(3, exception.Entries.Count);
    }

    [Fact]
    public void CheckAll_ThrowOnError_OnlyWarningsBroken_DoesNotThrow()
    {
        IBusinessRule[] rules = { CreateRule("WARN", broken: true, RuleSeverity.Warning), CreateRule("INFO", broken: true, RuleSeverity.Info) };

        BusinessRuleChecker.CheckAll(rules, new RuleEvaluationOptions { ThrowOn = RuleSeverity.Error });
    }

    [Fact]
    public void CheckAll_ThrowOnWarning_ThrowsWarningsAndErrorsCollectsInfo()
    {
        IBusinessRule[] rules =
        {
            CreateRule("INFO", broken: true, RuleSeverity.Info),
            CreateRule("WARN", broken: true, RuleSeverity.Warning),
            CreateRule("ERR", broken: true, RuleSeverity.Error)
        };

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckAll(rules, (BusinessRuleContext?)null, new RuleEvaluationOptions { ThrowOn = RuleSeverity.Warning }));

        Assert.Equal(new[] { "WARN", "ERR" }, exception.BrokenRules.Select(r => r.Code));
        Assert.Equal("INFO", Assert.Single(exception.CollectedRules).Code);
    }

    [Fact]
    public void CheckAll_DefaultThrowOn_ThrowsForInfoWithNothingCollected()
    {
        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckAll(new[] { CreateRule("INFO", broken: true, RuleSeverity.Info) }, new RuleEvaluationOptions()));

        Assert.Single(exception.BrokenRules);
        Assert.Empty(exception.CollectedRules);
        Assert.Single(exception.Entries);
    }

    [Fact]
    public void CheckAll_WithOptions_NothingBroken_DoesNotThrow()
    {
        BusinessRuleChecker.CheckAll(new[] { CreateRule(PassingCode, broken: false) }, new RuleEvaluationOptions());
    }

    [Fact]
    public void CheckAll_StopOnFirstFailureWithThrowOnError_DoesNotStopAtAWarning()
    {
        IBusinessRule[] rules = { CreateRule("WARN", broken: true, RuleSeverity.Warning), CreateRule("ERR", broken: true, RuleSeverity.Error) };

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckAll(rules, new RuleEvaluationOptions { StopOnFirstFailure = true, ThrowOn = RuleSeverity.Error }));

        Assert.Equal("ERR", Assert.Single(exception.BrokenRules).Code);
        Assert.Equal("WARN", Assert.Single(exception.CollectedRules).Code);
        Assert.Equal(new[] { RuleOutcome.Broken, RuleOutcome.Broken }, exception.Entries.Select(e => e.Outcome));
    }

    [Fact]
    public void CheckAll_StopOnFirstFailureWithThrowOnError_StopsAtTheFirstError()
    {
        int secondEvaluations = 0;
        IBusinessRule error = CreateRule("ERR", broken: true, RuleSeverity.Error);
        IBusinessRule counting = BusinessRuleBuilder.Create("COUNT").WithMessage("count").When(() => { secondEvaluations++; return true; }).Build();

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckAll(new[] { error, counting }, new RuleEvaluationOptions { StopOnFirstFailure = true, ThrowOn = RuleSeverity.Error }));

        Assert.Equal(0, secondEvaluations);
        Assert.Equal(RuleOutcome.NotEvaluated, exception.Entries[1].Outcome);
        Assert.Single(exception.BrokenRules);
    }

    [Fact]
    public void CheckAll_WithOptions_RaisesOnBatchCompletedBeforeThrowing()
    {
        RecordingObserver observer = new RecordingObserver();

        Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckAll(new[] { CreateRule(BrokenCode, broken: true) }, new RuleEvaluationOptions { Observer = observer }));

        Assert.NotNull(observer.CompletedResult);
        Assert.Equal("completed", observer.Sequence.Last());
    }

    [Fact]
    public void CheckAll_FaultPolicyRecord_NoBrokenRules_ReturnsNormallyAndObserverSeesTheFault()
    {
        RecordingObserver observer = new RecordingObserver();
        IBusinessRule[] rules = { CreateThrowingRule(ThrowingCode), CreateRule(PassingCode, broken: false) };

        BusinessRuleChecker.CheckAll(rules, new RuleEvaluationOptions { OnFault = FaultPolicy.Record, Observer = observer });

        Assert.Single(observer.Faulted);
        Assert.True(observer.CompletedResult!.HasFaults);
    }

    [Fact]
    public void CheckAll_FaultPolicyRecord_WithBrokenRule_ExceptionCarriesTheFaultInEntries()
    {
        IBusinessRule[] rules = { CreateThrowingRule(ThrowingCode), CreateRule(BrokenCode, broken: true) };

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckAll(rules, new RuleEvaluationOptions { OnFault = FaultPolicy.Record }));

        Assert.Equal(BrokenCode, Assert.Single(exception.BrokenRules).Code);
        Assert.Equal(RuleOutcome.Faulted, exception.Entries[0].Outcome);
        Assert.IsType<InvalidOperationException>(exception.Entries[0].Fault);
    }

    [Fact]
    public void CheckAll_FaultPolicyTreatAsBroken_ThrowsWithSynthesisedResult()
    {
        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckAll(new[] { CreateThrowingRule(ThrowingCode) }, new RuleEvaluationOptions { OnFault = FaultPolicy.TreatAsBroken }));

        Assert.Equal(ThrowingCode, Assert.Single(exception.BrokenRules).Code);
        Assert.True(Assert.Single(exception.Entries).IsFaulted);
    }

    [Fact]
    public void CheckAll_FaultPolicyThrow_PropagatesOriginalException()
    {
        Assert.Throws<InvalidOperationException>(
            () => BusinessRuleChecker.CheckAll(new[] { CreateThrowingRule(ThrowingCode) }, new RuleEvaluationOptions()));
    }

    // ---------------------------------------------------------------------
    // Guards
    // ---------------------------------------------------------------------

    [Fact]
    public void CheckAll_NullRules_ThrowsArgumentNullException()
    {
        IEnumerable<IBusinessRule> rules = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => BusinessRuleChecker.CheckAll(rules, new RuleEvaluationOptions()));
    }

    [Fact]
    public void CheckAll_NullOptions_ThrowsArgumentNullException()
    {
        RuleEvaluationOptions options = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => BusinessRuleChecker.CheckAll(Array.Empty<IBusinessRule>(), options));
    }

    [Fact]
    public void EvaluateAll_NullRules_ThrowsArgumentNullException()
    {
        IEnumerable<IBusinessRule> rules = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => BusinessRuleChecker.EvaluateAll(rules, new RuleEvaluationOptions()));
    }

    [Fact]
    public void EvaluateAll_NullOptions_ThrowsArgumentNullException()
    {
        RuleEvaluationOptions options = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => BusinessRuleChecker.EvaluateAll(Array.Empty<IBusinessRule>(), options));
    }

    [Fact]
    public void EvaluateAll_NullRuleInsideBatch_ThrowsArgumentException()
    {
        IBusinessRule[] rules = { CreateRule("A", broken: false), null! }; // intentional: exercising the null guard

        Assert.Throws<ArgumentException>(() => BusinessRuleChecker.EvaluateAll(rules, new RuleEvaluationOptions()));
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static IBusinessRule CreateRule(string code, bool broken, RuleSeverity severity = RuleSeverity.Error)
    {
        return BusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .WithSeverity(severity)
            .When(() => broken)
            .Build();
    }

    private static IBusinessRule CreateThrowingRule(string code)
    {
        return BusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .When(() => throw new InvalidOperationException(Boom))
            .Build();
    }

    private static IBusinessRule CreateSlowRule(string code, TimeSpan duration)
    {
        return BusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .When(() =>
            {
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                while (System.Diagnostics.Stopwatch.GetElapsedTime(start) < duration)
                {
                    Thread.SpinWait(50);
                }

                return false;
            })
            .Build();
    }

    private sealed class ContextRecordingRule : BaseBusinessRule
    {
        public bool ParameterlessOverloadCalled { get; private set; }

        public bool ContextOverloadCalled { get; private set; }

        public override string Code => "CTX.RECORDING";

        public override string Message => "records which overload ran";

        public override bool IsBroken()
        {
            ParameterlessOverloadCalled = true;
            return false;
        }

        public override bool IsBroken(BusinessRuleContext context)
        {
            ContextOverloadCalled = true;
            return false;
        }
    }

    private sealed class ThrowingMessageRule : IBusinessRule
    {
        public string Code => "THROWING.MESSAGE";

        public string Message => throw new NotSupportedException("message getter throws");

        public bool IsBroken() => throw new InvalidOperationException(Boom);

        public bool IsBroken(BusinessRuleContext context) => IsBroken();

        public void Check() => IsBroken();

        public void Check(BusinessRuleContext context) => IsBroken();

        public BusinessRuleResult Evaluate() => new BusinessRuleResult(Code, Message, RuleSeverity.Error);
    }

    private sealed class ThrowingCodeRule : IBusinessRule
    {
        public string Code => throw new NotSupportedException("code getter throws");

        public string Message => "code getter throws";

        public bool IsBroken() => throw new InvalidOperationException(Boom);

        public bool IsBroken(BusinessRuleContext context) => IsBroken();

        public void Check() => IsBroken();

        public void Check(BusinessRuleContext context) => IsBroken();

        public BusinessRuleResult Evaluate() => new BusinessRuleResult(Code, Message, RuleSeverity.Error);
    }

    private sealed class RecordingObserver : IRuleExecutionObserver
    {
        public List<string> Sequence { get; } = new List<string>();

        public List<(IBusinessRule rule, Exception exception)> Faulted { get; } = new List<(IBusinessRule rule, Exception exception)>();

        public List<TimeSpan> Elapsed { get; } = new List<TimeSpan>();

        public List<bool> IsBrokenFlags { get; } = new List<bool>();

        public int BatchStartedCount { get; private set; }

        public int BeforeCount { get; private set; }

        public int AfterCount { get; private set; }

        public int ElapsedCount { get; private set; }

        public int BrokenCount { get; private set; }

        public BusinessRuleResult? LastBrokenResult { get; private set; }

        public RuleEvaluationResult? CompletedResult { get; private set; }

        public void OnBatchStarted(int ruleCount)
        {
            BatchStartedCount = ruleCount;
            Sequence.Add($"started:{ruleCount}");
        }

        public void OnBeforeEvaluate(IBusinessRule rule)
        {
            BeforeCount++;
            Sequence.Add($"before:{rule.Code}");
        }

        public void OnAfterEvaluate(IBusinessRule rule, BusinessRuleResult? result)
        {
            AfterCount++;
            Sequence.Add($"after:{rule.Code}");
        }

        public void OnAfterEvaluate(IBusinessRule rule, bool isBroken, TimeSpan elapsed)
        {
            ElapsedCount++;
            Elapsed.Add(elapsed);
            IsBrokenFlags.Add(isBroken);
            Sequence.Add($"elapsed:{rule.Code}");
        }

        public void OnRuleBroken(IBusinessRule rule, BusinessRuleResult result)
        {
            BrokenCount++;
            LastBrokenResult = result;
            Sequence.Add($"broken:{rule.Code}");
        }

        public void OnRuleFaulted(IBusinessRule rule, Exception exception)
        {
            Faulted.Add((rule, exception));
            Sequence.Add($"faulted:{rule.Code}");
        }

        public void OnBatchCompleted(RuleEvaluationResult result)
        {
            CompletedResult = result;
            Sequence.Add("completed");
        }
    }
}
