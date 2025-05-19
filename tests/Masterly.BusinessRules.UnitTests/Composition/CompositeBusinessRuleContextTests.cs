﻿namespace Masterly.BusinessRules.UnitTests.Composition
{
    public class ContextAwareRule : BaseBusinessRule
    {
        private readonly BusinessRuleContext _context;
        private readonly string _key;
        public ContextAwareRule(BusinessRuleContext context, string key) => (_context, _key) = (context, key);

        public override string Code => "CONTEXT_RULE";
        public override string Message => "Value in context is invalid.";
        public override bool IsBroken() => !_context.TryGet<int>(_key, out var value) || value < 10;
    }

    public class CompositeBusinessRuleContextTests
    {
        [Fact]
        public void Composite_With_ContextAwareRules_Passes()
        {
            var context = new BusinessRuleContext();
            context.Set("threshold", 15);

            var rules = new IBusinessRule[]
            {
            new ContextAwareRule(context, "threshold")
            };

            var composite = new CompositeBusinessRule(rules);
            composite.Check(); // should not throw
        }

        [Fact]
        public void Composite_With_ContextAwareRules_Fails()
        {
            var context = new BusinessRuleContext();
            context.Set("threshold", 5);

            var rules = new IBusinessRule[]
            {
            new ContextAwareRule(context, "threshold")
            };

            var composite = new CompositeBusinessRule(rules);

            var ex = Assert.Throws<BusinessRuleValidationException>(() => composite.Check());
            Assert.Single(ex.BrokenRules);
        }
    }
}
