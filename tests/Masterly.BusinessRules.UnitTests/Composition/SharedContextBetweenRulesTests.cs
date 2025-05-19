namespace Masterly.BusinessRules.UnitTests.Composition
{
    public class GreaterThanTenRule(BusinessRuleContext _context) : BaseBusinessRule
    {
        public override string Code => "GT_10";
        public override string Message => "Value must be greater than 10.";
        public override bool IsBroken() => !_context.TryGet<int>("value", out int val) || val <= 10;
    }

    public class EvenNumberRule(BusinessRuleContext _context) : BaseBusinessRule
    {
        public override string Code => "EVEN";
        public override string Message => "Value must be even.";
        public override bool IsBroken() => !_context.TryGet<int>("value", out int val) || val % 2 != 0;
    }

    public class SharedContextBetweenRulesTests
    {
        [Fact]
        public void BothRules_Pass_WithSharedContext()
        {
            BusinessRuleContext context = new();
            context.Set("value", 12); // >10 and even

            IBusinessRule[] rules =
            [
                new GreaterThanTenRule(context),
                new EvenNumberRule(context)
            ];

            CompositeBusinessRule composite = new(rules);
            composite.Check(); // should not throw
        }

        [Fact]
        public void OneRuleFails_WithSharedContext()
        {
            BusinessRuleContext context = new();
            context.Set("value", 7); // <10 and odd

            IBusinessRule[] rules =
            [
                new GreaterThanTenRule(context),
                new EvenNumberRule(context)
            ];

            CompositeBusinessRule composite = new(rules);

            BusinessRuleValidationException ex = Assert.Throws<BusinessRuleValidationException>(() => composite.Check());
            Assert.Equal(2, ex.BrokenRules.Count);
        }
    }
}