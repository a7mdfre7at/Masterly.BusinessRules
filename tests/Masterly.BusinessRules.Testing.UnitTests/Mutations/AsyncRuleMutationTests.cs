namespace Masterly.BusinessRules.Testing.UnitTests;

public class AsyncRuleMutationTests
{
    private const string Code = "EMAIL.UNIQUE";
    private const string Message = "Email must be unique";
    private const string Name = "UniqueEmail";
    private const string Description = "Checks the email against the repository";
    private const string Category = "Identity";
    private const string FlagKey = "flag";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public void Invert_ReturnsInvertedAsyncBusinessRule_ForwardingMetadata()
    {
        IAsyncBusinessRule inner = CreateRule(broken: true);

        IAsyncBusinessRule mutant = inner.Invert();

        InvertedAsyncBusinessRule decorator = Assert.IsType<InvertedAsyncBusinessRule>(mutant);
        Assert.Same(inner, decorator.InnerRule);
        Assert.IsAssignableFrom<IDecoratedAsyncBusinessRule>(mutant);
        AssertMetadataForwarded(mutant, RuleSeverity.Error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Invert_FlipsVerdict(bool broken)
    {
        IAsyncBusinessRule mutant = CreateRule(broken).Invert();

        Assert.Equal(!broken, await mutant.IsBrokenAsync(new BusinessRuleContext(), TestToken));
        BusinessRuleResult? result = await mutant.EvaluateAsync(new BusinessRuleContext(), TestToken);
        Assert.Equal(!broken, result != null);
    }

    [Fact]
    public async Task Invert_ForwardsContextToInnerRule()
    {
        IAsyncBusinessRule contextRule = AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message)
            .WhenAsync((ctx, ct) => Task.FromResult(ctx.Get<bool>(FlagKey))).Build();
        IAsyncBusinessRule mutant = contextRule.Invert();

        Assert.False(await mutant.IsBrokenAsync(RuleTestHelper.CreateContext((FlagKey, true)), TestToken));
        Assert.True(await mutant.IsBrokenAsync(RuleTestHelper.CreateContext((FlagKey, false)), TestToken));
    }

    [Fact]
    public async Task Invert_EvaluateAsync_WhenBroken_CarriesTheRuleMetadata()
    {
        IAsyncBusinessRule mutant = CreateRule(broken: false).Invert();

        BusinessRuleResult? result = await mutant.EvaluateAsync(new BusinessRuleContext(), TestToken);

        Assert.NotNull(result);
        Assert.Equal(Code, result.Code);
        Assert.Equal(Message, result.Message);
        Assert.Equal(RuleSeverity.Error, result.Severity);
        Assert.Equal(Name, result.Name);
        Assert.Equal(Category, result.Category);
        Assert.Equal(new[] { "x" }, result.Tags);
    }

    [Fact]
    public async Task Invert_CheckAsync_ThrowsWhenInnerPassesAndPassesWhenInnerBroken()
    {
        IAsyncBusinessRule inverted = CreateRule(broken: false).Invert();
        IAsyncBusinessRule silenced = CreateRule(broken: true).Invert();

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(() => inverted.CheckAsync(new BusinessRuleContext(), TestToken));
        await silenced.CheckAsync(new BusinessRuleContext(), TestToken);

        Assert.Equal(Code, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task Invert_WhenCancelled_PropagatesCancellation()
    {
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();
        IAsyncBusinessRule honouring = AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message)
            .WhenAsync((ctx, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(true);
            })
            .Build();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => honouring.Invert().IsBrokenAsync(new BusinessRuleContext(), source.Token));
    }

    [Fact]
    public void Invert_NullRule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((IAsyncBusinessRule)null!).Invert());
    }

    [Fact]
    public void AlwaysPass_ReturnsAlwaysPassingAsyncBusinessRule_ForwardingMetadata()
    {
        IAsyncBusinessRule inner = CreateRule(broken: true);

        IAsyncBusinessRule mutant = inner.AlwaysPass();

        AlwaysPassingAsyncBusinessRule decorator = Assert.IsType<AlwaysPassingAsyncBusinessRule>(mutant);
        Assert.Same(inner, decorator.InnerRule);
        AssertMetadataForwarded(mutant, RuleSeverity.Error);
    }

    [Fact]
    public async Task AlwaysPass_NeverBreaksAndNeverEvaluatesInner()
    {
        int evaluations = 0;
        IAsyncBusinessRule counting = AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WhenAsync((ctx, ct) =>
        {
            evaluations++;
            return Task.FromResult(true);
        }).Build();

        IAsyncBusinessRule mutant = counting.AlwaysPass();

        Assert.False(await mutant.IsBrokenAsync(new BusinessRuleContext(), TestToken));
        Assert.Null(await mutant.EvaluateAsync(new BusinessRuleContext(), TestToken));
        await mutant.CheckAsync(new BusinessRuleContext(), TestToken);
        Assert.Equal(0, evaluations);
    }

    [Fact]
    public async Task AlwaysPass_WhenCancelled_ThrowsOperationCanceledException()
    {
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();
        IAsyncBusinessRule mutant = CreateRule(broken: true).AlwaysPass();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mutant.IsBrokenAsync(new BusinessRuleContext(), source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mutant.EvaluateAsync(new BusinessRuleContext(), source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mutant.CheckAsync(new BusinessRuleContext(), source.Token));
    }

    [Fact]
    public void AlwaysPass_NullRule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((IAsyncBusinessRule)null!).AlwaysPass());
    }

    [Theory]
    [InlineData(RuleSeverity.Error, RuleSeverity.Warning)]
    [InlineData(RuleSeverity.Warning, RuleSeverity.Info)]
    [InlineData(RuleSeverity.Info, RuleSeverity.Info)]
    public async Task DowngradeSeverity_ReportsOneLevelLower(RuleSeverity original, RuleSeverity expected)
    {
        IAsyncBusinessRule inner = CreateRule(broken: true, severity: original);

        IAsyncBusinessRule mutant = inner.DowngradeSeverity();

        SeverityOverrideAsyncBusinessRule decorator = Assert.IsType<SeverityOverrideAsyncBusinessRule>(mutant);
        Assert.Same(inner, decorator.InnerRule);
        Assert.Equal(expected, mutant.Severity);
        BusinessRuleResult? result = await mutant.EvaluateAsync(new BusinessRuleContext(), TestToken);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Severity);
    }

    [Fact]
    public void DowngradeSeverity_NullRule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((IAsyncBusinessRule)null!).DowngradeSeverity());
    }

    [Fact]
    public async Task SmokeMode_SuiteAssertingTheHappyPath_NoticesTheInvertedMutant()
    {
        IAsyncBusinessRule mutant = CreateRule(broken: false).Invert();

        await Assert.ThrowsAsync<BusinessRuleValidationException>(() => AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), mutant));
    }

    [Fact]
    public async Task ExplainAsync_Inverted_RecordsInvertedNodeWithTheWrappedRuleAsChild()
    {
        IAsyncBusinessRule mutant = CreateRule(broken: false).Invert();

        RuleExplanation explanation = await mutant.ExplainAsync(new BusinessRuleContext(), TestToken);

        Assert.Equal("INVERTED", explanation.Operator);
        Assert.True(explanation.IsBroken);
        RuleExplanation child = Assert.Single(explanation.Children);
        Assert.Equal(Code, child.Code);
        Assert.Equal(RuleOutcome.Passed, child.Outcome);
    }

    [Fact]
    public async Task ExplainAsync_AlwaysPass_RecordsTheWrappedRuleAsNotEvaluated()
    {
        IAsyncBusinessRule mutant = CreateRule(broken: true).AlwaysPass();

        RuleExplanation explanation = await mutant.ExplainAsync(new BusinessRuleContext(), TestToken);

        Assert.Equal("ALWAYS PASSING", explanation.Operator);
        Assert.False(explanation.IsBroken);
        Assert.Equal(RuleOutcome.NotEvaluated, Assert.Single(explanation.Children).Outcome);
    }

    [Fact]
    public async Task Composition_InvertedOperand_FlipsTheOperandInsideAnd()
    {
        IAsyncBusinessRule composed = CreateRule(broken: false).Invert().And(RuleTestHelper.CreateBrokenAsyncRule());

        Assert.True(await composed.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task EvaluateDecoratedAsync_NullEvaluation_ThrowsArgumentNullException()
    {
        InvertedAsyncBusinessRule inverted = Assert.IsType<InvertedAsyncBusinessRule>(CreateRule(broken: true).Invert());
        AlwaysPassingAsyncBusinessRule silenced = Assert.IsType<AlwaysPassingAsyncBusinessRule>(CreateRule(broken: true).AlwaysPass());

        await Assert.ThrowsAsync<ArgumentNullException>(() => inverted.EvaluateDecoratedAsync(null!, TestToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => silenced.EvaluateDecoratedAsync(null!, TestToken));
    }

    [Fact]
    public async Task EvaluateDecoratedAsync_AlwaysPass_CancelledToken_Throws()
    {
        AlwaysPassingAsyncBusinessRule silenced = Assert.IsType<AlwaysPassingAsyncBusinessRule>(CreateRule(broken: true).AlwaysPass());
        AsyncDecoratedRuleEvaluation evaluation = new AsyncDecoratedRuleEvaluation(silenced.InnerRule, new BusinessRuleContext());
        using CancellationTokenSource cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => silenced.EvaluateDecoratedAsync(evaluation, cancelled.Token));
    }

    private static void AssertMetadataForwarded(IAsyncBusinessRule mutant, RuleSeverity severity)
    {
        Assert.Equal(Code, mutant.Code);
        Assert.Equal(Message, mutant.Message);
        Assert.Equal(severity, mutant.Severity);
        Assert.Equal(Name, mutant.Name);
        Assert.Equal(Description, mutant.Description);
        Assert.Equal(Category, mutant.Category);
        Assert.Equal(new[] { "x" }, mutant.Tags);
    }

    private static IAsyncBusinessRule CreateRule(bool broken, RuleSeverity severity = RuleSeverity.Error)
    {
        return AsyncBusinessRuleBuilder.Create(Code)
            .WithMessage(Message)
            .WithSeverity(severity)
            .WithName(Name)
            .WithDescription(Description)
            .WithCategory(Category)
            .WithTags("x")
            .WhenAsync((ctx, ct) => Task.FromResult(broken))
            .Build();
    }
}
