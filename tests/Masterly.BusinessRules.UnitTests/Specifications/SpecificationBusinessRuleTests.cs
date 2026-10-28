using System.Linq.Expressions;
using System.Reflection;

namespace Masterly.BusinessRules.UnitTests;

public class SpecificationBusinessRuleTests
{
    // ---------------------------------------------------------------------
    // Metadata
    // ---------------------------------------------------------------------

    [Fact]
    public void SpecificationBusinessRule_HasDefaultMetadata()
    {
        OrderIsOverdueRule rule = new OrderIsOverdueRule();

        Assert.Equal(OrderIsOverdueRule.RuleCode, rule.Code);
        Assert.Equal(OrderIsOverdueRule.RuleMessage, rule.Message);
        Assert.Equal(RuleSeverity.Error, rule.Severity);
        Assert.Equal(nameof(OrderIsOverdueRule), rule.Name);
        Assert.Equal(string.Empty, rule.Description);
        Assert.Equal(string.Empty, rule.Category);
        Assert.Empty(rule.Tags);
    }

    [Fact]
    public void SpecificationBusinessRule_CanOverrideMetadata()
    {
        OrderIsFlaggedRule rule = new OrderIsFlaggedRule();

        Assert.Equal(RuleSeverity.Warning, rule.Severity);
        Assert.Equal("Flagged", rule.Name);
        Assert.Equal("Flags need a manual look", rule.Description);
        Assert.Equal("Review", rule.Category);
        Assert.Equal(new[] { "review", "manual" }, rule.Tags);
    }

    [Fact]
    public void SpecificationBusinessRule_IsASubjectTypedRuleAndASpecification()
    {
        OrderIsOverdueRule rule = new OrderIsOverdueRule();

        Assert.IsAssignableFrom<IBusinessRule<Order>>(rule);
        Assert.IsAssignableFrom<ISpecificationRule<Order>>(rule);
        Assert.IsAssignableFrom<BaseBusinessRule<Order>>(rule);
    }

    // ---------------------------------------------------------------------
    // IsBroken
    // ---------------------------------------------------------------------

    [Fact]
    public void IsBroken_SubjectViolatesPredicate_ReturnsTrue()
    {
        OrderIsOverdueRule rule = new OrderIsOverdueRule();

        bool broken = rule.IsBroken(SpecificationSamples.OverdueSmall());

        Assert.True(broken);
    }

    [Fact]
    public void IsBroken_SubjectSatisfiesPredicate_ReturnsFalse()
    {
        OrderIsOverdueRule rule = new OrderIsOverdueRule();

        Assert.False(rule.IsBroken(SpecificationSamples.CurrentSmall()));
        Assert.False(rule.IsBroken(SpecificationSamples.PaidBigFlagged()));
    }

    [Fact]
    public void IsBroken_WithContext_IgnoresContextAndEvaluatesTheSubject()
    {
        OrderIsOverdueRule rule = new OrderIsOverdueRule();
        BusinessRuleContext context = RuleTestHelper.CreateContext(("anything", 1));

        Assert.True(rule.IsBroken(SpecificationSamples.OverdueSmall(), context));
        Assert.False(rule.IsBroken(SpecificationSamples.CurrentSmall(), context));
    }

    [Fact]
    public void IsBroken_ValueTypeSubject_IsSupported()
    {
        CountingSpecificationRule rule = new CountingSpecificationRule();

        Assert.True(rule.IsBroken(-1));
        Assert.False(rule.IsBroken(0));
        Assert.False(rule.IsBroken(1));
    }

    [Fact]
    public void IsBroken_BothOverloads_AreSealed()
    {
        MethodInfo subjectOnly = typeof(SpecificationBusinessRule<Order>).GetMethod(nameof(SpecificationBusinessRule<Order>.IsBroken), new[] { typeof(Order) })!;
        MethodInfo withContext = typeof(SpecificationBusinessRule<Order>).GetMethod(nameof(SpecificationBusinessRule<Order>.IsBroken), new[] { typeof(Order), typeof(BusinessRuleContext) })!;

        Assert.True(subjectOnly.IsFinal);
        Assert.True(withContext.IsFinal);
    }

    // ---------------------------------------------------------------------
    // Lazy read and compile
    // ---------------------------------------------------------------------

