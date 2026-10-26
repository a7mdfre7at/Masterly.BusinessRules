namespace Masterly.BusinessRules.UnitTests;

public class AsyncBusinessRuleCheckerTypedTests
{
    private const string NoItemsCode = "ORDER.NO_ITEMS";
    private const string TooExpensiveCode = "ORDER.TOO_EXPENSIVE";
    private const string ThrowingCode = "ORDER.THROWS";
    private const string LimitKey = "limit";
    private const string CounterKey = "counter";
    private const decimal DefaultLimit = 100m;

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------
    // CheckAllAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CheckAllAsync_WhenRulesAreBroken_ThrowsWithTheBrokenCodes()
    {
        Order order = new Order(0, 500m);

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(() => AsyncBusinessRuleChecker.CheckAllAsync(order, Rules(), TestToken));

        Assert.Equal(new[] { NoItemsCode, TooExpensiveCode }, exception.BrokenRules.Select(r => r.Code));
    }

    [Fact]
    public async Task CheckAllAsync_WhenRulesPass_DoesNotThrow()
    {
        await AsyncBusinessRuleChecker.CheckAllAsync(new Order(2, 10m), Rules(), TestToken);
    }

    [Fact]
    public async Task CheckAllAsync_WithContext_ForwardsTheContextToEveryRule()
    {
        Order order = new Order(2, 50m);

        await AsyncBusinessRuleChecker.CheckAllAsync(RuleTestHelper.CreateContext((LimitKey, 60m)), order, Rules(), TestToken);
        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(RuleTestHelper.CreateContext((LimitKey, 40m)), order, Rules(), TestToken));

        Assert.Equal(TooExpensiveCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckAllAsync_WithOptionsThrowOn_IgnoresBrokenRulesBelowTheThreshold()
    {
        Order order = new Order(2, 500m);

        await AsyncBusinessRuleChecker.CheckAllAsync(order, Rules(), new AsyncRuleEvaluationOptions { ThrowOn = RuleSeverity.Error }, TestToken);
        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(order, Rules(), new AsyncRuleEvaluationOptions { ThrowOn = RuleSeverity.Warning }, TestToken));

        Assert.Equal(TooExpensiveCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckAllAsync_WithContextAndOptions_StopOnFirstFailure_LeavesLaterRulesNotEvaluated()
    {
        Order order = new Order(0, 500m);

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), order, Rules(), new AsyncRuleEvaluationOptions { StopOnFirstFailure = true }, TestToken));

