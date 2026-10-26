using System.Globalization;

namespace Masterly.BusinessRules.UnitTests;

public class RuleResultDetailsTests
{
    private const string Currency = "currency";
    private const string Usd = "USD";
    private const string Hint = "Reduce the amount.";

    [Fact]
    public void Empty_CarriesNothing()
    {
        RuleResultDetails empty = RuleResultDetails.Empty;

        Assert.True(empty.IsEmpty);
        Assert.Null(empty.DetailFormat);
        Assert.Null(empty.ActualValue);
        Assert.Null(empty.ExpectedValue);
        Assert.Null(empty.RemediationText);
        Assert.Empty(empty.Data);
        Assert.Null(empty.FormatDetail(null));
    }

    [Fact]
    public void Detail_NullFormat_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleResultDetails.Detail(null!));
    }

    [Fact]
    public void Detail_StoresFormatAndIsNotEmpty()
    {
        decimal amount = 5200m;

        RuleResultDetails details = RuleResultDetails.Detail($"Amount {amount}");

        Assert.False(details.IsEmpty);
        Assert.NotNull(details.DetailFormat);
        Assert.Equal("Amount {0}", details.DetailFormat!.Format);
    }

    [Fact]
    public void FormatDetail_NullProvider_UsesInvariantCulture()
    {
        decimal amount = 1234.5m;
        RuleResultDetails details = RuleResultDetails.Detail($"Amount {amount:N2}");

        string? text = details.FormatDetail(null);

        Assert.Equal("Amount 1,234.50", text);
    }

    [Fact]
    public void FormatDetail_WithProvider_UsesProvider()
    {
        decimal amount = 1234.5m;
        RuleResultDetails details = RuleResultDetails.Detail($"Amount {amount:N2}");

        string? text = details.FormatDetail(new CultureInfo("de-DE"));

        Assert.Equal("Amount 1.234,50", text);
    }

    [Fact]
    public void Actual_ReturnsNewInstanceAndLeavesSourceUnchanged()
    {
        RuleResultDetails source = RuleResultDetails.Empty;

        RuleResultDetails withActual = source.Actual(5200m);

        Assert.NotSame(source, withActual);
        Assert.Equal(5200m, withActual.ActualValue);
        Assert.True(source.IsEmpty);
        Assert.False(withActual.IsEmpty);
    }

    [Fact]
    public void Expected_ReturnsNewInstanceAndLeavesSourceUnchanged()
    {
        RuleResultDetails source = RuleResultDetails.Empty;

        RuleResultDetails withExpected = source.Expected("<= 5000");

        Assert.NotSame(source, withExpected);
        Assert.Equal("<= 5000", withExpected.ExpectedValue);
        Assert.Null(source.ExpectedValue);
    }

    [Fact]
    public void Actual_Null_ClearsValue()
    {
        RuleResultDetails details = RuleResultDetails.Empty.Actual(1).Actual(null);

        Assert.Null(details.ActualValue);
        Assert.True(details.IsEmpty);
    }

    [Fact]
    public void Remediation_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleResultDetails.Empty.Remediation(null!));
    }

    [Fact]
    public void Remediation_StoresHint()
    {
        RuleResultDetails details = RuleResultDetails.Empty.Remediation(Hint);

        Assert.Equal(Hint, details.RemediationText);
        Assert.False(details.IsEmpty);
    }

    [Fact]
    public void With_NullKey_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleResultDetails.Empty.With(null!, Usd));
    }

    [Fact]
    public void With_AddsEntryWithoutMutatingSource()
    {
        RuleResultDetails source = RuleResultDetails.Empty;

        RuleResultDetails withData = source.With(Currency, Usd);

        Assert.Empty(source.Data);
        Assert.Single(withData.Data);
        Assert.Equal(Usd, withData.Data[Currency]);
    }

    [Fact]
    public void With_SameKeyTwice_ReplacesValue()
    {
        RuleResultDetails details = RuleResultDetails.Empty.With(Currency, Usd).With(Currency, "EUR");

        Assert.Single(details.Data);
        Assert.Equal("EUR", details.Data[Currency]);
    }

    [Fact]
    public void With_NullValue_IsStored()
    {
        RuleResultDetails details = RuleResultDetails.Empty.With(Currency, null);

        Assert.True(details.Data.ContainsKey(Currency));
        Assert.Null(details.Data[Currency]);
        Assert.False(details.IsEmpty);
    }

    [Fact]
    public void Data_IsReadOnly()
    {
        RuleResultDetails details = RuleResultDetails.Empty.With(Currency, Usd);

        IDictionary<string, object?> mutable = Assert.IsAssignableFrom<IDictionary<string, object?>>(details.Data);

        Assert.Throws<NotSupportedException>(() => mutable.Add("other", 1));
    }

    [Fact]
    public void Chain_KeepsEveryEarlierValue()
    {
        decimal amount = 5200m;

        RuleResultDetails details = RuleResultDetails
            .Detail($"Amount {amount}")
            .Actual(amount)
            .Expected("<= 5000")
            .Remediation(Hint)
            .With(Currency, Usd);

        Assert.Equal("Amount 5200", details.FormatDetail(null));
        Assert.Equal(amount, details.ActualValue);
        Assert.Equal("<= 5000", details.ExpectedValue);
        Assert.Equal(Hint, details.RemediationText);
        Assert.Equal(Usd, details.Data[Currency]);
    }
}
