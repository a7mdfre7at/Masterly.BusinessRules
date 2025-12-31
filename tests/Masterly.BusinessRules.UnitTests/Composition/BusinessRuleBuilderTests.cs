using System.Threading.Tasks;

namespace Masterly.BusinessRules.UnitTests;

public class BusinessRuleBuilderTests
{
    [Fact]
    public void Build_WithAllProperties_CreatesRule()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create("TEST.CODE")
            .WithMessage("Test message")
            .WithSeverity(RuleSeverity.Warning)
            .WithName("Test Rule")
            .WithDescription("A test rule")
            .WithCategory("Testing")
            .WithTags("test", "unit")
            .When(() => true)
            .Build();

        Assert.Equal("TEST.CODE", rule.Code);
        Assert.Equal("Test message", rule.Message);
        Assert.Equal(RuleSeverity.Warning, rule.Severity);
        Assert.Equal("Test Rule", rule.Name);
        Assert.Equal("A test rule", rule.Description);
        Assert.Equal("Testing", rule.Category);
        Assert.Contains("test", rule.Tags);
        Assert.Contains("unit", rule.Tags);
        Assert.True(rule.IsBroken());
    }

    [Fact]
    public void Build_WithMinimalProperties_CreatesRule()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create("MINIMAL")
            .WithMessage("Minimal rule")
            .When(() => false)
            .Build();

        Assert.Equal("MINIMAL", rule.Code);
        Assert.Equal("Minimal rule", rule.Message);
        Assert.Equal(RuleSeverity.Error, rule.Severity);
        Assert.False(rule.IsBroken());
    }

    [Fact]
    public void Build_WithContextCondition_UsesContext()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create("CONTEXT.TEST")
            .WithMessage("Context test")
            .When(ctx => ctx.Get<bool>("flag"))
            .Build();

        BusinessRuleContext context = new BusinessRuleContext();
        context.Set("flag", true);

        Assert.True(rule.IsBroken(context));
    }

    [Fact]
    public void Build_WithoutCondition_Throws()
    {
        BusinessRuleBuilder builder = BusinessRuleBuilder.Create("NO.CONDITION")
            .WithMessage("No condition");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Check_WhenBroken_ThrowsException()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create("BROKEN")
            .WithMessage("Broken rule")
            .When(() => true)
            .Build();

        Assert.Throws<BusinessRuleValidationException>(() => rule.Check());
    }

    [Fact]
    public void Evaluate_ReturnsCorrectResult()
    {
        IBusinessRule rule = BusinessRuleBuilder.Create("EVAL.TEST")
            .WithMessage("Evaluation test")
            .WithSeverity(RuleSeverity.Warning)
            .When(() => true)
            .Build();

        BusinessRuleResult result = rule.Evaluate();

        Assert.Equal("EVAL.TEST", result.Code);
        Assert.Equal("Evaluation test", result.Message);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
    }
}

public class AsyncBusinessRuleBuilderTests
{
    [Fact]
    public async Task Build_WithAllProperties_CreatesAsyncRule()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create("ASYNC.TEST")
            .WithMessage("Async test message")
            .WithSeverity(RuleSeverity.Info)
            .WithName("Async Test Rule")
            .WithDescription("An async test rule")
            .WithCategory("Async Testing")
            .WithTags("async", "test")
            .WhenAsync(async (ctx, ct) => await Task.FromResult(true))
            .Build();

        Assert.Equal("ASYNC.TEST", rule.Code);
        Assert.Equal("Async test message", rule.Message);
        Assert.Equal(RuleSeverity.Info, rule.Severity);
        Assert.True(await rule.IsBrokenAsync(new BusinessRuleContext()));
    }

    [Fact]
    public async Task Build_WithSimpleAsyncCondition_Works()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create("SIMPLE.ASYNC")
            .WithMessage("Simple async")
            .WhenAsync(ctx => Task.FromResult(false))
            .Build();

        Assert.False(await rule.IsBrokenAsync(new BusinessRuleContext()));
    }

    [Fact]
    public void Build_WithoutCondition_Throws()
    {
        AsyncBusinessRuleBuilder builder = AsyncBusinessRuleBuilder.Create("NO.ASYNC.CONDITION")
            .WithMessage("No condition");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public async Task CheckAsync_WhenBroken_ThrowsException()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create("ASYNC.BROKEN")
            .WithMessage("Broken async rule")
            .WhenAsync((ctx, ct) => Task.FromResult(true))
            .Build();

        await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => rule.CheckAsync(new BusinessRuleContext()));
    }

    [Fact]
    public async Task EvaluateAsync_WhenBroken_ReturnsResult()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create("ASYNC.EVAL")
            .WithMessage("Async evaluation")
            .WithSeverity(RuleSeverity.Error)
            .WhenAsync((ctx, ct) => Task.FromResult(true))
            .Build();

        BusinessRuleResult? result = await rule.EvaluateAsync(new BusinessRuleContext());

        Assert.NotNull(result);
        Assert.Equal("ASYNC.EVAL", result.Code);
    }

    [Fact]
    public async Task EvaluateAsync_WhenNotBroken_ReturnsNull()
    {
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create("ASYNC.VALID")
            .WithMessage("Valid async rule")
            .WhenAsync((ctx, ct) => Task.FromResult(false))
            .Build();

        BusinessRuleResult? result = await rule.EvaluateAsync(new BusinessRuleContext());

        Assert.Null(result);
    }
}
