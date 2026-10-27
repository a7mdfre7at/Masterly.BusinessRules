namespace Masterly.BusinessRules.DependencyInjection.UnitTests;

public class RuleRunnerAsyncTests
{
    private const string NoItemsCode = "ORDER.NO_ITEMS";
    private const string TooManyItemsCode = "ORDER.TOO_MANY_ITEMS";
    private const string TooExpensiveCode = "ORDER.TOO_EXPENSIVE";
    private const string ThrowingCode = "ORDER.THROWS";
    private const string ContextSeenCode = "ORDER.CONTEXT";
    private const string LimitKey = "limit";
    private const string MarkerKey = "marker";
    private const decimal DefaultLimit = 100m;
    private const int MaxItems = 10;

    private static readonly IBusinessRule<Order>[] NoSyncRules = Array.Empty<IBusinessRule<Order>>();

    // ---------------------------------------------------------------------
    // CheckAllAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CheckAllAsync_WhenAsyncRulesAreBroken_ThrowsWithTheBrokenCodes()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(NoSyncRules, AsyncRules());

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => runner.CheckAllAsync(new Order(50, 500m), TestContext.Current.CancellationToken));

        Assert.Equal(new[] { TooManyItemsCode, TooExpensiveCode }, exception.BrokenRules.Select(result => result.Code));
    }

    [Fact]
    public async Task CheckAllAsync_EvaluatesSyncRulesFirstAndThenAsyncRules()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(new[] { NoItems() }, AsyncRules());

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => runner.CheckAllAsync(new Order(0, 500m), TestContext.Current.CancellationToken));

        Assert.Equal(new[] { NoItemsCode, TooExpensiveCode }, exception.BrokenRules.Select(result => result.Code));
        Assert.Equal(3, exception.Entries.Count);
    }

    [Fact]
    public async Task CheckAllAsync_WhenEveryRulePasses_DoesNotThrow()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(new[] { NoItems() }, AsyncRules());

        await runner.CheckAllAsync(new Order(2, 10m), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CheckAllAsync_EmptyRunner_DoesNotThrow()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(NoSyncRules, Array.Empty<IAsyncBusinessRule<Order>>());

        await runner.CheckAllAsync(new Order(0, -1m), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CheckAllAsync_WithContext_ForwardsTheContextToBothLists()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(new[] { ContextSeen() }, AsyncRules());
        Order order = new Order(2, 50m);
        BusinessRuleContext context = RuleTestHelper.CreateContext((LimitKey, 40m), (MarkerKey, true));

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => runner.CheckAllAsync(order, context, TestContext.Current.CancellationToken));

        Assert.Equal(new[] { ContextSeenCode, TooExpensiveCode }, exception.BrokenRules.Select(result => result.Code));
    }

    [Fact]
    public async Task CheckAllAsync_NullContext_EvaluatesAgainstAFreshContext()
    {
        BusinessRuleContext? seen = null;
        IAsyncBusinessRule<Order> capturing = AsyncBusinessRuleBuilder<Order>.Create("CAPTURE")
            .WithMessage("Captures.")
            .WhenAsync((order, ctx) =>
            {
                seen = ctx;
                return Task.FromResult(false);
            })
            .Build();
        RuleRunner<Order> runner = new RuleRunner<Order>(NoSyncRules, new[] { capturing });

        await runner.CheckAllAsync(new Order(1, 1m), null, TestContext.Current.CancellationToken);

        Assert.NotNull(seen);
        Assert.Equal(0, seen!.Count);
    }

    [Fact]
    public async Task CheckAllAsync_WithOptionsThrowOn_IgnoresBrokenRulesBelowTheThreshold()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(NoSyncRules, AsyncRules());
        Order order = new Order(2, 500m);

        await runner.CheckAllAsync(order, null, new AsyncRuleEvaluationOptions { ThrowOn = RuleSeverity.Error }, TestContext.Current.CancellationToken);
        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => runner.CheckAllAsync(order, null, new AsyncRuleEvaluationOptions { ThrowOn = RuleSeverity.Warning }, TestContext.Current.CancellationToken));

        Assert.Equal(TooExpensiveCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckAllAsync_WithOptionsRunInParallel_EvaluatesEveryRule()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(new[] { NoItems() }, AsyncRules());

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => runner.CheckAllAsync(new Order(0, 500m), null, new AsyncRuleEvaluationOptions { RunInParallel = true }, TestContext.Current.CancellationToken));

        Assert.Equal(2, exception.BrokenRules.Count);
        Assert.Equal(3, exception.Entries.Count);
    }

    [Fact]
    public async Task CheckAllAsync_NullOptions_ThrowsArgumentNullException()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(NoSyncRules, AsyncRules());

        await Assert.ThrowsAsync<ArgumentNullException>(() => runner.CheckAllAsync(new Order(1, 1m), null, null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CheckAllAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(NoSyncRules, new[] { Cancellable() });
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.CheckAllAsync(new Order(1, 1m), source.Token));
    }

    // ---------------------------------------------------------------------
    // EvaluateAllAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_ReturnsBrokenAndPassedRules()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(new[] { NoItems() }, AsyncRules());

        AsyncRuleEvaluationResult result = await runner.EvaluateAllAsync(new Order(0, 10m), TestContext.Current.CancellationToken);

        Assert.Equal(NoItemsCode, Assert.Single(result.BrokenRules).Code);
        Assert.Equal(new[] { TooManyItemsCode, TooExpensiveCode }, result.PassedRules.Select(rule => rule.Code));
    }

    [Fact]
    public async Task EvaluateAllAsync_EmptyRunner_ReportsAllPassed()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(NoSyncRules, Array.Empty<IAsyncBusinessRule<Order>>());

        AsyncRuleEvaluationResult result = await runner.EvaluateAllAsync(new Order(0, -1m), TestContext.Current.CancellationToken);

        Assert.True(result.AllPassed);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithContext_ForwardsTheContext()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(NoSyncRules, AsyncRules());

        AsyncRuleEvaluationResult result = await runner.EvaluateAllAsync(new Order(2, 50m), RuleTestHelper.CreateContext((LimitKey, 40m)), TestContext.Current.CancellationToken);

        Assert.Equal(TooExpensiveCode, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithOptionsFaultPolicyRecord_RecordsTheFault()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(NoSyncRules, new[] { Throwing() });

        AsyncRuleEvaluationResult result = await runner.EvaluateAllAsync(new Order(1, 1m), null, new AsyncRuleEvaluationOptions { OnFault = FaultPolicy.Record }, TestContext.Current.CancellationToken);

        Assert.True(result.HasFaults);
        Assert.Equal(ThrowingCode, Assert.Single(result.Faulted).Code);
    }

    [Fact]
    public async Task EvaluateAllAsync_NullOptions_ThrowsArgumentNullException()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(NoSyncRules, AsyncRules());

        await Assert.ThrowsAsync<ArgumentNullException>(() => runner.EvaluateAllAsync(new Order(1, 1m), null, null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EvaluateAllAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(NoSyncRules, new[] { Cancellable() });
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.EvaluateAllAsync(new Order(1, 1m), source.Token));
    }

    [Fact]
    public async Task EvaluateAllAsync_RecordsAdaptedSyncRulesAndBoundAsyncRulesInTheLedger()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(new[] { NoItems() }, new[] { TooManyItems() });

        AsyncRuleEvaluationResult result = await runner.EvaluateAllAsync(new Order(2, 10m), TestContext.Current.CancellationToken);

        Assert.IsType<SyncToAsyncRuleAdapter>(result.Entries[0].AsyncRule);
        Assert.IsType<BoundAsyncBusinessRule<Order>>(result.Entries[1].AsyncRule);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static IAsyncBusinessRule<Order>[] AsyncRules()
    {
        return new[] { TooManyItems(), TooExpensive() };
    }

    private static IBusinessRule<Order> NoItems()
    {
        return BusinessRuleBuilder<Order>.Create(NoItemsCode).WithMessage("Order must have items.").When(order => order.ItemCount == 0).Build();
    }

    private static IBusinessRule<Order> ContextSeen()
    {
        return BusinessRuleBuilder<Order>.Create(ContextSeenCode)
            .WithMessage("Marker present.")
            .When((order, ctx) => ctx.TryGet(MarkerKey, out bool marker) && marker)
            .Build();
    }

    private static IAsyncBusinessRule<Order> TooManyItems()
    {
        return AsyncBusinessRuleBuilder<Order>.Create(TooManyItemsCode).WithMessage("Too many items.").WhenAsync(order => Task.FromResult(order.ItemCount > MaxItems)).Build();
    }

    private static IAsyncBusinessRule<Order> TooExpensive()
    {
        return AsyncBusinessRuleBuilder<Order>.Create(TooExpensiveCode)
            .WithMessage("Order is too expensive.")
            .WithSeverity(RuleSeverity.Warning)
            .WhenAsync((order, ctx) => Task.FromResult(order.Total > (ctx.TryGet(LimitKey, out decimal limit) ? limit : DefaultLimit)))
            .Build();
    }

    private static IAsyncBusinessRule<Order> Throwing()
    {
        return AsyncBusinessRuleBuilder<Order>.Create(ThrowingCode).WithMessage("Throws.").WhenAsync(order => throw new InvalidOperationException("boom")).Build();
    }

    private static IAsyncBusinessRule<Order> Cancellable()
    {
        return AsyncBusinessRuleBuilder<Order>.Create("ORDER.CANCEL")
            .WithMessage("Honours cancellation.")
            .WhenAsync((order, ctx, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(false);
            })
            .Build();
    }
}
