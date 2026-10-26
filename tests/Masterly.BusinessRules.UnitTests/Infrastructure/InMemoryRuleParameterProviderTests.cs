namespace Masterly.BusinessRules.UnitTests;

public class InMemoryRuleParameterProviderTests
{
    private const string LimitName = "maxOrderAmount";
    private const string OtherName = "minimumAge";
    private const string RegionKey = "region";
    private const decimal GlobalLimit = 10_000m;
    private const decimal AcmeLimit = 50_000m;
    private const decimal EuLimit = 20_000m;
    private const decimal AcmeEuLimit = 70_000m;

    private static readonly RuleScope Acme = RuleScope.Tenant("acme");
    private static readonly RuleScope Eu = RuleScope.Global.With(RegionKey, "EU");
    private static readonly RuleScope AcmeEu = Acme.With(RegionKey, "EU");

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------
    // Set and lookup
    // ---------------------------------------------------------------------

    [Fact]
    public void TryGet_UnknownParameter_ReturnsFalse()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider();

        bool found = provider.TryGet(LimitName, null, out decimal value);

        Assert.False(found);
        Assert.Equal(0m, value);
        Assert.Equal(0, provider.Count);
    }

    [Fact]
    public void Set_GlobalValue_IsReturnedForNullScope()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider().Set(LimitName, GlobalLimit);

        Assert.True(provider.TryGet(LimitName, null, out decimal value));
        Assert.Equal(GlobalLimit, value);
        Assert.Equal(1, provider.Count);
    }

    [Fact]
    public void Set_GlobalValue_IsReturnedForGlobalScope()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider().Set(LimitName, GlobalLimit);

        Assert.True(provider.TryGet(LimitName, RuleScope.Global, out decimal value));
        Assert.Equal(GlobalLimit, value);
    }

    [Fact]
    public void Set_GlobalValue_AppliesToAnyScope()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider().Set(LimitName, GlobalLimit);

        Assert.True(provider.TryGet(LimitName, AcmeEu, out decimal value));
        Assert.Equal(GlobalLimit, value);
    }

    [Fact]
    public void TryGet_TenantScope_ReturnsTenantOverride()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider()
            .Set(LimitName, GlobalLimit)
            .Set(LimitName, AcmeLimit, Acme);

        Assert.True(provider.TryGet(LimitName, Acme, out decimal value));
        Assert.Equal(AcmeLimit, value);
    }

    [Fact]
    public void TryGet_OtherTenant_FallsBackToGlobal()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider()
            .Set(LimitName, GlobalLimit)
            .Set(LimitName, AcmeLimit, Acme);

        Assert.True(provider.TryGet(LimitName, RuleScope.Tenant("globex"), out decimal value));
        Assert.Equal(GlobalLimit, value);
    }

    [Fact]
    public void TryGet_ScopedOnly_GlobalRequest_ReturnsFalse()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider().Set(LimitName, AcmeLimit, Acme);

        Assert.False(provider.TryGet(LimitName, null, out decimal _));
        Assert.False(provider.TryGet(LimitName, RuleScope.Tenant("globex"), out decimal _));
    }

    [Fact]
    public void TryGet_MoreSpecificRequest_ReturnsTheMostSpecificApplicableValue()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider()
            .Set(LimitName, GlobalLimit)
            .Set(LimitName, AcmeLimit, Acme)
            .Set(LimitName, AcmeEuLimit, AcmeEu);

        Assert.True(provider.TryGet(LimitName, AcmeEu, out decimal tenantRegion));
        Assert.True(provider.TryGet(LimitName, Acme.With(RegionKey, "US"), out decimal tenantOnly));

        Assert.Equal(AcmeEuLimit, tenantRegion);
        Assert.Equal(AcmeLimit, tenantOnly);
    }

    [Fact]
    public void TryGet_EquallySpecificScopes_MostRecentlySetWins()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider()
            .Set(LimitName, AcmeLimit, Acme)
            .Set(LimitName, EuLimit, Eu);

        Assert.True(provider.TryGet(LimitName, AcmeEu, out decimal value));
        Assert.Equal(EuLimit, value);
    }

    [Fact]
    public void Set_SameScope_ReplacesTheValue()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider()
            .Set(LimitName, GlobalLimit, Acme)
            .Set(LimitName, AcmeLimit, RuleScope.Tenant("acme"));

        Assert.True(provider.TryGet(LimitName, Acme, out decimal value));
        Assert.Equal(AcmeLimit, value);
        Assert.Equal(1, provider.Count);
    }

    [Fact]
    public void Set_DifferentParameters_AreIndependent()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider()
            .Set(LimitName, GlobalLimit)
            .Set(OtherName, 18);

        Assert.True(provider.TryGet(OtherName, null, out int age));
        Assert.Equal(18, age);
        Assert.Equal(2, provider.Count);
    }

    [Fact]
    public void TryGet_NamesAreCaseSensitive()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider().Set(LimitName, GlobalLimit);

        Assert.False(provider.TryGet(LimitName.ToUpperInvariant(), null, out decimal _));
    }

    [Fact]
    public void TryGet_WrongType_ThrowsInvalidCastExceptionNamingParameterScopeAndTypes()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider().Set(LimitName, GlobalLimit, Acme);

        InvalidCastException exception = Assert.Throws<InvalidCastException>(() => provider.TryGet(LimitName, Acme, out int _));

        Assert.Contains(LimitName, exception.Message);
        Assert.Contains("tenant=acme", exception.Message);
        Assert.Contains("Decimal", exception.Message);
        Assert.Contains("Int32", exception.Message);
    }

    [Fact]
    public void TryGet_StoredNull_IsReturnedForNullableType()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider().Set<string?>(LimitName, null);

        Assert.True(provider.TryGet(LimitName, null, out string? value));
        Assert.Null(value);
    }

    [Fact]
    public void TryGet_StoredNull_RequestedAsValueType_ThrowsInvalidCastException()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider().Set<string?>(LimitName, null);

        Assert.Throws<InvalidCastException>(() => provider.TryGet(LimitName, null, out int _));
    }

    [Fact]
    public void TryGet_NullName_ThrowsArgumentNullException()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider();

        Assert.Throws<ArgumentNullException>(() => provider.TryGet(null!, null, out decimal _));
    }

    // ---------------------------------------------------------------------
    // Guards
    // ---------------------------------------------------------------------

    [Fact]
    public void Set_NullName_ThrowsArgumentNullException()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider();

        Assert.Throws<ArgumentNullException>(() => provider.Set(null!, GlobalLimit));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Set_BlankName_ThrowsArgumentException(string name)
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider();

        Assert.Throws<ArgumentException>(() => provider.Set(name, GlobalLimit));
    }

    [Fact]
    public void Set_NullScope_ThrowsArgumentNullException()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider();

        Assert.Throws<ArgumentNullException>(() => provider.Set(LimitName, GlobalLimit, null!));
    }

    // ---------------------------------------------------------------------
    // Remove
    // ---------------------------------------------------------------------

    [Fact]
    public void Remove_Name_RemovesEveryScope()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider()
            .Set(LimitName, GlobalLimit)
            .Set(LimitName, AcmeLimit, Acme);

        bool removed = provider.Remove(LimitName);

        Assert.True(removed);
        Assert.False(provider.TryGet(LimitName, Acme, out decimal _));
        Assert.Equal(0, provider.Count);
    }

    [Fact]
    public void Remove_UnknownName_ReturnsFalse()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider();

        Assert.False(provider.Remove(LimitName));
    }

    [Fact]
    public void Remove_NameAndScope_RemovesOnlyThatScope()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider()
            .Set(LimitName, GlobalLimit)
            .Set(LimitName, AcmeLimit, Acme);

        bool removed = provider.Remove(LimitName, Acme);

        Assert.True(removed);
        Assert.True(provider.TryGet(LimitName, Acme, out decimal value));
        Assert.Equal(GlobalLimit, value);
        Assert.Equal(1, provider.Count);
    }

    [Fact]
    public void Remove_NameAndUnknownScope_ReturnsFalse()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider().Set(LimitName, GlobalLimit);

        Assert.False(provider.Remove(LimitName, Acme));
        Assert.False(provider.Remove(OtherName, Acme));
        Assert.Equal(1, provider.Count);
    }

    [Fact]
    public void Remove_LastScopeOfAName_DropsTheName()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider().Set(LimitName, AcmeLimit, Acme);

        provider.Remove(LimitName, Acme);

        Assert.Equal(0, provider.Count);
        Assert.False(provider.Remove(LimitName));
    }

    [Fact]
    public void Remove_NullArguments_ThrowArgumentNullException()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider();

        Assert.Throws<ArgumentNullException>(() => provider.Remove(null!));
        Assert.Throws<ArgumentNullException>(() => provider.Remove(null!, Acme));
        Assert.Throws<ArgumentNullException>(() => provider.Remove(LimitName, null!));
    }

    // ---------------------------------------------------------------------
    // Async side
    // ---------------------------------------------------------------------

    [Fact]
    public async Task TryGetAsync_ReturnsSameAsTryGet()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider()
            .Set(LimitName, GlobalLimit)
            .Set(LimitName, AcmeLimit, Acme);

        RuleParameterLookup<decimal> tenant = await provider.TryGetAsync<decimal>(LimitName, Acme, TestToken);
        RuleParameterLookup<decimal> global = await provider.TryGetAsync<decimal>(LimitName, null, TestToken);
        RuleParameterLookup<decimal> unknown = await provider.TryGetAsync<decimal>(OtherName, null, TestToken);

        Assert.Equal(RuleParameterLookup<decimal>.Of(AcmeLimit), tenant);
        Assert.Equal(RuleParameterLookup<decimal>.Of(GlobalLimit), global);
        Assert.Equal(RuleParameterLookup<decimal>.Missing, unknown);
    }

    [Fact]
    public async Task TryGetAsync_CancelledToken_Throws()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider().Set(LimitName, GlobalLimit);
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.TryGetAsync<decimal>(LimitName, null, source.Token));
    }

    [Fact]
    public async Task TryGetAsync_WrongType_ThrowsInvalidCastException()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider().Set(LimitName, GlobalLimit);

        await Assert.ThrowsAsync<InvalidCastException>(() => provider.TryGetAsync<int>(LimitName, null, TestToken));
    }

    [Fact]
    public void Provider_ImplementsBothProviderInterfaces()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider();

        Assert.IsAssignableFrom<IRuleParameterProvider>(provider);
        Assert.IsAssignableFrom<IAsyncRuleParameterProvider>(provider);
    }
}
