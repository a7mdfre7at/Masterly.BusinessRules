namespace Masterly.BusinessRules.UnitTests;

public class ExplainableDecoratedRuleTests
{
    private const string InnerCode = "INNER";
    private const string OtherCode = "OTHER";
    private const string NegateOperator = "NEGATE";
    private const string SkipOperator = "SKIP";
    private const string MarkerKey = "marker";

    [Fact]
    public void Explain_ExplainableDecorator_RecordsDecoratorNodeWithInnerChild()
    {
        NegatingDecorator decorator = new NegatingDecorator(RuleTestHelper.CreatePassingRule(InnerCode));

        RuleExplanation explanation = decorator.Explain();

        Assert.Same(decorator, explanation.Rule);
        Assert.Equal(NegateOperator, explanation.Operator);
        Assert.Equal(RuleOutcome.Broken, explanation.Outcome);
        RuleExplanation child = Assert.Single(explanation.Children);
        Assert.Equal(InnerCode, child.Code);
        Assert.Equal(RuleOutcome.Passed, child.Outcome);
    }

    [Fact]
    public void Explain_ExplainableDecoratorOverComposedRule_RecordsTheInnerSubtree()
    {
        IBusinessRule inner = RuleTestHelper.CreateBrokenRule(InnerCode).And(RuleTestHelper.CreateBrokenRule(OtherCode));
        NegatingDecorator decorator = new NegatingDecorator(inner);

        RuleExplanation explanation = decorator.Explain();

        RuleExplanation andNode = Assert.Single(explanation.Children);
        Assert.Equal("AND", andNode.Operator);
        Assert.Equal(2, andNode.Children.Count);
        Assert.False(explanation.IsBroken);
    }

    [Fact]
    public void Explain_DecoratorThatSkipsInner_RecordsInnerAsNotEvaluated()
    {
        int innerEvaluations = 0;
        IBusinessRule inner = BusinessRuleBuilder.Create(InnerCode).When(() => ++innerEvaluations > 0).Build();
        SkippingDecorator decorator = new SkippingDecorator(inner);

        RuleExplanation explanation = decorator.Explain();

        Assert.Equal(SkipOperator, explanation.Operator);
        Assert.Equal(RuleOutcome.Passed, explanation.Outcome);
        RuleExplanation child = Assert.Single(explanation.Children);
        Assert.Equal(RuleOutcome.NotEvaluated, child.Outcome);
        Assert.Equal("not evaluated by decorator", child.Reason);
        Assert.Equal(0, innerEvaluations);
    }

    [Fact]
    public void Explain_ForwardsTheContextToTheInnerRule()
    {
        IBusinessRule inner = BusinessRuleBuilder.Create(InnerCode).When(context => context.ContainsKey(MarkerKey)).Build();
        NegatingDecorator decorator = new NegatingDecorator(inner);

        RuleExplanation explanation = decorator.Explain(RuleTestHelper.CreateContext((MarkerKey, true)));

        Assert.Equal(RuleOutcome.Broken, explanation.Children[0].Outcome);
        Assert.False(explanation.IsBroken);
    }

    [Fact]
    public void Explain_CaptureExceptions_InnerFaultIsRecordedAsFaultedChild()
    {
        IBusinessRule inner = BusinessRuleBuilder.Create(InnerCode).When(() => throw new InvalidOperationException("boom")).Build();
        NegatingDecorator decorator = new NegatingDecorator(inner);

        RuleExplanation explanation = decorator.Explain(captureExceptions: true);

        Assert.Equal(RuleOutcome.Faulted, Assert.Single(explanation.Children).Outcome);
    }

    [Fact]
    public void Explain_WithoutCapture_InnerFaultPropagates()
    {
        IBusinessRule inner = BusinessRuleBuilder.Create(InnerCode).When(() => throw new InvalidOperationException("boom")).Build();
        NegatingDecorator decorator = new NegatingDecorator(inner);

        Assert.Throws<InvalidOperationException>(() => decorator.Explain());
    }

    [Fact]
    public void Composition_PlainPath_UsesEvaluateDecoratedAsTheVerdict()
    {
        NegatingDecorator decorator = new NegatingDecorator(RuleTestHelper.CreatePassingRule(InnerCode));
        IBusinessRule composed = decorator.And(RuleTestHelper.CreateBrokenRule(OtherCode));

        bool isBroken = composed.IsBroken(new BusinessRuleContext());

        Assert.True(isBroken);
        Assert.Equal(1, decorator.DecorateCalls);
    }