    [Fact]
    public void Constructor_DoesNotReadBrokenWhen()
    {
        CountingSpecificationRule rule = new CountingSpecificationRule();

        Assert.Equal(0, rule.BrokenWhenReads);
    }

    [Fact]
    public void BrokenWhen_IsReadOnceAcrossEvaluationsAndExpressionAccesses()
    {
        CountingSpecificationRule rule = new CountingSpecificationRule();

        rule.IsBroken(-1);
        rule.IsBroken(5);
        rule.IsBroken(7, new BusinessRuleContext());
        Expression<Func<int, bool>> broken = rule.BrokenExpression;
        Expression<Func<int, bool>> satisfied = rule.SatisfiedExpression;

        Assert.Equal(1, rule.BrokenWhenReads);
        Assert.NotNull(broken);
        Assert.NotNull(satisfied);
    }

    [Fact]
    public void IsBroken_ConcurrentFirstUse_ReadsBrokenWhenOnceAndAgreesOnEveryThread()
    {
        CountingSpecificationRule rule = new CountingSpecificationRule();
        bool[] verdicts = new bool[64];

        Parallel.For(0, verdicts.Length, index => verdicts[index] = rule.IsBroken(-1));

        Assert.Equal(1, rule.BrokenWhenReads);
        Assert.DoesNotContain(false, verdicts);
    }

    [Fact]
    public void BrokenExpression_ReturnsTheSameInstanceOnEveryCall()
    {
        OrderIsOverdueRule rule = new OrderIsOverdueRule();

        Expression<Func<Order, bool>> first = rule.BrokenExpression;
        Expression<Func<Order, bool>> second = rule.BrokenExpression;

        Assert.Same(first, second);
        Assert.Same(rule.SatisfiedExpression, rule.SatisfiedExpression);
    }

    [Fact]
    public void BrokenExpression_IsTheBrokenWhenLambdaWithOneParameter()
    {
        OrderIsBigRule rule = new OrderIsBigRule();

        Expression<Func<Order, bool>> broken = rule.BrokenExpression;
        ExpressionShape shape = ExpressionShape.Of(broken);

        Assert.Single(broken.Parameters);
        Assert.Equal(ExpressionType.GreaterThan, broken.Body.NodeType);
        Assert.Equal(0, shape.InvocationCount);
        Assert.Equal(new[] { broken.Parameters[0] }, shape.ReferencedParameters);
    }

    // ---------------------------------------------------------------------
    // SatisfiedExpression
    // ---------------------------------------------------------------------

    [Fact]
    public void SatisfiedExpression_IsTheNegationOfBrokenOverTheSameParameter()
    {
        OrderIsOverdueRule rule = new OrderIsOverdueRule();

        Expression<Func<Order, bool>> broken = rule.BrokenExpression;
        Expression<Func<Order, bool>> satisfied = rule.SatisfiedExpression;

        Assert.Single(satisfied.Parameters);
        Assert.Same(broken.Parameters[0], satisfied.Parameters[0]);
        UnaryExpression negation = Assert.IsAssignableFrom<UnaryExpression>(satisfied.Body);
        Assert.Equal(ExpressionType.Not, negation.NodeType);
        Assert.Same(broken.Body, negation.Operand);
        Assert.Equal(0, ExpressionShape.Of(satisfied).InvocationCount);
    }

    [Fact]
    public void SatisfiedExpression_Compiled_IsTheComplementOfIsBrokenForEverySample()
    {
        OrderIsOverdueRule rule = new OrderIsOverdueRule();
        Func<Order, bool> satisfied = rule.SatisfiedExpression.Compile();

        foreach (Order order in SpecificationSamples.All())
        {
            Assert.Equal(!rule.IsBroken(order), satisfied(order));
        }
    }

    // ---------------------------------------------------------------------
    // Null BrokenWhen
    // ---------------------------------------------------------------------

    [Fact]
    public void IsBroken_NullBrokenWhen_ThrowsRuleConfigurationExceptionNamingTheCode()
    {
        NullBrokenWhenRule rule = new NullBrokenWhenRule();

        RuleConfigurationException exception = Assert.Throws<RuleConfigurationException>(() => rule.IsBroken(1));

        Assert.Equal(NullBrokenWhenRule.RuleCode, exception.RuleCode);
        Assert.Contains("BrokenWhen", exception.Message);
    }

