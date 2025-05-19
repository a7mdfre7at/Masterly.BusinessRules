namespace Masterly.BusinessRules.UnitTests;

public class RuleA(bool _isBroken) : BaseBusinessRule
{
    public override string Code => "RULE_A";
    public override string Message => "Rule A";
    public override bool IsBroken() => _isBroken;
}

public class BusinessRuleExtensionsTests
{
    [Fact]
    public void And_Composition_EvaluatesCorrectly()
    {
        IBusinessRule rule = new RuleA(false).And(new RuleA(false));
        Assert.False(rule.IsBroken());
    }

    [Fact]
    public void Or_Composition_EvaluatesCorrectly()
    {
        IBusinessRule rule = new RuleA(false).Or(new RuleA(true));
        Assert.True(rule.IsBroken());
    }

    [Fact]
    public void Not_Composition_EvaluatesCorrectly()
    {
        IBusinessRule rule = new RuleA(true).Not();
        Assert.False(rule.IsBroken());
    }
}
