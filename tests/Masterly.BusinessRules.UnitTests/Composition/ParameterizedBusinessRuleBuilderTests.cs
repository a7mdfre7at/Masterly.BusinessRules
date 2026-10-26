namespace Masterly.BusinessRules.UnitTests;

public class ParameterizedBusinessRuleBuilderTests
{
    private const string Code = "ORDER.TOO_LARGE";
    private const string Message = "Order amount exceeds the limit.";
    private const string LimitName = "maxOrderAmount";
    private const string AmountKey = "amount";
    private const decimal Fallback = 10_000m;
    private const decimal GlobalLimit = 20_000m;
    private const decimal AcmeLimit = 50_000m;

    private static readonly RuleScope Acme = RuleScope.Tenant("acme");

    // ---------------------------------------------------------------------
    // Builder guards
    // ---------------------------------------------------------------------

    [Fact]
    public void WithParameters_NullProvider_ThrowsArgumentNullException()
    {
        BusinessRuleBuilder builder = BusinessRuleBuilder.Create(Code);

        Assert.Throws<ArgumentNullException>(() => builder.WithParameters((IRuleParameterProvider)null!));
    }

    [Fact]
    public void WithParameter_NullName_ThrowsArgumentNullException()
    {
        BusinessRuleBuilder builder = BusinessRuleBuilder.Create(Code);

        Assert.Throws<ArgumentNullException>(() => builder.WithParameter(null!, Fallback));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void WithParameter_BlankName_ThrowsArgumentException(string name)
    {
        BusinessRuleBuilder builder = BusinessRuleBuilder.Create(Code);

        Assert.Throws<ArgumentException>(() => builder.WithParameter(name, Fallback));
    }

    [Fact]
    public void WithParameter_DuplicateName_ThrowsArgumentException()
    {
        BusinessRuleBuilder builder = BusinessRuleBuilder.Create(Code).WithParameter(LimitName, Fallback);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => builder.WithParameter(LimitName, 1));

        Assert.Contains(LimitName, exception.Message);
    }

    [Fact]
    public void When_NullParametersCondition_ThrowsArgumentNullException()
    {
        BusinessRuleBuilder builder = BusinessRuleBuilder.Create(Code);

        Assert.Throws<ArgumentNullException>(() => builder.When((Func<BusinessRuleContext, RuleParameters, bool>)null!));
    }

