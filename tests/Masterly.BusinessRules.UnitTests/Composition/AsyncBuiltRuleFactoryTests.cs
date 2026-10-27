namespace Masterly.BusinessRules.UnitTests;

public class AsyncBuiltRuleFactoryTests
{
    private const string Code = "ORDER.LIMIT.ASYNC";
    private const string Message = "Order exceeds the limit";
    private const string Name = "Order limit";
    private const string Description = "Orders above the limit need approval";
    private const string Category = "Orders";
    private const string LimitName = "limit";
    private const decimal Fallback = 100m;
    private const decimal TenantLimit = 500m;
    private const decimal RemoteLimit = 900m;

    private static readonly RuleScope Acme = RuleScope.Tenant("acme");

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public void WithRuleFactory_NullFactory_ThrowsArgumentNullException()
    {
        AsyncBusinessRuleBuilder builder = AsyncBusinessRuleBuilder.Create(Code);

        Assert.Throws<ArgumentNullException>(() => builder.WithRuleFactory(null!));
    }

    [Fact]
    public void Build_WithRuleFactory_ReturnsTheFactoryRuleWithoutACondition()
    {
        IAsyncBusinessRule created = RuleTestHelper.CreateBrokenAsyncRule(Code, Message);
        CapturingAsyncFactory factory = new CapturingAsyncFactory(_ => created);

        IAsyncBusinessRule built = AsyncBusinessRuleBuilder.Create(Code).WithRuleFactory(factory).Build();

        Assert.Same(created, built);
        Assert.Equal(1, factory.Calls);
    }

    [Fact]
    public void Build_WithRuleFactory_PassesTheConfiguredMetadata()
    {
        CapturingAsyncFactory factory = new CapturingAsyncFactory(RuleTestHelper.CreatePassingAsyncRule);

        AsyncBusinessRuleBuilder.Create(Code)
            .WithMessage(Message)
            .WithSeverity(RuleSeverity.Info)
            .WithName(Name)
            .WithDescription(Description)
            .WithCategory(Category)
            .WithTags("x")
            .WithRuleFactory(factory)
            .Build();

        BuiltRuleDefinition definition = Assert.IsType<BuiltRuleDefinition>(factory.Definition);
        Assert.Equal(Code, definition.Code);
        Assert.Equal(Message, definition.Message);
        Assert.Equal(RuleSeverity.Info, definition.Severity);
        Assert.Equal(Name, definition.Name);
        Assert.Equal(Description, definition.Description);
        Assert.Equal(Category, definition.Category);
        Assert.Equal(new[] { "x" }, definition.Tags);
    }

    [Fact]
    public void Build_FactoryReturnsNull_ThrowsInvalidOperationException()
    {
        CapturingAsyncFactory factory = new CapturingAsyncFactory(_ => null!);

        Assert.Throws<InvalidOperationException>(() => AsyncBusinessRuleBuilder.Create(Code).WithRuleFactory(factory).Build());
    }

