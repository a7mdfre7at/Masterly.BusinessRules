namespace Masterly.BusinessRules.UnitTests;

public class BusinessRuleCheckerContextFaultTests
{
    private const string LimitCode = "LIMIT.EXCEEDED";
    private const string PassingCode = "PASS";
    private const string OrderKeyName = "order";
    private const string LimitKeyName = "limit";

    private static readonly ContextKey<decimal> LimitKey = new ContextKey<decimal>(LimitKeyName);

    // ---------------------------------------------------------------------
    // FaultPolicy.Throw
    // ---------------------------------------------------------------------

    [Fact]
    public void CheckAll_RuleReadsMissingKey_ThrowsBusinessRuleContextExceptionNamingTheRule()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(OrderKeyName, "order-1");
        IBusinessRule rule = CreateLimitRule();

        BusinessRuleContextException exception = Assert.Throws<BusinessRuleContextException>(
            () => BusinessRuleChecker.CheckAll(new[] { rule }, context: context));

        Assert.Equal(LimitCode, exception.RuleCode);
        Assert.Equal(LimitKeyName, exception.Key);
        Assert.Equal($"Rule {LimitCode} failed to read context: Context has no key '{LimitKeyName}'. Available keys: {OrderKeyName}", exception.Message);
        ContextKeyNotFoundException inner = Assert.IsType<ContextKeyNotFoundException>(exception.InnerException);
        Assert.Equal(LimitKeyName, inner.Key);
    }

    [Fact]
    public void CheckAll_RuleReadsWrongType_WrapsContextValueTypeMismatchException()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(LimitKeyName, "not a decimal");
        IBusinessRule rule = CreateLimitRule();

        BusinessRuleContextException exception = Assert.Throws<BusinessRuleContextException>(
            () => BusinessRuleChecker.CheckAll(new[] { rule }, context: context));

        Assert.Equal(LimitCode, exception.RuleCode);
        ContextValueTypeMismatchException inner = Assert.IsType<ContextValueTypeMismatchException>(exception.InnerException);
        Assert.Equal(typeof(string), inner.ActualType);
        Assert.Equal(typeof(decimal), inner.RequestedType);
        Assert.Equal($"Rule {LimitCode} failed to read context: {inner.Message}", exception.Message);
    }

    [Fact]
    public void EvaluateAll_WithOptionsAndFaultPolicyThrow_WrapsContextReadFailure()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        IBusinessRule rule = CreateLimitRule();

        BusinessRuleContextException exception = Assert.Throws<BusinessRuleContextException>(
            () => BusinessRuleChecker.EvaluateAll(new[] { rule }, context, new RuleEvaluationOptions { OnFault = FaultPolicy.Throw }));

        Assert.Equal(LimitCode, exception.RuleCode);
        Assert.IsType<ContextKeyNotFoundException>(exception.InnerException);
    }

    [Fact]
    public void EvaluateAll_BooleanOverload_WrapsContextReadFailure()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        IBusinessRule rule = CreateLimitRule();

        BusinessRuleContextException exception = Assert.Throws<BusinessRuleContextException>(
            () => BusinessRuleChecker.EvaluateAll(new[] { rule }, context));

        Assert.Equal(LimitCode, exception.RuleCode);
    }

    [Fact]
    public void CheckAll_RuleThrowsNonContextException_PropagatesTheSameInstanceUnwrapped()
    {
        InvalidOperationException boom = new InvalidOperationException("boom");
        IBusinessRule rule = BusinessRuleBuilder.Create(LimitCode).When(() => throw boom).Build();

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() => BusinessRuleChecker.CheckAll(rule));

        Assert.Same(boom, thrown);
    }

    [Fact]
    public void CheckAll_RuleThrowsBareKeyNotFoundException_IsNotWrapped()
    {
        KeyNotFoundException bare = new KeyNotFoundException("a user dictionary miss");
        IBusinessRule rule = BusinessRuleBuilder.Create(LimitCode).When(() => throw bare).Build();

        KeyNotFoundException thrown = Assert.Throws<KeyNotFoundException>(() => BusinessRuleChecker.CheckAll(rule));

        Assert.Same(bare, thrown);
    }

    [Fact]
    public void CheckAll_RuleThrowsAlreadyWrappedException_IsNotWrappedTwice()
    {
        BusinessRuleContextException wrapped = new BusinessRuleContextException(
            "INNER", LimitKeyName, new ContextKeyNotFoundException(LimitKeyName, Array.Empty<string>()));
        IBusinessRule rule = BusinessRuleBuilder.Create(LimitCode).When(() => throw wrapped).Build();

        BusinessRuleContextException thrown = Assert.Throws<BusinessRuleContextException>(() => BusinessRuleChecker.CheckAll(rule));

        Assert.Same(wrapped, thrown);
        Assert.Equal("INNER", thrown.RuleCode);
    }

    [Fact]
    public void CheckAll_RuleWithThrowingCodeGetter_FallsBackToTypeNameWithoutMaskingTheFault()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        ThrowingCodeRule rule = new ThrowingCodeRule();

        BusinessRuleContextException exception = Assert.Throws<BusinessRuleContextException>(
            () => BusinessRuleChecker.CheckAll(new IBusinessRule[] { rule }, context: context));

        Assert.Equal(nameof(ThrowingCodeRule), exception.RuleCode);
        Assert.IsType<ContextKeyNotFoundException>(exception.InnerException);
    }

    [Fact]
    public void CheckAll_ContextReadFailureInsideComposedRule_ReportsTheOuterRuleCode()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        IBusinessRule group = RuleGroup.All("GROUP", "group message", CreateLimitRule(), RuleTestHelper.CreatePassingRule(PassingCode));

        BusinessRuleContextException exception = Assert.Throws<BusinessRuleContextException>(
            () => BusinessRuleChecker.CheckAll(new[] { group }, context: context));

        Assert.Equal(group.Code, exception.RuleCode);
        Assert.Equal(LimitKeyName, exception.Key);
    }

    [Fact]
    public void CheckAll_StopOnFirstFailure_ContextFaultInSecondRule_StillWrapped()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        IBusinessRule[] rules = { RuleTestHelper.CreatePassingRule(PassingCode), CreateLimitRule() };

        BusinessRuleContextException exception = Assert.Throws<BusinessRuleContextException>(
            () => BusinessRuleChecker.CheckAll(rules, stopOnFirstFailure: true, context: context));

        Assert.Equal(LimitCode, exception.RuleCode);
    }

    // ---------------------------------------------------------------------
    // Observer
    // ---------------------------------------------------------------------

    [Fact]
    public void CheckAll_Observer_ReceivesTheWrappedExceptionInOnRuleFaulted()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        RecordingObserver observer = new RecordingObserver();
        IBusinessRule rule = CreateLimitRule();

        Assert.Throws<BusinessRuleContextException>(
            () => BusinessRuleChecker.CheckAll(new[] { rule }, context, new RuleEvaluationOptions { Observer = observer }));

        (IBusinessRule faultedRule, Exception fault) = Assert.Single(observer.Faulted);
        Assert.Same(rule, faultedRule);
        BusinessRuleContextException wrapped = Assert.IsType<BusinessRuleContextException>(fault);
        Assert.Equal(LimitCode, wrapped.RuleCode);
    }

    // ---------------------------------------------------------------------
    // FaultPolicy.Record / TreatAsBroken
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_FaultPolicyRecord_LedgerFaultIsTheWrappedException()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        IBusinessRule limit = CreateLimitRule();
        IBusinessRule passing = RuleTestHelper.CreatePassingRule(PassingCode);

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(
            new[] { limit, passing }, context, new RuleEvaluationOptions { OnFault = FaultPolicy.Record });

        RuleEvaluationEntry faulted = Assert.Single(result.Faulted);
        Assert.Same(limit, faulted.Rule);
        Assert.Equal(RuleOutcome.Faulted, faulted.Outcome);
        BusinessRuleContextException fault = Assert.IsType<BusinessRuleContextException>(faulted.Fault);
        Assert.Equal(LimitCode, fault.RuleCode);
        Assert.Equal(LimitKeyName, fault.Key);
        Assert.Same(passing, Assert.Single(result.PassedRules));
        Assert.Empty(result.BrokenRules);
    }

    [Fact]
    public void EvaluateAll_FaultPolicyTreatAsBroken_ResultMessageNamesRuleAndKey()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        IBusinessRule limit = CreateLimitRule();

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(
            new[] { limit }, context, new RuleEvaluationOptions { OnFault = FaultPolicy.TreatAsBroken });

        BusinessRuleResult broken = Assert.Single(result.BrokenRules);
        Assert.Equal(LimitCode, broken.Code);
        RuleEvaluationEntry entry = Assert.Single(result.Entries);
        Assert.IsType<BusinessRuleContextException>(entry.Fault);
    }

    [Fact]
    public void EvaluateAll_FaultPolicyRecord_RuleWithTypedKeyPresent_IsNotFaulted()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((LimitKey, 1000m));
        IBusinessRule limit = CreateLimitRule();

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(
            new[] { limit }, context, new RuleEvaluationOptions { OnFault = FaultPolicy.Record });

        Assert.False(result.HasFaults);
        Assert.Equal(LimitCode, Assert.Single(result.BrokenRules).Code);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static IBusinessRule CreateLimitRule()
    {
        return BusinessRuleBuilder.Create(LimitCode)
            .WithMessage("Transaction amount exceeds limit.")
            .When(ctx => 5000m > ctx.Get(LimitKey))
            .Build();
    }

    private sealed class ThrowingCodeRule : BaseBusinessRule
    {
        public override string Code => throw new InvalidOperationException("no code");
        public override string Message => "message";
        public override bool IsBroken() => false;
        public override bool IsBroken(BusinessRuleContext context) => context.Get(LimitKey) < 0m;
    }

    private sealed class RecordingObserver : IRuleExecutionObserver
    {
        public List<(IBusinessRule rule, Exception exception)> Faulted { get; } = new List<(IBusinessRule, Exception)>();

        public void OnBeforeEvaluate(IBusinessRule rule) { }
        public void OnAfterEvaluate(IBusinessRule rule, BusinessRuleResult? result) { }
        public void OnRuleBroken(IBusinessRule rule, BusinessRuleResult result) { }
        public void OnRuleFaulted(IBusinessRule rule, Exception exception) => Faulted.Add((rule, exception));
    }
}
