namespace Masterly.BusinessRules.DependencyInjection.UnitTests;

public class RuleRegistryTests
{
    private const string CheckoutSet = "checkout";
    private const string RefundSet = "refund";
    private const string CartEmptyCode = "CART.EMPTY";
    private const string LimitCode = "ORDER.LIMIT";
    private const string AcmePoCode = "ACME.PO";
    private const string EuVatCode = "EU.VAT";
    private const string RegionKey = "region";
    private const string Eu = "EU";
    private const string Us = "US";
    private const string Acme = "acme";
    private const string Globex = "globex";
    private const string AmountKey = "amount";
    private const string LimitParameter = "limit";

    private static readonly RuleScope AcmeScope = RuleScope.Tenant(Acme);
    private static readonly RuleScope AcmeEuScope = RuleScope.Tenant(Acme).With(RegionKey, Eu);
    private static readonly RuleScope EuScope = RuleScope.Global.With(RegionKey, Eu);

    // ---------------------------------------------------------------------
    // Set names
    // ---------------------------------------------------------------------

    [Fact]
    public void SetNames_AreSortedAndDistinct()
    {
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(RefundSet, set => { })
            .AddSet(CheckoutSet, set => { })
            .AddSet(CheckoutSet, AcmeScope, set => { })
            .Build();

        Assert.Equal(new[] { CheckoutSet, RefundSet }, registry.SetNames);
    }

