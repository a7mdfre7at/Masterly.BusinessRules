namespace Masterly.BusinessRules.UnitTests;

public class RuleParametersTests
{
    private const string Code = "ORDER.TOO_LARGE";
    private const string LimitName = "maxOrderAmount";
    private const string LabelName = "label";
    private const decimal Fallback = 10_000m;

    // ---------------------------------------------------------------------
    // Reserved context keys
    // ---------------------------------------------------------------------

    [Fact]
    public void ContextKey_BindsTheSyncProviderKeyName()
    {
        Assert.Equal(RuleParameters.ProviderKeyName, RuleParameters.ContextKey.Name);
        Assert.Equal(typeof(IRuleParameterProvider), RuleParameters.ContextKey.ValueType);
    }

    [Fact]
    public void AsyncContextKey_BindsTheAsyncProviderKeyName()
    {
        Assert.Equal(RuleParameters.AsyncProviderKeyName, RuleParameters.AsyncContextKey.Name);
        Assert.Equal(typeof(IAsyncRuleParameterProvider), RuleParameters.AsyncContextKey.ValueType);
    }

    [Fact]
    public void ScopeKey_BindsTheScopeKeyName()
    {
        Assert.Equal(RuleParameters.ScopeKeyName, RuleParameters.ScopeKey.Name);
        Assert.Equal(typeof(RuleScope), RuleParameters.ScopeKey.ValueType);
    }

    [Fact]
    public void KeyNames_AreDistinctAndReserved()
    {
        string[] names = { RuleParameters.ProviderKeyName, RuleParameters.AsyncProviderKeyName, RuleParameters.ScopeKeyName };

        Assert.Equal(3, names.Distinct(StringComparer.Ordinal).Count());
        Assert.All(names, name => Assert.StartsWith("__", name));
    }

    [Fact]
    public void ScopeKey_RoundTripsThroughTheContext()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        RuleScope scope = RuleScope.Tenant("acme");

        context.Set(RuleParameters.ScopeKey, scope);

