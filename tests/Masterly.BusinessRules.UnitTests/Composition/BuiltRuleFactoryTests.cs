namespace Masterly.BusinessRules.UnitTests;

public class BuiltRuleFactoryTests
{
    private const string Code = "ORDER.LIMIT";
    private const string Message = "Order exceeds the limit";
    private const string Name = "Order limit";
    private const string Description = "Orders above the limit need approval";
    private const string Category = "Orders";
    private const string LimitName = "limit";
    private const decimal Fallback = 100m;
    private const decimal TenantLimit = 500m;

    private static readonly RuleScope Acme = RuleScope.Tenant("acme");

    [Fact]
    public void WithRuleFactory_NullFactory_ThrowsArgumentNullException()
    {
        BusinessRuleBuilder builder = BusinessRuleBuilder.Create(Code);

        Assert.Throws<ArgumentNullException>(() => builder.WithRuleFactory(null!));
    }

    [Fact]
    public void Build_WithRuleFactory_ReturnsTheFactoryRuleWithoutACondition()
    {
        IBusinessRule created = RuleTestHelper.CreateBrokenRule(Code, Message);
        CapturingFactory factory = new CapturingFactory(_ => created);

        IBusinessRule built = BusinessRuleBuilder.Create(Code).WithRuleFactory(factory).Build();

        Assert.Same(created, built);
        Assert.Equal(1, factory.Calls);
    }

    [Fact]
    public void Build_WithRuleFactory_PassesTheConfiguredMetadata()
    {
        CapturingFactory factory = new CapturingFactory(RuleTestHelper.CreatePassingRule);

        BusinessRuleBuilder.Create(Code)
            .WithMessage(Message)
            .WithSeverity(RuleSeverity.Warning)
            .WithName(Name)
            .WithDescription(Description)
            .WithCategory(Category)
            .WithTags("a", "b")
            .WithRuleFactory(factory)
            .Build();

        BuiltRuleDefinition definition = Assert.IsType<BuiltRuleDefinition>(factory.Definition);
        Assert.Equal(Code, definition.Code);
        Assert.Equal(Message, definition.Message);
        Assert.Equal(RuleSeverity.Warning, definition.Severity);
        Assert.Equal(Name, definition.Name);
        Assert.Equal(Description, definition.Description);
        Assert.Equal(Category, definition.Category);
        Assert.Equal(new[] { "a", "b" }, definition.Tags);
        Assert.False(definition.HasDetails);
    }

    [Fact]
    public void Build_WithRuleFactoryAndNoName_DefinitionNameDefaultsToCode()
    {
        CapturingFactory factory = new CapturingFactory(RuleTestHelper.CreatePassingRule);

        BusinessRuleBuilder.Create(Code).WithRuleFactory(factory).Build();

        Assert.Equal(Code, factory.Definition!.Name);
        Assert.Equal(string.Empty, factory.Definition.Description);
        Assert.Empty(factory.Definition.Tags);
    }

    [Fact]
    public void Build_WithRuleFactory_TagsAreSnapshotAtBuildTime()
    {
        CapturingFactory factory = new CapturingFactory(RuleTestHelper.CreatePassingRule);
        BusinessRuleBuilder builder = BusinessRuleBuilder.Create(Code).WithTags("first").WithRuleFactory(factory);

        builder.Build();
        builder.WithTags("second");

        Assert.Equal(new[] { "first" }, factory.Definition!.Tags);
    }

    [Fact]
    public void Build_FactoryReturnsNull_ThrowsInvalidOperationException()
    {
        CapturingFactory factory = new CapturingFactory(_ => null!);

        Assert.Throws<InvalidOperationException>(() => BusinessRuleBuilder.Create(Code).WithRuleFactory(factory).Build());
    }

    [Fact]
    public void When_AfterWithRuleFactory_ReplacesTheFactory()
    {
        CapturingFactory factory = new CapturingFactory(RuleTestHelper.CreatePassingRule);

        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WithRuleFactory(factory).When(() => true).Build();

        Assert.Equal(0, factory.Calls);
        Assert.True(rule.IsBroken());
    }

    [Fact]
    public void WithRuleFactory_AfterWhen_ReplacesTheCondition()
    {
        IBusinessRule created = RuleTestHelper.CreatePassingRule(Code);
        CapturingFactory factory = new CapturingFactory(_ => created);

        IBusinessRule rule = BusinessRuleBuilder.Create(Code).When(() => true).WithRuleFactory(factory).Build();

        Assert.Same(created, rule);
    }

    [Fact]
    public void Definition_ResolveParameters_WithoutProvider_UsesTheFallback()
    {
        CapturingFactory factory = new CapturingFactory(RuleTestHelper.CreatePassingRule);
        BusinessRuleBuilder.Create(Code).WithParameter(LimitName, Fallback).WithRuleFactory(factory).Build();

        RuleParameters parameters = factory.Definition!.ResolveParameters(new BusinessRuleContext());

        Assert.Equal(Fallback, parameters.Get<decimal>(LimitName));
    }

