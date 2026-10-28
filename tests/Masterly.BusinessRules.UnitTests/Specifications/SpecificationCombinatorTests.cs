using System.Linq.Expressions;

namespace Masterly.BusinessRules.UnitTests;

public class SpecificationCombinatorTests
{
    // ---------------------------------------------------------------------
    // Truth tables (satisfaction semantics)
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void AllOf_IsBrokenWhenAnyOperandIsBroken(bool firstBroken, bool secondBroken, bool expected)
    {
        ISpecificationRule<Order> composed = Fake("A", firstBroken).AllOf(Fake("B", secondBroken));

        Assert.Equal(expected, composed.IsBroken(new Order()));
        Assert.Equal(expected, composed.IsBroken(new Order(), new BusinessRuleContext()));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void AnyOf_IsBrokenOnlyWhenEveryOperandIsBroken(bool firstBroken, bool secondBroken, bool expected)
    {
        ISpecificationRule<Order> composed = Fake("A", firstBroken).AnyOf(Fake("B", secondBroken));

        Assert.Equal(expected, composed.IsBroken(new Order()));
        Assert.Equal(expected, composed.IsBroken(new Order(), new BusinessRuleContext()));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Not_InvertsTheOperand(bool operandBroken, bool expected)
    {
        ISpecificationRule<Order> composed = Fake("A", operandBroken).Not();

        Assert.Equal(expected, composed.IsBroken(new Order()));
        Assert.Equal(expected, composed.IsBroken(new Order(), new BusinessRuleContext()));
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(true, true, true, true)]
    public void AllOf_ThreeOperands_IsBrokenWhenAnyOperandIsBroken(bool firstBroken, bool secondBroken, bool thirdBroken, bool expected)
    {
        ISpecificationRule<Order> composed = Fake("A", firstBroken).AllOf(Fake("B", secondBroken), Fake("C", thirdBroken));

        Assert.Equal(expected, composed.IsBroken(new Order()));
        Assert.Equal(expected, composed.BrokenExpression.Compile()(new Order()));
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, false, false, false)]
    public void AnyOf_ThreeOperands_IsBrokenOnlyWhenEveryOperandIsBroken(bool firstBroken, bool secondBroken, bool thirdBroken, bool expected)
    {
        ISpecificationRule<Order> composed = Fake("A", firstBroken).AnyOf(Fake("B", secondBroken), Fake("C", thirdBroken));

        Assert.Equal(expected, composed.IsBroken(new Order()));
        Assert.Equal(expected, composed.BrokenExpression.Compile()(new Order()));
    }

    [Fact]
    public void AnyOf_ThreeOperands_OrderBrokenForEveryOperand_IsBroken()
    {
        ISpecificationRule<Order> composed = new OrderIsOverdueRule().AnyOf(new OrderIsBigRule(), new OrderIsFlaggedRule());

        Assert.True(composed.IsBroken(SpecificationSamples.OverdueBigFlagged()));
    }

    [Fact]
    public void AnyOf_ThreeOperands_OrderSatisfyingOneOperand_IsNotBroken()
    {
        ISpecificationRule<Order> composed = new OrderIsOverdueRule().AnyOf(new OrderIsBigRule(), new OrderIsFlaggedRule());

        Assert.False(composed.IsBroken(SpecificationSamples.OverdueBig()));
    }

    [Fact]
    public void AllOf_ThreeOperands_OrderSatisfyingEveryOperand_IsNotBroken()
    {
        ISpecificationRule<Order> composed = new OrderIsOverdueRule().AllOf(new OrderIsBigRule(), new OrderIsFlaggedRule());

        Assert.False(composed.IsBroken(SpecificationSamples.CurrentSmall()));
        Assert.True(composed.IsBroken(SpecificationSamples.CurrentSmallFlagged()));
    }

    [Fact]
    public void Chained_AllOfThenAnyOf_NestsLeftToRight()
    {
        OrderIsOverdueRule overdue = new OrderIsOverdueRule();
        OrderIsBigRule big = new OrderIsBigRule();
        OrderIsFlaggedRule flagged = new OrderIsFlaggedRule();

        ISpecificationRule<Order> combo = overdue.AllOf(big).AnyOf(flagged);

        AnyOfSpecificationRule<Order> any = Assert.IsType<AnyOfSpecificationRule<Order>>(combo);
        Assert.IsType<AllOfSpecificationRule<Order>>(any.Operands[0]);
        Assert.Same(flagged, any.Operands[1]);
        foreach (Order order in SpecificationSamples.All())
        {
            bool expected = (overdue.IsBroken(order) || big.IsBroken(order)) && flagged.IsBroken(order);
            Assert.Equal(expected, combo.IsBroken(order));
        }
    }

    [Fact]
    public void AllOf_WithoutOthers_IsASingleOperandNodeWithTheOperandsVerdict()
    {
        OrderIsBigRule big = new OrderIsBigRule();

        ISpecificationRule<Order> composed = big.AllOf();

        AllOfSpecificationRule<Order> node = Assert.IsType<AllOfSpecificationRule<Order>>(composed);
        Assert.Single(node.Operands);
        Assert.True(composed.IsBroken(SpecificationSamples.OverdueBig()));
        Assert.False(composed.IsBroken(SpecificationSamples.OverdueSmall()));
    }

    // ---------------------------------------------------------------------
    // Expression tree shape: one lambda, one parameter, no Invoke
    // ---------------------------------------------------------------------

    [Fact]
    public void AllOf_BrokenExpression_IsOneLambdaWithAnOrElseBodyAndNoInvoke()
    {
        OrderIsOverdueRule overdue = new OrderIsOverdueRule();
        OrderIsBigRule big = new OrderIsBigRule();

        Expression<Func<Order, bool>> broken = overdue.AllOf(big).BrokenExpression;
        ExpressionShape shape = ExpressionShape.Of(broken);

        Assert.Single(broken.Parameters);
        Assert.Equal(ExpressionType.OrElse, broken.Body.NodeType);
        Assert.Single(shape.Lambdas);
        Assert.Equal(0, shape.InvocationCount);
        Assert.Equal(new[] { broken.Parameters[0] }, shape.ReferencedParameters);
    }

    [Fact]
    public void AnyOf_BrokenExpression_IsOneLambdaWithAnAndAlsoBodyAndNoInvoke()
    {
        OrderIsOverdueRule overdue = new OrderIsOverdueRule();
        OrderIsBigRule big = new OrderIsBigRule();

        Expression<Func<Order, bool>> broken = overdue.AnyOf(big).BrokenExpression;
        ExpressionShape shape = ExpressionShape.Of(broken);

        Assert.Single(broken.Parameters);
        Assert.Equal(ExpressionType.AndAlso, broken.Body.NodeType);
        Assert.Single(shape.Lambdas);
        Assert.Equal(0, shape.InvocationCount);
        Assert.Equal(new[] { broken.Parameters[0] }, shape.ReferencedParameters);
    }

    [Fact]
    public void AllOf_SplicesTheSecondOperandOntoTheFirstOperandsParameter()
    {
        OrderIsOverdueRule overdue = new OrderIsOverdueRule();
        OrderIsBigRule big = new OrderIsBigRule();
        ParameterExpression firstParameter = overdue.BrokenExpression.Parameters[0];
        ParameterExpression secondParameter = big.BrokenExpression.Parameters[0];
        Assert.NotSame(firstParameter, secondParameter);

        Expression<Func<Order, bool>> broken = overdue.AllOf(big).BrokenExpression;
        BinaryExpression body = Assert.IsAssignableFrom<BinaryExpression>(broken.Body);

        Assert.Same(firstParameter, broken.Parameters[0]);
        Assert.Same(overdue.BrokenExpression.Body, body.Left);
        Assert.DoesNotContain(secondParameter, ExpressionShape.Of(body.Right).ReferencedParameters);
        Assert.Contains(firstParameter, ExpressionShape.Of(body.Right).ReferencedParameters);
    }

    [Fact]
    public void Nested_Combinators_StillProduceOneLambdaOverOneParameter()
    {
        OrderIsOverdueRule overdue = new OrderIsOverdueRule();
        OrderIsBigRule big = new OrderIsBigRule();
        OrderIsFlaggedRule flagged = new OrderIsFlaggedRule();

        Expression<Func<Order, bool>> broken = overdue.AllOf(big).AnyOf(flagged.Not()).BrokenExpression;
        ExpressionShape shape = ExpressionShape.Of(broken);

        Assert.Single(broken.Parameters);
        Assert.Single(shape.Lambdas);
        Assert.Equal(0, shape.InvocationCount);
        Assert.Equal(new[] { broken.Parameters[0] }, shape.ReferencedParameters);
        BinaryExpression body = Assert.IsAssignableFrom<BinaryExpression>(broken.Body);
        Assert.Equal(ExpressionType.AndAlso, body.NodeType);
        Assert.Equal(ExpressionType.OrElse, body.Left.NodeType);
        Assert.Equal(ExpressionType.Not, body.Right.NodeType);
    }

    [Fact]
    public void Not_BrokenExpression_IsTheOperandsSatisfiedExpression()
    {
        OrderIsBigRule big = new OrderIsBigRule();

        ISpecificationRule<Order> inverted = big.Not();

        Assert.Same(big.SatisfiedExpression, inverted.BrokenExpression);
        Assert.Same(big.BrokenExpression, inverted.SatisfiedExpression);
    }

    [Fact]
    public void Not_Twice_DoesNotPileUpNegations()
    {
        OrderIsBigRule big = new OrderIsBigRule();

        ISpecificationRule<Order> doubled = big.Not().Not();

        Assert.Same(big.BrokenExpression, doubled.BrokenExpression);
        Assert.Same(big.SatisfiedExpression, doubled.SatisfiedExpression);
    }

    [Fact]
    public void SatisfiedExpression_OfAllOf_IsTheNegationOfItsBrokenExpression()
    {
        OrderIsOverdueRule overdue = new OrderIsOverdueRule();
        OrderIsBigRule big = new OrderIsBigRule();
        ISpecificationRule<Order> composed = overdue.AllOf(big);

        Expression<Func<Order, bool>> satisfied = composed.SatisfiedExpression;

        UnaryExpression negation = Assert.IsAssignableFrom<UnaryExpression>(satisfied.Body);
        Assert.Equal(ExpressionType.Not, negation.NodeType);
        Assert.Same(composed.BrokenExpression.Body, negation.Operand);
        Assert.Same(composed.BrokenExpression.Parameters[0], satisfied.Parameters[0]);
    }

    [Fact]
    public void BrokenExpression_OfANode_ReturnsTheSameInstanceOnEveryCall()
    {
        ISpecificationRule<Order> composed = new OrderIsOverdueRule().AllOf(new OrderIsBigRule());

        Assert.Same(composed.BrokenExpression, composed.BrokenExpression);
        Assert.Same(composed.SatisfiedExpression, composed.SatisfiedExpression);
    }

    [Fact]
    public void BrokenExpression_OfANode_ConcurrentFirstAccess_BuildsOneInstance()
    {
        ISpecificationRule<Order> node = new OrderIsOverdueRule().AllOf(new OrderIsBigRule(), new OrderIsFlaggedRule());
        Expression<Func<Order, bool>>[] observed = new Expression<Func<Order, bool>>[32];

        Parallel.For(0, observed.Length, index => observed[index] = node.BrokenExpression);

        Assert.All(observed, expression => Assert.Same(observed[0], expression));
    }

    [Fact]
    public void CompiledSatisfiedExpression_IsTheComplementOfIsBrokenForEveryNode()
    {
        OrderIsOverdueRule overdue = new OrderIsOverdueRule();
        OrderIsBigRule big = new OrderIsBigRule();
        ISpecificationRule<Order>[] nodes = { overdue.AllOf(big), overdue.AnyOf(big), overdue.Not() };

        foreach (ISpecificationRule<Order> node in nodes)
        {
            Func<Order, bool> satisfied = node.SatisfiedExpression.Compile();
            foreach (Order order in SpecificationSamples.All())
            {
                Assert.Equal(!node.IsBroken(order), satisfied(order));
            }
        }
    }

    // ---------------------------------------------------------------------
    // Metadata of the nodes
    // ---------------------------------------------------------------------

    [Fact]
    public void AllOf_SynthesisesCodeMessageNameAndDescription()
    {
        ISpecificationRule<Order> composed = new OrderIsOverdueRule().AllOf(new OrderIsBigRule());

        Assert.Equal($"ALL({OrderIsOverdueRule.RuleCode},{OrderIsBigRule.RuleCode})", composed.Code);
        Assert.Equal($"All rules must be satisfied: {OrderIsOverdueRule.RuleMessage} AND {OrderIsBigRule.RuleMessage}", composed.Message);
        Assert.Equal("AllOfSpecificationRule", composed.Name);
        Assert.Equal("Satisfied only when every operand is satisfied; broken when any operand is broken.", composed.Description);
        Assert.Equal(RuleSeverity.Error, composed.Severity);
        Assert.Equal(string.Empty, composed.Category);
        Assert.Empty(composed.Tags);
    }

    [Fact]
    public void AnyOf_SynthesisesCodeMessageNameAndDescription()
    {
        ISpecificationRule<Order> composed = new OrderIsOverdueRule().AnyOf(new OrderIsBigRule(), new OrderIsFlaggedRule());

        Assert.Equal($"ANY({OrderIsOverdueRule.RuleCode},{OrderIsBigRule.RuleCode},{OrderIsFlaggedRule.RuleCode})", composed.Code);
        Assert.Equal($"At least one rule must be satisfied: {OrderIsOverdueRule.RuleMessage} OR {OrderIsBigRule.RuleMessage} OR {OrderIsFlaggedRule.RuleMessage}", composed.Message);
        Assert.Equal("AnyOfSpecificationRule", composed.Name);
        Assert.Equal("Satisfied when any operand is satisfied; broken only when every operand is broken.", composed.Description);
    }

    [Fact]
    public void Not_SynthesisesCodeMessageNameAndDescription()
    {
        ISpecificationRule<Order> composed = new OrderIsBigRule().Not();

        Assert.Equal($"!{OrderIsBigRule.RuleCode}", composed.Code);
        Assert.Equal($"NOT: {OrderIsBigRule.RuleMessage}", composed.Message);
        Assert.Equal("NotSpecificationRule", composed.Name);
        Assert.Equal("Satisfied when the operand is broken; broken when the operand is satisfied.", composed.Description);
    }

    [Fact]
    public void Nodes_ExposeOperatorAndOperands()
    {
        OrderIsOverdueRule overdue = new OrderIsOverdueRule();
        OrderIsBigRule big = new OrderIsBigRule();

        AllOfSpecificationRule<Order> all = Assert.IsType<AllOfSpecificationRule<Order>>(overdue.AllOf(big));
        AnyOfSpecificationRule<Order> any = Assert.IsType<AnyOfSpecificationRule<Order>>(overdue.AnyOf(big));
        NotSpecificationRule<Order> not = Assert.IsType<NotSpecificationRule<Order>>(overdue.Not());

        Assert.Equal(RuleOperator.All, all.Operator);
        Assert.Equal(RuleOperator.Any, any.Operator);
        Assert.Equal(RuleOperator.Not, not.Operator);
        Assert.Equal(new ISpecificationRule<Order>[] { overdue, big }, all.Operands);
        Assert.Equal(new ISpecificationRule<Order>[] { overdue, big }, any.Operands);
        Assert.Equal(new ISpecificationRule<Order>[] { overdue }, not.Operands);
    }

    [Fact]
    public void Evaluate_OnANode_ReturnsAResultCarryingTheSynthesisedMetadata()
    {
        ISpecificationRule<Order> composed = new OrderIsOverdueRule().AllOf(new OrderIsBigRule());

        BusinessRuleResult result = composed.Evaluate(SpecificationSamples.OverdueBig());
        BusinessRuleResult withContext = composed.Evaluate(SpecificationSamples.OverdueBig(), new BusinessRuleContext());

        Assert.Equal(composed.Code, result.Code);
        Assert.Equal(composed.Message, result.Message);
        Assert.Equal("AllOfSpecificationRule", result.Name);
        Assert.Equal(composed.Code, withContext.Code);
    }

    [Fact]
    public void Not_OnASpecification_BindsToTheSpecificationOverloadNotTheTypedOne()
    {
        ISpecificationRule<Order> spec = new OrderIsBigRule();
        IBusinessRule<Order> typed = new OrderIsBigRule();

        Assert.IsType<NotSpecificationRule<Order>>(spec.Not());
        Assert.IsType<NotBusinessRule<Order>>(typed.Not());
    }

    // ---------------------------------------------------------------------
    // Interoperability with the typed checkers
    // ---------------------------------------------------------------------

    [Fact]
    public void CheckAll_WithANode_ThrowsWithTheSynthesisedCode()
    {
        ISpecificationRule<Order> composed = new OrderIsOverdueRule().AllOf(new OrderIsBigRule());

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.For(SpecificationSamples.OverdueSmall()).CheckAll(new[] { composed }));

        Assert.Equal(composed.Code, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void For_WithANode_BindsAsAnOrdinaryRule()
    {
        ISpecificationRule<Order> composed = new OrderIsOverdueRule().AnyOf(new OrderIsBigRule());

        RuleTestHelper.AssertBroken(composed.For(SpecificationSamples.OverdueBig()));
        RuleTestHelper.AssertNotBroken(composed.For(SpecificationSamples.OverdueSmall()));
    }

    // ---------------------------------------------------------------------
    // Guards
    // ---------------------------------------------------------------------

    [Fact]
    public void AllOf_NullFirst_ThrowsArgumentNullException()
    {
        ISpecificationRule<Order> first = null!;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => first.AllOf(new OrderIsBigRule()));

        Assert.Equal("first", exception.ParamName);
    }

    [Fact]
    public void AnyOf_NullFirst_ThrowsArgumentNullException()
    {
        ISpecificationRule<Order> first = null!;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => first.AnyOf(new OrderIsBigRule()));

        Assert.Equal("first", exception.ParamName);
    }