    [Fact]
    public void Contains_KnownName_ReturnsTrue()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => { }).Build();

        Assert.True(registry.Contains(CheckoutSet));
        Assert.False(registry.Contains(RefundSet));
        Assert.False(registry.Contains(CheckoutSet.ToUpperInvariant()));
    }

    [Fact]
    public void Contains_NullName_ThrowsArgumentNullException()
    {
        RuleRegistry registry = RuleRegistry.Create().Build();

        Assert.Throws<ArgumentNullException>(() => registry.Contains(null!));
    }

    // ---------------------------------------------------------------------
    // Resolve: lookup
    // ---------------------------------------------------------------------

    [Fact]
    public void Resolve_UnknownSet_ThrowsRuleSetNotFoundExceptionListingTheSets()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => { }).AddSet(RefundSet, set => { }).Build();

        RuleSetNotFoundException exception = Assert.Throws<RuleSetNotFoundException>(() => registry.Resolve("checkuot"));

        Assert.Equal("checkuot", exception.SetName);
        Assert.Equal(new[] { CheckoutSet, RefundSet }, exception.AvailableSets);
        Assert.Contains("checkuot", exception.Message);
        Assert.Contains($"{CheckoutSet}, {RefundSet}", exception.Message);
    }

    [Fact]
    public void Resolve_NullSetName_ThrowsArgumentNullException()
    {
        RuleRegistry registry = RuleRegistry.Create().Build();

        Assert.Throws<ArgumentNullException>(() => registry.Resolve(null!));
    }

    [Fact]
    public void Resolve_SetWithOnlyAsyncRules_ReturnsEmpty()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.AddAsyncRule(RuleTestHelper.CreatePassingAsyncRule())).Build();

        Assert.Empty(registry.Resolve(CheckoutSet));
    }

    [Fact]
    public void Resolve_EmptySet_ReturnsEmpty()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => { }).Build();

        Assert.Empty(registry.Resolve(CheckoutSet));
    }

    // ---------------------------------------------------------------------
    // Resolve: layering
    // ---------------------------------------------------------------------

    [Fact]
    public void Resolve_GlobalLayer_ReturnsRulesInDeclarationOrder()
    {
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, set => set.Add(Passing(CartEmptyCode)).Add(Passing(LimitCode)))
            .Build();

        Assert.Equal(new[] { CartEmptyCode, LimitCode }, Codes(registry.Resolve(CheckoutSet)));
    }

    [Fact]
    public void Resolve_NullScope_RequestsTheGlobalScope()
    {
        RuleRegistry registry = Layered();

        Assert.Equal(new[] { CartEmptyCode, LimitCode }, Codes(registry.Resolve(CheckoutSet)));
        Assert.Equal(new[] { CartEmptyCode, LimitCode }, Codes(registry.Resolve(CheckoutSet, RuleScope.Global)));
    }

    [Fact]
    public void Resolve_TenantScope_DisablesAndAppendsFromTheTenantLayer()
    {
        RuleRegistry registry = Layered();

        Assert.Equal(new[] { LimitCode, AcmePoCode }, Codes(registry.Resolve(CheckoutSet, AcmeScope)));
    }

    [Fact]
    public void Resolve_OtherTenant_IgnoresTheAcmeLayer()
    {
        RuleRegistry registry = Layered();

        Assert.Equal(new[] { CartEmptyCode, LimitCode }, Codes(registry.Resolve(CheckoutSet, RuleScope.Tenant(Globex))));
    }

    [Fact]
    public void Resolve_MoreSpecificRequest_MergesEveryApplicableLayerLeastSpecificFirst()
    {
        RuleRegistry registry = Layered();

        Assert.Equal(new[] { LimitCode, AcmePoCode, EuVatCode }, Codes(registry.Resolve(CheckoutSet, AcmeEuScope)));
    }

    [Fact]
    public void Resolve_RequestWithAnotherRegion_SkipsOnlyTheRegionLayer()
    {
        RuleRegistry registry = Layered();

        Assert.Equal(new[] { LimitCode, AcmePoCode }, Codes(registry.Resolve(CheckoutSet, RuleScope.Tenant(Acme).With(RegionKey, Us))));
    }

    [Fact]
    public void Resolve_EquallySpecificLayers_AreMergedInDeclarationOrder()
    {
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, EuScope, set => set.Add(Passing(EuVatCode)))
            .AddSet(CheckoutSet, AcmeScope, set => set.Add(Passing(AcmePoCode)))
            .Build();

        Assert.Equal(new[] { EuVatCode, AcmePoCode }, Codes(registry.Resolve(CheckoutSet, AcmeEuScope)));
    }

    [Fact]
    public void Resolve_LaterEquallySpecificLayer_CanDisableAnEarlierOnesRule()
    {
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, EuScope, set => set.Add(Passing(EuVatCode)))
            .AddSet(CheckoutSet, AcmeScope, set => set.Disable(EuVatCode).Add(Passing(AcmePoCode)))
            .Build();

        Assert.Equal(new[] { AcmePoCode }, Codes(registry.Resolve(CheckoutSet, AcmeEuScope)));
    }

    [Fact]
    public void Resolve_EarlierEquallySpecificLayer_CannotDisableALaterOnesRule()
    {
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, EuScope, set => set.Disable(AcmePoCode))
            .AddSet(CheckoutSet, AcmeScope, set => set.Add(Passing(AcmePoCode)))
            .Build();

        Assert.Equal(new[] { AcmePoCode }, Codes(registry.Resolve(CheckoutSet, AcmeEuScope)));
    }

    [Fact]
    public void Resolve_DisableInLessSpecificLayer_DoesNotRemoveAMoreSpecificAddition()
    {
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, set => set.Disable(AcmePoCode))
            .AddSet(CheckoutSet, AcmeScope, set => set.Add(Passing(AcmePoCode)))
            .Build();

        Assert.Equal(new[] { AcmePoCode }, Codes(registry.Resolve(CheckoutSet, AcmeScope)));
    }

    [Fact]
    public void Resolve_DisableRemovesEveryRuleWithTheCode()
    {
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, set => set.Add(Passing(CartEmptyCode)).Add(Broken(CartEmptyCode)).Add(Passing(LimitCode)))
            .AddSet(CheckoutSet, AcmeScope, set => set.Disable(CartEmptyCode))
            .Build();

        Assert.Equal(new[] { LimitCode }, Codes(registry.Resolve(CheckoutSet, AcmeScope)));
    }

    [Fact]
    public void Resolve_DisableOfAnUnknownCode_IsANoOp()
    {
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, set => set.Add(Passing(LimitCode)))
            .AddSet(CheckoutSet, AcmeScope, set => set.Disable("NOT.THERE"))
            .Build();

        Assert.Equal(new[] { LimitCode }, Codes(registry.Resolve(CheckoutSet, AcmeScope)));
    }

    [Fact]
    public void Resolve_Replace_SwapsTheRuleAndAppendsTheReplacement()
    {
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, set => set.Add(Broken(CartEmptyCode)).Add(Passing(LimitCode)))
            .AddSet(CheckoutSet, AcmeScope, set => set.Replace(CartEmptyCode, _ => Passing(CartEmptyCode)))
            .Build();

        IReadOnlyList<IBusinessRule> rules = registry.Resolve(CheckoutSet, AcmeScope);

        Assert.Equal(new[] { LimitCode, CartEmptyCode }, Codes(rules));
        Assert.False(rules[1].IsBroken());
    }

    [Fact]
    public void Resolve_SameLayerDisableThenAdd_KeepsTheLayersOwnAddition()
    {
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, set => set.Disable(LimitCode).Add(Passing(LimitCode)))
            .Build();

        Assert.Equal(new[] { LimitCode }, Codes(registry.Resolve(CheckoutSet)));
    }

    [Fact]
    public void Resolve_DifferentSets_AreIndependent()
    {
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, set => set.Add(Passing(CartEmptyCode)))
            .AddSet(RefundSet, set => set.Add(Passing(LimitCode)))
            .AddSet(RefundSet, AcmeScope, set => set.Disable(CartEmptyCode))
            .Build();

        Assert.Equal(new[] { CartEmptyCode }, Codes(registry.Resolve(CheckoutSet, AcmeScope)));
        Assert.Equal(new[] { LimitCode }, Codes(registry.Resolve(RefundSet, AcmeScope)));
    }

    // ---------------------------------------------------------------------
    // Resolve: factories and context
    // ---------------------------------------------------------------------

    [Fact]
    public void Resolve_PassesTheGivenContextToEveryFactory()
    {
        List<BusinessRuleContext> seen = new List<BusinessRuleContext>();
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, set => set.Add(ctx => Capture(ctx, seen, CartEmptyCode)).Add(ctx => Capture(ctx, seen, LimitCode)))
            .Build();
        BusinessRuleContext context = new BusinessRuleContext();

        registry.Resolve(CheckoutSet, null, context);

        Assert.Equal(2, seen.Count);
        Assert.All(seen, candidate => Assert.Same(context, candidate));
    }

    [Fact]
    public void Resolve_NullContext_HandsAFreshContextToTheFactories()
    {
        List<BusinessRuleContext> seen = new List<BusinessRuleContext>();
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.Add(ctx => Capture(ctx, seen, LimitCode))).Build();

        registry.Resolve(CheckoutSet);

        Assert.NotNull(Assert.Single(seen));
    }

    [Fact]
    public void Resolve_StampsTheRequestedScopeOnAContextWithoutOne()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => { }).Build();
        BusinessRuleContext context = new BusinessRuleContext();

        registry.Resolve(CheckoutSet, AcmeScope, context);

        Assert.Equal(AcmeScope, context.Get(RuleParameters.ScopeKey));
    }

    [Fact]
    public void Resolve_NullScope_StampsTheGlobalScope()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => { }).Build();
        BusinessRuleContext context = new BusinessRuleContext();

        registry.Resolve(CheckoutSet, null, context);

        Assert.Equal(RuleScope.Global, context.Get(RuleParameters.ScopeKey));
    }

    [Fact]
    public void Resolve_LeavesAnExistingScopeOnTheContextUntouched()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => { }).Build();
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(RuleParameters.ScopeKey, EuScope);

        registry.Resolve(CheckoutSet, AcmeScope, context);

        Assert.Equal(EuScope, context.Get(RuleParameters.ScopeKey));
    }

    [Fact]
    public void Resolve_FactoriesRunOnEveryCall()
    {
        int calls = 0;
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.Add(_ =>
        {
            calls++;
            return Passing(LimitCode);
        })).Build();

        registry.Resolve(CheckoutSet);
        registry.Resolve(CheckoutSet);

        Assert.Equal(2, calls);
    }

    [Fact]
    public void Resolve_OnlyRunsFactoriesOfApplicableLayers()
    {
        int acmeCalls = 0;
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, set => set.Add(Passing(LimitCode)))
            .AddSet(CheckoutSet, AcmeScope, set => set.Add(_ =>
            {
                acmeCalls++;
                return Passing(AcmePoCode);
            }))
            .Build();

        registry.Resolve(CheckoutSet, RuleScope.Tenant(Globex));

        Assert.Equal(0, acmeCalls);
    }

    [Fact]
    public void Resolve_FactoryReturningNull_ThrowsInvalidOperationExceptionNamingThePosition()
    {
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, AcmeScope, set => set.Add(Passing(LimitCode)).Add(_ => null!))
            .Build();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => registry.Resolve(CheckoutSet, AcmeScope));

        Assert.Contains("position 1", exception.Message);
        Assert.Contains(CheckoutSet, exception.Message);
        Assert.Contains(AcmeScope.ToString(), exception.Message);
    }

    [Fact]
    public void Resolve_FactoryThrows_Propagates()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.Add(_ => throw new FormatException("bad"))).Build();

        Assert.Throws<FormatException>(() => registry.Resolve(CheckoutSet));
    }

    // ---------------------------------------------------------------------
    // CheckAll
    // ---------------------------------------------------------------------

    [Fact]
    public void CheckAll_BrokenRule_ThrowsWithTheBrokenCodes()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.Add(Broken(CartEmptyCode)).Add(Passing(LimitCode))).Build();

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => registry.CheckAll(CheckoutSet));

        Assert.Equal(CartEmptyCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void CheckAll_PassingRules_DoesNotThrow()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.Add(Passing(LimitCode))).Build();

        registry.CheckAll(CheckoutSet);
    }

    [Fact]
    public void CheckAll_EmptySet_DoesNotThrow()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => { }).Build();

        registry.CheckAll(CheckoutSet, AcmeScope);
    }

    [Fact]
    public void CheckAll_UnknownSet_ThrowsRuleSetNotFoundException()
    {
        RuleRegistry registry = RuleRegistry.Create().Build();

        Assert.Throws<RuleSetNotFoundException>(() => registry.CheckAll(CheckoutSet));
    }

    [Fact]
    public void CheckAll_TenantScope_ChecksTheMergedLayers()
    {
        RuleRegistry registry = RuleRegistry.Create()
            .AddSet(CheckoutSet, set => set.Add(Broken(CartEmptyCode)))
            .AddSet(CheckoutSet, AcmeScope, set => set.Disable(CartEmptyCode).Add(Broken(AcmePoCode)))
            .Build();

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => registry.CheckAll(CheckoutSet, AcmeScope));

        Assert.Equal(AcmePoCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void CheckAll_PassesTheContextToTheRules()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.Add(ContextFlag())).Build();

        registry.CheckAll(CheckoutSet, null, RuleTestHelper.CreateContext((TestRuleCodes.FlagKey, false)));
        Assert.Throws<BusinessRuleValidationException>(() => registry.CheckAll(CheckoutSet, null, RuleTestHelper.CreateContext((TestRuleCodes.FlagKey, true))));
    }

    [Fact]
    public void CheckAll_ParameterizedRule_ResolvesParametersForTheRequestedScope()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.Add(ParameterizedLimit())).Build();

        registry.CheckAll(CheckoutSet, AcmeScope, RuleTestHelper.CreateContext((AmountKey, 30m)));
        Assert.Throws<BusinessRuleValidationException>(() => registry.CheckAll(CheckoutSet, RuleScope.Tenant(Globex), RuleTestHelper.CreateContext((AmountKey, 30m))));
    }

    [Fact]
    public void CheckAll_WithOptionsThrowOn_IgnoresBrokenRulesBelowTheThreshold()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.Add(Broken(CartEmptyCode).WithSeverity(RuleSeverity.Warning))).Build();

        registry.CheckAll(CheckoutSet, null, null, new RuleEvaluationOptions { ThrowOn = RuleSeverity.Error });
        Assert.Throws<BusinessRuleValidationException>(() => registry.CheckAll(CheckoutSet, null, null, new RuleEvaluationOptions { ThrowOn = RuleSeverity.Warning }));
    }

    [Fact]
    public void CheckAll_WithOptionsStopOnFirstFailure_LeavesLaterRulesNotEvaluated()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.Add(Broken(CartEmptyCode)).Add(Broken(LimitCode))).Build();

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => registry.CheckAll(CheckoutSet, null, null, new RuleEvaluationOptions { StopOnFirstFailure = true }));

        Assert.Equal(CartEmptyCode, Assert.Single(exception.BrokenRules).Code);
        Assert.Equal(RuleOutcome.NotEvaluated, exception.Entries[1].Outcome);
    }

    [Fact]
    public void CheckAll_NullOptions_ThrowsArgumentNullException()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => { }).Build();

        Assert.Throws<ArgumentNullException>(() => registry.CheckAll(CheckoutSet, null, null, null!));
    }

    // ---------------------------------------------------------------------
    // EvaluateAll
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateAll_ReturnsBrokenAndPassedRules()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.Add(Broken(CartEmptyCode)).Add(Passing(LimitCode))).Build();

        RuleEvaluationResult result = registry.EvaluateAll(CheckoutSet);

        Assert.Equal(CartEmptyCode, Assert.Single(result.BrokenRules).Code);
        Assert.Equal(LimitCode, Assert.Single(result.PassedRules).Code);
    }

    [Fact]
    public void EvaluateAll_EmptySet_ReportsAllPassed()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => { }).Build();

        RuleEvaluationResult result = registry.EvaluateAll(CheckoutSet);

        Assert.True(result.AllPassed);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public void EvaluateAll_TenantScope_EvaluatesTheMergedLayers()
    {
        RuleRegistry registry = Layered();

        RuleEvaluationResult result = registry.EvaluateAll(CheckoutSet, AcmeEuScope, RuleTestHelper.CreateContext((AmountKey, 1m)));

        Assert.Equal(new[] { LimitCode, AcmePoCode, EuVatCode }, result.Entries.Select(entry => entry.Code));
    }

    [Fact]
    public void EvaluateAll_UnknownSet_ThrowsRuleSetNotFoundException()
    {
        RuleRegistry registry = RuleRegistry.Create().Build();

        Assert.Throws<RuleSetNotFoundException>(() => registry.EvaluateAll(CheckoutSet));
    }

    [Fact]
    public void EvaluateAll_WithOptionsFaultPolicyRecord_RecordsTheFault()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => set.Add(Throwing(LimitCode))).Build();

        RuleEvaluationResult result = registry.EvaluateAll(CheckoutSet, null, null, new RuleEvaluationOptions { OnFault = FaultPolicy.Record });

        Assert.True(result.HasFaults);
        Assert.Equal(LimitCode, Assert.Single(result.Faulted).Code);
    }

    [Fact]
    public void EvaluateAll_NullOptions_ThrowsArgumentNullException()
    {
        RuleRegistry registry = RuleRegistry.Create().AddSet(CheckoutSet, set => { }).Build();

        Assert.Throws<ArgumentNullException>(() => registry.EvaluateAll(CheckoutSet, null, null, null!));
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static RuleRegistry Layered()
    {
        return RuleRegistry.Create()
            .AddSet(CheckoutSet, set => set.Add(Passing(CartEmptyCode)).Add(Passing(LimitCode)))
            .AddSet(CheckoutSet, AcmeScope, set => set.Disable(CartEmptyCode).Add(Passing(AcmePoCode)))
            .AddSet(CheckoutSet, AcmeEuScope, set => set.Add(Passing(EuVatCode)))
            .Build();
    }

    private static IEnumerable<string> Codes(IEnumerable<IBusinessRule> rules)
    {
        return rules.Select(rule => rule.Code);
    }

    private static IBusinessRule Passing(string code)
    {
        return RuleTestHelper.CreatePassingRule(code);
    }

    private static IBusinessRule Broken(string code)
    {
        return RuleTestHelper.CreateBrokenRule(code);
    }

    private static IBusinessRule Throwing(string code)
    {
        return BusinessRuleBuilder.Create(code).WithMessage("Throws.").When(() => throw new InvalidOperationException("boom")).Build();
    }

    private static IBusinessRule ContextFlag()
    {
        return new ContextFlagRule();
    }

    private static IBusinessRule ParameterizedLimit()
    {
        InMemoryRuleParameterProvider parameters = new InMemoryRuleParameterProvider()
            .Set(LimitParameter, 10m)
            .Set(LimitParameter, 50m, AcmeScope);

        return BusinessRuleBuilder.Create(LimitCode)
            .WithMessage("Amount exceeds the limit.")
            .WithParameters(parameters)
            .WithParameter<decimal>(LimitParameter, 10m)
            .When((ctx, p) => ctx.Get<decimal>(AmountKey) > p.Get<decimal>(LimitParameter))
            .Build();
    }

    private static IBusinessRule Capture(BusinessRuleContext context, List<BusinessRuleContext> seen, string code)
    {
        seen.Add(context);
        return Passing(code);
    }
}