    [Fact]
    public void Definition_ResolveParameters_UsesTheInjectedProviderForTheContextScope()
    {
        InMemoryRuleParameterProvider provider = RuleTestHelper.CreateParameterProvider(LimitName, TenantLimit, Acme);
        CapturingFactory factory = new CapturingFactory(RuleTestHelper.CreatePassingRule);
        BusinessRuleBuilder.Create(Code).WithParameters(provider).WithParameter(LimitName, Fallback).WithRuleFactory(factory).Build();
        BusinessRuleContext acme = new BusinessRuleContext();
        acme.Set(RuleParameters.ScopeKey, Acme);

        RuleParameters parameters = factory.Definition!.ResolveParameters(acme);

        Assert.Equal(TenantLimit, parameters.Get<decimal>(LimitName));
        Assert.Equal(Acme, parameters.Scope);
    }

    [Fact]
    public void Definition_ResolveParameters_NullContext_ResolvesTheGlobalScope()
    {
        InMemoryRuleParameterProvider provider = RuleTestHelper.CreateParameterProvider(LimitName, TenantLimit);
        CapturingFactory factory = new CapturingFactory(RuleTestHelper.CreatePassingRule);
        BusinessRuleBuilder.Create(Code).WithParameters(provider).WithParameter(LimitName, Fallback).WithRuleFactory(factory).Build();

        RuleParameters parameters = factory.Definition!.ResolveParameters(null);

        Assert.Equal(TenantLimit, parameters.Get<decimal>(LimitName));
    }

    [Fact]
    public void Definition_ResolveParameters_NothingDeclared_ReturnsEmpty()
    {
        CapturingFactory factory = new CapturingFactory(RuleTestHelper.CreatePassingRule);
        BusinessRuleBuilder.Create(Code).WithRuleFactory(factory).Build();

        RuleParameters parameters = factory.Definition!.ResolveParameters(new BusinessRuleContext());

        Assert.Equal(0, parameters.Count);
    }

    [Fact]
    public void Definition_CreateResult_AttachesTheConfiguredDetails()
    {
        CapturingFactory factory = new CapturingFactory(RuleTestHelper.CreatePassingRule);
        BusinessRuleBuilder.Create(Code)
            .WithMessage(Message)
            .WithActual(context => context.Get<decimal>("amount"))
            .WithExpected(Fallback)
            .WithRemediation("Split the order")
            .WithRuleFactory(factory)
            .Build();
        IBusinessRule rule = RuleTestHelper.CreateBrokenRule(Code, Message);

        BusinessRuleResult result = factory.Definition!.CreateResult(rule, RuleTestHelper.CreateContext(("amount", 120m)));

        Assert.True(factory.Definition.HasDetails);
        Assert.Equal(Code, result.Code);
        Assert.Equal(120m, result.Actual);
        Assert.Equal(Fallback, result.Expected);
        Assert.Equal("Split the order", result.Remediation);
    }

    [Fact]
    public void Definition_CreateResult_ThrowingDetail_DegradesToResultWithoutDetails()
    {
        CapturingFactory factory = new CapturingFactory(RuleTestHelper.CreatePassingRule);
        BusinessRuleBuilder.Create(Code).WithActual(_ => throw new InvalidOperationException()).WithRuleFactory(factory).Build();

        BusinessRuleResult result = factory.Definition!.CreateResult(RuleTestHelper.CreateBrokenRule(Code), null);

        Assert.Equal(Code, result.Code);
        Assert.Null(result.Actual);
    }

    [Fact]
    public void Definition_CreateResult_NullRule_ThrowsArgumentNullException()
    {
        CapturingFactory factory = new CapturingFactory(RuleTestHelper.CreatePassingRule);
        BusinessRuleBuilder.Create(Code).WithRuleFactory(factory).Build();

        Assert.Throws<ArgumentNullException>(() => factory.Definition!.CreateResult(null!, null));
    }

    private sealed class CapturingFactory : IBuiltRuleFactory
    {
        private readonly Func<BuiltRuleDefinition, IBusinessRule> _create;

        public CapturingFactory(Func<string, string, IBusinessRule> create)
            : this(definition => create(definition.Code, definition.Message))
        {
        }

        public CapturingFactory(Func<BuiltRuleDefinition, IBusinessRule> create)
        {
            _create = create;
        }

        public BuiltRuleDefinition? Definition { get; private set; }

        public int Calls { get; private set; }

        public IBusinessRule Create(BuiltRuleDefinition definition)
        {
            Calls++;
            Definition = definition;
            return _create(definition);
        }
    }
}
