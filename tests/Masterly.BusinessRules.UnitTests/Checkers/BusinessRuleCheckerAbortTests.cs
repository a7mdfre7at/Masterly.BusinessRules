namespace Masterly.BusinessRules.UnitTests;

/// <summary>
/// The observer callbacks of a batch that ends by an exception (cancellation, a propagating fault, an invalid batch)
/// and of a shadow-mode rule whose inner rule throws.
/// </summary>
public class BusinessRuleCheckerAbortTests
{
    private const string PassingCode = "PASS";
    private const string CancelledCode = "CANCELLED";
    private const string ThrowingCode = "THROWS";
    private const string ShadowCode = "SHADOW.RULE";
    private const string Flag = "shadow-flag";

    // ---------------------------------------------------------------------
    // Cancellation
    // ---------------------------------------------------------------------

    [Fact]
    public void CheckAll_RuleCancelled_RaisesOnRuleCancelledAndOnBatchAbortedOnly()
    {
        OperationCanceledException cancellation = new OperationCanceledException();
        RecordingObserver observer = new RecordingObserver();
        IBusinessRule[] rules = { RuleTestHelper.CreatePassingRule(PassingCode), Throwing(CancelledCode, cancellation) };

        OperationCanceledException thrown = Assert.Throws<OperationCanceledException>(
            () => BusinessRuleChecker.CheckAll(rules, new BusinessRuleContext(), new RuleEvaluationOptions { Observer = observer, OnFault = FaultPolicy.Record }));

        Assert.Same(cancellation, thrown);
        Assert.Equal(
            new[] { "batch-started:2", "before:PASS", "after:PASS", "before:CANCELLED", "cancelled:CANCELLED", "aborted:OperationCanceledException" },
            observer.Calls);
        Assert.Same(cancellation, observer.CancelledWith);
        Assert.Same(cancellation, observer.AbortedWith);
    }

    [Fact]
    public void EvaluateAll_RuleCancelledUnderTreatAsBroken_IsNeverReportedAsAFault()
    {
        RecordingObserver observer = new RecordingObserver();

        Assert.Throws<OperationCanceledException>(
            () => BusinessRuleChecker.EvaluateAll(new[] { Throwing(CancelledCode, new OperationCanceledException()) }, new BusinessRuleContext(), new RuleEvaluationOptions { Observer = observer, OnFault = FaultPolicy.TreatAsBroken }));

        Assert.DoesNotContain(observer.Calls, call => call.StartsWith("faulted:", StringComparison.Ordinal) || call.StartsWith("broken:", StringComparison.Ordinal));
        Assert.DoesNotContain("completed", observer.Calls);
    }

    [Fact]
    public void CheckAll_ObserverWithoutTheNewHooks_StillPropagatesTheCancellation()
    {
        MinimalObserver observer = new MinimalObserver();

        Assert.Throws<OperationCanceledException>(
            () => BusinessRuleChecker.CheckAll(new[] { Throwing(CancelledCode, new OperationCanceledException()) }, new RuleEvaluationOptions { Observer = observer }));

        Assert.Equal(1, observer.BeforeCount);
    }

    // ---------------------------------------------------------------------
    // Faults and invalid batches
    // ---------------------------------------------------------------------

    [Fact]
    public void CheckAll_FaultUnderThrow_RaisesOnRuleFaultedThenOnBatchAborted()
    {
        InvalidOperationException fault = new InvalidOperationException("boom");
        RecordingObserver observer = new RecordingObserver();

        Assert.Throws<InvalidOperationException>(() => BusinessRuleChecker.CheckAll(new[] { Throwing(ThrowingCode, fault) }, new RuleEvaluationOptions { Observer = observer }));

        Assert.Equal(new[] { "batch-started:1", "before:THROWS", "faulted:THROWS", "aborted:InvalidOperationException" }, observer.Calls);
        Assert.Same(fault, observer.AbortedWith);
    }

