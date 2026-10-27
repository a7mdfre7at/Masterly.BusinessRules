namespace Masterly.BusinessRules.Expressions.UnitTests;

public class RuleExpressionBindingTests
{
    private const string AmountKey = "amount";
    private const string CustomerKey = "customer";
    private const string LimitName = "maxOrderAmount";
    private const string BrokenExpression = "amount > @maxOrderAmount";

    private static readonly RuleScope Acme = RuleScope.Tenant("acme");

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------
    // Null guards
    // ---------------------------------------------------------------------

    [Fact]
    public void Evaluate_NullContext_ThrowsArgumentNullException()
    {
        RuleExpression expression = RuleExpression.Parse("amount > 1");

        Assert.Throws<ArgumentNullException>(() => expression.Evaluate(null!));
    }

    [Fact]
    public async Task EvaluateAsync_NullContext_ThrowsArgumentNullException()
    {
        RuleExpression expression = RuleExpression.Parse("amount > 1");

        await Assert.ThrowsAsync<ArgumentNullException>(() => expression.EvaluateAsync(null!, cancellationToken: TestToken));
    }

    [Fact]
    public void Explain_NullContext_ThrowsArgumentNullException()
    {
        RuleExpression expression = RuleExpression.Parse("amount > 1");

        Assert.Throws<ArgumentNullException>(() => expression.Explain(null!));
    }

    [Fact]
    public async Task ExplainAsync_NullContext_ThrowsArgumentNullException()
    {
        RuleExpression expression = RuleExpression.Parse("amount > 1");

        await Assert.ThrowsAsync<ArgumentNullException>(() => expression.ExplainAsync(null!, cancellationToken: TestToken));
    }

    // ---------------------------------------------------------------------
    // Unbound identifiers
    // ---------------------------------------------------------------------

    [Fact]
    public void Evaluate_UnboundIdentifier_ThrowsBindingExceptionNamingAvailableKeys()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext(("orderId", 1), (CustomerKey, "x"));

        RuleExpressionBindingException error = Assert.Throws<RuleExpressionBindingException>(() => RuleExpression.Parse("amount > 1").Evaluate(context));

