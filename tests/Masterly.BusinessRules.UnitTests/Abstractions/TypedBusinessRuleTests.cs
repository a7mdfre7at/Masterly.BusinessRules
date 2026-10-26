using System.Globalization;

namespace Masterly.BusinessRules.UnitTests;

public class TypedBusinessRuleTests
{
    private const string Code = "ORDER.NO_ITEMS";
    private const string Message = "Order must contain at least one item.";
    private const string LimitKey = "limit";

    // ---------------------------------------------------------------------
    // Metadata defaults
    // ---------------------------------------------------------------------

    [Fact]
    public void BaseBusinessRuleOfT_HasDefaultMetadata()
    {
        OrderMustHaveItemsRule rule = new OrderMustHaveItemsRule();

        Assert.Equal(Code, rule.Code);
        Assert.Equal(Message, rule.Message);
        Assert.Equal(RuleSeverity.Error, rule.Severity);
        Assert.Equal(nameof(OrderMustHaveItemsRule), rule.Name);
        Assert.Equal(string.Empty, rule.Description);
        Assert.Equal(string.Empty, rule.Category);
        Assert.Empty(rule.Tags);
    }

    [Fact]
    public void BaseBusinessRuleOfT_CanOverrideMetadata()
    {
        OrderTotalRule rule = new OrderTotalRule();

        Assert.Equal(RuleSeverity.Warning, rule.Severity);
        Assert.Equal("Order total", rule.Name);
        Assert.Equal("Checks the order total", rule.Description);
        Assert.Equal("Orders", rule.Category);
        Assert.Equal(new[] { "order", "total" }, rule.Tags);
    }

    // ---------------------------------------------------------------------
    // IsBroken
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void IsBroken_WithSubject_EvaluatesTheSubject(int itemCount, bool expected)
    {
        OrderMustHaveItemsRule rule = new OrderMustHaveItemsRule();

        bool isBroken = rule.IsBroken(new Order(itemCount, 10m));

        Assert.Equal(expected, isBroken);
    }

    [Fact]
    public void IsBroken_WithContext_DefaultsToSubjectOnlyOverload()
    {
        CountingRule rule = new CountingRule();

        bool isBroken = rule.IsBroken(new Order(0, 10m), new BusinessRuleContext());

        Assert.True(isBroken);
        Assert.Equal(1, rule.SubjectOnlyCalls);
    }

    [Fact]
    public void IsBroken_WithContext_OverrideReceivesSubjectAndContext()
    {
        OrderTotalRule rule = new OrderTotalRule();
        BusinessRuleContext context = RuleTestHelper.CreateContext((LimitKey, 50m));

        Assert.True(rule.IsBroken(new Order(1, 100m), context));
        Assert.False(rule.IsBroken(new Order(1, 10m), context));
    }

    [Fact]
    public void IsBroken_SameInstance_ServesManySubjects()
    {
        OrderMustHaveItemsRule rule = new OrderMustHaveItemsRule();

        Assert.True(rule.IsBroken(new Order(0, 1m)));
        Assert.False(rule.IsBroken(new Order(3, 1m)));
        Assert.True(rule.IsBroken(new Order(0, 1m)));
    }

    [Fact]
    public void IsBroken_StructSubject_IsSupported()
    {
        NegativeNumberRule rule = new NegativeNumberRule();

        Assert.True(rule.IsBroken(-1));
        Assert.False(rule.IsBroken(0));
    }

    [Fact]
    public void IsBroken_NullSubject_IsHandedToTheRule()
    {
        OrderMustExistRule rule = new OrderMustExistRule();

        Assert.True(rule.IsBroken(null!));
        Assert.False(rule.IsBroken(new Order(1, 1m)));
    }

    // ---------------------------------------------------------------------
    // Evaluate / Describe
    // ---------------------------------------------------------------------

    [Fact]
    public void Evaluate_IsAPureFactoryCarryingMetadata()
    {
        CountingRule rule = new CountingRule();

        BusinessRuleResult result = rule.Evaluate(new Order(0, 1m));

        Assert.Equal(0, rule.SubjectOnlyCalls);
        Assert.Equal(Code, result.Code);
        Assert.Equal(Message, result.Message);
        Assert.Equal(RuleSeverity.Error, result.Severity);
        Assert.Equal(nameof(CountingRule), result.Name);
    }

    [Fact]
    public void Evaluate_WithoutContext_CallsDescribeWithNullContextAndSubject()
    {
        DescribingRule rule = new DescribingRule();

        BusinessRuleResult result = rule.Evaluate(new Order(7, 1m));

        Assert.Null(rule.LastContext);
        Assert.Equal(7, rule.LastSubject!.ItemCount);
        Assert.Equal(7, result.Actual);
        Assert.Equal("Order has 7 items.", result.Detail);
    }

