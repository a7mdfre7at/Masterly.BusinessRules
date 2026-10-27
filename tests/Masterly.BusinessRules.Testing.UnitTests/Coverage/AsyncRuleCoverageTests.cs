namespace Masterly.BusinessRules.Testing.UnitTests;

public class AsyncRuleCoverageTests
{
    private const string Code = "EMAIL.UNIQUE";
    private const string OtherCode = "EMAIL.FORMAT";
    private const string Message = "Email must be unique";
    private const string Name = "UniqueEmail";
    private const string Description = "Checks the email against the repository";
    private const string Category = "Identity";
    private const string FlagKey = "flag";
    private const int ParallelRules = 50;

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public void WithCoverage_ReturnsCoverageAsyncBusinessRule_AndRegistersCode()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IAsyncBusinessRule inner = CreateRule(broken: true);

        IAsyncBusinessRule covered = inner.WithCoverage(session);

        CoverageAsyncBusinessRule decorator = Assert.IsType<CoverageAsyncBusinessRule>(covered);
        Assert.Same(inner, decorator.InnerRule);
        Assert.Same(session, decorator.Session);
        Assert.IsAssignableFrom<IDecoratedAsyncBusinessRule>(covered);
        Assert.Equal(new[] { Code }, session.Report().NeverEvaluated);
    }

    [Fact]
    public void WithCoverage_NullRule_ThrowsArgumentNullException()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => ((IAsyncBusinessRule)null!).WithCoverage(session));

        Assert.Equal("innerRule", exception.ParamName);
    }

    [Fact]
    public void WithCoverage_NullSession_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => CreateRule(broken: true).WithCoverage(null!));

        Assert.Equal("session", exception.ParamName);
    }

    [Fact]
    public void WithCoverage_ForwardsAllMetadata()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();

        IAsyncBusinessRule covered = CreateRule(broken: true).WithCoverage(session);

        Assert.Equal(Code, covered.Code);
        Assert.Equal(Message, covered.Message);
        Assert.Equal(RuleSeverity.Info, covered.Severity);
        Assert.Equal(Name, covered.Name);
        Assert.Equal(Description, covered.Description);
        Assert.Equal(Category, covered.Category);
        Assert.Equal(new[] { "x" }, covered.Tags);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IsBrokenAsync_ForwardsVerdictAndRecordsOutcome(bool broken)
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IAsyncBusinessRule covered = CreateRule(broken).WithCoverage(session);

        bool verdict = await covered.IsBrokenAsync(new BusinessRuleContext(), TestToken);

        Assert.Equal(broken, verdict);
        RuleCoverageEntry entry = Assert.Single(session.Report().Entries);
        Assert.Equal(broken ? 1 : 0, entry.BrokenCount);
        Assert.Equal(broken ? 0 : 1, entry.PassedCount);
    }

    [Fact]
    public async Task IsBrokenAsync_ForwardsContextToInnerRule()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IAsyncBusinessRule contextRule = AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message)
            .WhenAsync((ctx, ct) => Task.FromResult(ctx.Get<bool>(FlagKey))).Build();
        IAsyncBusinessRule covered = contextRule.WithCoverage(session);

        Assert.True(await covered.IsBrokenAsync(RuleTestHelper.CreateContext((FlagKey, true)), TestToken));
        Assert.False(await covered.IsBrokenAsync(RuleTestHelper.CreateContext((FlagKey, false)), TestToken));

        Assert.True(session.Report().Find(Code)!.IsFullyCovered);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EvaluateAsync_ForwardsResultAndRecordsOutcome(bool broken)
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IAsyncBusinessRule covered = CreateRule(broken).WithCoverage(session);

        BusinessRuleResult? result = await covered.EvaluateAsync(new BusinessRuleContext(), TestToken);

        Assert.Equal(broken, result != null);
        RuleCoverageEntry entry = Assert.Single(session.Report().Entries);
        Assert.Equal(broken, entry.SeenBroken);
        Assert.Equal(!broken, entry.SeenPassed);
    }

    [Fact]
    public async Task CheckAsync_WhenBroken_RecordsBrokenAndRethrows()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IAsyncBusinessRule covered = CreateRule(broken: true).WithCoverage(session);

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => covered.CheckAsync(new BusinessRuleContext(), TestToken));

        Assert.Equal(Code, Assert.Single(exception.BrokenRules).Code);
        Assert.Equal(1, session.Report().Find(Code)!.BrokenCount);
    }

    [Fact]
    public async Task CheckAsync_WhenPassing_RecordsPassed()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IAsyncBusinessRule covered = CreateRule(broken: false).WithCoverage(session);

        await covered.CheckAsync(new BusinessRuleContext(), TestToken);

        Assert.Equal(1, session.Report().Find(Code)!.PassedCount);
    }

    [Fact]
    public async Task IsBrokenAsync_WhenInnerThrows_PropagatesAndRecordsNothing()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IAsyncBusinessRule throwing = AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message)
            .WhenAsync((ctx, ct) => throw new InvalidOperationException("boom")).Build();
        IAsyncBusinessRule covered = throwing.WithCoverage(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() => covered.IsBrokenAsync(new BusinessRuleContext(), TestToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => covered.CheckAsync(new BusinessRuleContext(), TestToken));

        Assert.False(session.Report().Find(Code)!.WasEvaluated);
    }

    [Fact]
    public async Task IsBrokenAsync_WhenCancelled_PropagatesAndRecordsNothing()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();
        IAsyncBusinessRule honouring = AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message)
            .WhenAsync((ctx, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(true);
            })
            .Build();
        IAsyncBusinessRule covered = honouring.WithCoverage(session);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => covered.IsBrokenAsync(new BusinessRuleContext(), source.Token));

        Assert.False(session.Report().Find(Code)!.WasEvaluated);
    }

    [Fact]
    public async Task Checker_WithOperandsWrappedBeforeComposition_ReportsShortCircuitedOperand()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IAsyncBusinessRule first = CreateRule(broken: false).WithCoverage(session);
        IAsyncBusinessRule second = CreateRule(broken: true, code: OtherCode).WithCoverage(session);

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), new[] { first.And(second) }, cancellationToken: TestToken);

        Assert.True(result.AllPassed);
        RuleCoverageReport report = session.Report();
        Assert.Equal(new[] { OtherCode }, report.NeverEvaluated);
        Assert.Equal(1, report.Find(Code)!.PassedCount);
    }

    [Fact]
    public async Task Checker_ParallelBatch_RecordsEveryRule()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        List<IAsyncBusinessRule> rules = new List<IAsyncBusinessRule>();
        for (int index = 0; index < ParallelRules; index++)
        {
            rules.Add(CreateRule(broken: index % 2 == 0, code: $"RULE.{index}").WithCoverage(session));
        }

        AsyncRuleEvaluationOptions options = new AsyncRuleEvaluationOptions { RunInParallel = true };
        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), rules, options, TestToken);

        Assert.Equal(ParallelRules / 2, result.BrokenRules.Count);
        RuleCoverageReport report = session.Report();
        Assert.Equal(ParallelRules, report.RuleCount);
        Assert.Empty(report.NeverEvaluated);
        Assert.Equal(0.5, report.OutcomeCoverage);
    }

    [Fact]
    public async Task ExplainAsync_RecordsCoverageNodeWithTheWrappedRuleAsChild()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IAsyncBusinessRule covered = CreateRule(broken: true).WithCoverage(session);

        RuleExplanation explanation = await covered.ExplainAsync(new BusinessRuleContext(), TestToken);

        Assert.Equal(Code, explanation.Code);
        Assert.Equal("COVERAGE", explanation.Operator);
        Assert.True(explanation.IsBroken);
        RuleExplanation child = Assert.Single(explanation.Children);
        Assert.Equal(Code, child.Code);
        Assert.Equal(RuleOutcome.Broken, child.Outcome);
        Assert.Equal(1, session.Report().Find(Code)!.BrokenCount);
    }

    [Fact]
    public async Task ExplainAsync_CoverageOverComposedRule_ShowsTheComposedTreeAndRecordsOnce()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IAsyncBusinessRule composed = CreateRule(broken: true).And(CreateRule(broken: false, OtherCode));
        IAsyncBusinessRule covered = composed.WithCoverage(session);

        RuleExplanation explanation = await covered.ExplainAsync(new BusinessRuleContext(), TestToken);

        RuleExplanation andNode = Assert.Single(explanation.Children);
        Assert.Equal("AND", andNode.Operator);
        Assert.Equal(2, andNode.Children.Count);
        RuleCoverageEntry entry = session.Report().Find(composed.Code)!;
        Assert.Equal(1, entry.PassedCount);
        Assert.Equal(0, entry.BrokenCount);
    }

    [Fact]
    public async Task Composition_CoverageOperands_RecordThroughTheSingleEvaluation()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IAsyncBusinessRule first = CreateRule(broken: true).WithCoverage(session);
        IAsyncBusinessRule second = CreateRule(broken: false, OtherCode).WithCoverage(session);

        bool isBroken = await first.Or(second).IsBrokenAsync(new BusinessRuleContext(), TestToken);

        Assert.True(isBroken);
        Assert.Equal(1, session.Report().Find(Code)!.BrokenCount);
        Assert.False(session.Report().Find(OtherCode)!.WasEvaluated);
    }

    [Fact]
    public async Task EvaluateDecoratedAsync_NullEvaluation_ThrowsArgumentNullException()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        CoverageAsyncBusinessRule covered = Assert.IsType<CoverageAsyncBusinessRule>(CreateRule(broken: true).WithCoverage(session));

        await Assert.ThrowsAsync<ArgumentNullException>(() => covered.EvaluateDecoratedAsync(null!, TestToken));
    }

    [Fact]
    public async Task ExplainAsync_CancelledToken_ThrowsAndRecordsNothing()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IAsyncBusinessRule covered = CreateRule(broken: true).WithCoverage(session);
        using CancellationTokenSource cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => covered.ExplainAsync(new BusinessRuleContext(), cancelled.Token));

        Assert.False(session.Report().Find(Code)!.WasEvaluated);
    }

    private static IAsyncBusinessRule CreateRule(bool broken, string code = Code)
    {
        return AsyncBusinessRuleBuilder.Create(code)
            .WithMessage(Message)
            .WithSeverity(RuleSeverity.Info)
            .WithName(Name)
            .WithDescription(Description)
            .WithCategory(Category)
            .WithTags("x")
            .WhenAsync((ctx, ct) => Task.FromResult(broken))
            .Build();
    }
}
