namespace Masterly.BusinessRules.UnitTests.Composition
{
    public class ContextAwareRule : BaseBusinessRule
    {
        private readonly BusinessRuleContext _context;
        private readonly string _key;
        public ContextAwareRule(BusinessRuleContext context, string key) => (_context, _key) = (context, key);

        public override string Code => "CONTEXT_RULE";
        public override string Message => "Value in context is invalid.";
        public override bool IsBroken() => !_context.TryGet<int>(_key, out int value) || value < 10;
    }

    public class CompositeBusinessRuleContextTests
    {
        [Fact]
        public void Composite_With_ContextAwareRules_Passes()
        {
            BusinessRuleContext context = new BusinessRuleContext();
            context.Set("threshold", 15);

            IBusinessRule[] rules = new IBusinessRule[]
            {
            new ContextAwareRule(context, "threshold")
            };

            CompositeBusinessRule composite = new CompositeBusinessRule(rules);
            composite.Check(); // should not throw
        }

        [Fact]
        public void Composite_With_ContextAwareRules_Fails()
        {
            BusinessRuleContext context = new BusinessRuleContext();
            context.Set("threshold", 5);

            IBusinessRule[] rules = new IBusinessRule[]
            {
            new ContextAwareRule(context, "threshold")
            };

            CompositeBusinessRule composite = new CompositeBusinessRule(rules);

            BusinessRuleValidationException ex = Assert.Throws<BusinessRuleValidationException>(() => composite.Check());
            Assert.Single(ex.BrokenRules);
        }
    }
}
