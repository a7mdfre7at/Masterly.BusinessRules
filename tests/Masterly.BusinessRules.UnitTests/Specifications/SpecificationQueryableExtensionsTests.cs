using System.Linq.Expressions;

namespace Masterly.BusinessRules.UnitTests;

public class SpecificationQueryableExtensionsTests
{
    // ---------------------------------------------------------------------
    // Filtering an in-memory IQueryable
    // ---------------------------------------------------------------------

    [Fact]
    public void WhereBroken_ReturnsOnlyTheViolatingSubjects()
    {
        IQueryable<Order> orders = SpecificationSamples.All().AsQueryable();

        List<Order> violators = orders.WhereBroken(new OrderIsOverdueRule()).ToList();

        Assert.Equal(new[] { 1, 2 }, violators.Select(o => o.Id));
    }

    [Fact]
    public void WhereSatisfies_ReturnsOnlyTheCompliantSubjects()
    {
        IQueryable<Order> orders = SpecificationSamples.All().AsQueryable();

        List<Order> compliant = orders.WhereSatisfies(new OrderIsOverdueRule()).ToList();

        Assert.Equal(new[] { 3, 4, 5 }, compliant.Select(o => o.Id));
    }

    [Fact]
    public void WhereBroken_AndWhereSatisfies_PartitionTheSource()
    {
        IQueryable<Order> orders = SpecificationSamples.All().AsQueryable();
        ISpecificationRule<Order> rule = new OrderIsBigRule();

        List<int> broken = orders.WhereBroken(rule).Select(o => o.Id).ToList();
        List<int> satisfied = orders.WhereSatisfies(rule).Select(o => o.Id).ToList();

        Assert.Empty(broken.Intersect(satisfied));
        Assert.Equal(orders.Select(o => o.Id).OrderBy(id => id), broken.Concat(satisfied).OrderBy(id => id));
    }

    [Fact]
    public void WhereBroken_EmptySource_ReturnsEmpty()
    {
        IQueryable<Order> orders = new List<Order>().AsQueryable();

        Assert.Empty(orders.WhereBroken(new OrderIsOverdueRule()));
        Assert.Empty(orders.WhereSatisfies(new OrderIsOverdueRule()));
    }

    [Fact]
    public void WhereBroken_NothingViolates_ReturnsEmpty()
    {
        IQueryable<Order> orders = new List<Order> { SpecificationSamples.CurrentSmall() }.AsQueryable();

        Assert.Empty(orders.WhereBroken(new OrderIsOverdueRule()));
    }

    [Fact]
    public void WhereBroken_WithACombinator_MatchesTheInMemoryVerdicts()
    {
        List<Order> source = SpecificationSamples.All();
        ISpecificationRule<Order> combo = new OrderIsOverdueRule().AllOf(new OrderIsBigRule()).AnyOf(new OrderIsFlaggedRule());

        List<int> translated = source.AsQueryable().WhereBroken(combo).Select(o => o.Id).ToList();
        List<int> inMemory = source.Where(o => combo.IsBroken(o)).Select(o => o.Id).ToList();

        Assert.Equal(inMemory, translated);
    }

    [Fact]
    public void WhereSatisfies_WithACombinator_MatchesTheInMemoryVerdicts()
    {
        List<Order> source = SpecificationSamples.All();
        ISpecificationRule<Order> combo = new OrderIsOverdueRule().AllOf(new OrderIsBigRule()).AnyOf(new OrderIsFlaggedRule().Not());

        List<int> translated = source.AsQueryable().WhereSatisfies(combo).Select(o => o.Id).ToList();
        List<int> inMemory = source.Where(o => !combo.IsBroken(o)).Select(o => o.Id).ToList();

        Assert.Equal(inMemory, translated);
    }

    [Fact]
    public void WhereBroken_AgreesWithCheckAllForEverySubject()
    {
        List<Order> source = SpecificationSamples.All();
        ISpecificationRule<Order> rule = new OrderIsOverdueRule();
        HashSet<int> violators = source.AsQueryable().WhereBroken(rule).Select(o => o.Id).ToHashSet();

        foreach (Order order in source)
        {
            RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(order, new[] { rule });
            Assert.Equal(violators.Contains(order.Id), result.HasBrokenRules);
        }
    }

    // ---------------------------------------------------------------------
    // Expression tree handed to the provider
    // ---------------------------------------------------------------------

    [Fact]
    public void WhereBroken_AppendsASingleWhereCallQuotingTheRulesBrokenExpression()
    {
        IQueryable<Order> orders = SpecificationSamples.All().AsQueryable();
        OrderIsOverdueRule rule = new OrderIsOverdueRule();

        IQueryable<Order> query = orders.WhereBroken(rule);

        MethodCallExpression where = Assert.IsAssignableFrom<MethodCallExpression>(query.Expression);
        Assert.Equal(nameof(Queryable.Where), where.Method.Name);
        Assert.Same(orders.Expression, where.Arguments[0]);
        UnaryExpression quote = Assert.IsAssignableFrom<UnaryExpression>(where.Arguments[1]);
        Assert.Equal(ExpressionType.Quote, quote.NodeType);
        Assert.Same(rule.BrokenExpression, quote.Operand);
    }

