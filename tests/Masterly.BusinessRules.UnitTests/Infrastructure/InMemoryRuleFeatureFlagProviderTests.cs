namespace Masterly.BusinessRules.UnitTests;

public class InMemoryRuleFeatureFlagProviderTests
{
    private const string Flag = "credit-score-v2";
    private const string OtherFlag = "sanctions-check";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public void GetRollout_UnknownFlag_ReturnsOff()
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider();

        Assert.Equal(RuleRollout.Off, provider.GetRollout(Flag, new BusinessRuleContext()));
        Assert.Equal(RuleRollout.Off, provider.DefaultRollout);
        Assert.Equal(0, provider.Count);
    }

    [Fact]
    public void GetRollout_UnknownFlag_ReturnsConfiguredDefault()
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider(RuleRollout.Shadow());

        Assert.Equal(RuleRollout.Shadow(), provider.GetRollout(Flag, new BusinessRuleContext()));
    }

    [Fact]
    public void Set_WithRollout_IsReturnedByGetRollout()
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider();

        provider.Set(Flag, RuleRollout.Enforced(10));

        Assert.Equal(RuleRollout.Enforced(10), provider.GetRollout(Flag, new BusinessRuleContext()));
        Assert.Equal(1, provider.Count);
    }

    [Fact]
    public void Set_WithModeAndPercent_BuildsRollout()
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider();

        provider.Set(Flag, RuleRolloutMode.Shadow, 25);

        Assert.Equal(RuleRollout.Shadow(25), provider.GetRollout(Flag, new BusinessRuleContext()));
    }

    [Fact]
    public void Set_WithModeOnly_DefaultsToEverybody()
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider();

        provider.Set(Flag, RuleRolloutMode.Enforced);

        Assert.Equal(RuleRollout.Enforced(), provider.GetRollout(Flag, new BusinessRuleContext()));
    }

    [Fact]
    public void Set_ReplacesEarlierRollout()
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider().Set(Flag, RuleRolloutMode.Off);

        provider.Set(Flag, RuleRolloutMode.Enforced);

        Assert.Equal(RuleRollout.Enforced(), provider.GetRollout(Flag, new BusinessRuleContext()));
        Assert.Equal(1, provider.Count);
    }

    [Fact]
    public void Set_IsChainable()
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider()
            .Set(Flag, RuleRolloutMode.Shadow)
            .Set(OtherFlag, RuleRolloutMode.Enforced, 10);

        Assert.Equal(RuleRollout.Shadow(), provider.GetRollout(Flag, new BusinessRuleContext()));
        Assert.Equal(RuleRollout.Enforced(10), provider.GetRollout(OtherFlag, new BusinessRuleContext()));
        Assert.Equal(2, provider.Count);
    }

    [Fact]
    public void Set_InvalidPercent_ThrowsArgumentOutOfRangeException()
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider();

        Assert.Throws<ArgumentOutOfRangeException>(() => provider.Set(Flag, RuleRolloutMode.Enforced, 101));
    }

    [Fact]
    public void Set_NullFlag_ThrowsArgumentNullException()
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider();

        Assert.Throws<ArgumentNullException>(() => provider.Set(null!, RuleRollout.Off));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Set_BlankFlag_ThrowsArgumentException(string flag)
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider();

        Assert.Throws<ArgumentException>(() => provider.Set(flag, RuleRollout.Off));
    }

    [Fact]
    public void GetRollout_FlagNamesAreCaseSensitive()
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider().Set(Flag, RuleRolloutMode.Enforced);

        Assert.Equal(RuleRollout.Off, provider.GetRollout(Flag.ToUpperInvariant(), new BusinessRuleContext()));
    }

    [Fact]
    public void GetRollout_NullFlag_ThrowsArgumentNullException()
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider();

        Assert.Throws<ArgumentNullException>(() => provider.GetRollout(null!, new BusinessRuleContext()));
    }

    [Fact]
    public void Remove_SetFlag_ReturnsTrueAndRestoresDefault()
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider().Set(Flag, RuleRolloutMode.Enforced);

        bool removed = provider.Remove(Flag);

        Assert.True(removed);
        Assert.Equal(RuleRollout.Off, provider.GetRollout(Flag, new BusinessRuleContext()));
        Assert.Equal(0, provider.Count);
    }

    [Fact]
    public void Remove_UnknownFlag_ReturnsFalse()
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider();

        Assert.False(provider.Remove(Flag));
    }

    [Fact]
    public void Remove_NullFlag_ThrowsArgumentNullException()
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider();

        Assert.Throws<ArgumentNullException>(() => provider.Remove(null!));
    }

    [Fact]
    public void TryGet_SetFlag_ReturnsRollout()
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider().Set(Flag, RuleRolloutMode.Shadow, 5);

        bool found = provider.TryGet(Flag, out RuleRollout rollout);

        Assert.True(found);
        Assert.Equal(RuleRollout.Shadow(5), rollout);
    }

    [Fact]
    public void TryGet_UnknownFlag_ReturnsFalseAndDefault()
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider(RuleRollout.Enforced());

        bool found = provider.TryGet(Flag, out RuleRollout rollout);

        Assert.False(found);
        Assert.Equal(default, rollout);
    }

    [Fact]
    public void TryGet_NullFlag_ThrowsArgumentNullException()
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider();

        Assert.Throws<ArgumentNullException>(() => provider.TryGet(null!, out _));
    }

    [Fact]
    public async Task GetRolloutAsync_ReturnsSameAsGetRollout()
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider().Set(Flag, RuleRolloutMode.Enforced, 10);

        RuleRollout rollout = await provider.GetRolloutAsync(Flag, new BusinessRuleContext(), TestToken);

        Assert.Equal(RuleRollout.Enforced(10), rollout);
        Assert.Equal(RuleRollout.Off, await provider.GetRolloutAsync(OtherFlag, new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task GetRolloutAsync_CancelledToken_Throws()
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider();
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetRolloutAsync(Flag, new BusinessRuleContext(), source.Token));
    }

    [Fact]
    public void Provider_ImplementsBothProviderInterfaces()
    {
        InMemoryRuleFeatureFlagProvider provider = new InMemoryRuleFeatureFlagProvider();

        Assert.IsAssignableFrom<IRuleFeatureFlagProvider>(provider);
        Assert.IsAssignableFrom<IAsyncRuleFeatureFlagProvider>(provider);
    }
}
