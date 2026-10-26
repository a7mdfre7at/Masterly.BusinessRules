namespace Masterly.BusinessRules.UnitTests;

public class AsyncBusinessRuleCheckerRichResultTests
{
    private const string Payments = "Payments";
    private const string Identity = "Identity";
    private const string CustomerFacing = "customer-facing";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private sealed class ThrowingRule : BaseAsyncBusinessRule
    {
        public override string Code => "THROWS";
        public override string Message => "Throws";
        public override string Name => "ThrowingRule";
        public override string Category => Payments;
        public override IReadOnlyList<string> Tags => new[] { CustomerFacing };

        public override Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("boom");
    }

    private sealed class ThrowingMetadataRule : BaseAsyncBusinessRule
    {
        public override string Code => throw new InvalidOperationException("no code");
        public override string Message => "Throws";

        public override Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("boom");
    }

    private static IAsyncBusinessRule Rule(string code, string category, params string[] tags)
        => AsyncBusinessRuleBuilder.Create(code)
            .WithName($"{code}-name")
            .WithCategory(category)
            .WithTags(tags)
            .WhenAsync((_, _) => Task.FromResult(true))
            .Build();

    [Fact]
    public async Task CheckAllAsync_ExceptionResultsCarryMetadata()
    {
        IAsyncBusinessRule[] rules = { Rule("A", Payments, CustomerFacing), Rule("B", Identity, "internal"), RuleTestHelper.CreatePassingAsyncRule() };

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(rules, cancellationToken: TestToken));

        Assert.Equal(new[] { "A-name", "B-name" }, exception.BrokenRules.Select(result => result.Name));
        Assert.Equal(new[] { Payments, Identity }, exception.BrokenRules.Select(result => result.Category));
        Assert.Equal("A", Assert.Single(exception.ByTag(CustomerFacing)).Code);
        Assert.Equal("B", Assert.Single(exception.ByCategory(Identity)).Code);
    }

    [Fact]
    public async Task EvaluateAllAsync_Parallel_ResultsCarryMetadata()
    {
        IAsyncBusinessRule[] rules = { Rule("A", Payments, CustomerFacing), Rule("B", Identity, "internal") };

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(rules, runInParallel: true, TestToken);

        Assert.Equal("A", Assert.Single(result.ByTag("CUSTOMER-FACING")).Code);
        Assert.Equal("B", Assert.Single(result.ByCategory(Identity)).Code);
        Assert.All(result.Entries, entry => Assert.Equal(entry.AsyncRule!.Name, entry.Result!.Name));
    }

    [Fact]
    public async Task CheckAllAsync_WithThrowOn_CollectedRulesCarryMetadata()
    {
        IAsyncBusinessRule warning = Rule("W", Payments, CustomerFacing).WithSeverity(RuleSeverity.Warning);
        IAsyncBusinessRule error = Rule("E", Identity);

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new[] { warning, error }, new AsyncRuleEvaluationOptions { ThrowOn = RuleSeverity.Error }, TestToken));

        BusinessRuleResult collected = Assert.Single(exception.CollectedRules);
        Assert.Equal("W-name", collected.Name);
        Assert.True(collected.HasTag(CustomerFacing));
        Assert.Empty(exception.ByTag(CustomerFacing));
    }

    [Fact]
    public async Task EvaluateAllAsync_TreatAsBroken_StandInResultCarriesMetadata()
    {
        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(
            new IAsyncBusinessRule[] { new ThrowingRule() },
            new AsyncRuleEvaluationOptions { OnFault = FaultPolicy.TreatAsBroken },
            TestToken);

        BusinessRuleResult standIn = Assert.Single(result.BrokenRules);
        Assert.Equal("ThrowingRule", standIn.Name);
        Assert.Equal(Payments, standIn.Category);
        Assert.True(standIn.HasTag(CustomerFacing));
        Assert.Null(standIn.Detail);
    }

    [Fact]
    public async Task EvaluateAllAsync_TreatAsBroken_ThrowingMetadata_FallsBackToTypeName()
    {
        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(
            new IAsyncBusinessRule[] { new ThrowingMetadataRule() },
            new AsyncRuleEvaluationOptions { OnFault = FaultPolicy.TreatAsBroken },
            TestToken);

        BusinessRuleResult standIn = Assert.Single(result.BrokenRules);
        Assert.Equal(nameof(ThrowingMetadataRule), standIn.Code);
        Assert.Equal(nameof(ThrowingMetadataRule), standIn.Name);
        Assert.Empty(standIn.Tags);
    }

    [Fact]
    public async Task CheckAllAsync_ComposedRule_MemberResultsCarryMetadata()
    {
        IAsyncBusinessRule group = AsyncRuleGroup.All("G", "Group", Rule("A", Payments, CustomerFacing), Rule("B", Identity));

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), group));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(nameof(GroupAsyncBusinessRule), result.Name);
        Assert.Equal(new[] { "A-name", "B-name" }, result.Details.Select(member => member.Name));
        Assert.Equal("A", Assert.Single(exception.Flatten().Where(leaf => leaf.HasTag(CustomerFacing))).Code);
    }

    [Fact]
    public async Task CheckByCategoryAsync_ExceptionResultsCarryCategory()
    {
        IAsyncBusinessRule[] rules = { Rule("A", Payments, CustomerFacing), Rule("B", Identity) };

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckByCategoryAsync(new BusinessRuleContext(), rules, Identity, TestToken));

        Assert.Equal(Identity, Assert.Single(exception.BrokenRules).Category);
    }

    [Fact]
    public async Task CheckAllAsync_NoBrokenRules_DoesNotThrow()
    {
        await AsyncBusinessRuleChecker.CheckAllAsync(RuleTestHelper.CreatePassingAsyncRule(), RuleTestHelper.CreatePassingAsyncRule());
    }
}
