namespace Masterly.BusinessRules.UnitTests;

public class BusinessRuleContextTests
{
    [Fact]
    public void Set_And_Get_WorksCorrectly()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set("key", "value");

        Assert.Equal("value", context.Get<string>("key"));
    }

    [Fact]
    public void TryGet_ExistingKey_ReturnsTrue()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set("key", 42);

        Assert.True(context.TryGet<int>("key", out int value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGet_NonExistingKey_ReturnsFalse()
    {
        BusinessRuleContext context = new BusinessRuleContext();

        Assert.False(context.TryGet<int>("nonexistent", out _));
    }

    [Fact]
    public void ContainsKey_ExistingKey_ReturnsTrue()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set("key", "value");

        Assert.True(context.ContainsKey("key"));
    }

    [Fact]
    public void ContainsKey_NonExistingKey_ReturnsFalse()
    {
        BusinessRuleContext context = new BusinessRuleContext();

        Assert.False(context.ContainsKey("nonexistent"));
    }

    [Fact]
    public void Remove_ExistingKey_RemovesItem()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set("key", "value");

        Assert.True(context.Remove("key"));
        Assert.False(context.ContainsKey("key"));
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set("key1", "value1");
        context.Set("key2", "value2");

        context.Clear();

        Assert.Equal(0, context.Count);
    }

    [Fact]
    public void Count_ReturnsCorrectCount()
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set("key1", "value1");
        context.Set("key2", "value2");

        Assert.Equal(2, context.Count);
    }

    [Fact]
    public void GenericContext_ProvidesTypedData()
    {
        TestContextData data = new TestContextData { Name = "Test", Value = 42 };
        BusinessRuleContext<TestContextData> context = new BusinessRuleContext<TestContextData>(data);

        Assert.Equal("Test", context.Data.Name);
        Assert.Equal(42, context.Data.Value);
    }

    [Fact]
    public void GenericContext_InheritsFromBusinessRuleContext()
    {
        TestContextData data = new TestContextData { Name = "Test", Value = 42 };
        BusinessRuleContext<TestContextData> context = new BusinessRuleContext<TestContextData>(data);

        // Can still use Set/Get
        context.Set("extra", "value");
        Assert.Equal("value", context.Get<string>("extra"));
    }

    private class TestContextData
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}

public class RuleWithContextSupport : BaseBusinessRule
{
    public override string Code => "CONTEXT_RULE";
    public override string Message => "Rule that uses context";

    public override bool IsBroken() => false;

    public override bool IsBroken(BusinessRuleContext context)
    {
        return context.TryGet<bool>("shouldBeBroken", out bool broken) && broken;
    }
}

public class ContextSupportTests
{
    [Fact]
    public void IsBroken_WithContext_UsesContextData()
    {
        RuleWithContextSupport rule = new RuleWithContextSupport();
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set("shouldBeBroken", true);

        Assert.True(rule.IsBroken(context));
    }

    [Fact]
    public void IsBroken_WithoutContextData_ReturnsFalse()
    {
        RuleWithContextSupport rule = new RuleWithContextSupport();
        BusinessRuleContext context = new BusinessRuleContext();

        Assert.False(rule.IsBroken(context));
    }

    [Fact]
    public void Check_WithContext_ThrowsWhenBroken()
    {
        RuleWithContextSupport rule = new RuleWithContextSupport();
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set("shouldBeBroken", true);

        Assert.Throws<BusinessRuleValidationException>(() => rule.Check(context));
    }
}