    [Fact]
    public void EvaluateAll_FaultUnderRecord_CompletesTheBatchWithoutAborting()
    {
        RecordingObserver observer = new RecordingObserver();

        BusinessRuleChecker.EvaluateAll(new[] { Throwing(ThrowingCode, new InvalidOperationException("boom")) }, new RuleEvaluationOptions { Observer = observer, OnFault = FaultPolicy.Record });

        Assert.Equal("completed", observer.Calls.Last());
        Assert.Null(observer.AbortedWith);
    }

    [Fact]
    public void EvaluateAll_NullRuleInTheBatch_RaisesOnBatchAbortedWithTheArgumentException()
    {
        RecordingObserver observer = new RecordingObserver();
        IBusinessRule[] rules = { RuleTestHelper.CreatePassingRule(PassingCode), null! };

        ArgumentException thrown = Assert.Throws<ArgumentException>(() => BusinessRuleChecker.EvaluateAll(rules, new RuleEvaluationOptions { Observer = observer }));

        Assert.Same(thrown, observer.AbortedWith);
        Assert.Equal("rules", thrown.ParamName);
    }

    [Fact]
    public void CheckAll_BrokenRules_CompletesTheBatchBeforeThrowingAndNeverAborts()
    {
        RecordingObserver observer = new RecordingObserver();

        Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.CheckAll(new[] { RuleTestHelper.CreateBrokenRule() }, new RuleEvaluationOptions { Observer = observer }));

