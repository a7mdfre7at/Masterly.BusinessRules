using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules.UnitTests;

/// <summary>
/// Pins the truth tables of the legacy <c>And()</c>, <c>Or()</c> and <c>Not()</c> composition operators.
/// The operators combine the BROKEN state of their operands:
/// <c>And</c> is broken only when both operands are broken, <c>Or</c> is broken when either operand is broken,
/// and <c>Not</c> flips the broken state. These tests exist so that the behaviour cannot drift silently;
/// changing any row is a breaking change that must be announced in the CHANGELOG.
/// </summary>
public class CompositionTruthTableTests
{
    private const string FirstCode = "FIRST";
    private const string SecondCode = "SECOND";

    // ---------------------------------------------------------------------
    // Sync: And
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void And_IsBroken_IsBrokenOnlyWhenBothOperandsAreBroken(bool firstBroken, bool secondBroken, bool expectedBroken)
    {
        IBusinessRule first = CreateRule(firstBroken, FirstCode);
        IBusinessRule second = CreateRule(secondBroken, SecondCode);
        IBusinessRule composed = first.And(second);

        bool actualBroken = composed.IsBroken();

        Assert.Equal(expectedBroken, actualBroken);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void And_IsBrokenWithContext_MatchesParameterlessTruthTable(bool firstBroken, bool secondBroken, bool expectedBroken)
    {
        IBusinessRule composed = CreateRule(firstBroken, FirstCode).And(CreateRule(secondBroken, SecondCode));
        BusinessRuleContext context = RuleTestHelper.CreateContext();

        bool actualBroken = composed.IsBroken(context);

        Assert.Equal(expectedBroken, actualBroken);
    }

    [Fact]
    public void And_Check_WhenOnlyOneOperandBroken_DoesNotThrow()
    {
        IBusinessRule composed = RuleTestHelper.CreateBrokenRule(FirstCode).And(RuleTestHelper.CreatePassingRule(SecondCode));

        composed.Check();
    }

    [Fact]
    public void And_Check_WhenBothOperandsBroken_ThrowsWithSynthesisedCode()
    {
        IBusinessRule composed = RuleTestHelper.CreateBrokenRule(FirstCode).And(RuleTestHelper.CreateBrokenRule(SecondCode));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => composed.Check());

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal($"{FirstCode}+{SecondCode}", result.Code);
    }

    // ---------------------------------------------------------------------
    // Sync: Or
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void Or_IsBroken_IsBrokenWhenEitherOperandIsBroken(bool firstBroken, bool secondBroken, bool expectedBroken)
    {
        IBusinessRule first = CreateRule(firstBroken, FirstCode);
        IBusinessRule second = CreateRule(secondBroken, SecondCode);
        IBusinessRule composed = first.Or(second);

        bool actualBroken = composed.IsBroken();

        Assert.Equal(expectedBroken, actualBroken);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void Or_IsBrokenWithContext_MatchesParameterlessTruthTable(bool firstBroken, bool secondBroken, bool expectedBroken)
    {
        IBusinessRule composed = CreateRule(firstBroken, FirstCode).Or(CreateRule(secondBroken, SecondCode));
        BusinessRuleContext context = RuleTestHelper.CreateContext();

        bool actualBroken = composed.IsBroken(context);

        Assert.Equal(expectedBroken, actualBroken);
    }

    [Fact]
    public void Or_Check_WhenNeitherOperandBroken_DoesNotThrow()
    {
        IBusinessRule composed = RuleTestHelper.CreatePassingRule(FirstCode).Or(RuleTestHelper.CreatePassingRule(SecondCode));

        composed.Check();
    }

    [Fact]
    public void Or_Check_WhenOnlyOneOperandBroken_ThrowsWithSynthesisedCode()
    {
        IBusinessRule composed = RuleTestHelper.CreatePassingRule(FirstCode).Or(RuleTestHelper.CreateBrokenRule(SecondCode));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => composed.Check());

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal($"{FirstCode}|{SecondCode}", result.Code);
    }

