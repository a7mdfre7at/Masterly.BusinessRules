namespace Masterly.BusinessRules.UnitTests;

/// <summary>
/// A later <c>When</c> call replaces every condition set before it on the untyped sync builder, and a parameters
/// condition never outlives a later <c>When</c> on the subject-typed one (whose subject and context conditions coexist
/// by design, see <c>TypedBusinessRuleBuilderTests</c>). Mirrors <see cref="AsyncBusinessRuleBuilderConditionReplacementTests"/>.
/// </summary>
public class BusinessRuleBuilderConditionReplacementTests
{
    private const string Code = "LATER.WINS";

    [Fact]
    public void When_ParametersConditionThenContextCondition_LaterConditionWins()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WithMessage("m").When((ctx, p) => true).When(ctx => false).Build();

        Assert.False(rule.IsBroken(new BusinessRuleContext()));
        Assert.False(rule.IsBroken());
    }

    [Fact]
    public void When_ParametersConditionThenParameterlessCondition_LaterConditionWins()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WithMessage("m").When((ctx, p) => true).When(() => false).Build();

        Assert.False(rule.IsBroken(new BusinessRuleContext()));
        Assert.False(rule.IsBroken());
    }

    [Fact]
    public void When_ContextConditionThenParameterlessCondition_LaterConditionWins()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WithMessage("m").When(ctx => true).When(() => false).Build();

        Assert.False(rule.IsBroken(new BusinessRuleContext()));
        Assert.False(rule.IsBroken());
    }

    [Fact]
    public void When_ParameterlessConditionThenContextCondition_LaterConditionWins()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WithMessage("m").When(() => true).When(ctx => false).Build();

        Assert.False(rule.IsBroken(new BusinessRuleContext()));
        Assert.False(rule.IsBroken());
    }

    [Fact]
    public void When_ContextConditionThenParametersCondition_LaterConditionWins()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WithMessage("m").When(ctx => true).When((ctx, p) => false).Build();

        Assert.False(rule.IsBroken(new BusinessRuleContext()));
        Assert.False(rule.IsBroken());
    }

    [Fact]
    public void When_ParametersConditionThenCompiledCondition_LaterConditionWins()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WithMessage("m").When((ctx, p) => true).When(new FixedCompiler(false), "false").Build();

        Assert.False(rule.IsBroken(new BusinessRuleContext()));
    }

    [Fact]
    public void When_CompiledConditionThenParametersCondition_LaterConditionWins()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WithMessage("m").When(new FixedCompiler(true), "true").When((ctx, p) => false).Build();

        Assert.False(rule.IsBroken(new BusinessRuleContext()));
    }

    [Fact]
    public void When_FactoryThenCondition_ConditionReplacesTheFactory()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WithMessage("m").WithRuleFactory(new ThrowingFactory()).When(() => true).Build();

        Assert.True(rule.IsBroken());
    }

    [Fact]
    public void TypedWhen_ParametersConditionThenSubjectCondition_LaterConditionWins()
    {
        IBusinessRule<int> rule = BusinessRuleBuilder<int>.Create(Code).WithMessage("m").When((value, ctx, p) => true).When(value => false).Build();

        Assert.False(rule.IsBroken(1, new BusinessRuleContext()));
        Assert.False(rule.IsBroken(1));
    }

    [Fact]
    public void TypedWhen_ParametersConditionThenContextCondition_LaterConditionWins()
    {
        IBusinessRule<int> rule = BusinessRuleBuilder<int>.Create(Code).WithMessage("m").When((value, ctx, p) => true).When((value, ctx) => false).Build();

        Assert.False(rule.IsBroken(1, new BusinessRuleContext()));
    }

    [Fact]
    public void TypedWhen_SubjectConditionThenParametersCondition_LaterConditionWins()
    {
        IBusinessRule<int> rule = BusinessRuleBuilder<int>.Create(Code).WithMessage("m").When(value => true).When((value, ctx, p) => false).Build();

        Assert.False(rule.IsBroken(1, new BusinessRuleContext()));
    }

    private sealed class FixedCompiler : IRuleConditionCompiler
    {
        private readonly bool _verdict;

        public FixedCompiler(bool verdict)
        {
            _verdict = verdict;
        }

        public Func<BusinessRuleContext, bool> Compile(string expression) => _ => _verdict;
    }

    private sealed class ThrowingFactory : IBuiltRuleFactory
    {
        public IBusinessRule Create(BuiltRuleDefinition definition) => throw new InvalidOperationException("The factory must have been replaced.");
    }
}

