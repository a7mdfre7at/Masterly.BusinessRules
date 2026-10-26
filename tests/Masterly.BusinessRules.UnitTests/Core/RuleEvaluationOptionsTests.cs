namespace Masterly.BusinessRules.UnitTests;

public class RuleEvaluationOptionsTests
{
    [Fact]
    public void RuleEvaluationOptions_Defaults_MatchLegacyCheckerBehaviour()
    {
        RuleEvaluationOptions options = new RuleEvaluationOptions();

        Assert.False(options.StopOnFirstFailure);
        Assert.Equal(FaultPolicy.Throw, options.OnFault);
        Assert.Equal(RuleSeverity.Info, options.ThrowOn);
        Assert.Null(options.Observer);
    }

    [Fact]
    public void AsyncRuleEvaluationOptions_Defaults_MatchLegacyCheckerBehaviour()
    {
        AsyncRuleEvaluationOptions options = new AsyncRuleEvaluationOptions();

        Assert.False(options.StopOnFirstFailure);
        Assert.False(options.RunInParallel);
        Assert.Equal(FaultPolicy.Throw, options.OnFault);
        Assert.Equal(RuleSeverity.Info, options.ThrowOn);
        Assert.Null(options.Observer);
        Assert.False(options.EffectiveParallel);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void AsyncRuleEvaluationOptions_EffectiveParallel_FailFastWinsOverParallel(bool runInParallel, bool stopOnFirstFailure, bool expected)
    {
        AsyncRuleEvaluationOptions options = new AsyncRuleEvaluationOptions
        {
            RunInParallel = runInParallel,
            StopOnFirstFailure = stopOnFirstFailure
        };

        Assert.Equal(expected, options.EffectiveParallel);
    }

    [Fact]
    public void FaultPolicy_HasNoTreatAsPassedMember()
    {
        string[] names = Enum.GetNames(typeof(FaultPolicy));

        Assert.Equal(new[] { nameof(FaultPolicy.Throw), nameof(FaultPolicy.Record), nameof(FaultPolicy.TreatAsBroken) }, names);
    }
}
