namespace Masterly.BusinessRules.UnitTests;

/// <summary>
/// Checker and gate paths that only the edge cases reach: a null rule behind a fail-fast break, the legacy
/// <c>stopOnFirstFailure</c> overloads, the provider-less gate constructor and the passing gated <c>Check</c>.
/// </summary>
public class BusinessRuleCheckerPathTests
{
    private const string Flag = "path-flag";

    [Fact]
    public void EvaluateAll_StopOnFirstFailure_NullRuleAfterTheBreak_ThrowsArgumentException()
    {
        IBusinessRule[] rules = { RuleTestHelper.CreateBrokenRule(), null! };

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => BusinessRuleChecker.EvaluateAll(rules, new RuleEvaluationOptions { StopOnFirstFailure = true }));

        Assert.Equal("rules", exception.ParamName);
        Assert.Contains("position 1", exception.Message);
    }

    [Fact]
    public void CheckAll_StopOnFirstFailureTrue_ReportsOnlyTheFirstBrokenRule()
    {
        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckAll(true, RuleTestHelper.CreateBrokenRule("FIRST"), RuleTestHelper.CreateBrokenRule("SECOND")));

        Assert.Equal("FIRST", Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void CheckAll_StopOnFirstFailureFalse_ReportsEveryBrokenRule()
    {
        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckAll(false, RuleTestHelper.CreateBrokenRule("FIRST"), RuleTestHelper.CreateBrokenRule("SECOND")));

        Assert.Equal(2, exception.BrokenRules.Count);
    }

    [Fact]
    public void CheckAll_ContextAndStopOnFirstFailureTrue_ForwardsTheContextAndStops()
    {
        IBusinessRule contextRule = BusinessRuleBuilder.Create("CONTEXT").WithMessage("m").When(ctx => ctx.Get<bool>("flag")).Build();

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckAll(RuleTestHelper.CreateContext(("flag", true)), true, contextRule, RuleTestHelper.CreateBrokenRule("SECOND")));

        Assert.Equal("CONTEXT", Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void FeatureGated_ProviderlessConstructorWithAProviderInTheContext_AppliesTheGate()
    {
        FeatureGatedBusinessRule gated = new FeatureGatedBusinessRule(RuleTestHelper.CreateBrokenRule("GATED"), Flag);
        BusinessRuleContext enforced = new BusinessRuleContext();
        enforced.Set(RuleFeatureFlags.ContextKey, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));
        BusinessRuleContext off = new BusinessRuleContext();
        off.Set(RuleFeatureFlags.ContextKey, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Off));

        Assert.True(gated.IsBroken(enforced));
        Assert.False(gated.IsBroken(off));
    }

    [Fact]
    public void FeatureGated_ProviderlessConstructorWithoutAProvider_ThrowsRuleConfigurationException()
    {
        FeatureGatedBusinessRule gated = new FeatureGatedBusinessRule(RuleTestHelper.CreateBrokenRule("GATED"), Flag);

        Assert.Throws<RuleConfigurationException>(() => gated.IsBroken(new BusinessRuleContext()));
    }

    [Fact]
    public void FeatureGated_CheckWithAPassingInnerRule_DoesNotThrow()
    {
        IBusinessRule gated = RuleTestHelper.CreatePassingRule("GATED").GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        Exception? withoutContext = Record.Exception(() => gated.Check());
        Exception? withContext = Record.Exception(() => gated.Check(new BusinessRuleContext()));

        Assert.Null(withoutContext);
        Assert.Null(withContext);
    }

    [Fact]
    public void FeatureGated_CheckWithABrokenEnforcedRule_Throws()
    {
        IBusinessRule gated = RuleTestHelper.CreateBrokenRule("GATED").GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => gated.Check(new BusinessRuleContext()));

        Assert.Equal("GATED", Assert.Single(exception.BrokenRules).Code);
    }
}

/// <summary>
/// The async mirror of <see cref="BusinessRuleCheckerPathTests"/>.
/// </summary>
public class AsyncBusinessRuleCheckerPathTests
{
    private const string Flag = "path-flag";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EvaluateAllAsync_StopOnFirstFailure_NullRuleAfterTheBreak_ThrowsArgumentException()
    {
        IAsyncBusinessRule[] rules = { RuleTestHelper.CreateBrokenAsyncRule(), null! };

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), rules, new AsyncRuleEvaluationOptions { StopOnFirstFailure = true }, TestToken));

        Assert.Equal("rules", exception.ParamName);
        Assert.Contains("position 1", exception.Message);
    }

    [Fact]
    public async Task CheckAllAsync_StopOnFirstFailureTrue_ReportsOnlyTheFirstBrokenRule()
    {
        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(true, RuleTestHelper.CreateBrokenAsyncRule("FIRST"), RuleTestHelper.CreateBrokenAsyncRule("SECOND")));

        Assert.Equal("FIRST", Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckAllAsync_ContextAndStopOnFirstFailureTrue_ForwardsTheContextAndStops()
    {
        IAsyncBusinessRule contextRule = AsyncBusinessRuleBuilder.Create("CONTEXT").WithMessage("m").WhenAsync(ctx => Task.FromResult(ctx.Get<bool>("flag"))).Build();

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(RuleTestHelper.CreateContext(("flag", true)), true, contextRule, RuleTestHelper.CreateBrokenAsyncRule("SECOND")));

        Assert.Equal("CONTEXT", Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckAllAsync_ContextAndStopOnFirstFailureFalse_ReportsEveryBrokenRule()
    {
        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), false, RuleTestHelper.CreateBrokenAsyncRule("FIRST"), RuleTestHelper.CreateBrokenAsyncRule("SECOND")));

        Assert.Equal(2, exception.BrokenRules.Count);
    }

    [Fact]
    public async Task FeatureGated_ProviderlessConstructorWithAProviderInTheContext_AppliesTheGate()
    {
        FeatureGatedAsyncBusinessRule gated = new FeatureGatedAsyncBusinessRule(RuleTestHelper.CreateBrokenAsyncRule("GATED"), Flag);
        BusinessRuleContext enforced = new BusinessRuleContext();
        enforced.Set(RuleFeatureFlags.ContextKey, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));
        BusinessRuleContext off = new BusinessRuleContext();
        off.Set(RuleFeatureFlags.ContextKey, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Off));

        Assert.True(await gated.IsBrokenAsync(enforced, TestToken));
        Assert.False(await gated.IsBrokenAsync(off, TestToken));
    }

    [Fact]
    public async Task FeatureGated_ProviderlessConstructorWithoutAProvider_ThrowsRuleConfigurationException()
    {
        FeatureGatedAsyncBusinessRule gated = new FeatureGatedAsyncBusinessRule(RuleTestHelper.CreateBrokenAsyncRule("GATED"), Flag);

        await Assert.ThrowsAsync<RuleConfigurationException>(() => gated.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task FeatureGated_CheckAsyncWithAPassingInnerRule_DoesNotThrow()
    {
        IAsyncBusinessRule gated = RuleTestHelper.CreatePassingAsyncRule("GATED").GatedBy(Flag, RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced));

        Exception? exception = await Record.ExceptionAsync(() => gated.CheckAsync(new BusinessRuleContext(), TestToken));

        Assert.Null(exception);
    }
}