/// <summary>
/// A later <c>WhenAsync</c> call replaces every condition set before it on the untyped and the subject-typed async
/// builders (mirrors <see cref="BusinessRuleBuilderConditionReplacementTests"/>).
/// </summary>
public class AsyncBusinessRuleBuilderConditionReplacementTests
{
    private const string Code = "LATER.WINS";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WhenAsync_ParametersConditionThenContextCondition_LaterConditionWins()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WithMessage("m").WhenAsync((ctx, p, ct) => Task.FromResult(true)).WhenAsync(ctx => Task.FromResult(false)).Build();

        Assert.False(await rule.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task WhenAsync_ParametersConditionThenTokenCondition_LaterConditionWins()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WithMessage("m").WhenAsync((ctx, p, ct) => Task.FromResult(true)).WhenAsync((ctx, ct) => Task.FromResult(false)).Build();

        Assert.False(await rule.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task WhenAsync_ContextConditionThenParametersCondition_LaterConditionWins()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WithMessage("m").WhenAsync(ctx => Task.FromResult(true)).WhenAsync((ctx, p, ct) => Task.FromResult(false)).Build();

        Assert.False(await rule.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task WhenAsync_ParametersConditionThenCompiledCondition_LaterConditionWins()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WithMessage("m").WhenAsync((ctx, p, ct) => Task.FromResult(true)).WhenAsync(new FixedAsyncCompiler(false), "false").Build();

        Assert.False(await rule.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task WhenAsync_CompiledConditionThenParametersCondition_LaterConditionWins()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WithMessage("m").WhenAsync(new FixedAsyncCompiler(true), "true").WhenAsync((ctx, p, ct) => Task.FromResult(false)).Build();

        Assert.False(await rule.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task TypedWhenAsync_ParametersConditionThenSubjectCondition_LaterConditionWins()
    {
        IAsyncBusinessRule<int> rule = AsyncBusinessRuleBuilder<int>.Create(Code).WithMessage("m")
            .WhenAsync((value, ctx, p, ct) => Task.FromResult(true))
            .WhenAsync(value => Task.FromResult(false))
            .Build();

        Assert.False(await rule.IsBrokenAsync(1, new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task TypedWhenAsync_SubjectConditionThenParametersCondition_LaterConditionWins()
    {
        IAsyncBusinessRule<int> rule = AsyncBusinessRuleBuilder<int>.Create(Code).WithMessage("m")
            .WhenAsync(value => Task.FromResult(true))
            .WhenAsync((value, ctx, p, ct) => Task.FromResult(false))
            .Build();

        Assert.False(await rule.IsBrokenAsync(1, new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task TypedWhenAsync_ContextConditionThenSubjectCondition_LaterConditionWins()
    {
        IAsyncBusinessRule<int> rule = AsyncBusinessRuleBuilder<int>.Create(Code).WithMessage("m")
            .WhenAsync((value, ctx) => Task.FromResult(true))
            .WhenAsync(value => Task.FromResult(false))
            .Build();

        Assert.False(await rule.IsBrokenAsync(1, new BusinessRuleContext(), TestToken));
    }

    private sealed class FixedAsyncCompiler : IAsyncRuleConditionCompiler
    {
        private readonly bool _verdict;

        public FixedAsyncCompiler(bool verdict)
        {
            _verdict = verdict;
        }

        public Func<BusinessRuleContext, CancellationToken, Task<bool>> Compile(string expression) => (_, _) => Task.FromResult(_verdict);
    }
}
