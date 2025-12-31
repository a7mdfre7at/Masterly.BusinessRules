using System.Threading.Tasks;

namespace Masterly.BusinessRules.UnitTests;

public class RuleTestHelperTests
{
    [Fact]
    public void AssertBroken_WhenBroken_DoesNotThrow()
    {
        RuleA rule = new RuleA(true);

        // Should not throw
        RuleTestHelper.AssertBroken(rule);
    }

    [Fact]
    public void AssertBroken_WhenNotBroken_ThrowsAssertionException()
    {
        RuleA rule = new RuleA(false);

        Assert.Throws<RuleAssertionException>(() => RuleTestHelper.AssertBroken(rule));
    }

    [Fact]
    public void AssertBroken_WithContext_Works()
    {
        RuleWithContextSupport rule = new RuleWithContextSupport();
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set("shouldBeBroken", true);

        // Should not throw
        RuleTestHelper.AssertBroken(rule, context);
    }

    [Fact]
    public void AssertNotBroken_WhenNotBroken_DoesNotThrow()
    {
        RuleA rule = new RuleA(false);

        // Should not throw
        RuleTestHelper.AssertNotBroken(rule);
    }

    [Fact]
    public void AssertNotBroken_WhenBroken_ThrowsAssertionException()
    {
        RuleA rule = new RuleA(true);

        Assert.Throws<RuleAssertionException>(() => RuleTestHelper.AssertNotBroken(rule));
    }

    [Fact]
    public void AssertNotBroken_WithContext_Works()
    {
        RuleWithContextSupport rule = new RuleWithContextSupport();
        BusinessRuleContext context = new BusinessRuleContext();
        // Not setting "shouldBeBroken", so rule should pass

        // Should not throw
        RuleTestHelper.AssertNotBroken(rule, context);
    }

    [Fact]
    public async Task AssertBrokenAsync_WhenBroken_DoesNotThrow()
    {
        AsyncRuleA rule = new AsyncRuleA(true);

        // Should not throw
        await RuleTestHelper.AssertBrokenAsync(rule);
    }

    [Fact]
    public async Task AssertBrokenAsync_WhenNotBroken_ThrowsAssertionException()
    {
        AsyncRuleA rule = new AsyncRuleA(false);

        await Assert.ThrowsAsync<RuleAssertionException>(
            () => RuleTestHelper.AssertBrokenAsync(rule));
    }

    [Fact]
    public async Task AssertNotBrokenAsync_WhenNotBroken_DoesNotThrow()
    {
        AsyncRuleA rule = new AsyncRuleA(false);

        // Should not throw
        await RuleTestHelper.AssertNotBrokenAsync(rule);
    }

    [Fact]
    public async Task AssertNotBrokenAsync_WhenBroken_ThrowsAssertionException()
    {
        AsyncRuleA rule = new AsyncRuleA(true);

        await Assert.ThrowsAsync<RuleAssertionException>(
            () => RuleTestHelper.AssertNotBrokenAsync(rule));
    }

    [Fact]
    public void CreateContext_WithKeyValuePairs_CreatesContext()
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext(
            ("name", "John"),
            ("age", 25),
            ("active", true));

        Assert.Equal("John", context.Get<string>("name"));
        Assert.Equal(25, context.Get<int>("age"));
        Assert.True(context.Get<bool>("active"));
    }

    [Fact]
    public void CreateContext_Generic_CreatesTypedContext()
    {
        TestData data = new TestData { Value = 42 };
        BusinessRuleContext<TestData> context = RuleTestHelper.CreateContext(data);

        Assert.Equal(42, context.Data.Value);
    }

    [Fact]
    public void CreateBrokenRule_CreatesAlwaysBrokenRule()
    {
        IBusinessRule rule = RuleTestHelper.CreateBrokenRule();

        Assert.True(rule.IsBroken());
    }

    [Fact]
    public void CreateBrokenRule_WithCustomCodeAndMessage_Works()
    {
        IBusinessRule rule = RuleTestHelper.CreateBrokenRule("CUSTOM.CODE", "Custom message");

        Assert.Equal("CUSTOM.CODE", rule.Code);
        Assert.Equal("Custom message", rule.Message);
        Assert.True(rule.IsBroken());
    }

    [Fact]
    public void CreatePassingRule_CreatesAlwaysPassingRule()
    {
        IBusinessRule rule = RuleTestHelper.CreatePassingRule();

        Assert.False(rule.IsBroken());
    }

    [Fact]
    public async Task CreateBrokenAsyncRule_CreatesAlwaysBrokenAsyncRule()
    {
        IAsyncBusinessRule rule = RuleTestHelper.CreateBrokenAsyncRule();

        Assert.True(await rule.IsBrokenAsync(new BusinessRuleContext()));
    }

    [Fact]
    public async Task CreatePassingAsyncRule_CreatesAlwaysPassingAsyncRule()
    {
        IAsyncBusinessRule rule = RuleTestHelper.CreatePassingAsyncRule();

        Assert.False(await rule.IsBrokenAsync(new BusinessRuleContext()));
    }

    private class TestData
    {
        public int Value { get; set; }
    }
}
