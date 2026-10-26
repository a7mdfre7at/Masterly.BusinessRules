namespace Masterly.BusinessRules.UnitTests;

public class RuleTestHelperParameterProviderTests
{
    private const string LimitName = "maxOrderAmount";
    private const string OtherName = "minimumAge";
    private const decimal Limit = 50_000m;

    [Fact]
    public void CreateParameterProvider_SetsAGlobalValue()
    {
        InMemoryRuleParameterProvider provider = RuleTestHelper.CreateParameterProvider(LimitName, Limit);

        Assert.True(provider.TryGet(LimitName, null, out decimal value));
        Assert.Equal(Limit, value);
        Assert.Equal(1, provider.Count);
    }

    [Fact]
    public void CreateParameterProvider_WithScope_SetsAScopedValue()
    {
        RuleScope acme = RuleScope.Tenant("acme");

        InMemoryRuleParameterProvider provider = RuleTestHelper.CreateParameterProvider(LimitName, Limit, acme);

        Assert.True(provider.TryGet(LimitName, acme, out decimal value));
        Assert.Equal(Limit, value);
        Assert.False(provider.TryGet(LimitName, null, out decimal _));
    }

    [Fact]
    public void CreateParameterProvider_OtherParametersAreMissing()
    {
        InMemoryRuleParameterProvider provider = RuleTestHelper.CreateParameterProvider(LimitName, Limit);

        Assert.False(provider.TryGet(OtherName, null, out int _));
    }

    [Fact]
    public void CreateParameterProvider_CanBeExtendedWithSet()
    {
        InMemoryRuleParameterProvider provider = RuleTestHelper.CreateParameterProvider(LimitName, Limit).Set(OtherName, 18);

        Assert.True(provider.TryGet(OtherName, null, out int age));
        Assert.Equal(18, age);
        Assert.Equal(2, provider.Count);
    }

    [Fact]
    public void CreateParameterProvider_NullName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleTestHelper.CreateParameterProvider(null!, Limit));
    }

    [Fact]
    public void CreateParameterProvider_BlankName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => RuleTestHelper.CreateParameterProvider(" ", Limit));
    }
}
