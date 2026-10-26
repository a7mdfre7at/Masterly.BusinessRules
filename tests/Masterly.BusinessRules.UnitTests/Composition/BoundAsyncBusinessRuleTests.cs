namespace Masterly.BusinessRules.UnitTests;

public class BoundAsyncBusinessRuleTests
{
    private const string Code = "ORDER.TOO_LARGE";
    private const string Message = "Order has too many items.";
    private const string Name = "Too large";
    private const string Description = "Checks the item count";
    private const string Category = "Orders";
    private const string LimitKey = "limit";
    private const int DefaultLimit = 2;

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------
    // Construction
    // ---------------------------------------------------------------------

    [Fact]
    public void For_ReturnsBoundAsyncBusinessRuleOverRuleAndSubject()
    {
        IAsyncBusinessRule<int> rule = CreateRule();

        BoundAsyncBusinessRule<int> bound = rule.For(5);

        Assert.Same(rule, bound.Rule);
        Assert.Equal(5, bound.Subject);
        Assert.IsAssignableFrom<IAsyncBusinessRule>(bound);
    }

    [Fact]
    public void For_NullRule_ThrowsArgumentNullException()
    {
        IAsyncBusinessRule<int> rule = null!;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => rule.For(1));

        Assert.Equal("rule", exception.ParamName);
    }

    [Fact]
    public void Constructor_NullRule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BoundAsyncBusinessRule<int>(null!, 1));
    }

    [Fact]
    public async Task For_NullSubject_IsAllowedAndHandedToTheRule()
    {
        IAsyncBusinessRule<string> rule = AsyncBusinessRuleBuilder<string>.Create("S.NULL").WithMessage("m").WhenAsync(value => Task.FromResult(value == null)).Build();

        IAsyncBusinessRule bound = rule.For(null!);

        Assert.True(await bound.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    // ---------------------------------------------------------------------
    // Metadata forwarding
    // ---------------------------------------------------------------------

    [Fact]
    public void BoundRule_ForwardsAllSevenMetadataMembers()
    {
        IAsyncBusinessRule bound = CreateRule().For(1);

        Assert.Equal(Code, bound.Code);
        Assert.Equal(Message, bound.Message);
        Assert.Equal(RuleSeverity.Warning, bound.Severity);
        Assert.Equal(Name, bound.Name);
        Assert.Equal(Description, bound.Description);
        Assert.Equal(Category, bound.Category);
        Assert.Equal(new[] { "a", "b" }, bound.Tags);
    }

    // ---------------------------------------------------------------------
    // Evaluation
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(3, true)]
    [InlineData(1, false)]
    public async Task IsBrokenAsync_EvaluatesTypedRuleAgainstBoundSubject(int itemCount, bool expected)
    {
        IAsyncBusinessRule bound = CreateRule().For(itemCount);

        Assert.Equal(expected, await bound.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_ForwardsSubjectAndContext()
    {
        IAsyncBusinessRule bound = CreateRule().For(5);

        Assert.True(await bound.IsBrokenAsync(RuleTestHelper.CreateContext((LimitKey, 4)), TestToken));
        Assert.False(await bound.IsBrokenAsync(RuleTestHelper.CreateContext((LimitKey, 10)), TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_ForwardsCancellationToken()
    {
        CancellationToken? observed = null;
        IAsyncBusinessRule<int> rule = AsyncBusinessRuleBuilder<int>.Create(Code).WithMessage(Message)
            .WhenAsync((_, _, ct) => { observed = ct; return Task.FromResult(false); })
            .Build();
        using CancellationTokenSource source = new CancellationTokenSource();

        await rule.For(1).IsBrokenAsync(new BusinessRuleContext(), source.Token);

        Assert.Equal(source.Token, observed);
    }

    [Fact]
    public async Task IsBrokenAsync_CancelledToken_PropagatesCancellation()
    {
        IAsyncBusinessRule<int> rule = AsyncBusinessRuleBuilder<int>.Create(Code).WithMessage(Message)
            .WhenAsync(async (_, _, ct) => { await Task.Delay(1, ct); return true; })
            .Build();
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rule.For(1).IsBrokenAsync(new BusinessRuleContext(), source.Token));
    }

    [Fact]
    public async Task EvaluateAsync_WhenPassing_ReturnsNull()
    {
        IAsyncBusinessRule bound = CreateRule().For(1);

        Assert.Null(await bound.EvaluateAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task EvaluateAsync_WhenBroken_ReturnsResultWithContextAwareDetails()
    {
        IAsyncBusinessRule bound = CreateRule().For(5);

        BusinessRuleResult? result = await bound.EvaluateAsync(RuleTestHelper.CreateContext((LimitKey, 4)), TestToken);

        Assert.NotNull(result);
        Assert.Equal(Code, result.Code);
        Assert.Equal(Name, result.Name);
        Assert.Equal(5, result.Actual);
        Assert.Equal(4, result.Expected);
    }

    [Fact]
    public async Task CheckAsync_WhenBroken_ThrowsWithTheRuleResult()
    {
        IAsyncBusinessRule bound = CreateRule().For(3);

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(() => bound.CheckAsync(new BusinessRuleContext(), TestToken));

        Assert.Equal(Code, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckAsync_WhenPassing_DoesNotThrow()
    {
        IAsyncBusinessRule bound = CreateRule().For(1);

        await bound.CheckAsync(new BusinessRuleContext(), TestToken);
    }

    // ---------------------------------------------------------------------
    // Interoperability with the non-generic world
    // ---------------------------------------------------------------------

    [Fact]
    public async Task BoundRule_CanBeCached()
    {
        int evaluations = 0;
        IAsyncBusinessRule<int> rule = AsyncBusinessRuleBuilder<int>.Create(Code).WithMessage(Message).WhenAsync(_ => { evaluations++; return Task.FromResult(true); }).Build();
        IAsyncBusinessRule cached = rule.For(1).Cached(TimeSpan.FromMinutes(1));

        Assert.True(await cached.IsBrokenAsync(new BusinessRuleContext(), TestToken));
        Assert.True(await cached.IsBrokenAsync(new BusinessRuleContext(), TestToken));

        Assert.Equal(1, evaluations);
    }

    [Fact]
    public async Task BoundRule_CanBeSeverityOverridden()
    {
        IAsyncBusinessRule softened = CreateRule().For(3).WithSeverity(RuleSeverity.Info);

        Assert.Equal(RuleSeverity.Info, softened.Severity);
        Assert.True(await softened.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task BoundRule_CanBeComposedWithNonGenericOperators()
    {
        IAsyncBusinessRule composed = CreateRule().For(3).And(RuleTestHelper.CreateBrokenRule("OTHER").ToAsync());

        Assert.True(await composed.IsBrokenAsync(new BusinessRuleContext(), TestToken));
        Assert.Equal($"{Code}+OTHER", composed.Code);
    }

    [Fact]
    public async Task BoundRule_RunsThroughTheChecker()
    {
        IAsyncBusinessRule[] rules = { CreateRule().For(3), CreateRule().For(1) };

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), rules, false, TestToken);

        Assert.Equal(Code, Assert.Single(result.BrokenRules).Code);
        Assert.Same(rules[1], Assert.Single(result.PassedRules));
    }

    [Fact]
    public async Task BoundRule_ExplainsAsALeaf()
    {
        IAsyncBusinessRule bound = CreateRule().For(3);

        RuleExplanation explanation = await bound.ExplainAsync(new BusinessRuleContext(), TestToken);

        Assert.True(explanation.IsBroken);
        Assert.Equal(Code, explanation.Code);
        Assert.Empty(explanation.Children);
    }

    private static IAsyncBusinessRule<int> CreateRule()
    {
        return AsyncBusinessRuleBuilder<int>.Create(Code)
            .WithMessage(Message)
            .WithSeverity(RuleSeverity.Warning)
            .WithName(Name)
            .WithDescription(Description)
            .WithCategory(Category)
            .WithTags("a", "b")
            .WithActual((count, _) => count)
            .WithExpected((_, ctx) => ctx.TryGet(LimitKey, out int limit) ? limit : DefaultLimit)
            .WhenAsync((count, ctx) => Task.FromResult(count > (ctx.TryGet(LimitKey, out int limit) ? limit : DefaultLimit)))
            .Build();
    }
}