    // ---------------------------------------------------------------------
    // Sync: Not
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Not_IsBroken_InvertsOperandBrokenState(bool operandBroken, bool expectedBroken)
    {
        IBusinessRule composed = CreateRule(operandBroken, FirstCode).Not();

        bool actualBroken = composed.IsBroken();

        Assert.Equal(expectedBroken, actualBroken);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Not_IsBrokenWithContext_MatchesParameterlessTruthTable(bool operandBroken, bool expectedBroken)
    {
        IBusinessRule composed = CreateRule(operandBroken, FirstCode).Not();
        BusinessRuleContext context = RuleTestHelper.CreateContext();

        bool actualBroken = composed.IsBroken(context);

        Assert.Equal(expectedBroken, actualBroken);
    }

    [Fact]
    public void Not_Not_RestoresOperandBrokenState()
    {
        IBusinessRule composed = RuleTestHelper.CreateBrokenRule(FirstCode).Not().Not();

        bool actualBroken = composed.IsBroken();

        Assert.True(actualBroken);
        Assert.Equal($"!!{FirstCode}", composed.Code);
    }

    // ---------------------------------------------------------------------
    // Async: And
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task AndAsync_IsBrokenAsync_IsBrokenOnlyWhenBothOperandsAreBroken(bool firstBroken, bool secondBroken, bool expectedBroken)
    {
        IAsyncBusinessRule first = CreateAsyncRule(firstBroken, FirstCode);
        IAsyncBusinessRule second = CreateAsyncRule(secondBroken, SecondCode);
        IAsyncBusinessRule composed = first.And(second);

        bool actualBroken = await composed.IsBrokenAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(expectedBroken, actualBroken);
    }

    [Fact]
    public async Task AndAsync_CheckAsync_WhenOnlyOneOperandBroken_DoesNotThrow()
    {
        IAsyncBusinessRule composed = RuleTestHelper.CreateBrokenAsyncRule(FirstCode).And(RuleTestHelper.CreatePassingAsyncRule(SecondCode));

        await composed.CheckAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AndAsync_CheckAsync_WhenBothOperandsBroken_ThrowsWithSynthesisedCode()
    {
        IAsyncBusinessRule composed = RuleTestHelper.CreateBrokenAsyncRule(FirstCode).And(RuleTestHelper.CreateBrokenAsyncRule(SecondCode));

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => composed.CheckAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal($"{FirstCode}+{SecondCode}", result.Code);
    }

    [Fact]
    public async Task AndAsync_IsBrokenAsync_WhenFirstOperandPasses_ShortCircuitsSecondOperand()
    {
        int secondEvaluations = 0;
        IAsyncBusinessRule second = CreateCountingAsyncRule(SecondCode, () => secondEvaluations++);
        IAsyncBusinessRule composed = RuleTestHelper.CreatePassingAsyncRule(FirstCode).And(second);

        bool actualBroken = await composed.IsBrokenAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.False(actualBroken);
        Assert.Equal(0, secondEvaluations);
    }

    // ---------------------------------------------------------------------
    // Async: Or
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public async Task OrAsync_IsBrokenAsync_IsBrokenWhenEitherOperandIsBroken(bool firstBroken, bool secondBroken, bool expectedBroken)
    {
        IAsyncBusinessRule first = CreateAsyncRule(firstBroken, FirstCode);
        IAsyncBusinessRule second = CreateAsyncRule(secondBroken, SecondCode);
        IAsyncBusinessRule composed = first.Or(second);

        bool actualBroken = await composed.IsBrokenAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(expectedBroken, actualBroken);
    }

    [Fact]
    public async Task OrAsync_CheckAsync_WhenNeitherOperandBroken_DoesNotThrow()
    {
        IAsyncBusinessRule composed = RuleTestHelper.CreatePassingAsyncRule(FirstCode).Or(RuleTestHelper.CreatePassingAsyncRule(SecondCode));

        await composed.CheckAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OrAsync_CheckAsync_WhenOnlyOneOperandBroken_ThrowsWithSynthesisedCode()
    {
        IAsyncBusinessRule composed = RuleTestHelper.CreatePassingAsyncRule(FirstCode).Or(RuleTestHelper.CreateBrokenAsyncRule(SecondCode));

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => composed.CheckAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal($"{FirstCode}|{SecondCode}", result.Code);
    }

    [Fact]
    public async Task OrAsync_IsBrokenAsync_WhenFirstOperandBroken_ShortCircuitsSecondOperand()
    {
        int secondEvaluations = 0;
        IAsyncBusinessRule second = CreateCountingAsyncRule(SecondCode, () => secondEvaluations++);
        IAsyncBusinessRule composed = RuleTestHelper.CreateBrokenAsyncRule(FirstCode).Or(second);

        bool actualBroken = await composed.IsBrokenAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(actualBroken);
        Assert.Equal(0, secondEvaluations);
    }

    // ---------------------------------------------------------------------
    // Async: Not
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task NotAsync_IsBrokenAsync_InvertsOperandBrokenState(bool operandBroken, bool expectedBroken)
    {
        IAsyncBusinessRule composed = CreateAsyncRule(operandBroken, FirstCode).Not();

        bool actualBroken = await composed.IsBrokenAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(expectedBroken, actualBroken);
    }

    [Fact]
    public async Task NotAsync_Not_RestoresOperandBrokenState()
    {
        IAsyncBusinessRule composed = RuleTestHelper.CreateBrokenAsyncRule(FirstCode).Not().Not();

        bool actualBroken = await composed.IsBrokenAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(actualBroken);
        Assert.Equal($"!!{FirstCode}", composed.Code);
    }

    // ---------------------------------------------------------------------
    // Async: context forwarding and cancellation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task AndAsync_IsBrokenAsync_ForwardsContextToBothOperands()
    {
        const string flagKey = "flag";
        IAsyncBusinessRule first = CreateContextReadingAsyncRule(FirstCode, flagKey);
        IAsyncBusinessRule second = CreateContextReadingAsyncRule(SecondCode, flagKey);
        IAsyncBusinessRule composed = first.And(second);
        BusinessRuleContext context = RuleTestHelper.CreateContext((flagKey, true));

        bool actualBroken = await composed.IsBrokenAsync(context, TestContext.Current.CancellationToken);

        Assert.True(actualBroken);
    }

    [Fact]
    public async Task AndAsync_IsBrokenAsync_WhenOperandObservesCancelledToken_PropagatesCancellation()
    {
        IAsyncBusinessRule cancellable = CreateCancellationObservingAsyncRule(FirstCode);
        IAsyncBusinessRule composed = cancellable.And(RuleTestHelper.CreateBrokenAsyncRule(SecondCode));
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => composed.IsBrokenAsync(RuleTestHelper.CreateContext(), cancellation.Token));
    }

    [Fact]
    public async Task OrAsync_IsBrokenAsync_WhenOperandObservesCancelledToken_PropagatesCancellation()
    {
        IAsyncBusinessRule cancellable = CreateCancellationObservingAsyncRule(FirstCode);
        IAsyncBusinessRule composed = cancellable.Or(RuleTestHelper.CreatePassingAsyncRule(SecondCode));
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => composed.IsBrokenAsync(RuleTestHelper.CreateContext(), cancellation.Token));
    }

    [Fact]
    public async Task NotAsync_IsBrokenAsync_WhenOperandObservesCancelledToken_PropagatesCancellation()
    {
        IAsyncBusinessRule composed = CreateCancellationObservingAsyncRule(FirstCode).Not();
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => composed.IsBrokenAsync(RuleTestHelper.CreateContext(), cancellation.Token));
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static IBusinessRule CreateRule(bool broken, string code)
    {
        return broken ? RuleTestHelper.CreateBrokenRule(code) : RuleTestHelper.CreatePassingRule(code);
    }

    private static IAsyncBusinessRule CreateAsyncRule(bool broken, string code)
    {
        return broken ? RuleTestHelper.CreateBrokenAsyncRule(code) : RuleTestHelper.CreatePassingAsyncRule(code);
    }

    private static IAsyncBusinessRule CreateCountingAsyncRule(string code, Action onEvaluated)
    {
        return AsyncBusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .WhenAsync((ctx, ct) =>
            {
                onEvaluated();
                return Task.FromResult(true);
            })
            .Build();
    }

    private static IAsyncBusinessRule CreateContextReadingAsyncRule(string code, string flagKey)
    {
        return AsyncBusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .WhenAsync((ctx, ct) => Task.FromResult(ctx.Get<bool>(flagKey)))
            .Build();
    }

    private static IAsyncBusinessRule CreateCancellationObservingAsyncRule(string code)
    {
        return AsyncBusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .WhenAsync((ctx, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(true);
            })
            .Build();
    }
}
