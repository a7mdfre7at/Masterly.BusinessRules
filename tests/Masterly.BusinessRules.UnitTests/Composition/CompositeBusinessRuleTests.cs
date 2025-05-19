namespace Masterly.BusinessRules.UnitTests;

public class CompositeBusinessRuleTests
{
    [Fact]
    public void CompositeBusinessRule_Throws_WhenAnyRuleBroken()
    {
        List<IBusinessRule> rules =
        [
            new RuleA(false),
            new RuleA(true)
        ];

        var composite = new CompositeBusinessRule(rules);
        Assert.True(composite.IsBroken());
        Assert.Throws<BusinessRuleValidationException>(() => composite.Check());
    }

    [Fact]
    public void CompositeBusinessRule_DoesNotThrow_WhenNoRulesBroken()
    {
        List<IBusinessRule> rules =
        [
            new RuleA(false),
            new RuleA(false)
        ];

        CompositeBusinessRule composite = new(rules);
        Assert.False(composite.IsBroken());
        composite.Check();
    }

    [Fact]
    public void CompositeBusinessRule_EvaluateAll_ReturnsBrokenRules()
    {
        List<IBusinessRule> rules =
        [
            new RuleA(false),
            new RuleA(true)
        ];

        CompositeBusinessRule composite = new(rules);
        IReadOnlyCollection<BusinessRuleResult> brokenRules = composite.EvaluateAll();

        Assert.Single(brokenRules);
        Assert.Equal("RULE_A", brokenRules.First().Code);
    }

    [Fact]
    public void CompositeBusinessRule_Context_Throws_WhenAnyRuleBroken()
    {
        BusinessRuleContext context = new();
        context.Set("key", "value");

        List<IBusinessRule> rules =
        [
            new RuleA(false),
            new RuleA(true)
        ];

        CompositeBusinessRule composite = new(rules);

        Assert.True(composite.IsBroken());
        Assert.Throws<BusinessRuleValidationException>(() => composite.Check());
    }
}