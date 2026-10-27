namespace Masterly.BusinessRules.Testing.UnitTests;

public class AsyncRuleBoundaryFinderTests
{
    private const string Code = "LIMIT";
    private const string Message = "Limit exceeded";
    private const decimal Limit = 10_000m;
    private const int AdultAge = 18;
    private const int EndpointProbes = 2;
    private const int BisectionProbesForHundredMillionPoints = 27;

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FindAsync_LimitRule_LocatesAdjacentGridPointsAtTheStep()
    {
        RuleBoundary<decimal> boundary = await RuleBoundaryFinder.FindAsync<decimal>(amount => LimitRule(amount), 0m, 1_000_000m, 0.01m, TestToken);

        Assert.Equal(10_000.00m, boundary.LastPassing);
        Assert.Equal(10_000.01m, boundary.FirstBroken);
        Assert.Equal(Monotonicity.BrokenAbove, boundary.Monotonicity);
        Assert.Equal(0.01m, boundary.Step);
        Assert.InRange(boundary.ProbeCount, EndpointProbes + BisectionProbesForHundredMillionPoints, EndpointProbes + BisectionProbesForHundredMillionPoints + RuleBoundaryFinder.MonotonicitySampleCount);
    }

    [Fact]
    public async Task FindAsync_ProbeCount_EqualsFactoryInvocations()
    {
        int invocations = 0;

        RuleBoundary<int> boundary = await RuleBoundaryFinder.FindAsync<int>(
            age =>
            {
                invocations++;
                return MinimumAgeRule(age);
            },
            0,
            120,
            1,
            TestToken);

        Assert.Equal(invocations, boundary.ProbeCount);
    }

    [Fact]
    public async Task FindAsync_MinimumRule_ReportsBrokenBelow()
    {
        RuleBoundary<int> boundary = await RuleBoundaryFinder.FindAsync<int>(age => MinimumAgeRule(age), 0, 120, 1, TestToken);

        Assert.Equal(AdultAge, boundary.LastPassing);
        Assert.Equal(AdultAge - 1, boundary.FirstBroken);
        Assert.Equal(Monotonicity.BrokenBelow, boundary.Monotonicity);
    }

    [Fact]
    public async Task FindAsync_PassesOneContextToEveryProbe()
    {
        HashSet<BusinessRuleContext> contexts = new HashSet<BusinessRuleContext>(ReferenceEqualityComparer.Instance);

        await RuleBoundaryFinder.FindAsync<int>(
            value => AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message)
                .WhenAsync((ctx, ct) =>
                {
                    contexts.Add(ctx);
                    return Task.FromResult(value >= 5);
                })
                .Build(),
            0,
            10,
            1,
            TestToken);

