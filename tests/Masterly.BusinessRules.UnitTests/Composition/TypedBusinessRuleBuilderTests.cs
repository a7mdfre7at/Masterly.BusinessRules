using System.Globalization;

namespace Masterly.BusinessRules.UnitTests;

public class TypedBusinessRuleBuilderTests
{
    private const string Code = "ORDER.TOO_LARGE";
    private const string Message = "Order has too many items.";
    private const string FlagKey = "flag";
    private const int Limit = 100;

    // ---------------------------------------------------------------------
    // Conditions
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(101, true)]
    [InlineData(100, false)]
    public void When_WithSubject_EvaluatesTheSubject(int itemCount, bool expected)
    {
        IBusinessRule<Order> rule = BusinessRuleBuilder<Order>.Create(Code).WithMessage(Message).When(order => order.ItemCount > Limit).Build();

        Assert.Equal(expected, rule.IsBroken(new Order(itemCount)));
        Assert.Equal(expected, rule.IsBroken(new Order(itemCount), new BusinessRuleContext()));
    }

    [Fact]
    public void When_WithSubjectAndContext_ReceivesBoth()
    {
        IBusinessRule<Order> rule = BusinessRuleBuilder<Order>.Create(Code).WithMessage(Message)
            .When((order, ctx) => order.ItemCount > 0 && ctx.Get<bool>(FlagKey))
            .Build();

        Assert.True(rule.IsBroken(new Order(1), RuleTestHelper.CreateContext((FlagKey, true))));
        Assert.False(rule.IsBroken(new Order(1), RuleTestHelper.CreateContext((FlagKey, false))));
        Assert.False(rule.IsBroken(new Order(0), RuleTestHelper.CreateContext((FlagKey, true))));
    }

    [Fact]
    public void IsBroken_WithoutContext_WhenOnlyContextConditionConfigured_UsesEmptyContext()
    {
        BusinessRuleContext? observed = null;
        IBusinessRule<Order> rule = BusinessRuleBuilder<Order>.Create(Code).WithMessage(Message)
            .When((_, ctx) => { observed = ctx; return true; })
            .Build();

        Assert.True(rule.IsBroken(new Order(1)));
        Assert.NotNull(observed);
        Assert.Equal(0, observed.Count);
    }

    [Fact]
    public void IsBroken_WithContext_WhenOnlySubjectConditionConfigured_UsesSubjectCondition()
    {
        IBusinessRule<Order> rule = BusinessRuleBuilder<Order>.Create(Code).WithMessage(Message).When(order => order.ItemCount == 0).Build();

        Assert.True(rule.IsBroken(new Order(0), new BusinessRuleContext()));
    }

    [Fact]
    public void IsBroken_WhenBothConditionsConfigured_ContextOverloadPrefersContextCondition()
    {
        IBusinessRule<Order> rule = BusinessRuleBuilder<Order>.Create(Code).WithMessage(Message)
            .When(_ => false)
            .When((_, _) => true)
            .Build();

        Assert.False(rule.IsBroken(new Order(0)));
        Assert.True(rule.IsBroken(new Order(0), new BusinessRuleContext()));
    }

    [Fact]
    public void Build_WithoutCondition_ThrowsInvalidOperationException()
    {
        BusinessRuleBuilder<Order> builder = BusinessRuleBuilder<Order>.Create(Code).WithMessage(Message);

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void When_StructSubject_IsSupported()
    {
        IBusinessRule<int> rule = BusinessRuleBuilder<int>.Create(Code).WithMessage(Message).When(value => value < 0).Build();

        Assert.True(rule.IsBroken(-1));
        Assert.False(rule.IsBroken(1));
    }

    // ---------------------------------------------------------------------
    // Metadata
    // ---------------------------------------------------------------------

    [Fact]
    public void Build_CarriesConfiguredMetadata()
    {
        IBusinessRule<Order> rule = BusinessRuleBuilder<Order>.Create(Code)
            .WithMessage(Message)
            .WithSeverity(RuleSeverity.Warning)
            .WithName("Too large")
            .WithDescription("Checks the item count")
            .WithCategory("Orders")
            .WithTags("order")
            .WithTags("size")
            .When(_ => true)
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
        IBusinessRule<Order> rule = BusinessRuleBuilder<Order>.Create(Code).When(_ => true).Build();

        Assert.Equal(Code, rule.Name);
        Assert.Equal("Business rule violated.", rule.Message);
        Assert.Equal(RuleSeverity.Error, rule.Severity);
    }

    [Fact]
    public void Builder_IsABusinessRuleMetadataBuilder_AndChainsKeepTheTypedBuilder()
    {
        BusinessRuleBuilder<Order> builder = BusinessRuleBuilder<Order>.Create(Code);

        BusinessRuleBuilder<Order> chained = builder.WithMessage(Message).WithSeverity(RuleSeverity.Info).WithTags("x");

        Assert.Same(builder, chained);
        Assert.IsAssignableFrom<BusinessRuleMetadataBuilder<BusinessRuleBuilder<Order>>>(builder);
    }

    // ---------------------------------------------------------------------
    // Details
    // ---------------------------------------------------------------------

    [Fact]
    public void Evaluate_WithSubjectAwareDetails_BindsTheSubject()
    {
        IBusinessRule<Order> rule = BusinessRuleBuilder<Order>.Create(Code).WithMessage(Message)
            .WithDetail((order, _) => $"Order has {order.ItemCount} items.")
            .WithActual((order, _) => order.ItemCount)
            .WithExpected((_, _) => Limit)
            .WithRemediation((order, _) => $"Remove {order.ItemCount - Limit} items.")
            .When(order => order.ItemCount > Limit)
            .Build();

        BusinessRuleResult result = rule.Evaluate(new Order(105));

        Assert.Equal("Order has 105 items.", result.Detail);
        Assert.Equal(105, result.Actual);
        Assert.Equal(Limit, result.Expected);
        Assert.Equal("Remove 5 items.", result.Remediation);
    }

    [Fact]
    public void Evaluate_WithContext_SubjectAwareDetailsReceiveTheContextAndItsFormatProvider()
    {
        BusinessRuleContext context = new BusinessRuleContext { FormatProvider = new CultureInfo("de-DE") };
        context.Set(FlagKey, true);
        IBusinessRule<Order> rule = BusinessRuleBuilder<Order>.Create(Code).WithMessage(Message)
            .WithDetail((order, ctx) => $"Flag {ctx.Get<bool>(FlagKey)} with total {order.Total}.")
            .When(_ => true)
            .Build();

        BusinessRuleResult result = rule.Evaluate(new Order(1, 1234.5m), context);

        Assert.Equal("Flag True with total 1234,5.", result.Detail);
    }

    [Fact]
    public void Evaluate_WithContextOnlyDetails_StillWorksOnTheTypedBuilder()
    {
        IBusinessRule<Order> rule = BusinessRuleBuilder<Order>.Create(Code).WithMessage(Message)
            .WithDetail(ctx => $"Flag is {ctx.Get<bool>(FlagKey)}.")
            .WithExpected(Limit)
            .WithRemediation("Reduce the order.")
            .When(_ => true)
            .Build();

        BusinessRuleResult result = rule.Evaluate(new Order(1), RuleTestHelper.CreateContext((FlagKey, true)));

        Assert.Equal("Flag is True.", result.Detail);
        Assert.Equal(Limit, result.Expected);
        Assert.Equal("Reduce the order.", result.Remediation);
    }

    [Fact]
    public void Evaluate_SubjectAwareDetail_TakesPrecedenceOverContextOnlyDetail()
    {
        IBusinessRule<Order> rule = BusinessRuleBuilder<Order>.Create(Code).WithMessage(Message)
            .WithDetail(_ => $"context only")
            .WithActual(_ => "context actual")
            .WithDetail((order, _) => $"subject {order.ItemCount}")
            .When(_ => true)
            .Build();

        BusinessRuleResult result = rule.Evaluate(new Order(3));

        Assert.Equal("subject 3", result.Detail);
        Assert.Equal("context actual", result.Actual);
    }

    [Fact]
    public void Evaluate_WithoutDetails_CarriesMetadataOnly()
    {
        IBusinessRule<Order> rule = BusinessRuleBuilder<Order>.Create(Code).WithMessage(Message).When(_ => true).Build();

        BusinessRuleResult result = rule.Evaluate(new Order(1));

        Assert.Equal(Code, result.Code);
        Assert.Null(result.Detail);
        Assert.Null(result.Actual);
    }

    [Fact]
    public void Evaluate_WhenADetailFunctionThrows_DegradesToEmptyDetails()
    {
        IBusinessRule<Order> rule = BusinessRuleBuilder<Order>.Create(Code).WithMessage(Message)
            .WithActual((_, _) => 1)
            .WithDetail((_, _) => throw new InvalidOperationException("boom"))
            .When(_ => true)
            .Build();

        BusinessRuleResult result = rule.Evaluate(new Order(1));

        Assert.Equal(Code, result.Code);
        Assert.Null(result.Detail);
        Assert.Null(result.Actual);
    }

    [Fact]
    public void DetailFunctions_AreNotInvokedByIsBroken()
    {
        int detailCalls = 0;
        IBusinessRule<Order> rule = BusinessRuleBuilder<Order>.Create(Code).WithMessage(Message)
            .WithActual((_, _) => { detailCalls++; return 1; })
            .When(_ => true)
            .Build();

        rule.IsBroken(new Order(1));
        rule.IsBroken(new Order(1), new BusinessRuleContext());

        Assert.Equal(0, detailCalls);
    }

    [Fact]
    public void WithDetail_Null_ThrowsArgumentNullException()
    {
        BusinessRuleBuilder<Order> builder = BusinessRuleBuilder<Order>.Create(Code);

        Assert.Throws<ArgumentNullException>(() => builder.WithDetail((Func<Order, BusinessRuleContext, FormattableString>)null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithActual((Func<Order, BusinessRuleContext, object?>)null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithExpected((Func<Order, BusinessRuleContext, object?>)null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithRemediation((Func<Order, BusinessRuleContext, string>)null!));
    }

    // ---------------------------------------------------------------------
    // Bound and checked
    // ---------------------------------------------------------------------

    [Fact]
    public void BuiltRule_BoundToSubject_ChecksLikeAnyRule()
    {
        IBusinessRule<Order> rule = BusinessRuleBuilder<Order>.Create(Code).WithMessage(Message).When(order => order.ItemCount > Limit).Build();

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => rule.For(new Order(101)).Check());

        Assert.Equal(Code, Assert.Single(exception.BrokenRules).Code);
        rule.For(new Order(1)).Check();
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
