namespace Masterly.BusinessRules.UnitTests;

public class SeverityOverrideRuleTests
{
    private const string Code = "EMAIL.FORMAT";
    private const string Message = "Email format is invalid";
    private const string Name = "EmailFormat";
    private const string Description = "Checks the email format";
    private const string Category = "Identity";
    private const string FlagKey = "flag";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------
    // Sync
    // ---------------------------------------------------------------------

    [Fact]
    public void WithSeverity_ReturnsSeverityOverrideBusinessRule()
    {
        IBusinessRule inner = CreateSyncRule(broken: true);

        IBusinessRule softened = inner.WithSeverity(RuleSeverity.Warning);

        SeverityOverrideBusinessRule decorator = Assert.IsType<SeverityOverrideBusinessRule>(softened);
        Assert.Same(inner, decorator.InnerRule);
        Assert.IsAssignableFrom<IDecoratedBusinessRule>(softened);
    }

    [Fact]
    public void WithSeverity_OverridesSeverityAndForwardsEveryOtherMetadataMember()
    {
        IBusinessRule inner = CreateSyncRule(broken: true);

        IBusinessRule softened = inner.WithSeverity(RuleSeverity.Info);

        Assert.Equal(RuleSeverity.Info, softened.Severity);
        Assert.Equal(Code, softened.Code);
        Assert.Equal(Message, softened.Message);
        Assert.Equal(Name, softened.Name);
        Assert.Equal(Description, softened.Description);
        Assert.Equal(Category, softened.Category);
        Assert.Equal(new[] { "a", "b" }, softened.Tags);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsBroken_ForwardsVerdict(bool broken)
    {
        IBusinessRule softened = CreateSyncRule(broken).WithSeverity(RuleSeverity.Warning);

        Assert.Equal(broken, softened.IsBroken());
        Assert.Equal(broken, softened.IsBroken(new BusinessRuleContext()));
    }

    [Fact]
    public void IsBroken_WithContext_ForwardsContextToInnerRule()
    {
        IBusinessRule contextRule = BusinessRuleBuilder.Create(Code).WithMessage(Message).When(ctx => ctx.Get<bool>(FlagKey)).Build();
        IBusinessRule softened = contextRule.WithSeverity(RuleSeverity.Warning);

        Assert.True(softened.IsBroken(RuleTestHelper.CreateContext((FlagKey, true))));
        Assert.False(softened.IsBroken(RuleTestHelper.CreateContext((FlagKey, false))));
    }

    [Fact]
    public void Check_WhenBroken_ThrowsWithOverriddenSeverity()
    {
        IBusinessRule softened = CreateSyncRule(broken: true).WithSeverity(RuleSeverity.Warning);

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => softened.Check());

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(Code, result.Code);
        Assert.Equal(Message, result.Message);
    }

    [Fact]
    public void Check_WithContext_WhenBroken_ThrowsWithOverriddenSeverity()
    {
        IBusinessRule softened = CreateSyncRule(broken: true).WithSeverity(RuleSeverity.Info);

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => softened.Check(new BusinessRuleContext()));