    [Fact]
    public void Explain_ComposedOverDecorator_RecordsTheDecoratorNodeAsOperand()
    {
        NegatingDecorator decorator = new NegatingDecorator(RuleTestHelper.CreatePassingRule(InnerCode));
        IBusinessRule composed = decorator.Or(RuleTestHelper.CreatePassingRule(OtherCode));

        RuleExplanation explanation = composed.Explain();

        RuleExplanation decoratorNode = explanation.Children[0];
        Assert.Equal(NegateOperator, decoratorNode.Operator);
        Assert.Equal(InnerCode, Assert.Single(decoratorNode.Children).Code);
    }

    [Fact]
    public void EvaluateInner_CalledTwice_EvaluatesTheInnerRuleOnce()
    {
        int innerEvaluations = 0;
        IBusinessRule inner = BusinessRuleBuilder.Create(InnerCode).When(() => ++innerEvaluations > 0).Build();
        DecoratedRuleEvaluation evaluation = new DecoratedRuleEvaluation(inner, null);

        BusinessRuleResult? first = evaluation.EvaluateInner();
        BusinessRuleResult? second = evaluation.EvaluateInner();

        Assert.Same(first, second);
        Assert.Equal(1, innerEvaluations);
        Assert.True(evaluation.IsInnerEvaluated);
    }

    [Fact]
    public void DecoratedRuleEvaluation_PlainConstructor_IsNotExplainingAndNotYetEvaluated()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        IBusinessRule inner = RuleTestHelper.CreateBrokenRule(InnerCode);

        DecoratedRuleEvaluation evaluation = new DecoratedRuleEvaluation(inner, context);

        Assert.Same(inner, evaluation.InnerRule);
        Assert.Same(context, evaluation.Context);
        Assert.False(evaluation.IsExplaining);
        Assert.False(evaluation.IsInnerEvaluated);
    }

    [Fact]
    public void DecoratedRuleEvaluation_NullInnerRule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DecoratedRuleEvaluation(null!, null));
    }

    [Fact]
    public void EvaluateInner_ComposedInnerRule_KeepsMemberDetails()
    {
        IBusinessRule inner = RuleGroup.All("GROUP", "group broken", RuleTestHelper.CreateBrokenRule(InnerCode), RuleTestHelper.CreatePassingRule(OtherCode));
        DecoratedRuleEvaluation evaluation = new DecoratedRuleEvaluation(inner, null);

        BusinessRuleResult? result = evaluation.EvaluateInner();

        Assert.NotNull(result);
        Assert.Equal(InnerCode, Assert.Single(result!.Details).Code);
    }

    private sealed class NegatingDecorator : IExplainableDecoratedBusinessRule
    {
        public NegatingDecorator(IBusinessRule innerRule)
        {
            InnerRule = innerRule;
        }

        public int DecorateCalls { get; private set; }

        public IBusinessRule InnerRule { get; }

        public string ExplanationOperator => NegateOperator;

        public string Code => InnerRule.Code;

        public string Message => InnerRule.Message;

        public bool IsBroken() => EvaluateDecorated(new DecoratedRuleEvaluation(InnerRule, null)) != null;

        public bool IsBroken(BusinessRuleContext context) => EvaluateDecorated(new DecoratedRuleEvaluation(InnerRule, context)) != null;

        public void Check()
        {
            if (IsBroken())
            {
                throw new BusinessRuleValidationException(new[] { Evaluate() });
            }
        }

        public void Check(BusinessRuleContext context)
        {
            if (IsBroken(context))
            {
                throw new BusinessRuleValidationException(new[] { Evaluate(context) });
            }
        }

        public BusinessRuleResult Evaluate() => new BusinessRuleResult(Code, Message, InnerRule.Severity);

        public BusinessRuleResult Evaluate(BusinessRuleContext context) => Evaluate();

        public BusinessRuleResult? EvaluateDecorated(DecoratedRuleEvaluation evaluation)
        {
            DecorateCalls++;
            return evaluation.EvaluateInner() == null ? Evaluate() : null;
        }
    }

    private sealed class SkippingDecorator : IExplainableDecoratedBusinessRule
    {
        public SkippingDecorator(IBusinessRule innerRule)
        {
            InnerRule = innerRule;
        }

        public IBusinessRule InnerRule { get; }

        public string ExplanationOperator => SkipOperator;

        public string Code => InnerRule.Code;

        public string Message => InnerRule.Message;

        public bool IsBroken() => false;

        public bool IsBroken(BusinessRuleContext context) => false;

        public void Check()
        {
        }

        public void Check(BusinessRuleContext context)
        {
        }

        public BusinessRuleResult Evaluate() => new BusinessRuleResult(Code, Message, InnerRule.Severity);

        public BusinessRuleResult Evaluate(BusinessRuleContext context) => Evaluate();

        public BusinessRuleResult? EvaluateDecorated(DecoratedRuleEvaluation evaluation) => null;
    }
}
