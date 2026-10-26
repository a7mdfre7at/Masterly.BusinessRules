namespace Masterly.BusinessRules.UnitTests;

/// <summary>
/// Covers the structural And/Or/Not nodes on the async side: runtime types, walker interfaces,
/// context forwarding, short-circuiting, cancellation and member results.
/// </summary>
public class AsyncStructuralCompositionTests
{
    private const string FirstCode = "FIRST";
    private const string SecondCode = "SECOND";
    private const string FlagKey = "flag";

    [Fact]
    public void And_ReturnsAndAsyncBusinessRule_WithOperatorAndOperands()
    {
        IAsyncBusinessRule first = RuleTestHelper.CreatePassingAsyncRule(FirstCode);
        IAsyncBusinessRule second = RuleTestHelper.CreatePassingAsyncRule(SecondCode);

        IAsyncBusinessRule composed = first.And(second);

        AndAsyncBusinessRule node = Assert.IsType<AndAsyncBusinessRule>(composed);
        Assert.Equal(RuleOperator.And, node.Operator);
        Assert.Equal(new[] { first, second }, node.Operands);
    }

    [Fact]
    public void Or_ReturnsOrAsyncBusinessRule_WithOperatorAndOperands()
    {
        IAsyncBusinessRule first = RuleTestHelper.CreatePassingAsyncRule(FirstCode);
        IAsyncBusinessRule second = RuleTestHelper.CreatePassingAsyncRule(SecondCode);

        IAsyncBusinessRule composed = first.Or(second);

        OrAsyncBusinessRule node = Assert.IsType<OrAsyncBusinessRule>(composed);
        Assert.Equal(RuleOperator.Or, node.Operator);
        Assert.Equal(new[] { first, second }, node.Operands);
    }

    [Fact]
    public void Not_ReturnsNotAsyncBusinessRule_WithSingleOperand()
    {
        IAsyncBusinessRule operand = RuleTestHelper.CreatePassingAsyncRule(FirstCode);

        IAsyncBusinessRule composed = operand.Not();

        NotAsyncBusinessRule node = Assert.IsType<NotAsyncBusinessRule>(composed);
        Assert.Equal(RuleOperator.Not, node.Operator);
        IAsyncBusinessRule only = Assert.Single(node.Operands);
        Assert.Same(operand, only);
    }

