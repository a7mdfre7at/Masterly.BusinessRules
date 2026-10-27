using Masterly.BusinessRules.Diagnostics.UnitTests.Support;

namespace Masterly.BusinessRules.Diagnostics.UnitTests.Tracing;

public class TracingRuleObserverOptionsTests
{
    [Fact]
    public void Defaults_AreConservative()
    {
        TracingRuleObserverOptions options = new TracingRuleObserverOptions();

        Assert.False(options.MessageAsEvent);
        Assert.True(options.RecordMetrics);
        Assert.Same(BusinessRulesMetrics.Default, options.Metrics);
        Assert.Equal(TracingRuleObserverOptions.LeafCodeTagSelector, options.CodeTagSelector);
    }

    [Fact]
    public void CodeTagSelector_SetToNull_Throws()
    {
        TracingRuleObserverOptions options = new TracingRuleObserverOptions();

        Assert.Throws<ArgumentNullException>(() => options.CodeTagSelector = null!);
    }

    [Fact]
    public void Metrics_SetToNull_Throws()
    {
        TracingRuleObserverOptions options = new TracingRuleObserverOptions();

        Assert.Throws<ArgumentNullException>(() => options.Metrics = null!);
    }

    [Fact]
    public void LeafCodeTagSelector_NullRule_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TracingRuleObserverOptions.LeafCodeTagSelector(null!));
    }

    [Fact]
    public void LeafCodeTagSelector_LeafRule_ReturnsCode()
    {
        IBusinessRule leaf = TestRules.Sync("ORDER.NO_ITEMS", true);

        Assert.Equal("ORDER.NO_ITEMS", TracingRuleObserverOptions.LeafCodeTagSelector(leaf));
    }

    [Fact]
    public void LeafCodeTagSelector_ComposedRule_ReturnsNull()
    {
        IBusinessRule composed = TestRules.Sync("A", true).And(TestRules.Sync("B", true));
        IAsyncBusinessRule composedAsync = TestRules.Async("A", true).Or(TestRules.Async("B", true));

        Assert.Null(TracingRuleObserverOptions.LeafCodeTagSelector(composed));
        Assert.Null(TracingRuleObserverOptions.LeafCodeTagSelector(composedAsync));
    }

    [Fact]
    public void LeafCodeTagSelector_DecoratedComposedRule_UnwrapsAndReturnsNull()
    {
        IBusinessRule decorated = TestRules.Sync("A", true).And(TestRules.Sync("B", true)).Cached(TimeSpan.FromMinutes(1)).WithSeverity(RuleSeverity.Warning);
        IAsyncBusinessRule decoratedAsync = TestRules.Async("A", true).And(TestRules.Async("B", true)).WithSeverity(RuleSeverity.Warning);

        Assert.Null(TracingRuleObserverOptions.LeafCodeTagSelector(decorated));
        Assert.Null(TracingRuleObserverOptions.LeafCodeTagSelector(decoratedAsync));
    }

    [Fact]
    public void LeafCodeTagSelector_DecoratedLeafRule_ReturnsOuterCode()
    {
        IBusinessRule decorated = TestRules.Sync("A", true).Cached(TimeSpan.FromMinutes(1));

        Assert.Equal("A", TracingRuleObserverOptions.LeafCodeTagSelector(decorated));
    }

    [Fact]
    public void LeafCodeTagSelector_AdaptedComposedRule_UnwrapsAdapters()
    {
        IAsyncBusinessRule syncToAsync = TestRules.Sync("A", true).And(TestRules.Sync("B", true)).ToAsync();
        IBusinessRule asyncToSync = new AsyncToSyncRuleAdapter(TestRules.Async("A", true).And(TestRules.Async("B", true)));

        Assert.Null(TracingRuleObserverOptions.LeafCodeTagSelector(syncToAsync));
        Assert.Null(TracingRuleObserverOptions.LeafCodeTagSelector(asyncToSync));
    }

    [Fact]
    public void LeafCodeTagSelector_AdaptedLeafRule_ReturnsCode()
    {
        IAsyncBusinessRule syncToAsync = TestRules.Sync("A", true).ToAsync();

        Assert.Equal("A", TracingRuleObserverOptions.LeafCodeTagSelector(syncToAsync));
    }

    [Fact]
    public void CodeTagSelector_RoadmapSample_Compiles()
    {
        TracingRuleObserverOptions options = new TracingRuleObserverOptions
        {
            MessageAsEvent = false,
            CodeTagSelector = rule => rule is IComposedBusinessRule ? null : rule.Code
        };

        Assert.Null(options.CodeTagSelector(TestRules.Sync("A", true).And(TestRules.Sync("B", true))));
        Assert.Equal("A", options.CodeTagSelector(TestRules.Sync("A", true)));
    }
}