    [Fact]
    public void BrokenExpression_NullBrokenWhen_ThrowsRuleConfigurationException()
    {
        NullBrokenWhenRule rule = new NullBrokenWhenRule();

        Assert.Throws<RuleConfigurationException>(() => rule.BrokenExpression);
        Assert.Throws<RuleConfigurationException>(() => rule.SatisfiedExpression);
    }

    // ---------------------------------------------------------------------
    // Evaluate and Describe
    // ---------------------------------------------------------------------

    [Fact]
    public void Evaluate_ReturnsResultWithMetadataAndDescribeDetails()
    {
        OrderIsFlaggedRule rule = new OrderIsFlaggedRule();
        Order order = SpecificationSamples.PaidBigFlagged();

        BusinessRuleResult result = rule.Evaluate(order);

        Assert.Equal(OrderIsFlaggedRule.RuleCode, result.Code);
        Assert.Equal(OrderIsFlaggedRule.RuleMessage, result.Message);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal("Flagged", result.Name);
        Assert.Equal("Review", result.Category);
        Assert.Equal($"Order {order.Id} is flagged", result.Detail);
        Assert.Equal(order.Id, result.Actual);
    }

    [Fact]
    public void Evaluate_WithContext_ReturnsResultWithMetadata()
    {
        OrderIsOverdueRule rule = new OrderIsOverdueRule();

        BusinessRuleResult result = rule.Evaluate(SpecificationSamples.OverdueSmall(), new BusinessRuleContext());

        Assert.Equal(OrderIsOverdueRule.RuleCode, result.Code);
        Assert.Equal(nameof(OrderIsOverdueRule), result.Name);
    }

    // ---------------------------------------------------------------------
    // Interoperability with the typed world
    // ---------------------------------------------------------------------

    [Fact]
    public void For_BindsTheSpecificationToASubjectAsAnOrdinaryRule()
    {
        OrderIsOverdueRule rule = new OrderIsOverdueRule();

        IBusinessRule bound = rule.For(SpecificationSamples.OverdueSmall());

        RuleTestHelper.AssertBroken(bound);
        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => bound.Check());
        Assert.Equal(OrderIsOverdueRule.RuleCode, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void CheckAll_TypedOverload_ThrowsWithTheSpecificationCodes()
    {
        ISpecificationRule<Order>[] rules = { new OrderIsOverdueRule(), new OrderIsBigRule() };

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.For(SpecificationSamples.OverdueBig()).CheckAll(rules));

        Assert.Equal(new[] { OrderIsOverdueRule.RuleCode, OrderIsBigRule.RuleCode }, exception.BrokenRules.Select(r => r.Code));
    }

    [Fact]
    public void EvaluateAll_TypedOverload_PassingSubject_AllPassed()
    {
        ISpecificationRule<Order>[] rules = { new OrderIsOverdueRule(), new OrderIsBigRule() };

        RuleEvaluationResult result = BusinessRuleChecker.For(SpecificationSamples.CurrentSmall()).EvaluateAll(rules);

        Assert.True(result.AllPassed);
        Assert.Equal(2, result.PassedRules.Count);
    }

    [Fact]
    public void TypedAnd_OnASpecification_IsAnOrdinaryTypedRuleNotASpecification()
    {
        OrderIsOverdueRule overdue = new OrderIsOverdueRule();
        OrderIsBigRule big = new OrderIsBigRule();

        IBusinessRule<Order> composed = overdue.And(big);

        Assert.IsNotAssignableFrom<ISpecificationRule<Order>>(composed);
        Assert.True(composed.IsBroken(SpecificationSamples.OverdueBig()));
        Assert.False(composed.IsBroken(SpecificationSamples.OverdueSmall()));
    }

    [Fact]
    public void Cached_OnABoundSpecification_ServesTheSameVerdict()
    {
        CountingSpecificationRule rule = new CountingSpecificationRule();
        IBusinessRule cached = rule.For(-1).Cached(TimeSpan.FromMinutes(1));

        Assert.True(cached.IsBroken());
        Assert.True(cached.IsBroken());
        Assert.Equal(1, rule.BrokenWhenReads);
    }
}
