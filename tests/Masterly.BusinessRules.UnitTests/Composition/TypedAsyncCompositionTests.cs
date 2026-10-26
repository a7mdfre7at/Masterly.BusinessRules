namespace Masterly.BusinessRules.UnitTests;

public class TypedAsyncCompositionTests
{
    private const string FlagKey = "flag";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------
    // Truth tables
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task And_IsBrokenOnlyWhenBothOperandsAreBroken(bool firstBroken, bool secondBroken, bool expected)
    {
        IAsyncBusinessRule<int> composed = Rule("A", firstBroken).And(Rule("B", secondBroken));

        Assert.Equal(expected, await composed.IsBrokenAsync(0, new BusinessRuleContext(), TestToken));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public async Task Or_IsBrokenWhenEitherOperandIsBroken(bool firstBroken, bool secondBroken, bool expected)
    {
        IAsyncBusinessRule<int> composed = Rule("A", firstBroken).Or(Rule("B", secondBroken));

        Assert.Equal(expected, await composed.IsBrokenAsync(0, new BusinessRuleContext(), TestToken));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Not_InvertsTheOperand(bool operandBroken, bool expected)
    {
        IAsyncBusinessRule<int> composed = Rule("A", operandBroken).Not();

        Assert.Equal(expected, await composed.IsBrokenAsync(0, new BusinessRuleContext(), TestToken));
    }

    // ---------------------------------------------------------------------
    // Subject, context and token forwarding
    // ---------------------------------------------------------------------

    [Fact]
    public async Task And_ForwardsTheSubjectToBothOperands()
    {
        List<int> seen = new List<int>();
        IAsyncBusinessRule<int> composed = Recording("A", seen, broken: true).And(Recording("B", seen, broken: true));

        await composed.IsBrokenAsync(42, new BusinessRuleContext(), TestToken);

        Assert.Equal(new[] { 42, 42 }, seen);
    }

    [Fact]
    public async Task Or_ForwardsTheSubjectToBothOperands()
    {
        List<int> seen = new List<int>();
        IAsyncBusinessRule<int> composed = Recording("A", seen, broken: false).Or(Recording("B", seen, broken: false));

        await composed.IsBrokenAsync(7, new BusinessRuleContext(), TestToken);

        Assert.Equal(new[] { 7, 7 }, seen);
    }

    [Fact]
    public async Task Not_ForwardsTheSubjectToTheOperand()
    {
        List<int> seen = new List<int>();
        IAsyncBusinessRule<int> composed = Recording("A", seen, broken: true).Not();

        await composed.IsBrokenAsync(9, new BusinessRuleContext(), TestToken);

        Assert.Equal(new[] { 9 }, seen);
    }

    [Fact]
    public async Task And_ForwardsTheContextToBothOperands()
    {
        IAsyncBusinessRule<int> composed = ContextRule("A").And(ContextRule("B"));

        Assert.True(await composed.IsBrokenAsync(0, RuleTestHelper.CreateContext((FlagKey, true)), TestToken));
        Assert.False(await composed.IsBrokenAsync(0, RuleTestHelper.CreateContext((FlagKey, false)), TestToken));
    }

    [Fact]
    public async Task Or_ForwardsTheContextToBothOperands()
    {
        IAsyncBusinessRule<int> composed = Rule("A", false).Or(ContextRule("B"));

        Assert.True(await composed.IsBrokenAsync(0, RuleTestHelper.CreateContext((FlagKey, true)), TestToken));
        Assert.False(await composed.IsBrokenAsync(0, RuleTestHelper.CreateContext((FlagKey, false)), TestToken));
    }

    [Fact]
    public async Task Not_ForwardsTheContextToTheOperand()
    {
        IAsyncBusinessRule<int> composed = ContextRule("A").Not();

        Assert.False(await composed.IsBrokenAsync(0, RuleTestHelper.CreateContext((FlagKey, true)), TestToken));
        Assert.True(await composed.IsBrokenAsync(0, RuleTestHelper.CreateContext((FlagKey, false)), TestToken));
    }

    [Fact]
    public async Task Operators_ForwardTheCancellationTokenToOperands()
    {
        List<CancellationToken> seen = new List<CancellationToken>();
        IAsyncBusinessRule<int> composed = TokenRecording("A", seen).And(TokenRecording("B", seen)).Not();
        using CancellationTokenSource source = new CancellationTokenSource();

        await composed.IsBrokenAsync(0, new BusinessRuleContext(), source.Token);

        Assert.Equal(new[] { source.Token, source.Token }, seen);
    }

    [Fact]
    public async Task Operators_CancelledToken_PropagatesCancellation()
    {
        IAsyncBusinessRule<int> cancelling = AsyncBusinessRuleBuilder<int>.Create("C").WithMessage("c")
            .WhenAsync(async (_, _, ct) => { await Task.Delay(1, ct); return true; })
            .Build();
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelling.And(Rule("B", true)).IsBrokenAsync(0, new BusinessRuleContext(), source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelling.Or(Rule("B", true)).IsBrokenAsync(0, new BusinessRuleContext(), source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelling.Not().IsBrokenAsync(0, new BusinessRuleContext(), source.Token));
    }

    // ---------------------------------------------------------------------
    // Short-circuit
    // ---------------------------------------------------------------------

    [Fact]
    public async Task And_WhenFirstPasses_DoesNotEvaluateSecond()
    {
        int secondCalls = 0;
        IAsyncBusinessRule<int> composed = Rule("A", false).And(Counting("B", true, () => secondCalls++));

        Assert.False(await composed.IsBrokenAsync(0, new BusinessRuleContext(), TestToken));
        Assert.Equal(0, secondCalls);
    }

    [Fact]
    public async Task And_WhenFirstBroken_EvaluatesSecond()
    {
        int secondCalls = 0;
        IAsyncBusinessRule<int> composed = Rule("A", true).And(Counting("B", false, () => secondCalls++));

        Assert.False(await composed.IsBrokenAsync(0, new BusinessRuleContext(), TestToken));
        Assert.Equal(1, secondCalls);
    }

    [Fact]
    public async Task Or_WhenFirstBroken_DoesNotEvaluateSecond()
    {
        int secondCalls = 0;
        IAsyncBusinessRule<int> composed = Rule("A", true).Or(Counting("B", false, () => secondCalls++));

        Assert.True(await composed.IsBrokenAsync(0, new BusinessRuleContext(), TestToken));
        Assert.Equal(0, secondCalls);
    }

    [Fact]
    public async Task Or_WhenFirstPasses_EvaluatesSecond()
    {
        int secondCalls = 0;
        IAsyncBusinessRule<int> composed = Rule("A", false).Or(Counting("B", true, () => secondCalls++));

        Assert.True(await composed.IsBrokenAsync(0, new BusinessRuleContext(), TestToken));
        Assert.Equal(1, secondCalls);
    }

    // ---------------------------------------------------------------------
    // Metadata and structure
    // ---------------------------------------------------------------------

    [Fact]
    public void And_SynthesisesCodeMessageAndMetadata()
    {
        IAsyncBusinessRule<int> first = Rule("A", true);
        IAsyncBusinessRule<int> second = Rule("B", true);

        IAsyncBusinessRule<int> composed = first.And(second);

        AndAsyncBusinessRule<int> node = Assert.IsType<AndAsyncBusinessRule<int>>(composed);
        Assert.Equal("A+B", node.Code);
        Assert.Equal("msg A AND msg B", node.Message);
        Assert.Equal(RuleSeverity.Error, node.Severity);
        Assert.Equal("AndAsyncBusinessRule", node.Name);
        Assert.Equal("Broken only when both operands are broken.", node.Description);
        Assert.Equal(string.Empty, node.Category);
        Assert.Empty(node.Tags);
        Assert.Equal(RuleOperator.And, node.Operator);
        Assert.Equal(new[] { first, second }, node.Operands);
    }

    [Fact]
    public void Or_SynthesisesCodeMessageAndMetadata()
    {
        IAsyncBusinessRule<int> composed = Rule("A", true).Or(Rule("B", true));

        OrAsyncBusinessRule<int> node = Assert.IsType<OrAsyncBusinessRule<int>>(composed);
        Assert.Equal("A|B", node.Code);
        Assert.Equal("msg A OR msg B", node.Message);
        Assert.Equal("OrAsyncBusinessRule", node.Name);
        Assert.Equal("Broken when either operand is broken.", node.Description);
        Assert.Equal(RuleOperator.Or, node.Operator);
    }

    [Fact]
    public void Not_SynthesisesCodeMessageAndMetadata()
    {
        IAsyncBusinessRule<int> operand = Rule("A", true);

        IAsyncBusinessRule<int> composed = operand.Not();

        NotAsyncBusinessRule<int> node = Assert.IsType<NotAsyncBusinessRule<int>>(composed);
        Assert.Equal("!A", node.Code);
        Assert.Equal("NOT: msg A", node.Message);
        Assert.Equal("NotAsyncBusinessRule", node.Name);
        Assert.Equal("Broken when the operand passes.", node.Description);
        Assert.Equal(RuleOperator.Not, node.Operator);
        Assert.Same(operand, Assert.Single(node.Operands));
    }

    [Fact]
    public void TypedNodes_ReportTheSameCodesAndMessagesAsTheNonGenericAsyncNodes()
    {
        IAsyncBusinessRule<int> typed = Rule("A", true).And(Rule("B", true)).Or(Rule("C", true).Not());
        IAsyncBusinessRule plain = PlainRule("A").And(PlainRule("B")).Or(PlainRule("C").Not());

        Assert.Equal(plain.Code, typed.Code);
        Assert.Equal(plain.Message, typed.Message);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsNodeResultWhenBrokenAndNullOtherwise()
    {
        IAsyncBusinessRule<int> broken = Rule("A", true).And(Rule("B", true));
        IAsyncBusinessRule<int> passing = Rule("A", true).And(Rule("B", false));

        BusinessRuleResult? brokenResult = await broken.EvaluateAsync(0, new BusinessRuleContext(), TestToken);
        BusinessRuleResult? passingResult = await passing.EvaluateAsync(0, new BusinessRuleContext(), TestToken);

        Assert.NotNull(brokenResult);
        Assert.Equal("A+B", brokenResult.Code);
        Assert.Equal("AndAsyncBusinessRule", brokenResult.Name);
        Assert.Null(passingResult);
    }

    [Fact]
    public void Operators_NullOperands_ThrowArgumentNullException()
    {
        IAsyncBusinessRule<int> rule = Rule("A", true);
        IAsyncBusinessRule<int> missing = null!;

        Assert.Throws<ArgumentNullException>(() => rule.And(missing));
        Assert.Throws<ArgumentNullException>(() => missing.And(rule));
        Assert.Throws<ArgumentNullException>(() => rule.Or(missing));
        Assert.Throws<ArgumentNullException>(() => missing.Or(rule));
        Assert.Throws<ArgumentNullException>(() => missing.Not());
    }

    // ---------------------------------------------------------------------
    // Chaining, binding and checking
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Operators_Chain()
    {
        IAsyncBusinessRule<int> composed = Rule("A", true).And(Rule("B", true)).Or(Rule("C", false)).Not();

        Assert.False(await composed.IsBrokenAsync(0, new BusinessRuleContext(), TestToken));
        Assert.Equal("!A+B|C", composed.Code);
    }

    [Fact]
    public async Task Composed_BoundToSubject_RunsThroughCheckAndChecker()
    {
        IAsyncBusinessRule<int> composed = Threshold("BIG", 10).And(Threshold("HUGE", 100));

        await Assert.ThrowsAsync<BusinessRuleValidationException>(() => composed.For(500).CheckAsync(new BusinessRuleContext(), TestToken));
        await composed.For(50).CheckAsync(new BusinessRuleContext(), TestToken);
        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(500, new[] { composed }, TestToken);
        Assert.Equal("BIG+HUGE", Assert.Single(result.BrokenRules).Code);
    }

    // ---------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------

    private static IAsyncBusinessRule<int> Rule(string code, bool broken)
    {
        return AsyncBusinessRuleBuilder<int>.Create(code).WithMessage($"msg {code}").WhenAsync(_ => Task.FromResult(broken)).Build();
    }

    private static IAsyncBusinessRule PlainRule(string code)
    {
        return AsyncBusinessRuleBuilder.Create(code).WithMessage($"msg {code}").WhenAsync(_ => Task.FromResult(true)).Build();
    }

    private static IAsyncBusinessRule<int> Counting(string code, bool broken, Action onEvaluate)
    {
        return AsyncBusinessRuleBuilder<int>.Create(code).WithMessage($"msg {code}").WhenAsync(_ => { onEvaluate(); return Task.FromResult(broken); }).Build();
    }

    private static IAsyncBusinessRule<int> Recording(string code, List<int> seen, bool broken)
    {
        return AsyncBusinessRuleBuilder<int>.Create(code).WithMessage($"msg {code}").WhenAsync(subject => { seen.Add(subject); return Task.FromResult(broken); }).Build();
    }

    private static IAsyncBusinessRule<int> TokenRecording(string code, List<CancellationToken> seen)
    {
        return AsyncBusinessRuleBuilder<int>.Create(code).WithMessage($"msg {code}").WhenAsync((_, _, ct) => { seen.Add(ct); return Task.FromResult(true); }).Build();
    }

    private static IAsyncBusinessRule<int> ContextRule(string code)
    {
        return AsyncBusinessRuleBuilder<int>.Create(code).WithMessage($"msg {code}").WhenAsync((_, ctx) => Task.FromResult(ctx.Get<bool>(FlagKey))).Build();
    }

    private static IAsyncBusinessRule<int> Threshold(string code, int limit)
    {
        return AsyncBusinessRuleBuilder<int>.Create(code).WithMessage($"msg {code}").WhenAsync(value => Task.FromResult(value > limit)).Build();
    }
}
