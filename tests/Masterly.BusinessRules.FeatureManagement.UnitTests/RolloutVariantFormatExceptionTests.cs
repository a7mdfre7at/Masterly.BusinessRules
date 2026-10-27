namespace Masterly.BusinessRules.FeatureManagement.UnitTests;

public class RolloutVariantFormatExceptionTests
{
    private const string Flag = "credit-score-v2";

    [Fact]
    public void Constructor_SetsFlagAndVariant()
    {
        RolloutVariantFormatException exception = new(Flag, "Big");

        Assert.Equal(Flag, exception.Flag);
        Assert.Equal("Big", exception.Variant);
    }

    [Fact]
    public void Constructor_DerivesFromFormatException()
    {
        RolloutVariantFormatException exception = new(Flag, "Big");

        Assert.IsAssignableFrom<FormatException>(exception);
    }

    [Fact]
    public void Message_NamesFlagVariantAndConvention()
    {
        RolloutVariantFormatException exception = new(Flag, "Big");

        Assert.Contains($"'{Flag}'", exception.Message);
        Assert.Contains("'Big'", exception.Message);
        Assert.Contains(RolloutVariantConvention.ModeKey, exception.Message);
        Assert.Contains(RolloutVariantConvention.PercentKey, exception.Message);
        Assert.Contains("VariantRollouts", exception.Message);
    }

    [Fact]
    public void Message_NullVariant_SaysUnnamed()
    {
        RolloutVariantFormatException exception = new(Flag, null);

        Assert.Null(exception.Variant);
        Assert.Contains("<unnamed>", exception.Message);
    }

    [Fact]
    public void Constructor_NullFlag_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RolloutVariantFormatException(null!, "Big"));
    }
}
