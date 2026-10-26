namespace Masterly.BusinessRules.UnitTests;

/// <summary>
/// Async mirror of <see cref="ContextAwareEvaluateTests"/>: every async construction site populates the result's
/// metadata, decorators keep the inner rule's <c>Describe</c> details, and the adapter carries the sync rule's details.
/// </summary>
public class AsyncRichResultTests
{
    private const string AmountKey = "amount";
    private const string Category = "Payments";
    private const string Tag = "customer-facing";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private sealed class ContextDescribeAsyncRule : BaseAsyncBusinessRule
    {
        private readonly bool _broken;

        public ContextDescribeAsyncRule(bool broken = true) => _broken = broken;

        public int Evaluations { get; private set; }

        public override string Code => "CTX";
        public override string Message => "Context rule";
        public override string Name => "ContextRule";
        public override string Category => AsyncRichResultTests.Category;
        public override IReadOnlyList<string> Tags => new[] { Tag };

        public override Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            Evaluations++;
            return Task.FromResult(_broken);
        }

        protected override RuleResultDetails Describe(BusinessRuleContext? context)
        {
            decimal amount = context!.Get<decimal>(AmountKey); // intentional: async rules always receive a context
            return RuleResultDetails.Detail($"Amount is {amount}").Actual(amount);
        }
    }

    private sealed class ContextDescribeSyncRule : BaseBusinessRule
    {
        public override string Code => "SYNC";
        public override string Message => "Sync rule";
        public override string Name => "SyncName";

        public override bool IsBroken() => true;

        protected override RuleResultDetails Describe(BusinessRuleContext? context)
        {
            decimal amount = context!.Get<decimal>(AmountKey); // intentional: the adapter always forwards a context
            return RuleResultDetails.Detail($"Amount is {amount}").Remediation("Lower it.");
        }
    }

    private static BusinessRuleContext Context(decimal amount) => RuleTestHelper.CreateContext((AmountKey, amount));

    [Fact]
    public async Task AndAsyncBusinessRule_Broken_ResultCarriesNodeMetadataAndMemberDetails()
    {
        AndAsyncBusinessRule node = new AndAsyncBusinessRule(new ContextDescribeAsyncRule(), new ContextDescribeAsyncRule());

        BusinessRuleResult? result = await node.EvaluateAsync(Context(42m), TestToken);

        Assert.NotNull(result);
        Assert.Equal(nameof(AndAsyncBusinessRule), result!.Name);
        Assert.Equal(string.Empty, result.Category);
        Assert.Empty(result.Tags);
        Assert.Equal(2, result.Details.Count);
        Assert.All(result.Details, member =>
        {
            Assert.Equal("ContextRule", member.Name);
            Assert.Equal("Amount is 42", member.Detail);
        });
    }

    [Fact]
    public async Task OrAsyncBusinessRule_Broken_ResultCarriesNodeMetadata()
    {
        OrAsyncBusinessRule node = new OrAsyncBusinessRule(new ContextDescribeAsyncRule(), RuleTestHelper.CreatePassingAsyncRule());

        BusinessRuleResult? result = await node.EvaluateAsync(Context(1m), TestToken);

        Assert.Equal(nameof(OrAsyncBusinessRule), result!.Name);
        Assert.Equal(Category, Assert.Single(result.Details).Category);
    }

    [Fact]
    public async Task NotAsyncBusinessRule_Broken_ResultCarriesNodeMetadata()
    {
        NotAsyncBusinessRule node = new NotAsyncBusinessRule(new ContextDescribeAsyncRule(broken: false));

        BusinessRuleResult? result = await node.EvaluateAsync(Context(1m), TestToken);

        Assert.Equal(nameof(NotAsyncBusinessRule), result!.Name);
        Assert.Equal(node.Code, result.Code);
    }

    [Fact]
    public async Task GroupAsyncBusinessRule_Sequential_ResultCarriesNodeMetadataAndMemberDetails()
    {
        GroupAsyncBusinessRule group = AsyncRuleGroup.All("G", "Group", new ContextDescribeAsyncRule(), RuleTestHelper.CreatePassingAsyncRule());

        BusinessRuleResult? result = await group.EvaluateAsync(Context(5m), TestToken);

        Assert.Equal(nameof(GroupAsyncBusinessRule), result!.Name);
        Assert.Equal("Amount is 5", Assert.Single(result.Details).Detail);
    }

    [Fact]
    public async Task GroupAsyncBusinessRule_Parallel_ResultCarriesNodeMetadataAndMemberDetails()
    {
        GroupAsyncBusinessRule group = AsyncRuleGroup.All("G", "Group", new ContextDescribeAsyncRule(), RuleTestHelper.CreatePassingAsyncRule())
            .WithParallelEvaluation();

        BusinessRuleResult? result = await group.EvaluateAsync(Context(5m), TestToken);

        Assert.Equal(nameof(GroupAsyncBusinessRule), result!.Name);
        Assert.Equal(5m, Assert.Single(result.Details).Actual);
    }

    [Fact]
    public async Task CompositeAsyncBusinessRule_Rules_ResultCarriesNodeMetadataAndMemberDetails()
    {
        CompositeAsyncBusinessRule composite = new CompositeAsyncBusinessRule(new[] { new ContextDescribeAsyncRule() });

        BusinessRuleResult? result = await composite.EvaluateAsync(Context(3m), TestToken);

        Assert.Equal("CompositeAsyncBusinessRule", result!.Name);
        Assert.Equal("Amount is 3", Assert.Single(result.Details).Detail);
    }

    [Fact]
    public async Task CompositeAsyncBusinessRule_Delegate_ResultCarriesMetadata()
    {
        CompositeAsyncBusinessRule composite = new CompositeAsyncBusinessRule((_, _) => Task.FromResult(true), "Delegate failed", "DELEGATE");

        BusinessRuleResult? result = await composite.EvaluateAsync(Context(1m), TestToken);

        Assert.Equal("DELEGATE", result!.Code);
        Assert.Equal("CompositeAsyncBusinessRule", result.Name);
        Assert.Empty(result.Details);
    }

    [Fact]
    public async Task CachedAsyncBusinessRule_FreshEvaluation_KeepsInnerDetails()
    {
        CachedAsyncBusinessRule cached = new CachedAsyncBusinessRule(new ContextDescribeAsyncRule(), TimeSpan.FromMinutes(5));

        BusinessRuleResult? result = await cached.EvaluateAsync(Context(8m), TestToken);

        Assert.Equal("Amount is 8", result!.Detail);
        Assert.Equal("ContextRule", result.Name);
    }

    [Fact]
    public async Task CachedAsyncBusinessRule_CacheHit_CarriesForwardedMetadataWithoutDetails()
    {
        ContextDescribeAsyncRule inner = new ContextDescribeAsyncRule();
        CachedAsyncBusinessRule cached = new CachedAsyncBusinessRule(inner, TimeSpan.FromMinutes(5));
        await cached.IsBrokenAsync(Context(1m), TestToken);

        BusinessRuleResult? result = await cached.EvaluateAsync(Context(2m), TestToken);

        Assert.Equal(1, inner.Evaluations);
        Assert.Equal("ContextRule", result!.Name);
        Assert.Equal(Category, result.Category);
        Assert.True(result.HasTag(Tag));
        Assert.Null(result.Detail);
    }

    [Fact]
    public async Task ConditionalAsyncBusinessRule_Broken_KeepsInnerDetails()
    {
        ConditionalAsyncBusinessRule conditional = new ConditionalAsyncBusinessRule(new ContextDescribeAsyncRule(), _ => true);

        BusinessRuleResult? result = await conditional.EvaluateAsync(Context(4m), TestToken);

        Assert.Equal("Amount is 4", result!.Detail);
        Assert.Equal(Category, result.Category);
    }

    [Fact]
    public async Task SeverityOverrideAsyncBusinessRule_Broken_KeepsDetailsAndOverridesSeverity()
    {
        IAsyncBusinessRule softened = new ContextDescribeAsyncRule().WithSeverity(RuleSeverity.Warning);

        BusinessRuleResult? result = await softened.EvaluateAsync(Context(9m), TestToken);

        Assert.Equal(RuleSeverity.Warning, result!.Severity);
        Assert.Equal("Amount is 9", result.Detail);
        Assert.Equal(9m, result.Actual);
        Assert.Equal("ContextRule", result.Name);
        Assert.True(result.HasTag(Tag));
    }

    [Fact]
    public async Task SeverityOverrideAsyncBusinessRule_OverGroup_KeepsMemberDetails()
    {
        IAsyncBusinessRule softened = AsyncRuleGroup.All("G", "Group", new ContextDescribeAsyncRule()).WithSeverity(RuleSeverity.Info);

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => softened.CheckAsync(Context(7m), TestToken));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(RuleSeverity.Info, result.Severity);
        Assert.Equal(nameof(GroupAsyncBusinessRule), result.Name);
        Assert.Equal("Amount is 7", Assert.Single(result.Details).Detail);
    }

    [Fact]
    public async Task SyncToAsyncRuleAdapter_EvaluateAsync_CarriesSyncRuleContextDetails()
    {
        SyncToAsyncRuleAdapter adapter = new SyncToAsyncRuleAdapter(new ContextDescribeSyncRule());

        BusinessRuleResult? result = await adapter.EvaluateAsync(Context(12m), TestToken);

        Assert.Equal("SyncName", result!.Name);
        Assert.Equal("Amount is 12", result.Detail);
        Assert.Equal("Lower it.", result.Remediation);
    }

    [Fact]
    public async Task SyncToAsyncRuleAdapter_CheckAsync_CarriesSyncRuleContextDetails()
    {
        SyncToAsyncRuleAdapter adapter = new SyncToAsyncRuleAdapter(new ContextDescribeSyncRule());

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => adapter.CheckAsync(Context(13m), TestToken));

        Assert.Equal("Amount is 13", Assert.Single(exception.BrokenRules).Detail);
    }

    [Fact]
    public async Task Checker_Parallel_ResultsCarryMetadataAndDetails()
    {
        IAsyncBusinessRule[] rules = { new ContextDescribeAsyncRule(), RuleTestHelper.CreatePassingAsyncRule() };

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(Context(21m), rules, runInParallel: true, TestToken);

        BusinessRuleResult broken = Assert.Single(result.BrokenRules);
        Assert.Equal("ContextRule", broken.Name);
        Assert.Equal("Amount is 21", broken.Detail);
        Assert.Single(result.ByCategory(Category));
    }
}
