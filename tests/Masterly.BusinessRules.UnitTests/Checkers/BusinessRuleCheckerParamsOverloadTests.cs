namespace Masterly.BusinessRules.UnitTests;

/// <summary>
/// The params forms of the typed checkers (rules of different classes listed directly) and the token-free filtered
/// async overloads, plus the overload-resolution cases they must not disturb.
/// </summary>
public class BusinessRuleCheckerParamsOverloadTests
{
    [Fact]
    public void CheckAll_SubjectWithRulesOfDifferentClasses_ThrowsWithTheBrokenCodes()
    {
        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckAll(50, new OverLimitRule(10), new NegativeRule()));

        Assert.Equal(OverLimitRule.RuleCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void CheckAll_SubjectWithPassingRules_DoesNotThrow()
    {
        Exception? exception = Record.Exception(() => BusinessRuleChecker.CheckAll(5, new OverLimitRule(10), new NegativeRule()));

        Assert.Null(exception);
    }

    [Fact]
    public void CheckAll_SubjectWithASingleTypedRule_ChecksIt()
    {
        Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.CheckAll(-1, new NegativeRule()));
    }

    [Fact]
    public void EvaluateAll_SubjectWithRulesOfDifferentClasses_ReportsEveryRuleInOrder()
    {
        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(-5, new NegativeRule(), new OverLimitRule(10));

        Assert.Equal(new[] { NegativeRule.RuleCode, OverLimitRule.RuleCode }, result.Entries.Select(entry => entry.Code));
        Assert.Equal(NegativeRule.RuleCode, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public void CheckAll_SingleUntypedRule_StillBindsToTheUntypedParamsOverload()
    {
        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.CheckAll(RuleTestHelper.CreateBrokenRule("UNTYPED")));

        Assert.Equal("UNTYPED", Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void CheckAll_TwoUntypedRules_StillBindToTheUntypedParamsOverload()
    {
        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckAll(RuleTestHelper.CreateBrokenRule("U1"), RuleTestHelper.CreateBrokenRule("U2")));

        Assert.Equal(new[] { "U1", "U2" }, exception.BrokenRules.Select(result => result.Code));
    }

    [Fact]
    public void CheckAll_ParamsWithANullRule_ThrowsArgumentExceptionNamingThePosition()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => BusinessRuleChecker.CheckAll(1, new NegativeRule(), (IBusinessRule<int>)null!));

        Assert.Equal("rules", exception.ParamName);
        Assert.Contains("position 1", exception.Message);
    }

    [Fact]
    public void EvaluateAll_ParamsWithANullArray_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => BusinessRuleChecker.EvaluateAll(1, new NegativeRule(), (IBusinessRule<int>[])null!));

        Assert.Equal("others", exception.ParamName);
    }

    private sealed class OverLimitRule : BaseBusinessRule<int>
    {
        public const string RuleCode = "OVER.LIMIT";

        private readonly int _limit;

        public OverLimitRule(int limit)
        {
            _limit = limit;
        }

        public override string Code => RuleCode;

        public override string Message => "Over the limit.";

        public override bool IsBroken(int subject) => subject > _limit;
    }

    private sealed class NegativeRule : BaseBusinessRule<int>
    {
        public const string RuleCode = "NEGATIVE";

        public override string Code => RuleCode;

        public override string Message => "Negative.";

        public override bool IsBroken(int subject) => subject < 0;
    }
}