        Assert.Equal(RuleSeverity.Info, Assert.Single(exception.BrokenRules).Severity);
    }

    [Fact]
    public void Check_WhenPassing_DoesNotThrow()
    {
        IBusinessRule softened = CreateSyncRule(broken: false).WithSeverity(RuleSeverity.Warning);

        softened.Check();
        softened.Check(new BusinessRuleContext());
    }

    [Fact]
    public void Evaluate_IsAPureFactoryWithOverriddenSeverity()
    {
        int evaluations = 0;
        IBusinessRule counting = BusinessRuleBuilder.Create(Code).WithMessage(Message).When(() => { evaluations++; return true; }).Build();
        IBusinessRule softened = counting.WithSeverity(RuleSeverity.Warning);

        BusinessRuleResult result = softened.Evaluate();

        Assert.Equal(0, evaluations);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(Code, result.Code);
    }

    [Fact]
    public void Check_OnComposedInnerRule_KeepsMemberDetails()
    {
        IBusinessRule group = RuleGroup.All("GROUP", "group", RuleTestHelper.CreateBrokenRule("M1"), RuleTestHelper.CreatePassingRule("M2"));
        IBusinessRule softened = group.WithSeverity(RuleSeverity.Warning);

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => softened.Check());

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal("M1", Assert.Single(result.Details).Code);
    }

    [Fact]
    public void Checker_UsesOverriddenSeverityForThreshold()
    {
        IBusinessRule softened = CreateSyncRule(broken: true).WithSeverity(RuleSeverity.Warning);

        BusinessRuleChecker.CheckAll(new[] { softened }, new RuleEvaluationOptions { ThrowOn = RuleSeverity.Error });

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(softened);
        Assert.True(result.HasWarnings);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Explain_ReportsSeverityNodeAboveInnerRule()
    {
        IBusinessRule inner = CreateSyncRule(broken: true);
        IBusinessRule softened = inner.WithSeverity(RuleSeverity.Warning);

        RuleExplanation explanation = softened.Explain();

        Assert.Same(softened, explanation.Rule);
        Assert.Equal("SEVERITY", explanation.Operator);
        Assert.Equal(RuleOutcome.Broken, explanation.Outcome);
        Assert.Equal(RuleSeverity.Warning, explanation.Severity);
        RuleExplanation child = Assert.Single(explanation.Children);
        Assert.Same(inner, child.Rule);
        Assert.Equal(RuleOutcome.Broken, child.Outcome);
    }

    [Fact]
    public void Explain_EvaluatesInnerRuleExactlyOnce()
    {
        int evaluations = 0;
        IBusinessRule counting = BusinessRuleBuilder.Create(Code).WithMessage(Message).When(() => { evaluations++; return true; }).Build();

        counting.WithSeverity(RuleSeverity.Warning).Explain();

        Assert.Equal(1, evaluations);
    }

    [Fact]
    public void WithSeverity_NullRule_ThrowsArgumentNullException()
    {
        IBusinessRule rule = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => rule.WithSeverity(RuleSeverity.Warning));
    }

    [Fact]
    public void Constructor_NullInnerRule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SeverityOverrideBusinessRule(null!, RuleSeverity.Warning));
    }

    [Fact]
    public void WithSeverity_OnGroup_InstanceMethodKeepsGroupType()
    {
        GroupBusinessRule group = RuleGroup.All("GROUP", "group", RuleTestHelper.CreateBrokenRule("M1"));

        GroupBusinessRule softened = group.WithSeverity(RuleSeverity.Warning);

        Assert.Equal(RuleSeverity.Warning, softened.Severity);
    }

    // ---------------------------------------------------------------------
    // Async
    // ---------------------------------------------------------------------

    [Fact]
    public void WithSeverity_Async_ReturnsSeverityOverrideAsyncBusinessRule()
    {
        IAsyncBusinessRule inner = CreateAsyncRule(broken: true);

        IAsyncBusinessRule softened = inner.WithSeverity(RuleSeverity.Warning);

        SeverityOverrideAsyncBusinessRule decorator = Assert.IsType<SeverityOverrideAsyncBusinessRule>(softened);
        Assert.Same(inner, decorator.InnerRule);
        Assert.IsAssignableFrom<IDecoratedAsyncBusinessRule>(softened);
    }

    [Fact]
    public void WithSeverity_Async_OverridesSeverityAndForwardsEveryOtherMetadataMember()
    {
        IAsyncBusinessRule softened = CreateAsyncRule(broken: true).WithSeverity(RuleSeverity.Info);

        Assert.Equal(RuleSeverity.Info, softened.Severity);
        Assert.Equal(Code, softened.Code);
        Assert.Equal(Message, softened.Message);
        Assert.Equal(Name, softened.Name);
        Assert.Equal(Description, softened.Description);
        Assert.Equal(Category, softened.Category);
        Assert.Equal(new[] { "a", "b" }, softened.Tags);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IsBrokenAsync_ForwardsVerdict(bool broken)
    {
        IAsyncBusinessRule softened = CreateAsyncRule(broken).WithSeverity(RuleSeverity.Warning);

        Assert.Equal(broken, await softened.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task EvaluateAsync_WhenBroken_ReturnsResultWithOverriddenSeverity()
    {
        IAsyncBusinessRule softened = CreateAsyncRule(broken: true).WithSeverity(RuleSeverity.Warning);

        BusinessRuleResult? result = await softened.EvaluateAsync(new BusinessRuleContext(), TestToken);

        Assert.NotNull(result);
        Assert.Equal(RuleSeverity.Warning, result!.Severity);
        Assert.Equal(Code, result.Code);
        Assert.Equal(Message, result.Message);
    }

    [Fact]
    public async Task EvaluateAsync_WhenPassing_ReturnsNull()
    {
        IAsyncBusinessRule softened = CreateAsyncRule(broken: false).WithSeverity(RuleSeverity.Warning);

        Assert.Null(await softened.EvaluateAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task CheckAsync_WhenBroken_ThrowsWithOverriddenSeverity()
    {
        IAsyncBusinessRule softened = CreateAsyncRule(broken: true).WithSeverity(RuleSeverity.Info);

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(() => softened.CheckAsync(new BusinessRuleContext(), TestToken));

        Assert.Equal(RuleSeverity.Info, Assert.Single(exception.BrokenRules).Severity);
    }

    [Fact]
    public async Task CheckAsync_WhenPassing_DoesNotThrow()
    {
        IAsyncBusinessRule softened = CreateAsyncRule(broken: false).WithSeverity(RuleSeverity.Warning);

        await softened.CheckAsync(new BusinessRuleContext(), TestToken);
    }

    [Fact]
    public async Task EvaluateAsync_OnComposedInnerRule_KeepsMemberDetails()
    {
        IAsyncBusinessRule group = AsyncRuleGroup.All("GROUP", "group", RuleTestHelper.CreateBrokenAsyncRule("M1"), RuleTestHelper.CreatePassingAsyncRule("M2"));
        IAsyncBusinessRule softened = group.WithSeverity(RuleSeverity.Warning);

        BusinessRuleResult? result = await softened.EvaluateAsync(new BusinessRuleContext(), TestToken);

        Assert.NotNull(result);
        Assert.Equal(RuleSeverity.Warning, result!.Severity);
        Assert.Equal("M1", Assert.Single(result.Details).Code);
    }

    [Fact]
    public async Task AsyncChecker_UsesOverriddenSeverityForThreshold()
    {
        IAsyncBusinessRule softened = CreateAsyncRule(broken: true).WithSeverity(RuleSeverity.Warning);

        await AsyncBusinessRuleChecker.CheckAllAsync(new[] { softened }, new AsyncRuleEvaluationOptions { ThrowOn = RuleSeverity.Error }, TestToken);

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(softened);
        Assert.True(result.HasWarnings);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public async Task ExplainAsync_ReportsSeverityNodeAboveInnerRule()
    {
        IAsyncBusinessRule inner = CreateAsyncRule(broken: true);
        IAsyncBusinessRule softened = inner.WithSeverity(RuleSeverity.Warning);

        RuleExplanation explanation = await softened.ExplainAsync(new BusinessRuleContext(), TestToken);

        Assert.Same(softened, explanation.AsyncRule);
        Assert.Equal("SEVERITY", explanation.Operator);
        Assert.Equal(RuleOutcome.Broken, explanation.Outcome);
        Assert.Equal(RuleSeverity.Warning, explanation.Severity);
        RuleExplanation child = Assert.Single(explanation.Children);
        Assert.Same(inner, child.AsyncRule);
    }

    [Fact]
    public async Task IsBrokenAsync_CancelledToken_Propagates()
    {
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();
        IAsyncBusinessRule cancelAware = AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message)
            .WhenAsync((ctx, ct) => { ct.ThrowIfCancellationRequested(); return Task.FromResult(true); }).Build();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelAware.WithSeverity(RuleSeverity.Warning).IsBrokenAsync(new BusinessRuleContext(), source.Token));
    }

    [Fact]
    public void WithSeverity_Async_NullRule_ThrowsArgumentNullException()
    {
        IAsyncBusinessRule rule = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => rule.WithSeverity(RuleSeverity.Warning));
    }

    [Fact]
    public void Constructor_Async_NullInnerRule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SeverityOverrideAsyncBusinessRule(null!, RuleSeverity.Warning));
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static IBusinessRule CreateSyncRule(bool broken)
    {
        return BusinessRuleBuilder.Create(Code)
            .WithMessage(Message)
            .WithSeverity(RuleSeverity.Error)
            .WithName(Name)
            .WithDescription(Description)
            .WithCategory(Category)
            .WithTags("a", "b")
            .When(() => broken)
            .Build();
    }

    private static IAsyncBusinessRule CreateAsyncRule(bool broken)
    {
        return AsyncBusinessRuleBuilder.Create(Code)
            .WithMessage(Message)
            .WithSeverity(RuleSeverity.Error)
            .WithName(Name)
            .WithDescription(Description)
            .WithCategory(Category)
            .WithTags("a", "b")
            .WhenAsync(ctx => Task.FromResult(broken))
            .Build();
    }
}
