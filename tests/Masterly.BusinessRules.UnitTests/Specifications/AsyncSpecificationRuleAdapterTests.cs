namespace Masterly.BusinessRules.UnitTests;

public class AsyncSpecificationRuleAdapterTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------
    // Construction
    // ---------------------------------------------------------------------

    [Fact]
    public void ToAsync_ReturnsAnAdapterOverTheSpecification()
    {
        OrderIsOverdueRule rule = new OrderIsOverdueRule();

        IAsyncBusinessRule<Order> adapted = rule.ToAsync();

        AsyncSpecificationRuleAdapter<Order> adapter = Assert.IsType<AsyncSpecificationRuleAdapter<Order>>(adapted);
        Assert.Same(rule, adapter.InnerRule);
    }

    [Fact]
    public void ToAsync_NullRule_ThrowsArgumentNullException()
    {
        ISpecificationRule<Order> rule = null!;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => rule.ToAsync());

        Assert.Equal("rule", exception.ParamName);
    }

    [Fact]
    public void Constructor_NullRule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AsyncSpecificationRuleAdapter<Order>(null!));
    }

    // ---------------------------------------------------------------------
    // Forwarding
    // ---------------------------------------------------------------------

    [Fact]
    public void Adapter_ForwardsAllSevenMetadataMembers()
    {
        OrderIsFlaggedRule rule = new OrderIsFlaggedRule();

        IAsyncBusinessRule<Order> adapted = rule.ToAsync();

        Assert.Equal(rule.Code, adapted.Code);
        Assert.Equal(rule.Message, adapted.Message);
        Assert.Equal(RuleSeverity.Warning, adapted.Severity);
        Assert.Equal("Flagged", adapted.Name);
        Assert.Equal("Flags need a manual look", adapted.Description);
        Assert.Equal("Review", adapted.Category);
        Assert.Equal(new[] { "review", "manual" }, adapted.Tags);
    }

    [Fact]
    public void Adapter_ExposesTheSpecificationsExpressions()
    {
        OrderIsOverdueRule rule = new OrderIsOverdueRule();

        AsyncSpecificationRuleAdapter<Order> adapter = new AsyncSpecificationRuleAdapter<Order>(rule);

        Assert.Same(rule.BrokenExpression, adapter.BrokenExpression);
        Assert.Same(rule.SatisfiedExpression, adapter.SatisfiedExpression);
    }

    // ---------------------------------------------------------------------
    // IsBrokenAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task IsBrokenAsync_SubjectViolates_ReturnsTrue()
    {
        IAsyncBusinessRule<Order> adapted = new OrderIsOverdueRule().ToAsync();

        bool broken = await adapted.IsBrokenAsync(SpecificationSamples.OverdueSmall(), new BusinessRuleContext(), TestToken);

        Assert.True(broken);
    }

    [Fact]
    public async Task IsBrokenAsync_SubjectSatisfies_ReturnsFalse()
    {
        IAsyncBusinessRule<Order> adapted = new OrderIsOverdueRule().ToAsync();

        bool broken = await adapted.IsBrokenAsync(SpecificationSamples.CurrentSmall(), new BusinessRuleContext(), TestToken);

        Assert.False(broken);
    }

    [Fact]
    public async Task IsBrokenAsync_AgreesWithTheSyncVerdictForEverySample()
    {
        OrderIsBigRule rule = new OrderIsBigRule();
        IAsyncBusinessRule<Order> adapted = rule.ToAsync();

        foreach (Order order in SpecificationSamples.All())
        {
            Assert.Equal(rule.IsBroken(order), await adapted.IsBrokenAsync(order, new BusinessRuleContext(), TestToken));
        }
    }

    [Fact]
    public async Task IsBrokenAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        IAsyncBusinessRule<Order> adapted = new OrderIsOverdueRule().ToAsync();
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => adapted.IsBrokenAsync(SpecificationSamples.OverdueSmall(), new BusinessRuleContext(), source.Token));
    }

    // ---------------------------------------------------------------------
    // EvaluateAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAsync_SubjectViolates_ReturnsTheSpecificationsResult()
    {
        IAsyncBusinessRule<Order> adapted = new OrderIsFlaggedRule().ToAsync();
        Order order = SpecificationSamples.PaidBigFlagged();

        BusinessRuleResult? result = await adapted.EvaluateAsync(order, new BusinessRuleContext(), TestToken);

        Assert.NotNull(result);
        Assert.Equal(OrderIsFlaggedRule.RuleCode, result.Code);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal($"Order {order.Id} is flagged", result.Detail);
    }

    [Fact]
    public async Task EvaluateAsync_SubjectSatisfies_ReturnsNull()
    {
        IAsyncBusinessRule<Order> adapted = new OrderIsFlaggedRule().ToAsync();

        BusinessRuleResult? result = await adapted.EvaluateAsync(SpecificationSamples.CurrentSmall(), new BusinessRuleContext(), TestToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task EvaluateAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        IAsyncBusinessRule<Order> adapted = new OrderIsOverdueRule().ToAsync();
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => adapted.EvaluateAsync(SpecificationSamples.OverdueSmall(), new BusinessRuleContext(), source.Token));
    }

    // ---------------------------------------------------------------------
    // Interoperability with the async world
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CheckAllAsync_TypedOverload_ThrowsWithTheSpecificationCodes()
    {
        IAsyncBusinessRule<Order>[] rules = { new OrderIsOverdueRule().ToAsync(), new OrderIsBigRule().ToAsync() };

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.For(SpecificationSamples.OverdueBig()).CheckAllAsync(new BusinessRuleContext(), rules, TestToken));

        Assert.Equal(new[] { OrderIsOverdueRule.RuleCode, OrderIsBigRule.RuleCode }, exception.BrokenRules.Select(r => r.Code));
    }

    [Fact]
    public async Task EvaluateAllAsync_TypedOverload_PassingSubject_AllPassed()
    {
        IAsyncBusinessRule<Order>[] rules = { new OrderIsOverdueRule().ToAsync(), new OrderIsBigRule().ToAsync() };

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.For(SpecificationSamples.CurrentSmall()).EvaluateAllAsync(rules, TestToken);

        Assert.True(result.AllPassed);
        Assert.Equal(2, result.PassedRules.Count);
    }

    [Fact]
    public async Task For_BindsTheAdapterToASubjectAsAnOrdinaryAsyncRule()
    {
        IAsyncBusinessRule bound = new OrderIsOverdueRule().ToAsync().For(SpecificationSamples.OverdueSmall());

        await RuleTestHelper.AssertBrokenAsync(bound, new BusinessRuleContext(), TestToken);
        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => bound.CheckAsync(new BusinessRuleContext(), TestToken));
        Assert.Equal(OrderIsOverdueRule.RuleCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task ToAsync_OnACombinator_KeepsTheCombinatorsVerdictAndCode()
    {
        ISpecificationRule<Order> combo = new OrderIsOverdueRule().AllOf(new OrderIsBigRule());
        IAsyncBusinessRule<Order> adapted = combo.ToAsync();

        Assert.Equal(combo.Code, adapted.Code);
        foreach (Order order in SpecificationSamples.All())
        {
            Assert.Equal(combo.IsBroken(order), await adapted.IsBrokenAsync(order, new BusinessRuleContext(), TestToken));
        }
    }

    [Fact]
    public async Task TypedAsyncAnd_OnAnAdapter_ComposesWithOtherAsyncRules()
    {
        IAsyncBusinessRule<Order> overdue = new OrderIsOverdueRule().ToAsync();
        IAsyncBusinessRule<Order> big = new OrderIsBigRule().ToAsync();

        IAsyncBusinessRule<Order> composed = overdue.And(big);

        Assert.True(await composed.IsBrokenAsync(SpecificationSamples.OverdueBig(), new BusinessRuleContext(), TestToken));
        Assert.False(await composed.IsBrokenAsync(SpecificationSamples.OverdueSmall(), new BusinessRuleContext(), TestToken));
    }
}
