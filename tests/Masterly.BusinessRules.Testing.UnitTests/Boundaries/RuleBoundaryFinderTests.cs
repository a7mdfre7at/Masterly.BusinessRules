namespace Masterly.BusinessRules.Testing.UnitTests;

public class RuleBoundaryFinderTests
{
    private const string Code = "LIMIT";
    private const string Message = "Limit exceeded";
    private const decimal Limit = 10_000m;
    private const int AdultAge = 18;
    private const int EndpointProbes = 2;
    private const int BisectionProbesForHundredMillionPoints = 27;

    [Fact]
    public void Find_LimitRule_LocatesAdjacentGridPointsAtTheStep()
    {
        RuleBoundary<decimal> boundary = RuleBoundaryFinder.Find<decimal>(amount => LimitRule(amount), from: 0m, to: 1_000_000m, step: 0.01m);

        Assert.Equal(10_000.00m, boundary.LastPassing);
        Assert.Equal(10_000.01m, boundary.FirstBroken);
        Assert.Equal(Monotonicity.BrokenAbove, boundary.Monotonicity);
        Assert.Equal(0.01m, boundary.Step);
        Assert.InRange(boundary.ProbeCount, EndpointProbes + BisectionProbesForHundredMillionPoints, EndpointProbes + BisectionProbesForHundredMillionPoints + RuleBoundaryFinder.MonotonicitySampleCount);
    }

    [Fact]
    public void Find_ProbeCount_EqualsFactoryInvocations()
    {
        int invocations = 0;

        RuleBoundary<int> boundary = RuleBoundaryFinder.Find<int>(
            age =>
            {
                invocations++;
                return MinimumAgeRule(age);
            },
            from: 0,
            to: 120,
            step: 1);

        Assert.Equal(invocations, boundary.ProbeCount);
    }

    [Fact]
    public void Find_MinimumRule_ReportsBrokenBelow()
    {
        RuleBoundary<int> boundary = RuleBoundaryFinder.Find<int>(age => MinimumAgeRule(age), from: 0, to: 120, step: 1);

        Assert.Equal(AdultAge, boundary.LastPassing);
        Assert.Equal(AdultAge - 1, boundary.FirstBroken);
        Assert.Equal(Monotonicity.BrokenBelow, boundary.Monotonicity);
        Assert.Equal($"breaks up to 17, passes from 18 ({boundary.ProbeCount} probes)", boundary.ToString());
    }

    [Fact]
    public void Find_LimitRule_ToStringDescribesBoundary()
    {
        RuleBoundary<decimal> boundary = RuleBoundaryFinder.Find<decimal>(amount => LimitRule(amount), 0m, 20_000m, 0.5m);

        Assert.Equal($"passes up to 10000.0, breaks from 10000.5 ({boundary.ProbeCount} probes)", boundary.ToString());
    }

    [Fact]
    public void Find_DoubleInput_LocatesBoundaryWithinOneStep()
    {
        const double threshold = 0.5;

        RuleBoundary<double> boundary = RuleBoundaryFinder.Find<double>(
            value => BusinessRuleBuilder.Create(Code).WithMessage(Message).When(() => value > threshold).Build(),
            from: 0.0,
            to: 1.0,
            step: 0.001);

        Assert.True(boundary.LastPassing <= threshold);
        Assert.True(boundary.FirstBroken > threshold);
        Assert.Equal(0.001, boundary.FirstBroken - boundary.LastPassing, 1e-9);
    }

    [Fact]
    public void Find_BoundaryAtLowerEdge_ReturnsFirstTwoGridPoints()
    {
        RuleBoundary<int> boundary = RuleBoundaryFinder.Find<int>(value => BrokenFrom(value, 1), from: 0, to: 10, step: 1);

        Assert.Equal(0, boundary.LastPassing);
        Assert.Equal(1, boundary.FirstBroken);
    }

    [Fact]
    public void Find_BoundaryAtUpperEdge_ReturnsLastTwoGridPoints()
    {
        RuleBoundary<int> boundary = RuleBoundaryFinder.Find<int>(value => BrokenFrom(value, 10), from: 0, to: 10, step: 1);

        Assert.Equal(9, boundary.LastPassing);
        Assert.Equal(10, boundary.FirstBroken);
    }

    [Fact]
    public void Find_TwoPointGrid_NeedsOnlyTheEndpoints()
    {
        RuleBoundary<int> boundary = RuleBoundaryFinder.Find<int>(value => BrokenFrom(value, 1), from: 0, to: 1, step: 1);

        Assert.Equal(0, boundary.LastPassing);
        Assert.Equal(1, boundary.FirstBroken);
        Assert.Equal(EndpointProbes, boundary.ProbeCount);
    }

    [Fact]
    public void Find_StepNotDividingRange_StaysInsideTheRange()
    {
        RuleBoundary<int> boundary = RuleBoundaryFinder.Find<int>(value => BrokenFrom(value, 7), from: 0, to: 10, step: 3);

        Assert.Equal(6, boundary.LastPassing);
        Assert.Equal(9, boundary.FirstBroken);
    }

    [Fact]
    public void Find_WhenBrokenAtBothEnds_ThrowsRuleAssertionException()
    {
        RuleAssertionException exception = Assert.Throws<RuleAssertionException>(
            () => RuleBoundaryFinder.Find<int>(value => RuleTestHelper.CreateBrokenRule(), from: 0, to: 10, step: 1));

        Assert.Equal("No boundary found: the rule is broken at both 0 and 10.", exception.Message);
    }