    [Fact]
    public void AllOf_NullOthersArray_ThrowsArgumentNullException()
    {
        ISpecificationRule<Order>[] others = null!;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new OrderIsBigRule().AllOf(others));

        Assert.Equal("others", exception.ParamName);
    }

    [Fact]
    public void AnyOf_NullOthersArray_ThrowsArgumentNullException()
    {
        ISpecificationRule<Order>[] others = null!;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new OrderIsBigRule().AnyOf(others));

        Assert.Equal("others", exception.ParamName);
    }

    [Fact]
    public void AllOf_NullOperandInOthers_ThrowsArgumentExceptionNamingThePosition()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new OrderIsBigRule().AllOf(null!, new OrderIsOverdueRule()));

        Assert.Equal("operands", exception.ParamName);
        Assert.Contains("position 1", exception.Message);
    }

    [Fact]
    public void AnyOf_NullOperandInOthers_ThrowsArgumentExceptionNamingThePosition()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new OrderIsBigRule().AnyOf(new OrderIsOverdueRule(), null!));

        Assert.Equal("operands", exception.ParamName);
        Assert.Contains("position 2", exception.Message);
    }

    [Fact]
    public void Not_NullRule_ThrowsArgumentNullException()
    {
        ISpecificationRule<Order> rule = null!;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => rule.Not());

        Assert.Equal("rule", exception.ParamName);
    }

    [Fact]
    public void Constructors_NullOperands_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AllOfSpecificationRule<Order>(null!));
        Assert.Throws<ArgumentNullException>(() => new AnyOfSpecificationRule<Order>(null!));
        Assert.Throws<ArgumentNullException>(() => new NotSpecificationRule<Order>(null!));
    }

    [Fact]
    public void Constructors_EmptyOperands_ThrowArgumentException()
    {
        ISpecificationRule<Order>[] empty = Array.Empty<ISpecificationRule<Order>>();

        Assert.Throws<ArgumentException>(() => new AllOfSpecificationRule<Order>(empty));
        Assert.Throws<ArgumentException>(() => new AnyOfSpecificationRule<Order>(empty));
    }

    [Fact]
    public void Constructor_DoesNotTouchTheOperandsExpressions()
    {
        CountingSpecificationRule counting = new CountingSpecificationRule();

        AllOfSpecificationRule<int> node = new AllOfSpecificationRule<int>(new[] { counting });

        Assert.Equal(0, counting.BrokenWhenReads);
        Assert.NotNull(node);
    }

    [Fact]
    public void AllOf_OperandWithNullBrokenExpression_ThrowsRuleConfigurationExceptionNamingTheOperand()
    {
        FakeSpecificationRule faulty = new FakeSpecificationRule("FAULTY", null, o => true);
        ISpecificationRule<Order> composed = new OrderIsBigRule().AllOf(faulty);

        RuleConfigurationException exception = Assert.Throws<RuleConfigurationException>(() => composed.BrokenExpression);

        Assert.Equal("FAULTY", exception.RuleCode);
    }

    [Fact]
    public void Not_OperandWithNullSatisfiedExpression_ThrowsRuleConfigurationExceptionNamingTheOperand()
    {
        FakeSpecificationRule faulty = new FakeSpecificationRule("FAULTY", o => true, null);
        ISpecificationRule<Order> composed = faulty.Not();

        RuleConfigurationException exception = Assert.Throws<RuleConfigurationException>(() => composed.IsBroken(new Order()));

        Assert.Equal("FAULTY", exception.RuleCode);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static FakeSpecificationRule Fake(string code, bool broken)
    {
        return broken
            ? new FakeSpecificationRule(code, o => true, o => false)
            : new FakeSpecificationRule(code, o => false, o => true);
    }
}
