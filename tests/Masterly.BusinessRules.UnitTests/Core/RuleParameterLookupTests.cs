namespace Masterly.BusinessRules.UnitTests;

public class RuleParameterLookupTests
{
    private const decimal Limit = 50_000m;
    private const decimal Fallback = 10_000m;

    [Fact]
    public void Missing_IsNotFoundWithDefaultValue()
    {
        RuleParameterLookup<decimal> lookup = RuleParameterLookup<decimal>.Missing;

        Assert.False(lookup.Found);
        Assert.Equal(0m, lookup.Value);
        Assert.Equal(default, lookup);
    }

    [Fact]
    public void Of_IsFoundWithTheValue()
    {
        RuleParameterLookup<decimal> lookup = RuleParameterLookup<decimal>.Of(Limit);

        Assert.True(lookup.Found);
        Assert.Equal(Limit, lookup.Value);
    }

    [Fact]
    public void Of_Null_IsAFoundNull()
    {
        RuleParameterLookup<string?> lookup = RuleParameterLookup<string?>.Of(null);

        Assert.True(lookup.Found);
        Assert.Null(lookup.Value);
        Assert.NotEqual(RuleParameterLookup<string?>.Missing, lookup);
    }

    [Fact]
    public void GetValueOrFallback_Found_ReturnsValue()
    {
        Assert.Equal(Limit, RuleParameterLookup<decimal>.Of(Limit).GetValueOrFallback(Fallback));
    }

    [Fact]
    public void GetValueOrFallback_Missing_ReturnsFallback()
    {
        Assert.Equal(Fallback, RuleParameterLookup<decimal>.Missing.GetValueOrFallback(Fallback));
    }

    [Fact]
    public void Equals_SameValue_AreEqual()
    {
        RuleParameterLookup<decimal> first = RuleParameterLookup<decimal>.Of(Limit);
        RuleParameterLookup<decimal> second = RuleParameterLookup<decimal>.Of(Limit);

        Assert.True(first.Equals(second));
        Assert.True(first.Equals((object)second));
        Assert.True(first == second);
        Assert.False(first != second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentValue_AreNotEqual()
    {
        Assert.True(RuleParameterLookup<decimal>.Of(Limit) != RuleParameterLookup<decimal>.Of(Fallback));
    }

    [Fact]
    public void Equals_MissingEqualsMissing()
    {
        Assert.True(RuleParameterLookup<decimal>.Missing == RuleParameterLookup<decimal>.Missing);
        Assert.Equal(RuleParameterLookup<decimal>.Missing.GetHashCode(), default(RuleParameterLookup<decimal>).GetHashCode());
    }

    [Fact]
    public void Equals_OtherType_IsFalse()
    {
        Assert.False(RuleParameterLookup<decimal>.Of(Limit).Equals(Limit));
    }

    [Fact]
    public void ToString_ReportsFoundOrMissing()
    {
        Assert.Equal("found: 50000", RuleParameterLookup<decimal>.Of(Limit).ToString());
        Assert.Equal("missing", RuleParameterLookup<decimal>.Missing.ToString());
    }
}
