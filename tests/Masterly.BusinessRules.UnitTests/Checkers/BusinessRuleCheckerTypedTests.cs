namespace Masterly.BusinessRules.UnitTests;

/// <summary>
/// The subject checker reached through <see cref="BusinessRuleChecker.For{TSubject}(TSubject)"/>
/// (<see cref="SubjectRuleChecker{TSubject}"/>) and <c>EvaluateEach</c>.
/// </summary>
public class BusinessRuleCheckerTypedTests
{
    private const string NoItemsCode = "ORDER.NO_ITEMS";
    private const string TooExpensiveCode = "ORDER.TOO_EXPENSIVE";
    private const string ThrowingCode = "ORDER.THROWS";
    private const string LimitKey = "limit";
    private const string CounterKey = "counter";
    private const decimal DefaultLimit = 100m;

    // ---------------------------------------------------------------------
    // For
    // ---------------------------------------------------------------------

    [Fact]
    public void For_ExposesTheSubject()
    {
        Order order = new Order(1, 1m);

        SubjectRuleChecker<Order> checker = BusinessRuleChecker.For(order);

        Assert.Same(order, checker.Subject);
    }

    [Fact]
    public void For_NullSubject_IsHandedToTheRules()
    {
        IBusinessRule<Order> missing = BusinessRuleBuilder<Order>.Create("MISSING").WithMessage("m").When(order => order == null).Build();

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.For((Order)null!).CheckAll(new[] { missing }));

        Assert.Equal("MISSING", Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void For_EvaluateAllWithABareNullContext_BindsTheContextOverload()
    {
        RuleEvaluationResult result = BusinessRuleChecker.For(new Order(0, 1m)).EvaluateAll(Rules(), null);

        Assert.Equal(NoItemsCode, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public void For_CheckAllWithADefaultContext_BindsTheContextOverload()
    {
        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.For(new Order(0, 1m)).CheckAll(Rules(), default));

        Assert.Equal(NoItemsCode, Assert.Single(exception.BrokenRules).Code);
    }

    // ---------------------------------------------------------------------
    // CheckAll
    // ---------------------------------------------------------------------

    [Fact]
    public void CheckAll_WhenRulesAreBroken_ThrowsWithTheBrokenCodes()
    {
        Order order = new Order(0, 500m);

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.For(order).CheckAll(Rules()));

        Assert.Equal(new[] { NoItemsCode, TooExpensiveCode }, exception.BrokenRules.Select(r => r.Code));
    }

    [Fact]
    public void CheckAll_WhenRulesPass_DoesNotThrow()
    {
        BusinessRuleChecker.For(new Order(2, 10m)).CheckAll(Rules());
    }

    [Fact]
    public void CheckAll_WithContext_ForwardsTheContextToEveryRule()
    {
        Order order = new Order(2, 50m);

        BusinessRuleChecker.For(order).CheckAll(Rules(), RuleTestHelper.CreateContext((LimitKey, 60m)));
        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.For(order).CheckAll(Rules(), RuleTestHelper.CreateContext((LimitKey, 40m))));

        Assert.Equal(TooExpensiveCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void CheckAll_WithOptionsThrowOn_IgnoresBrokenRulesBelowTheThreshold()
    {
        Order order = new Order(2, 500m);

        BusinessRuleChecker.For(order).CheckAll(Rules(), new RuleEvaluationOptions { ThrowOn = RuleSeverity.Error });
        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.For(order).CheckAll(Rules(), new RuleEvaluationOptions { ThrowOn = RuleSeverity.Warning }));

        Assert.Equal(TooExpensiveCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void CheckAll_WithContextAndOptions_StopOnFirstFailure_LeavesLaterRulesNotEvaluated()
    {
        Order order = new Order(0, 500m);

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.For(order).CheckAll(Rules(), new BusinessRuleContext(), new RuleEvaluationOptions { StopOnFirstFailure = true }));

        Assert.Equal(NoItemsCode, Assert.Single(exception.BrokenRules).Code);
        Assert.Equal(new[] { RuleOutcome.Broken, RuleOutcome.NotEvaluated }, exception.Entries.Select(e => e.Outcome));
    }

    [Fact]
    public void CheckAll_WithObserver_ReceivesBoundRulesOverTheSubject()
    {
        Order order = new Order(0, 10m);
        RecordingObserver observer = new RecordingObserver();

        Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.For(order).CheckAll(Rules(), new RuleEvaluationOptions { Observer = observer }));

        Assert.Equal(new[] { NoItemsCode, TooExpensiveCode }, observer.Seen.Select(r => r.Code));
        Assert.All(observer.Seen, rule => Assert.Same(order, Assert.IsType<BoundBusinessRule<Order>>(rule).Subject));
        Assert.Equal(2, observer.BatchSize);
    }

    [Fact]
    public void CheckAll_NullRules_ThrowsArgumentNullException()
    {
        IEnumerable<IBusinessRule<Order>> rules = null!;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => BusinessRuleChecker.For(new Order(1, 1m)).CheckAll(rules));

        Assert.Equal("rules", exception.ParamName);
    }

