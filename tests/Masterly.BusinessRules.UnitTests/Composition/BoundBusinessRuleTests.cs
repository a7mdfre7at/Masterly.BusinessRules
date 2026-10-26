namespace Masterly.BusinessRules.UnitTests;

public class BoundBusinessRuleTests
{
    private const string Code = "ORDER.TOO_LARGE";
    private const string Message = "Order has too many items.";
    private const string Name = "Too large";
    private const string Description = "Checks the item count";
    private const string Category = "Orders";
    private const string LimitKey = "limit";
    private const int DefaultLimit = 2;

    // ---------------------------------------------------------------------
    // Construction
    // ---------------------------------------------------------------------

    [Fact]
    public void For_ReturnsBoundBusinessRuleOverRuleAndSubject()
    {
        IBusinessRule<int> rule = CreateRule();

        BoundBusinessRule<int> bound = rule.For(5);

        Assert.Same(rule, bound.Rule);
        Assert.Equal(5, bound.Subject);
        Assert.IsAssignableFrom<IBusinessRule>(bound);
    }

    [Fact]
    public void For_NullRule_ThrowsArgumentNullException()
    {
        IBusinessRule<int> rule = null!;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => rule.For(1));

        Assert.Equal("rule", exception.ParamName);
    }

    [Fact]
    public void Constructor_NullRule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BoundBusinessRule<int>(null!, 1));
    }

    [Fact]
    public void For_NullSubject_IsAllowedAndHandedToTheRule()
    {
        IBusinessRule<string> rule = BusinessRuleBuilder<string>.Create("S.NULL").WithMessage("m").When(value => value == null).Build();

        IBusinessRule bound = rule.For(null!);

        Assert.True(bound.IsBroken());
    }

    // ---------------------------------------------------------------------
    // Metadata forwarding
    // ---------------------------------------------------------------------

    [Fact]
    public void BoundRule_ForwardsAllSevenMetadataMembers()
    {
        IBusinessRule bound = CreateRule().For(1);

        Assert.Equal(Code, bound.Code);
        Assert.Equal(Message, bound.Message);
        Assert.Equal(RuleSeverity.Warning, bound.Severity);
        Assert.Equal(Name, bound.Name);
        Assert.Equal(Description, bound.Description);
        Assert.Equal(Category, bound.Category);
        Assert.Equal(new[] { "a", "b" }, bound.Tags);
    }

    // ---------------------------------------------------------------------
    // Evaluation
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(3, true)]
    [InlineData(1, false)]
    public void IsBroken_EvaluatesTypedRuleAgainstBoundSubject(int itemCount, bool expected)
    {
        IBusinessRule bound = CreateRule().For(itemCount);

        Assert.Equal(expected, bound.IsBroken());
    }

    [Fact]
    public void IsBroken_WithContext_ForwardsSubjectAndContext()
    {
        IBusinessRule bound = CreateRule().For(5);

        Assert.True(bound.IsBroken(RuleTestHelper.CreateContext((LimitKey, 4))));
        Assert.False(bound.IsBroken(RuleTestHelper.CreateContext((LimitKey, 10))));
    }

    [Fact]
    public void Check_WhenBroken_ThrowsWithTheRuleResult()
    {
        IBusinessRule bound = CreateRule().For(3);

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => bound.Check());

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(Code, result.Code);
        Assert.Equal(Name, result.Name);
        Assert.Equal(Category, result.Category);
    }

    [Fact]
    public void Check_WithContext_WhenBroken_ThrowsWithContextAwareDetails()
    {
        IBusinessRule bound = CreateRule().For(5);

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => bound.Check(RuleTestHelper.CreateContext((LimitKey, 4))));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(5, result.Actual);
        Assert.Equal(4, result.Expected);
    }

    [Fact]
    public void Check_WhenPassing_DoesNotThrow()
    {
        IBusinessRule bound = CreateRule().For(1);

        bound.Check();
        bound.Check(new BusinessRuleContext());
    }

    [Fact]
    public void Evaluate_IsAPureFactoryForwardingToTheTypedRule()
    {
        int evaluations = 0;
        IBusinessRule<int> rule = BusinessRuleBuilder<int>.Create(Code).WithMessage(Message).When(_ => { evaluations++; return true; }).Build();
        IBusinessRule bound = rule.For(1);

        BusinessRuleResult result = bound.Evaluate();

        Assert.Equal(0, evaluations);
        Assert.Equal(Code, result.Code);
        Assert.Equal(Message, result.Message);
    }

    [Fact]
    public void Evaluate_WithContext_ForwardsSubjectAndContextToTheFactory()
    {
        IBusinessRule bound = CreateRule().For(9);

        BusinessRuleResult result = bound.Evaluate(RuleTestHelper.CreateContext((LimitKey, 3)));

        Assert.Equal(9, result.Actual);
        Assert.Equal(3, result.Expected);
    }

    // ---------------------------------------------------------------------
    // Interoperability with the non-generic world
    // ---------------------------------------------------------------------

    [Fact]
    public void BoundRule_CanBeCached()
    {
        int evaluations = 0;
        IBusinessRule<int> rule = BusinessRuleBuilder<int>.Create(Code).WithMessage(Message).When(_ => { evaluations++; return true; }).Build();
        IBusinessRule cached = rule.For(1).Cached(TimeSpan.FromMinutes(1));

        Assert.True(cached.IsBroken());
        Assert.True(cached.IsBroken());

        Assert.Equal(1, evaluations);
    }

    [Fact]
    public void BoundRule_CanBeSeverityOverridden()
    {
        IBusinessRule softened = CreateRule().For(3).WithSeverity(RuleSeverity.Info);

        Assert.Equal(RuleSeverity.Info, softened.Severity);
        Assert.Equal(Code, softened.Code);
        Assert.True(softened.IsBroken());
    }

    [Fact]
    public void BoundRule_CanBeComposedWithNonGenericOperators()
    {
        IBusinessRule composed = CreateRule().For(3).And(RuleTestHelper.CreateBrokenRule("OTHER"));

        Assert.True(composed.IsBroken());
        Assert.Equal($"{Code}+OTHER", composed.Code);
    }

    [Fact]
    public void BoundRule_RunsThroughTheChecker()
    {
        IBusinessRule[] rules = { CreateRule().For(3), CreateRule().For(1) };

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rules);

        Assert.Equal(Code, Assert.Single(result.BrokenRules).Code);
        Assert.Same(rules[1], Assert.Single(result.PassedRules));
    }

    [Fact]
    public void BoundRule_ExplainsAsALeaf()
    {
        IBusinessRule bound = CreateRule().For(3);

        RuleExplanation explanation = bound.Explain();

        Assert.True(explanation.IsBroken);
        Assert.Equal(Code, explanation.Code);
        Assert.Empty(explanation.Children);
    }

    [Fact]
    public void BoundRules_OverTheSameInstance_EvaluateIndependently()
    {
        IBusinessRule<int> rule = CreateRule();
        IBusinessRule first = rule.For(3);
        IBusinessRule second = rule.For(1);

        Assert.True(first.IsBroken());
        Assert.False(second.IsBroken());
    }

    private static IBusinessRule<int> CreateRule()
    {
        return BusinessRuleBuilder<int>.Create(Code)
            .WithMessage(Message)
            .WithSeverity(RuleSeverity.Warning)
            .WithName(Name)
            .WithDescription(Description)
            .WithCategory(Category)
            .WithTags("a", "b")
            .WithActual((count, _) => count)
            .WithExpected((_, ctx) => ctx.TryGet(LimitKey, out int limit) ? limit : DefaultLimit)
            .When((count, ctx) => count > (ctx.TryGet(LimitKey, out int limit) ? limit : DefaultLimit))
            .Build();
    }
}
