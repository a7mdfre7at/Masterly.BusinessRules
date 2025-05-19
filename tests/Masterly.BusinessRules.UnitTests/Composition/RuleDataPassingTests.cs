namespace Masterly.BusinessRules.UnitTests.Composition
{
    public class ProducerRule(BusinessRuleContext _context) : BaseBusinessRule
    {
        public override string Code => "PRODUCER";
        public override string Message => "Sets data for other rules.";
        public override bool IsBroken()
        {
            _context.Set("shared", 42); // sets a value into context
            return false;
        }
    }

    public class ConsumerRule(BusinessRuleContext _context) : BaseBusinessRule
    {
        public override string Code => "CONSUMER";
        public override string Message => "Checks value set by another rule.";
        public override bool IsBroken() => !_context.TryGet<int>("shared", out int value) || value != 42;
    }

    public class RuleDataPassingTests
    {
        [Fact]
        public void RuleDataFlow_FromProducerToConsumer_Works()
        {
            BusinessRuleContext context = new();

            ProducerRule producer = new(context);
            ConsumerRule consumer = new(context);

            // Evaluate manually to ensure producer runs first
            producer.Check();
            consumer.Check();
        }

        [Fact]
        public void RuleDataFlow_Fails_IfProducerNotExecuted()
        {
            BusinessRuleContext context = new();

            ConsumerRule consumer = new(context);

            Assert.Throws<BusinessRuleValidationException>(() => consumer.Check());
        }
    }
}