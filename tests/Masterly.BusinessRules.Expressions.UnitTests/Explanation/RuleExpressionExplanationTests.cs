using System.Globalization;

namespace Masterly.BusinessRules.Expressions.UnitTests;

public class RuleExpressionExplanationTests
{
    private const string AmountKey = "amount";
    private const string CustomerKey = "customer";
    private const string LimitName = "maxOrderAmount";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private static BusinessRuleContext OrderContext(decimal amount = 12_000m, CustomerTier tier = CustomerTier.Gold)
        => RuleTestHelper.CreateContext((AmountKey, amount), (CustomerKey, new Customer { Name = "Alice", Tier = tier, Ssn = "123-45-6789" }), (LimitName, 10_000m));

    // ---------------------------------------------------------------------
    // Rendered
    // ---------------------------------------------------------------------

    [Fact]
    public void Explain_SingleComparison_RendersBoundValuesAndVerdict()
    {
        RuleExpressionExplanation why = RuleExpression.Parse("amount > @maxOrderAmount").Explain(OrderContext());

        Assert.Equal("amount(12000) > @maxOrderAmount(10000) => true", why.Rendered);
        Assert.True(why.Result);
    }

    [Fact]
    public void Explain_Conjunction_AnnotatesEachComparison()
    {
        RuleExpressionExplanation why = RuleExpression.Parse("amount > @maxOrderAmount && customer.Tier != 'VIP'").Explain(OrderContext());

        Assert.Equal("(amount(12000) > @maxOrderAmount(10000) [true]) && (customer.Tier(Gold) != 'VIP' [true]) => true", why.Rendered);
    }

    [Fact]
    public void Explain_ShortCircuit_MarksTheSkippedBranchNotEvaluated()
    {
        RuleExpressionExplanation why = RuleExpression.Parse("amount > @maxOrderAmount && customer.Tier != 'VIP'").Explain(OrderContext(amount: 5m));

        Assert.Equal("(amount(5) > @maxOrderAmount(10000) [false]) && (customer.Tier != 'VIP' [not evaluated]) => false", why.Rendered);
        Assert.False(why.Result);
    }

    [Fact]
    public void Explain_StringValues_AreQuoted()
    {
        RuleExpressionExplanation why = RuleExpression.Parse("customer.Name == 'Bob'").Explain(OrderContext());

        Assert.Equal("customer.Name('Alice') == 'Bob' => false", why.Rendered);
    }

    [Fact]
    public void Explain_NullValue_RendersNull()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((CustomerKey, new Customer { Address = null }));

        RuleExpressionExplanation why = RuleExpression.Parse("customer.Address.City == null").Explain(context);

