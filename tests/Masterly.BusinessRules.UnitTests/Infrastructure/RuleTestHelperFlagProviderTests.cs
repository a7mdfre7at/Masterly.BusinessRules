namespace Masterly.BusinessRules.UnitTests;

public class RuleTestHelperFlagProviderTests
{
    private const string Flag = "credit-score-v2";
    private const string OtherFlag = "sanctions-check";

    [Fact]
    public void CreateFlagProvider_SetsTheFlag()
    {
        InMemoryRuleFeatureFlagProvider provider = RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow, percent: 100);

        Assert.Equal(RuleRollout.Shadow(100), provider.GetRollout(Flag, new BusinessRuleContext()));
        Assert.Equal(1, provider.Count);
    }

    [Fact]
    public void CreateFlagProvider_PercentDefaultsToEverybody()
    {
        InMemoryRuleFeatureFlagProvider provider = RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced);

        Assert.Equal(RuleRollout.Enforced(), provider.GetRollout(Flag, new BusinessRuleContext()));
    }

    [Fact]
    public void CreateFlagProvider_OtherFlagsAreOff()
    {
        InMemoryRuleFeatureFlagProvider provider = RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced);

        Assert.Equal(RuleRollout.Off, provider.GetRollout(OtherFlag, new BusinessRuleContext()));
    }

    [Fact]
    public void CreateFlagProvider_CanBeExtendedWithSet()
    {
        InMemoryRuleFeatureFlagProvider provider = RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Shadow).Set(OtherFlag, RuleRolloutMode.Enforced, 10);

        Assert.Equal(RuleRollout.Enforced(10), provider.GetRollout(OtherFlag, new BusinessRuleContext()));
    }

    [Fact]
    public void CreateFlagProvider_InvalidPercent_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RuleTestHelper.CreateFlagProvider(Flag, RuleRolloutMode.Enforced, percent: 101));
    }

    [Fact]
    public void CreateFlagProvider_NullFlag_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleTestHelper.CreateFlagProvider(null!, RuleRolloutMode.Enforced));
    }

    [Fact]
    public void CreateFlagProvider_BlankFlag_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => RuleTestHelper.CreateFlagProvider(" ", RuleRolloutMode.Enforced));
    }
}
