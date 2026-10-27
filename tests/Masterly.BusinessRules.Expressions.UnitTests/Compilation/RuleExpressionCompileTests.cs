namespace Masterly.BusinessRules.Expressions.UnitTests;

public class RuleExpressionCompileTests
{
    private const string AmountKey = "amount";
    private const string LimitName = "maxOrderAmount";

    private static BusinessRuleContext<Order> Typed(Order order) => new BusinessRuleContext<Order>(order);

    // ---------------------------------------------------------------------
    // Static Data paths
    // ---------------------------------------------------------------------

    [Fact]
    public void Compile_DataPath_EvaluatesAgainstTypedData()
    {
        Func<BusinessRuleContext<Order>, bool> compiled = RuleExpression.Parse("Data.Items.Count == 0").Compile<Order>();

        Assert.True(compiled(Typed(TestOrders.Empty())));
        Assert.False(compiled(Typed(TestOrders.Standard())));
    }

    [Fact]
    public void Compile_MisspelledMember_FailsAtCompileTimeWithPosition()
    {
        RuleExpression expression = RuleExpression.Parse("Data.Total > 1 && Data.Itemz.Count == 0");

        RuleExpressionParseException error = Assert.Throws<RuleExpressionParseException>(() => expression.Compile<Order>());

        Assert.Equal(1, error.Line);
        Assert.Equal(24, error.Column);
        Assert.Contains("Itemz", error.Message);
        Assert.Contains(nameof(Order), error.Message);
        Assert.Contains(nameof(Order.Items), error.Message);
    }

    [Fact]
    public void Compile_MisspelledNestedMember_NamesTheDeclaringType()
    {
        RuleExpressionParseException error = Assert.Throws<RuleExpressionParseException>(() => RuleExpression.Parse("Data.Customer.Tir == 'vip'").Compile<Order>());

        Assert.Contains(nameof(Customer), error.Message);
        Assert.Equal(15, error.Column);
    }

    [Fact]
    public void Compile_MemberFilter_AppliesAtCompileTime()
    {
        RuleExpressionOptions options = RuleExpressionOptions.Default.WithMemberFilter(property => property.Name != nameof(Customer.Ssn));

        Assert.Throws<RuleExpressionParseException>(() => RuleExpression.Parse("Data.Customer.Ssn == ''", options).Compile<Order>());
    }

    [Fact]
    public void Compile_RequireAttribute_AppliesAtCompileTime()
    {
        RuleExpressionOptions options = RuleExpressionOptions.Default.WithRequireRuleAccessibleAttribute(true);

        Func<BusinessRuleContext<AnnotatedOrder>, bool> compiled = RuleExpression.Parse("Data.Customer.Tier == 'vip'", options).Compile<AnnotatedOrder>();
        Assert.Throws<RuleExpressionParseException>(() => RuleExpression.Parse("Data.Customer.Ssn == ''", options).Compile<AnnotatedOrder>());

        Assert.True(compiled(new BusinessRuleContext<AnnotatedOrder>(new AnnotatedOrder { Customer = new AnnotatedCustomer { Tier = CustomerTier.Vip } })));
    }

    [Theory]
    [InlineData("Data.Customer.Address.City == null", true)]
    [InlineData("Data.Customer.Address.City != null", false)]
    [InlineData("Data.Customer.Age > 18", false)]
    [InlineData("Data.Customer.Age == null", true)]
    [InlineData("Data.Customer.Tier == 'gold'", false)]
    [InlineData("Data.Customer.Name.Length > 1", false)]
    [InlineData("Data.Total == 0", true)]
    public void Compile_NullSegment_YieldsNull(string text, bool expected)
    {
        Func<BusinessRuleContext<Order>, bool> compiled = RuleExpression.Parse(text).Compile<Order>();

        Assert.Equal(expected, compiled(Typed(TestOrders.Empty())));
    }

    [Theory]
    [InlineData("Data.Customer.Age > 18")]
    [InlineData("Data.Customer.Age == 34")]
    [InlineData("Data.Customer.Address.City == 'Amman'")]
    [InlineData("Data.Customer.Tier == 'gold'")]
    [InlineData("Data.Customer.Tier > 0")]
    [InlineData("Data.Customer.Name.startsWith('A')")]
    [InlineData("Data.Customer.Name.Length == 5")]
    [InlineData("Data.Tags.Length == 2")]
    [InlineData("Data.Tags.Count == 2")]
    [InlineData("Data.Codes.Count == 3")]
    [InlineData("Data.Items.Count * 2 == 4")]
    [InlineData("Data.Total > 10000 && Data.Customer.Tier != 'VIP'")]
    [InlineData("Data.IsGift == false")]
    [InlineData("!Data.IsGift")]
    [InlineData("Data.Customer.Tier in ['vip', 'gold']")]
    [InlineData("'rush' in Data.Tags")]
    [InlineData("Data.Tags.contains('rush')")]
    [InlineData("Data != null")]
    [InlineData("-Data.Total == -12000")]
    [InlineData("Data.Total / 4 == 3000")]
    public void Compile_TypedPaths_EvaluateLikeTheInterpreter(string text)
    {
        RuleExpression expression = RuleExpression.Parse(text);
        BusinessRuleContext<Order> context = Typed(TestOrders.Standard());

        Func<BusinessRuleContext<Order>, bool> compiled = expression.Compile<Order>();

        Assert.True(compiled(context));
        Assert.Equal(expression.Evaluate(context), compiled(context));
    }

