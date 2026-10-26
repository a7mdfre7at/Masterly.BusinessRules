using System.Globalization;

namespace Masterly.BusinessRules.UnitTests;

public class RuleConditionCompilerTests
{
    private const string Code = "ORDER.TOO_LARGE";
    private const string Message = "Order amount exceeds the limit.";
    private const string AmountKey = "amount";
    private const string BrokenExpression = "amount > 100";
    private const string PassingExpression = "amount > 1000";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------
    // Sync seam
    // ---------------------------------------------------------------------

    [Fact]
    public void When_WithCompiler_EvaluatesTheCompiledDelegate()
    {
        FakeCompiler compiler = new FakeCompiler();
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WithMessage(Message).When(compiler, BrokenExpression).Build();

        Assert.True(rule.IsBroken(RuleTestHelper.CreateContext((AmountKey, 500m))));
        Assert.False(rule.IsBroken(RuleTestHelper.CreateContext((AmountKey, 50m))));
    }

    [Fact]
    public void When_WithCompiler_CompilesAtBuilderTime()
    {
        FakeCompiler compiler = new FakeCompiler();

        BusinessRuleBuilder.Create(Code).When(compiler, BrokenExpression);

        Assert.Equal(new[] { BrokenExpression }, compiler.Compiled);
    }

    [Fact]
    public void When_WithCompiler_CompilesOnceNotPerEvaluation()
    {
        FakeCompiler compiler = new FakeCompiler();
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).When(compiler, PassingExpression).Build();

        rule.IsBroken(RuleTestHelper.CreateContext((AmountKey, 1m)));
        rule.IsBroken(RuleTestHelper.CreateContext((AmountKey, 1m)));