        Assert.Equal("customer.Address.City(null) == null => true", why.Rendered);
    }

    [Fact]
    public void Explain_MethodCall_RendersTargetValueAndResult()
    {
        RuleExpressionExplanation why = RuleExpression.Parse("customer.Name.startsWith/ci('a') || amount > 1").Explain(OrderContext());

        Assert.Equal("customer.Name('Alice').startsWith/ci('a') [true] || (amount > 1 [not evaluated]) => true", why.Rendered);
    }

    [Fact]
    public void Explain_InList_RendersItemsAndResult()
    {
        RuleExpressionExplanation why = RuleExpression.Parse("customer.Tier in ['vip', 'gold']").Explain(OrderContext());

        Assert.Equal("customer.Tier(Gold) in ['vip', 'gold'] => true", why.Rendered);
    }

    [Fact]
    public void Explain_Arithmetic_AnnotatesIntermediateResult()
    {
        RuleExpressionExplanation why = RuleExpression.Parse("amount * 2 > 20000").Explain(OrderContext());

        Assert.Equal("(amount(12000) * 2 [24000]) > 20000 => true", why.Rendered);
    }

    [Fact]
    public void Explain_Not_AnnotatesOperand()
    {
        RuleExpressionExplanation why = RuleExpression.Parse("!(amount > 1)").Explain(OrderContext());

        Assert.Equal("!(amount(12000) > 1 [true]) => false", why.Rendered);
    }

    [Fact]
    public void Explain_CollectionValue_RendersItems()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext(("tags", new[] { "rush", "fragile" }));

        RuleExpressionExplanation why = RuleExpression.Parse("'rush' in tags").Explain(context);

        Assert.Equal("'rush' in tags(['rush', 'fragile']) => true", why.Rendered);
    }

    [Fact]
    public void Explain_BooleanValue_RendersLowercase()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext(("flag", true));

        RuleExpressionExplanation why = RuleExpression.Parse("flag == true").Explain(context);

        Assert.Equal("flag(true) == true => true", why.Rendered);
    }

    [Fact]
    public void Explain_DateTimeValue_FormatsWithOptionsCulture()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext(("placedAt", new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)), ("cutoff", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        RuleExpressionExplanation why = RuleExpression.Parse("placedAt > cutoff").Explain(context);

        Assert.Equal("placedAt(01/15/2026 00:00:00) > cutoff(01/01/2026 00:00:00) => true", why.Rendered);
    }

    [Fact]
    public void Explain_Culture_FormatsNumbersWithIt()
    {
        RuleExpressionOptions options = RuleExpressionOptions.Default.WithCulture(CultureInfo.GetCultureInfo("de-DE"));
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 12000.5m));

        RuleExpressionExplanation why = RuleExpression.Parse("amount > 1.5", options).Explain(context);

        Assert.Equal("amount(12000,5) > 1.5 => true", why.Rendered);
    }

    [Fact]
    public void Explain_ToString_ReturnsRendered()
    {
        RuleExpressionExplanation why = RuleExpression.Parse("amount > 1").Explain(OrderContext());

        Assert.Equal(why.Rendered, why.ToString());
    }

    [Fact]
    public void Explain_ExposesTheExpression()
    {
        RuleExpression expression = RuleExpression.Parse("amount > 1");

        RuleExpressionExplanation why = expression.Explain(OrderContext());

        Assert.Same(expression, why.Expression);
    }

    // ---------------------------------------------------------------------
    // Steps
    // ---------------------------------------------------------------------

    [Fact]
    public void Explain_Steps_RecordBindingsThenOperationsInEvaluationOrder()
    {
        RuleExpressionExplanation why = RuleExpression.Parse("amount > @maxOrderAmount").Explain(OrderContext());

        Assert.Collection(
            why.Steps,
            step =>
            {
                Assert.Equal(RuleExpressionStepKind.Binding, step.Kind);
                Assert.Equal("amount", step.NodeText);
                Assert.Equal(12_000m, step.BoundValue);
                Assert.Equal(12_000m, step.Result);
            },
            step =>
            {
                Assert.Equal(RuleExpressionStepKind.Binding, step.Kind);
                Assert.Equal("@maxOrderAmount", step.NodeText);
                Assert.Equal(10_000m, step.BoundValue);
            },
            step =>
            {
                Assert.Equal(RuleExpressionStepKind.Operation, step.Kind);
                Assert.Equal("amount > @maxOrderAmount", step.NodeText);
                Assert.Null(step.BoundValue);
                Assert.Equal(true, step.Result);
            });
    }

    [Fact]
    public void Explain_Steps_SkippedBranchHasNoSteps()
    {
        RuleExpressionExplanation why = RuleExpression.Parse("amount > 100000 && customer.Tier != 'VIP'").Explain(OrderContext());

        Assert.DoesNotContain(why.Steps, step => step.NodeText == "customer.Tier");
        Assert.DoesNotContain(why.Steps, step => step.NodeText == "customer.Tier != 'VIP'");
        Assert.Equal(3, why.Steps.Count);
    }

    [Fact]
    public void Explain_Steps_MemberPathIsOneBinding()
    {
        RuleExpressionExplanation why = RuleExpression.Parse("customer.Tier == 'Gold'").Explain(OrderContext());

        RuleExpressionStep binding = Assert.Single(why.Steps, step => step.Kind == RuleExpressionStepKind.Binding);
        Assert.Equal("customer.Tier", binding.NodeText);
        Assert.Equal(CustomerTier.Gold, binding.BoundValue);
    }

    [Fact]
    public void Explain_Steps_LiteralsAreNotSteps()
    {
        RuleExpressionExplanation why = RuleExpression.Parse("1 + 2 == 3").Explain(new BusinessRuleContext());

        Assert.Equal(new[] { "1 + 2", "1 + 2 == 3" }, why.Steps.Select(step => step.NodeText));
    }

    [Fact]
    public void Explain_Steps_ArithmeticResultIsRecorded()
    {
        RuleExpressionExplanation why = RuleExpression.Parse("amount * 2 > 1").Explain(OrderContext());

        RuleExpressionStep product = why.Steps.Single(step => step.NodeText == "amount * 2");
        Assert.Equal(24_000m, product.Result);
    }

    [Fact]
    public void Step_ToString_FormatsBindingAndOperation()
    {
        RuleExpressionStep binding = new RuleExpressionStep(RuleExpressionStepKind.Binding, "amount", 5, 5);
        RuleExpressionStep operation = new RuleExpressionStep(RuleExpressionStepKind.Operation, "amount > 1", null, true);
        RuleExpressionStep nullBinding = new RuleExpressionStep(RuleExpressionStepKind.Binding, "x", null, null);

        Assert.Equal("amount = 5", binding.ToString());
        Assert.Equal("amount > 1 => True", operation.ToString());
        Assert.Equal("x = null", nullBinding.ToString());
    }

    // ---------------------------------------------------------------------
    // Verdict consistency
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("amount > @maxOrderAmount")]
    [InlineData("amount > @maxOrderAmount && customer.Tier != 'VIP'")]
    [InlineData("customer.Name.startsWith('Z') || amount in [1, 12000]")]
    [InlineData("!(amount > 1) || customer.Tier == 'gold'")]
    public void Explain_Result_MatchesEvaluate(string text)
    {
        RuleExpression expression = RuleExpression.Parse(text);
        BusinessRuleContext context = OrderContext();

        Assert.Equal(expression.Evaluate(context), expression.Explain(context).Result);
    }

    // ---------------------------------------------------------------------
    // Redaction
    // ---------------------------------------------------------------------

    [Fact]
    public void Explain_Redact_MasksTheNamedPathEverywhere()
    {
        RuleExpressionExplanation why = RuleExpression.Parse("customer.Ssn != '' && amount > 1").Explain(
            OrderContext(),
            redact: (name, value) => name == "customer.Ssn" ? "***" : value?.ToString());

        Assert.Equal("(customer.Ssn(***) != '' [true]) && (amount(12000) > 1 [true]) => true", why.Rendered);
        RuleExpressionStep ssn = why.Steps.Single(step => step.NodeText == "customer.Ssn");
        Assert.Equal("***", ssn.BoundValue);
        Assert.Equal("***", ssn.Result);
        Assert.DoesNotContain("123-45-6789", why.Rendered);
        Assert.DoesNotContain(why.Steps, step => Equals(step.BoundValue, "123-45-6789") || Equals(step.Result, "123-45-6789"));
    }

    [Fact]
    public void Explain_Redact_ReceivesParameterNameWithAt()
    {
        List<string> names = new List<string>();

        RuleExpression.Parse("amount > @maxOrderAmount").Explain(OrderContext(), (name, value) =>
        {
            names.Add(name);
            return value?.ToString();
        });

        Assert.Equal(new[] { "amount", "@maxOrderAmount" }, names);
    }

    [Fact]
    public void Explain_Redact_ReturningNull_RendersNull()
    {
        RuleExpressionExplanation why = RuleExpression.Parse("amount > 1").Explain(OrderContext(), (_, _) => null);

        Assert.Equal("amount(null) > 1 => true", why.Rendered);
        Assert.Null(why.Steps[0].BoundValue);
    }

    [Fact]
    public void Explain_Redact_DoesNotAffectOperationResults()
    {
        RuleExpressionExplanation why = RuleExpression.Parse("amount > 1").Explain(OrderContext(), (_, _) => "hidden");

        Assert.Equal(true, why.Steps[^1].Result);
        Assert.True(why.Result);
    }

    [Fact]
    public void Explain_Redact_DoesNotChangeTheVerdict()
    {
        RuleExpression expression = RuleExpression.Parse("customer.Ssn == '123-45-6789'");

        RuleExpressionExplanation why = expression.Explain(OrderContext(), (_, _) => "***");

        Assert.True(why.Result);
    }

    // ---------------------------------------------------------------------
    // Parameters and async
    // ---------------------------------------------------------------------

    [Fact]
    public void Explain_WithProvider_ShowsProviderValue()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m));

        RuleExpressionExplanation why = RuleExpression.Parse("amount > @maxOrderAmount").Explain(context, null, RuleTestHelper.CreateParameterProvider(LimitName, 1_000m));

        Assert.Equal("amount(500) > @maxOrderAmount(1000) => false", why.Rendered);
    }

    [Fact]
    public async Task ExplainAsync_WithAsyncProvider_ShowsProviderValue()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((AmountKey, 500m));

        RuleExpressionExplanation why = await RuleExpression.Parse("amount > @maxOrderAmount").ExplainAsync(context, null, RuleTestHelper.CreateParameterProvider(LimitName, 1_000m), null, TestToken);

        Assert.Equal("amount(500) > @maxOrderAmount(1000) => false", why.Rendered);
    }

    [Fact]
    public async Task ExplainAsync_MatchesExplain()
    {
        RuleExpression expression = RuleExpression.Parse("amount > @maxOrderAmount && customer.Tier != 'VIP'");
        BusinessRuleContext context = OrderContext();

        RuleExpressionExplanation sync = expression.Explain(context);
        RuleExpressionExplanation async = await expression.ExplainAsync(context, cancellationToken: TestToken);

        Assert.Equal(sync.Rendered, async.Rendered);
        Assert.Equal(sync.Steps.Select(step => step.ToString()), async.Steps.Select(step => step.ToString()));
    }

    [Fact]
    public async Task ExplainAsync_Redact_Applies()
    {
        RuleExpressionExplanation why = await RuleExpression.Parse("customer.Ssn != ''").ExplainAsync(OrderContext(), (_, _) => "***", cancellationToken: TestToken);

        Assert.Equal("customer.Ssn(***) != '' => true", why.Rendered);
    }

    [Fact]
    public async Task ExplainAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RuleExpression.Parse("amount > 1").ExplainAsync(OrderContext(), cancellationToken: source.Token));
    }

    [Fact]
    public void Explain_UnboundIdentifier_ThrowsBindingException()
    {
        Assert.Throws<RuleExpressionBindingException>(() => RuleExpression.Parse("missing > 1").Explain(OrderContext()));
    }
}