    [Fact]
    public void And_ImplementsIComposedAsyncBusinessRule()
    {
        IAsyncBusinessRule composed = RuleTestHelper.CreatePassingAsyncRule(FirstCode).And(RuleTestHelper.CreatePassingAsyncRule(SecondCode));

        Assert.IsAssignableFrom<IComposedAsyncBusinessRule>(composed);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Or_IsBrokenAsync_ForwardsContextToBothOperands(bool flag, bool expectedBroken)
    {
        IAsyncBusinessRule composed = CreateContextReadingRule(FirstCode).Or(CreateContextReadingRule(SecondCode));
        BusinessRuleContext context = RuleTestHelper.CreateContext((FlagKey, flag));

        bool actualBroken = await composed.IsBrokenAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(expectedBroken, actualBroken);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Not_IsBrokenAsync_ForwardsContextToOperand(bool flag, bool expectedBroken)
    {
        IAsyncBusinessRule composed = CreateContextReadingRule(FirstCode).Not();
        BusinessRuleContext context = RuleTestHelper.CreateContext((FlagKey, flag));

        bool actualBroken = await composed.IsBrokenAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(expectedBroken, actualBroken);
    }

    [Fact]
    public async Task And_IsBrokenAsync_EvaluatesEachOperandExactlyOnce()
    {
        int firstEvaluations = 0;
        int secondEvaluations = 0;
        IAsyncBusinessRule composed = CreateCountingRule(FirstCode, broken: true, () => firstEvaluations++)
            .And(CreateCountingRule(SecondCode, broken: true, () => secondEvaluations++));

        await composed.IsBrokenAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(1, firstEvaluations);
        Assert.Equal(1, secondEvaluations);
    }

    [Fact]
    public async Task And_CheckAsync_WhenBroken_ResultDetailsContainBothOperandResults()
    {
        IAsyncBusinessRule composed = RuleTestHelper.CreateBrokenAsyncRule(FirstCode).And(RuleTestHelper.CreateBrokenAsyncRule(SecondCode));

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => composed.CheckAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(new[] { FirstCode, SecondCode }, result.Details.Select(detail => detail.Code));
    }

    [Fact]
    public async Task Or_EvaluateAsync_WhenOnlySecondBroken_ResultDetailsContainSecondOnly()
    {
        IAsyncBusinessRule composed = RuleTestHelper.CreatePassingAsyncRule(FirstCode).Or(RuleTestHelper.CreateBrokenAsyncRule(SecondCode));

        BusinessRuleResult? result = await composed.EvaluateAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        BusinessRuleResult detail = Assert.Single(result.Details);
        Assert.Equal(SecondCode, detail.Code);
    }

    [Fact]
    public async Task Or_EvaluateAsync_WhenPassing_ReturnsNull()
    {
        IAsyncBusinessRule composed = RuleTestHelper.CreatePassingAsyncRule(FirstCode).Or(RuleTestHelper.CreatePassingAsyncRule(SecondCode));

        BusinessRuleResult? result = await composed.EvaluateAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task Not_CheckAsync_WhenBroken_ResultHasNoDetails()
    {
        IAsyncBusinessRule composed = RuleTestHelper.CreatePassingAsyncRule(FirstCode).Not();

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => composed.CheckAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal($"!{FirstCode}", result.Code);
        Assert.Empty(result.Details);
    }

    [Fact]
    public void And_Message_KeepsLegacySynthesisedText()
    {
        IAsyncBusinessRule composed = RuleTestHelper.CreateBrokenAsyncRule(FirstCode, "first").And(RuleTestHelper.CreateBrokenAsyncRule(SecondCode, "second"));

        Assert.Equal("first AND second", composed.Message);
        Assert.Equal($"{FirstCode}+{SecondCode}", composed.Code);
    }

    [Fact]
    public void Or_Message_KeepsLegacySynthesisedText()
    {
        IAsyncBusinessRule composed = RuleTestHelper.CreateBrokenAsyncRule(FirstCode, "first").Or(RuleTestHelper.CreateBrokenAsyncRule(SecondCode, "second"));

        Assert.Equal("first OR second", composed.Message);
        Assert.Equal($"{FirstCode}|{SecondCode}", composed.Code);
    }

    [Fact]
    public void Not_Message_KeepsLegacySynthesisedText()
    {
        IAsyncBusinessRule composed = RuleTestHelper.CreateBrokenAsyncRule(FirstCode, "first").Not();

        Assert.Equal("NOT: first", composed.Message);
    }

    [Fact]
    public void AndAsyncBusinessRule_Constructor_NullOperand_ThrowsArgumentNullException()
    {
        IAsyncBusinessRule rule = RuleTestHelper.CreatePassingAsyncRule(FirstCode);

        Assert.Throws<ArgumentNullException>(() => new AndAsyncBusinessRule(null!, rule));
        Assert.Throws<ArgumentNullException>(() => new AndAsyncBusinessRule(rule, null!));
    }

    [Fact]
    public void OrAsyncBusinessRule_Constructor_NullOperand_ThrowsArgumentNullException()
    {
        IAsyncBusinessRule rule = RuleTestHelper.CreatePassingAsyncRule(FirstCode);

        Assert.Throws<ArgumentNullException>(() => new OrAsyncBusinessRule(null!, rule));
        Assert.Throws<ArgumentNullException>(() => new OrAsyncBusinessRule(rule, null!));
    }

    [Fact]
    public void NotAsyncBusinessRule_Constructor_NullOperand_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new NotAsyncBusinessRule(null!));
    }

    [Fact]
    public async Task And_IsBrokenAsync_WithCancelledToken_ThrowsBeforeEvaluatingOperands()
    {
        int evaluations = 0;
        IAsyncBusinessRule composed = CreateCountingRule(FirstCode, broken: true, () => evaluations++)
            .And(CreateCountingRule(SecondCode, broken: true, () => evaluations++));
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => composed.IsBrokenAsync(RuleTestHelper.CreateContext(), cancellation.Token));

        Assert.Equal(0, evaluations);
    }