        Assert.Equal(AmountKey, error.Identifier);
        Assert.Equal(new[] { CustomerKey, "orderId" }, error.AvailableKeys);
        Assert.Contains("'amount'", error.Message);
        Assert.Contains("customer, orderId", error.Message);
    }

    [Fact]
    public void Evaluate_UnboundIdentifierOnEmptyContext_SaysNothingIsAvailable()
    {
        RuleExpressionBindingException error = Assert.Throws<RuleExpressionBindingException>(() => RuleExpression.Parse("amount > 1").Evaluate(new BusinessRuleContext()));

        Assert.Empty(error.AvailableKeys);
        Assert.Contains("nothing is available", error.Message);
    }

    [Fact]
    public void Evaluate_IdentifiersAreCaseSensitive()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext(("Amount", 5));

        Assert.Throws<RuleExpressionBindingException>(() => RuleExpression.Parse("amount > 1").Evaluate(context));
    }

    [Fact]
    public void Evaluate_UnboundParameter_ThrowsBindingExceptionWithAtPrefix()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 5));

        RuleExpressionBindingException error = Assert.Throws<RuleExpressionBindingException>(() => RuleExpression.Parse(BrokenExpression).Evaluate(context));

        Assert.Equal("@" + LimitName, error.Identifier);
        Assert.Equal(new[] { AmountKey }, error.AvailableKeys);
    }

    [Fact]
    public void Evaluate_UnknownMember_ThrowsBindingExceptionListingAccessibleMembers()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((CustomerKey, new Customer()));

        RuleExpressionBindingException error = Assert.Throws<RuleExpressionBindingException>(() => RuleExpression.Parse("customer.Nope == 1").Evaluate(context));

        Assert.Equal("customer.Nope", error.Identifier);
        Assert.Equal(new[] { "Address", "Age", "Name", "Ssn", "Tier" }, error.AvailableKeys);
    }

    [Fact]
    public void Evaluate_UnknownNestedMember_NamesTheFullPath()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((CustomerKey, new Customer { Address = new Address() }));

        RuleExpressionBindingException error = Assert.Throws<RuleExpressionBindingException>(() => RuleExpression.Parse("customer.Address.Zip == 1").Evaluate(context));

        Assert.Equal("customer.Address.Zip", error.Identifier);
        Assert.Equal(new[] { "City" }, error.AvailableKeys);
    }

    [Fact]
    public void Evaluate_MemberOnPrimitive_ListsBuiltIns()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, "text"));

        RuleExpressionBindingException error = Assert.Throws<RuleExpressionBindingException>(() => RuleExpression.Parse("amount.Nope == 1").Evaluate(context));

        Assert.Contains("Count", error.AvailableKeys);
        Assert.Contains("Length", error.AvailableKeys);
    }

    [Fact]
    public void Evaluate_MemberNameInOtherCase_BindsWhenUnambiguous()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((CustomerKey, new Customer { Tier = CustomerTier.Vip }));

        Assert.True(RuleExpression.Parse("customer.tier == 'vip'").Evaluate(context));
    }

    [Fact]
    public void Evaluate_MemberNameInOtherCase_ExactMatchWinsOverCaseInsensitive()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext(("subject", new CaseCollidingSubject { Value = 1, VALUE = 2 }));

        Assert.True(RuleExpression.Parse("subject.Value == 1").Evaluate(context));
        Assert.True(RuleExpression.Parse("subject.VALUE == 2").Evaluate(context));
    }

    [Fact]
    public void Evaluate_MemberNameAmbiguousIgnoringCase_ThrowsBindingException()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext(("subject", new CaseCollidingSubject { Value = 1, VALUE = 2 }));

        Assert.Throws<RuleExpressionBindingException>(() => RuleExpression.Parse("subject.vAlUe == 1").Evaluate(context));
    }

    [Fact]
    public void Evaluate_ThrowingGetter_PropagatesTheOriginalException()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext(("subject", new ThrowingSubject()));

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => RuleExpression.Parse("subject.Boom == 1").Evaluate(context));

        Assert.Equal(ThrowingSubject.FailureMessage, error.Message);
    }

    [Fact]
    public void Evaluate_IndexerIsNotAMember()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext(("list", new List<int> { 1 }));

        Assert.Throws<RuleExpressionBindingException>(() => RuleExpression.Parse("list.Item == 1").Evaluate(context));
    }

    // ---------------------------------------------------------------------
    // Member filter and [RuleAccessible]
    // ---------------------------------------------------------------------

    [Fact]
    public void Evaluate_MemberFilterRejects_ThrowsBindingException()
    {
        RuleExpressionOptions options = RuleExpressionOptions.Default.WithMemberFilter(property => property.Name != nameof(Customer.Ssn));
        BusinessRuleContext context = RuleTestHelper.CreateContext((CustomerKey, new Customer { Ssn = "123", Name = "Alice" }));

        RuleExpressionBindingException error = Assert.Throws<RuleExpressionBindingException>(() => RuleExpression.Parse("customer.Ssn == '123'", options).Evaluate(context));

        Assert.Equal("customer.Ssn", error.Identifier);
        Assert.DoesNotContain(nameof(Customer.Ssn), error.AvailableKeys);
        Assert.True(RuleExpression.Parse("customer.Name == 'Alice'", options).Evaluate(context));
    }

    [Fact]
    public void Evaluate_RequireAttribute_ReadsAnnotatedProperty()
    {
        RuleExpressionOptions options = RuleExpressionOptions.Default.WithRequireRuleAccessibleAttribute(true);
        BusinessRuleContext context = RuleTestHelper.CreateContext((CustomerKey, new AnnotatedCustomer { Tier = CustomerTier.Vip }));

        Assert.True(RuleExpression.Parse("customer.Tier == 'vip'", options).Evaluate(context));
    }

    [Fact]
    public void Evaluate_RequireAttribute_RejectsUnannotatedProperty()
    {
        RuleExpressionOptions options = RuleExpressionOptions.Default.WithRequireRuleAccessibleAttribute(true);
        BusinessRuleContext context = RuleTestHelper.CreateContext((CustomerKey, new AnnotatedCustomer { Ssn = "123" }));

        RuleExpressionBindingException error = Assert.Throws<RuleExpressionBindingException>(() => RuleExpression.Parse("customer.Ssn == '123'", options).Evaluate(context));

        Assert.Equal(new[] { nameof(AnnotatedCustomer.Tier) }, error.AvailableKeys);
    }

    [Fact]
    public void Evaluate_RequireAttribute_TypeLevelAttributeCoversEveryProperty()
    {
        RuleExpressionOptions options = RuleExpressionOptions.Default.WithRequireRuleAccessibleAttribute(true);
        BusinessRuleContext context = RuleTestHelper.CreateContext(("order", new AnnotatedOrder { Total = 5, Customer = new AnnotatedCustomer { Tier = CustomerTier.Gold } }));

        Assert.True(RuleExpression.Parse("order.Total == 5 && order.Customer.Tier == 'gold'", options).Evaluate(context));
    }

    [Fact]
    public void Evaluate_RequireAttribute_CountAndLengthStayReadable()
    {
        RuleExpressionOptions options = RuleExpressionOptions.Default.WithRequireRuleAccessibleAttribute(true);
        BusinessRuleContext context = RuleTestHelper.CreateContext((CustomerKey, new AnnotatedCustomer { Notes = new List<string> { "a", "b" } }), ("name", "Alice"));

        Assert.True(RuleExpression.Parse("name.Length == 5", options).Evaluate(context));
        Assert.Throws<RuleExpressionBindingException>(() => RuleExpression.Parse("customer.Notes.Count == 2", options).Evaluate(context));
    }

    [Fact]
    public void Evaluate_FilterAndAttribute_BothMustAllow()
    {
        RuleExpressionOptions options = RuleExpressionOptions.Default
            .WithRequireRuleAccessibleAttribute(true)
            .WithMemberFilter(property => property.Name != nameof(AnnotatedCustomer.Tier));
        BusinessRuleContext context = RuleTestHelper.CreateContext((CustomerKey, new AnnotatedCustomer { Tier = CustomerTier.Vip }));

        Assert.Throws<RuleExpressionBindingException>(() => RuleExpression.Parse("customer.Tier == 'vip'", options).Evaluate(context));
    }

    // ---------------------------------------------------------------------
    // Parameter resolution (sync)
    // ---------------------------------------------------------------------

    [Fact]
    public void Evaluate_Parameter_FallsBackToContextBag()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m), (LimitName, 100m));

        Assert.True(RuleExpression.Parse(BrokenExpression).Evaluate(context));
    }

    [Fact]
    public void Evaluate_Parameter_InjectedProviderWinsOverContextBag()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m), (LimitName, 100m));
        InMemoryRuleParameterProvider provider = RuleTestHelper.CreateParameterProvider(LimitName, 1_000m);

        Assert.False(RuleExpression.Parse(BrokenExpression).Evaluate(context, provider));
    }

    [Fact]
    public void Evaluate_Parameter_ContextProviderOverridesInjectedProvider()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m));
        context.Set(RuleParameters.ContextKey, RuleTestHelper.CreateParameterProvider(LimitName, 100m));
        InMemoryRuleParameterProvider injected = RuleTestHelper.CreateParameterProvider(LimitName, 1_000m);

        Assert.True(RuleExpression.Parse(BrokenExpression).Evaluate(context, injected));
    }

    [Fact]
    public void Evaluate_Parameter_ProviderWithoutValue_FallsThroughToNextSource()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m), (LimitName, 100m));
        context.Set(RuleParameters.ContextKey, new InMemoryRuleParameterProvider());

        Assert.True(RuleExpression.Parse(BrokenExpression).Evaluate(context, new InMemoryRuleParameterProvider()));
    }

    [Fact]
    public void Evaluate_Parameter_ResolvesForTheScopeOnTheContext()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider()
            .Set(LimitName, 100m)
            .Set(LimitName, 1_000m, Acme);
        BusinessRuleContext global = RuleTestHelper.CreateContext((AmountKey, 500m));
        BusinessRuleContext acme = RuleTestHelper.CreateContext((AmountKey, 500m));
        acme.Set(RuleParameters.ScopeKey, Acme);

        Assert.True(RuleExpression.Parse(BrokenExpression).Evaluate(global, provider));
        Assert.False(RuleExpression.Parse(BrokenExpression).Evaluate(acme, provider));
    }

    [Fact]
    public void Evaluate_Parameter_WrongTypedScopeKey_ThrowsContextValueTypeMismatchException()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m), (RuleParameters.ScopeKeyName, "acme"));

        Assert.Throws<ContextValueTypeMismatchException>(() => RuleExpression.Parse(BrokenExpression).Evaluate(context, RuleTestHelper.CreateParameterProvider(LimitName, 1m)));
    }

    [Fact]
    public void Evaluate_Parameter_WrongTypedProviderKey_ThrowsContextValueTypeMismatchException()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m), (RuleParameters.ProviderKeyName, "not a provider"));

        Assert.Throws<ContextValueTypeMismatchException>(() => RuleExpression.Parse(BrokenExpression).Evaluate(context));
    }

    [Fact]
    public void Evaluate_Parameter_StoredNullScope_IsGlobal()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m));
        context.Set<RuleScope?>(RuleParameters.ScopeKeyName, null);
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider().Set(LimitName, 100m).Set(LimitName, 1_000m, Acme);

        Assert.True(RuleExpression.Parse(BrokenExpression).Evaluate(context, provider));
    }

    [Fact]
    public void Evaluate_Parameter_NullValueFromProvider_BindsAsNull()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m));
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider().Set<string?>(LimitName, null);

        Assert.True(RuleExpression.Parse("@maxOrderAmount == null").Evaluate(context, provider));
    }

    [Fact]
    public void Evaluate_ParameterMemberAccess_Works()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 5));
        InMemoryRuleParameterProvider provider = RuleTestHelper.CreateParameterProvider("allowed", new List<int> { 5, 6 });

        Assert.True(RuleExpression.Parse("@allowed.Count == 2 && amount in @allowed").Evaluate(context, provider));
    }

    // ---------------------------------------------------------------------
    // Parameter resolution (async)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAsync_WithoutParameters_Evaluates()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 5));

        Assert.True(await RuleExpression.Parse("amount > 1").EvaluateAsync(context, cancellationToken: TestToken));
    }

    [Fact]
    public async Task EvaluateAsync_Parameter_FallsBackToContextBag()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m), (LimitName, 100m));

        Assert.True(await RuleExpression.Parse(BrokenExpression).EvaluateAsync(context, cancellationToken: TestToken));
    }

    [Fact]
    public async Task EvaluateAsync_Parameter_AsyncProviderWinsOverSyncProvider()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m));
        InMemoryRuleParameterProvider asyncProvider = RuleTestHelper.CreateParameterProvider(LimitName, 1_000m);
        InMemoryRuleParameterProvider syncProvider = RuleTestHelper.CreateParameterProvider(LimitName, 100m);

        Assert.False(await RuleExpression.Parse(BrokenExpression).EvaluateAsync(context, asyncProvider, syncProvider, TestToken));
    }

    [Fact]
    public async Task EvaluateAsync_Parameter_AsyncContextKeyWinsOverEverything()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m), (LimitName, 1m));
        context.Set(RuleParameters.AsyncContextKey, RuleTestHelper.CreateParameterProvider(LimitName, 1_000m));
        context.Set(RuleParameters.ContextKey, RuleTestHelper.CreateParameterProvider(LimitName, 1m));

        Assert.False(await RuleExpression.Parse(BrokenExpression).EvaluateAsync(context, RuleTestHelper.CreateParameterProvider(LimitName, 1m), RuleTestHelper.CreateParameterProvider(LimitName, 1m), TestToken));
    }

    [Fact]
    public async Task EvaluateAsync_Parameter_SyncContextKeyWinsOverInjectedAsync()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m));
        context.Set(RuleParameters.ContextKey, RuleTestHelper.CreateParameterProvider(LimitName, 1_000m));

        Assert.False(await RuleExpression.Parse(BrokenExpression).EvaluateAsync(context, RuleTestHelper.CreateParameterProvider(LimitName, 1m), null, TestToken));
    }

    [Fact]
    public async Task EvaluateAsync_Parameter_InjectedSyncProviderIsLastResortBeforeBag()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m), (LimitName, 1m));

        Assert.False(await RuleExpression.Parse(BrokenExpression).EvaluateAsync(context, null, RuleTestHelper.CreateParameterProvider(LimitName, 1_000m), TestToken));
    }

    [Fact]
    public async Task EvaluateAsync_Parameter_ResolvesForTheScopeOnTheContext()
    {
        InMemoryRuleParameterProvider provider = new InMemoryRuleParameterProvider().Set(LimitName, 100m).Set(LimitName, 1_000m, Acme);
        BusinessRuleContext acme = RuleTestHelper.CreateContext((AmountKey, 500m));
        acme.Set(RuleParameters.ScopeKey, Acme);

        Assert.False(await RuleExpression.Parse(BrokenExpression).EvaluateAsync(acme, provider, cancellationToken: TestToken));
    }

    [Fact]
    public async Task EvaluateAsync_UnboundParameter_ThrowsBindingException()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m));

        RuleExpressionBindingException error = await Assert.ThrowsAsync<RuleExpressionBindingException>(() => RuleExpression.Parse(BrokenExpression).EvaluateAsync(context, new InMemoryRuleParameterProvider(), cancellationToken: TestToken));

        Assert.Equal("@" + LimitName, error.Identifier);
    }

    [Fact]
    public async Task EvaluateAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m));
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RuleExpression.Parse("amount > 1").EvaluateAsync(context, cancellationToken: source.Token));
    }

    [Fact]
    public async Task EvaluateAsync_CancelledTokenWithParameters_ThrowsOperationCanceledException()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m));
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RuleExpression.Parse(BrokenExpression).EvaluateAsync(context, RuleTestHelper.CreateParameterProvider(LimitName, 1m), cancellationToken: source.Token));
    }

    [Fact]
    public async Task EvaluateAsync_PassesTheTokenToTheAsyncProvider()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m));
        RecordingAsyncProvider provider = new RecordingAsyncProvider(100m);
        using CancellationTokenSource source = new CancellationTokenSource();

        await RuleExpression.Parse(BrokenExpression).EvaluateAsync(context, provider, cancellationToken: source.Token);

        Assert.Equal(source.Token, provider.LastToken);
        Assert.Equal(new[] { LimitName }, provider.Requested);
    }

    [Fact]
    public async Task EvaluateAsync_ResolvesEachParameterOnce()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m));
        RecordingAsyncProvider provider = new RecordingAsyncProvider(100m);

        await RuleExpression.Parse("amount > @maxOrderAmount && amount > @maxOrderAmount + 1").EvaluateAsync(context, provider, cancellationToken: TestToken);

        Assert.Single(provider.Requested);
    }

    // ---------------------------------------------------------------------
    // Exception type
    // ---------------------------------------------------------------------

    [Fact]
    public void BindingException_SortsKeysOrdinally()
    {
        RuleExpressionBindingException error = new RuleExpressionBindingException("x", new[] { "b", "B", "a" });

        Assert.Equal(new[] { "B", "a", "b" }, error.AvailableKeys);
        Assert.Equal("x", error.Identifier);
    }

    [Fact]
    public void BindingException_NullArguments_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RuleExpressionBindingException(null!, Array.Empty<string>()));
        Assert.Throws<ArgumentNullException>(() => new RuleExpressionBindingException("x", null!));
    }

    [Fact]
    public void EvaluationException_ExposesNodeTextAndInner()
    {
        InvalidOperationException inner = new InvalidOperationException("inner");

        RuleExpressionEvaluationException error = new RuleExpressionEvaluationException("a + b", "boom", inner);

        Assert.Equal("a + b", error.NodeText);
        Assert.Same(inner, error.InnerException);
        Assert.Equal("boom in 'a + b'.", error.Message);
    }

    private sealed class RecordingAsyncProvider : IAsyncRuleParameterProvider
    {
        private readonly object _value;

        public RecordingAsyncProvider(object value) => _value = value;

        public List<string> Requested { get; } = new List<string>();

        public CancellationToken LastToken { get; private set; }

        public Task<RuleParameterLookup<T>> TryGetAsync<T>(string name, RuleScope? scope, CancellationToken cancellationToken = default)
        {
            Requested.Add(name);
            LastToken = cancellationToken;
            return Task.FromResult(RuleParameterLookup<T>.Of((T)_value));
        }
    }
}