        Assert.Equal(NoItemsCode, Assert.Single(exception.BrokenRules).Code);
        Assert.Equal(new[] { RuleOutcome.Broken, RuleOutcome.NotEvaluated }, exception.Entries.Select(e => e.Outcome));
    }

    [Fact]
    public async Task CheckAllAsync_WithParallelOptions_EvaluatesEveryRule()
    {
        Order order = new Order(0, 500m);

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(order, Rules(), new AsyncRuleEvaluationOptions { RunInParallel = true }, TestToken));

        Assert.Equal(2, exception.BrokenRules.Count);
    }

    [Fact]
    public async Task CheckAllAsync_WithObserver_ReceivesBoundRulesOverTheSubject()
    {
        Order order = new Order(0, 10m);
        RecordingObserver observer = new RecordingObserver();

        await Assert.ThrowsAsync<BusinessRuleValidationException>(() => AsyncBusinessRuleChecker.CheckAllAsync(order, Rules(), new AsyncRuleEvaluationOptions { Observer = observer }, TestToken));

        Assert.Equal(new[] { NoItemsCode, TooExpensiveCode }, observer.Seen.Select(r => r.Code));
        Assert.All(observer.Seen, rule => Assert.Same(order, Assert.IsType<BoundAsyncBusinessRule<Order>>(rule).Subject));
        Assert.Equal(2, observer.BatchSize);
    }

    [Fact]
    public async Task CheckAllAsync_NullRules_ThrowsArgumentNullException()
    {
        IEnumerable<IAsyncBusinessRule<Order>> rules = null!;

        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => AsyncBusinessRuleChecker.CheckAllAsync(new Order(1, 1m), rules, TestToken));

        Assert.Equal("rules", exception.ParamName);
    }

    [Fact]
    public async Task CheckAllAsync_NullRuleInsideTheSet_ThrowsArgumentExceptionNamingThePosition()
    {
        IAsyncBusinessRule<Order>[] rules = { NoItems(), null!, TooExpensive() };

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() => AsyncBusinessRuleChecker.CheckAllAsync(new Order(1, 1m), rules, TestToken));

        Assert.Equal("rules", exception.ParamName);
        Assert.Contains("position 1", exception.Message);
    }

    [Fact]
    public async Task CheckAllAsync_NullContext_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => AsyncBusinessRuleChecker.CheckAllAsync((BusinessRuleContext)null!, new Order(1, 1m), Rules(), TestToken));
    }

    [Fact]
    public async Task CheckAllAsync_NullOptions_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => AsyncBusinessRuleChecker.CheckAllAsync(new Order(1, 1m), Rules(), (AsyncRuleEvaluationOptions)null!, TestToken));
    }

    [Fact]
    public async Task CheckAllAsync_EmptyRules_DoesNotThrow()
    {
        await AsyncBusinessRuleChecker.CheckAllAsync(new Order(0, 0m), Array.Empty<IAsyncBusinessRule<Order>>(), TestToken);
    }

    [Fact]
    public async Task CheckAllAsync_CancelledToken_PropagatesCancellation()
    {
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AsyncBusinessRuleChecker.CheckAllAsync(new Order(0, 0m), Rules(), source.Token));
    }

    [Fact]
    public async Task CheckAllAsync_StructSubject_IsSupported()
    {
        IAsyncBusinessRule<int> negative = AsyncBusinessRuleBuilder<int>.Create("NEG").WithMessage("m").WhenAsync(value => Task.FromResult(value < 0)).Build();

        await AsyncBusinessRuleChecker.CheckAllAsync(1, new[] { negative }, TestToken);
        await Assert.ThrowsAsync<BusinessRuleValidationException>(() => AsyncBusinessRuleChecker.CheckAllAsync(-1, new[] { negative }, TestToken));
    }

    // ---------------------------------------------------------------------
    // EvaluateAllAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_ReturnsLedgerEntriesOverBoundRules()
    {
        Order order = new Order(0, 10m);

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(order, Rules(), TestToken);

        Assert.Equal(NoItemsCode, Assert.Single(result.BrokenRules).Code);
        Assert.Equal(new[] { RuleOutcome.Broken, RuleOutcome.Passed }, result.Entries.Select(e => e.Outcome));
        Assert.All(result.Entries, entry => Assert.Same(order, Assert.IsType<BoundAsyncBusinessRule<Order>>(entry.AsyncRule).Subject));
    }

    [Fact]
    public async Task EvaluateAllAsync_WithContext_ForwardsTheContext()
    {
        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(RuleTestHelper.CreateContext((LimitKey, 40m)), new Order(1, 50m), Rules(), TestToken);

        Assert.Equal(TooExpensiveCode, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithOptionsRecord_RecordsFaultsInsteadOfThrowing()
    {
        IAsyncBusinessRule<Order>[] rules = { Throwing(), NoItems() };

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new Order(0, 1m), rules, new AsyncRuleEvaluationOptions { OnFault = FaultPolicy.Record }, TestToken);

        Assert.Equal(ThrowingCode, Assert.Single(result.Faulted).Code);
        Assert.Equal(NoItemsCode, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public async Task EvaluateAllAsync_WithContextAndOptions_RunsInParallelAndNeverThrowsForBrokenRules()
    {
        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new Order(0, 500m), Rules(), new AsyncRuleEvaluationOptions { RunInParallel = true, ThrowOn = RuleSeverity.Info }, TestToken);

        Assert.Equal(2, result.BrokenRules.Count);
    }

    [Fact]
    public async Task EvaluateAllAsync_NullRules_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => AsyncBusinessRuleChecker.EvaluateAllAsync(new Order(1, 1m), (IEnumerable<IAsyncBusinessRule<Order>>)null!, TestToken));
    }

    [Fact]
    public async Task EvaluateAllAsync_EmptyRules_ReturnsEmptyResult()
    {
        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new Order(1, 1m), Array.Empty<IAsyncBusinessRule<Order>>(), TestToken);

        Assert.True(result.AllPassed);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public async Task EvaluateAllAsync_AcceptsContravariantRuleSets()
    {
        IAsyncBusinessRule<object> notNull = AsyncBusinessRuleBuilder<object>.Create("NULL").WithMessage("m").WhenAsync(value => Task.FromResult(value == null)).Build();
        IEnumerable<IAsyncBusinessRule<Order>> rules = new List<IAsyncBusinessRule<object>> { notNull };

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new Order(1, 1m), rules, TestToken);

        Assert.True(result.AllPassed);
    }

    // ---------------------------------------------------------------------
    // CheckBySeverityAsync / CheckByCategoryAsync / CheckByTagsAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CheckBySeverityAsync_ChecksOnlyMatchingSeverities()
    {
        Order order = new Order(0, 500m);
        BusinessRuleContext context = new BusinessRuleContext();

        await AsyncBusinessRuleChecker.CheckBySeverityAsync(context, order, Rules(), TestToken, RuleSeverity.Info);
        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckBySeverityAsync(context, order, Rules(), TestToken, RuleSeverity.Warning));

        Assert.Equal(TooExpensiveCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckByCategoryAsync_ChecksOnlyMatchingCategory()
    {
        Order order = new Order(0, 500m);
        BusinessRuleContext context = new BusinessRuleContext();

        await AsyncBusinessRuleChecker.CheckByCategoryAsync(context, order, Rules(), "Nothing", TestToken);
        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckByCategoryAsync(context, order, Rules(), "Money", TestToken));

        Assert.Equal(TooExpensiveCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckByTagsAsync_ChecksOnlyMatchingTagsCaseInsensitively()
    {
        Order order = new Order(0, 500m);
        BusinessRuleContext context = new BusinessRuleContext();

        await AsyncBusinessRuleChecker.CheckByTagsAsync(context, order, Rules(), TestToken, "nothing");
        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckByTagsAsync(context, order, Rules(), TestToken, "ITEMS"));

        Assert.Equal(NoItemsCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckByTagsAsync_NonGeneric_ChecksOnlyMatchingTags()
    {
        IAsyncBusinessRule[] rules = { NoItems().For(new Order(0, 1m)), TooExpensive().For(new Order(0, 500m)) };
        BusinessRuleContext context = new BusinessRuleContext();

        await AsyncBusinessRuleChecker.CheckByTagsAsync(context, rules, TestToken, "nothing");
        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckByTagsAsync(context, rules, TestToken, "money"));

        Assert.Equal(TooExpensiveCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckByFilters_NullRules_ThrowArgumentNullException()
    {
        IEnumerable<IAsyncBusinessRule<Order>> rules = null!;
        Order order = new Order(1, 1m);
        BusinessRuleContext context = new BusinessRuleContext();

        await Assert.ThrowsAsync<ArgumentNullException>(() => AsyncBusinessRuleChecker.CheckBySeverityAsync(context, order, rules, TestToken, RuleSeverity.Error));
        await Assert.ThrowsAsync<ArgumentNullException>(() => AsyncBusinessRuleChecker.CheckByCategoryAsync(context, order, rules, "Money", TestToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => AsyncBusinessRuleChecker.CheckByTagsAsync(context, order, rules, TestToken, "items"));
    }

    [Fact]
    public async Task CheckByFilters_CancelledToken_PropagatesCancellation()
    {
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();
        Order order = new Order(0, 500m);
        BusinessRuleContext context = new BusinessRuleContext();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AsyncBusinessRuleChecker.CheckBySeverityAsync(context, order, Rules(), source.Token, RuleSeverity.Error));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AsyncBusinessRuleChecker.CheckByCategoryAsync(context, order, Rules(), "Orders", source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AsyncBusinessRuleChecker.CheckByTagsAsync(context, order, Rules(), source.Token, "items"));
    }

    // ---------------------------------------------------------------------
    // EvaluateEachAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateEachAsync_ReportsEverySubjectByPosition()
    {
        Order first = new Order(0, 10m);
        Order second = new Order(1, 10m);
        Order third = new Order(0, 500m);

        AsyncSubjectEvaluationCollection<Order> each = await AsyncBusinessRuleChecker.EvaluateEachAsync(new[] { first, second, third }, Rules(), TestToken);

        Assert.Equal(3, each.Count);
        Assert.Equal(new[] { 0, 1, 2 }, each.Select(e => e.Index));
        Assert.Same(first, each[0].Subject);
        Assert.Same(second, each[1].Subject);
        Assert.Same(third, each[2].Subject);
        Assert.Equal(NoItemsCode, Assert.Single(each[0].BrokenRules).Code);
        Assert.True(each[1].AllPassed);
        Assert.Equal(new[] { NoItemsCode, TooExpensiveCode }, each[2].Result.BrokenRules.Select(r => r.Code));
    }

    [Fact]
    public async Task EvaluateEachAsync_ExposesAggregates()
    {
        AsyncSubjectEvaluationCollection<Order> each = await AsyncBusinessRuleChecker.EvaluateEachAsync(new[] { new Order(0, 10m), new Order(1, 10m), new Order(0, 500m) }, Rules(), TestToken);

        Assert.False(each.AllPassed);
        Assert.True(each.HasBrokenRules);
        Assert.Equal(new[] { 0, 2 }, each.Broken.Select(e => e.Index));
        Assert.Equal(new[] { 1 }, each.Passed.Select(e => e.Index));
        Assert.Equal(new[] { NoItemsCode, NoItemsCode, TooExpensiveCode }, each.BrokenRules.Select(r => r.Code));
        Assert.True(each.TotalDuration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task EvaluateEachAsync_IsAssignableToReadOnlyList()
    {
        IReadOnlyList<AsyncSubjectEvaluation<Order>> each = await AsyncBusinessRuleChecker.EvaluateEachAsync(new[] { new Order(1, 1m) }, Rules(), TestToken);

        Assert.Single(each);
    }

    [Fact]
    public async Task EvaluateEachAsync_EmptySubjects_ReturnsEmptyCollectionThatAllPassed()
    {
        AsyncSubjectEvaluationCollection<Order> each = await AsyncBusinessRuleChecker.EvaluateEachAsync(Array.Empty<Order>(), Rules(), TestToken);

        Assert.Empty(each);
        Assert.True(each.AllPassed);
        Assert.False(each.HasBrokenRules);
    }

    [Fact]
    public async Task EvaluateEachAsync_NullArguments_ThrowArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => AsyncBusinessRuleChecker.EvaluateEachAsync((IEnumerable<Order>)null!, Rules(), TestToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => AsyncBusinessRuleChecker.EvaluateEachAsync(new[] { new Order(1, 1m) }, (IEnumerable<IAsyncBusinessRule<Order>>)null!, TestToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => AsyncBusinessRuleChecker.EvaluateEachAsync(new[] { new Order(1, 1m) }, Rules(), (AsyncRuleEvaluationOptions)null!, TestToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => AsyncBusinessRuleChecker.EvaluateEachAsync((BusinessRuleContext)null!, new[] { new Order(1, 1m) }, Rules(), TestToken));
    }

    [Fact]
    public async Task EvaluateEachAsync_NullRuleInsideTheSet_ThrowsBeforeEvaluatingAnySubject()
    {
        IAsyncBusinessRule<Order>[] rules = { NoItems(), null! };

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() => AsyncBusinessRuleChecker.EvaluateEachAsync(new[] { new Order(1, 1m) }, rules, TestToken));

        Assert.Contains("position 1", exception.Message);
    }

    [Fact]
    public async Task EvaluateEachAsync_DuplicateSubjects_AreEvaluatedSeparately()
    {
        Order order = new Order(0, 1m);

        AsyncSubjectEvaluationCollection<Order> each = await AsyncBusinessRuleChecker.EvaluateEachAsync(new[] { order, order }, Rules(), TestToken);

        Assert.Equal(2, each.Count);
        Assert.All(each, evaluation => Assert.True(evaluation.HasBrokenRules));
    }

    [Fact]
    public async Task EvaluateEachAsync_ValueTypeSubjects_AreSupported()
    {
        IAsyncBusinessRule<int> negative = AsyncBusinessRuleBuilder<int>.Create("NEG").WithMessage("m").WhenAsync(value => Task.FromResult(value < 0)).Build();

        AsyncSubjectEvaluationCollection<int> each = await AsyncBusinessRuleChecker.EvaluateEachAsync(new[] { -1, 0, -2 }, new[] { negative }, TestToken);

        Assert.Equal(new[] { true, false, true }, each.Select(e => e.HasBrokenRules));
        Assert.Equal(-2, each[2].Subject);
    }

    [Fact]
    public async Task EvaluateEachAsync_SharesTheContextAcrossSubjects()
    {
        IAsyncBusinessRule<Order> counting = AsyncBusinessRuleBuilder<Order>.Create("COUNT").WithMessage("m")
            .WhenAsync((_, ctx) => { ctx.Set(CounterKey, ctx.TryGet(CounterKey, out int count) ? count + 1 : 1); return Task.FromResult(false); })
            .Build();
        BusinessRuleContext context = new BusinessRuleContext();

        await AsyncBusinessRuleChecker.EvaluateEachAsync(context, new[] { new Order(1, 1m), new Order(2, 2m), new Order(3, 3m) }, new[] { counting }, TestToken);

        Assert.Equal(3, context.Get<int>(CounterKey));
    }

    [Fact]
    public async Task EvaluateEachAsync_WithOptionsStopOnFirstFailure_AppliesWithinEachSubjectOnly()
    {
        AsyncSubjectEvaluationCollection<Order> each = await AsyncBusinessRuleChecker.EvaluateEachAsync(
            new[] { new Order(0, 500m), new Order(0, 500m) },
            Rules(),
            new AsyncRuleEvaluationOptions { StopOnFirstFailure = true },
            TestToken);

        Assert.Equal(2, each.Count);
        Assert.All(each, evaluation => Assert.Equal(new[] { RuleOutcome.Broken, RuleOutcome.NotEvaluated }, evaluation.Result.Entries.Select(e => e.Outcome)));
    }

    [Fact]
    public async Task EvaluateEachAsync_WithContextAndOptions_ForwardsBothAndRunsEachBatchInParallel()
    {
        RecordingObserver observer = new RecordingObserver();

        AsyncSubjectEvaluationCollection<Order> each = await AsyncBusinessRuleChecker.EvaluateEachAsync(
            RuleTestHelper.CreateContext((LimitKey, 40m)),
            new[] { new Order(1, 50m) },
            Rules(),
            new AsyncRuleEvaluationOptions { RunInParallel = true, Observer = observer },
            TestToken);

        Assert.Equal(TooExpensiveCode, Assert.Single(each[0].BrokenRules).Code);
        Assert.Equal(2, observer.Seen.Count);
    }

    [Fact]
    public async Task EvaluateEachAsync_CancelledToken_PropagatesCancellation()
    {
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AsyncBusinessRuleChecker.EvaluateEachAsync(new[] { new Order(1, 1m) }, Rules(), source.Token));
    }

    [Fact]
    public async Task EvaluateEachAsync_WithPerSubjectCachedBoundRules_DoesNotCrossContaminate()
    {
        IAsyncBusinessRule<Order> shared = NoItems();
        Order broken = new Order(0, 1m);
        Order passing = new Order(1, 1m);
        IAsyncBusinessRule brokenCached = shared.For(broken).Cached(TimeSpan.FromMinutes(1));
        IAsyncBusinessRule passingCached = shared.For(passing).Cached(TimeSpan.FromMinutes(1));
        BusinessRuleContext context = new BusinessRuleContext();

        AsyncRuleEvaluationResult firstPass = await AsyncBusinessRuleChecker.EvaluateAllAsync(context, new[] { brokenCached, passingCached }, false, TestToken);
        AsyncRuleEvaluationResult secondPass = await AsyncBusinessRuleChecker.EvaluateAllAsync(context, new[] { brokenCached, passingCached }, false, TestToken);
        AsyncSubjectEvaluationCollection<Order> each = await AsyncBusinessRuleChecker.EvaluateEachAsync(new[] { broken, passing, broken }, new[] { shared }, TestToken);

        Assert.Equal(NoItemsCode, Assert.Single(firstPass.BrokenRules).Code);
        Assert.Same(passingCached, Assert.Single(firstPass.PassedRules));
        Assert.Equal(NoItemsCode, Assert.Single(secondPass.BrokenRules).Code);
        Assert.Same(passingCached, Assert.Single(secondPass.PassedRules));
        Assert.Equal(new[] { true, false, true }, each.Select(e => e.HasBrokenRules));
    }

    // ---------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------

    private static IAsyncBusinessRule<Order>[] Rules() => new[] { NoItems(), TooExpensive() };

    private static IAsyncBusinessRule<Order> NoItems()
    {
        return AsyncBusinessRuleBuilder<Order>.Create(NoItemsCode)
            .WithMessage("Order must contain at least one item.")
            .WithCategory("Orders")
            .WithTags("items")
            .WhenAsync(order => Task.FromResult(order.ItemCount == 0))
            .Build();
    }

    private static IAsyncBusinessRule<Order> TooExpensive()
    {
        return AsyncBusinessRuleBuilder<Order>.Create(TooExpensiveCode)
            .WithMessage("Order total exceeds the limit.")
            .WithSeverity(RuleSeverity.Warning)
            .WithCategory("Money")
            .WithTags("money")
            .WhenAsync((order, ctx) => Task.FromResult(order.Total > (ctx.TryGet(LimitKey, out decimal limit) ? limit : DefaultLimit)))
            .Build();
    }

    private static IAsyncBusinessRule<Order> Throwing()
    {
        return AsyncBusinessRuleBuilder<Order>.Create(ThrowingCode).WithMessage("boom").WhenAsync(_ => throw new InvalidOperationException("boom")).Build();
    }

    private sealed class Order
    {
        public Order(int itemCount, decimal total)
        {
            ItemCount = itemCount;
            Total = total;
        }

        public int ItemCount { get; }
        public decimal Total { get; }
    }

    private sealed class RecordingObserver : IAsyncRuleExecutionObserver
    {
        private readonly object _lock = new object();
        public List<IAsyncBusinessRule> Seen { get; } = new List<IAsyncBusinessRule>();
        public int BatchSize { get; private set; }

        public Task OnBeforeEvaluateAsync(IAsyncBusinessRule rule, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                Seen.Add(rule);
            }

            return Task.CompletedTask;
        }

        public Task OnAfterEvaluateAsync(IAsyncBusinessRule rule, BusinessRuleResult? result, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task OnRuleBrokenAsync(IAsyncBusinessRule rule, BusinessRuleResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OnBatchStartedAsync(int ruleCount, CancellationToken cancellationToken = default)
        {
            BatchSize = ruleCount;
            return Task.CompletedTask;
        }
    }
}
