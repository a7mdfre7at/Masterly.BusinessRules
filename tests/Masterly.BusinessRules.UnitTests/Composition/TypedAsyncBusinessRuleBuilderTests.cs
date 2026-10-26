using System.Globalization;

namespace Masterly.BusinessRules.UnitTests;

public class TypedAsyncBusinessRuleBuilderTests
{
    private const string Code = "ORDER.TOO_LARGE";
    private const string Message = "Order has too many items.";
    private const string FlagKey = "flag";
    private const int Limit = 100;

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------
    // Conditions
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(101, true)]
    [InlineData(100, false)]
    public async Task WhenAsync_WithSubject_EvaluatesTheSubject(int itemCount, bool expected)
    {
        IAsyncBusinessRule<Order> rule = AsyncBusinessRuleBuilder<Order>.Create(Code).WithMessage(Message)
            .WhenAsync(order => Task.FromResult(order.ItemCount > Limit))
            .Build();

        Assert.Equal(expected, await rule.IsBrokenAsync(new Order(itemCount), new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task WhenAsync_WithSubjectAndContext_ReceivesBoth()
    {
        IAsyncBusinessRule<Order> rule = AsyncBusinessRuleBuilder<Order>.Create(Code).WithMessage(Message)
            .WhenAsync((order, ctx) => Task.FromResult(order.ItemCount > 0 && ctx.Get<bool>(FlagKey)))
            .Build();

        Assert.True(await rule.IsBrokenAsync(new Order(1), RuleTestHelper.CreateContext((FlagKey, true)), TestToken));
        Assert.False(await rule.IsBrokenAsync(new Order(1), RuleTestHelper.CreateContext((FlagKey, false)), TestToken));
        Assert.False(await rule.IsBrokenAsync(new Order(0), RuleTestHelper.CreateContext((FlagKey, true)), TestToken));
    }

    [Fact]
    public async Task WhenAsync_WithSubjectContextAndToken_ForwardsTheToken()
    {
        CancellationToken? observed = null;
        IAsyncBusinessRule<Order> rule = AsyncBusinessRuleBuilder<Order>.Create(Code).WithMessage(Message)
            .WhenAsync((_, _, ct) => { observed = ct; return Task.FromResult(true); })
            .Build();
        using CancellationTokenSource source = new CancellationTokenSource();

        await rule.IsBrokenAsync(new Order(1), new BusinessRuleContext(), source.Token);

        Assert.Equal(source.Token, observed);
    }

    [Fact]
    public async Task WhenAsync_CancelledToken_PropagatesCancellation()
    {
        IAsyncBusinessRule<Order> rule = AsyncBusinessRuleBuilder<Order>.Create(Code).WithMessage(Message)
            .WhenAsync(async (_, _, ct) => { await Task.Delay(1, ct); return true; })
            .Build();
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rule.IsBrokenAsync(new Order(1), new BusinessRuleContext(), source.Token));
    }

    [Fact]
    public void Build_WithoutCondition_ThrowsInvalidOperationException()
    {
        AsyncBusinessRuleBuilder<Order> builder = AsyncBusinessRuleBuilder<Order>.Create(Code).WithMessage(Message);

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public async Task WhenAsync_StructSubject_IsSupported()
    {
        IAsyncBusinessRule<int> rule = AsyncBusinessRuleBuilder<int>.Create(Code).WithMessage(Message).WhenAsync(value => Task.FromResult(value < 0)).Build();

        Assert.True(await rule.IsBrokenAsync(-1, new BusinessRuleContext(), TestToken));
        Assert.False(await rule.IsBrokenAsync(1, new BusinessRuleContext(), TestToken));
    }

    // ---------------------------------------------------------------------
    // Metadata
    // ---------------------------------------------------------------------

    [Fact]
    public void Build_CarriesConfiguredMetadata()
    {
        IAsyncBusinessRule<Order> rule = AsyncBusinessRuleBuilder<Order>.Create(Code)
            .WithMessage(Message)
            .WithSeverity(RuleSeverity.Warning)
            .WithName("Too large")
            .WithDescription("Checks the item count")
            .WithCategory("Orders")
            .WithTags("order", "size")
            .WhenAsync(_ => Task.FromResult(true))
            .Build();

        Assert.Equal(Code, rule.Code);
        Assert.Equal(Message, rule.Message);
        Assert.Equal(RuleSeverity.Warning, rule.Severity);
        Assert.Equal("Too large", rule.Name);
        Assert.Equal("Checks the item count", rule.Description);
        Assert.Equal("Orders", rule.Category);
        Assert.Equal(new[] { "order", "size" }, rule.Tags);
    }

    [Fact]
    public void Build_WithoutName_DefaultsNameToCode()
    {
        IAsyncBusinessRule<Order> rule = AsyncBusinessRuleBuilder<Order>.Create(Code).WhenAsync(_ => Task.FromResult(true)).Build();

        Assert.Equal(Code, rule.Name);
        Assert.Equal("Async business rule violated.", rule.Message);
    }

    [Fact]
    public void Builder_IsABusinessRuleMetadataBuilder_AndChainsKeepTheTypedBuilder()
    {
        AsyncBusinessRuleBuilder<Order> builder = AsyncBusinessRuleBuilder<Order>.Create(Code);

        AsyncBusinessRuleBuilder<Order> chained = builder.WithMessage(Message).WithCategory("x");

        Assert.Same(builder, chained);
        Assert.IsAssignableFrom<BusinessRuleMetadataBuilder<AsyncBusinessRuleBuilder<Order>>>(builder);
    }

    // ---------------------------------------------------------------------
    // Details
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAsync_WhenPassing_ReturnsNull()
    {
        IAsyncBusinessRule<Order> rule = AsyncBusinessRuleBuilder<Order>.Create(Code).WithMessage(Message).WhenAsync(_ => Task.FromResult(false)).Build();

        Assert.Null(await rule.EvaluateAsync(new Order(1), new BusinessRuleContext(), TestToken));
    }

    [Fact]
    public async Task EvaluateAsync_WithSubjectAwareDetails_BindsTheSubject()
    {
        IAsyncBusinessRule<Order> rule = AsyncBusinessRuleBuilder<Order>.Create(Code).WithMessage(Message)
            .WithDetail((order, _) => $"Order has {order.ItemCount} items.")
            .WithActual((order, _) => order.ItemCount)
            .WithExpected((_, _) => Limit)
            .WithRemediation((order, _) => $"Remove {order.ItemCount - Limit} items.")
            .WhenAsync(order => Task.FromResult(order.ItemCount > Limit))
            .Build();

        BusinessRuleResult? result = await rule.EvaluateAsync(new Order(105), new BusinessRuleContext(), TestToken);

        Assert.NotNull(result);
        Assert.Equal("Order has 105 items.", result.Detail);
        Assert.Equal(105, result.Actual);
        Assert.Equal(Limit, result.Expected);
        Assert.Equal("Remove 5 items.", result.Remediation);
    }

    [Fact]
    public async Task EvaluateAsync_SubjectAwareDetailsReceiveTheContextAndItsFormatProvider()
    {
        BusinessRuleContext context = new BusinessRuleContext { FormatProvider = new CultureInfo("de-DE") };
        context.Set(FlagKey, true);
        IAsyncBusinessRule<Order> rule = AsyncBusinessRuleBuilder<Order>.Create(Code).WithMessage(Message)
            .WithDetail((order, ctx) => $"Flag {ctx.Get<bool>(FlagKey)} with total {order.Total}.")
            .WhenAsync(_ => Task.FromResult(true))
            .Build();

        BusinessRuleResult? result = await rule.EvaluateAsync(new Order(1, 1234.5m), context, TestToken);

        Assert.NotNull(result);
        Assert.Equal("Flag True with total 1234,5.", result.Detail);
    }

    [Fact]
    public async Task EvaluateAsync_WithContextOnlyDetails_StillWorksOnTheTypedBuilder()
    {
        IAsyncBusinessRule<Order> rule = AsyncBusinessRuleBuilder<Order>.Create(Code).WithMessage(Message)
            .WithDetail(ctx => $"Flag is {ctx.Get<bool>(FlagKey)}.")
            .WithExpected(Limit)
            .WithRemediation("Reduce the order.")
            .WhenAsync(_ => Task.FromResult(true))
            .Build();

        BusinessRuleResult? result = await rule.EvaluateAsync(new Order(1), RuleTestHelper.CreateContext((FlagKey, true)), TestToken);

        Assert.NotNull(result);
        Assert.Equal("Flag is True.", result.Detail);
        Assert.Equal(Limit, result.Expected);
        Assert.Equal("Reduce the order.", result.Remediation);
    }

    [Fact]
    public async Task EvaluateAsync_SubjectAwareDetail_TakesPrecedenceOverContextOnlyDetail()
    {
        IAsyncBusinessRule<Order> rule = AsyncBusinessRuleBuilder<Order>.Create(Code).WithMessage(Message)
            .WithDetail(_ => $"context only")
            .WithActual(_ => "context actual")
            .WithDetail((order, _) => $"subject {order.ItemCount}")
            .WhenAsync(_ => Task.FromResult(true))
            .Build();

        BusinessRuleResult? result = await rule.EvaluateAsync(new Order(3), new BusinessRuleContext(), TestToken);

        Assert.NotNull(result);
        Assert.Equal("subject 3", result.Detail);
        Assert.Equal("context actual", result.Actual);
    }

    [Fact]
    public async Task EvaluateAsync_WhenADetailFunctionThrows_DegradesToEmptyDetails()
    {
        IAsyncBusinessRule<Order> rule = AsyncBusinessRuleBuilder<Order>.Create(Code).WithMessage(Message)
            .WithActual((_, _) => 1)
            .WithDetail((_, _) => throw new InvalidOperationException("boom"))
            .WhenAsync(_ => Task.FromResult(true))
            .Build();

        BusinessRuleResult? result = await rule.EvaluateAsync(new Order(1), new BusinessRuleContext(), TestToken);

        Assert.NotNull(result);
        Assert.Equal(Code, result.Code);
        Assert.Null(result.Detail);
        Assert.Null(result.Actual);
    }

    [Fact]
    public async Task DetailFunctions_AreNotInvokedOnThePassingPath()
    {
        int detailCalls = 0;
        IAsyncBusinessRule<Order> rule = AsyncBusinessRuleBuilder<Order>.Create(Code).WithMessage(Message)
            .WithActual((_, _) => { detailCalls++; return 1; })
            .WhenAsync(_ => Task.FromResult(false))
            .Build();

        await rule.IsBrokenAsync(new Order(1), new BusinessRuleContext(), TestToken);
        await rule.EvaluateAsync(new Order(1), new BusinessRuleContext(), TestToken);

        Assert.Equal(0, detailCalls);
    }

    [Fact]
    public void WithDetail_Null_ThrowsArgumentNullException()
    {
        AsyncBusinessRuleBuilder<Order> builder = AsyncBusinessRuleBuilder<Order>.Create(Code);

        Assert.Throws<ArgumentNullException>(() => builder.WithDetail((Func<Order, BusinessRuleContext, FormattableString>)null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithActual((Func<Order, BusinessRuleContext, object?>)null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithExpected((Func<Order, BusinessRuleContext, object?>)null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithRemediation((Func<Order, BusinessRuleContext, string>)null!));
    }

    // ---------------------------------------------------------------------
    // Bound and checked
    // ---------------------------------------------------------------------

    [Fact]
    public async Task BuiltRule_BoundToSubject_ChecksLikeAnyRule()
    {
        IAsyncBusinessRule<Order> rule = AsyncBusinessRuleBuilder<Order>.Create(Code).WithMessage(Message)
            .WhenAsync(order => Task.FromResult(order.ItemCount > Limit))
            .Build();

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(() => rule.For(new Order(101)).CheckAsync(new BusinessRuleContext(), TestToken));

        Assert.Equal(Code, Assert.Single(exception.BrokenRules).Code);
        await rule.For(new Order(1)).CheckAsync(new BusinessRuleContext(), TestToken);
    }

    private sealed class Order
    {
        public Order(int itemCount, decimal total = 0m)
        {
            ItemCount = itemCount;
            Total = total;
        }

        public int ItemCount { get; }
        public decimal Total { get; }
    }
}
