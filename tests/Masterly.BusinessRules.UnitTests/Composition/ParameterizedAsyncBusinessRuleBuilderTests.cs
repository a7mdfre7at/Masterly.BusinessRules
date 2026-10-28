namespace Masterly.BusinessRules.UnitTests;

public class ParameterizedAsyncBusinessRuleBuilderTests
{
    private const string Code = "ORDER.TOO_LARGE";
    private const string Message = "Order amount exceeds the limit.";
    private const string LimitName = "maxOrderAmount";
    private const string AmountKey = "amount";
    private const decimal Fallback = 10_000m;
    private const decimal GlobalLimit = 20_000m;
    private const decimal AcmeLimit = 50_000m;
    private const decimal AsyncLimit = 30_000m;

    private static readonly RuleScope Acme = RuleScope.Tenant("acme");

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------
    // Builder guards
    // ---------------------------------------------------------------------

    [Fact]
    public void WithParameters_NullAsyncProvider_ThrowsArgumentNullException()
    {
        AsyncBusinessRuleBuilder builder = AsyncBusinessRuleBuilder.Create(Code);

        Assert.Throws<ArgumentNullException>(() => builder.WithParameters((IAsyncRuleParameterProvider)null!));
    }

    [Fact]
    public void WithParameters_NullSyncProvider_ThrowsArgumentNullException()
    {
        AsyncBusinessRuleBuilder builder = AsyncBusinessRuleBuilder.Create(Code);

        Assert.Throws<ArgumentNullException>(() => builder.WithParameters((IRuleParameterProvider)null!));
    }

    [Fact]
    public void WithParameter_DuplicateName_ThrowsArgumentException()
    {
        AsyncBusinessRuleBuilder builder = AsyncBusinessRuleBuilder.Create(Code).WithParameter(LimitName, Fallback);

        Assert.Throws<ArgumentException>(() => builder.WithParameter(LimitName, 1));
    }

    [Fact]
    public void WhenAsync_NullParametersCondition_ThrowsArgumentNullException()
    {
        AsyncBusinessRuleBuilder builder = AsyncBusinessRuleBuilder.Create(Code);

        Assert.Throws<ArgumentNullException>(() => builder.WhenAsync((Func<BusinessRuleContext, RuleParameters, CancellationToken, Task<bool>>)null!));
    }