    [Fact]
    public void NestedComposition_KeepsTreeShape()
    {
        IAsyncBusinessRule a = RuleTestHelper.CreatePassingAsyncRule("A");
        IAsyncBusinessRule b = RuleTestHelper.CreatePassingAsyncRule("B");
        IAsyncBusinessRule c = RuleTestHelper.CreatePassingAsyncRule("C");

        IAsyncBusinessRule composed = a.And(b).Or(c.Not());

        IComposedAsyncBusinessRule root = Assert.IsAssignableFrom<IComposedAsyncBusinessRule>(composed);
        Assert.Equal(RuleOperator.Or, root.Operator);
        Assert.Equal(RuleOperator.And, Assert.IsAssignableFrom<IComposedAsyncBusinessRule>(root.Operands[0]).Operator);
        Assert.Equal(RuleOperator.Not, Assert.IsAssignableFrom<IComposedAsyncBusinessRule>(root.Operands[1]).Operator);
        Assert.Equal("A+B|!C", composed.Code);
    }

    [Fact]
    public void CompositeAsyncBusinessRule_WithRules_ImplementsIComposed_WithOperatorAll()
    {
        IAsyncBusinessRule first = RuleTestHelper.CreatePassingAsyncRule(FirstCode);
        IAsyncBusinessRule second = RuleTestHelper.CreatePassingAsyncRule(SecondCode);
        CompositeAsyncBusinessRule composite = new CompositeAsyncBusinessRule(new[] { first, second });

        IComposedAsyncBusinessRule composed = Assert.IsAssignableFrom<IComposedAsyncBusinessRule>(composite);

        Assert.Equal(RuleOperator.All, composed.Operator);
        Assert.Equal(new[] { first, second }, composed.Operands);
    }

    [Fact]
    public void CompositeAsyncBusinessRule_WithDelegate_HasNoOperands()
    {
        CompositeAsyncBusinessRule composite = new CompositeAsyncBusinessRule((ctx, ct) => Task.FromResult(true), "message", "CODE");

        IComposedAsyncBusinessRule composed = Assert.IsAssignableFrom<IComposedAsyncBusinessRule>(composite);

        Assert.Equal(RuleOperator.All, composed.Operator);
        Assert.Empty(composed.Operands);
        Assert.Null(composite.Rules);
    }

    [Fact]
    public async Task CompositeAsyncBusinessRule_EvaluateAsync_WhenBroken_CarriesEachBrokenChildInDetails()
    {
        CompositeAsyncBusinessRule composite = new CompositeAsyncBusinessRule(new[]
        {
            RuleTestHelper.CreateBrokenAsyncRule(FirstCode),
            RuleTestHelper.CreatePassingAsyncRule("MIDDLE"),
            RuleTestHelper.CreateBrokenAsyncRule(SecondCode)
        });

        BusinessRuleResult? result = await composite.EvaluateAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("CompositeAsyncRule", result.Code);
        Assert.Equal(new[] { FirstCode, SecondCode }, result.Details.Select(detail => detail.Code));
    }

