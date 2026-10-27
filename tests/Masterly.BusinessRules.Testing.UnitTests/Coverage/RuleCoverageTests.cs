namespace Masterly.BusinessRules.Testing.UnitTests;

public class RuleCoverageTests
{
    private const string Code = "CUSTOMER.ADULT";
    private const string OtherCode = "CUSTOMER.ACTIVE";
    private const string Message = "Customer must be an adult";
    private const string Name = "CustomerIsAdult";
    private const string Description = "Checks the customer's age";
    private const string Category = "Customer";
    private const string FlagKey = "flag";
    private const int ParallelRecordings = 2_000;

    [Fact]
    public void Begin_ReturnsOpenEmptySession()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();

        Assert.False(session.IsEnded);
        Assert.Equal(0, session.RuleCount);
        Assert.Equal(0, session.Report().RuleCount);
    }

    [Fact]
    public void WithCoverage_ReturnsCoverageBusinessRule_AndRegistersCode()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IBusinessRule inner = CreateRule(broken: true);

        IBusinessRule covered = inner.WithCoverage(session);

        CoverageBusinessRule decorator = Assert.IsType<CoverageBusinessRule>(covered);
        Assert.Same(inner, decorator.InnerRule);
        Assert.Same(session, decorator.Session);
        Assert.IsAssignableFrom<IDecoratedBusinessRule>(covered);
        RuleCoverageEntry entry = Assert.Single(session.Report().Entries);
        Assert.Equal(Code, entry.Code);
        Assert.False(entry.WasEvaluated);
    }

    [Fact]
    public void WithCoverage_NullRule_ThrowsArgumentNullException()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => ((IBusinessRule)null!).WithCoverage(session));

        Assert.Equal("innerRule", exception.ParamName);
    }

    [Fact]
    public void WithCoverage_NullSession_ThrowsArgumentNullException()
    {
        IBusinessRule inner = CreateRule(broken: true);

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => inner.WithCoverage(null!));

        Assert.Equal("session", exception.ParamName);
    }

    [Fact]
    public void WithCoverage_ForwardsAllMetadata()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();

        IBusinessRule covered = CreateRule(broken: true).WithCoverage(session);

        Assert.Equal(Code, covered.Code);
        Assert.Equal(Message, covered.Message);
        Assert.Equal(RuleSeverity.Warning, covered.Severity);
        Assert.Equal(Name, covered.Name);
        Assert.Equal(Description, covered.Description);
        Assert.Equal(Category, covered.Category);
        Assert.Equal(new[] { "a", "b" }, covered.Tags);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsBroken_Parameterless_ForwardsVerdictAndRecordsOutcome(bool broken)
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IBusinessRule covered = CreateRule(broken).WithCoverage(session);

        bool verdict = covered.IsBroken();

        Assert.Equal(broken, verdict);
        RuleCoverageEntry entry = Assert.Single(session.Report().Entries);
        Assert.Equal(broken ? 1 : 0, entry.BrokenCount);
        Assert.Equal(broken ? 0 : 1, entry.PassedCount);
    }

    [Fact]
    public void IsBroken_WithContext_ForwardsContextAndRecordsBothOutcomes()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IBusinessRule contextRule = BusinessRuleBuilder.Create(Code).WithMessage(Message).When(ctx => ctx.Get<bool>(FlagKey)).Build();
        IBusinessRule covered = contextRule.WithCoverage(session);

        Assert.True(covered.IsBroken(RuleTestHelper.CreateContext((FlagKey, true))));
        Assert.False(covered.IsBroken(RuleTestHelper.CreateContext((FlagKey, false))));

        RuleCoverageEntry entry = Assert.Single(session.Report().Entries);
        Assert.True(entry.IsFullyCovered);
        Assert.Equal(1, entry.BrokenCount);
        Assert.Equal(1, entry.PassedCount);
    }

    [Fact]
    public void Check_WhenBroken_RecordsBrokenAndRethrowsInnerException()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IBusinessRule covered = CreateRule(broken: true).WithCoverage(session);

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => covered.Check());

        Assert.Equal(Code, Assert.Single(exception.BrokenRules).Code);
        Assert.Equal(1, session.Report().Find(Code)!.BrokenCount);
    }

    [Fact]
    public void Check_WhenPassing_RecordsPassed()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IBusinessRule covered = CreateRule(broken: false).WithCoverage(session);

        covered.Check();

        Assert.Equal(1, session.Report().Find(Code)!.PassedCount);
    }

    [Fact]
    public void Check_WithContext_ForwardsContextAndRecords()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IBusinessRule contextRule = BusinessRuleBuilder.Create(Code).WithMessage(Message).When(ctx => ctx.Get<bool>(FlagKey)).Build();
        IBusinessRule covered = contextRule.WithCoverage(session);

        covered.Check(RuleTestHelper.CreateContext((FlagKey, false)));
        Assert.Throws<BusinessRuleValidationException>(() => covered.Check(RuleTestHelper.CreateContext((FlagKey, true))));

        Assert.True(session.Report().Find(Code)!.IsFullyCovered);
    }

    [Fact]
    public void Evaluate_IsAFactory_ForwardsAndRecordsNothing()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IBusinessRule covered = CreateRule(broken: true).WithCoverage(session);

        BusinessRuleResult result = covered.Evaluate();
        BusinessRuleResult contextResult = covered.Evaluate(new BusinessRuleContext());

        Assert.Equal(Code, result.Code);
        Assert.Equal(Code, contextResult.Code);
        Assert.False(session.Report().Find(Code)!.WasEvaluated);
    }

    [Fact]
    public void IsBroken_WhenInnerThrows_PropagatesAndRecordsNothing()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IBusinessRule throwing = BusinessRuleBuilder.Create(Code).WithMessage(Message).When(() => throw new InvalidOperationException("boom")).Build();
        IBusinessRule covered = throwing.WithCoverage(session);

        Assert.Throws<InvalidOperationException>(() => covered.IsBroken());
        Assert.Throws<InvalidOperationException>(() => covered.Check());

        Assert.False(session.Report().Find(Code)!.WasEvaluated);
    }

    [Fact]
    public void Checker_WithOperandsWrappedBeforeComposition_ReportsOperandOutcomesAndShortCircuit()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IBusinessRule adult = CreateRule(broken: false).WithCoverage(session);
        IBusinessRule active = CreateRule(broken: true, code: OtherCode).WithCoverage(session);

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { adult.And(active) });

        Assert.True(result.AllPassed);
        RuleCoverageReport report = session.Report();
        Assert.Equal(new[] { OtherCode }, report.NeverEvaluated);
        Assert.Equal(1, report.Find(Code)!.PassedCount);
        Assert.Contains(OtherCode, report.ToText());
    }

    [Fact]
    public void Checker_CheckAll_RecordsEveryRule()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IBusinessRule[] rules =
        {
            CreateRule(broken: false).WithCoverage(session),
            CreateRule(broken: true, code: OtherCode).WithCoverage(session),
        };

        Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.CheckAll(rules));

        RuleCoverageReport report = session.Report();
        Assert.Equal(new[] { OtherCode }, report.NeverPassed);
        Assert.Equal(new[] { Code }, report.NeverBroken);
        Assert.Equal(0.5, report.OutcomeCoverage);
    }

    [Fact]
    public void Session_SameCodeFromTwoRules_MergesIntoOneEntry()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IBusinessRule first = CreateRule(broken: true).WithCoverage(session);
        IBusinessRule second = CreateRule(broken: false).WithCoverage(session);

        first.IsBroken();
        second.IsBroken();

        RuleCoverageEntry entry = Assert.Single(session.Report().Entries);
        Assert.True(entry.IsFullyCovered);
        Assert.Equal(1, session.RuleCount);
    }

    [Fact]
    public void Session_Dispose_EndsRecordingButKeepsReport()
    {
        RuleCoverageSession session = RuleCoverage.Begin();
        IBusinessRule covered = CreateRule(broken: true).WithCoverage(session);
        covered.IsBroken();

        session.Dispose();
        session.Dispose();
        covered.IsBroken();
        CreateRule(broken: false, code: OtherCode).WithCoverage(session);
        session.Register(CreateRule(broken: false, code: OtherCode));

        Assert.True(session.IsEnded);
        RuleCoverageEntry entry = Assert.Single(session.Report().Entries);
        Assert.Equal(1, entry.BrokenCount);
    }

    [Fact]
    public void Session_Reset_ForgetsEntriesAndStaysOpen()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IBusinessRule covered = CreateRule(broken: true).WithCoverage(session);
        covered.IsBroken();

        session.Reset();

        Assert.Equal(0, session.RuleCount);
        Assert.False(session.IsEnded);
        covered.IsBroken();
        Assert.Equal(1, session.Report().Find(Code)!.BrokenCount);
    }

    [Fact]
    public void Session_RecordAndRegister_NullRule_ThrowArgumentNullException()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();

        Assert.Throws<ArgumentNullException>(() => session.Record(null!, true));
        Assert.Throws<ArgumentNullException>(() => session.Register(null!));
    }

    [Fact]
    public void Session_Record_FromManyThreads_CountsEveryOutcome()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IBusinessRule rule = CreateRule(broken: true);

        Parallel.For(0, ParallelRecordings, index => session.Record(rule, index % 2 == 0));

        RuleCoverageEntry entry = Assert.Single(session.Report().Entries);
        Assert.Equal(ParallelRecordings / 2, entry.BrokenCount);
        Assert.Equal(ParallelRecordings / 2, entry.PassedCount);
    }

    [Fact]
    public void Explain_RecordsCoverageNodeWithTheWrappedRuleAsChild()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IBusinessRule covered = CreateRule(broken: true).WithCoverage(session);

        RuleExplanation explanation = covered.Explain();

        Assert.Equal(Code, explanation.Code);
        Assert.Equal("COVERAGE", explanation.Operator);
        Assert.True(explanation.IsBroken);
        RuleExplanation child = Assert.Single(explanation.Children);
        Assert.Equal(Code, child.Code);
        Assert.Equal(RuleOutcome.Broken, child.Outcome);
        Assert.Equal(1, session.Report().Find(Code)!.BrokenCount);
    }

    [Fact]
    public void Explain_CoverageOverComposedRule_ShowsTheComposedTreeAndRecordsOnce()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IBusinessRule composed = CreateRule(broken: true).And(CreateRule(broken: false, OtherCode));
        IBusinessRule covered = composed.WithCoverage(session);

        RuleExplanation explanation = covered.Explain();

        RuleExplanation andNode = Assert.Single(explanation.Children);
        Assert.Equal("AND", andNode.Operator);
        Assert.Equal(2, andNode.Children.Count);
        RuleCoverageEntry entry = session.Report().Find(composed.Code)!;
        Assert.Equal(1, entry.PassedCount);
        Assert.Equal(0, entry.BrokenCount);
    }

    [Fact]
    public void Composition_CoverageOperands_RecordThroughTheSingleEvaluation()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        IBusinessRule first = CreateRule(broken: true).WithCoverage(session);
        IBusinessRule second = CreateRule(broken: false, OtherCode).WithCoverage(session);

        bool isBroken = first.Or(second).IsBroken(new BusinessRuleContext());

        Assert.True(isBroken);
        Assert.Equal(1, session.Report().Find(Code)!.BrokenCount);
        Assert.False(session.Report().Find(OtherCode)!.WasEvaluated);
    }

    [Fact]
    public void EvaluateDecorated_NullEvaluation_ThrowsArgumentNullException()
    {
        using RuleCoverageSession session = RuleCoverage.Begin();
        CoverageBusinessRule covered = Assert.IsType<CoverageBusinessRule>(CreateRule(broken: true).WithCoverage(session));

        Assert.Throws<ArgumentNullException>(() => covered.EvaluateDecorated(null!));
    }

    private static IBusinessRule CreateRule(bool broken, string code = Code)
    {
        return BusinessRuleBuilder.Create(code)
            .WithMessage(Message)
            .WithSeverity(RuleSeverity.Warning)
            .WithName(Name)
            .WithDescription(Description)
            .WithCategory(Category)
            .WithTags("a", "b")
            .When(() => broken)
            .Build();
    }
}
