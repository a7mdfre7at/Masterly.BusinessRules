using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules.UnitTests;

public class RuleMetadataTests
{
    [Fact]
    public void BaseBusinessRule_HasDefaultMetadata()
    {
        RuleA rule = new RuleA(false);

        Assert.Equal("RuleA", rule.Name); // Defaults to type name
        Assert.Equal(string.Empty, rule.Description);
        Assert.Equal(string.Empty, rule.Category);
        Assert.Empty(rule.Tags);
    }

    [Fact]
    public void BaseBusinessRule_CanOverrideMetadata()
    {
        RuleWithMetadata rule = new RuleWithMetadata();

        Assert.Equal("Custom Rule Name", rule.Name);
        Assert.Equal("A custom rule description", rule.Description);
        Assert.Equal("Testing", rule.Category);
        Assert.Contains("test", rule.Tags);
        Assert.Contains("custom", rule.Tags);
    }

    [Fact]
    public void BaseAsyncBusinessRule_HasDefaultMetadata()
    {
        AsyncRuleA rule = new AsyncRuleA(false);

        Assert.Equal("AsyncRuleA", rule.Name);
        Assert.Equal(string.Empty, rule.Description);
        Assert.Equal(string.Empty, rule.Category);
        Assert.Empty(rule.Tags);
    }

    [Fact]
    public void BaseAsyncBusinessRule_CanOverrideMetadata()
    {
        AsyncRuleWithMetadata rule = new AsyncRuleWithMetadata();

        Assert.Equal("Async Custom Rule", rule.Name);
        Assert.Equal("An async custom rule", rule.Description);
        Assert.Equal("Async Testing", rule.Category);
        Assert.Contains("async", rule.Tags);
    }

    [Fact]
    public void CompositeBusinessRule_HasMetadata()
    {
        CompositeBusinessRule composite = new CompositeBusinessRule(new[] { new RuleA(false) });

        Assert.Equal("CompositeBusinessRule", composite.Name);
        Assert.NotEmpty(composite.Description);
        Assert.Empty(composite.Tags);
    }

    [Fact]
    public void CompositeAsyncBusinessRule_HasMetadata()
    {
        CompositeAsyncBusinessRule composite = new CompositeAsyncBusinessRule(new[] { new AsyncRuleA(false) });

        Assert.Equal("CompositeAsyncBusinessRule", composite.Name);
        Assert.NotEmpty(composite.Description);
        Assert.Empty(composite.Tags);
    }

    [Fact]
    public void SimpleBusinessRule_InheritsBaseMetadata()
    {
        SimpleBusinessRule rule = new SimpleBusinessRule(() => false, "Test message", "TEST.CODE");

        Assert.Equal("SimpleBusinessRule", rule.Name);
        Assert.Equal(string.Empty, rule.Description);
    }

    [Fact]
    public void AndOrNot_Name_IsStructuralNodeTypeName()
    {
        IBusinessRule and = new RuleA(false).And(new RuleA(false));
        IBusinessRule or = new RuleA(false).Or(new RuleA(false));
        IBusinessRule not = new RuleA(false).Not();

        Assert.Equal("AndBusinessRule", and.Name);
        Assert.Equal("OrBusinessRule", or.Name);
        Assert.Equal("NotBusinessRule", not.Name);
        Assert.Equal(RuleSeverity.Error, and.Severity);
        Assert.Empty(and.Category);
        Assert.Empty(and.Tags);
    }

    [Fact]
    public void AsyncAndOrNot_Name_IsStructuralNodeTypeName()
    {
        IAsyncBusinessRule and = new AsyncRuleA(false).And(new AsyncRuleA(false));
        IAsyncBusinessRule or = new AsyncRuleA(false).Or(new AsyncRuleA(false));
        IAsyncBusinessRule not = new AsyncRuleA(false).Not();

        Assert.Equal("AndAsyncBusinessRule", and.Name);
        Assert.Equal("OrAsyncBusinessRule", or.Name);
        Assert.Equal("NotAsyncBusinessRule", not.Name);
        Assert.Equal(RuleSeverity.Error, and.Severity);
        Assert.Empty(and.Category);
        Assert.Empty(and.Tags);
    }

    [Fact]
    public void GroupBusinessRule_Name_IsTypeName()
    {
        GroupBusinessRule group = RuleGroup.All("GROUP", "Group", new RuleA(false));
        GroupAsyncBusinessRule asyncGroup = AsyncRuleGroup.All("GROUP", "Group", new AsyncRuleA(false));

        Assert.Equal("GroupBusinessRule", group.Name);
        Assert.Equal("GroupAsyncBusinessRule", asyncGroup.Name);
        Assert.NotEmpty(group.Description);
        Assert.NotEmpty(asyncGroup.Description);
    }

    [Fact]
    public void IBusinessRule_MetadataInInterface()
    {
        IBusinessRule rule = new RuleWithMetadata();

        Assert.Equal("Custom Rule Name", rule.Name);
        Assert.Equal("A custom rule description", rule.Description);
        Assert.Equal("Testing", rule.Category);
        Assert.Equal(2, rule.Tags.Count);
    }

    [Fact]
    public void IAsyncBusinessRule_MetadataInInterface()
    {
        IAsyncBusinessRule rule = new AsyncRuleWithMetadata();

        Assert.Equal("Async Custom Rule", rule.Name);
        Assert.Equal("An async custom rule", rule.Description);
        Assert.Equal("Async Testing", rule.Category);
    }

    private class RuleWithMetadata : BaseBusinessRule
    {
        public override string Code => "METADATA.RULE";
        public override string Message => "Rule with metadata";
        public override string Name => "Custom Rule Name";
        public override string Description => "A custom rule description";
        public override string Category => "Testing";
        public override IReadOnlyList<string> Tags => new[] { "test", "custom" };
        public override bool IsBroken() => false;
    }

    private class AsyncRuleWithMetadata : BaseAsyncBusinessRule
    {
        public override string Code => "ASYNC.METADATA";
        public override string Message => "Async rule with metadata";
        public override string Name => "Async Custom Rule";
        public override string Description => "An async custom rule";
        public override string Category => "Async Testing";
        public override IReadOnlyList<string> Tags => new[] { "async", "custom" };

        public override Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}
