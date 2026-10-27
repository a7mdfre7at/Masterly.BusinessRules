using System.Globalization;
using System.Text.RegularExpressions;
using CsCheck;

namespace Masterly.BusinessRules.Testing.UnitTests;

public class RulePropertiesTests
{
    private const string Code = "LIMIT";
    private const string Message = "Limit exceeded";
    private const decimal Limit = 10_000m;
    private const int AdultAge = 18;
    private const int Iterations = 300;
    private const int Seed = 42;
    private const string WrittenKey = "written";
    private const string CounterKey = "count";

    [Fact]
    public void For_NullFactory_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => RuleProperties.For<int>((Func<int, IBusinessRule>)null!, Gen.Int[0, 10]));

        Assert.Equal("factory", exception.ParamName);
    }

    [Fact]
    public void For_NullGenerator_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => RuleProperties.For<int>(age => MinimumAgeRule(age), null!));

        Assert.Equal("generator", exception.ParamName);
    }

    [Fact]
    public void For_ReturnsEmptyPropertySet()
    {
        RuleProperties<int> properties = RuleProperties.For<int>(age => MinimumAgeRule(age), Gen.Int[0, 120]);

        Assert.Equal(0, properties.LawCount);
        Assert.Empty(properties.LawNames);
    }

    [Fact]
    public void Laws_AreFluentAndNamedInOrder()
    {
        RuleProperties<int> properties = RuleProperties.For<int>(age => MinimumAgeRule(age), Gen.Int[0, 120]);

        RuleProperties<int> returned = properties.MonotoneIn(Monotonicity.BrokenBelow).ContextPure();

        Assert.Same(properties, returned);
        Assert.Equal(2, properties.LawCount);
        Assert.Equal(new[] { "MonotoneIn(BrokenBelow)", "ContextPure" }, properties.LawNames);
    }

    [Fact]
    public void Verify_MonotoneBrokenAbove_HoldsForLimitRule()
    {
        RuleProperties.For<decimal>(amount => LimitRule(amount), Gen.Decimal[0.01m, 100_000m])
            .MonotoneIn(Monotonicity.BrokenAbove)
            .ContextPure()
            .Verify(iterations: Iterations, seed: Seed);
    }

    [Fact]
    public void Verify_MonotoneBrokenBelow_HoldsForMinimumRule()
    {
        RuleProperties.For<int>(age => MinimumAgeRule(age), Gen.Int[0, 120])
            .MonotoneIn(Monotonicity.BrokenBelow)
            .Verify(iterations: Iterations, seed: Seed);
    }

    [Fact]
    public void Verify_WrongDirection_ThrowsRuleAssertionExceptionWithCounterExample()
    {
        RuleProperties<int> properties = RuleProperties.For<int>(age => MinimumAgeRule(age), Gen.Int[0, 120]).MonotoneIn(Monotonicity.BrokenAbove);

        RuleAssertionException exception = Assert.Throws<RuleAssertionException>(() => properties.Verify(iterations: Iterations, seed: Seed));

        Assert.StartsWith("Property 'MonotoneIn(BrokenAbove)' failed.", exception.Message);
        Assert.Contains("MonotoneIn(BrokenAbove) violated: the rule is broken at", exception.Message);
        Assert.Contains("but passing at", exception.Message);
    }

    [Fact]
    public void Verify_WithSeed_IsDeterministic()
    {
        Func<RuleProperties<int>> windowed = () => RuleProperties.For<int>(value => BrokenInWindow(value, 40, 60), Gen.Int[0, 1_000]).MonotoneIn(Monotonicity.BrokenAbove);

        RuleAssertionException first = Assert.Throws<RuleAssertionException>(() => windowed().Verify(iterations: Iterations, seed: Seed));
        RuleAssertionException second = Assert.Throws<RuleAssertionException>(() => windowed().Verify(iterations: Iterations, seed: Seed));

        Assert.Equal(first.Message, second.Message);
    }

    [Fact]
    public void Verify_NonMonotoneRule_ReportsShrunkCounterExample()
    {
        RuleProperties<int> properties = RuleProperties.For<int>(value => BrokenInWindow(value, 40, 60), Gen.Int[0, 1_000]).MonotoneIn(Monotonicity.BrokenAbove);

        RuleAssertionException exception = Assert.Throws<RuleAssertionException>(() => properties.Verify(iterations: Iterations, seed: Seed));

        Match counterExample = Regex.Match(exception.Message, @"broken at (\d+) but passing at (\d+)");
        Assert.True(counterExample.Success, exception.Message);
        Assert.InRange(int.Parse(counterExample.Groups[1].Value, CultureInfo.InvariantCulture), 40, 60);
        Assert.True(int.Parse(counterExample.Groups[2].Value, CultureInfo.InvariantCulture) > 60, exception.Message);
    }

    [Fact]
    public void Verify_WithoutSeed_ReportsTheSeedToReproduce()
    {
        RuleProperties<int> properties = RuleProperties.For<int>(value => BrokenInWindow(value, 40, 60), Gen.Int[0, 1_000]).MonotoneIn(Monotonicity.BrokenAbove);

        RuleAssertionException exception = Assert.Throws<RuleAssertionException>(() => properties.Verify(iterations: Iterations));

        Assert.Contains("seed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verify_ContextPure_HoldsForRuleThatOnlyReads()
    {
        RuleProperties.For<int>(
                value => BusinessRuleBuilder.Create(Code).WithMessage(Message).When(ctx => ctx.ContainsKey(CounterKey) && value > 5).Build(),
                Gen.Int[0, 10])
            .ContextPure()
            .Verify(iterations: Iterations, seed: Seed);
    }

    [Fact]
    public void Verify_ContextPure_RuleAddingKey_ThrowsNamingTheKey()
    {
        RuleProperties<int> properties = RuleProperties.For<int>(
                value => BusinessRuleBuilder.Create(Code).WithMessage(Message).When(ctx =>
                {
                    ctx.Set(WrittenKey, value);
                    return false;
                }).Build(),
                Gen.Int[0, 10])
            .ContextPure();

        RuleAssertionException exception = Assert.Throws<RuleAssertionException>(() => properties.Verify(iterations: Iterations, seed: Seed));

        Assert.StartsWith("Property 'ContextPure' failed.", exception.Message);
        Assert.Contains("ContextPure violated: evaluating the rule for", exception.Message);
        Assert.Contains($"Added: {WrittenKey}.", exception.Message);
    }

    [Fact]
    public void Verify_ContextPure_RuleChangingKey_ThrowsNamingTheKey()
    {
        RuleProperties<int> properties = RuleProperties.For<int>(
                value => BusinessRuleBuilder.Create(Code).WithMessage(Message).When(ctx =>
                {
                    ctx.Set(CounterKey, ctx.Get<int>(CounterKey) + 1);
                    return false;
                }).Build(),
                Gen.Int[0, 10])
            .ContextPure(() => RuleTestHelper.CreateContext((CounterKey, 0)));

        RuleAssertionException exception = Assert.Throws<RuleAssertionException>(() => properties.Verify(iterations: Iterations, seed: Seed));

        Assert.Contains($"Changed: {CounterKey}.", exception.Message);
    }

    [Fact]
    public void Verify_ContextPure_RuleRemovingKey_ThrowsNamingTheKey()
    {
        RuleProperties<int> properties = RuleProperties.For<int>(
                value => BusinessRuleBuilder.Create(Code).WithMessage(Message).When(ctx => ctx.Remove(CounterKey)).Build(),
                Gen.Int[0, 10])
            .ContextPure(() => RuleTestHelper.CreateContext((CounterKey, 0)));

        RuleAssertionException exception = Assert.Throws<RuleAssertionException>(() => properties.Verify(iterations: Iterations, seed: Seed));

        Assert.Contains($"Removed: {CounterKey}.", exception.Message);
    }

    [Fact]
    public void Verify_ContextPure_RewritingTheSameValue_Holds()
    {
        RuleProperties.For<int>(
                value => BusinessRuleBuilder.Create(Code).WithMessage(Message).When(ctx =>
                {
                    ctx.Set(CounterKey, ctx.Get<int>(CounterKey));
                    return false;
                }).Build(),
                Gen.Int[0, 10])
            .ContextPure(() => RuleTestHelper.CreateContext((CounterKey, 7)))
            .Verify(iterations: Iterations, seed: Seed);
    }

    [Fact]
    public void ContextPure_NullContextFactory_ThrowsArgumentNullException()
    {
        RuleProperties<int> properties = RuleProperties.For<int>(age => MinimumAgeRule(age), Gen.Int[0, 120]);

        Assert.Throws<ArgumentNullException>(() => properties.ContextPure(null!));
    }

    [Fact]
    public void Verify_ContextFactoryReturningNull_ThrowsInvalidOperationException()
    {
        RuleProperties<int> properties = RuleProperties.For<int>(age => MinimumAgeRule(age), Gen.Int[0, 120]).ContextPure(() => null!);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => properties.Verify(iterations: Iterations, seed: Seed));

        Assert.Equal("The context factory returned null.", exception.Message);
    }

    [Fact]
    public void Verify_RuleFactoryReturningNull_ThrowsInvalidOperationException()
    {
        RuleProperties<int> properties = RuleProperties.For<int>(age => (IBusinessRule)null!, Gen.Int[0, 120]).MonotoneIn(Monotonicity.BrokenBelow);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => properties.Verify(iterations: Iterations, seed: Seed));

        Assert.StartsWith("The rule factory returned null for the value", exception.Message);
    }

    [Fact]
    public void Verify_GeneratorThatCannotProduceAnInput_ThrowsInvalidOperationExceptionNotAFailure()
    {
        RuleProperties<int> properties = RuleProperties.For<int>(age => MinimumAgeRule(age), Gen.Int[0, 120].Where(age => age < 0)).MonotoneIn(Monotonicity.BrokenBelow);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => properties.Verify(iterations: Iterations, seed: Seed));

        Assert.StartsWith("The generator could not produce an input:", exception.Message);
        Assert.IsType<CsCheckException>(exception.InnerException);
    }

    [Fact]
    public void Verify_WithSeed_FailureMessageNamesTheSeedAndTheCounterExample()
    {
        RuleProperties<int> properties = RuleProperties.For<int>(value => BrokenInWindow(value, 40, 60), Gen.Int[0, 1_000]).MonotoneIn(Monotonicity.BrokenAbove);

        RuleAssertionException exception = Assert.Throws<RuleAssertionException>(() => properties.Verify(iterations: Iterations, seed: Seed));

        Assert.Contains($"(seed {Seed},", exception.Message);
        Assert.Contains($"pass seed: {Seed} to reproduce.", exception.Message);
        Assert.Matches(@"Counter-example: \(\d+, \d+\)", exception.Message);
    }

    [Fact]
    public void Verify_DifferentSeeds_SampleDifferentInputs()
    {
        List<int> firstRun = new List<int>();
        List<int> secondRun = new List<int>();

        RuleProperties.For<int>(value => RecordingRule(firstRun, value), Gen.Int[0, 1_000_000]).ContextPure().Verify(iterations: Iterations, seed: Seed);
        RuleProperties.For<int>(value => RecordingRule(secondRun, value), Gen.Int[0, 1_000_000]).ContextPure().Verify(iterations: Iterations, seed: Seed + 1);

        Assert.NotEqual(firstRun, secondRun);
    }

    [Fact]
    public void Verify_SameSeed_SamplesTheSameInputs()
    {
        List<int> firstRun = new List<int>();
        List<int> secondRun = new List<int>();

        RuleProperties.For<int>(value => RecordingRule(firstRun, value), Gen.Int[0, 1_000_000]).ContextPure().Verify(iterations: Iterations, seed: Seed);
        RuleProperties.For<int>(value => RecordingRule(secondRun, value), Gen.Int[0, 1_000_000]).ContextPure().Verify(iterations: Iterations, seed: Seed);

        Assert.Equal(firstRun, secondRun);
        Assert.Equal(Iterations, firstRun.Count);
    }

    [Fact]
    public void Verify_RuleThatThrows_IsReportedAsFailureWithTheFault()
    {
        RuleProperties<int> properties = RuleProperties.For<int>(
                value => BusinessRuleBuilder.Create(Code).WithMessage(Message).When(() => value > 5 ? throw new InvalidOperationException("boom") : false).Build(),
                Gen.Int[0, 10])
            .MonotoneIn(Monotonicity.BrokenAbove);

        RuleAssertionException exception = Assert.Throws<RuleAssertionException>(() => properties.Verify(iterations: Iterations, seed: Seed));

        Assert.Contains("boom", exception.Message);
    }

    [Fact]
    public void Verify_WithoutLaws_ThrowsInvalidOperationException()
    {
        RuleProperties<int> properties = RuleProperties.For<int>(age => MinimumAgeRule(age), Gen.Int[0, 120]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => properties.Verify());

        Assert.Equal("No property was added; call MonotoneIn or ContextPure before Verify.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Verify_NonPositiveIterations_ThrowsArgumentOutOfRangeException(int iterations)
    {
        RuleProperties<int> properties = RuleProperties.For<int>(age => MinimumAgeRule(age), Gen.Int[0, 120]).MonotoneIn(Monotonicity.BrokenBelow);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => properties.Verify(iterations));

        Assert.Equal("iterations", exception.ParamName);
    }

    [Fact]
    public void Verify_DefaultIterations_IsOneHundred()
    {
        int invocations = 0;

        RuleProperties.For<int>(
                age =>
                {
                    invocations++;
                    return MinimumAgeRule(age);
                },
                Gen.Int[0, 120])
            .MonotoneIn(Monotonicity.BrokenBelow)
            .Verify(seed: Seed);

        Assert.Equal(100, RuleProperties.DefaultIterations);
        Assert.Equal(RuleProperties.DefaultIterations * 2, invocations);
    }

    [Fact]
    public void MonotoneIn_UndefinedMonotonicity_ThrowsArgumentOutOfRangeException()
    {
        RuleProperties<int> properties = RuleProperties.For<int>(age => MinimumAgeRule(age), Gen.Int[0, 120]);

        Assert.Throws<ArgumentOutOfRangeException>(() => properties.MonotoneIn((Monotonicity)42));
    }

    [Fact]
    public void MonotoneIn_NonComparableInputWithoutComparer_ThrowsInvalidOperationException()
    {
        RuleProperties<Order> properties = RuleProperties.For<Order>(order => LimitRule(order.Amount), OrderGenerator());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => properties.MonotoneIn(Monotonicity.BrokenAbove));

        Assert.Equal("Order is not comparable; pass an IComparer to MonotoneIn.", exception.Message);
    }

    [Fact]
    public void MonotoneIn_NonComparableInputWithComparer_Verifies()
    {
        RuleProperties.For<Order>(order => LimitRule(order.Amount), OrderGenerator())
            .MonotoneIn(Monotonicity.BrokenAbove, Comparer<Order>.Create((left, right) => left.Amount.CompareTo(right.Amount)))
            .ContextPure()
            .Verify(iterations: Iterations, seed: Seed);
    }

    private static Gen<Order> OrderGenerator()
    {
        return Gen.Select(Gen.Decimal[0.01m, 100_000m], amount => new Order(amount));
    }

    private static IBusinessRule LimitRule(decimal amount)
    {
        return BusinessRuleBuilder.Create(Code).WithMessage(Message).When(() => amount > Limit).Build();
    }

    private static IBusinessRule MinimumAgeRule(int age)
    {
        return BusinessRuleBuilder.Create(Code).WithMessage(Message).When(() => age < AdultAge).Build();
    }

    private static IBusinessRule RecordingRule(List<int> seen, int value)
    {
        seen.Add(value);
        return MinimumAgeRule(value);
    }

    private static IBusinessRule BrokenInWindow(int value, int windowStart, int windowEnd)
    {
        return BusinessRuleBuilder.Create(Code).WithMessage(Message).When(() => value >= windowStart && value <= windowEnd).Build();
    }

    private sealed class Order
    {
        public Order(decimal amount)
        {
            Amount = amount;
        }

        public decimal Amount { get; }
    }
}
