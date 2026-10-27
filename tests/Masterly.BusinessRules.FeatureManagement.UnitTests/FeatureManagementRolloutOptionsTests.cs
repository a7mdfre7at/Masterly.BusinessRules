using Microsoft.FeatureManagement.FeatureFilters;

namespace Masterly.BusinessRules.FeatureManagement.UnitTests;

public class FeatureManagementRolloutOptionsTests
{
    private const string Dark = "dark";

    [Fact]
    public void Defaults_EnforcedOnEnabled_ThrowOnUnrecognized_NoSelector_NoMappings()
    {
        FeatureManagementRolloutOptions options = new();

        Assert.Equal(RuleRollout.Enforced(), options.EnabledRollout);
        Assert.Equal(UnrecognizedVariantBehavior.Throw, options.OnUnrecognizedVariant);
        Assert.Null(options.TargetingContextSelector);
        Assert.Empty(options.VariantRollouts);
    }

    [Fact]
    public void Setters_RoundTrip()
    {
        Func<BusinessRuleContext, ITargetingContext?> selector = ctx => new TargetingContext { UserId = "u" };
        FeatureManagementRolloutOptions options = new FeatureManagementRolloutOptions()
        {
            EnabledRollout = RuleRollout.Shadow(),
            OnUnrecognizedVariant = UnrecognizedVariantBehavior.Off,
            TargetingContextSelector = selector
        };

        Assert.Equal(RuleRollout.Shadow(), options.EnabledRollout);
        Assert.Equal(UnrecognizedVariantBehavior.Off, options.OnUnrecognizedVariant);
        Assert.Same(selector, options.TargetingContextSelector);
    }

    [Fact]
    public void MapVariant_AddsMapping_ReturnsSameOptions()
    {
        FeatureManagementRolloutOptions options = new();

        FeatureManagementRolloutOptions returned = options.MapVariant(Dark, RuleRollout.Shadow(10));

        Assert.Same(options, returned);
        Assert.Single(options.VariantRollouts);
        Assert.Equal(RuleRollout.Shadow(10), options.VariantRollouts[Dark]);
    }

    [Fact]
    public void MapVariant_ModeAndPercent_BuildsRollout()
    {
        FeatureManagementRolloutOptions options = new FeatureManagementRolloutOptions().MapVariant(Dark, RuleRolloutMode.Enforced, 25);

        Assert.Equal(RuleRollout.Enforced(25), options.VariantRollouts[Dark]);
    }

    [Fact]
    public void MapVariant_ModeOnly_DefaultsTo100Percent()
    {
        FeatureManagementRolloutOptions options = new FeatureManagementRolloutOptions().MapVariant(Dark, RuleRolloutMode.Shadow);

        Assert.Equal(RuleRollout.Shadow(), options.VariantRollouts[Dark]);
    }

    [Fact]
    public void MapVariant_SameNameTwice_LastWins()
    {
        FeatureManagementRolloutOptions options = new FeatureManagementRolloutOptions()
            .MapVariant(Dark, RuleRollout.Shadow())
            .MapVariant(Dark, RuleRollout.Enforced());

        Assert.Single(options.VariantRollouts);
        Assert.Equal(RuleRollout.Enforced(), options.VariantRollouts[Dark]);
    }

    [Fact]
    public void MapVariant_NullName_ThrowsArgumentNullException()
    {
        FeatureManagementRolloutOptions options = new();

        Assert.Throws<ArgumentNullException>(() => options.MapVariant(null!, RuleRollout.Shadow()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MapVariant_BlankName_ThrowsArgumentException(string name)
    {
        FeatureManagementRolloutOptions options = new();

        Assert.Throws<ArgumentException>(() => options.MapVariant(name, RuleRollout.Shadow()));
    }

    [Fact]
    public void MapVariant_InvalidPercent_ThrowsArgumentOutOfRangeException()
    {
        FeatureManagementRolloutOptions options = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.MapVariant(Dark, RuleRolloutMode.Shadow, 101));
    }

    [Fact]
    public void TryGetVariantRollout_IsCaseInsensitive()
    {
        FeatureManagementRolloutOptions options = new FeatureManagementRolloutOptions().MapVariant(Dark, RuleRollout.Shadow());

        bool found = options.TryGetVariantRollout("DARK", out RuleRollout rollout);

        Assert.True(found);
        Assert.Equal(RuleRollout.Shadow(), rollout);
    }

    [Fact]
    public void TryGetVariantRollout_Unmapped_ReturnsFalseAndOff()
    {
        FeatureManagementRolloutOptions options = new();

        bool found = options.TryGetVariantRollout("other", out RuleRollout rollout);

        Assert.False(found);
        Assert.Equal(RuleRollout.Off, rollout);
    }

    [Fact]
    public void TryGetVariantRollout_NullName_ReturnsFalse()
    {
        FeatureManagementRolloutOptions options = new FeatureManagementRolloutOptions().MapVariant(Dark, RuleRollout.Shadow());

        bool found = options.TryGetVariantRollout(null, out RuleRollout rollout);

        Assert.False(found);
        Assert.Equal(RuleRollout.Off, rollout);
    }
}