    [Fact]
    public void WhereSatisfies_AppendsASingleWhereCallQuotingTheRulesSatisfiedExpression()
    {
        IQueryable<Order> orders = SpecificationSamples.All().AsQueryable();
        OrderIsOverdueRule rule = new OrderIsOverdueRule();

        IQueryable<Order> query = orders.WhereSatisfies(rule);

        MethodCallExpression where = Assert.IsAssignableFrom<MethodCallExpression>(query.Expression);
        Assert.Equal(nameof(Queryable.Where), where.Method.Name);
        UnaryExpression quote = Assert.IsAssignableFrom<UnaryExpression>(where.Arguments[1]);
        Assert.Same(rule.SatisfiedExpression, quote.Operand);
    }

    [Fact]
    public void WhereBroken_WithACombinator_HandsTheProviderOneLambdaOverOneParameterWithoutInvoke()
    {
        IQueryable<Order> orders = SpecificationSamples.All().AsQueryable();
        ISpecificationRule<Order> combo = new OrderIsOverdueRule().AllOf(new OrderIsBigRule()).AnyOf(new OrderIsFlaggedRule().Not());

        IQueryable<Order> query = orders.WhereBroken(combo);
        ExpressionShape shape = ExpressionShape.Of(query.Expression);

        LambdaExpression predicate = Assert.Single(shape.Lambdas);
        Assert.Single(predicate.Parameters);
        Assert.Equal(0, shape.InvocationCount);
        Assert.Equal(new[] { predicate.Parameters[0] }, shape.ReferencedParameters);
    }

    [Fact]
    public void WhereSatisfies_WithACombinator_HandsTheProviderTheNegatedSingleLambda()
    {
        IQueryable<Order> orders = SpecificationSamples.All().AsQueryable();
        ISpecificationRule<Order> combo = new OrderIsOverdueRule().AllOf(new OrderIsBigRule());

        IQueryable<Order> query = orders.WhereSatisfies(combo);
        ExpressionShape shape = ExpressionShape.Of(query.Expression);

        LambdaExpression predicate = Assert.Single(shape.Lambdas);
        Assert.Equal(ExpressionType.Not, predicate.Body.NodeType);
        Assert.Equal(0, shape.InvocationCount);
        Assert.Equal(new[] { predicate.Parameters[0] }, shape.ReferencedParameters);
    }

    [Fact]
    public void WhereBroken_IsComposableWithFurtherQueryOperators()
    {
        IQueryable<Order> orders = SpecificationSamples.All().AsQueryable();

        List<int> ids = orders.WhereBroken(new OrderIsOverdueRule()).Where(o => o.Total > 100m).OrderByDescending(o => o.Id).Select(o => o.Id).ToList();

        Assert.Equal(new[] { 2 }, ids);
    }

    // ---------------------------------------------------------------------
    // Guards
    // ---------------------------------------------------------------------

    [Fact]
    public void WhereBroken_NullSource_ThrowsArgumentNullException()
    {
        IQueryable<Order> source = null!;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => source.WhereBroken(new OrderIsOverdueRule()));

        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public void WhereSatisfies_NullSource_ThrowsArgumentNullException()
    {
        IQueryable<Order> source = null!;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => source.WhereSatisfies(new OrderIsOverdueRule()));

        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public void WhereBroken_NullRule_ThrowsArgumentNullException()
    {
        IQueryable<Order> orders = SpecificationSamples.All().AsQueryable();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => orders.WhereBroken(null!));

        Assert.Equal("rule", exception.ParamName);
    }

    [Fact]
    public void WhereSatisfies_NullRule_ThrowsArgumentNullException()
    {
        IQueryable<Order> orders = SpecificationSamples.All().AsQueryable();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => orders.WhereSatisfies(null!));

        Assert.Equal("rule", exception.ParamName);
    }

    [Fact]
    public void WhereBroken_RuleWithNullBrokenExpression_ThrowsRuleConfigurationException()
    {
        IQueryable<Order> orders = SpecificationSamples.All().AsQueryable();
        FakeSpecificationRule faulty = new FakeSpecificationRule("FAULTY", null, o => true);

        RuleConfigurationException exception = Assert.Throws<RuleConfigurationException>(() => orders.WhereBroken(faulty));

        Assert.Equal("FAULTY", exception.RuleCode);
    }

    [Fact]
    public void WhereSatisfies_RuleWithNullSatisfiedExpression_ThrowsRuleConfigurationException()
    {
        IQueryable<Order> orders = SpecificationSamples.All().AsQueryable();
        FakeSpecificationRule faulty = new FakeSpecificationRule("FAULTY", o => true, null);

        RuleConfigurationException exception = Assert.Throws<RuleConfigurationException>(() => orders.WhereSatisfies(faulty));

        Assert.Equal("FAULTY", exception.RuleCode);
    }
}
