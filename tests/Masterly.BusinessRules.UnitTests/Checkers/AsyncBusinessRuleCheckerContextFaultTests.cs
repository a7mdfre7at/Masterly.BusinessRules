namespace Masterly.BusinessRules.UnitTests;

public class AsyncBusinessRuleCheckerContextFaultTests
{
    private const string LimitCode = "LIMIT.EXCEEDED";
    private const string PassingCode = "PASS";
    private const string OrderKeyName = "order";
    private const string LimitKeyName = "limit";

    private static readonly ContextKey<decimal> LimitKey = new ContextKey<decimal>(LimitKeyName);

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------
    // FaultPolicy.Throw
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CheckAllAsync_RuleReadsMissingKey_ThrowsBusinessRuleContextExceptionNamingTheRule()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(OrderKeyName, "order-1");
        IAsyncBusinessRule rule = CreateLimitRule();

        BusinessRuleContextException exception = await Assert.ThrowsAsync<BusinessRuleContextException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(context, new[] { rule }, cancellationToken: TestToken));

        Assert.Equal(LimitCode, exception.RuleCode);
        Assert.Equal(LimitKeyName, exception.Key);
        Assert.Equal($"Rule {LimitCode} failed to read context: Context has no key '{LimitKeyName}'. Available keys: {OrderKeyName}", exception.Message);
        ContextKeyNotFoundException inner = Assert.IsType<ContextKeyNotFoundException>(exception.InnerException);
        Assert.Equal(LimitKeyName, inner.Key);
    }

    [Fact]
    public async Task CheckAllAsync_RuleReadsWrongType_WrapsContextValueTypeMismatchException()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(LimitKeyName, "not a decimal");
        IAsyncBusinessRule rule = CreateLimitRule();

        BusinessRuleContextException exception = await Assert.ThrowsAsync<BusinessRuleContextException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(context, new[] { rule }, cancellationToken: TestToken));

        Assert.Equal(LimitCode, exception.RuleCode);
        ContextValueTypeMismatchException inner = Assert.IsType<ContextValueTypeMismatchException>(exception.InnerException);
        Assert.Equal(typeof(string), inner.ActualType);
        Assert.Equal(typeof(decimal), inner.RequestedType);
        Assert.Equal($"Rule {LimitCode} failed to read context: {inner.Message}", exception.Message);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithOptionsAndFaultPolicyThrow_WrapsContextReadFailure()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        IAsyncBusinessRule rule = CreateLimitRule();

        BusinessRuleContextException exception = await Assert.ThrowsAsync<BusinessRuleContextException>(
            () => AsyncBusinessRuleChecker.EvaluateAllAsync(context, new[] { rule }, new AsyncRuleEvaluationOptions { OnFault = FaultPolicy.Throw }, TestToken));

        Assert.Equal(LimitCode, exception.RuleCode);
        Assert.IsType<ContextKeyNotFoundException>(exception.InnerException);
    }

    [Fact]
    public async Task EvaluateAllAsync_RunInParallel_WrapsContextReadFailure()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        IAsyncBusinessRule[] rules = { RuleTestHelper.CreatePassingAsyncRule(PassingCode), CreateLimitRule() };

        BusinessRuleContextException exception = await Assert.ThrowsAsync<BusinessRuleContextException>(
            () => AsyncBusinessRuleChecker.EvaluateAllAsync(context, rules, new AsyncRuleEvaluationOptions { RunInParallel = true }, TestToken));

        Assert.Equal(LimitCode, exception.RuleCode);
    }

    [Fact]
    public async Task CheckAllAsync_RuleThrowsNonContextException_PropagatesTheSameInstanceUnwrapped()
    {
        InvalidOperationException boom = new InvalidOperationException("boom");
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(LimitCode).WhenAsync((ctx, ct) => throw boom).Build();

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new[] { rule }, cancellationToken: TestToken));

        Assert.Same(boom, thrown);
    }

    [Fact]
    public async Task CheckAllAsync_RuleThrowsBareKeyNotFoundException_IsNotWrapped()
    {
        KeyNotFoundException bare = new KeyNotFoundException("a user dictionary miss");
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(LimitCode).WhenAsync((ctx, ct) => throw bare).Build();

        KeyNotFoundException thrown = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new[] { rule }, cancellationToken: TestToken));

        Assert.Same(bare, thrown);
    }

    [Fact]
    public async Task CheckAllAsync_RuleThrowsAlreadyWrappedException_IsNotWrappedTwice()
    {
        BusinessRuleContextException wrapped = new BusinessRuleContextException(
            "INNER", LimitKeyName, new ContextKeyNotFoundException(LimitKeyName, Array.Empty<string>()));
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(LimitCode).WhenAsync((ctx, ct) => throw wrapped).Build();

        BusinessRuleContextException thrown = await Assert.ThrowsAsync<BusinessRuleContextException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new[] { rule }, cancellationToken: TestToken));

        Assert.Same(wrapped, thrown);
        Assert.Equal("INNER", thrown.RuleCode);
    }

    [Fact]
    public async Task CheckAllAsync_RuleWithThrowingCodeGetter_FallsBackToTypeNameWithoutMaskingTheFault()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        ThrowingCodeAsyncRule rule = new ThrowingCodeAsyncRule();

        BusinessRuleContextException exception = await Assert.ThrowsAsync<BusinessRuleContextException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(context, new IAsyncBusinessRule[] { rule }, cancellationToken: TestToken));

        Assert.Equal(nameof(ThrowingCodeAsyncRule), exception.RuleCode);
        Assert.IsType<ContextKeyNotFoundException>(exception.InnerException);
    }

    [Fact]
    public async Task CheckAllAsync_ContextReadFailureInsideComposedRule_ReportsTheOuterRuleCode()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        IAsyncBusinessRule group = AsyncRuleGroup.All("GROUP", "group message", CreateLimitRule(), RuleTestHelper.CreatePassingAsyncRule(PassingCode));

        BusinessRuleContextException exception = await Assert.ThrowsAsync<BusinessRuleContextException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(context, new[] { group }, cancellationToken: TestToken));

        Assert.Equal(group.Code, exception.RuleCode);
        Assert.Equal(LimitKeyName, exception.Key);
    }

    [Fact]
    public async Task CheckAllAsync_CancelledBeforeRuleRuns_ThrowsOperationCanceledExceptionNotWrapped()
    {
        using CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();
        BusinessRuleContext context = new BusinessRuleContext();
        IAsyncBusinessRule rule = CreateLimitRule();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(context, new[] { rule }, cancellationToken: cts.Token));
    }

    // ---------------------------------------------------------------------
    // Observer
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CheckAllAsync_Observer_ReceivesTheWrappedExceptionInOnRuleFaulted()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        RecordingObserver observer = new RecordingObserver();
        IAsyncBusinessRule rule = CreateLimitRule();

        await Assert.ThrowsAsync<BusinessRuleContextException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(context, new[] { rule }, new AsyncRuleEvaluationOptions { Observer = observer }, TestToken));

        (IAsyncBusinessRule faultedRule, Exception fault) = Assert.Single(observer.Faulted);
        Assert.Same(rule, faultedRule);
        BusinessRuleContextException wrapped = Assert.IsType<BusinessRuleContextException>(fault);
        Assert.Equal(LimitCode, wrapped.RuleCode);
    }

    // ---------------------------------------------------------------------
    // FaultPolicy.Record / TreatAsBroken
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_FaultPolicyRecord_LedgerFaultIsTheWrappedException()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        IAsyncBusinessRule limit = CreateLimitRule();
        IAsyncBusinessRule passing = RuleTestHelper.CreatePassingAsyncRule(PassingCode);

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(
            context, new[] { limit, passing }, new AsyncRuleEvaluationOptions { OnFault = FaultPolicy.Record }, TestToken);

        RuleEvaluationEntry faulted = Assert.Single(result.Faulted);
        Assert.Same(limit, faulted.AsyncRule);
        Assert.Equal(RuleOutcome.Faulted, faulted.Outcome);
        BusinessRuleContextException fault = Assert.IsType<BusinessRuleContextException>(faulted.Fault);
        Assert.Equal(LimitCode, fault.RuleCode);
        Assert.Equal(LimitKeyName, fault.Key);
        Assert.Same(passing, Assert.Single(result.PassedRules));
        Assert.Empty(result.BrokenRules);
    }

    [Fact]
    public async Task EvaluateAllAsync_FaultPolicyRecordInParallel_LedgerFaultIsTheWrappedException()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        IAsyncBusinessRule limit = CreateLimitRule();
        IAsyncBusinessRule passing = RuleTestHelper.CreatePassingAsyncRule(PassingCode);

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(
            context, new[] { passing, limit }, new AsyncRuleEvaluationOptions { OnFault = FaultPolicy.Record, RunInParallel = true }, TestToken);

        RuleEvaluationEntry faulted = Assert.Single(result.Faulted);
        Assert.Same(limit, faulted.AsyncRule);
        Assert.IsType<BusinessRuleContextException>(faulted.Fault);
    }

    [Fact]
    public async Task EvaluateAllAsync_FaultPolicyTreatAsBroken_ResultCarriesRuleCodeAndWrappedFault()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        IAsyncBusinessRule limit = CreateLimitRule();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(
            context, new[] { limit }, new AsyncRuleEvaluationOptions { OnFault = FaultPolicy.TreatAsBroken }, TestToken);

        BusinessRuleResult broken = Assert.Single(result.BrokenRules);
        Assert.Equal(LimitCode, broken.Code);
        RuleEvaluationEntry entry = Assert.Single(result.Entries);
        Assert.IsType<BusinessRuleContextException>(entry.Fault);
    }

    [Fact]
    public async Task EvaluateAllAsync_FaultPolicyRecord_RuleWithTypedKeyPresent_IsNotFaulted()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((LimitKey, 1000m));
        IAsyncBusinessRule limit = CreateLimitRule();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(
            context, new[] { limit }, new AsyncRuleEvaluationOptions { OnFault = FaultPolicy.Record }, TestToken);

        Assert.False(result.HasFaults);
        Assert.Equal(LimitCode, Assert.Single(result.BrokenRules).Code);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static IAsyncBusinessRule CreateLimitRule()
    {
        return AsyncBusinessRuleBuilder.Create(LimitCode)
            .WithMessage("Transaction amount exceeds limit.")
            .WhenAsync((ctx, ct) => Task.FromResult(5000m > ctx.Get(LimitKey)))
            .Build();
    }

    private sealed class ThrowingCodeAsyncRule : BaseAsyncBusinessRule
    {
        public override string Code => throw new InvalidOperationException("no code");
        public override string Message => "message";

        public override Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(context.Get(LimitKey) < 0m);
        }
    }

    private sealed class RecordingObserver : IAsyncRuleExecutionObserver
    {
        public List<(IAsyncBusinessRule rule, Exception exception)> Faulted { get; } = new List<(IAsyncBusinessRule, Exception)>();

        public Task OnBeforeEvaluateAsync(IAsyncBusinessRule rule, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task OnAfterEvaluateAsync(IAsyncBusinessRule rule, BusinessRuleResult? result, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task OnRuleBrokenAsync(IAsyncBusinessRule rule, BusinessRuleResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OnRuleFaultedAsync(IAsyncBusinessRule rule, Exception exception, CancellationToken cancellationToken = default)
        {
            Faulted.Add((rule, exception));
            return Task.CompletedTask;
        }
    }
}
