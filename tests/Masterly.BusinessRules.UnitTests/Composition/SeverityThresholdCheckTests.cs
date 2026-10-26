namespace Masterly.BusinessRules.UnitTests;

public class SeverityThresholdCheckTests
{
    private const string Code = "LIMIT.EXCEEDED";
    private const string Message = "Limit exceeded";
    private const string FlagKey = "flag";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------
    // Sync
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(RuleSeverity.Warning, RuleSeverity.Error, false)]
    [InlineData(RuleSeverity.Error, RuleSeverity.Error, true)]
    [InlineData(RuleSeverity.Info, RuleSeverity.Warning, false)]
    [InlineData(RuleSeverity.Warning, RuleSeverity.Warning, true)]
    [InlineData(RuleSeverity.Error, RuleSeverity.Info, true)]
    [InlineData(RuleSeverity.Info, RuleSeverity.Info, true)]
    public void Check_ThrowOn_ThrowsOnlyAtOrAboveThreshold(RuleSeverity ruleSeverity, RuleSeverity throwOn, bool expectThrow)
    {
        IBusinessRule rule = CreateSyncRule(broken: true, ruleSeverity);

        if (expectThrow)
        {
            Assert.Throws<BusinessRuleValidationException>(() => rule.Check(throwOn));
        }
        else
        {
            rule.Check(throwOn);
        }
    }

    [Fact]
    public void Check_ThrowOn_WhenPassing_DoesNotThrow()
    {
        IBusinessRule rule = CreateSyncRule(broken: false, RuleSeverity.Error);

        rule.Check(RuleSeverity.Info);
    }

    [Fact]
    public void Check_ThrowOn_ExceptionCarriesTheResult()
    {
        IBusinessRule rule = CreateSyncRule(broken: true, RuleSeverity.Error);

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => rule.Check(RuleSeverity.Error));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(Code, result.Code);
        Assert.Empty(exception.CollectedRules);
    }

    [Fact]
    public void Check_WithContextAndThrowOn_ForwardsContext()
    {
        IBusinessRule contextRule = BusinessRuleBuilder.Create(Code).WithMessage(Message).When(ctx => ctx.Get<bool>(FlagKey)).Build();

        Assert.Throws<BusinessRuleValidationException>(() => contextRule.Check(RuleTestHelper.CreateContext((FlagKey, true)), RuleSeverity.Error));
        contextRule.Check(RuleTestHelper.CreateContext((FlagKey, false)), RuleSeverity.Error);
    }

    [Fact]
    public void Check_WithContextAndThrowOn_BelowThreshold_DoesNotThrow()
    {
        IBusinessRule softened = CreateSyncRule(broken: true, RuleSeverity.Error).WithSeverity(RuleSeverity.Warning);

        softened.Check(new BusinessRuleContext(), RuleSeverity.Error);
    }

    [Fact]
    public void Check_ThrowOn_EvaluatesRuleExactlyOnce()
    {
        int evaluations = 0;
        IBusinessRule counting = BusinessRuleBuilder.Create(Code).WithMessage(Message).When(() => { evaluations++; return true; }).Build();

        Assert.Throws<BusinessRuleValidationException>(() => counting.Check(RuleSeverity.Info));

        Assert.Equal(1, evaluations);
    }

    [Fact]
    public void Check_ThrowOn_OnComposedRule_KeepsMemberDetails()
    {
        IBusinessRule group = RuleGroup.All("GROUP", "group", RuleTestHelper.CreateBrokenRule("M1"), RuleTestHelper.CreatePassingRule("M2"));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => group.Check(RuleSeverity.Error));

        Assert.Equal("M1", Assert.Single(Assert.Single(exception.BrokenRules).Details).Code);
    }

    [Fact]
    public void Check_ThrowOn_NullRule_ThrowsArgumentNullException()
    {
        IBusinessRule rule = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => rule.Check(RuleSeverity.Error));
    }

    [Fact]
    public void Check_WithContextAndThrowOn_NullContext_ThrowsArgumentNullException()
    {
        IBusinessRule rule = CreateSyncRule(broken: true, RuleSeverity.Error);
        BusinessRuleContext context = null!; // intentional: exercising the null guard

        Assert.Throws<ArgumentNullException>(() => rule.Check(context, RuleSeverity.Error));
    }

    // ---------------------------------------------------------------------
    // Async
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(RuleSeverity.Warning, RuleSeverity.Error, false)]
    [InlineData(RuleSeverity.Error, RuleSeverity.Error, true)]
    [InlineData(RuleSeverity.Info, RuleSeverity.Warning, false)]
    [InlineData(RuleSeverity.Warning, RuleSeverity.Warning, true)]
    [InlineData(RuleSeverity.Error, RuleSeverity.Info, true)]
    [InlineData(RuleSeverity.Info, RuleSeverity.Info, true)]
    public async Task CheckAsync_ThrowOn_ThrowsOnlyAtOrAboveThreshold(RuleSeverity ruleSeverity, RuleSeverity throwOn, bool expectThrow)
    {
        IAsyncBusinessRule rule = CreateAsyncRule(broken: true, ruleSeverity);
        BusinessRuleContext context = new BusinessRuleContext();

        if (expectThrow)
        {
            await Assert.ThrowsAsync<BusinessRuleValidationException>(() => rule.CheckAsync(context, throwOn, TestToken));
        }
        else
        {
            await rule.CheckAsync(context, throwOn, TestToken);
        }
    }

    [Fact]
    public async Task CheckAsync_ThrowOn_WhenPassing_DoesNotThrow()
    {
        IAsyncBusinessRule rule = CreateAsyncRule(broken: false, RuleSeverity.Error);

        await rule.CheckAsync(new BusinessRuleContext(), RuleSeverity.Info, TestToken);
    }

    [Fact]
    public async Task CheckAsync_ThrowOn_ExceptionCarriesTheResult()
    {
        IAsyncBusinessRule rule = CreateAsyncRule(broken: true, RuleSeverity.Error);

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(() => rule.CheckAsync(new BusinessRuleContext(), RuleSeverity.Error, TestToken));

        Assert.Equal(Code, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckAsync_ThrowOn_ForwardsContext()
    {
        IAsyncBusinessRule contextRule = AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WhenAsync(ctx => Task.FromResult(ctx.Get<bool>(FlagKey))).Build();

        await Assert.ThrowsAsync<BusinessRuleValidationException>(() => contextRule.CheckAsync(RuleTestHelper.CreateContext((FlagKey, true)), RuleSeverity.Error, TestToken));
        await contextRule.CheckAsync(RuleTestHelper.CreateContext((FlagKey, false)), RuleSeverity.Error, TestToken);
    }

    [Fact]
    public async Task CheckAsync_ThrowOn_WithSeverityOverrideBelowThreshold_DoesNotThrow()
    {
        IAsyncBusinessRule softened = CreateAsyncRule(broken: true, RuleSeverity.Error).WithSeverity(RuleSeverity.Warning);

        await softened.CheckAsync(new BusinessRuleContext(), RuleSeverity.Error, TestToken);
    }

    [Fact]
    public async Task CheckAsync_ThrowOn_OnComposedRule_KeepsMemberDetails()
    {
        IAsyncBusinessRule group = AsyncRuleGroup.All("GROUP", "group", RuleTestHelper.CreateBrokenAsyncRule("M1"), RuleTestHelper.CreatePassingAsyncRule("M2"));

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(() => group.CheckAsync(new BusinessRuleContext(), RuleSeverity.Error, TestToken));

        Assert.Equal("M1", Assert.Single(Assert.Single(exception.BrokenRules).Details).Code);
    }

    [Fact]
    public async Task CheckAsync_ThrowOn_CancelledToken_Propagates()
    {
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();
        IAsyncBusinessRule cancelAware = AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message)
            .WhenAsync((ctx, ct) => { ct.ThrowIfCancellationRequested(); return Task.FromResult(true); }).Build();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelAware.CheckAsync(new BusinessRuleContext(), RuleSeverity.Error, source.Token));
    }

    [Fact]
    public async Task CheckAsync_ThrowOn_NullRule_ThrowsArgumentNullException()
    {
        IAsyncBusinessRule rule = null!; // intentional: exercising the null guard

        await Assert.ThrowsAsync<ArgumentNullException>(() => rule.CheckAsync(new BusinessRuleContext(), RuleSeverity.Error, TestToken));
    }

    [Fact]
    public async Task CheckAsync_ThrowOn_NullContext_ThrowsArgumentNullException()
    {
        IAsyncBusinessRule rule = CreateAsyncRule(broken: true, RuleSeverity.Error);
        BusinessRuleContext context = null!; // intentional: exercising the null guard

        await Assert.ThrowsAsync<ArgumentNullException>(() => rule.CheckAsync(context, RuleSeverity.Error, TestToken));
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static IBusinessRule CreateSyncRule(bool broken, RuleSeverity severity)
    {
        return BusinessRuleBuilder.Create(Code).WithMessage(Message).WithSeverity(severity).When(() => broken).Build();
    }

    private static IAsyncBusinessRule CreateAsyncRule(bool broken, RuleSeverity severity)
    {
        return AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WithSeverity(severity).WhenAsync(ctx => Task.FromResult(broken)).Build();
    }
}
