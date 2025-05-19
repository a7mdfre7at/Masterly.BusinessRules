namespace Masterly.BusinessRules.UnitTests;

public class DummyRule(bool _isBroken) : BaseBusinessRule
{
    public override string Code => "DUMMY";
    public override string Message => "Dummy rule failed.";
    public override bool IsBroken() => _isBroken;
}

public class BusinessRuleCheckerTests
{
    [Fact]
    public void CheckAll_NoBrokenRules_DoesNotThrow()
    {
        BusinessRuleChecker.CheckAll(new DummyRule(false), new DummyRule(false));
    }

    [Fact]
    public void CheckAll_BrokenRules_ThrowsException()
    {
        Assert.Throws<BusinessRuleValidationException>(() =>
            BusinessRuleChecker.CheckAll(new DummyRule(true)));
    }
}
