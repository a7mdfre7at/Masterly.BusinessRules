using System.Globalization;
using System.Text.RegularExpressions;
using CsCheck;

namespace Masterly.BusinessRules.Testing.UnitTests;

public class AsyncRulePropertiesTests
{
    private const string Code = "LIMIT";
    private const string Message = "Limit exceeded";
    private const decimal Limit = 10_000m;
    private const int AdultAge = 18;
    private const int Iterations = 300;
    private const int Seed = 42;
    private const string WrittenKey = "written";
    private const string CounterKey = "count";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public void For_NullFactory_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => RuleProperties.For<int>((Func<int, IAsyncBusinessRule>)null!, Gen.Int[0, 10]));

        Assert.Equal("factory", exception.ParamName);
    }

    [Fact]
    public void For_NullGenerator_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => RuleProperties.For<int>(age => MinimumAgeRule(age), null!));

        Assert.Equal("generator", exception.ParamName);
    }

    [Fact]
    public void For_WithAsyncFactory_ReturnsAsyncPropertySet()
    {
        AsyncRuleProperties<int> properties = RuleProperties.For<int>(age => MinimumAgeRule(age), Gen.Int[0, 120]);

        Assert.Equal(0, properties.LawCount);
        Assert.Empty(properties.LawNames);
    }

    [Fact]
    public void Laws_AreFluentAndNamedInOrder()
    {
        AsyncRuleProperties<int> properties = RuleProperties.For<int>(age => MinimumAgeRule(age), Gen.Int[0, 120]);

        AsyncRuleProperties<int> returned = properties.ContextPure().MonotoneIn(Monotonicity.BrokenBelow);

        Assert.Same(properties, returned);
        Assert.Equal(new[] { "ContextPure", "MonotoneIn(BrokenBelow)" }, properties.LawNames);
    }

    [Fact]
    public async Task VerifyAsync_MonotoneBrokenAbove_HoldsForLimitRule()
    {
        await RuleProperties.For<decimal>(amount => LimitRule(amount), Gen.Decimal[0.01m, 100_000m])
            .MonotoneIn(Monotonicity.BrokenAbove)
            .ContextPure()
            .VerifyAsync(iterations: Iterations, seed: Seed, cancellationToken: TestToken);
    }

    [Fact]
    public async Task VerifyAsync_BrokenBelowOnANonMonotoneRule_ReportsBrokenAboveThePassingValue()
    {
        AsyncRuleProperties<int> properties = RuleProperties.For<int>(value => BrokenInWindow(value, 400, 600), Gen.Int[0, 1_000]).MonotoneIn(Monotonicity.BrokenBelow);

        RuleAssertionException exception = await Assert.ThrowsAsync<RuleAssertionException>(() => properties.VerifyAsync(iterations: Iterations, seed: Seed, cancellationToken: TestToken));

        Assert.StartsWith("Property 'MonotoneIn(BrokenBelow)' failed.", exception.Message);
        Match counterExample = Regex.Match(exception.Message, @"broken at (\d+) but passing at (\d+)");
        Assert.True(counterExample.Success, exception.Message);
        Assert.InRange(int.Parse(counterExample.Groups[1].Value, CultureInfo.InvariantCulture), 400, 600);
        Assert.True(int.Parse(counterExample.Groups[2].Value, CultureInfo.InvariantCulture) < 400, exception.Message);
    }

    [Fact]
    public async Task VerifyAsync_MonotoneBrokenBelow_HoldsForMinimumRule()
    {
        await RuleProperties.For<int>(age => MinimumAgeRule(age), Gen.Int[0, 120])
            .MonotoneIn(Monotonicity.BrokenBelow)
            .VerifyAsync(iterations: Iterations, seed: Seed, cancellationToken: TestToken);
    }

    [Fact]
    public async Task VerifyAsync_WrongDirection_ThrowsRuleAssertionExceptionWithCounterExample()
    {
        AsyncRuleProperties<int> properties = RuleProperties.For<int>(age => MinimumAgeRule(age), Gen.Int[0, 120]).MonotoneIn(Monotonicity.BrokenAbove);

        RuleAssertionException exception = await Assert.ThrowsAsync<RuleAssertionException>(() => properties.VerifyAsync(iterations: Iterations, seed: Seed, cancellationToken: TestToken));

        Assert.StartsWith("Property 'MonotoneIn(BrokenAbove)' failed.", exception.Message);
        Assert.Contains("MonotoneIn(BrokenAbove) violated: the rule is broken at", exception.Message);
    }

    [Fact]
    public async Task VerifyAsync_WithSeed_IsDeterministic()
    {
        Func<AsyncRuleProperties<int>> windowed = () => RuleProperties.For<int>(value => BrokenInWindow(value, 40, 60), Gen.Int[0, 1_000]).MonotoneIn(Monotonicity.BrokenAbove);

        RuleAssertionException first = await Assert.ThrowsAsync<RuleAssertionException>(() => windowed().VerifyAsync(iterations: Iterations, seed: Seed, cancellationToken: TestToken));
        RuleAssertionException second = await Assert.ThrowsAsync<RuleAssertionException>(() => windowed().VerifyAsync(iterations: Iterations, seed: Seed, cancellationToken: TestToken));

        Assert.Equal(first.Message, second.Message);
    }

    [Fact]
    public async Task VerifyAsync_NonMonotoneRule_ReportsShrunkCounterExample()
    {
        AsyncRuleProperties<int> properties = RuleProperties.For<int>(value => BrokenInWindow(value, 40, 60), Gen.Int[0, 1_000]).MonotoneIn(Monotonicity.BrokenAbove);

        RuleAssertionException exception = await Assert.ThrowsAsync<RuleAssertionException>(() => properties.VerifyAsync(iterations: Iterations, seed: Seed, cancellationToken: TestToken));

        Match counterExample = Regex.Match(exception.Message, @"broken at (\d+) but passing at (\d+)");
        Assert.True(counterExample.Success, exception.Message);
        Assert.InRange(int.Parse(counterExample.Groups[1].Value, CultureInfo.InvariantCulture), 40, 60);
        Assert.True(int.Parse(counterExample.Groups[2].Value, CultureInfo.InvariantCulture) > 60, exception.Message);
    }

    [Fact]
    public async Task VerifyAsync_ContextPure_HoldsForRuleThatOnlyReads()
    {
        await RuleProperties.For<int>(
                value => AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WhenAsync((ctx, ct) => Task.FromResult(ctx.ContainsKey(CounterKey) && value > 5)).Build(),
                Gen.Int[0, 10])
            .ContextPure()
            .VerifyAsync(iterations: Iterations, seed: Seed, cancellationToken: TestToken);
    }

    [Fact]
    public async Task VerifyAsync_ContextPure_RuleAddingKey_ThrowsNamingTheKey()
    {
        AsyncRuleProperties<int> properties = RuleProperties.For<int>(
                value => AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WhenAsync((ctx, ct) =>
                {
                    ctx.Set(WrittenKey, value);
                    return Task.FromResult(false);
                }).Build(),
                Gen.Int[0, 10])
            .ContextPure();

        RuleAssertionException exception = await Assert.ThrowsAsync<RuleAssertionException>(() => properties.VerifyAsync(iterations: Iterations, seed: Seed, cancellationToken: TestToken));

        Assert.StartsWith("Property 'ContextPure' failed.", exception.Message);
        Assert.Contains($"Added: {WrittenKey}.", exception.Message);
    }

    [Fact]
    public async Task VerifyAsync_ContextPure_RuleChangingKey_ThrowsNamingTheKey()
    {
        AsyncRuleProperties<int> properties = RuleProperties.For<int>(
                value => AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WhenAsync((ctx, ct) =>
                {
                    ctx.Set(CounterKey, ctx.Get<int>(CounterKey) + 1);
                    return Task.FromResult(false);
                }).Build(),
                Gen.Int[0, 10])
            .ContextPure(() => RuleTestHelper.CreateContext((CounterKey, 0)));

        RuleAssertionException exception = await Assert.ThrowsAsync<RuleAssertionException>(() => properties.VerifyAsync(iterations: Iterations, seed: Seed, cancellationToken: TestToken));

        Assert.Contains($"Changed: {CounterKey}.", exception.Message);
    }

    [Fact]
    public void ContextPure_NullContextFactory_ThrowsArgumentNullException()
    {
        AsyncRuleProperties<int> properties = RuleProperties.For<int>(age => MinimumAgeRule(age), Gen.Int[0, 120]);

        Assert.Throws<ArgumentNullException>(() => properties.ContextPure(null!));
    }

    [Fact]
    public async Task VerifyAsync_ContextFactoryReturningNull_ThrowsInvalidOperationException()
    {
        AsyncRuleProperties<int> properties = RuleProperties.For<int>(age => MinimumAgeRule(age), Gen.Int[0, 120]).ContextPure(() => null!);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => properties.VerifyAsync(iterations: Iterations, seed: Seed, cancellationToken: TestToken));

        Assert.Equal("The context factory returned null.", exception.Message);
    }

    [Fact]
    public async Task VerifyAsync_RuleFactoryReturningNull_ThrowsInvalidOperationException()
    {
        AsyncRuleProperties<int> properties = RuleProperties.For<int>(age => (IAsyncBusinessRule)null!, Gen.Int[0, 120]).MonotoneIn(Monotonicity.BrokenBelow);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => properties.VerifyAsync(iterations: Iterations, seed: Seed, cancellationToken: TestToken));

        Assert.StartsWith("The rule factory returned null for the value", exception.Message);
    }

    [Fact]
    public async Task VerifyAsync_GeneratorThatCannotProduceAnInput_ThrowsInvalidOperationExceptionNotAFailure()
    {
        AsyncRuleProperties<int> properties = RuleProperties.For<int>(age => MinimumAgeRule(age), Gen.Int[0, 120].Where(age => age < 0)).MonotoneIn(Monotonicity.BrokenBelow);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => properties.VerifyAsync(iterations: Iterations, seed: Seed, cancellationToken: TestToken));

        Assert.StartsWith("The generator could not produce an input:", exception.Message);
        Assert.IsType<CsCheckException>(exception.InnerException);
    }

    [Fact]
    public async Task VerifyAsync_WithSeed_FailureMessageNamesTheSeedAndTheCounterExample()
    {
        AsyncRuleProperties<int> properties = RuleProperties.For<int>(value => BrokenInWindow(value, 40, 60), Gen.Int[0, 1_000]).MonotoneIn(Monotonicity.BrokenAbove);

        RuleAssertionException exception = await Assert.ThrowsAsync<RuleAssertionException>(() => properties.VerifyAsync(iterations: Iterations, seed: Seed, cancellationToken: TestToken));

        Assert.Contains($"(seed {Seed},", exception.Message);
        Assert.Contains($"pass seed: {Seed} to reproduce.", exception.Message);
        Assert.Matches(@"Counter-example: \(\d+, \d+\)", exception.Message);
    }

    [Fact]
    public async Task VerifyAsync_SameSeed_SamplesTheSameInputs()
    {
        List<int> firstRun = new List<int>();
        List<int> secondRun = new List<int>();

        await RuleProperties.For<int>(value => RecordingRule(firstRun, value), Gen.Int[0, 1_000_000]).ContextPure().VerifyAsync(iterations: Iterations, seed: Seed, cancellationToken: TestToken);
        await RuleProperties.For<int>(value => RecordingRule(secondRun, value), Gen.Int[0, 1_000_000]).ContextPure().VerifyAsync(iterations: Iterations, seed: Seed, cancellationToken: TestToken);

        Assert.Equal(firstRun, secondRun);
        Assert.Equal(Iterations, firstRun.Count);
    }

    [Fact]
    public async Task VerifyAsync_RuleThatThrows_IsReportedAsFailureWithTheFault()
    {
        AsyncRuleProperties<int> properties = RuleProperties.For<int>(
                value => AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message)
                    .WhenAsync((ctx, ct) => value > 5 ? throw new InvalidOperationException("boom") : Task.FromResult(false)).Build(),
                Gen.Int[0, 10])
            .MonotoneIn(Monotonicity.BrokenAbove);

        RuleAssertionException exception = await Assert.ThrowsAsync<RuleAssertionException>(() => properties.VerifyAsync(iterations: Iterations, seed: Seed, cancellationToken: TestToken));

        Assert.Contains("boom", exception.Message);
    }

    [Fact]
    public async Task VerifyAsync_WithoutLaws_ThrowsInvalidOperationException()
    {
        AsyncRuleProperties<int> properties = RuleProperties.For<int>(age => MinimumAgeRule(age), Gen.Int[0, 120]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => properties.VerifyAsync(cancellationToken: TestToken));
    }

    [Fact]
    public async Task VerifyAsync_NonPositiveIterations_ThrowsArgumentOutOfRangeException()
    {
        AsyncRuleProperties<int> properties = RuleProperties.For<int>(age => MinimumAgeRule(age), Gen.Int[0, 120]).MonotoneIn(Monotonicity.BrokenBelow);

        ArgumentOutOfRangeException exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => properties.VerifyAsync(0, cancellationToken: TestToken));

        Assert.Equal("iterations", exception.ParamName);
    }

    [Fact]
    public async Task VerifyAsync_WhenCancelledBeforeStart_ThrowsWithoutBuildingAnyRule()
    {
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();
        int invocations = 0;
        AsyncRuleProperties<int> properties = RuleProperties.For<int>(
                age =>
                {
                    invocations++;
                    return MinimumAgeRule(age);
                },
                Gen.Int[0, 120])
            .MonotoneIn(Monotonicity.BrokenBelow);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => properties.VerifyAsync(iterations: Iterations, seed: Seed, cancellationToken: source.Token));

        Assert.Equal(0, invocations);
    }

    [Fact]
    public async Task VerifyAsync_WhenCancelledByARule_PropagatesCancellationNotAFailure()
    {
        using CancellationTokenSource source = new CancellationTokenSource();
        AsyncRuleProperties<int> properties = RuleProperties.For<int>(
                value => AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message)
                    .WhenAsync((ctx, ct) =>
                    {
                        source.Cancel();
                        ct.ThrowIfCancellationRequested();
                        return Task.FromResult(value < AdultAge);
                    })
                    .Build(),
                Gen.Int[0, 120])
            .MonotoneIn(Monotonicity.BrokenBelow);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => properties.VerifyAsync(iterations: Iterations, seed: Seed, cancellationToken: source.Token));
    }

    [Fact]
    public void MonotoneIn_UndefinedMonotonicity_ThrowsArgumentOutOfRangeException()
    {
        AsyncRuleProperties<int> properties = RuleProperties.For<int>(age => MinimumAgeRule(age), Gen.Int[0, 120]);

        Assert.Throws<ArgumentOutOfRangeException>(() => properties.MonotoneIn((Monotonicity)42));
    }

    private static IAsyncBusinessRule LimitRule(decimal amount)
    {
        return AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WhenAsync((ctx, ct) => Task.FromResult(amount > Limit)).Build();
    }

    private static IAsyncBusinessRule MinimumAgeRule(int age)
    {
        return AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WhenAsync((ctx, ct) => Task.FromResult(age < AdultAge)).Build();
    }

    private static IAsyncBusinessRule RecordingRule(List<int> seen, int value)
    {
        seen.Add(value);
        return MinimumAgeRule(value);
    }

    private static IAsyncBusinessRule BrokenInWindow(int value, int windowStart, int windowEnd)
    {
        return AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WhenAsync((ctx, ct) => Task.FromResult(value >= windowStart && value <= windowEnd)).Build();
    }
}
