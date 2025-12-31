using System.Threading.Tasks;

namespace Masterly.BusinessRules.UnitTests;

public class SyncToAsyncAdapterTests
{
    [Fact]
    public async Task SyncToAsyncRuleAdapter_IsBrokenAsync_ReturnsCorrectResult()
    {
        RuleA syncRule = new RuleA(true);
        SyncToAsyncRuleAdapter asyncAdapter = new SyncToAsyncRuleAdapter(syncRule);

        Assert.True(await asyncAdapter.IsBrokenAsync(new BusinessRuleContext()));
    }

    [Fact]
    public async Task SyncToAsyncRuleAdapter_PreservesMetadata()
    {
        IBusinessRule syncRule = BusinessRuleBuilder.Create("SYNC.RULE")
            .WithMessage("Sync message")
            .WithSeverity(RuleSeverity.Warning)
            .WithName("Sync Rule")
            .WithDescription("A sync rule")
            .WithCategory("Testing")
            .WithTags("sync", "test")
            .When(() => false)
            .Build();

        SyncToAsyncRuleAdapter asyncAdapter = new SyncToAsyncRuleAdapter(syncRule);

        Assert.Equal("SYNC.RULE", asyncAdapter.Code);
        Assert.Equal("Sync message", asyncAdapter.Message);
        Assert.Equal(RuleSeverity.Warning, asyncAdapter.Severity);
        Assert.Equal("Sync Rule", asyncAdapter.Name);
        Assert.Equal("A sync rule", asyncAdapter.Description);
        Assert.Equal("Testing", asyncAdapter.Category);
        Assert.Contains("sync", asyncAdapter.Tags);
    }

    [Fact]
    public async Task SyncToAsyncRuleAdapter_EvaluateAsync_WhenBroken_ReturnsResult()
    {
        RuleA syncRule = new RuleA(true);
        SyncToAsyncRuleAdapter asyncAdapter = new SyncToAsyncRuleAdapter(syncRule);

        BusinessRuleResult? result = await asyncAdapter.EvaluateAsync(new BusinessRuleContext());

        Assert.NotNull(result);
        Assert.Equal("RULE_A", result.Code);
    }

    [Fact]
    public async Task SyncToAsyncRuleAdapter_EvaluateAsync_WhenNotBroken_ReturnsNull()
    {
        RuleA syncRule = new RuleA(false);
        SyncToAsyncRuleAdapter asyncAdapter = new SyncToAsyncRuleAdapter(syncRule);

        BusinessRuleResult? result = await asyncAdapter.EvaluateAsync(new BusinessRuleContext());

        Assert.Null(result);
    }

    [Fact]
    public async Task SyncToAsyncRuleAdapter_CheckAsync_WhenBroken_Throws()
    {
        RuleA syncRule = new RuleA(true);
        SyncToAsyncRuleAdapter asyncAdapter = new SyncToAsyncRuleAdapter(syncRule);

        await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => asyncAdapter.CheckAsync(new BusinessRuleContext()));
    }

    [Fact]
    public async Task SyncToAsyncRuleAdapter_UsesContext()
    {
        RuleWithContextSupport syncRule = new RuleWithContextSupport();
        SyncToAsyncRuleAdapter asyncAdapter = new SyncToAsyncRuleAdapter(syncRule);

        BusinessRuleContext context = new BusinessRuleContext();
        context.Set("shouldBeBroken", true);

        Assert.True(await asyncAdapter.IsBrokenAsync(context));
    }

    [Fact]
    public void ToAsync_Extension_CreatesAdapter()
    {
        RuleA syncRule = new RuleA(false);
        IAsyncBusinessRule asyncRule = syncRule.ToAsync();

        Assert.IsType<SyncToAsyncRuleAdapter>(asyncRule);
    }
}

public class AsyncToSyncAdapterTests
{
    [Fact]
    public void AsyncToSyncRuleAdapter_IsBroken_ReturnsCorrectResult()
    {
        AsyncRuleA asyncRule = new AsyncRuleA(true);
        AsyncToSyncRuleAdapter syncAdapter = new AsyncToSyncRuleAdapter(asyncRule);

        Assert.True(syncAdapter.IsBroken());
    }

    [Fact]
    public void AsyncToSyncRuleAdapter_PreservesMetadata()
    {
        IAsyncBusinessRule asyncRule = AsyncBusinessRuleBuilder.Create("ASYNC.RULE")
            .WithMessage("Async message")
            .WithSeverity(RuleSeverity.Info)
            .WithName("Async Rule")
            .WithDescription("An async rule")
            .WithCategory("Async Testing")
            .WithTags("async", "test")
            .WhenAsync((ctx, ct) => Task.FromResult(false))
            .Build();

        AsyncToSyncRuleAdapter syncAdapter = new AsyncToSyncRuleAdapter(asyncRule);

        Assert.Equal("ASYNC.RULE", syncAdapter.Code);
        Assert.Equal("Async message", syncAdapter.Message);
        Assert.Equal(RuleSeverity.Info, syncAdapter.Severity);
        Assert.Equal("Async Rule", syncAdapter.Name);
        Assert.Equal("An async rule", syncAdapter.Description);
        Assert.Equal("Async Testing", syncAdapter.Category);
        Assert.Contains("async", syncAdapter.Tags);
    }

    [Fact]
    public void AsyncToSyncRuleAdapter_IsBroken_WithContext_Works()
    {
        IAsyncBusinessRule asyncRule = AsyncBusinessRuleBuilder.Create("CTX.ASYNC")
            .WithMessage("Context async")
            .WhenAsync((ctx, ct) => Task.FromResult(ctx.Get<bool>("broken")))
            .Build();

        BusinessRuleContext context = new BusinessRuleContext();
        context.Set("broken", true);

        AsyncToSyncRuleAdapter syncAdapter = new AsyncToSyncRuleAdapter(asyncRule, context);

        Assert.True(syncAdapter.IsBroken());
    }

    [Fact]
    public void AsyncToSyncRuleAdapter_Check_WhenBroken_Throws()
    {
        AsyncRuleA asyncRule = new AsyncRuleA(true);
        AsyncToSyncRuleAdapter syncAdapter = new AsyncToSyncRuleAdapter(asyncRule);

        Assert.Throws<BusinessRuleValidationException>(() => syncAdapter.Check());
    }

    [Fact]
    public void AsyncToSyncRuleAdapter_Evaluate_ReturnsResult()
    {
        AsyncRuleA asyncRule = new AsyncRuleA(true);
        AsyncToSyncRuleAdapter syncAdapter = new AsyncToSyncRuleAdapter(asyncRule);

        BusinessRuleResult result = syncAdapter.Evaluate();

        Assert.Equal("ASYNC_RULE_A", result.Code);
    }
}
