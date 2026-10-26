namespace Masterly.BusinessRules.UnitTests;

public class BusinessRuleCheckerShadowTests
{
    private const string ShadowCode = "SHADOW.RULE";
    private const string ShadowMessage = "Shadow rule would have failed";
    private const string PassingCode = "PASS";
    private const string BrokenCode = "BROKEN";
    private const string Flag = "credit-score-v2";
    private const string OtherFlag = "sanctions-check";

    // ---------------------------------------------------------------------
    // ShadowResults
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_WithOptions_ShadowViolation_IsCollectedNotBroken()
    {
        IBusinessRule shadow = CreateShadowRule();
        IBusinessRule passing = RuleTestHelper.CreatePassingRule(PassingCode);

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { shadow, passing }, new BusinessRuleContext(), new RuleEvaluationOptions());

        BusinessRuleResult shadowResult = Assert.Single(result.ShadowResults);
        Assert.Equal(ShadowCode, shadowResult.Code);
        Assert.Equal(ShadowMessage, shadowResult.Message);
        Assert.True(result.HasShadowResults);
        Assert.Empty(result.BrokenRules);
        Assert.True(result.AllPassed);
        Assert.Equal(2, result.PassedRules.Count);
        Assert.Equal(RuleOutcome.Passed, result.Entries[0].Outcome);
        Assert.Null(result.Entries[0].Result);
    }

    [Fact]
    public void EvaluateAll_WithOptions_ShadowRulePassing_CollectsNothing()
    {
        IBusinessRule shadow = RuleTestHelper.CreatePassingRule(ShadowCode).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { shadow }, new BusinessRuleContext(), new RuleEvaluationOptions());

        Assert.Empty(result.ShadowResults);
        Assert.False(result.HasShadowResults);
    }

    [Fact]
    public void EvaluateAll_WithOptions_EnforcedRule_IsBrokenNotShadow()
    {
        IBusinessRule enforced = RuleTestHelper.CreateBrokenRule(ShadowCode, ShadowMessage).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { enforced }, new BusinessRuleContext(), new RuleEvaluationOptions());

        Assert.Equal(ShadowCode, Assert.Single(result.BrokenRules).Code);
        Assert.Empty(result.ShadowResults);
    }

    [Fact]
    public void EvaluateAll_WithOptions_SeveralShadowViolations_AreCollectedInOrder()
    {
        IBusinessRule first = RuleTestHelper.CreateBrokenRule("S1").GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));
        IBusinessRule second = RuleTestHelper.CreateBrokenRule(BrokenCode);
        IBusinessRule third = RuleTestHelper.CreateBrokenRule("S3").GatedBy(OtherFlag, RuleTestHelper.CreateFlagProvider(OtherFlag, RuleRolloutMode.Shadow));

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { first, second, third }, new BusinessRuleContext(), new RuleEvaluationOptions());

        Assert.Equal(new[] { "S1", "S3" }, result.ShadowResults.Select(r => r.Code));
        Assert.Equal(BrokenCode, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public void EvaluateAll_BooleanOverloadWithContext_CollectsShadowResults()
    {
        IBusinessRule shadow = CreateShadowRule();

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { shadow }, new BusinessRuleContext());

        Assert.Equal(ShadowCode, Assert.Single(result.ShadowResults).Code);
    }

    [Fact]
    public void EvaluateAll_WithoutContext_EvaluatesGatedRuleUngated()
    {
        IBusinessRule shadow = CreateShadowRule();

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(shadow);

        Assert.Equal(ShadowCode, Assert.Single(result.BrokenRules).Code);
        Assert.Empty(result.ShadowResults);
    }

    [Fact]
    public void EvaluateAll_WithOptions_NestedShadowRuleInGroup_IsCollected()
    {
        IBusinessRule shadow = CreateShadowRule();
        IBusinessRule group = RuleGroup.All("GROUP", "group", shadow, RuleTestHelper.CreatePassingRule(PassingCode));

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { group }, new BusinessRuleContext(), new RuleEvaluationOptions());

        Assert.Equal(ShadowCode, Assert.Single(result.ShadowResults).Code);
        Assert.True(result.AllPassed);
    }

    [Fact]
    public void EvaluateAll_WithOptions_ShadowInsideCachedDecorator_IsCollected()
    {
        IBusinessRule cachedShadow = CreateShadowRule().Cached(TimeSpan.FromMinutes(5));

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { cachedShadow }, new BusinessRuleContext(), new RuleEvaluationOptions());

        Assert.Equal(ShadowCode, Assert.Single(result.ShadowResults).Code);
    }

    [Fact]
    public void EvaluateAll_WithOptions_StopOnFirstFailure_ShadowViolationDoesNotStopTheBatch()
    {
        IBusinessRule shadow = CreateShadowRule();
        IBusinessRule broken = RuleTestHelper.CreateBrokenRule(BrokenCode);

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { shadow, broken }, new BusinessRuleContext(), new RuleEvaluationOptions { StopOnFirstFailure = true });

        Assert.Equal(RuleOutcome.Broken, result.Entries[1].Outcome);
        Assert.Single(result.ShadowResults);
    }

    // ---------------------------------------------------------------------
    // CheckAll
    // ---------------------------------------------------------------------

    [Fact]
    public void CheckAll_WithOptions_ShadowViolation_DoesNotThrow()
    {
        IBusinessRule shadow = CreateShadowRule();

        BusinessRuleChecker.CheckAll(new[] { shadow }, new BusinessRuleContext(), new RuleEvaluationOptions());
    }

    [Fact]
    public void CheckAll_WithContext_ShadowViolation_DoesNotThrow()
    {
        IBusinessRule shadow = CreateShadowRule();

        BusinessRuleChecker.CheckAll(new BusinessRuleContext(), shadow);
    }

    [Fact]
    public void CheckAll_WithOptions_ShadowViolation_ReachesOnBatchCompletedResult()
    {
        RecordingObserver observer = new RecordingObserver();
        IBusinessRule shadow = CreateShadowRule();

        BusinessRuleChecker.CheckAll(new[] { shadow }, new BusinessRuleContext(), new RuleEvaluationOptions { Observer = observer });

        Assert.NotNull(observer.Completed);
        Assert.Equal(ShadowCode, Assert.Single(observer.Completed!.ShadowResults).Code);
    }

    [Fact]
    public void CheckAll_WithContext_EnforcedViolation_Throws()
    {
        IBusinessRule enforced = RuleTestHelper.CreateBrokenRule(ShadowCode).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.CheckAll(new BusinessRuleContext(), enforced));
    }

    // ---------------------------------------------------------------------
    // Observer
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_WithObserver_ShadowViolation_RaisesOnShadowViolationWithGatedRule()
    {
        RecordingObserver observer = new RecordingObserver();
        FeatureGatedBusinessRule shadow = CreateShadowRule();

        BusinessRuleChecker.EvaluateAll(new[] { (IBusinessRule)shadow }, new BusinessRuleContext(), new RuleEvaluationOptions { Observer = observer });

        (IBusinessRule rule, BusinessRuleResult result) = Assert.Single(observer.ShadowViolations);
        Assert.Same(shadow, rule);
        Assert.Equal(ShadowCode, result.Code);
    }

    [Fact]
    public void EvaluateAll_WithObserver_ShadowViolation_IsReportedAsPassedByTheOtherCallbacks()
    {
        RecordingObserver observer = new RecordingObserver();
        IBusinessRule shadow = CreateShadowRule();

        BusinessRuleChecker.EvaluateAll(new[] { shadow }, new BusinessRuleContext(), new RuleEvaluationOptions { Observer = observer });

        Assert.Empty(observer.Broken);
        Assert.Null(Assert.Single(observer.AfterResults));
        Assert.False(Assert.Single(observer.AfterVerdicts));
    }

    [Fact]
    public void EvaluateAll_WithObserver_CallbackOrder_ShadowViolationFollowsTheRuleCallbacks()
    {
        RecordingObserver observer = new RecordingObserver();
        IBusinessRule shadow = CreateShadowRule();

        BusinessRuleChecker.EvaluateAll(new[] { shadow }, new BusinessRuleContext(), new RuleEvaluationOptions { Observer = observer });

        Assert.Equal(new[] { "BatchStarted", "Before", "AfterResult", "AfterVerdict", "Shadow", "BatchCompleted" }, observer.Sequence);
    }

    [Fact]
    public void EvaluateAll_WithObserver_NestedShadowRule_ReportsTheGatedRuleNotTheGroup()
    {
        RecordingObserver observer = new RecordingObserver();
        FeatureGatedBusinessRule shadow = CreateShadowRule();
        IBusinessRule group = RuleGroup.All("GROUP", "group", shadow);

        BusinessRuleChecker.EvaluateAll(new[] { group }, new BusinessRuleContext(), new RuleEvaluationOptions { Observer = observer });

        Assert.Same(shadow, Assert.Single(observer.ShadowViolations).Rule);
    }

    [Fact]
    public void EvaluateAll_WithObserver_AsyncShadowRuleThroughAdapter_IsWrappedForTheObserver()
    {
        RecordingObserver observer = new RecordingObserver();
        FeatureGatedAsyncBusinessRule asyncShadow = RuleTestHelper.CreateBrokenAsyncRule(ShadowCode).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));
        IBusinessRule adapted = new AsyncToSyncRuleAdapter(asyncShadow);

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { adapted }, new BusinessRuleContext(), new RuleEvaluationOptions { Observer = observer });

        Assert.Equal(ShadowCode, Assert.Single(result.ShadowResults).Code);
        AsyncToSyncRuleAdapter reported = Assert.IsType<AsyncToSyncRuleAdapter>(Assert.Single(observer.ShadowViolations).Rule);
        Assert.Same(asyncShadow, reported.InnerRule);
    }

    [Fact]
    public void EvaluateAll_WithObserverNotOverridingTheHook_DoesNotThrow()
    {
        MinimalObserver observer = new MinimalObserver();
        IBusinessRule shadow = CreateShadowRule();

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { shadow }, new BusinessRuleContext(), new RuleEvaluationOptions { Observer = observer });

        Assert.Single(result.ShadowResults);
        Assert.Equal(1, observer.BeforeCount);
    }

    // ---------------------------------------------------------------------
    // Context hygiene
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_WithOptions_LeavesTheContextStoreUntouched()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext(("customerId", "c-1"));
        IBusinessRule shadow = CreateShadowRule();

        BusinessRuleChecker.EvaluateAll(new[] { shadow }, context, new RuleEvaluationOptions());

        Assert.Equal(new[] { "customerId" }, context.Keys);
        Assert.Equal(1, context.Count);
    }

    [Fact]
    public void EvaluateAll_WhenAFaultPropagates_LeavesTheContextStoreUntouched()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        IBusinessRule throwing = BusinessRuleBuilder.Create("THROWS").WithMessage("throws").When(() => throw new InvalidOperationException("boom")).Build();

        Assert.Throws<InvalidOperationException>(() => BusinessRuleChecker.EvaluateAll(new[] { throwing }, context, new RuleEvaluationOptions()));

        Assert.Equal(0, context.Count);
    }

    [Fact]
    public void EvaluateAll_ShadowPlumbingNeverAppearsInTheContextStoreDuringTheBatch()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        int keysSeenDuringBatch = -1;
        IBusinessRule probe = BusinessRuleBuilder.Create("PROBE").WithMessage("probe").When(ctx => { keysSeenDuringBatch = ctx.Count; return false; }).Build();

        BusinessRuleChecker.EvaluateAll(new[] { probe, CreateShadowRule() }, context, new RuleEvaluationOptions());

        Assert.Equal(0, keysSeenDuringBatch);
        Assert.Equal(0, context.Count);
    }

    [Fact]
    public void EvaluateAll_MissingKeyInsideABatch_ListsOnlyTheCallersKeys()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext(("order", "order-1"));
        IBusinessRule reader = BusinessRuleBuilder.Create("READER").WithMessage("reader").When(ctx => ctx.Get<decimal>("limit") > 0).Build();

        BusinessRuleContextException exception = Assert.Throws<BusinessRuleContextException>(() => BusinessRuleChecker.EvaluateAll(new[] { CreateShadowRule(), reader }, context, new RuleEvaluationOptions()));

        Assert.Equal(new[] { "order" }, Assert.IsType<ContextKeyNotFoundException>(exception.InnerException).AvailableKeys);
    }

    [Fact]
    public void EvaluateAll_NestedCheckerCallOnTheSameContext_KeepsTheOuterSink()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        IBusinessRule innerBatch = BusinessRuleBuilder.Create("NESTED").WithMessage("nested")
            .When(ctx => BusinessRuleChecker.EvaluateAll(new[] { RuleTestHelper.CreatePassingRule(PassingCode) }, ctx, new RuleEvaluationOptions()).HasBrokenRules)
            .Build();
        IBusinessRule shadow = CreateShadowRule();

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { innerBatch, shadow }, context, new RuleEvaluationOptions());

        Assert.Equal(ShadowCode, Assert.Single(result.ShadowResults).Code);
        Assert.Equal(0, context.Count);
    }

    // ---------------------------------------------------------------------
    // Typed checker projection
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_TypedOverload_CollectsShadowResults()
    {
        IBusinessRule<string> typed = BusinessRuleBuilder<string>.Create(ShadowCode).WithMessage(ShadowMessage).When(subject => subject.Length > 0).Build();
        IBusinessRule shadow = typed.For("subject").GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { shadow }, new BusinessRuleContext(), new RuleEvaluationOptions());

        Assert.Equal(ShadowCode, Assert.Single(result.ShadowResults).Code);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static FeatureGatedBusinessRule CreateShadowRule()
    {
        return RuleTestHelper.CreateBrokenRule(ShadowCode, ShadowMessage).GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow));
    }

    private sealed class RecordingObserver : IRuleExecutionObserver
    {
        public List<string> Sequence { get; } = new List<string>();
        public List<IBusinessRule> Broken { get; } = new List<IBusinessRule>();
        public List<BusinessRuleResult?> AfterResults { get; } = new List<BusinessRuleResult?>();
        public List<bool> AfterVerdicts { get; } = new List<bool>();
        public List<(IBusinessRule Rule, BusinessRuleResult Result)> ShadowViolations { get; } = new List<(IBusinessRule, BusinessRuleResult)>();
        public RuleEvaluationResult? Completed { get; private set; }

        public void OnBeforeEvaluate(IBusinessRule rule) => Sequence.Add("Before");

        public void OnAfterEvaluate(IBusinessRule rule, BusinessRuleResult? result)
        {
            Sequence.Add("AfterResult");
            AfterResults.Add(result);
        }

        public void OnRuleBroken(IBusinessRule rule, BusinessRuleResult result)
        {
            Sequence.Add("Broken");
            Broken.Add(rule);
        }

        public void OnBatchStarted(int ruleCount) => Sequence.Add("BatchStarted");

        public void OnAfterEvaluate(IBusinessRule rule, bool isBroken, TimeSpan elapsed)
        {
            Sequence.Add("AfterVerdict");
            AfterVerdicts.Add(isBroken);
        }

        public void OnBatchCompleted(RuleEvaluationResult result)
        {
            Sequence.Add("BatchCompleted");
            Completed = result;
        }

        public void OnShadowViolation(IBusinessRule rule, BusinessRuleResult result)
        {
            Sequence.Add("Shadow");
            ShadowViolations.Add((rule, result));
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