    [Fact]
    public async Task WhenAsync_AfterWithRuleFactory_ReplacesTheFactory()
    {
        CapturingAsyncFactory factory = new CapturingAsyncFactory(RuleTestHelper.CreatePassingAsyncRule);

        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WithRuleFactory(factory).WhenAsync(_ => Task.FromResult(true)).Build();

        Assert.Equal(0, factory.Calls);
        Assert.True(await rule.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public void WithRuleFactory_AfterWhenAsync_ReplacesTheCondition()
    {
        IAsyncBusinessRule created = RuleTestHelper.CreatePassingAsyncRule(Code);
        CapturingAsyncFactory factory = new CapturingAsyncFactory(_ => created);

        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code).WhenAsync(_ => Task.FromResult(true)).WithRuleFactory(factory).Build();

        Assert.Same(created, rule);
    }

    [Fact]
    public async Task Definition_ResolveParametersAsync_WithoutProvider_UsesTheFallback()
    {
        CapturingAsyncFactory factory = new CapturingAsyncFactory(RuleTestHelper.CreatePassingAsyncRule);
        AsyncBusinessRuleBuilder.Create(Code).WithParameter(LimitName, Fallback).WithRuleFactory(factory).Build();

        RuleParameters parameters = await factory.Definition!.ResolveParametersAsync(new BusinessRuleContext(), TestToken);

        Assert.Equal(Fallback, parameters.Get<decimal>(LimitName));
    }

    [Fact]
    public async Task Definition_ResolveParametersAsync_PrefersTheInjectedAsyncProvider()
    {
        InMemoryRuleParameterProvider sync = RuleTestHelper.CreateParameterProvider(LimitName, TenantLimit, Acme);
        InMemoryRuleParameterProvider remote = RuleTestHelper.CreateParameterProvider(LimitName, RemoteLimit, Acme);
        CapturingAsyncFactory factory = new CapturingAsyncFactory(RuleTestHelper.CreatePassingAsyncRule);
        AsyncBusinessRuleBuilder.Create(Code)
            .WithParameters((IRuleParameterProvider)sync)
            .WithParameters((IAsyncRuleParameterProvider)remote)
            .WithParameter(LimitName, Fallback)
            .WithRuleFactory(factory)
            .Build();
        BusinessRuleContext acme = new BusinessRuleContext();
        acme.Set(RuleParameters.ScopeKey, Acme);

        RuleParameters parameters = await factory.Definition!.ResolveParametersAsync(acme, TestToken);

        Assert.Equal(RemoteLimit, parameters.Get<decimal>(LimitName));
    }

    [Fact]
    public async Task Definition_ResolveParametersAsync_NullContext_ResolvesTheGlobalScope()
    {
        InMemoryRuleParameterProvider provider = RuleTestHelper.CreateParameterProvider(LimitName, TenantLimit);
        CapturingAsyncFactory factory = new CapturingAsyncFactory(RuleTestHelper.CreatePassingAsyncRule);
        AsyncBusinessRuleBuilder.Create(Code).WithParameters((IAsyncRuleParameterProvider)provider).WithParameter(LimitName, Fallback).WithRuleFactory(factory).Build();

        RuleParameters parameters = await factory.Definition!.ResolveParametersAsync(null, TestToken);

        Assert.Equal(TenantLimit, parameters.Get<decimal>(LimitName));
    }

    [Fact]
    public async Task Definition_ResolveParametersAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        CapturingAsyncFactory factory = new CapturingAsyncFactory(RuleTestHelper.CreatePassingAsyncRule);
        AsyncBusinessRuleBuilder.Create(Code).WithParameter(LimitName, Fallback).WithRuleFactory(factory).Build();
        using CancellationTokenSource cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => factory.Definition!.ResolveParametersAsync(new BusinessRuleContext(), cancelled.Token));
    }

    [Fact]
    public void Definition_CreateResult_AttachesTheConfiguredDetails()
    {
        CapturingAsyncFactory factory = new CapturingAsyncFactory(RuleTestHelper.CreatePassingAsyncRule);
        AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WithRemediation("Split the order").WithRuleFactory(factory).Build();

        BusinessRuleResult result = factory.Definition!.CreateResult(RuleTestHelper.CreateBrokenAsyncRule(Code, Message), new BusinessRuleContext());

        Assert.True(factory.Definition.HasDetails);
        Assert.Equal(Code, result.Code);
        Assert.Equal("Split the order", result.Remediation);
    }

    private sealed class CapturingAsyncFactory : IAsyncBuiltRuleFactory
    {
        private readonly Func<BuiltRuleDefinition, IAsyncBusinessRule> _create;

        public CapturingAsyncFactory(Func<string, string, IAsyncBusinessRule> create)
            : this(definition => create(definition.Code, definition.Message))
        {
        }

        public CapturingAsyncFactory(Func<BuiltRuleDefinition, IAsyncBusinessRule> create)
        {
            _create = create;
        }

        public BuiltRuleDefinition? Definition { get; private set; }

        public int Calls { get; private set; }

        public IAsyncBusinessRule Create(BuiltRuleDefinition definition)
        {
            Calls++;
            Definition = definition;
            return _create(definition);
        }
    }
}
