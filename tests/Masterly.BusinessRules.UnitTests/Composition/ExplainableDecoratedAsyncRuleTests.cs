namespace Masterly.BusinessRules.UnitTests;

public class ExplainableDecoratedAsyncRuleTests
{
    private const string InnerCode = "INNER.ASYNC";
    private const string OtherCode = "OTHER.ASYNC";
    private const string NegateOperator = "NEGATE";
    private const string SkipOperator = "SKIP";
    private const string MarkerKey = "marker";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExplainAsync_ExplainableDecorator_RecordsDecoratorNodeWithInnerChild()
    {
        NegatingAsyncDecorator decorator = new NegatingAsyncDecorator(RuleTestHelper.CreatePassingAsyncRule(InnerCode));

        RuleExplanation explanation = await decorator.ExplainAsync(new BusinessRuleContext(), TestToken);

        Assert.Same(decorator, explanation.AsyncRule);
        Assert.Equal(NegateOperator, explanation.Operator);
        Assert.Equal(RuleOutcome.Broken, explanation.Outcome);
        RuleExplanation child = Assert.Single(explanation.Children);
        Assert.Equal(InnerCode, child.Code);
        Assert.Equal(RuleOutcome.Passed, child.Outcome);
    }

    [Fact]
    public async Task ExplainAsync_ExplainableDecoratorOverComposedRule_RecordsTheInnerSubtree()
    {
        IAsyncBusinessRule inner = RuleTestHelper.CreateBrokenAsyncRule(InnerCode).And(RuleTestHelper.CreateBrokenAsyncRule(OtherCode));
        NegatingAsyncDecorator decorator = new NegatingAsyncDecorator(inner);

        RuleExplanation explanation = await decorator.ExplainAsync(new BusinessRuleContext(), TestToken);

        RuleExplanation andNode = Assert.Single(explanation.Children);
        Assert.Equal("AND", andNode.Operator);
        Assert.Equal(2, andNode.Children.Count);
        Assert.False(explanation.IsBroken);
    }

    [Fact]
    public async Task ExplainAsync_DecoratorThatSkipsInner_RecordsInnerAsNotEvaluated()
    {
        int innerEvaluations = 0;
        IAsyncBusinessRule inner = AsyncBusinessRuleBuilder.Create(InnerCode).WhenAsync(_ => Task.FromResult(++innerEvaluations > 0)).Build();
        SkippingAsyncDecorator decorator = new SkippingAsyncDecorator(inner);

        RuleExplanation explanation = await decorator.ExplainAsync(new BusinessRuleContext(), TestToken);

        Assert.Equal(SkipOperator, explanation.Operator);
        Assert.Equal(RuleOutcome.Passed, explanation.Outcome);
        RuleExplanation child = Assert.Single(explanation.Children);
        Assert.Equal(RuleOutcome.NotEvaluated, child.Outcome);
        Assert.Equal("not evaluated by decorator", child.Reason);
        Assert.Equal(0, innerEvaluations);
    }

    [Fact]
    public async Task ExplainAsync_ForwardsTheContextToTheInnerRule()
    {
        IAsyncBusinessRule inner = AsyncBusinessRuleBuilder.Create(InnerCode).WhenAsync(context => Task.FromResult(context.ContainsKey(MarkerKey))).Build();
        NegatingAsyncDecorator decorator = new NegatingAsyncDecorator(inner);

        RuleExplanation explanation = await decorator.ExplainAsync(RuleTestHelper.CreateContext((MarkerKey, true)), TestToken);

        Assert.Equal(RuleOutcome.Broken, explanation.Children[0].Outcome);
        Assert.False(explanation.IsBroken);
    }

    [Fact]
    public async Task ExplainAsync_CaptureExceptions_InnerFaultIsRecordedAsFaultedChild()
    {
        IAsyncBusinessRule inner = AsyncBusinessRuleBuilder.Create(InnerCode).WhenAsync(_ => throw new InvalidOperationException("boom")).Build();
        NegatingAsyncDecorator decorator = new NegatingAsyncDecorator(inner);

        RuleExplanation explanation = await decorator.ExplainAsync(new BusinessRuleContext(), captureExceptions: true, TestToken);

        Assert.Equal(RuleOutcome.Faulted, Assert.Single(explanation.Children).Outcome);
    }