    [Fact]
    public async Task CompositeAsyncBusinessRule_EmptyRules_PassesAndEvaluateAllIsEmpty()
    {
        CompositeAsyncBusinessRule composite = new CompositeAsyncBusinessRule(Array.Empty<IAsyncBusinessRule>());
        BusinessRuleContext context = RuleTestHelper.CreateContext();

        Assert.False(await composite.IsBrokenAsync(context, TestContext.Current.CancellationToken));
        Assert.Empty(await composite.EvaluateAllAsync(context, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void CachedAsyncBusinessRule_ImplementsIDecorated_ExposingInnerRule()
    {
        IAsyncBusinessRule inner = RuleTestHelper.CreatePassingAsyncRule(FirstCode);

        IAsyncBusinessRule cached = inner.Cached(TimeSpan.FromMinutes(1));

        IDecoratedAsyncBusinessRule decorated = Assert.IsAssignableFrom<IDecoratedAsyncBusinessRule>(cached);
        Assert.Same(inner, decorated.InnerRule);
    }

    [Fact]
    public async Task CachedAsyncBusinessRule_EvaluateAsync_WhenInnerGroupBroken_CarriesMemberDetails()
    {
        const string groupCode = "GROUP";
        IAsyncBusinessRule group = AsyncRuleGroup.All(groupCode, groupCode, RuleTestHelper.CreateBrokenAsyncRule(FirstCode), RuleTestHelper.CreateBrokenAsyncRule(SecondCode));
        IAsyncBusinessRule cached = group.Cached(TimeSpan.FromMinutes(1));

        BusinessRuleResult? result = await cached.EvaluateAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(groupCode, result.Code);
        Assert.Equal(new[] { FirstCode, SecondCode }, result.Details.Select(detail => detail.Code));
    }

    [Fact]
    public async Task CachedAsyncBusinessRule_CheckAsync_WhenInnerGroupBroken_ThrowsResultWithMemberDetails()
    {
        const string groupCode = "GROUP";
        IAsyncBusinessRule cached = AsyncRuleGroup.All(groupCode, groupCode, RuleTestHelper.CreateBrokenAsyncRule(FirstCode))
            .Cached(TimeSpan.FromMinutes(1));

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => cached.CheckAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(groupCode, result.Code);
        Assert.Equal(FirstCode, Assert.Single(result.Details).Code);
    }

    [Fact]
    public async Task CachedAsyncBusinessRule_EvaluateAsync_OnCacheHit_ReturnsResultWithoutDetails()
    {
        const string groupCode = "GROUP";
        IAsyncBusinessRule cached = AsyncRuleGroup.All(groupCode, groupCode, RuleTestHelper.CreateBrokenAsyncRule(FirstCode))
            .Cached(TimeSpan.FromMinutes(1));
        BusinessRuleContext context = RuleTestHelper.CreateContext();
        Assert.True(await cached.IsBrokenAsync(context, TestContext.Current.CancellationToken));

        BusinessRuleResult? result = await cached.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(groupCode, result.Code);
        Assert.Empty(result.Details);
    }

    [Fact]
    public async Task AsyncBusinessRuleChecker_CheckAllAsync_WithCachedGroup_SurfacesMemberDetails()
    {
        const string groupCode = "GROUP";
        IAsyncBusinessRule cached = AsyncRuleGroup.All(groupCode, groupCode, RuleTestHelper.CreateBrokenAsyncRule(FirstCode))
            .Cached(TimeSpan.FromMinutes(1));

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(RuleTestHelper.CreateContext(), cached));

        Assert.Equal(FirstCode, Assert.Single(Assert.Single(exception.BrokenRules).Details).Code);
    }

    [Fact]
    public void ConditionalAsyncBusinessRule_ImplementsIDecorated_ExposingInnerRule()
    {
        IAsyncBusinessRule inner = RuleTestHelper.CreatePassingAsyncRule(FirstCode);

        IAsyncBusinessRule conditional = inner.When(ctx => true);

        IDecoratedAsyncBusinessRule decorated = Assert.IsAssignableFrom<IDecoratedAsyncBusinessRule>(conditional);
        Assert.Same(inner, decorated.InnerRule);
    }

    [Fact]
    public void SyncToAsyncRuleAdapter_ExposesInnerRule()
    {
        IBusinessRule inner = RuleTestHelper.CreatePassingRule(FirstCode);

        SyncToAsyncRuleAdapter adapter = new SyncToAsyncRuleAdapter(inner);

        Assert.Same(inner, adapter.InnerRule);
    }

    [Fact]
    public async Task AsyncBusinessRuleChecker_CheckAllAsync_WithComposedRule_EvaluatesOperandsOnce()
    {
        int evaluations = 0;
        IAsyncBusinessRule composed = CreateCountingRule(FirstCode, broken: true, () => evaluations++)
            .And(CreateCountingRule(SecondCode, broken: true, () => evaluations++));

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(RuleTestHelper.CreateContext(), composed));

        Assert.Equal(2, evaluations);
        Assert.Equal(2, Assert.Single(exception.BrokenRules).Details.Count);
    }

    private static IAsyncBusinessRule CreateContextReadingRule(string code)
    {
        return AsyncBusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .WhenAsync((ctx, ct) => Task.FromResult(ctx.Get<bool>(FlagKey)))
            .Build();
    }

    private static IAsyncBusinessRule CreateCountingRule(string code, bool broken, Action onEvaluated)
    {
        return AsyncBusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .WhenAsync((ctx, ct) =>
            {
                onEvaluated();
                return Task.FromResult(broken);
            })
            .Build();
    }
}