        Assert.Equal("completed", observer.Calls.Last());
        Assert.Null(observer.AbortedWith);
    }

    // ---------------------------------------------------------------------
    // Shadow mode never blocks, even when the inner rule throws
    // ---------------------------------------------------------------------

    [Fact]
    public void CheckAll_ShadowRuleThrows_DoesNotThrowAndCollectsAStandInShadowResult()
    {
        InvalidOperationException fault = new InvalidOperationException("dark launch bug");
        IBusinessRule shadow = Shadow(Throwing(ShadowCode, fault));

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { shadow, RuleTestHelper.CreatePassingRule(PassingCode) }, new BusinessRuleContext(), new RuleEvaluationOptions());

        Assert.True(result.AllPassed);
        Assert.Equal(ShadowCode, Assert.Single(result.ShadowResults).Code);
        Assert.Equal(RuleOutcome.Passed, result.Entries[0].Outcome);
        Assert.Empty(result.Faulted);
    }

    [Fact]
    public void CheckAll_ShadowRuleThrowsUnderTheDefaultFaultPolicy_DoesNotThrow()
    {
        IBusinessRule shadow = Shadow(Throwing(ShadowCode, new InvalidOperationException("dark launch bug")));

        Exception? exception = Record.Exception(() => BusinessRuleChecker.CheckAll(new BusinessRuleContext(), shadow));

        Assert.Null(exception);
    }

    [Fact]
    public void EvaluateAll_ShadowRuleThrows_RaisesOnShadowViolationThenOnShadowFault()
    {
        InvalidOperationException fault = new InvalidOperationException("dark launch bug");
        RecordingObserver observer = new RecordingObserver();

        BusinessRuleChecker.EvaluateAll(new[] { Shadow(Throwing(ShadowCode, fault)) }, new BusinessRuleContext(), new RuleEvaluationOptions { Observer = observer });

        Assert.Equal(new[] { "batch-started:1", "before:SHADOW.RULE", "after:SHADOW.RULE", "shadow:SHADOW.RULE", "shadow-fault:SHADOW.RULE", "completed" }, observer.Calls);
        Assert.Same(fault, observer.ShadowFault);
    }

    [Fact]
    public void EvaluateAll_ShadowRuleBroken_RaisesOnlyOnShadowViolation()
    {
        RecordingObserver observer = new RecordingObserver();

        BusinessRuleChecker.EvaluateAll(new[] { Shadow(RuleTestHelper.CreateBrokenRule(ShadowCode)) }, new BusinessRuleContext(), new RuleEvaluationOptions { Observer = observer });

        Assert.Contains("shadow:SHADOW.RULE", observer.Calls);
        Assert.DoesNotContain("shadow-fault:SHADOW.RULE", observer.Calls);
    }

    [Fact]
    public void IsBroken_ShadowRuleThrowsOutsideABatch_PassesWithoutThrowing()
    {
        IBusinessRule shadow = Shadow(Throwing(ShadowCode, new InvalidOperationException("dark launch bug")));

        bool isBroken = shadow.IsBroken(new BusinessRuleContext());

        Assert.False(isBroken);
    }

    [Fact]
    public void IsBroken_ShadowRuleCancelled_PropagatesTheCancellation()
    {
        IBusinessRule shadow = Shadow(Throwing(ShadowCode, new OperationCanceledException()));

        Assert.Throws<OperationCanceledException>(() => shadow.IsBroken(new BusinessRuleContext()));
    }

    [Fact]
    public void IsBroken_EnforcedRuleThrows_PropagatesTheFault()
    {
        IBusinessRule enforced = Throwing(ShadowCode, new InvalidOperationException("boom")).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        Assert.Throws<InvalidOperationException>(() => enforced.IsBroken(new BusinessRuleContext()));
    }

    [Fact]
    public void Explain_ShadowRuleThrows_RecordsAFaultedInnerNodeUnderAPassingShadowNode()
    {
        IBusinessRule shadow = Shadow(Throwing(ShadowCode, new InvalidOperationException("dark launch bug")));

        RuleExplanation explanation = shadow.Explain(new BusinessRuleContext());

        Assert.Equal(RuleOutcome.Passed, explanation.Outcome);
        Assert.Equal("SHADOW", explanation.Operator);
        RuleExplanation inner = Assert.Single(explanation.Children);
        Assert.Equal(RuleOutcome.Faulted, inner.Outcome);
        Assert.IsType<InvalidOperationException>(inner.Fault);
    }

    // ---------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------

    private static IBusinessRule Throwing(string code, Exception exception)
    {
        return BusinessRuleBuilder.Create(code).WithMessage($"{code} message").When(() => throw exception).Build();
    }

    private static IBusinessRule Shadow(IBusinessRule inner)
    {
        return inner.GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));
    }

    private sealed class RecordingObserver : IRuleExecutionObserver
    {
        public List<string> Calls { get; } = new List<string>();

        public OperationCanceledException? CancelledWith { get; private set; }

        public Exception? AbortedWith { get; private set; }

        public Exception? ShadowFault { get; private set; }

        public void OnBeforeEvaluate(IBusinessRule rule) => Calls.Add($"before:{rule.Code}");

        public void OnAfterEvaluate(IBusinessRule rule, BusinessRuleResult? result) => Calls.Add($"after:{rule.Code}");

        public void OnRuleBroken(IBusinessRule rule, BusinessRuleResult result) => Calls.Add($"broken:{rule.Code}");

        public void OnBatchStarted(int ruleCount) => Calls.Add($"batch-started:{ruleCount}");

        public void OnRuleFaulted(IBusinessRule rule, Exception exception) => Calls.Add($"faulted:{rule.Code}");

        public void OnBatchCompleted(RuleEvaluationResult result) => Calls.Add("completed");

        public void OnShadowViolation(IBusinessRule rule, BusinessRuleResult result) => Calls.Add($"shadow:{rule.Code}");

        public void OnRuleCancelled(IBusinessRule rule, OperationCanceledException exception)
        {
            Calls.Add($"cancelled:{rule.Code}");
            CancelledWith = exception;
        }

        public void OnBatchAborted(Exception exception)
        {
            Calls.Add($"aborted:{exception.GetType().Name}");
            AbortedWith = exception;
        }

        public void OnShadowFault(IBusinessRule rule, Exception exception)
        {
            Calls.Add($"shadow-fault:{rule.Code}");
            ShadowFault = exception;
        }
    }

    private sealed class MinimalObserver : IRuleExecutionObserver
    {
        public int BeforeCount { get; private set; }

        public void OnBeforeEvaluate(IBusinessRule rule) => BeforeCount++;

        public void OnAfterEvaluate(IBusinessRule rule, BusinessRuleResult? result)
        {
        }

        public void OnRuleBroken(IBusinessRule rule, BusinessRuleResult result)
        {
        }
    }
}
