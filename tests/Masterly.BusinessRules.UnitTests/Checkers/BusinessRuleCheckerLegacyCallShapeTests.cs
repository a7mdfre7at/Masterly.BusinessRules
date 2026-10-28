#nullable disable

namespace Masterly.BusinessRules.UnitTests;

/// <summary>
/// Regression tests for 2.x-shaped checker calls that pass a positional <c>default</c> or <c>null</c> after the
/// rules. In 2.0.0 each of these bound to the boolean / context / token overload and reported the broken rule; 3.0.0
/// must bind them the same way instead of to a subject-typed or options overload (which threw
/// <see cref="ArgumentNullException"/> for <c>rules</c> or <c>options</c>, or did not compile). Nullable is disabled
/// because that is how a 2.x consumer compiles the calls: without the CS8625 hint.
/// </summary>
public class BusinessRuleCheckerLegacyCallShapeTests
{
    private const string BrokenCode = "SYNC.BROKEN";

    [Fact]
    public void CheckAll_RulesThenDefault_BindsTheStopOnFirstFailureOverload()
    {
        IBusinessRule[] rules = Rules();

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.CheckAll(rules, default));

        Assert.Equal(BrokenCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void CheckAll_ListThenDefault_BindsTheStopOnFirstFailureOverload()
    {
        List<IBusinessRule> rules = new List<IBusinessRule>(Rules());

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.CheckAll(rules, default));

        Assert.Equal(BrokenCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void CheckAll_RulesThenDefaultTwice_BindsTheObserverOverload()
    {
        IBusinessRule[] rules = Rules();

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.CheckAll(rules, default, default));

        Assert.Equal(BrokenCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void CheckAll_RulesThenDefaultAndNullObserver_BindsTheObserverOverload()
    {
        IBusinessRule[] rules = Rules();

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.CheckAll(rules, default, null));

        Assert.Equal(BrokenCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void EvaluateAll_RulesThenDefault_BindsTheContextOverload()
    {
        IBusinessRule[] rules = Rules();

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rules, default);

        Assert.True(result.HasBrokenRules);
        Assert.Equal(BrokenCode, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public void EvaluateAll_RulesThenNull_BindsTheContextOverload()
    {
        IBusinessRule[] rules = Rules();

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rules, null);

        Assert.Equal(BrokenCode, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public void CheckAll_RulesThenNullContextAndOptions_BindsTheOptionsOverload()
    {
        IBusinessRule[] rules = Rules();

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckAll(rules, null, new RuleEvaluationOptions { ThrowOn = RuleSeverity.Error }));

        Assert.Equal(BrokenCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void CheckAll_TypedNullOptions_StillThrowsArgumentNullExceptionForOptions()
    {
        IBusinessRule[] rules = Rules();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => BusinessRuleChecker.CheckAll(rules, (RuleEvaluationOptions)null));

        Assert.Equal("options", exception.ParamName);
    }

    private static IBusinessRule[] Rules()
    {
        return new[] { RuleTestHelper.CreateBrokenRule(BrokenCode) };
    }
}

/// <summary>
/// The async mirror of <see cref="BusinessRuleCheckerLegacyCallShapeTests"/>: a positional <c>default</c> for the
/// 2.x <c>CancellationToken</c> / <c>bool</c> parameters binds the 2.x overload and reports the broken rule. The
/// token-free calls are the point of these tests, so xUnit1051 is suppressed for them.
/// </summary>
#pragma warning disable xUnit1051 // the positional default token is the call shape under test
public class AsyncBusinessRuleCheckerLegacyCallShapeTests
{
    private const string BrokenCode = "ASYNC.BROKEN";
    private const string Tag = "x";
    private const string Category = "Orders";

    [Fact]
    public async Task CheckBySeverityAsync_ContextRulesDefaultSeverity_BindsTheTokenOverload()
    {
        IAsyncBusinessRule[] rules = Rules();

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckBySeverityAsync(new BusinessRuleContext(), rules, default, RuleSeverity.Error));

        Assert.Equal(BrokenCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckBySeverityAsync_ListThenDefaultSeverity_BindsTheTokenOverload()
    {
        List<IAsyncBusinessRule> rules = new List<IAsyncBusinessRule>(Rules());

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckBySeverityAsync(new BusinessRuleContext(), rules, default, RuleSeverity.Error));

        Assert.Equal(BrokenCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckByTagsAsync_ContextRulesDefaultTag_BindsTheTokenOverload()
    {
        IAsyncBusinessRule[] rules = Rules();

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckByTagsAsync(new BusinessRuleContext(), rules, default, Tag));

        Assert.Equal(BrokenCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckByCategoryAsync_ContextRulesCategoryDefault_BindsTheTokenOverload()
    {
        IAsyncBusinessRule[] rules = Rules();

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckByCategoryAsync(new BusinessRuleContext(), rules, Category, default));

        Assert.Equal(BrokenCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task EvaluateAllAsync_ContextRulesDefault_BindsTheRunInParallelOverload()
    {
        IAsyncBusinessRule[] rules = Rules();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), rules, default);

        Assert.True(result.HasBrokenRules);
        Assert.Equal(BrokenCode, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public async Task EvaluateAllAsync_ContextRulesDefaultDefault_BindsTheRunInParallelOverload()
    {
        IAsyncBusinessRule[] rules = Rules();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), rules, default, default);

        Assert.Equal(BrokenCode, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public async Task EvaluateAllAsync_RulesDefault_BindsTheRunInParallelOverload()
    {
        IAsyncBusinessRule[] rules = Rules();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(rules, default);

        Assert.Equal(BrokenCode, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public async Task EvaluateAllAsync_RulesDefaultDefault_BindsTheRunInParallelOverload()
    {
        IAsyncBusinessRule[] rules = Rules();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(rules, default, default);

        Assert.Equal(BrokenCode, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckAllAsync_ContextRulesDefault_BindsTheStopOnFirstFailureOverload()
    {
        IAsyncBusinessRule[] rules = Rules();

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), rules, default));

        Assert.Equal(BrokenCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckAllAsync_ContextRulesDefaultDefault_BindsTheStopOnFirstFailureOverload()
    {
        IAsyncBusinessRule[] rules = Rules();

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), rules, default, default));

        Assert.Equal(BrokenCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckAllAsync_RulesDefault_BindsTheStopOnFirstFailureOverload()
    {
        IAsyncBusinessRule[] rules = Rules();

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(rules, default));

        Assert.Equal(BrokenCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckAllAsync_RulesDefaultDefault_BindsTheStopOnFirstFailureOverload()
    {
        IAsyncBusinessRule[] rules = Rules();

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(rules, default, default));

        Assert.Equal(BrokenCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckAllAsync_TypedNullOptions_StillThrowsArgumentNullExceptionForOptions()
    {
        IAsyncBusinessRule[] rules = Rules();

        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), rules, (AsyncRuleEvaluationOptions)null, TestContext.Current.CancellationToken));

        Assert.Equal("options", exception.ParamName);
    }

    private static IAsyncBusinessRule[] Rules()
    {
        return new[]
        {
            AsyncBusinessRuleBuilder.Create(BrokenCode)
                .WithMessage("async broken")
                .WithSeverity(RuleSeverity.Error)
                .WithCategory(Category)
                .WithTags(Tag)
                .WhenAsync(context => Task.FromResult(true))
                .Build()
        };
    }
}
#pragma warning restore xUnit1051