        Assert.Equal(scope, context.Get(RuleParameters.ScopeKey));
    }

    // ---------------------------------------------------------------------
    // Empty
    // ---------------------------------------------------------------------

    [Fact]
    public void Empty_HasNoParametersAndNoScope()
    {
        RuleParameters parameters = RuleParameters.Empty;

        Assert.Equal(0, parameters.Count);
        Assert.Empty(parameters.Names);
        Assert.Null(parameters.Scope);
        Assert.False(parameters.Contains(LimitName));
    }

    [Fact]
    public void Empty_Get_ThrowsKeyNotFoundExceptionNamingTheParameter()
    {
        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(() => RuleParameters.Empty.Get<decimal>(LimitName));

        Assert.Contains(LimitName, exception.Message);
        Assert.Contains("declares no parameters", exception.Message);
    }

    [Fact]
    public void Empty_TryGet_ReturnsFalse()
    {
        Assert.False(RuleParameters.Empty.TryGet(LimitName, out decimal value));
        Assert.Equal(0m, value);
    }

    [Fact]
    public void Empty_NullName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RuleParameters.Empty.Get<decimal>(null!));
        Assert.Throws<ArgumentNullException>(() => RuleParameters.Empty.TryGet(null!, out decimal _));
        Assert.Throws<ArgumentNullException>(() => RuleParameters.Empty.Contains(null!));
    }

    // ---------------------------------------------------------------------
    // Resolved instances (observed through a built rule)
    // ---------------------------------------------------------------------

    [Fact]
    public void Resolved_ExposesDeclaredNamesCountAndScope()
    {
        RuleParameters? observed = null;
        IBusinessRule rule = BusinessRuleBuilder.Create(Code)
            .WithParameter(LimitName, Fallback)
            .WithParameter(LabelName, "default")
            .When((ctx, p) => { observed = p; return false; })
            .Build();
        BusinessRuleContext context = RuleTestHelper.CreateContext((RuleParameters.ScopeKey, RuleScope.Tenant("acme")));

        rule.IsBroken(context);

        Assert.NotNull(observed);
        Assert.Equal(2, observed.Count);
        Assert.Equal(new[] { LabelName, LimitName }, observed.Names.OrderBy(name => name, StringComparer.Ordinal));
        Assert.True(observed.Contains(LimitName));
        Assert.Equal(RuleScope.Tenant("acme"), observed.Scope);
    }

    [Fact]
    public void Resolved_Get_UndeclaredName_ListsDeclaredNames()
    {
        KeyNotFoundException? observed = null;
        IBusinessRule rule = BusinessRuleBuilder.Create(Code)
            .WithParameter(LimitName, Fallback)
            .When((ctx, p) =>
            {
                observed = Assert.Throws<KeyNotFoundException>(() => p.Get<decimal>("other"));
                return false;
            })
            .Build();

        rule.IsBroken(new BusinessRuleContext());

        Assert.NotNull(observed);
        Assert.Contains("'other'", observed.Message);
        Assert.Contains(LimitName, observed.Message);
    }

    [Fact]
    public void Resolved_Get_WrongType_ThrowsInvalidCastExceptionNamingBothTypes()
    {
        InvalidCastException? observed = null;
        IBusinessRule rule = BusinessRuleBuilder.Create(Code)
            .WithParameter(LimitName, Fallback)
            .When((ctx, p) =>
            {
                observed = Assert.Throws<InvalidCastException>(() => p.Get<int>(LimitName));
                return false;
            })
            .Build();

        rule.IsBroken(new BusinessRuleContext());

        Assert.NotNull(observed);
        Assert.Contains("Decimal", observed.Message);
        Assert.Contains("Int32", observed.Message);
    }

    [Fact]
    public void Resolved_TryGet_WrongType_ReturnsFalse()
    {
        bool? found = null;
        IBusinessRule rule = BusinessRuleBuilder.Create(Code)
            .WithParameter(LimitName, Fallback)
            .When((ctx, p) => { found = p.TryGet(LimitName, out int _); return false; })
            .Build();

        rule.IsBroken(new BusinessRuleContext());

        Assert.False(found);
    }

    [Fact]
    public void Resolved_TryGet_DeclaredTypedParameter_ReturnsTrueAndTheFallback()
    {
        bool? found = null;
        decimal value = 0m;
        IBusinessRule rule = BusinessRuleBuilder.Create(Code)
            .WithParameter(LimitName, 5m)
            .When((ctx, p) => { found = p.TryGet(LimitName, out value); return false; })
            .Build();

        rule.IsBroken(new BusinessRuleContext());

        Assert.True(found);
        Assert.Equal(5m, value);
    }

    [Fact]
    public void Resolved_TryGet_DeclaredParameterFromAProvider_ReturnsTrueAndTheProvidedValue()
    {
        bool? found = null;
        decimal value = 0m;
        IBusinessRule rule = BusinessRuleBuilder.Create(Code)
            .WithParameters(RuleTestHelper.CreateParameterProvider(LimitName, 42m))
            .WithParameter(LimitName, Fallback)
            .When((ctx, p) => { found = p.TryGet(LimitName, out value); return false; })
            .Build();

        rule.IsBroken(new BusinessRuleContext());

        Assert.True(found);
        Assert.Equal(42m, value);
    }

    [Fact]
    public void Resolved_TryGet_UndeclaredName_ReturnsFalse()
    {
        bool? found = null;
        IBusinessRule rule = BusinessRuleBuilder.Create(Code)
            .WithParameter(LimitName, Fallback)
            .When((ctx, p) => { found = p.TryGet("undeclared", out decimal _); return false; })
            .Build();

        rule.IsBroken(new BusinessRuleContext());

        Assert.False(found);
    }

    [Fact]
    public async Task ResolvedAsync_TryGet_DeclaredTypedParameter_ReturnsTrueAndTheValue()
    {
        bool? found = null;
        decimal value = 0m;
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code)
            .WithParameters(RuleTestHelper.CreateParameterProvider(LimitName, 42m))
            .WithParameter(LimitName, Fallback)
            .WhenAsync((ctx, p, ct) => { found = p.TryGet(LimitName, out value); return Task.FromResult(false); })
            .Build();

        await rule.IsBrokenAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken);

        Assert.True(found);
        Assert.Equal(42m, value);
    }

    [Fact]
    public async Task ResolvedAsync_TryGet_UndeclaredName_ReturnsFalse()
    {
        bool? found = null;
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code)
            .WithParameter(LimitName, Fallback)
            .WhenAsync((ctx, p, ct) => { found = p.TryGet("undeclared", out decimal _); return Task.FromResult(false); })
            .Build();

        await rule.IsBrokenAsync(new BusinessRuleContext(), TestContext.Current.CancellationToken);

        Assert.False(found);
    }

    [Fact]
    public void Resolved_NullFallback_IsReturnedAsNull()
    {
        string? observed = "not-null";
        IBusinessRule rule = BusinessRuleBuilder.Create(Code)
            .WithParameter<string?>(LabelName, null)
            .When((ctx, p) => { observed = p.Get<string?>(LabelName); return false; })
            .Build();

        rule.IsBroken(new BusinessRuleContext());

        Assert.Null(observed);
    }

    [Fact]
    public void Resolved_NullValue_RequestedAsValueType_ThrowsInvalidCastException()
    {
        InvalidCastException? observed = null;
        IBusinessRule rule = BusinessRuleBuilder.Create(Code)
            .WithParameter<string?>(LabelName, null)
            .When((ctx, p) =>
            {
                observed = Assert.Throws<InvalidCastException>(() => p.Get<int>(LabelName));
                return false;
            })
            .Build();

        rule.IsBroken(new BusinessRuleContext());

        Assert.NotNull(observed);
        Assert.Contains("null", observed.Message);
    }
}