    [Fact]
    public void Build_WithoutAnyCondition_ThrowsInvalidOperationException()
    {
        AsyncBusinessRuleBuilder builder = AsyncBusinessRuleBuilder.Create(Code).WithParameter(LimitName, Fallback);

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public async Task WhenAsync_LastConditionWins()
    {
        IAsyncBusinessRule parametersLast = AsyncBusinessRuleBuilder.Create(Code)
            .WhenAsync((ctx, ct) => Task.FromResult(false))
            .WhenAsync((ctx, p, ct) => Task.FromResult(true))
            .Build();
        IAsyncBusinessRule contextLast = AsyncBusinessRuleBuilder.Create(Code)
            .WhenAsync((ctx, p, ct) => Task.FromResult(true))
            .WhenAsync((ctx, ct) => Task.FromResult(false))
            .Build();

        Assert.True(await parametersLast.IsBrokenAsync(new BusinessRuleContext(), TestToken));
        Assert.False(await contextLast.IsBrokenAsync(new BusinessRuleContext(), TestToken));
    }

    // ---------------------------------------------------------------------
    // Resolution
    // ---------------------------------------------------------------------

    [Fact]
    public async Task IsBrokenAsync_NoProvider_UsesTheFallback()
    {
        IAsyncBusinessRule rule = CreateRule(syncProvider: null, asyncProvider: null);

        Assert.True(await rule.IsBrokenAsync(CreateContext(Fallback + 1), TestToken));
        Assert.False(await rule.IsBrokenAsync(CreateContext(Fallback), TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_SyncProvider_IsUsed()
    {
        IAsyncBusinessRule rule = CreateRule(syncProvider: new SyncOnlyProvider(GlobalLimit), asyncProvider: null);

        Assert.False(await rule.IsBrokenAsync(CreateContext(Fallback + 1), TestToken));
        Assert.True(await rule.IsBrokenAsync(CreateContext(GlobalLimit + 1), TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_AsyncProvider_IsUsed()
    {
        IAsyncBusinessRule rule = CreateRule(syncProvider: null, asyncProvider: new AsyncOnlyProvider(AsyncLimit));

        Assert.False(await rule.IsBrokenAsync(CreateContext(GlobalLimit + 1), TestToken));
        Assert.True(await rule.IsBrokenAsync(CreateContext(AsyncLimit + 1), TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_AsyncProvider_IsPreferredOverSyncProvider()
    {
        IAsyncBusinessRule rule = CreateRule(syncProvider: new SyncOnlyProvider(GlobalLimit), asyncProvider: new AsyncOnlyProvider(AsyncLimit));

        Assert.False(await rule.IsBrokenAsync(CreateContext(GlobalLimit + 1), TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_DualProvider_WorksThroughWithParameters()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code)
            .WithParameters(CreateLayeredProvider())
            .WithParameter(LimitName, Fallback)
            .WhenAsync((ctx, p, ct) => Task.FromResult(ctx.Get<decimal>(AmountKey) > p.Get<decimal>(LimitName)))
            .Build();

        Assert.False(await rule.IsBrokenAsync(CreateContext(GlobalLimit + 1, Acme), TestToken));
        Assert.True(await rule.IsBrokenAsync(CreateContext(GlobalLimit + 1), TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_ScopeOnContext_UsesTheTenantOverride()
    {
        IAsyncBusinessRule rule = CreateRule(syncProvider: CreateLayeredProvider(), asyncProvider: null);

        Assert.False(await rule.IsBrokenAsync(CreateContext(GlobalLimit + 1, Acme), TestToken));
        Assert.True(await rule.IsBrokenAsync(CreateContext(GlobalLimit + 1, RuleScope.Tenant("globex")), TestToken));
        Assert.True(await rule.IsBrokenAsync(CreateContext(AcmeLimit + 1, Acme), TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_AsyncProviderOnContext_OverridesEverything()
    {
        IAsyncBusinessRule rule = CreateRule(syncProvider: new SyncOnlyProvider(GlobalLimit), asyncProvider: new AsyncOnlyProvider(AsyncLimit));
        BusinessRuleContext context = CreateContext(AsyncLimit + 1);
        context.Set(RuleParameters.AsyncContextKey, new AsyncOnlyProvider(AcmeLimit));

        Assert.False(await rule.IsBrokenAsync(context, TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_SyncProviderOnContext_OverridesInjectedProviders()
    {
        IAsyncBusinessRule rule = CreateRule(syncProvider: new SyncOnlyProvider(GlobalLimit), asyncProvider: new AsyncOnlyProvider(AsyncLimit));
        BusinessRuleContext context = CreateContext(AsyncLimit + 1);
        context.Set(RuleParameters.ContextKey, new SyncOnlyProvider(AcmeLimit));

        Assert.False(await rule.IsBrokenAsync(context, TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_AsyncContextKey_WinsOverSyncContextKey()
    {
        IAsyncBusinessRule rule = CreateRule(syncProvider: null, asyncProvider: null);
        BusinessRuleContext context = CreateContext(GlobalLimit + 1);
        context.Set(RuleParameters.ContextKey, new SyncOnlyProvider(GlobalLimit));
        context.Set(RuleParameters.AsyncContextKey, new AsyncOnlyProvider(AcmeLimit));

        Assert.False(await rule.IsBrokenAsync(context, TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_ScopeKeyHoldsAnotherType_ThrowsContextValueTypeMismatchException()
    {
        IAsyncBusinessRule rule = CreateRule(syncProvider: CreateLayeredProvider(), asyncProvider: null);
        BusinessRuleContext context = CreateContext(GlobalLimit + 1);
        context.Set(RuleParameters.ScopeKeyName, "acme");

        ContextValueTypeMismatchException exception = await Assert.ThrowsAsync<ContextValueTypeMismatchException>(() => rule.IsBrokenAsync(context, TestToken));

        Assert.Equal(RuleParameters.ScopeKeyName, exception.Key);
        Assert.Equal(typeof(string), exception.ActualType);
        Assert.Equal(typeof(RuleScope), exception.RequestedType);
    }

    [Fact]
    public async Task IsBrokenAsync_AsyncProviderKeyHoldsAnotherType_ThrowsContextValueTypeMismatchException()
    {
        IAsyncBusinessRule rule = CreateRule(syncProvider: CreateLayeredProvider(), asyncProvider: null);
        BusinessRuleContext context = CreateContext(GlobalLimit + 1);
        context.Set(RuleParameters.AsyncProviderKeyName, new SyncOnlyProvider(AcmeLimit));

        ContextValueTypeMismatchException exception = await Assert.ThrowsAsync<ContextValueTypeMismatchException>(() => rule.IsBrokenAsync(context, TestToken));

        Assert.Equal(RuleParameters.AsyncProviderKeyName, exception.Key);
    }

    [Fact]
    public async Task IsBrokenAsync_SyncProviderKeyHoldsAnotherType_ThrowsContextValueTypeMismatchException()
    {
        IAsyncBusinessRule rule = CreateRule(syncProvider: CreateLayeredProvider(), asyncProvider: null);
        BusinessRuleContext context = CreateContext(GlobalLimit + 1);
        context.Set(RuleParameters.ProviderKeyName, new AsyncOnlyProvider(AcmeLimit));

        ContextValueTypeMismatchException exception = await Assert.ThrowsAsync<ContextValueTypeMismatchException>(() => rule.IsBrokenAsync(context, TestToken));

        Assert.Equal(RuleParameters.ProviderKeyName, exception.Key);
    }

    [Fact]
    public async Task IsBrokenAsync_ScopeKeyHoldsNull_RequestsTheGlobalScope()
    {
        IAsyncBusinessRule rule = CreateRule(syncProvider: CreateLayeredProvider(), asyncProvider: null);
        BusinessRuleContext context = CreateContext(GlobalLimit + 1);
        context.Set(RuleParameters.ScopeKeyName, (RuleScope?)null);

        Assert.True(await rule.IsBrokenAsync(context, TestToken));
    }

    [Fact]
    public async Task IsBrokenAsync_NullContext_UsesInjectedProviderAndGlobalScope()
    {
        RuleScope? observed = Acme;
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code)
            .WithParameters(CreateLayeredProvider())
            .WithParameter(LimitName, Fallback)
            .WhenAsync((ctx, p, ct) => { observed = p.Scope; return Task.FromResult(p.Get<decimal>(LimitName) != GlobalLimit); })
            .Build();

        Assert.False(await rule.IsBrokenAsync(null!, TestToken));
        Assert.Null(observed);
    }

    [Fact]
    public async Task CheckAllAsync_ScopeKeyHoldsAnotherType_WrapsIntoBusinessRuleContextException()
    {
        IAsyncBusinessRule rule = CreateRule(syncProvider: CreateLayeredProvider(), asyncProvider: null);
        BusinessRuleContext context = CreateContext(GlobalLimit + 1);
        context.Set(RuleParameters.ScopeKeyName, "acme");

        BusinessRuleContextException exception = await Assert.ThrowsAsync<BusinessRuleContextException>(() => AsyncBusinessRuleChecker.CheckAllAsync(context, new[] { rule }, cancellationToken: TestToken));

        Assert.Equal(Code, exception.RuleCode);
        Assert.Equal(RuleParameters.ScopeKeyName, exception.Key);
        Assert.IsType<ContextValueTypeMismatchException>(exception.InnerException);
    }

    [Fact]
    public async Task IsBrokenAsync_ForwardsTheCancellationTokenToProviderAndCondition()
    {
        CancellationToken? providerToken = null;
        CancellationToken? conditionToken = null;
        RecordingAsyncProvider provider = new RecordingAsyncProvider(token => providerToken = token);
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create(Code)
            .WithParameters(provider)
            .WithParameter(LimitName, Fallback)
            .WhenAsync((ctx, p, ct) => { conditionToken = ct; return Task.FromResult(true); })
            .Build();
        using CancellationTokenSource source = new CancellationTokenSource();

        await rule.IsBrokenAsync(new BusinessRuleContext(), source.Token);

        Assert.Equal(source.Token, providerToken);
        Assert.Equal(source.Token, conditionToken);
    }

    [Fact]
    public async Task IsBrokenAsync_CancelledToken_PropagatesCancellation()
    {
        IAsyncBusinessRule rule = CreateRule(syncProvider: null, asyncProvider: new InMemoryRuleParameterProvider().Set(LimitName, GlobalLimit));
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rule.IsBrokenAsync(CreateContext(Fallback), source.Token));
    }

    [Fact]
    public async Task IsBrokenAsync_ReadsTheProviderOnEveryEvaluation()
    {
        InMemoryRuleParameterProvider provider = RuleTestHelper.CreateParameterProvider(LimitName, GlobalLimit);
        IAsyncBusinessRule rule = CreateRule(syncProvider: null, asyncProvider: provider);
        BusinessRuleContext context = CreateContext(GlobalLimit + 1);

        bool before = await rule.IsBrokenAsync(context, TestToken);
        provider.Set(LimitName, AcmeLimit);
        bool after = await rule.IsBrokenAsync(context, TestToken);

        Assert.True(before);
        Assert.False(after);
    }

    [Fact]
    public async Task EvaluateAsync_Broken_ReturnsResultWithMetadata()
    {
        IAsyncBusinessRule rule = CreateRule(syncProvider: null, asyncProvider: null);

        BusinessRuleResult? result = await rule.EvaluateAsync(CreateContext(Fallback + 1), TestToken);

        Assert.NotNull(result);
        Assert.Equal(Code, result.Code);
        Assert.Equal(Message, result.Message);
    }

    [Fact]
    public async Task CheckAsync_Broken_Throws()
    {
        IAsyncBusinessRule rule = CreateRule(syncProvider: CreateLayeredProvider(), asyncProvider: null);

        await Assert.ThrowsAsync<BusinessRuleValidationException>(() => rule.CheckAsync(CreateContext(AcmeLimit + 1, Acme), TestToken));
    }

    [Fact]
    public async Task Checker_WithScopedContext_EvaluatesPerTenant()
    {
        IAsyncBusinessRule rule = CreateRule(syncProvider: CreateLayeredProvider(), asyncProvider: null);

        AsyncRuleEvaluationResult acme = await AsyncBusinessRuleChecker.EvaluateAllAsync(CreateContext(GlobalLimit + 1, Acme), new[] { rule }, cancellationToken: TestToken);
        AsyncRuleEvaluationResult globex = await AsyncBusinessRuleChecker.EvaluateAllAsync(CreateContext(GlobalLimit + 1, RuleScope.Tenant("globex")), new[] { rule }, cancellationToken: TestToken);

        Assert.True(acme.AllPassed);
        Assert.False(globex.AllPassed);
    }

    [Fact]
    public async Task IsBrokenAsync_ProviderThrows_PropagatesTheException()
    {
        IAsyncBusinessRule rule = CreateRule(syncProvider: null, asyncProvider: new ThrowingAsyncProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(() => rule.IsBrokenAsync(CreateContext(Fallback), TestToken));
    }

    // ---------------------------------------------------------------------
    // Typed builder
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Typed_WhenAsync_WithSubjectContextParametersAndToken_ReceivesAll()
    {
        CancellationToken? observed = null;
        IAsyncBusinessRule<decimal> rule = AsyncBusinessRuleBuilder<decimal>.Create(Code)
            .WithParameters(CreateLayeredProvider())
            .WithParameter(LimitName, Fallback)
            .WhenAsync((amount, ctx, p, ct) => { observed = ct; return Task.FromResult(amount > p.Get<decimal>(LimitName)); })
            .Build();
        using CancellationTokenSource source = new CancellationTokenSource();

        bool acme = await rule.IsBrokenAsync(GlobalLimit + 1, RuleTestHelper.CreateContext((RuleParameters.ScopeKey, Acme)), source.Token);
        bool global = await rule.IsBrokenAsync(GlobalLimit + 1, new BusinessRuleContext(), source.Token);

        Assert.False(acme);
        Assert.True(global);
        Assert.Equal(source.Token, observed);
    }

    [Fact]
    public async Task Typed_AsyncProvider_IsPreferredOverSyncProvider()
    {
        IAsyncBusinessRule<decimal> rule = AsyncBusinessRuleBuilder<decimal>.Create(Code)
            .WithParameters(new SyncOnlyProvider(GlobalLimit))
            .WithParameters(new AsyncOnlyProvider(AsyncLimit))
            .WithParameter(LimitName, Fallback)
            .WhenAsync((amount, ctx, p, ct) => Task.FromResult(amount > p.Get<decimal>(LimitName)))
            .Build();

        Assert.False(await rule.IsBrokenAsync(GlobalLimit + 1, new BusinessRuleContext(), TestToken));
        Assert.True(await rule.IsBrokenAsync(AsyncLimit + 1, new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task Typed_WithoutProvider_UsesFallback()
    {
        IAsyncBusinessRule<decimal> rule = AsyncBusinessRuleBuilder<decimal>.Create(Code)
            .WithParameter(LimitName, Fallback)
            .WhenAsync((amount, ctx, p, ct) => Task.FromResult(amount > p.Get<decimal>(LimitName)))
            .Build();

        Assert.True(await rule.IsBrokenAsync(Fallback + 1, new BusinessRuleContext(), TestToken));
        Assert.False(await rule.IsBrokenAsync(Fallback, new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public void Typed_WithParameters_NullAsyncProvider_ThrowsArgumentNullException()
    {
        AsyncBusinessRuleBuilder<decimal> builder = AsyncBusinessRuleBuilder<decimal>.Create(Code);

        Assert.Throws<ArgumentNullException>(() => builder.WithParameters((IAsyncRuleParameterProvider)null!));
    }

    [Fact]
    public void Typed_WhenAsync_NullParametersCondition_ThrowsArgumentNullException()
    {
        AsyncBusinessRuleBuilder<decimal> builder = AsyncBusinessRuleBuilder<decimal>.Create(Code);

        Assert.Throws<ArgumentNullException>(() => builder.WhenAsync((Func<decimal, BusinessRuleContext, RuleParameters, CancellationToken, Task<bool>>)null!));
    }

    [Fact]
    public void Typed_Build_WithoutAnyCondition_ThrowsInvalidOperationException()
    {
        AsyncBusinessRuleBuilder<decimal> builder = AsyncBusinessRuleBuilder<decimal>.Create(Code).WithParameter(LimitName, Fallback);

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public async Task Typed_LastConditionWins()
    {
        IAsyncBusinessRule<decimal> contextLast = AsyncBusinessRuleBuilder<decimal>.Create(Code)
            .WhenAsync((amount, ctx, p, ct) => Task.FromResult(true))
            .WhenAsync((amount, ctx, ct) => Task.FromResult(false))
            .Build();

        Assert.False(await contextLast.IsBrokenAsync(0m, new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task Typed_CancelledToken_PropagatesCancellation()
    {
        IAsyncBusinessRule<decimal> rule = AsyncBusinessRuleBuilder<decimal>.Create(Code)
            .WithParameters(new InMemoryRuleParameterProvider().Set(LimitName, GlobalLimit))
            .WithParameter(LimitName, Fallback)
            .WhenAsync((amount, ctx, p, ct) => Task.FromResult(true))
            .Build();
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rule.IsBrokenAsync(0m, new BusinessRuleContext(), source.Token));
    }

    [Fact]
    public async Task Typed_Checker_WithScopedContext_EvaluatesPerTenant()
    {
        IAsyncBusinessRule<decimal> rule = AsyncBusinessRuleBuilder<decimal>.Create(Code)
            .WithParameters(CreateLayeredProvider())
            .WithParameter(LimitName, Fallback)
            .WhenAsync((amount, ctx, p, ct) => Task.FromResult(amount > p.Get<decimal>(LimitName)))
            .Build();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.For(GlobalLimit + 1).EvaluateAllAsync(RuleTestHelper.CreateContext((RuleParameters.ScopeKey, Acme)), new[] { rule }, cancellationToken: TestToken);

        Assert.True(result.AllPassed);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static IAsyncBusinessRule CreateRule(IRuleParameterProvider? syncProvider, IAsyncRuleParameterProvider? asyncProvider)
    {
        AsyncBusinessRuleBuilder builder = AsyncBusinessRuleBuilder.Create(Code).WithMessage(Message).WithParameter(LimitName, Fallback);
        if (syncProvider != null)
        {
            builder.WithParameters(syncProvider);
        }

        if (asyncProvider != null)
        {
            builder.WithParameters(asyncProvider);
        }

        return builder.WhenAsync((ctx, p, ct) => Task.FromResult(ctx.Get<decimal>(AmountKey) > p.Get<decimal>(LimitName))).Build();
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

    private sealed class SyncOnlyProvider : IRuleParameterProvider
    {
        private readonly decimal _limit;

        public SyncOnlyProvider(decimal limit)
        {
            _limit = limit;
        }

        public bool TryGet<T>(string name, RuleScope? scope, out T value)
        {
            if (name == LimitName && _limit is T typed)
            {
                value = typed;
                return true;
            }

            value = default!;
            return false;
        }
    }

    private sealed class AsyncOnlyProvider : IAsyncRuleParameterProvider
    {
        private readonly decimal _limit;

        public AsyncOnlyProvider(decimal limit)
        {
            _limit = limit;
        }

        public async Task<RuleParameterLookup<T>> TryGetAsync<T>(string name, RuleScope? scope, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return name == LimitName && _limit is T typed ? RuleParameterLookup<T>.Of(typed) : RuleParameterLookup<T>.Missing;
        }
    }

    private sealed class RecordingAsyncProvider : IAsyncRuleParameterProvider
    {
        private readonly Action<CancellationToken> _onToken;

        public RecordingAsyncProvider(Action<CancellationToken> onToken)
        {
            _onToken = onToken;
        }

        public Task<RuleParameterLookup<T>> TryGetAsync<T>(string name, RuleScope? scope, CancellationToken cancellationToken = default)
        {
            _onToken(cancellationToken);
            return Task.FromResult(RuleParameterLookup<T>.Missing);
        }
    }

    private sealed class ThrowingAsyncProvider : IAsyncRuleParameterProvider
    {
        public Task<RuleParameterLookup<T>> TryGetAsync<T>(string name, RuleScope? scope, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("provider failure");
        }
    }
}
