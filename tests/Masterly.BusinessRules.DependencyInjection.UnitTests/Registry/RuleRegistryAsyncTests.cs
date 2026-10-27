namespace Masterly.BusinessRules.DependencyInjection.UnitTests;

public class RuleRegistryAsyncTests
{
    private const string CheckoutSet = "checkout";
    private const string CartEmptyCode = "CART.EMPTY";
    private const string LimitCode = "ORDER.LIMIT";
    private const string AcmePoCode = "ACME.PO";
    private const string EuVatCode = "EU.VAT";
    private const string RegionKey = "region";
    private const string Eu = "EU";
    private const string Acme = "acme";
    private const string Globex = "globex";
    private const string AmountKey = "amount";
    private const string LimitParameter = "limit";

    private static readonly RuleScope AcmeScope = RuleScope.Tenant(Acme);
    private static readonly RuleScope AcmeEuScope = RuleScope.Tenant(Acme).With(RegionKey, Eu);

    // ---------------------------------------------------------------------
    // ResolveAsyncRules
    // ---------------------------------------------------------------------

    [Fact]
    public void ResolveAsyncRules_UnknownSet_ThrowsRuleSetNotFoundException()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => { }).Build();

        RuleSetNotFoundException exception = Assert.Throws<RuleSetNotFoundException>(() => registry.ResolveAsyncRules("refund"));

        Assert.Equal("refund", exception.SetName);
        Assert.Equal(new[] { CheckoutSet }, exception.AvailableSets);
    }

    [Fact]
    public void ResolveAsyncRules_NullSetName_ThrowsArgumentNullException()
    {
        RuleRegistry registry = RuleRegistry.Create().Build();

        Assert.Throws<ArgumentNullException>(() => registry.ResolveAsyncRules(null!));
    }

    [Fact]
    public void ResolveAsyncRules_SetWithOnlySyncRules_ReturnsEmpty()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.Add(RuleTestHelper.CreatePassingRule())).Build();

        Assert.Empty(registry.ResolveAsyncRules(CheckoutSet));
    }

    [Fact]
    public void ResolveAsyncRules_GlobalLayer_ReturnsRulesInDeclarationOrder()
    {
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, set => set.AddAsyncRule(Passing(CartEmptyCode)).AddAsyncRule(Passing(LimitCode)))
            .Build();

        Assert.Equal(new[] { CartEmptyCode, LimitCode }, Codes(registry.ResolveAsyncRules(CheckoutSet)));
    }

    [Fact]
    public void ResolveAsyncRules_NullScope_RequestsTheGlobalScope()
    {
        RuleRegistry registry = Layered();

        Assert.Equal(new[] { CartEmptyCode, LimitCode }, Codes(registry.ResolveAsyncRules(CheckoutSet)));
    }

    [Fact]
    public void ResolveAsyncRules_TenantScope_DisablesAndAppendsFromTheTenantLayer()
    {
        RuleRegistry registry = Layered();

        Assert.Equal(new[] { LimitCode, AcmePoCode }, Codes(registry.ResolveAsyncRules(CheckoutSet, AcmeScope)));
    }

    [Fact]
    public void ResolveAsyncRules_MoreSpecificRequest_MergesEveryApplicableLayerLeastSpecificFirst()
    {
        RuleRegistry registry = Layered();

        Assert.Equal(new[] { LimitCode, AcmePoCode, EuVatCode }, Codes(registry.ResolveAsyncRules(CheckoutSet, AcmeEuScope)));
    }

    [Fact]
    public void ResolveAsyncRules_OtherTenant_IgnoresTheAcmeLayer()
    {
        RuleRegistry registry = Layered();

        Assert.Equal(new[] { CartEmptyCode, LimitCode }, Codes(registry.ResolveAsyncRules(CheckoutSet, RuleScope.Tenant(Globex))));
    }

    [Fact]
    public void ResolveAsyncRules_DisableAppliesToSyncAndAsyncRulesAlike()
    {
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, set => set.Add(RuleTestHelper.CreatePassingRule(CartEmptyCode)).AddAsyncRule(Passing(CartEmptyCode)).AddAsyncRule(Passing(LimitCode)))
            .AddSet(CheckoutSet, AcmeScope, set => set.Disable(CartEmptyCode))
            .Build();

        Assert.Empty(registry.Resolve(CheckoutSet, AcmeScope));
        Assert.Equal(new[] { LimitCode }, Codes(registry.ResolveAsyncRules(CheckoutSet, AcmeScope)));
    }

    [Fact]
    public async Task ResolveAsyncRules_ReplaceAsyncRule_SwapsTheRule()
    {
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, set => set.AddAsyncRule(Broken(CartEmptyCode)))
            .AddSet(CheckoutSet, AcmeScope, set => set.ReplaceAsyncRule(CartEmptyCode, _ => Passing(CartEmptyCode)))
            .Build();

        IAsyncBusinessRule rule = Assert.Single(registry.ResolveAsyncRules(CheckoutSet, AcmeScope));

        Assert.False(await rule.IsBrokenAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ResolveAsyncRules_PassesTheGivenContextToEveryFactory()
    {
        List<BusinessRuleContext> seen = new List<BusinessRuleContext>();
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, set => set.AddAsyncRule(ctx => Capture(ctx, seen, CartEmptyCode)).AddAsyncRule(ctx => Capture(ctx, seen, LimitCode)))
            .Build();
        BusinessRuleContext context = new BusinessRuleContext();

        registry.ResolveAsyncRules(CheckoutSet, null, context);

        Assert.Equal(2, seen.Count);
        Assert.All(seen, candidate => Assert.Same(context, candidate));
    }

    [Fact]
    public void ResolveAsyncRules_StampsTheRequestedScopeOnAContextWithoutOne()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => { }).Build();
        BusinessRuleContext context = new BusinessRuleContext();

        registry.ResolveAsyncRules(CheckoutSet, AcmeScope, context);

        Assert.Equal(AcmeScope, context.Get(RuleParameters.ScopeKey));
    }

    [Fact]
    public void ResolveAsyncRules_FactoryReturningNull_ThrowsInvalidOperationException()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.AddAsyncRule(_ => null!)).Build();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => registry.ResolveAsyncRules(CheckoutSet));

        Assert.Contains("position 0", exception.Message);
    }

    // ---------------------------------------------------------------------
    // CheckAllAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CheckAllAsync_BrokenRule_ThrowsWithTheBrokenCodes()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.AddAsyncRule(Broken(CartEmptyCode)).AddAsyncRule(Passing(LimitCode))).Build();

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => registry.CheckAllAsync(CheckoutSet, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(CartEmptyCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckAllAsync_PassingRules_DoesNotThrow()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.AddAsyncRule(Passing(LimitCode))).Build();

        await registry.CheckAllAsync(CheckoutSet, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CheckAllAsync_EmptySet_DoesNotThrow()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => { }).Build();

        await registry.CheckAllAsync(CheckoutSet, AcmeScope, null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CheckAllAsync_UnknownSet_ThrowsRuleSetNotFoundException()
    {
        RuleRegistry registry = RuleRegistry.Create().Build();

        await Assert.ThrowsAsync<RuleSetNotFoundException>(() => registry.CheckAllAsync(CheckoutSet, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CheckAllAsync_MixedSet_ChecksTheSyncRulesToo()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.Add(RuleTestHelper.CreateBrokenRule(CartEmptyCode)).AddAsyncRule(Passing(LimitCode))).Build();

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => registry.CheckAllAsync(CheckoutSet, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(CartEmptyCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckAllAsync_TenantScope_ChecksTheMergedLayers()
    {
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, set => set.AddAsyncRule(Broken(CartEmptyCode)))
            .AddSet(CheckoutSet, AcmeScope, set => set.Disable(CartEmptyCode).AddAsyncRule(Broken(AcmePoCode)))
            .Build();

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => registry.CheckAllAsync(CheckoutSet, AcmeScope, null, TestContext.Current.CancellationToken));

        Assert.Equal(AcmePoCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckAllAsync_PassesTheContextToTheRules()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.AddAsyncRule(new ContextFlagAsyncRule())).Build();

        await registry.CheckAllAsync(CheckoutSet, null, RuleTestHelper.CreateContext((TestRuleCodes.FlagKey, false)), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => registry.CheckAllAsync(CheckoutSet, null, RuleTestHelper.CreateContext((TestRuleCodes.FlagKey, true)), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CheckAllAsync_ParameterizedRule_ResolvesParametersForTheRequestedScope()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.AddAsyncRule(ParameterizedLimit())).Build();

        await registry.CheckAllAsync(CheckoutSet, AcmeScope, RuleTestHelper.CreateContext((AmountKey, 30m)), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => registry.CheckAllAsync(CheckoutSet, RuleScope.Tenant(Globex), RuleTestHelper.CreateContext((AmountKey, 30m)), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CheckAllAsync_WithOptionsThrowOn_IgnoresBrokenRulesBelowTheThreshold()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.AddAsyncRule(Broken(CartEmptyCode).WithSeverity(RuleSeverity.Warning))).Build();

        await registry.CheckAllAsync(CheckoutSet, null, null, new AsyncRuleEvaluationOptions { ThrowOn = RuleSeverity.Error }, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => registry.CheckAllAsync(CheckoutSet, null, null, new AsyncRuleEvaluationOptions { ThrowOn = RuleSeverity.Warning }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CheckAllAsync_WithOptionsRunInParallel_EvaluatesEveryRule()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.AddAsyncRule(Broken(CartEmptyCode)).AddAsyncRule(Broken(LimitCode))).Build();

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => registry.CheckAllAsync(CheckoutSet, null, null, new AsyncRuleEvaluationOptions { RunInParallel = true }, TestContext.Current.CancellationToken));

        Assert.Equal(2, exception.BrokenRules.Count);
    }

    [Fact]
    public async Task CheckAllAsync_NullOptions_ThrowsArgumentNullException()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => { }).Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => registry.CheckAllAsync(CheckoutSet, null, null, null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CheckAllAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.AddAsyncRule(Cancellable())).Build();
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => registry.CheckAllAsync(CheckoutSet, null, null, source.Token));
    }

    // ---------------------------------------------------------------------
    // EvaluateAllAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_ReturnsBrokenAndPassedRules()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.AddAsyncRule(Broken(CartEmptyCode)).AddAsyncRule(Passing(LimitCode))).Build();

        AsyncRuleEvaluationResult result = await registry.EvaluateAllAsync(CheckoutSet, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CartEmptyCode, Assert.Single(result.BrokenRules).Code);
        Assert.Equal(LimitCode, Assert.Single(result.PassedRules).Code);
    }

    [Fact]
    public async Task EvaluateAllAsync_EmptySet_ReportsAllPassed()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => { }).Build();

        AsyncRuleEvaluationResult result = await registry.EvaluateAllAsync(CheckoutSet, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.AllPassed);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public async Task EvaluateAllAsync_TenantScope_EvaluatesTheMergedLayers()
    {
        RuleRegistry registry = Layered();

        AsyncRuleEvaluationResult result = await registry.EvaluateAllAsync(CheckoutSet, AcmeEuScope, null, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { LimitCode, AcmePoCode, EuVatCode }, result.Entries.Select(entry => entry.Code));
    }

    [Fact]
    public async Task EvaluateAllAsync_UnknownSet_ThrowsRuleSetNotFoundException()
    {
        RuleRegistry registry = RuleRegistry.Create().Build();

        await Assert.ThrowsAsync<RuleSetNotFoundException>(() => registry.EvaluateAllAsync(CheckoutSet, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EvaluateAllAsync_WithOptionsFaultPolicyRecord_RecordsTheFault()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.AddAsyncRule(Throwing(LimitCode))).Build();

        AsyncRuleEvaluationResult result = await registry.EvaluateAllAsync(CheckoutSet, null, null, new AsyncRuleEvaluationOptions { OnFault = FaultPolicy.Record }, TestContext.Current.CancellationToken);

        Assert.True(result.HasFaults);
        Assert.Equal(LimitCode, Assert.Single(result.Faulted).Code);
    }

    [Fact]
    public async Task EvaluateAllAsync_NullOptions_ThrowsArgumentNullException()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => { }).Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => registry.EvaluateAllAsync(CheckoutSet, null, null, null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EvaluateAllAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.AddAsyncRule(Cancellable())).Build();
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => registry.EvaluateAllAsync(CheckoutSet, null, null, source.Token));
    }

    // ---------------------------------------------------------------------
    // Scope on a reused context
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CheckAllAsync_ContextReusedAcrossTenants_ResolvesParametersForEachRequestedScope()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.AddAsyncRule(ParameterizedLimit())).Build();
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 30m));
        await registry.CheckAllAsync(CheckoutSet, AcmeScope, context, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => registry.CheckAllAsync(CheckoutSet, RuleScope.Tenant(Globex), context, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CheckAllAsync_NullScopeWithATenantScopeInTheContext_ResolvesGlobalParameters()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.AddAsyncRule(ParameterizedLimit())).Build();
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 30m));
        context.Set(RuleParameters.ScopeKey, AcmeScope);

        await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => registry.CheckAllAsync(CheckoutSet, null, context, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CheckAllAsync_ContextWithAScope_RestoresItAfterTheBatch()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.AddAsyncRule(Passing(LimitCode))).Build();
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(RuleParameters.ScopeKey, AcmeEuScope);

        await registry.CheckAllAsync(CheckoutSet, AcmeScope, context, TestContext.Current.CancellationToken);

        Assert.Equal(AcmeEuScope, context.Get(RuleParameters.ScopeKey));
    }

    [Fact]
    public async Task CheckAllAsync_ContextWithoutAScope_RemovesTheScopeAfterTheBatch()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.AddAsyncRule(Passing(LimitCode))).Build();
        BusinessRuleContext context = new BusinessRuleContext();

        await registry.CheckAllAsync(CheckoutSet, AcmeScope, context, TestContext.Current.CancellationToken);

        Assert.False(context.ContainsKey(RuleParameters.ScopeKey));
    }

    [Fact]
    public async Task CheckAllAsync_CancelledToken_StillRestoresTheContextScope()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.AddAsyncRule(Cancellable())).Build();
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(RuleParameters.ScopeKey, AcmeEuScope);
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => registry.CheckAllAsync(CheckoutSet, AcmeScope, context, source.Token));

        Assert.Equal(AcmeEuScope, context.Get(RuleParameters.ScopeKey));
    }

    [Fact]
    public async Task EvaluateAllAsync_ContextWithAScope_EvaluatesWithTheRequestedScopeAndRestoresIt()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.AddAsyncRule(ParameterizedLimit())).Build();
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 30m));
        context.Set(RuleParameters.ScopeKey, AcmeScope);

        AsyncRuleEvaluationResult result = await registry.EvaluateAllAsync(CheckoutSet, RuleScope.Tenant(Globex), context, TestContext.Current.CancellationToken);

        Assert.Equal(LimitCode, Assert.Single(result.BrokenRules).Code);
        Assert.Equal(AcmeScope, context.Get(RuleParameters.ScopeKey));
    }

    // ---------------------------------------------------------------------
    // Sets mixing sync and async rules
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAllAsync_MixedSet_EvaluatesTheSyncRulesFirstThenTheAsyncRules()
    {
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, set => set.AddAsyncRule(Passing(LimitCode)).Add(RuleTestHelper.CreateBrokenRule(CartEmptyCode)))
            .AddSet(CheckoutSet, AcmeScope, set => set.Add(RuleTestHelper.CreatePassingRule(AcmePoCode)))
            .Build();

        AsyncRuleEvaluationResult result = await registry.EvaluateAllAsync(CheckoutSet, AcmeScope, null, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { CartEmptyCode, AcmePoCode, LimitCode }, result.Entries.Select(entry => entry.AsyncRule!.Code));
        Assert.Equal(CartEmptyCode, Assert.Single(result.BrokenRules).Code);
    }

    [Fact]
    public async Task CheckAllAsync_MixedSet_DisabledSyncCodeIsNotChecked()
    {
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, set => set.Add(RuleTestHelper.CreateBrokenRule(CartEmptyCode)).AddAsyncRule(Passing(LimitCode)))
            .AddSet(CheckoutSet, AcmeScope, set => set.Disable(CartEmptyCode))
            .Build();

        Exception? exception = await Record.ExceptionAsync(() => registry.CheckAllAsync(CheckoutSet, AcmeScope, null, TestContext.Current.CancellationToken));

        Assert.Null(exception);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static RuleRegistry Layered()
    {
        return RuleRegistry.Create()
            .AddSet(CheckoutSet, set => set.AddAsyncRule(Passing(CartEmptyCode)).AddAsyncRule(Passing(LimitCode)))
            .AddSet(CheckoutSet, AcmeScope, set => set.Disable(CartEmptyCode).AddAsyncRule(Passing(AcmePoCode)))
            .AddSet(CheckoutSet, AcmeEuScope, set => set.AddAsyncRule(Passing(EuVatCode)))
            .Build();
    }

    private static IEnumerable<string> Codes(IEnumerable<IAsyncBusinessRule> rules)
    {
        return rules.Select(rule => rule.Code);
    }

    private static IAsyncBusinessRule Passing(string code)
    {
        return RuleTestHelper.CreatePassingAsyncRule(code);
    }

    private static IAsyncBusinessRule Broken(string code)
    {
        return RuleTestHelper.CreateBrokenAsyncRule(code);
    }

    private static IAsyncBusinessRule Throwing(string code)
    {
        return AsyncBusinessRuleBuilder.Create(code).WithMessage("Throws.").WhenAsync((ctx, ct) => throw new InvalidOperationException("boom")).Build();
    }

    private static IAsyncBusinessRule Cancellable()
    {
        return AsyncBusinessRuleBuilder.Create("ORDER.CANCEL")
            .WithMessage("Honours cancellation.")
            .WhenAsync((ctx, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(false);
            })
            .Build();
    }

    private static IAsyncBusinessRule ParameterizedLimit()
    {
        InMemoryRuleParameterProvider parameters = new InMemoryRuleParameterProvider()
            .Set(LimitParameter, 10m)
            .Set(LimitParameter, 50m, AcmeScope);

        return AsyncBusinessRuleBuilder.Create(LimitCode)
            .WithMessage("Amount exceeds the limit.")
            .WithParameters(parameters)
            .WithParameter<decimal>(LimitParameter, 10m)
            .WhenAsync((ctx, p, ct) => Task.FromResult(ctx.Get<decimal>(AmountKey) > p.Get<decimal>(LimitParameter)))
            .Build();
    }

    private static IAsyncBusinessRule Capture(BusinessRuleContext context, List<BusinessRuleContext> seen, string code)
    {
        seen.Add(context);
        return Passing(code);
    }
}
