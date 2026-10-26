namespace Masterly.BusinessRules.UnitTests;

public class RuleScopeTests
{
    private const string Acme = "acme";
    private const string Globex = "globex";
    private const string RegionKey = "region";
    private const string Eu = "EU";
    private const string Us = "US";

    // ---------------------------------------------------------------------
    // Construction
    // ---------------------------------------------------------------------

    [Fact]
    public void Global_HasNoPairsAndZeroSpecificity()
    {
        RuleScope scope = RuleScope.Global;

        Assert.True(scope.IsGlobal);
        Assert.Equal(0, scope.Specificity);
        Assert.Empty(scope.Pairs);
        Assert.Null(scope.TenantId);
    }

    [Fact]
    public void Tenant_StoresTheTenantPair()
    {
        RuleScope scope = RuleScope.Tenant(Acme);

        Assert.False(scope.IsGlobal);
        Assert.Equal(1, scope.Specificity);
        Assert.Equal(Acme, scope.TenantId);
        Assert.Equal(new KeyValuePair<string, string>(RuleScope.TenantKey, Acme), Assert.Single(scope.Pairs));
    }

    [Fact]
    public void Tenant_NullTenantId_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleScope.Tenant(null!));
    }

    [Fact]
    public void With_AddsAPairAndRaisesSpecificity()
    {
        RuleScope scope = RuleScope.Tenant(Acme).With(RegionKey, Eu);

        Assert.Equal(2, scope.Specificity);
        Assert.True(scope.TryGet(RegionKey, out string region));
        Assert.Equal(Eu, region);
        Assert.Equal(Acme, scope.TenantId);
    }

    [Fact]
    public void With_DoesNotModifyTheOriginal()
    {
        RuleScope original = RuleScope.Tenant(Acme);

        RuleScope extended = original.With(RegionKey, Eu);

        Assert.Equal(1, original.Specificity);
        Assert.False(original.ContainsKey(RegionKey));
        Assert.NotSame(original, extended);
    }

    [Fact]
    public void With_ExistingKey_ReplacesTheValue()
    {
        RuleScope scope = RuleScope.Tenant(Acme).With(RuleScope.TenantKey, Globex);

        Assert.Equal(1, scope.Specificity);
        Assert.Equal(Globex, scope.TenantId);
    }

    [Fact]
    public void With_KeepsPairsSortedByKey()
    {
        RuleScope scope = RuleScope.Global.With(RegionKey, Eu).With("a", "1").With(RuleScope.TenantKey, Acme);

        Assert.Equal(new[] { "a", RegionKey, RuleScope.TenantKey }, scope.Pairs.Select(pair => pair.Key));
    }

    [Fact]
    public void With_EmptyValue_IsAllowed()
    {
        RuleScope scope = RuleScope.Global.With(RegionKey, string.Empty);

        Assert.True(scope.TryGet(RegionKey, out string value));
        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void With_NullKey_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => RuleScope.Global.With(null!, Eu));

        Assert.Equal("key", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void With_BlankKey_ThrowsArgumentException(string key)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => RuleScope.Global.With(key, Eu));

        Assert.Equal("key", exception.ParamName);
    }

    [Fact]
    public void With_NullValue_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => RuleScope.Global.With(RegionKey, null!));

        Assert.Equal("value", exception.ParamName);
    }

    // ---------------------------------------------------------------------
    // Lookup
    // ---------------------------------------------------------------------

    [Fact]
    public void TryGet_UnknownKey_ReturnsFalse()
    {
        RuleScope scope = RuleScope.Tenant(Acme);

        Assert.False(scope.TryGet(RegionKey, out string value));
        Assert.Null(value);
    }

    [Fact]
    public void TryGet_KeysAreCaseSensitive()
    {
        RuleScope scope = RuleScope.Tenant(Acme);

        Assert.False(scope.TryGet(RuleScope.TenantKey.ToUpperInvariant(), out _));
    }

    [Fact]
    public void TryGet_NullKey_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleScope.Global.TryGet(null!, out _));
    }

    [Fact]
    public void ContainsKey_ReportsPresence()
    {
        RuleScope scope = RuleScope.Tenant(Acme);

        Assert.True(scope.ContainsKey(RuleScope.TenantKey));
        Assert.False(scope.ContainsKey(RegionKey));
    }

    [Fact]
    public void ContainsKey_NullKey_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleScope.Global.ContainsKey(null!));
    }

    // ---------------------------------------------------------------------
    // AppliesTo
    // ---------------------------------------------------------------------

    [Fact]
    public void AppliesTo_Global_AppliesToEverything()
    {
        Assert.True(RuleScope.Global.AppliesTo(RuleScope.Global));
        Assert.True(RuleScope.Global.AppliesTo(RuleScope.Tenant(Acme)));
        Assert.True(RuleScope.Global.AppliesTo(RuleScope.Tenant(Acme).With(RegionKey, Eu)));
    }

    [Fact]
    public void AppliesTo_LessSpecificScope_AppliesToMoreSpecificOne()
    {
        RuleScope tenant = RuleScope.Tenant(Acme);
        RuleScope tenantRegion = tenant.With(RegionKey, Eu);

        Assert.True(tenant.AppliesTo(tenantRegion));
        Assert.False(tenantRegion.AppliesTo(tenant));
    }

    [Fact]
    public void AppliesTo_SameScope_IsTrue()
    {
        RuleScope scope = RuleScope.Tenant(Acme).With(RegionKey, Eu);

        Assert.True(scope.AppliesTo(RuleScope.Tenant(Acme).With(RegionKey, Eu)));
    }

    [Fact]
    public void AppliesTo_DifferentValue_IsFalse()
    {
        Assert.False(RuleScope.Tenant(Acme).AppliesTo(RuleScope.Tenant(Globex)));
        Assert.False(RuleScope.Global.With(RegionKey, Eu).AppliesTo(RuleScope.Tenant(Acme).With(RegionKey, Us)));
    }

    [Fact]
    public void AppliesTo_NonGlobalScope_DoesNotApplyToGlobal()
    {
        Assert.False(RuleScope.Tenant(Acme).AppliesTo(RuleScope.Global));
    }

    [Fact]
    public void AppliesTo_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleScope.Global.AppliesTo(null!));
    }

    // ---------------------------------------------------------------------
    // Equality
    // ---------------------------------------------------------------------

    [Fact]
    public void Equals_SamePairs_AreEqual()
    {
        RuleScope first = RuleScope.Tenant(Acme).With(RegionKey, Eu);
        RuleScope second = RuleScope.Tenant(Acme).With(RegionKey, Eu);

        Assert.True(first.Equals(second));
        Assert.True(first.Equals((object)second));
        Assert.True(first == second);
        Assert.False(first != second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_SamePairsInDifferentOrder_AreEqual()
    {
        RuleScope first = RuleScope.Tenant(Acme).With(RegionKey, Eu);
        RuleScope second = RuleScope.Global.With(RegionKey, Eu).With(RuleScope.TenantKey, Acme);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentValue_AreNotEqual()
    {
        Assert.NotEqual(RuleScope.Tenant(Acme), RuleScope.Tenant(Globex));
        Assert.True(RuleScope.Tenant(Acme) != RuleScope.Tenant(Globex));
    }

    [Fact]
    public void Equals_DifferentSpecificity_AreNotEqual()
    {
        Assert.NotEqual(RuleScope.Tenant(Acme), RuleScope.Tenant(Acme).With(RegionKey, Eu));
    }

    [Fact]
    public void Equals_ValuesAreCaseSensitive()
    {
        Assert.NotEqual(RuleScope.Tenant(Acme), RuleScope.Tenant(Acme.ToUpperInvariant()));
    }

    [Fact]
    public void Equals_GlobalEqualsGlobal()
    {
        Assert.Equal(RuleScope.Global, RuleScope.Global);
        Assert.True(RuleScope.Global == RuleScope.Global);
    }

    [Fact]
    public void Equals_Null_IsFalseAndOperatorsHandleNull()
    {
        RuleScope scope = RuleScope.Tenant(Acme);
        RuleScope? nullScope = null;

        Assert.False(scope.Equals(null));
        Assert.False(scope.Equals((object?)null));
        Assert.False(scope == nullScope);
        Assert.True(scope != nullScope);
        Assert.True(nullScope == null);
    }

    [Fact]
    public void Equals_OtherType_IsFalse()
    {
        Assert.False(RuleScope.Tenant(Acme).Equals(Acme));
    }

    // ---------------------------------------------------------------------
    // ToString
    // ---------------------------------------------------------------------

    [Fact]
    public void ToString_Global_ReportsGlobal()
    {
        Assert.Equal("(global)", RuleScope.Global.ToString());
    }

    [Fact]
    public void ToString_ListsPairsInKeyOrder()
    {
        RuleScope scope = RuleScope.Tenant(Acme).With(RegionKey, Eu);

        Assert.Equal("region=EU;tenant=acme", scope.ToString());
    }
}