    [Fact]
    public void Compile_NullableValueTypeSegment_ReadsThroughValue()
    {
        Func<BusinessRuleContext<Order>, bool> compiled = RuleExpression.Parse("Data.Customer.Age + 1 == 35").Compile<Order>();

        Assert.True(compiled(Typed(TestOrders.Standard())));
    }

    [Fact]
    public void Compile_DataAlone_BindsTheSubject()
    {
        Func<BusinessRuleContext<Order>, bool> compiled = RuleExpression.Parse("Data == null").Compile<Order>();

        Assert.False(compiled(Typed(TestOrders.Standard())));
    }

    [Fact]
    public void Compile_MemberInOtherCase_BindsWhenUnambiguous()
    {
        Func<BusinessRuleContext<Order>, bool> compiled = RuleExpression.Parse("Data.total > 1").Compile<Order>();

        Assert.True(compiled(Typed(TestOrders.Standard())));
    }

    // ---------------------------------------------------------------------
    // Dynamic parts inside a compiled expression
    // ---------------------------------------------------------------------

    [Fact]
    public void Compile_ContextIdentifier_BindsAtEvaluationTime()
    {
        Func<BusinessRuleContext<Order>, bool> compiled = RuleExpression.Parse("Data.Total > amount").Compile<Order>();
        BusinessRuleContext<Order> context = Typed(TestOrders.Standard());
        context.Set(AmountKey, 100m);

        Assert.True(compiled(context));
    }

    [Fact]
    public void Compile_UnboundContextIdentifier_ThrowsBindingExceptionAtEvaluation()
    {
        Func<BusinessRuleContext<Order>, bool> compiled = RuleExpression.Parse("amount > 1").Compile<Order>();

        RuleExpressionBindingException error = Assert.Throws<RuleExpressionBindingException>(() => compiled(Typed(TestOrders.Standard())));

        Assert.Equal(AmountKey, error.Identifier);
        Assert.Equal(new[] { RuleExpression.TypedDataKey }, error.AvailableKeys);
    }

    [Fact]
    public void Compile_DynamicMemberOnContextValue_UsesRuntimeLookup()
    {
        Func<BusinessRuleContext<Order>, bool> compiled = RuleExpression.Parse("customer.Tier == 'vip'").Compile<Order>();
        BusinessRuleContext<Order> context = Typed(TestOrders.Standard());
        context.Set("customer", new Customer { Tier = CustomerTier.Vip });

        Assert.True(compiled(context));
    }

    [Fact]
    public void Compile_DynamicMemberMissing_ThrowsBindingExceptionAtEvaluation()
    {
        Func<BusinessRuleContext<Order>, bool> compiled = RuleExpression.Parse("customer.Nope == 1").Compile<Order>();
        BusinessRuleContext<Order> context = Typed(TestOrders.Standard());
        context.Set("customer", new Customer());

        Assert.Throws<RuleExpressionBindingException>(() => compiled(context));
    }

    [Fact]
    public void Compile_Parameter_ResolvesThroughProvider()
    {
        Func<BusinessRuleContext<Order>, bool> compiled = RuleExpression.Parse("Data.Total > @maxOrderAmount").Compile<Order>(RuleTestHelper.CreateParameterProvider(LimitName, 10_000m));

        Assert.True(compiled(Typed(TestOrders.Standard())));
    }

    [Fact]
    public void Compile_Parameter_ContextProviderOverridesInjected()
    {
        Func<BusinessRuleContext<Order>, bool> compiled = RuleExpression.Parse("Data.Total > @maxOrderAmount").Compile<Order>(RuleTestHelper.CreateParameterProvider(LimitName, 10_000m));
        BusinessRuleContext<Order> context = Typed(TestOrders.Standard());
        context.Set(RuleParameters.ContextKey, RuleTestHelper.CreateParameterProvider(LimitName, 100_000m));

        Assert.False(compiled(context));
    }

