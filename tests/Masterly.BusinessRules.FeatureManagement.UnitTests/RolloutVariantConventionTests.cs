using Microsoft.Extensions.Configuration;

namespace Masterly.BusinessRules.FeatureManagement.UnitTests;

public class RolloutVariantConventionTests
{
    // ---------------------------------------------------------------------
    // TryParse / Parse: variant names
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("Shadow", RuleRolloutMode.Shadow, 100)]
    [InlineData("shadow", RuleRolloutMode.Shadow, 100)]
    [InlineData("ENFORCED", RuleRolloutMode.Enforced, 100)]
    [InlineData("Off", RuleRolloutMode.Off, 0)]
    [InlineData("Off-50", RuleRolloutMode.Off, 0)]
    [InlineData("Enforced-10", RuleRolloutMode.Enforced, 10)]
    [InlineData("enforced:25%", RuleRolloutMode.Enforced, 25)]
    [InlineData("Shadow_5", RuleRolloutMode.Shadow, 5)]
    [InlineData("Shadow/50", RuleRolloutMode.Shadow, 50)]
    [InlineData("Shadow 50", RuleRolloutMode.Shadow, 50)]
    [InlineData("  Shadow-10  ", RuleRolloutMode.Shadow, 10)]
    [InlineData("Enforced-100", RuleRolloutMode.Enforced, 100)]
    [InlineData("Enforced-0", RuleRolloutMode.Enforced, 0)]
    [InlineData("Shadow- 10 %", RuleRolloutMode.Shadow, 10)]
    public void TryParse_ValidName_ReturnsRollout(string name, RuleRolloutMode expectedMode, int expectedPercent)
    {
        bool parsed = RolloutVariantConvention.TryParse(name, out RuleRollout rollout);

        Assert.True(parsed);
        Assert.Equal(new RuleRollout(expectedMode, expectedPercent), rollout);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Big")]
    [InlineData("1")]
    [InlineData("Shadow-101")]
    [InlineData("Shadow--10")]
    [InlineData("Shadow-")]
    [InlineData("Shadow-ten")]
    [InlineData("Shadow-%")]
    [InlineData("Shadow-+10")]
    [InlineData("Shadow-10-20")]
    [InlineData("Shadow-1.5")]
    [InlineData("Shadowy")]
    public void TryParse_InvalidName_ReturnsFalseAndOff(string? name)
    {
        bool parsed = RolloutVariantConvention.TryParse(name, out RuleRollout rollout);

        Assert.False(parsed);
        Assert.Equal(RuleRollout.Off, rollout);
    }

    [Fact]
    public void TryParse_OffMode_IsCanonicalOff()
    {
        bool parsed = RolloutVariantConvention.TryParse("off:75%", out RuleRollout rollout);

        Assert.True(parsed);
        Assert.Equal(RuleRollout.Off, rollout);
        Assert.Equal(RuleRollout.MinPercent, rollout.Percent);
    }

    [Fact]
    public void TryRead_OffMode_IsCanonicalOff()
    {
        IConfigurationSection section = Variants.WithConfiguration("x", (RolloutVariantConvention.ModeKey, "Off"), (RolloutVariantConvention.PercentKey, "50")).Configuration;

        bool read = RolloutVariantConvention.TryRead(section, out RuleRollout rollout);

        Assert.True(read);
        Assert.Equal(RuleRollout.Off, rollout);
    }

    [Fact]
    public void Parse_ValidName_ReturnsRollout()
    {
        RuleRollout rollout = RolloutVariantConvention.Parse("Enforced-10");

        Assert.Equal(RuleRollout.Enforced(10), rollout);
    }

    [Fact]
    public void Parse_InvalidName_ThrowsFormatException()
    {
        FormatException exception = Assert.Throws<FormatException>(() => RolloutVariantConvention.Parse("Big"));

        Assert.Contains("'Big'", exception.Message);
        Assert.Contains("Shadow", exception.Message);
    }

    [Fact]
    public void Parse_NullName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RolloutVariantConvention.Parse(null!));
    }

    // ---------------------------------------------------------------------
    // TryRead: configuration sections
    // ---------------------------------------------------------------------

    [Fact]
    public void TryRead_NullSection_ReturnsFalse()
    {
        bool read = RolloutVariantConvention.TryRead(null, out RuleRollout rollout);

        Assert.False(read);
        Assert.Equal(RuleRollout.Off, rollout);
    }

    [Fact]
    public void TryRead_EmptySection_ReturnsFalse()
    {
        bool read = RolloutVariantConvention.TryRead(Variants.EmptySection(), out RuleRollout rollout);

        Assert.False(read);
        Assert.Equal(RuleRollout.Off, rollout);
    }

    [Fact]
    public void TryRead_ModeOnly_DefaultsPercentTo100()
    {
        IConfigurationSection section = Variants.WithConfiguration("x", (RolloutVariantConvention.ModeKey, "Shadow")).Configuration;

        bool read = RolloutVariantConvention.TryRead(section, out RuleRollout rollout);

        Assert.True(read);
        Assert.Equal(RuleRollout.Shadow(), rollout);
    }

    [Fact]
    public void TryRead_ModeAndPercent_ReturnsBoth()
    {
        IConfigurationSection section = Variants.WithConfiguration("x", (RolloutVariantConvention.ModeKey, "enforced"), (RolloutVariantConvention.PercentKey, "10")).Configuration;

        bool read = RolloutVariantConvention.TryRead(section, out RuleRollout rollout);

        Assert.True(read);
        Assert.Equal(RuleRollout.Enforced(10), rollout);
    }

    [Fact]
    public void TryRead_PercentWithSuffix_IsAccepted()
    {
        IConfigurationSection section = Variants.WithConfiguration("x", (RolloutVariantConvention.ModeKey, "Enforced"), (RolloutVariantConvention.PercentKey, "25%")).Configuration;

        bool read = RolloutVariantConvention.TryRead(section, out RuleRollout rollout);

        Assert.True(read);
        Assert.Equal(RuleRollout.Enforced(25), rollout);
    }

    [Fact]
    public void TryRead_WhitespacePercent_DefaultsTo100()
    {
        IConfigurationSection section = Variants.WithConfiguration("x", (RolloutVariantConvention.ModeKey, "Shadow"), (RolloutVariantConvention.PercentKey, "  ")).Configuration;

        bool read = RolloutVariantConvention.TryRead(section, out RuleRollout rollout);

        Assert.True(read);
        Assert.Equal(RuleRollout.Shadow(), rollout);
    }

    [Theory]
    [InlineData("Big", "10")]
    [InlineData("Shadow", "101")]
    [InlineData("Shadow", "ten")]
    [InlineData("Shadow", "-1")]
    [InlineData("", "10")]
    public void TryRead_InvalidModeOrPercent_ReturnsFalse(string mode, string percent)
    {
        IConfigurationSection section = Variants.WithConfiguration("x", (RolloutVariantConvention.ModeKey, mode), (RolloutVariantConvention.PercentKey, percent)).Configuration;

        bool read = RolloutVariantConvention.TryRead(section, out RuleRollout rollout);

        Assert.False(read);
        Assert.Equal(RuleRollout.Off, rollout);
    }

    [Fact]
    public void TryRead_ScalarValue_IsParsedLikeAName()
    {
        IConfigurationSection section = Variants.WithScalarConfiguration("x", "Shadow-10").Configuration;

        bool read = RolloutVariantConvention.TryRead(section, out RuleRollout rollout);

        Assert.True(read);
        Assert.Equal(RuleRollout.Shadow(10), rollout);
    }

    [Fact]
    public void TryRead_InvalidScalarValue_ReturnsFalse()
    {
        IConfigurationSection section = Variants.WithScalarConfiguration("x", "Big").Configuration;

        bool read = RolloutVariantConvention.TryRead(section, out RuleRollout rollout);

        Assert.False(read);
        Assert.Equal(RuleRollout.Off, rollout);
    }

    [Fact]
    public void TryRead_ModeKeyWinsOverScalarValue()
    {
        Dictionary<string, string?> data = new()
        {
            ["variant"] = "Enforced",
            [$"variant:{RolloutVariantConvention.ModeKey}"] = "Shadow"
        };
        IConfigurationSection section = new ConfigurationBuilder().AddInMemoryCollection(data).Build().GetSection("variant");

        bool read = RolloutVariantConvention.TryRead(section, out RuleRollout rollout);

        Assert.True(read);
        Assert.Equal(RuleRollout.Shadow(), rollout);
    }

    // ---------------------------------------------------------------------
    // Constants
    // ---------------------------------------------------------------------

    [Fact]
    public void Constants_MatchDocumentedConvention()
    {
        Assert.Equal("Mode", RolloutVariantConvention.ModeKey);
        Assert.Equal("Percent", RolloutVariantConvention.PercentKey);
        Assert.Equal('%', RolloutVariantConvention.PercentSuffix);
        Assert.Equal("-:_/ ", RolloutVariantConvention.Separators);
    }
}
