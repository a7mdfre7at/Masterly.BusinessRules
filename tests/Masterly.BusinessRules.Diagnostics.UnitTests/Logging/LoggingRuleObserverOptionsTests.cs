using Microsoft.Extensions.Logging;

namespace Masterly.BusinessRules.Diagnostics.UnitTests.Logging;

public class LoggingRuleObserverOptionsTests
{
    [Fact]
    public void Defaults_AreConservative()
    {
        LoggingRuleObserverOptions options = new LoggingRuleObserverOptions();

        Assert.False(options.LogPassedRules);
        Assert.Equal(LogLevel.Debug, options.PassedLevel);
        Assert.Equal(LogLevel.Error, options.FaultedLevel);
        Assert.True(options.LogBatches);
        Assert.Equal(LogLevel.Debug, options.BatchLevel);
        Assert.Equal(LogLevel.Information, options.ShadowViolationLevel);
        Assert.Equal(LogLevel.Warning, options.DefaultBrokenLevel);
        Assert.True(options.IncludeMessage);
    }

    [Theory]
    [InlineData(RuleSeverity.Info, LogLevel.Debug)]
    [InlineData(RuleSeverity.Warning, LogLevel.Information)]
    [InlineData(RuleSeverity.Error, LogLevel.Warning)]
    public void BrokenLevelFor_DefaultMap_ErrorSeverityIsOnlyAWarning(RuleSeverity severity, LogLevel expected)
    {
        LoggingRuleObserverOptions options = new LoggingRuleObserverOptions();

        LogLevel level = options.BrokenLevelFor(severity);

        Assert.Equal(expected, level);
    }

    [Fact]
    public void BrokenLevelFor_SeverityMissingFromMap_UsesDefaultBrokenLevel()
    {
        LoggingRuleObserverOptions options = new LoggingRuleObserverOptions { DefaultBrokenLevel = LogLevel.Critical };
        options.BrokenLevelBySeverity.Remove(RuleSeverity.Error);

        LogLevel level = options.BrokenLevelFor(RuleSeverity.Error);

        Assert.Equal(LogLevel.Critical, level);
    }

    [Fact]
    public void BrokenLevelBySeverity_CollectionInitializer_OverridesDefaults()
    {
        LoggingRuleObserverOptions options = new LoggingRuleObserverOptions
        {
            BrokenLevelBySeverity =
            {
                [RuleSeverity.Error] = LogLevel.Error,
                [RuleSeverity.Info] = LogLevel.None
            }
        };

        Assert.Equal(LogLevel.Error, options.BrokenLevelFor(RuleSeverity.Error));
        Assert.Equal(LogLevel.None, options.BrokenLevelFor(RuleSeverity.Info));
        Assert.Equal(LogLevel.Information, options.BrokenLevelFor(RuleSeverity.Warning));
    }

    [Fact]
    public void RedactedMessage_IsAStableSentinel()
    {
        Assert.Equal("[redacted]", LoggingRuleObserverOptions.RedactedMessage);
    }
}
