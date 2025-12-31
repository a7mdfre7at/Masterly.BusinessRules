using System.Threading.Tasks;

namespace Masterly.BusinessRules.UnitTests;

public class ConditionalRuleTests
{
    [Fact]
    public void ConditionalBusinessRule_ConditionTrue_EvaluatesInnerRule()
    {
        RuleA innerRule = new RuleA(true);
        ConditionalBusinessRule conditionalRule = new ConditionalBusinessRule(innerRule, () => true);

        Assert.True(conditionalRule.IsBroken());
    }

    [Fact]
    public void ConditionalBusinessRule_ConditionFalse_SkipsInnerRule()
    {
        RuleA innerRule = new RuleA(true);
        ConditionalBusinessRule conditionalRule = new ConditionalBusinessRule(innerRule, () => false);

        Assert.False(conditionalRule.IsBroken());
    }

    [Fact]
    public void ConditionalBusinessRule_PreservesMetadata()
    {
        IBusinessRule innerRule = BusinessRuleBuilder.Create("INNER")
            .WithMessage("Inner message")
            .WithSeverity(RuleSeverity.Warning)
            .WithCategory("Test")
            .When(() => true)
            .Build();

        ConditionalBusinessRule conditionalRule = new ConditionalBusinessRule(innerRule, () => true);

        Assert.Equal("INNER", conditionalRule.Code);
        Assert.Equal("Inner message", conditionalRule.Message);
        Assert.Equal(RuleSeverity.Warning, conditionalRule.Severity);
        Assert.Equal("Test", conditionalRule.Category);
    }

    [Fact]
    public void ConditionalBusinessRule_WithContext_UsesContext()
    {
        RuleWithContextSupport innerRule = new RuleWithContextSupport();
        ConditionalBusinessRule conditionalRule = new ConditionalBusinessRule(innerRule, () => true);

        BusinessRuleContext context = new BusinessRuleContext();
        context.Set("shouldBeBroken", true);

        Assert.True(conditionalRule.IsBroken(context));
    }

    [Fact]
    public void ConditionalBusinessRule_Check_SkipsWhenConditionFalse()
    {
        RuleA innerRule = new RuleA(true);
        ConditionalBusinessRule conditionalRule = new ConditionalBusinessRule(innerRule, () => false);

        // Should not throw because condition is false
        conditionalRule.Check();
    }

    [Fact]
    public void ConditionalBusinessRule_Check_ThrowsWhenConditionTrueAndBroken()
    {
        RuleA innerRule = new RuleA(true);
        ConditionalBusinessRule conditionalRule = new ConditionalBusinessRule(innerRule, () => true);

        Assert.Throws<BusinessRuleValidationException>(() => conditionalRule.Check());
    }

    [Fact]
    public void When_Extension_CreatesConditionalRule()
    {
        bool conditionCalled = false;
        IBusinessRule rule = new RuleA(true).When(() =>
        {
            conditionCalled = true;
            return false;
        });

        rule.IsBroken();

        Assert.True(conditionCalled);
        Assert.False(rule.IsBroken());
    }
}

public class ConditionalAsyncRuleTests
{
    [Fact]
    public async Task ConditionalAsyncBusinessRule_ConditionTrue_EvaluatesInnerRule()
    {
        AsyncRuleA innerRule = new AsyncRuleA(true);
        ConditionalAsyncBusinessRule conditionalRule = new ConditionalAsyncBusinessRule(innerRule, ctx => true);

        Assert.True(await conditionalRule.IsBrokenAsync(new BusinessRuleContext()));
    }

    [Fact]
    public async Task ConditionalAsyncBusinessRule_ConditionFalse_SkipsInnerRule()
    {
        AsyncRuleA innerRule = new AsyncRuleA(true);
        ConditionalAsyncBusinessRule conditionalRule = new ConditionalAsyncBusinessRule(innerRule, ctx => false);

        Assert.False(await conditionalRule.IsBrokenAsync(new BusinessRuleContext()));
    }

    [Fact]
    public async Task ConditionalAsyncBusinessRule_UsesContextInCondition()
    {
        AsyncRuleA innerRule = new AsyncRuleA(true);
        ConditionalAsyncBusinessRule conditionalRule = new ConditionalAsyncBusinessRule(
            innerRule,
            ctx => ctx.TryGet<bool>("runRule", out bool run) && run);

        BusinessRuleContext context = new BusinessRuleContext();
        context.Set("runRule", false);

        Assert.False(await conditionalRule.IsBrokenAsync(context));

        context.Set("runRule", true);
        Assert.True(await conditionalRule.IsBrokenAsync(context));
    }

    [Fact]
    public async Task ConditionalAsyncBusinessRule_EvaluateAsync_ReturnsNullWhenSkipped()
    {
        AsyncRuleA innerRule = new AsyncRuleA(true);
        ConditionalAsyncBusinessRule conditionalRule = new ConditionalAsyncBusinessRule(innerRule, ctx => false);

        BusinessRuleResult? result = await conditionalRule.EvaluateAsync(new BusinessRuleContext());

        Assert.Null(result);
    }

    [Fact]
    public async Task ConditionalAsyncBusinessRule_CheckAsync_SkipsWhenConditionFalse()
    {
        AsyncRuleA innerRule = new AsyncRuleA(true);
        ConditionalAsyncBusinessRule conditionalRule = new ConditionalAsyncBusinessRule(innerRule, ctx => false);

        // Should not throw
        await conditionalRule.CheckAsync(new BusinessRuleContext());
    }
}