/// <summary>
/// The async mirror of <see cref="BusinessRuleCheckerParamsOverloadTests"/>.
/// </summary>
public class AsyncBusinessRuleCheckerParamsOverloadTests
{
    private const string Pci = "pci";
    private const string Gdpr = "gdpr";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CheckAllAsync_SubjectWithRulesOfDifferentClasses_ThrowsWithTheBrokenCodes()
    {
        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), 50, new OverLimitAsyncRule(10), new NegativeAsyncRule()));

        Assert.Equal(OverLimitAsyncRule.RuleCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task EvaluateAllAsync_SubjectWithRulesOfDifferentClasses_ReportsEveryRuleInOrder()
    {
        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), -5, new NegativeAsyncRule(), new OverLimitAsyncRule(10));

        Assert.Equal(new[] { NegativeAsyncRule.RuleCode, OverLimitAsyncRule.RuleCode }, result.Entries.Select(entry => entry.Code));
        Assert.Equal(NegativeAsyncRule.RuleCode, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckAllAsync_ContextAndTwoUntypedRules_StillBindToTheUntypedParamsOverload()
    {
        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), RuleTestHelper.CreateBrokenAsyncRule("U1"), RuleTestHelper.CreateBrokenAsyncRule("U2")));

        Assert.Equal(new[] { "U1", "U2" }, exception.BrokenRules.Select(result => result.Code));
    }

    [Fact]
    public async Task CheckAllAsync_ParamsWithANullContext_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => AsyncBusinessRuleChecker.CheckAllAsync(null!, 1, new NegativeAsyncRule(), new OverLimitAsyncRule(1)));
    }

    [Fact]
    public async Task EvaluateAllAsync_ParamsWithANullRule_ThrowsArgumentExceptionNamingThePosition()
    {
        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() => AsyncBusinessRuleChecker.EvaluateAllAsync(new BusinessRuleContext(), 1, new NegativeAsyncRule(), (IAsyncBusinessRule<int>)null!));

        Assert.Equal("rules", exception.ParamName);
    }

    [Fact]
    public async Task CheckByTagsAsync_TagsListedAfterTheRules_ChecksOnlyTheTaggedRules()
    {
        IAsyncBusinessRule[] rules = { Tagged("PCI.RULE", Pci), Tagged("GDPR.RULE", Gdpr), Tagged("OTHER.RULE", "other") };

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckByTagsAsync(new BusinessRuleContext(), rules, Pci, Gdpr));

        Assert.Equal(new[] { "PCI.RULE", "GDPR.RULE" }, exception.BrokenRules.Select(result => result.Code));
    }

    [Fact]
    public async Task CheckByTagsAsync_NoTags_ChecksNothing()
    {
        IAsyncBusinessRule[] rules = { Tagged("PCI.RULE", Pci) };

        Exception? exception = await Record.ExceptionAsync(() => AsyncBusinessRuleChecker.CheckByTagsAsync(new BusinessRuleContext(), rules));

        Assert.Null(exception);
    }

    [Fact]
    public async Task CheckByTagsAsync_TokenThenTags_StillBindsToTheTokenOverload()
    {
        using CancellationTokenSource cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        IAsyncBusinessRule[] rules = { Tagged("PCI.RULE", Pci) };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AsyncBusinessRuleChecker.CheckByTagsAsync(new BusinessRuleContext(), rules, cancelled.Token, Pci));
    }

    [Fact]
    public async Task TypedCheckByTagsAsync_TagsListedAfterTheRules_ChecksOnlyTheTaggedRules()
    {
        IAsyncBusinessRule<int>[] rules = { TypedTagged("PCI.RULE", Pci), TypedTagged("OTHER.RULE", "other") };

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckByTagsAsync(new BusinessRuleContext(), 1, rules, Pci));

        Assert.Equal("PCI.RULE", Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task TypedCheckBySeverityAsync_SeveritiesListedAfterTheRules_ChecksOnlyThoseSeverities()
    {
        IAsyncBusinessRule<int>[] rules = { TypedWithSeverity("WARN.RULE", RuleSeverity.Warning), TypedWithSeverity("ERR.RULE", RuleSeverity.Error) };

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckBySeverityAsync(new BusinessRuleContext(), 1, rules, RuleSeverity.Error, RuleSeverity.Info));

        Assert.Equal("ERR.RULE", Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task TypedCheckBySeverityAsync_TokenThenSeverities_StillBindsToTheTokenOverload()
    {
        IAsyncBusinessRule<int>[] rules = { TypedWithSeverity("WARN.RULE", RuleSeverity.Warning) };

        Exception? exception = await Record.ExceptionAsync(() => AsyncBusinessRuleChecker.CheckBySeverityAsync(new BusinessRuleContext(), 1, rules, TestToken, RuleSeverity.Error));

        Assert.Null(exception);
    }

    private static IAsyncBusinessRule Tagged(string code, string tag)
    {
        return AsyncBusinessRuleBuilder.Create(code).WithMessage(code).WithTags(tag).WhenAsync(ctx => Task.FromResult(true)).Build();
    }

    private static IAsyncBusinessRule<int> TypedTagged(string code, string tag)
    {
        return AsyncBusinessRuleBuilder<int>.Create(code).WithMessage(code).WithTags(tag).WhenAsync(value => Task.FromResult(true)).Build();
    }

    private static IAsyncBusinessRule<int> TypedWithSeverity(string code, RuleSeverity severity)
    {
        return AsyncBusinessRuleBuilder<int>.Create(code).WithMessage(code).WithSeverity(severity).WhenAsync(value => Task.FromResult(true)).Build();
    }

    private sealed class OverLimitAsyncRule : BaseAsyncBusinessRule<int>
    {
        public const string RuleCode = "OVER.LIMIT";

        private readonly int _limit;

        public OverLimitAsyncRule(int limit)
        {
            _limit = limit;
        }

        public override string Code => RuleCode;

        public override string Message => "Over the limit.";

        public override Task<bool> IsBrokenAsync(int subject, BusinessRuleContext context, CancellationToken cancellationToken = default) => Task.FromResult(subject > _limit);
    }

    private sealed class NegativeAsyncRule : BaseAsyncBusinessRule<int>
    {
        public const string RuleCode = "NEGATIVE";

        public override string Code => RuleCode;

        public override string Message => "Negative.";

        public override Task<bool> IsBrokenAsync(int subject, BusinessRuleContext context, CancellationToken cancellationToken = default) => Task.FromResult(subject < 0);
    }
}