    [Fact]
    public void Evaluate_WithContext_CallsDescribeWithContextAndUsesItsFormatProvider()
    {
        DescribingRule rule = new DescribingRule();
        BusinessRuleContext context = new BusinessRuleContext { FormatProvider = new CultureInfo("de-DE") };

        BusinessRuleResult result = rule.Evaluate(new Order(1, 1234.5m), context);

        Assert.Same(context, rule.LastContext);
        Assert.Equal("Total is 1234,5.", result.Expected);
    }

    [Fact]
    public void Evaluate_WhenDescribeThrows_DegradesToEmptyDetails()
    {
        ThrowingDescribeRule rule = new ThrowingDescribeRule();

        BusinessRuleResult result = rule.Evaluate(new Order(0, 1m));

        Assert.Equal(Code, result.Code);
        Assert.Null(result.Detail);
        Assert.Null(result.Actual);
    }

    [Fact]
    public void Describe_IsNotCalledByIsBroken()
    {
        DescribingRule rule = new DescribingRule();

        rule.IsBroken(new Order(0, 1m));
        rule.IsBroken(new Order(0, 1m), new BusinessRuleContext());

        Assert.Equal(0, rule.DescribeCalls);
    }

    // ---------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------

    private sealed class Order
    {
        public Order(int itemCount, decimal total)
        {
            ItemCount = itemCount;
            Total = total;
        }

        public int ItemCount { get; }
        public decimal Total { get; }
    }

    private sealed class OrderMustHaveItemsRule : BaseBusinessRule<Order>
    {
        public override string Code => TypedBusinessRuleTests.Code;
        public override string Message => TypedBusinessRuleTests.Message;
        public override bool IsBroken(Order order) => order.ItemCount == 0;
    }

    private sealed class OrderMustExistRule : BaseBusinessRule<Order>
    {
        public override string Code => "ORDER.MISSING";
        public override string Message => "Order is required.";
        public override bool IsBroken(Order order) => order == null;
    }

    private sealed class NegativeNumberRule : BaseBusinessRule<int>
    {
        public override string Code => "NUMBER.NEGATIVE";
        public override string Message => "Number must not be negative.";
        public override bool IsBroken(int number) => number < 0;
    }

    private sealed class OrderTotalRule : BaseBusinessRule<Order>
    {
        public override string Code => "ORDER.TOTAL";
        public override string Message => "Order total exceeds the limit.";
        public override RuleSeverity Severity => RuleSeverity.Warning;
        public override string Name => "Order total";
        public override string Description => "Checks the order total";
        public override string Category => "Orders";
        public override IReadOnlyList<string> Tags => new[] { "order", "total" };
        public override bool IsBroken(Order order) => false;
        public override bool IsBroken(Order order, BusinessRuleContext context) => order.Total > context.Get<decimal>(LimitKey);
    }

    private sealed class CountingRule : BaseBusinessRule<Order>
    {
        public int SubjectOnlyCalls { get; private set; }
        public override string Code => TypedBusinessRuleTests.Code;
        public override string Message => TypedBusinessRuleTests.Message;

        public override bool IsBroken(Order order)
        {
            SubjectOnlyCalls++;
            return order.ItemCount == 0;
        }
    }

    private sealed class DescribingRule : BaseBusinessRule<Order>
    {
        public int DescribeCalls { get; private set; }
        public Order? LastSubject { get; private set; }
        public BusinessRuleContext? LastContext { get; private set; }
        public override string Code => TypedBusinessRuleTests.Code;
        public override string Message => TypedBusinessRuleTests.Message;
        public override bool IsBroken(Order order) => order.ItemCount == 0;

        protected override RuleResultDetails Describe(Order order, BusinessRuleContext? context)
        {
            DescribeCalls++;
            LastSubject = order;
            LastContext = context;
            return RuleResultDetails.Detail($"Order has {order.ItemCount} items.")
                .Actual(order.ItemCount)
                .Expected(string.Format(context?.FormatProvider ?? CultureInfo.InvariantCulture, "Total is {0}.", order.Total));
        }
    }

    private sealed class ThrowingDescribeRule : BaseBusinessRule<Order>
    {
        public override string Code => TypedBusinessRuleTests.Code;
        public override string Message => TypedBusinessRuleTests.Message;
        public override bool IsBroken(Order order) => true;
        protected override RuleResultDetails Describe(Order order, BusinessRuleContext? context) => throw new InvalidOperationException("boom");
    }
}