        Assert.Single(compiler.Compiled);
    }

    [Fact]
    public void When_WithCompiler_ParameterlessIsBroken_UsesAnEmptyContext()
    {
        FakeCompiler compiler = new FakeCompiler();
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).When(compiler, BrokenExpression).Build();

        Assert.Throws<ContextKeyNotFoundException>(() => rule.IsBroken());
    }

    [Fact]
    public void When_CompilerThrows_PropagatesAtBuilderTime()
    {
        ThrowingCompiler compiler = new ThrowingCompiler();

        Assert.Throws<FormatException>(() => BusinessRuleBuilder.Create(Code).When(compiler, "not valid"));
    }

    [Fact]
    public void When_CompilerReturnsNull_ThrowsArgumentNullException()
    {
        NullCompiler compiler = new NullCompiler();

        Assert.Throws<ArgumentNullException>(() => BusinessRuleBuilder.Create(Code).When(compiler, BrokenExpression));
    }

    [Fact]
    public void When_NullCompiler_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => BusinessRuleBuilder.Create(Code).When((IRuleConditionCompiler)null!, BrokenExpression));
    }

    [Fact]
    public void When_NullExpression_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => BusinessRuleBuilder.Create(Code).When(new FakeCompiler(), null!));
    }

    // ---------------------------------------------------------------------
    // Async seam
    // ---------------------------------------------------------------------

    [Fact]
    public async Task WhenAsync_WithCompiler_EvaluatesTheCompiledDelegate()
    {
        FakeAsyncCompiler compiler = new FakeAsyncCompiler();
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WhenAsync(compiler, BrokenExpression).Build();

        Assert.True(await rule.IsBrokenAsync(RuleTestHelper.CreateContext((AmountKey, 500m)), TestToken));
        Assert.False(await rule.IsBrokenAsync(RuleTestHelper.CreateContext((AmountKey, 50m)), TestToken));
    }

    [Fact]
    public void WhenAsync_WithCompiler_CompilesAtBuilderTime()
    {
        FakeAsyncCompiler compiler = new FakeAsyncCompiler();

        AsyncBusinessRuleBuilder.Create(Code).WhenAsync(compiler, BrokenExpression);

        Assert.Equal(new[] { BrokenExpression }, compiler.Compiled);
    }

    [Fact]
    public async Task WhenAsync_WithCompiler_ForwardsTheCancellationToken()
    {
        FakeAsyncCompiler compiler = new FakeAsyncCompiler();
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WhenAsync(compiler, BrokenExpression).Build();
        using CancellationTokenSource source = new CancellationTokenSource();

        await rule.IsBrokenAsync(RuleTestHelper.CreateContext((AmountKey, 1m)), source.Token);

        Assert.Equal(source.Token, compiler.ObservedToken);
    }

    [Fact]
    public async Task WhenAsync_WithCompiler_CancelledToken_PropagatesCancellation()
    {
        FakeAsyncCompiler compiler = new FakeAsyncCompiler();
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WhenAsync(compiler, BrokenExpression).Build();
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rule.IsBrokenAsync(RuleTestHelper.CreateContext((AmountKey, 1m)), source.Token));
    }

    [Fact]
    public void WhenAsync_CompilerThrows_PropagatesAtBuilderTime()
    {
        ThrowingCompiler compiler = new ThrowingCompiler();

        Assert.Throws<FormatException>(() => AsyncBusinessRuleBuilder.Create(Code).WhenAsync(compiler, "not valid"));
    }

    [Fact]
    public void WhenAsync_CompilerReturnsNull_ThrowsArgumentNullException()
    {
        NullCompiler compiler = new NullCompiler();

        Assert.Throws<ArgumentNullException>(() => AsyncBusinessRuleBuilder.Create(Code).WhenAsync(compiler, BrokenExpression));
    }

    [Fact]
    public void WhenAsync_NullCompiler_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AsyncBusinessRuleBuilder.Create(Code).WhenAsync((IAsyncRuleConditionCompiler)null!, BrokenExpression));
    }

    [Fact]
    public void WhenAsync_NullExpression_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AsyncBusinessRuleBuilder.Create(Code).WhenAsync(new FakeAsyncCompiler(), null!));
    }

    // ---------------------------------------------------------------------
    // Fakes: "amount > N" against the context key "amount"
    // ---------------------------------------------------------------------

    private static Func<BusinessRuleContext, bool> Parse(string expression)
    {
        decimal threshold = decimal.Parse(expression.Substring(expression.IndexOf('>') + 1).Trim(), CultureInfo.InvariantCulture);
        return context => context.Get<decimal>(AmountKey) > threshold;
    }

    private sealed class FakeCompiler : IRuleConditionCompiler
    {
        public List<string> Compiled { get; } = new List<string>();

        public Func<BusinessRuleContext, bool> Compile(string expression)
        {
            Compiled.Add(expression);
            return Parse(expression);
        }
    }

    private sealed class FakeAsyncCompiler : IAsyncRuleConditionCompiler
    {
        public List<string> Compiled { get; } = new List<string>();

        public CancellationToken? ObservedToken { get; private set; }

        public Func<BusinessRuleContext, CancellationToken, Task<bool>> Compile(string expression)
        {
            Compiled.Add(expression);
            Func<BusinessRuleContext, bool> condition = Parse(expression);
            return (context, cancellationToken) =>
            {
                ObservedToken = cancellationToken;
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(condition(context));
            };
        }
    }

    private sealed class ThrowingCompiler : IRuleConditionCompiler, IAsyncRuleConditionCompiler
    {
        Func<BusinessRuleContext, bool> IRuleConditionCompiler.Compile(string expression) => throw new FormatException(expression);

        Func<BusinessRuleContext, CancellationToken, Task<bool>> IAsyncRuleConditionCompiler.Compile(string expression) => throw new FormatException(expression);
    }

    private sealed class NullCompiler : IRuleConditionCompiler, IAsyncRuleConditionCompiler
    {
        Func<BusinessRuleContext, bool> IRuleConditionCompiler.Compile(string expression) => null!;

        Func<BusinessRuleContext, CancellationToken, Task<bool>> IAsyncRuleConditionCompiler.Compile(string expression) => null!;
    }
}