        Assert.Single(contexts);
    }

    [Fact]
    public async Task FindAsync_TwoPointGrid_NeedsOnlyTheEndpoints()
    {
        RuleBoundary<int> boundary = await RuleBoundaryFinder.FindAsync<int>(value => BrokenFrom(value, 1), 0, 1, 1, TestToken);

        Assert.Equal(0, boundary.LastPassing);
        Assert.Equal(1, boundary.FirstBroken);
        Assert.Equal(EndpointProbes, boundary.ProbeCount);
    }

    [Fact]
    public async Task FindAsync_WhenBrokenAtBothEnds_ThrowsRuleAssertionException()
    {
        RuleAssertionException exception = await Assert.ThrowsAsync<RuleAssertionException>(
            () => RuleBoundaryFinder.FindAsync<int>(value => RuleTestHelper.CreateBrokenAsyncRule(), 0, 10, 1, TestToken));

        Assert.Equal("No boundary found: the rule is broken at both 0 and 10.", exception.Message);
    }

    [Fact]
    public async Task FindAsync_WhenPassingAtBothEnds_ThrowsRuleAssertionException()
    {
        RuleAssertionException exception = await Assert.ThrowsAsync<RuleAssertionException>(
            () => RuleBoundaryFinder.FindAsync<int>(value => RuleTestHelper.CreatePassingAsyncRule(), 0, 10, 1, TestToken));

        Assert.Equal("No boundary found: the rule is passing at both 0 and 10.", exception.Message);
    }

    [Fact]
    public async Task FindAsync_NonMonotoneRule_ThrowsNamingTheContradictingPair()
    {
        RuleAssertionException exception = await Assert.ThrowsAsync<RuleAssertionException>(
            () => RuleBoundaryFinder.FindAsync<int>(value => BrokenInWindowOrAbove(value, 3, 4, 8), 0, 10, 1, TestToken));

        Assert.Equal("The rule is not monotone (BrokenAbove): it is broken at 3 but passing at 7.", exception.Message);
    }

    [Fact]
    public async Task FindAsync_NullFactory_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => RuleBoundaryFinder.FindAsync<int>(null!, 0, 10, 1, TestToken));

        Assert.Equal("factory", exception.ParamName);
    }

    [Fact]
    public async Task FindAsync_FactoryReturningNull_ThrowsInvalidOperationException()
    {
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RuleBoundaryFinder.FindAsync<int>(value => null!, 0, 10, 1, TestToken));

        Assert.Equal("The rule factory returned null for the value 0.", exception.Message);
    }

    [Fact]
    public async Task FindAsync_InvalidStep_ThrowsBeforeBuildingAnyRule()
    {
        int invocations = 0;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => RuleBoundaryFinder.FindAsync<int>(
                value =>
                {
                    invocations++;
                    return BrokenFrom(value, 5);
                },
                0,
                10,
                0,
                TestToken));

        Assert.Equal(0, invocations);
    }

    [Fact]
    public async Task FindAsync_DoubleInput_LocatesBoundaryWithinOneStep()
    {
        const double threshold = 0.5;

        RuleBoundary<double> boundary = await RuleBoundaryFinder.FindAsync<double>(
            value => AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WhenAsync((ctx, ct) => Task.FromResult(value > threshold)).Build(),
            0.0,
            1.0,
            0.001,
            TestToken);

        Assert.True(boundary.LastPassing <= threshold);
        Assert.True(boundary.FirstBroken > threshold);
        Assert.Equal(0.001, boundary.FirstBroken - boundary.LastPassing, 1e-9);
    }

    [Fact]
    public async Task FindAsync_BoundaryAtLowerEdge_ReturnsFirstTwoGridPoints()
    {
        RuleBoundary<int> boundary = await RuleBoundaryFinder.FindAsync<int>(value => BrokenFrom(value, 1), 0, 10, 1, TestToken);

        Assert.Equal(0, boundary.LastPassing);
        Assert.Equal(1, boundary.FirstBroken);
    }

    [Fact]
    public async Task FindAsync_BoundaryAtUpperEdge_ReturnsLastTwoGridPoints()
    {
        RuleBoundary<int> boundary = await RuleBoundaryFinder.FindAsync<int>(value => BrokenFrom(value, 10), 0, 10, 1, TestToken);

        Assert.Equal(9, boundary.LastPassing);
        Assert.Equal(10, boundary.FirstBroken);
    }

    [Fact]
    public async Task FindAsync_NonMonotoneRuleAboveTheBoundary_ThrowsNamingTheContradictingPair()
    {
        RuleAssertionException exception = await Assert.ThrowsAsync<RuleAssertionException>(
            () => RuleBoundaryFinder.FindAsync<int>(value => BrokenFromExcept(value, 2, 9), 0, 10, 1, TestToken));

        Assert.Equal("The rule is not monotone (BrokenAbove): it is passing at 9 but broken at 2.", exception.Message);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(10, 0)]
    public async Task FindAsync_FromNotBelowTo_ThrowsArgumentException(int from, int to)
    {
        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => RuleBoundaryFinder.FindAsync<int>(value => BrokenFrom(value, 5), from, to, 1, TestToken));

        Assert.Equal("from", exception.ParamName);
    }

    [Fact]
    public async Task FindAsync_WhenCancelledBeforeStart_ThrowsWithoutBuildingAnyRule()
    {
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();
        int invocations = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => RuleBoundaryFinder.FindAsync<int>(
                value =>
                {
                    invocations++;
                    return BrokenFrom(value, 5);
                },
                0,
                10,
                1,
                source.Token));

        Assert.Equal(0, invocations);
    }

    [Fact]
    public async Task FindAsync_WhenCancelledByARule_PropagatesCancellation()
    {
        using CancellationTokenSource source = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => RuleBoundaryFinder.FindAsync<int>(
                value => AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message)
                    .WhenAsync((ctx, ct) =>
                    {
                        source.Cancel();
                        ct.ThrowIfCancellationRequested();
                        return Task.FromResult(value >= 5);
                    })
                    .Build(),
                0,
                10,
                1,
                source.Token));
    }

    private static IAsyncBusinessRule LimitRule(decimal amount)
    {
        return AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WhenAsync((ctx, ct) => Task.FromResult(amount > Limit)).Build();
    }

    private static IAsyncBusinessRule MinimumAgeRule(int age)
    {
        return AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WhenAsync((ctx, ct) => Task.FromResult(age < AdultAge)).Build();
    }

    private static IAsyncBusinessRule BrokenFrom(int value, int threshold)
    {
        return AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WhenAsync((ctx, ct) => Task.FromResult(value >= threshold)).Build();
    }

    private static IAsyncBusinessRule BrokenFromExcept(int value, int threshold, int exception)
    {
        return AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WhenAsync((ctx, ct) => Task.FromResult(value >= threshold && value != exception)).Build();
    }

    private static IAsyncBusinessRule BrokenInWindowOrAbove(int value, int windowStart, int windowEnd, int threshold)
    {
        return AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message)
            .WhenAsync((ctx, ct) => Task.FromResult((value >= windowStart && value <= windowEnd) || value >= threshold))
            .Build();
    }
}