    [Fact]
    public void Compile_Parameter_FallsBackToContextBag()
    {
        Func<BusinessRuleContext<Order>, bool> compiled = RuleExpression.Parse("Data.Total > @maxOrderAmount").Compile<Order>();
        BusinessRuleContext<Order> context = Typed(TestOrders.Standard());
        context.Set(LimitName, 100_000m);

        Assert.False(compiled(context));
    }

    [Fact]
    public void Compile_UnboundParameter_ThrowsBindingExceptionAtEvaluation()
    {
        Func<BusinessRuleContext<Order>, bool> compiled = RuleExpression.Parse("Data.Total > @maxOrderAmount").Compile<Order>();

        RuleExpressionBindingException error = Assert.Throws<RuleExpressionBindingException>(() => compiled(Typed(TestOrders.Standard())));

        Assert.Equal("@" + LimitName, error.Identifier);
    }

    // ---------------------------------------------------------------------
    // Coercion parity with the interpreter
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("value == 100")]
    [InlineData("value == 100.0")]
    [InlineData("value > 99.5 && value < 100.5")]
    [InlineData("value / 4 == 25")]
    [InlineData("value % 3 == 1")]
    [InlineData("-value == -100")]
    [InlineData("value in [1, 100]")]
    [InlineData("value ~= 100")]
    [InlineData("value != 'abc'")]
    [InlineData("value == 'abc'")]
    [InlineData("!(value > 1000)")]
    [InlineData("value > 1 || missing > 1")]
    public void Compile_Operators_AgreeWithTheInterpreter(string text)
    {
        RuleExpression expression = RuleExpression.Parse(text);
        BusinessRuleContext<Order> context = Typed(TestOrders.Standard());
        context.Set("value", 100);

        Func<BusinessRuleContext<Order>, bool> compiled = expression.Compile<Order>();

        Assert.Equal(expression.Evaluate(context), compiled(context));
    }

    [Theory]
    [InlineData("tier == 'gold'")]
    [InlineData("tier == 1")]
    [InlineData("tier ~= 'GOLD'")]
    [InlineData("tier > 0")]
    [InlineData("tier in ['vip', 'gold']")]
    [InlineData("name.startsWith('A')")]
    [InlineData("name.startsWith/ci('a')")]
    [InlineData("name ~= 'ALICE'")]
    [InlineData("name < 'B'")]
    [InlineData("name + '!' == 'Alice!'")]
    public void Compile_EnumAndStringCoercions_AgreeWithTheInterpreter(string text)
    {
        RuleExpression expression = RuleExpression.Parse(text);
        BusinessRuleContext<Order> context = Typed(TestOrders.Standard());
        context.Set("tier", CustomerTier.Gold);
        context.Set("name", "Alice");

        Func<BusinessRuleContext<Order>, bool> compiled = expression.Compile<Order>();

        Assert.True(compiled(context));
        Assert.Equal(expression.Evaluate(context), compiled(context));
    }

    [Theory]
    [InlineData("value < 'abc'")]
    [InlineData("value + 'x' == 1")]
    [InlineData("!value")]
    [InlineData("value && true")]
    [InlineData("value in 5")]
    [InlineData("value / 0 == 1")]
    public void Compile_UnsupportedOperands_ThrowEvaluationExceptionLikeTheInterpreter(string text)
    {
        RuleExpression expression = RuleExpression.Parse(text);
        BusinessRuleContext<Order> context = Typed(TestOrders.Standard());
        context.Set("value", 100);

        Func<BusinessRuleContext<Order>, bool> compiled = expression.Compile<Order>();

        Assert.Throws<RuleExpressionEvaluationException>(() => expression.Evaluate(context));
        Assert.Throws<RuleExpressionEvaluationException>(() => compiled(context));
    }

    [Fact]
    public void Compile_ShortCircuits_LikeTheInterpreter()
    {
        Func<BusinessRuleContext<Order>, bool> compiled = RuleExpression.Parse("Data.Total < 1 && missing > 1").Compile<Order>();

        Assert.False(compiled(Typed(TestOrders.Standard())));
    }

    [Fact]
    public void Compile_NonBooleanRoot_ThrowsEvaluationException()
    {
        Func<BusinessRuleContext<Order>, bool> compiled = RuleExpression.Parse("Data.Total").Compile<Order>();

        Assert.Throws<RuleExpressionEvaluationException>(() => compiled(Typed(TestOrders.Standard())));
    }

    [Fact]
    public void Compile_IsReusableAcrossContexts()
    {
        Func<BusinessRuleContext<Order>, bool> compiled = RuleExpression.Parse("Data.Items.Count == 0").Compile<Order>();

        Assert.True(compiled(Typed(TestOrders.Empty())));
        Assert.False(compiled(Typed(TestOrders.Standard())));
        Assert.True(compiled(Typed(TestOrders.Empty())));
    }
}
