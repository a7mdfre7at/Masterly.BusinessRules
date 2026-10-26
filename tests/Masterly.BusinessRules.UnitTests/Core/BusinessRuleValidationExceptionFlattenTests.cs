namespace Masterly.BusinessRules.UnitTests;

public class BusinessRuleValidationExceptionFlattenTests
{
    [Fact]
    public void Flatten_OnlyLeafResults_ReturnsThemUnchanged()
    {
        BusinessRuleResult first = new BusinessRuleResult("A", "A", RuleSeverity.Error);
        BusinessRuleResult second = new BusinessRuleResult("B", "B", RuleSeverity.Error);
        BusinessRuleValidationException exception = new BusinessRuleValidationException(new[] { first, second });

        List<BusinessRuleResult> leaves = exception.Flatten().ToList();

        Assert.Equal(2, leaves.Count);
        Assert.Same(first, leaves[0]);
        Assert.Same(second, leaves[1]);
    }

    [Fact]
    public void Flatten_ComposedResults_ReplacesThemByTheirLeaves()
    {
        BusinessRuleResult leafA = new BusinessRuleResult("A", "A", RuleSeverity.Error);
        BusinessRuleResult leafB = new BusinessRuleResult("B", "B", RuleSeverity.Error);
        BusinessRuleResult group = new BusinessRuleResult("GROUP", "Group", RuleSeverity.Error, new[] { leafA, leafB });
        BusinessRuleResult leafC = new BusinessRuleResult("C", "C", RuleSeverity.Error);
        BusinessRuleValidationException exception = new BusinessRuleValidationException(new[] { group, leafC });

        List<BusinessRuleResult> leaves = exception.Flatten().ToList();

        Assert.Equal(new[] { "A", "B", "C" }, leaves.Select(leaf => leaf.Code));
    }

    [Fact]
    public void Flatten_NoBrokenRules_ReturnsEmpty()
    {
        BusinessRuleValidationException exception = new BusinessRuleValidationException(Array.Empty<BusinessRuleResult>());

        Assert.Empty(exception.Flatten());
    }

    [Fact]
    public void Flatten_FromGroupCheck_ReturnsBrokenMembers()
    {
        IBusinessRule group = RuleGroup.Any(
            "PAYMENT.NONE_VALID",
            "At least one payment method must be valid",
            RuleTestHelper.CreateBrokenRule("CARD"),
            RuleTestHelper.CreateBrokenRule("BANK"));

        BusinessRuleValidationException exception = Assert.Throws<BusinessRuleValidationException>(() => group.Check());

        Assert.Single(exception.BrokenRules);
        Assert.Equal(new[] { "CARD", "BANK" }, exception.Flatten().Select(leaf => leaf.Code));
    }
}