    [Fact]
    public async Task ExplainAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        NegatingAsyncDecorator decorator = new NegatingAsyncDecorator(RuleTestHelper.CreatePassingAsyncRule(InnerCode));
        using CancellationTokenSource cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => decorator.ExplainAsync(new BusinessRuleContext(), captureExceptions: true, cancelled.Token));
    }

    [Fact]
    public async Task Composition_PlainPath_UsesEvaluateDecoratedAsyncAsTheVerdict()
    {
        NegatingAsyncDecorator decorator = new NegatingAsyncDecorator(RuleTestHelper.CreatePassingAsyncRule(InnerCode));
        IAsyncBusinessRule composed = decorator.And(RuleTestHelper.CreateBrokenAsyncRule(OtherCode));

        bool isBroken = await composed.IsBrokenAsync(new BusinessRuleContext(), TestToken);

        Assert.True(isBroken);
        Assert.Equal(1, decorator.DecorateCalls);
    }

    [Fact]
    public async Task EvaluateInnerAsync_CalledTwice_EvaluatesTheInnerRuleOnce()
    {
        int innerEvaluations = 0;
        IAsyncBusinessRule inner = AsyncBusinessRuleBuilder.Create(InnerCode).WhenAsync(_ => Task.FromResult(++innerEvaluations > 0)).Build();
        AsyncDecoratedRuleEvaluation evaluation = new AsyncDecoratedRuleEvaluation(inner, new BusinessRuleContext());

        BusinessRuleResult? first = await evaluation.EvaluateInnerAsync(TestToken);
        BusinessRuleResult? second = await evaluation.EvaluateInnerAsync(TestToken);

        Assert.Same(first, second);
        Assert.Equal(1, innerEvaluations);
        Assert.True(evaluation.IsInnerEvaluated);
    }

    [Fact]
    public async Task EvaluateInnerAsync_CancelledToken_ThrowsAndLeavesInnerUnevaluated()
    {
        AsyncDecoratedRuleEvaluation evaluation = new AsyncDecoratedRuleEvaluation(RuleTestHelper.CreateBrokenAsyncRule(InnerCode), new BusinessRuleContext());
        using CancellationTokenSource cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => evaluation.EvaluateInnerAsync(cancelled.Token));

        Assert.False(evaluation.IsInnerEvaluated);
    }

    [Fact]
    public void AsyncDecoratedRuleEvaluation_PlainConstructor_IsNotExplainingAndNotYetEvaluated()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        IAsyncBusinessRule inner = RuleTestHelper.CreateBrokenAsyncRule(InnerCode);

        AsyncDecoratedRuleEvaluation evaluation = new AsyncDecoratedRuleEvaluation(inner, context);

        Assert.Same(inner, evaluation.InnerRule);
        Assert.Same(context, evaluation.Context);
        Assert.False(evaluation.IsExplaining);
        Assert.False(evaluation.IsInnerEvaluated);
    }

    [Fact]
    public void AsyncDecoratedRuleEvaluation_NullInnerRule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AsyncDecoratedRuleEvaluation(null!, new BusinessRuleContext()));
    }

    [Fact]
    public async Task EvaluateInnerAsync_ComposedInnerRule_KeepsMemberDetails()
    {
        IAsyncBusinessRule inner = AsyncRuleGroup.All("GROUP", "group broken", RuleTestHelper.CreateBrokenAsyncRule(InnerCode), RuleTestHelper.CreatePassingAsyncRule(OtherCode));
        AsyncDecoratedRuleEvaluation evaluation = new AsyncDecoratedRuleEvaluation(inner, new BusinessRuleContext());

        BusinessRuleResult? result = await evaluation.EvaluateInnerAsync(TestToken);

        Assert.NotNull(result);
        Assert.Equal(InnerCode, Assert.Single(result!.Details).Code);
    }

    private sealed class NegatingAsyncDecorator : IExplainableDecoratedAsyncBusinessRule
    {
        public NegatingAsyncDecorator(IAsyncBusinessRule innerRule)
        {
            InnerRule = innerRule;
        }

        public int DecorateCalls { get; private set; }

        public IAsyncBusinessRule InnerRule { get; }

        public string ExplanationOperator => NegateOperator;

        public string Code => InnerRule.Code;

        public string Message => InnerRule.Message;

        public async Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => await EvaluateAsync(context, cancellationToken) != null;

        public Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => EvaluateDecoratedAsync(new AsyncDecoratedRuleEvaluation(InnerRule, context), cancellationToken);

        public async Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            BusinessRuleResult? result = await EvaluateAsync(context, cancellationToken);
            if (result != null)
            {
                throw new BusinessRuleValidationException(new[] { result });
            }
        }

        public async Task<BusinessRuleResult?> EvaluateDecoratedAsync(AsyncDecoratedRuleEvaluation evaluation, CancellationToken cancellationToken)
        {
            DecorateCalls++;
            BusinessRuleResult? inner = await evaluation.EvaluateInnerAsync(cancellationToken);
            return inner == null ? new BusinessRuleResult(Code, Message, InnerRule.Severity) : null;
        }
    }

    private sealed class SkippingAsyncDecorator : IExplainableDecoratedAsyncBusinessRule
    {
        public SkippingAsyncDecorator(IAsyncBusinessRule innerRule)
        {
            InnerRule = innerRule;
        }

        public IAsyncBusinessRule InnerRule { get; }

        public string ExplanationOperator => SkipOperator;

        public string Code => InnerRule.Code;

        public string Message => InnerRule.Message;

        public Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => Task.FromResult<BusinessRuleResult?>(null);

        public Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<BusinessRuleResult?> EvaluateDecoratedAsync(AsyncDecoratedRuleEvaluation evaluation, CancellationToken cancellationToken)
            => Task.FromResult<BusinessRuleResult?>(null);
    }
}