    [Fact]
    public void Build_WithParametersCondition_Succeeds()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code).WithMessage(Message).When((ctx, p) => true).Build();

        Assert.Equal(Code, rule.Code);
        Assert.Equal(Message, rule.Message);
    }

    [Fact]
    public void Build_WithoutAnyCondition_ThrowsInvalidOperationException()
    {
        BusinessRuleBuilder builder = BusinessRuleBuilder.Create(Code).WithParameter(LimitName, Fallback);

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    // ---------------------------------------------------------------------
    // Resolution
    // ---------------------------------------------------------------------

    [Fact]
    public void IsBroken_NoProvider_UsesTheFallback()
    {
        IBusinessRule rule = CreateRule(provider: null);

        Assert.True(rule.IsBroken(CreateContext(Fallback + 1)));
        Assert.False(rule.IsBroken(CreateContext(Fallback)));
    }

    [Fact]
    public void IsBroken_ProviderWithoutValue_UsesTheFallback()
    {
        IBusinessRule rule = CreateRule(new InMemoryRuleParameterProvider());

        Assert.True(rule.IsBroken(CreateContext(Fallback + 1)));
    }

    [Fact]
    public void IsBroken_ProviderWithGlobalValue_UsesIt()
    {
        IBusinessRule rule = CreateRule(RuleTestHelper.CreateParameterProvider(LimitName, GlobalLimit));

        Assert.False(rule.IsBroken(CreateContext(Fallback + 1)));
        Assert.True(rule.IsBroken(CreateContext(GlobalLimit + 1)));
    }

    [Fact]
    public void IsBroken_ScopeOnContext_UsesTheTenantOverride()
    {
        IBusinessRule rule = CreateRule(CreateLayeredProvider());

        BusinessRuleContext acmeContext = CreateContext(GlobalLimit + 1, Acme);
        BusinessRuleContext globexContext = CreateContext(GlobalLimit + 1, RuleScope.Tenant("globex"));

        Assert.False(rule.IsBroken(acmeContext));
        Assert.True(rule.IsBroken(globexContext));
        Assert.True(rule.IsBroken(CreateContext(AcmeLimit + 1, Acme)));
    }

    [Fact]
    public void IsBroken_NoScopeOnContext_RequestsTheGlobalScope()
    {
        RuleScope? observed = Acme;
        IBusinessRule rule = BusinessRuleBuilder.Create(Code)
            .WithParameters(CreateLayeredProvider())
            .WithParameter(LimitName, Fallback)
            .When((ctx, p) => { observed = p.Scope; return p.Get<decimal>(LimitName) != GlobalLimit; })
            .Build();

        Assert.False(rule.IsBroken(new BusinessRuleContext()));
        Assert.Null(observed);
    }

    [Fact]
    public void IsBroken_ProviderOnContext_OverridesInjectedProvider()
    {
        IBusinessRule rule = CreateRule(RuleTestHelper.CreateParameterProvider(LimitName, GlobalLimit));
        BusinessRuleContext context = CreateContext(GlobalLimit + 1);
        context.Set(RuleParameters.ContextKey, RuleTestHelper.CreateParameterProvider(LimitName, AcmeLimit));

        Assert.False(rule.IsBroken(context));
    }

    [Fact]
    public void IsBroken_ProviderOnContextWithoutInjectedOne_IsUsed()
    {
        IBusinessRule rule = CreateRule(provider: null);
        BusinessRuleContext context = CreateContext(GlobalLimit + 1);
        context.Set(RuleParameters.ContextKey, RuleTestHelper.CreateParameterProvider(LimitName, AcmeLimit));

        Assert.False(rule.IsBroken(context));
    }

    [Fact]
    public void IsBroken_ReadsTheProviderOnEveryEvaluation()
    {
        InMemoryRuleParameterProvider provider = RuleTestHelper.CreateParameterProvider(LimitName, GlobalLimit);
        IBusinessRule rule = CreateRule(provider);
        BusinessRuleContext context = CreateContext(GlobalLimit + 1);

        bool before = rule.IsBroken(context);
        provider.Set(LimitName, AcmeLimit);
        bool after = rule.IsBroken(context);

        Assert.True(before);
        Assert.False(after);
    }

    [Fact]
    public void IsBroken_Parameterless_UsesInjectedProviderAndGlobalScope()
    {
        RuleParameters? observed = null;
        IBusinessRule rule = BusinessRuleBuilder.Create(Code)
            .WithParameters(CreateLayeredProvider())
            .WithParameter(LimitName, Fallback)
            .When((ctx, p) => { observed = p; return true; })
            .Build();

        Assert.True(rule.IsBroken());
        Assert.NotNull(observed);
        Assert.Null(observed.Scope);
        Assert.Equal(GlobalLimit, observed.Get<decimal>(LimitName));
    }

    [Fact]
    public void IsBroken_Parameterless_WithoutProvider_UsesFallbackAndNeverThrows()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code)
            .WithParameter(LimitName, Fallback)
            .When((ctx, p) => p.Get<decimal>(LimitName) == Fallback)
            .Build();

        Assert.True(rule.IsBroken());
        Assert.Throws<BusinessRuleValidationException>(() => rule.Check());
    }

    [Fact]
    public void IsBroken_Composition_WithoutContext_UsesFallback()
    {
        IBusinessRule parameterized = BusinessRuleBuilder.Create(Code)
            .WithParameter(LimitName, Fallback)
            .When((ctx, p) => p.Get<decimal>(LimitName) == Fallback)
            .Build();

        IBusinessRule composed = parameterized.And(RuleTestHelper.CreateBrokenRule());

        Assert.True(composed.IsBroken());
    }

    [Fact]
    public void IsBroken_ParametersCondition_TakesPrecedenceOverContextCondition()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create(Code)
            .When(ctx => false)
            .When((ctx, p) => true)
            .Build();

        Assert.True(rule.IsBroken(new BusinessRuleContext()));
    }

    [Fact]
    public void Check_WithContext_ThrowsWithTheRuleResult()
    {
        IBusinessRule rule = CreateRule(CreateLayeredProvider());

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => rule.Check(CreateContext(AcmeLimit + 1, Acme)));

        Assert.Equal(Code, Assert.Single(exception.BrokenRules).Code);
    }

    [Fact]
    public void Checker_WithScopedContext_EvaluatesPerTenant()
    {
        IBusinessRule rule = CreateRule(CreateLayeredProvider());

        RuleEvaluationResult acme = BusinessRuleChecker.EvaluateAll(new[] { rule }, CreateContext(GlobalLimit + 1, Acme));
        RuleEvaluationResult globex = BusinessRuleChecker.EvaluateAll(new[] { rule }, CreateContext(GlobalLimit + 1, RuleScope.Tenant("globex")));

        Assert.True(acme.AllPassed);
        Assert.False(globex.AllPassed);
    }

    [Fact]
    public void IsBroken_MultipleParameters_AreAllResolved()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider().Set(LimitName, GlobalLimit).Set("minimumAge", 21);
        IBusinessRule rule = BusinessRuleBuilder.Create(Code)
            .WithParameters(provider)
            .WithParameter(LimitName, Fallback)
            .WithParameter("minimumAge", 18)
            .WithParameter("label", "default")
            .When((ctx, p) => p.Get<decimal>(LimitName) == GlobalLimit && p.Get<int>("minimumAge") == 21 && p.Get<string>("label") == "default")
            .Build();

        Assert.True(rule.IsBroken(new BusinessRuleContext()));
    }

    [Fact]
    public void IsBroken_ProviderThrows_PropagatesTheException()
    {
        IBusinessRule rule = CreateRule(new ThrowingProvider());

        Assert.Throws<InvalidOperationException>(() => rule.IsBroken(CreateContext(Fallback)));
    }

    [Fact]
    public void Explain_ParameterizedRule_ReportsTheVerdict()
    {
        IBusinessRule rule = CreateRule(CreateLayeredProvider());

        RuleExplanation explanation = rule.Explain(CreateContext(AcmeLimit + 1, Acme));

        Assert.Equal(RuleOutcome.Broken, explanation.Outcome);
    }

    // ---------------------------------------------------------------------
    // Typed builder
    // ---------------------------------------------------------------------

    [Fact]
    public void Typed_When_WithSubjectContextAndParameters_ReceivesAllThree()
    {
        IBusinessRule<decimal> rule = BusinessRuleBuilder<decimal>.Create(Code)
            .WithParameters(CreateLayeredProvider())
            .WithParameter(LimitName, Fallback)
            .When((amount, ctx, p) => amount > p.Get<decimal>(LimitName))
            .Build();

        Assert.False(rule.IsBroken(GlobalLimit + 1, RuleTestHelper.CreateContext((RuleParameters.ScopeKey, Acme))));
        Assert.True(rule.IsBroken(GlobalLimit + 1, new BusinessRuleContext()));
    }

    [Fact]
    public void Typed_IsBroken_SubjectOnly_UsesInjectedProviderAndGlobalScope()
    {
        IBusinessRule<decimal> rule = BusinessRuleBuilder<decimal>.Create(Code)
            .WithParameters(CreateLayeredProvider())
            .WithParameter(LimitName, Fallback)
            .When((amount, ctx, p) => amount > p.Get<decimal>(LimitName))
            .Build();

        Assert.True(rule.IsBroken(GlobalLimit + 1));
        Assert.False(rule.IsBroken(GlobalLimit));
    }

    [Fact]
    public void Typed_IsBroken_WithoutProvider_UsesFallback()
    {
        IBusinessRule<decimal> rule = BusinessRuleBuilder<decimal>.Create(Code)
            .WithParameter(LimitName, Fallback)
            .When((amount, ctx, p) => amount > p.Get<decimal>(LimitName))
            .Build();

        Assert.True(rule.IsBroken(Fallback + 1));
        Assert.False(rule.IsBroken(Fallback));
    }

    [Fact]
    public void Typed_ProviderOnContext_OverridesInjectedProvider()
    {
        IBusinessRule<decimal> rule = BusinessRuleBuilder<decimal>.Create(Code)
            .WithParameters(RuleTestHelper.CreateParameterProvider(LimitName, GlobalLimit))
            .WithParameter(LimitName, Fallback)
            .When((amount, ctx, p) => amount > p.Get<decimal>(LimitName))
            .Build();
        BusinessRuleContext context = RuleTestHelper.CreateContext((RuleParameters.ContextKey, (IRuleParameterProvider)RuleTestHelper.CreateParameterProvider(LimitName, AcmeLimit)));

        Assert.False(rule.IsBroken(GlobalLimit + 1, context));
    }

    [Fact]
    public void Typed_When_NullParametersCondition_ThrowsArgumentNullException()
    {
        BusinessRuleBuilder<decimal> builder = BusinessRuleBuilder<decimal>.Create(Code);

        Assert.Throws<ArgumentNullException>(() => builder.When((Func<decimal, BusinessRuleContext, RuleParameters, bool>)null!));
    }

    [Fact]
    public void Typed_Build_WithoutAnyCondition_ThrowsInvalidOperationException()
    {
        BusinessRuleBuilder<decimal> builder = BusinessRuleBuilder<decimal>.Create(Code).WithParameter(LimitName, Fallback);

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Typed_ParametersCondition_TakesPrecedenceOverContextCondition()
    {
        IBusinessRule<decimal> rule = BusinessRuleBuilder<decimal>.Create(Code)
            .When((amount, ctx) => false)
            .When((amount, ctx, p) => true)
            .Build();

        Assert.True(rule.IsBroken(0m, new BusinessRuleContext()));
    }

    [Fact]
    public void Typed_Bound_WorksWithCheckerAndScope()
    {
        IBusinessRule<decimal> rule = BusinessRuleBuilder<decimal>.Create(Code)
            .WithParameters(CreateLayeredProvider())
            .WithParameter(LimitName, Fallback)
            .When((amount, ctx, p) => amount > p.Get<decimal>(LimitName))
            .Build();

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(GlobalLimit + 1, new[] { rule }, RuleTestHelper.CreateContext((RuleParameters.ScopeKey, Acme)));

        Assert.True(result.AllPassed);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static IBusinessRule CreateRule(IRuleParameterProvider? provider)
    {
        BusinessRuleBuilder builder = BusinessRuleBuilder.Create(Code).WithMessage(Message).WithParameter(LimitName, Fallback);
        if (provider != null)
        {
            builder.WithParameters(provider);
        }

        return builder.When((ctx, p) => ctx.Get<decimal>(AmountKey) > p.Get<decimal>(LimitName)).Build();
    }

    private static InMemoryRuleParameterProvider CreateLayeredProvider()
    {
        return new InMemoryRuleParameterProvider().Set(LimitName, GlobalLimit).Set(LimitName, AcmeLimit, Acme);
    }

    private static BusinessRuleContext CreateContext(decimal amount, RuleScope? scope = null)
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, amount));
        if (scope != null)
        {
            context.Set(RuleParameters.ScopeKey, scope);
        }

        return context;
    }

    private sealed class ThrowingProvider : IRuleParameterProvider
    {
        public bool TryGet<T>(string name, RuleScope? scope, out T value)
        {
            throw new InvalidOperationException("provider failure");
        }
    }
}
