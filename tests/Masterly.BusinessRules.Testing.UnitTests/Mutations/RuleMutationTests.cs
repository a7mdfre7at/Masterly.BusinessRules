namespace Masterly.BusinessRules.Testing.UnitTests;

public class RuleMutationTests
{
    private const string Code = "CUSTOMER.ADULT";
    private const string Message = "Customer must be an adult";
    private const string Name = "CustomerIsAdult";
    private const string Description = "Checks the customer's age";
    private const string Category = "Customer";
    private const string FlagKey = "flag";

    [Fact]
    public void Invert_ReturnsInvertedBusinessRule()
    {
        IBusinessRule inner = CreateRule(broken: true);

        IBusinessRule mutant = inner.Invert();

        InvertedBusinessRule decorator = Assert.IsType<InvertedBusinessRule>(mutant);
        Assert.Same(inner, decorator.InnerRule);
        Assert.IsAssignableFrom<IDecoratedBusinessRule>(mutant);
    }

    [Fact]
    public void Invert_ForwardsAllMetadata()
    {
        IBusinessRule mutant = CreateRule(broken: true).Invert();

        AssertMetadataForwarded(mutant, RuleSeverity.Error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Invert_FlipsVerdict(bool broken)
    {
        IBusinessRule mutant = CreateRule(broken).Invert();

        Assert.Equal(!broken, mutant.IsBroken());
        Assert.Equal(!broken, mutant.IsBroken(new BusinessRuleContext()));
    }

    [Fact]
    public void Invert_ForwardsContextToInnerRule()
    {
        IBusinessRule contextRule = BusinessRuleBuilder.Create(Code).WithMessage(Message).When(ctx => ctx.Get<bool>(FlagKey)).Build();
        IBusinessRule mutant = contextRule.Invert();

        Assert.False(mutant.IsBroken(RuleTestHelper.CreateContext((FlagKey, true))));
        Assert.True(mutant.IsBroken(RuleTestHelper.CreateContext((FlagKey, false))));
    }

    [Fact]
    public void Invert_Check_ThrowsWhenInnerPasses()
    {
        IBusinessRule mutant = CreateRule(broken: false).Invert();

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => mutant.Check());
        Assert.Throws<BusinessRuleValidationException>(() => mutant.Check(new BusinessRuleContext()));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(Code, result.Code);
        Assert.Equal(Message, result.Message);
    }

    [Fact]
    public void Invert_Check_PassesWhenInnerBroken()
    {
        IBusinessRule mutant = CreateRule(broken: true).Invert();

        mutant.Check();
        mutant.Check(new BusinessRuleContext());
    }

    [Fact]
    public void Invert_Evaluate_ForwardsInnerFactory()
    {
        IBusinessRule mutant = CreateRule(broken: false).Invert();

        Assert.Equal(Code, mutant.Evaluate().Code);
        Assert.Equal(Code, mutant.Evaluate(new BusinessRuleContext()).Code);
    }

    [Fact]
    public void Invert_NullRule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((IBusinessRule)null!).Invert());
    }

    [Fact]
    public void AlwaysPass_ReturnsAlwaysPassingBusinessRule_ForwardingMetadata()
    {
        IBusinessRule inner = CreateRule(broken: true);

        IBusinessRule mutant = inner.AlwaysPass();

        AlwaysPassingBusinessRule decorator = Assert.IsType<AlwaysPassingBusinessRule>(mutant);
        Assert.Same(inner, decorator.InnerRule);
        AssertMetadataForwarded(mutant, RuleSeverity.Error);
    }

    [Fact]
    public void AlwaysPass_NeverBreaksAndNeverEvaluatesInner()
    {
        int evaluations = 0;
        IBusinessRule counting = BusinessRuleBuilder.Create(Code).WithMessage(Message).When(() =>
        {
            evaluations++;
            return true;
        }).Build();

        IBusinessRule mutant = counting.AlwaysPass();

        Assert.False(mutant.IsBroken());
        Assert.False(mutant.IsBroken(new BusinessRuleContext()));
        mutant.Check();
        mutant.Check(new BusinessRuleContext());
        Assert.Equal(0, evaluations);
    }

    [Fact]
    public void AlwaysPass_Evaluate_ForwardsInnerFactory()
    {
        IBusinessRule mutant = CreateRule(broken: true).AlwaysPass();

        Assert.Equal(Code, mutant.Evaluate().Code);
        Assert.Equal(Code, mutant.Evaluate(new BusinessRuleContext()).Code);
    }

    [Fact]
    public void AlwaysPass_NullRule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((IBusinessRule)null!).AlwaysPass());
    }

    [Theory]
    [InlineData(RuleSeverity.Error, RuleSeverity.Warning)]
    [InlineData(RuleSeverity.Warning, RuleSeverity.Info)]
    [InlineData(RuleSeverity.Info, RuleSeverity.Info)]
    public void DowngradeSeverity_ReportsOneLevelLower(RuleSeverity original, RuleSeverity expected)
    {
        IBusinessRule inner = CreateRule(broken: true, severity: original);

        IBusinessRule mutant = inner.DowngradeSeverity();

        SeverityOverrideBusinessRule decorator = Assert.IsType<SeverityOverrideBusinessRule>(mutant);
        Assert.Same(inner, decorator.InnerRule);
        Assert.Equal(expected, mutant.Severity);
        Assert.True(mutant.IsBroken());
        Assert.Equal(expected, Assert.Single(Assert.Throws<BusinessRuleValidationException>(() => mutant.Check()).BrokenRules).Severity);
    }

    [Fact]
    public void DowngradeSeverity_NullRule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((IBusinessRule)null!).DowngradeSeverity());
    }

    [Fact]
    public void SmokeMode_SuiteAssertingTheHappyPath_NoticesTheInvertedMutant()
    {
        IBusinessRule mutant = CreateRule(broken: false).Invert();

        Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.CheckAll(new[] { mutant }));
    }

    [Fact]
    public void SmokeMode_SuiteAssertingTheBrokenPath_NoticesTheSilencedMutant()
    {
        IBusinessRule mutant = CreateRule(broken: true).AlwaysPass();

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { mutant });

        Assert.True(result.AllPassed);
    }

    [Fact]
    public void Explain_Inverted_RecordsInvertedNodeWithTheWrappedRuleAsChild()
    {
        IBusinessRule mutant = CreateRule(broken: false).Invert();

        RuleExplanation explanation = mutant.Explain();

        Assert.Equal("INVERTED", explanation.Operator);
        Assert.True(explanation.IsBroken);
        RuleExplanation child = Assert.Single(explanation.Children);
        Assert.Equal(Code, child.Code);
        Assert.Equal(RuleOutcome.Passed, child.Outcome);
    }

    [Fact]
    public void Explain_InvertedBrokenRule_PassesWithTheBrokenRuleAsChild()
    {
        IBusinessRule mutant = CreateRule(broken: true).Invert();

        RuleExplanation explanation = mutant.Explain();

        Assert.Equal("INVERTED", explanation.Operator);
        Assert.False(explanation.IsBroken);
        Assert.Equal(RuleOutcome.Broken, Assert.Single(explanation.Children).Outcome);
    }

    [Fact]
    public void EvaluateAll_InvertedBrokenRule_PassesThroughTheWalker()
    {
        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(CreateRule(broken: true).Invert());

        Assert.True(result.AllPassed);
        Assert.Equal(Code, Assert.Single(result.PassedRules).Code);
    }

    [Fact]
    public void Explain_AlwaysPass_RecordsTheWrappedRuleAsNotEvaluated()
    {
        IBusinessRule mutant = CreateRule(broken: true).AlwaysPass();

        RuleExplanation explanation = mutant.Explain();

        Assert.Equal("ALWAYS PASSING", explanation.Operator);
        Assert.False(explanation.IsBroken);
        Assert.Equal(RuleOutcome.NotEvaluated, Assert.Single(explanation.Children).Outcome);
    }

    [Fact]
    public void Composition_InvertedOperand_FlipsTheOperandInsideAnd()
    {
        IBusinessRule composed = CreateRule(broken: false).Invert().And(RuleTestHelper.CreateBrokenRule());

        Assert.True(composed.IsBroken(new BusinessRuleContext()));
    }

    [Fact]
    public void EvaluateDecorated_NullEvaluation_ThrowsArgumentNullException()
    {
        InvertedBusinessRule inverted = Assert.IsType<InvertedBusinessRule>(CreateRule(broken: true).Invert());
        AlwaysPassingBusinessRule silenced = Assert.IsType<AlwaysPassingBusinessRule>(CreateRule(broken: true).AlwaysPass());

        Assert.Throws<ArgumentNullException>(() => inverted.EvaluateDecorated(null!));
        Assert.Throws<ArgumentNullException>(() => silenced.EvaluateDecorated(null!));
    }

    private static void AssertMetadataForwarded(IBusinessRule mutant, RuleSeverity severity)
    {
        Assert.Equal(Code, mutant.Code);
        Assert.Equal(Message, mutant.Message);
        Assert.Equal(severity, mutant.Severity);
        Assert.Equal(Name, mutant.Name);
        Assert.Equal(Description, mutant.Description);
        Assert.Equal(Category, mutant.Category);
        Assert.Equal(new[] { "a", "b" }, mutant.Tags);
    }

    private static IBusinessRule CreateRule(bool broken, RuleSeverity severity = RuleSeverity.Error)
    {
        return BusinessRuleBuilder.Create(Code)
            .WithMessage(Message)
            .WithSeverity(severity)
            .WithName(Name)
            .WithDescription(Description)
            .WithCategory(Category)
            .WithTags("a", "b")
            .When(() => broken)
            .Build();
    }
}
