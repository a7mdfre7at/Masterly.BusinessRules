namespace Masterly.BusinessRules.UnitTests
{
    public class BusinessRuleTests
    {
        private sealed class AlwaysBrokenRule : BaseBusinessRule
        {
            public override string Code => "TEST001";
            public override string Message => "Always broken.";

            public override bool IsBroken() => true;
        }

        private sealed class AlwaysValidRule : BaseBusinessRule
        {
            public override string Code => "TEST002";
            public override string Message => "Always valid.";
            public override bool IsBroken() => false;
        }

        [Fact]
        public void BrokenRule_ShouldThrow()
        {
            AlwaysBrokenRule rule = new();
            BusinessRuleValidationException ex = Assert.Throws<BusinessRuleValidationException>(() => rule.Check());
            Assert.Single(ex.BrokenRules);
            Assert.Equal("TEST001", ex.BrokenRules.First().Code);
        }

        [Fact]
        public void ValidRule_ShouldNotThrow()
        {
            var rule = new AlwaysValidRule();
            rule.Check();
        }

        [Fact]
        public void CompositeRule_ShouldEvaluateMultiple()
        {
            BaseBusinessRule[] rules = [new AlwaysValidRule(), new AlwaysBrokenRule()];
            CompositeBusinessRule composite = new(rules);

            Assert.True(composite.IsBroken());
            var ex = Assert.Throws<BusinessRuleValidationException>(() => composite.Check());
            Assert.Single(ex.BrokenRules);
        }
    }
}