    [Fact]
    public void Find_WhenPassingAtBothEnds_ThrowsRuleAssertionException()
    {
        RuleAssertionException exception = Assert.Throws<RuleAssertionException>(
            () => RuleBoundaryFinder.Find<decimal>(value => RuleTestHelper.CreatePassingRule(), from: 0m, to: 2.5m, step: 0.5m));

        Assert.Equal("No boundary found: the rule is passing at both 0 and 2.5.", exception.Message);
    }

    [Fact]
    public void Find_NonMonotoneRule_ThrowsNamingTheContradictingPair()
    {
        RuleAssertionException exception = Assert.Throws<RuleAssertionException>(
            () => RuleBoundaryFinder.Find<int>(value => BrokenInWindowOrAbove(value, 3, 4, 8), from: 0, to: 10, step: 1));

        Assert.Equal("The rule is not monotone (BrokenAbove): it is broken at 3 but passing at 7.", exception.Message);
    }

    [Fact]
    public void Find_NonMonotoneRuleAboveTheBoundary_ThrowsNamingTheContradictingPair()
    {
        RuleAssertionException exception = Assert.Throws<RuleAssertionException>(
            () => RuleBoundaryFinder.Find<int>(value => BrokenFromExcept(value, 2, 9), from: 0, to: 10, step: 1));

        Assert.Equal("The rule is not monotone (BrokenAbove): it is passing at 9 but broken at 2.", exception.Message);
    }

    [Fact]
    public void Find_NullFactory_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => RuleBoundaryFinder.Find<int>(null!, 0, 10, 1));

        Assert.Equal("factory", exception.ParamName);
    }

    [Fact]
    public void Find_FactoryReturningNull_ThrowsInvalidOperationException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => RuleBoundaryFinder.Find<int>(value => null!, 0, 10, 1));

        Assert.Equal("The rule factory returned null for the value 0.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Find_NonPositiveStep_ThrowsArgumentOutOfRangeException(int step)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => RuleBoundaryFinder.Find<int>(value => BrokenFrom(value, 5), 0, 10, step));

        Assert.Equal("step", exception.ParamName);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(11, 10)]
    public void Find_FromNotBelowTo_ThrowsArgumentException(int from, int to)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => RuleBoundaryFinder.Find<int>(value => BrokenFrom(value, 5), from, to, 1));

        Assert.Equal("from", exception.ParamName);
    }

    [Fact]
    public void Find_StepLargerThanRange_ThrowsArgumentException()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => RuleBoundaryFinder.Find<int>(value => BrokenFrom(value, 5), 0, 10, 11));

        Assert.Equal("step", exception.ParamName);
        Assert.StartsWith("The range holds a single grid point", exception.Message);
    }

    [Theory]
    [InlineData(double.NaN, 1.0, 0.1, "from")]
    [InlineData(0.0, double.PositiveInfinity, 0.1, "to")]
    [InlineData(0.0, 1.0, double.NaN, "step")]
    public void Find_NonFiniteBound_ThrowsArgumentException(double from, double to, double step, string parameter)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => RuleBoundaryFinder.Find<double>(value => BrokenFrom(value, 0.5), from, to, step));

        Assert.Equal(parameter, exception.ParamName);
    }

    [Fact]
    public void Find_StepTooSmallToMoveFrom_ThrowsArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => RuleBoundaryFinder.Find<double>(value => BrokenFrom(value, 2.0), 1.0, 3.0, 1e-20));

        Assert.Equal("step", exception.ParamName);
        Assert.StartsWith("The step is too small", exception.Message);
    }

    [Fact]
    public void Find_TooManyGridPoints_ThrowsArgumentException()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => RuleBoundaryFinder.Find<double>(value => BrokenFrom(value, 0.5), 0.0, 1e30, 1e-10));

        Assert.Equal("step", exception.ParamName);
        Assert.StartsWith("The range holds too many grid points", exception.Message);
    }

    [Fact]
    public void RuleBoundary_Constructor_GuardsArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuleBoundary<int>(1, 2, Monotonicity.BrokenAbove, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuleBoundary<int>(1, 2, (Monotonicity)42, 1, 1));
    }

    private static IBusinessRule LimitRule(decimal amount)
    {
        return BusinessRuleBuilder.Create(Code).WithMessage(Message).When(() => amount > Limit).Build();
    }

    private static IBusinessRule MinimumAgeRule(int age)
    {
        return BusinessRuleBuilder.Create(Code).WithMessage(Message).When(() => age < AdultAge).Build();
    }

    private static IBusinessRule BrokenFrom<T>(T value, T threshold) where T : IComparable<T>
    {
        return BusinessRuleBuilder.Create(Code).WithMessage(Message).When(() => value.CompareTo(threshold) >= 0).Build();
    }

    private static IBusinessRule BrokenFromExcept(int value, int threshold, int exception)
    {
        return BusinessRuleBuilder.Create(Code).WithMessage(Message).When(() => value >= threshold && value != exception).Build();
    }

    private static IBusinessRule BrokenInWindowOrAbove(int value, int windowStart, int windowEnd, int threshold)
    {
        return BusinessRuleBuilder.Create(Code).WithMessage(Message)
            .When(() => (value >= windowStart && value <= windowEnd) || value >= threshold)
            .Build();
    }
}