    [Fact]
    public void CheckAll_NullRuleInsideTheSet_ThrowsArgumentExceptionNamingThePosition()
    {
        IBusinessRule<Order>[] rules = { NoItems(), null!, TooExpensive() };

        ArgumentException exception = Assert.Throws<ArgumentException>(() => BusinessRuleChecker.For(new Order(1, 1m)).CheckAll(rules));

        Assert.Equal("rules", exception.ParamName);
        Assert.Contains("position 1", exception.Message);
    }

    [Fact]
    public void CheckAll_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => BusinessRuleChecker.For(new Order(1, 1m)).CheckAll(Rules(), (RuleEvaluationOptions)null!));
    }

    [Fact]
    public void CheckAll_EmptyRules_DoesNotThrow()
    {
        BusinessRuleChecker.For(new Order(0, 0m)).CheckAll(Array.Empty<IBusinessRule<Order>>());
    }

    [Fact]
    public void CheckAll_StructSubject_IsSupported()
    {
        IBusinessRule<int> negative = BusinessRuleBuilder<int>.Create("NEG").WithMessage("m").When(value => value < 0).Build();

        BusinessRuleChecker.For(1).CheckAll(new[] { negative });
        Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.For(-1).CheckAll(new[] { negative }));
    }

    // ---------------------------------------------------------------------
    // EvaluateAll
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_ReturnsLedgerEntriesOverBoundRules()
    {
        Order order = new Order(0, 10m);

        RuleEvaluationResult result = BusinessRuleChecker.For(order).EvaluateAll(Rules());

        Assert.Equal(NoItemsCode, Assert.Single(result.BrokenRules).Code);
        Assert.Equal(2, result.Entries.Count);
        Assert.Equal(new[] { RuleOutcome.Broken, RuleOutcome.Passed }, result.Entries.Select(e => e.Outcome));
        Assert.All(result.Entries, entry => Assert.Same(order, Assert.IsType<BoundBusinessRule<Order>>(entry.Rule).Subject));
    }

    [Fact]
    public void EvaluateAll_WithContext_ForwardsTheContext()
    {
        RuleEvaluationResult result = BusinessRuleChecker.For(new Order(1, 50m)).EvaluateAll(Rules(), RuleTestHelper.CreateContext((LimitKey, 40m)));

        Assert.Equal(TooExpensiveCode, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public void EvaluateAll_WithOptionsRecord_RecordsFaultsInsteadOfThrowing()
    {
        IBusinessRule<Order>[] rules = { Throwing(), NoItems() };

        RuleEvaluationResult result = BusinessRuleChecker.For(new Order(0, 1m)).EvaluateAll(rules, new RuleEvaluationOptions { OnFault = FaultPolicy.Record });

        Assert.Equal(ThrowingCode, Assert.Single(result.Faulted).Code);
        Assert.Equal(NoItemsCode, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public void EvaluateAll_WithContextAndOptions_NeverThrowsForBrokenRules()
    {
        RuleEvaluationResult result = BusinessRuleChecker.For(new Order(0, 500m)).EvaluateAll(Rules(), new BusinessRuleContext(), new RuleEvaluationOptions { ThrowOn = RuleSeverity.Info });

        Assert.Equal(2, result.BrokenRules.Count);
    }

    [Fact]
    public void EvaluateAll_NullRules_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => BusinessRuleChecker.For(new Order(1, 1m)).EvaluateAll((IEnumerable<IBusinessRule<Order>>)null!));
    }

    [Fact]
    public void EvaluateAll_EmptyRules_ReturnsEmptyResult()
    {
        RuleEvaluationResult result = BusinessRuleChecker.For(new Order(1, 1m)).EvaluateAll(Array.Empty<IBusinessRule<Order>>());

        Assert.True(result.AllPassed);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public void EvaluateAll_AcceptsContravariantRuleSets()
    {
        IBusinessRule<object> notNull = BusinessRuleBuilder<object>.Create("NULL").WithMessage("m").When(value => value == null).Build();
        IEnumerable<IBusinessRule<Order>> rules = new List<IBusinessRule<object>> { notNull };

        RuleEvaluationResult result = BusinessRuleChecker.For(new Order(1, 1m)).EvaluateAll(rules);

        Assert.True(result.AllPassed);
    }

    [Fact]
    public void EvaluateAll_WithTypedNullContext_StillBindsTheNonGenericOverload()
    {
        IBusinessRule[] rules = { RuleTestHelper.CreateBrokenRule("PLAIN") };

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rules, (BusinessRuleContext?)null);

        Assert.Equal("PLAIN", Assert.Single(result.BrokenRules).Code);
    }

    // ---------------------------------------------------------------------
    // CheckBySeverity / CheckByCategory / CheckByTags
    // ---------------------------------------------------------------------

    [Fact]
    public void CheckBySeverity_ChecksOnlyMatchingSeverities()
    {
        Order order = new Order(0, 500m);

        BusinessRuleChecker.For(order).CheckBySeverity(Rules(), RuleSeverity.Info);
        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.For(order).CheckBySeverity(Rules(), RuleSeverity.Warning));

        Assert.Equal(TooExpensiveCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void CheckByCategory_ChecksOnlyMatchingCategory()
    {
        Order order = new Order(0, 500m);

        BusinessRuleChecker.For(order).CheckByCategory(Rules(), "Nothing");
        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.For(order).CheckByCategory(Rules(), "Money"));

        Assert.Equal(TooExpensiveCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void CheckByTags_ChecksOnlyMatchingTagsCaseInsensitively()
    {
        Order order = new Order(0, 500m);

        BusinessRuleChecker.For(order).CheckByTags(Rules(), "nothing");
        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.For(order).CheckByTags(Rules(), "ITEMS"));

        Assert.Equal(NoItemsCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void CheckByFilters_NullRules_ThrowArgumentNullException()
    {
        IEnumerable<IBusinessRule<Order>> rules = null!;
        Order order = new Order(1, 1m);

        Assert.Throws<ArgumentNullException>(() => BusinessRuleChecker.For(order).CheckBySeverity(rules, RuleSeverity.Error));
        Assert.Throws<ArgumentNullException>(() => BusinessRuleChecker.For(order).CheckByCategory(rules, "Money"));
        Assert.Throws<ArgumentNullException>(() => BusinessRuleChecker.For(order).CheckByTags(rules, "items"));
    }

    // ---------------------------------------------------------------------
    // EvaluateEach
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateEach_ReportsEverySubjectByPosition()
    {
        Order first = new Order(0, 10m);
        Order second = new Order(1, 10m);
        Order third = new Order(0, 500m);

        SubjectEvaluationCollection<Order> each = BusinessRuleChecker.EvaluateEach(new[] { first, second, third }, Rules());

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
    public void EvaluateEach_ExposesAggregates()
    {
        SubjectEvaluationCollection<Order> each = BusinessRuleChecker.EvaluateEach(new[] { new Order(0, 10m), new Order(1, 10m), new Order(0, 500m) }, Rules());

        Assert.False(each.AllPassed);
        Assert.True(each.HasBrokenRules);
        Assert.Equal(new[] { 0, 2 }, each.Broken.Select(e => e.Index));
        Assert.Equal(new[] { 1 }, each.Passed.Select(e => e.Index));
        Assert.Equal(new[] { NoItemsCode, NoItemsCode, TooExpensiveCode }, each.BrokenRules.Select(r => r.Code));
        Assert.True(each.TotalDuration >= TimeSpan.Zero);
    }

    [Fact]
    public void EvaluateEach_IsAssignableToReadOnlyList()
    {
        IReadOnlyList<SubjectEvaluation<Order>> each = BusinessRuleChecker.EvaluateEach(new[] { new Order(1, 1m) }, Rules());

        Assert.Single(each);
    }

    [Fact]
    public void EvaluateEach_WhenEverySubjectPasses_AllPassed()
    {
        SubjectEvaluationCollection<Order> each = BusinessRuleChecker.EvaluateEach(new[] { new Order(1, 1m), new Order(2, 2m) }, Rules());

        Assert.True(each.AllPassed);
        Assert.False(each.HasBrokenRules);
        Assert.Empty(each.Broken);
        Assert.Equal(2, each.Passed.Count());
    }

    [Fact]
    public void EvaluateEach_EmptySubjects_ReturnsEmptyCollectionThatAllPassed()
    {
        SubjectEvaluationCollection<Order> each = BusinessRuleChecker.EvaluateEach(Array.Empty<Order>(), Rules());

        Assert.Empty(each);
        Assert.True(each.AllPassed);
        Assert.False(each.HasBrokenRules);
        Assert.Equal(TimeSpan.Zero, each.TotalDuration);
    }

    [Fact]
    public void EvaluateEach_NullArguments_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => BusinessRuleChecker.EvaluateEach((IEnumerable<Order>)null!, Rules()));
        Assert.Throws<ArgumentNullException>(() => BusinessRuleChecker.EvaluateEach(new[] { new Order(1, 1m) }, (IEnumerable<IBusinessRule<Order>>)null!));
        Assert.Throws<ArgumentNullException>(() => BusinessRuleChecker.EvaluateEach(new[] { new Order(1, 1m) }, Rules(), (RuleEvaluationOptions)null!));
    }

    [Fact]
    public void EvaluateEach_NullRuleInsideTheSet_ThrowsBeforeEvaluatingAnySubject()
    {
        IBusinessRule<Order>[] rules = { NoItems(), null! };

        ArgumentException exception = Assert.Throws<ArgumentException>(() => BusinessRuleChecker.EvaluateEach(new[] { new Order(1, 1m) }, rules));

        Assert.Contains("position 1", exception.Message);
    }

    [Fact]
    public void EvaluateEach_DuplicateSubjects_AreEvaluatedSeparately()
    {
        Order order = new Order(0, 1m);

        SubjectEvaluationCollection<Order> each = BusinessRuleChecker.EvaluateEach(new[] { order, order }, Rules());

        Assert.Equal(2, each.Count);
        Assert.All(each, evaluation => Assert.True(evaluation.HasBrokenRules));
        Assert.Equal(new[] { 0, 1 }, each.Select(e => e.Index));
    }

    [Fact]
    public void EvaluateEach_ValueTypeSubjects_AreSupported()
    {
        IBusinessRule<int> negative = BusinessRuleBuilder<int>.Create("NEG").WithMessage("m").When(value => value < 0).Build();

        SubjectEvaluationCollection<int> each = BusinessRuleChecker.EvaluateEach(new[] { -1, 0, -2 }, new[] { negative });

        Assert.Equal(new[] { true, false, true }, each.Select(e => e.HasBrokenRules));
        Assert.Equal(-2, each[2].Subject);
    }

    [Fact]
    public void EvaluateEach_SharesTheContextAcrossSubjects()
    {
        IBusinessRule<Order> counting = BusinessRuleBuilder<Order>.Create("COUNT").WithMessage("m")
            .When((_, ctx) => { ctx.Set(CounterKey, ctx.TryGet(CounterKey, out int count) ? count + 1 : 1); return false; })
            .Build();
        BusinessRuleContext context = new BusinessRuleContext();

        BusinessRuleChecker.EvaluateEach(new[] { new Order(1, 1m), new Order(2, 2m), new Order(3, 3m) }, new[] { counting }, context);

        Assert.Equal(3, context.Get<int>(CounterKey));
    }

    [Fact]
    public void EvaluateEach_EvaluatesEveryRuleOncePerSubject()
    {
        int evaluations = 0;
        IBusinessRule<Order> counting = BusinessRuleBuilder<Order>.Create("COUNT").WithMessage("m").When(_ => { evaluations++; return false; }).Build();

        BusinessRuleChecker.EvaluateEach(new[] { new Order(1, 1m), new Order(2, 2m) }, new[] { counting, counting });

        Assert.Equal(4, evaluations);
    }

    [Fact]
    public void EvaluateEach_WithOptionsStopOnFirstFailure_AppliesWithinEachSubjectOnly()
    {
        SubjectEvaluationCollection<Order> each = BusinessRuleChecker.EvaluateEach(
            new[] { new Order(0, 500m), new Order(0, 500m) },
            Rules(),
            new RuleEvaluationOptions { StopOnFirstFailure = true });

        Assert.Equal(2, each.Count);
        Assert.All(each, evaluation => Assert.Equal(new[] { RuleOutcome.Broken, RuleOutcome.NotEvaluated }, evaluation.Result.Entries.Select(e => e.Outcome)));
    }

    [Fact]
    public void EvaluateEach_WithContextAndOptions_ForwardsBoth()
    {
        RecordingObserver observer = new RecordingObserver();

        SubjectEvaluationCollection<Order> each = BusinessRuleChecker.EvaluateEach(
            new[] { new Order(1, 50m) },
            Rules(),
            RuleTestHelper.CreateContext((LimitKey, 40m)),
            new RuleEvaluationOptions { Observer = observer });

        Assert.Equal(TooExpensiveCode, Assert.Single(each[0].BrokenRules).Code);
        Assert.Equal(2, observer.Seen.Count);
    }

    [Fact]
    public void EvaluateEach_WithPerSubjectCachedBoundRules_DoesNotCrossContaminate()
    {
        IBusinessRule<Order> shared = NoItems();
        Order broken = new Order(0, 1m);
        Order passing = new Order(1, 1m);
        IBusinessRule brokenCached = shared.For(broken).Cached(TimeSpan.FromMinutes(1));
        IBusinessRule passingCached = shared.For(passing).Cached(TimeSpan.FromMinutes(1));

        RuleEvaluationResult firstPass = BusinessRuleChecker.EvaluateAll(brokenCached, passingCached);
        RuleEvaluationResult secondPass = BusinessRuleChecker.EvaluateAll(brokenCached, passingCached);
        SubjectEvaluationCollection<Order> each = BusinessRuleChecker.EvaluateEach(new[] { broken, passing, broken }, new[] { shared });

        Assert.Equal(NoItemsCode, Assert.Single(firstPass.BrokenRules).Code);
        Assert.Same(passingCached, Assert.Single(firstPass.PassedRules));
        Assert.Equal(NoItemsCode, Assert.Single(secondPass.BrokenRules).Code);
        Assert.Same(passingCached, Assert.Single(secondPass.PassedRules));
        Assert.Equal(new[] { true, false, true }, each.Select(e => e.HasBrokenRules));
    }

    // ---------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------

    private static IBusinessRule<Order>[] Rules() => new[] { NoItems(), TooExpensive() };

    private static IBusinessRule<Order> NoItems()
    {
        return BusinessRuleBuilder<Order>.Create(NoItemsCode)
            .WithMessage("Order must contain at least one item.")
            .WithCategory("Orders")
            .WithTags("items")
            .When(order => order.ItemCount == 0)
            .Build();
    }

    private static IBusinessRule<Order> TooExpensive()
    {
        return BusinessRuleBuilder<Order>.Create(TooExpensiveCode)
            .WithMessage("Order total exceeds the limit.")
            .WithSeverity(RuleSeverity.Warning)
            .WithCategory("Money")
            .WithTags("money")
            .When((order, ctx) => order.Total > (ctx.TryGet(LimitKey, out decimal limit) ? limit : DefaultLimit))
            .Build();
    }

    private static IBusinessRule<Order> Throwing()
    {
        return BusinessRuleBuilder<Order>.Create(ThrowingCode).WithMessage("boom").When(_ => throw new InvalidOperationException("boom")).Build();
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

    private sealed class RecordingObserver : IRuleExecutionObserver
    {
        public List<IBusinessRule> Seen { get; } = new List<IBusinessRule>();
        public int BatchSize { get; private set; }
        public void OnBeforeEvaluate(IBusinessRule rule) => Seen.Add(rule);
        public void OnAfterEvaluate(IBusinessRule rule, BusinessRuleResult? result) { }
        public void OnRuleBroken(IBusinessRule rule, BusinessRuleResult result) { }
        public void OnBatchStarted(int ruleCount) => BatchSize = ruleCount;
    }
}
