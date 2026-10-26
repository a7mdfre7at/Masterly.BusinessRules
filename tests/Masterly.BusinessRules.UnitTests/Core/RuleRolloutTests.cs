namespace Masterly.BusinessRules.UnitTests;

public class RuleRolloutTests
{
    [Fact]
    public void Default_IsOffAtZeroPercent()
    {
        RuleRollout rollout = default;

        Assert.Equal(RuleRolloutMode.Off, rollout.Mode);
        Assert.Equal(0, rollout.Percent);
        Assert.True(rollout.IsOff);
        Assert.Equal(RuleRollout.Off, rollout);
    }

    [Fact]
    public void Constructor_StoresModeAndPercent()
    {
        RuleRollout rollout = new RuleRollout(RuleRolloutMode.Shadow, 25);

        Assert.Equal(RuleRolloutMode.Shadow, rollout.Mode);
        Assert.Equal(25, rollout.Percent);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Constructor_PercentOutOfRange_ThrowsArgumentOutOfRangeException(int percent)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new RuleRollout(RuleRolloutMode.Enforced, percent));

        Assert.Equal("percent", exception.ParamName);
    }

    [Fact]
    public void Constructor_UndefinedMode_ThrowsArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new RuleRollout((RuleRolloutMode)42, 50));

        Assert.Equal("mode", exception.ParamName);
    }

    [Fact]
    public void Shadow_DefaultsToEverybody()
    {
        RuleRollout rollout = RuleRollout.Shadow();

        Assert.Equal(RuleRolloutMode.Shadow, rollout.Mode);
        Assert.Equal(RuleRollout.MaxPercent, rollout.Percent);
        Assert.True(rollout.IsComplete);
        Assert.False(rollout.IsOff);
    }

    [Fact]
    public void Enforced_WithPercent_StoresPercent()
    {
        RuleRollout rollout = RuleRollout.Enforced(10);

        Assert.Equal(RuleRolloutMode.Enforced, rollout.Mode);
        Assert.Equal(10, rollout.Percent);
        Assert.False(rollout.IsComplete);
        Assert.False(rollout.IsOff);
    }

    [Fact]
    public void Shadow_PercentOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RuleRollout.Shadow(101));
        Assert.Throws<ArgumentOutOfRangeException>(() => RuleRollout.Enforced(-1));
    }

    [Theory]
    [InlineData(RuleRolloutMode.Off, 100)]
    [InlineData(RuleRolloutMode.Shadow, 0)]
    [InlineData(RuleRolloutMode.Enforced, 0)]
    public void IsOff_OffModeOrZeroPercent_IsTrue(RuleRolloutMode mode, int percent)
    {
        Assert.True(new RuleRollout(mode, percent).IsOff);
    }

    [Fact]
    public void Includes_BucketBelowPercent_IsTrue()
    {
        RuleRollout rollout = RuleRollout.Enforced(10);

        Assert.True(rollout.Includes(0));
        Assert.True(rollout.Includes(9));
        Assert.False(rollout.Includes(10));
        Assert.False(rollout.Includes(99));
    }

    [Fact]
    public void Includes_OffRollout_IsFalseForEveryBucket()
    {
        RuleRollout off = new RuleRollout(RuleRolloutMode.Off, 100);

        Assert.False(off.Includes(0));
        Assert.False(off.Includes(99));
    }

    [Fact]
    public void Includes_CompleteRollout_IsTrueForEveryBucket()
    {
        RuleRollout complete = RuleRollout.Shadow();

        Assert.True(complete.Includes(0));
        Assert.True(complete.Includes(99));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void Includes_BucketOutOfRange_ThrowsArgumentOutOfRangeException(int bucket)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RuleRollout.Enforced().Includes(bucket));
    }

    [Fact]
    public void Equals_SameModeAndPercent_IsEqual()
    {
        RuleRollout first = RuleRollout.Shadow(10);
        RuleRollout second = new RuleRollout(RuleRolloutMode.Shadow, 10);

        Assert.True(first.Equals(second));
        Assert.True(first.Equals((object)second));
        Assert.True(first == second);
        Assert.False(first != second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentModeOrPercent_IsNotEqual()
    {
        RuleRollout shadow = RuleRollout.Shadow(10);

        Assert.NotEqual(shadow, RuleRollout.Enforced(10));
        Assert.NotEqual(shadow, RuleRollout.Shadow(11));
        Assert.True(shadow != RuleRollout.Enforced(10));
        Assert.False(shadow.Equals(null));
        Assert.False(shadow.Equals("Shadow 10%"));
    }

    [Fact]
    public void ToString_ReportsModeAndPercent()
    {
        Assert.Equal("Shadow 10%", RuleRollout.Shadow(10).ToString());
        Assert.Equal("Off 0%", RuleRollout.Off.ToString());
    }
}
