namespace Masterly.BusinessRules.DependencyInjection.UnitTests;

public class RuleRunnerTests
{
    private const string NoItemsCode = "ORDER.NO_ITEMS";
    private const string TooExpensiveCode = "ORDER.TOO_EXPENSIVE";
    private const string ThrowingCode = "ORDER.THROWS";
    private const string AsyncCode = "ORDER.ASYNC";
    private const string LimitKey = "limit";
    private const decimal DefaultLimit = 100m;

    private static readonly IAsyncBusinessRule<Order>[] NoAsyncRules = Array.Empty<IAsyncBusinessRule<Order>>();

    // ---------------------------------------------------------------------
    // Construction
    // ---------------------------------------------------------------------

    [Fact]
    public void Constructor_NullRules_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new RuleRunner<Order>(null!, NoAsyncRules));

        Assert.Equal("rules", exception.ParamName);
    }

    [Fact]
    public void Constructor_NullAsyncRules_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new RuleRunner<Order>(Rules(), null!));

        Assert.Equal("asyncRules", exception.ParamName);
    }

    [Fact]
    public void Constructor_NullRuleInTheCollection_ThrowsArgumentExceptionNamingThePosition()
    {
        IBusinessRule<Order>[] rules = { NoItems(), null! };

        ArgumentException exception = Assert.Throws<ArgumentException>(() => new RuleRunner<Order>(rules, NoAsyncRules));

        Assert.Equal("rules", exception.ParamName);
        Assert.Contains("position 1", exception.Message);
    }

    [Fact]
    public void Constructor_ExposesTheRulesInTheOrderGiven()
    {
        IBusinessRule<Order>[] rules = Rules();

        RuleRunner<Order> runner = new RuleRunner<Order>(rules, NoAsyncRules);

        Assert.Equal(rules, runner.Rules);
        Assert.Empty(runner.AsyncRules);
        Assert.False(runner.HasAsyncRules);
    }

    [Fact]
    public void Constructor_EnumeratesTheCollectionsOnce()
    {
        int enumerations = 0;
        IEnumerable<IBusinessRule<Order>> rules = Enumerate(() => enumerations++, NoItems());

        RuleRunner<Order> runner = new RuleRunner<Order>(rules, NoAsyncRules);
        runner.CheckAll(new Order(1, 10m));
        runner.CheckAll(new Order(1, 10m));

        Assert.Equal(1, enumerations);
    }

    // ---------------------------------------------------------------------
    // CheckAll
    // ---------------------------------------------------------------------

    [Fact]
    public void CheckAll_WhenRulesAreBroken_ThrowsWithTheBrokenCodes()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(Rules(), NoAsyncRules);

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => runner.CheckAll(new Order(0, 500m)));

        Assert.Equal(new[] { NoItemsCode, TooExpensiveCode }, exception.BrokenRules.Select(result => result.Code));
    }

    [Fact]
    public void CheckAll_WhenRulesPass_DoesNotThrow()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(Rules(), NoAsyncRules);

        runner.CheckAll(new Order(2, 10m));
    }

    [Fact]
    public void CheckAll_EmptyRunner_DoesNotThrow()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(Array.Empty<IBusinessRule<Order>>(), NoAsyncRules);

        runner.CheckAll(new Order(0, -1m));
    }

    [Fact]
    public void CheckAll_WithContext_ForwardsTheContextToEveryRule()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(Rules(), NoAsyncRules);
        Order order = new Order(2, 50m);

        runner.CheckAll(order, RuleTestHelper.CreateContext((LimitKey, 60m)));
        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => runner.CheckAll(order, RuleTestHelper.CreateContext((LimitKey, 40m))));

        Assert.Equal(TooExpensiveCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void CheckAll_WithOptionsThrowOn_IgnoresBrokenRulesBelowTheThreshold()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(Rules(), NoAsyncRules);
        Order order = new Order(2, 500m);

        runner.CheckAll(order, null, new RuleEvaluationOptions { ThrowOn = RuleSeverity.Error });
        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => runner.CheckAll(order, null, new RuleEvaluationOptions { ThrowOn = RuleSeverity.Warning }));

        Assert.Equal(TooExpensiveCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void CheckAll_WithOptionsStopOnFirstFailure_LeavesLaterRulesNotEvaluated()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(Rules(), NoAsyncRules);

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => runner.CheckAll(new Order(0, 500m), null, new RuleEvaluationOptions { StopOnFirstFailure = true }));

        Assert.Equal(NoItemsCode, Assert.Single(exception.BrokenRules).Code);
        Assert.Equal(RuleOutcome.NotEvaluated, exception.Entries[1].Outcome);
    }

    [Fact]
    public void CheckAll_NullOptions_ThrowsArgumentNullException()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(Rules(), NoAsyncRules);

        Assert.Throws<ArgumentNullException>(() => runner.CheckAll(new Order(1, 1m), null, null!));
    }

    [Fact]
    public void CheckAll_WhenAsyncRulesAreRegistered_ThrowsInvalidOperationExceptionNamingThem()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(Rules(), new[] { PassingAsync() });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => runner.CheckAll(new Order(2, 10m)));

        Assert.True(runner.HasAsyncRules);
        Assert.Contains(AsyncCode, exception.Message);
        Assert.Contains("CheckAllAsync", exception.Message);
    }

    [Fact]
    public void HasAsyncRules_ThroughTheInterface_ReportsWhetherAsyncRulesAreRegistered()
    {
        IRuleRunner<Order> withAsync = new RuleRunner<Order>(Rules(), new[] { PassingAsync() });
        IRuleRunner<Order> syncOnly = new RuleRunner<Order>(Rules(), NoAsyncRules);

        Assert.True(withAsync.HasAsyncRules);
        Assert.False(syncOnly.HasAsyncRules);
    }

    [Fact]
    public void CheckAll_WithOptions_WhenAsyncRulesAreRegistered_ThrowsInvalidOperationException()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(Rules(), new[] { PassingAsync() });

        Assert.Throws<InvalidOperationException>(() => runner.CheckAll(new Order(2, 10m), null, new RuleEvaluationOptions()));
    }

    [Fact]
    public void CheckAll_ValueTypeSubject_IsSupported()
    {
        IBusinessRule<int> negative = BusinessRuleBuilder<int>.Create("INT.NEGATIVE").WithMessage("Negative.").When(value => value < 0).Build();
        RuleRunner<int> runner = new RuleRunner<int>(new[] { negative }, Array.Empty<IAsyncBusinessRule<int>>());

        runner.CheckAll(5);
        Assert.Throws<BusinessRuleValidationException>(() => runner.CheckAll(-5));
    }

    // ---------------------------------------------------------------------
    // EvaluateAll
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_ReturnsBrokenAndPassedRules()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(Rules(), NoAsyncRules);

        RuleEvaluationResult result = runner.EvaluateAll(new Order(0, 10m));

        Assert.Equal(NoItemsCode, Assert.Single(result.BrokenRules).Code);
        Assert.Equal(TooExpensiveCode, Assert.Single(result.PassedRules).Code);
    }

    [Fact]
    public void EvaluateAll_EmptyRunner_ReportsAllPassed()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(Array.Empty<IBusinessRule<Order>>(), NoAsyncRules);

        RuleEvaluationResult result = runner.EvaluateAll(new Order(0, -1m));

        Assert.True(result.AllPassed);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public void EvaluateAll_WithContext_ForwardsTheContext()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(Rules(), NoAsyncRules);

        RuleEvaluationResult result = runner.EvaluateAll(new Order(2, 50m), RuleTestHelper.CreateContext((LimitKey, 40m)));

        Assert.Equal(TooExpensiveCode, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public void EvaluateAll_WithOptionsFaultPolicyRecord_RecordsTheFault()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(new[] { NoItems(), Throwing() }, NoAsyncRules);

        RuleEvaluationResult result = runner.EvaluateAll(new Order(1, 1m), null, new RuleEvaluationOptions { OnFault = FaultPolicy.Record });

        Assert.True(result.HasFaults);
        Assert.Equal(ThrowingCode, Assert.Single(result.Faulted).Code);
        Assert.True(result.AllPassed);
    }

    [Fact]
    public void EvaluateAll_NullOptions_ThrowsArgumentNullException()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(Rules(), NoAsyncRules);

        Assert.Throws<ArgumentNullException>(() => runner.EvaluateAll(new Order(1, 1m), null, null!));
    }

    [Fact]
    public void EvaluateAll_WhenAsyncRulesAreRegistered_ThrowsInvalidOperationException()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(Rules(), new[] { PassingAsync() });

        Assert.Throws<InvalidOperationException>(() => runner.EvaluateAll(new Order(2, 10m)));
        Assert.Throws<InvalidOperationException>(() => runner.EvaluateAll(new Order(2, 10m), null, new RuleEvaluationOptions()));
    }

    [Fact]
    public void EvaluateAll_RecordsTheBoundRulesInTheLedger()
    {
        RuleRunner<Order> runner = new RuleRunner<Order>(Rules(), NoAsyncRules);

        RuleEvaluationResult result = runner.EvaluateAll(new Order(2, 10m));

        Assert.All(result.Entries, entry => Assert.IsType<BoundBusinessRule<Order>>(entry.Rule));
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static IBusinessRule<Order>[] Rules()
    {
        return new[] { NoItems(), TooExpensive() };
    }

    private static IBusinessRule<Order> NoItems()
    {
        return BusinessRuleBuilder<Order>.Create(NoItemsCode).WithMessage("Order must have items.").When(order => order.ItemCount == 0).Build();
    }

    private static IBusinessRule<Order> TooExpensive()
    {
        return BusinessRuleBuilder<Order>.Create(TooExpensiveCode)
            .WithMessage("Order is too expensive.")
            .WithSeverity(RuleSeverity.Warning)
            .When((order, ctx) => order.Total > (ctx.TryGet(LimitKey, out decimal limit) ? limit : DefaultLimit))
            .Build();
    }

    private static IBusinessRule<Order> Throwing()
    {
        return BusinessRuleBuilder<Order>.Create(ThrowingCode).WithMessage("Throws.").When(order => throw new InvalidOperationException("boom")).Build();
    }

    private static IAsyncBusinessRule<Order> PassingAsync()
    {
        return AsyncBusinessRuleBuilder<Order>.Create(AsyncCode).WithMessage("Passes.").WhenAsync(order => Task.FromResult(false)).Build();
    }

    private static IEnumerable<IBusinessRule<Order>> Enumerate(Action onEnumerate, params IBusinessRule<Order>[] rules)
    {
        onEnumerate();
        foreach (IBusinessRule<Order> rule in rules)
        {
            yield return rule;
        }
    }
}
