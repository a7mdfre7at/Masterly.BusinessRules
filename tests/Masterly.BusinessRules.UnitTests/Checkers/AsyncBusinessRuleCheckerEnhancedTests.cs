using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules.UnitTests;

public class AsyncBusinessRuleCheckerEnhancedTests
{
    [Fact]
    public async Task CheckAllAsync_WithRulesCollection_Works()
    {
        IAsyncBusinessRule[] rules = new IAsyncBusinessRule[] { new AsyncRuleA(false), new AsyncRuleA(false) };
        BusinessRuleContext context = new BusinessRuleContext();

        // Should not throw
        await AsyncBusinessRuleChecker.CheckAllAsync(context, rules);
    }

    [Fact]
    public async Task CheckAllAsync_StopOnFirstFailure_StopsEarly()
    {
        int evaluationCount = 0;

        IAsyncBusinessRule CreateCountingRule(bool broken)
        {
            return AsyncBusinessRuleBuilder.Create($"ASYNC_RULE_{evaluationCount}")
                .WithMessage("Counting rule")
                .WhenAsync(async (ctx, ct) =>
                {
                    evaluationCount++;
                    return await Task.FromResult(broken);
                })
                .Build();
        }

        IAsyncBusinessRule[] rules = new[]
        {
            CreateCountingRule(false),
            CreateCountingRule(true),  // This one breaks
            CreateCountingRule(true)   // This should not be evaluated
        };

        BusinessRuleContext context = new BusinessRuleContext();

        try
        {
            await AsyncBusinessRuleChecker.CheckAllAsync(
                context, rules, stopOnFirstFailure: true);
        }
        catch (BusinessRuleValidationException ex)
        {
            Assert.Single(ex.BrokenRules);
        }

        Assert.Equal(2, evaluationCount); // Only first two evaluated
    }

    [Fact]
    public async Task CheckAllAsync_RunInParallel_EvaluatesAllRules()
    {
        List<string> evaluatedRules = new List<string>();

        IAsyncBusinessRule CreateTrackingRule(string name, bool broken)
        {
            return AsyncBusinessRuleBuilder.Create(name)
                .WithMessage($"Rule {name}")
                .WhenAsync(async (ctx, ct) =>
                {
                    await Task.Delay(10, ct);
                    lock (evaluatedRules)
                    {
                        evaluatedRules.Add(name);
                    }
                    return broken;
                })
                .Build();
        }

        IAsyncBusinessRule[] rules = new[]
        {
            CreateTrackingRule("A", false),
            CreateTrackingRule("B", false),
            CreateTrackingRule("C", false)
        };

        BusinessRuleContext context = new BusinessRuleContext();

        await AsyncBusinessRuleChecker.CheckAllAsync(
            context, rules, runInParallel: true);

        Assert.Equal(3, evaluatedRules.Count);
    }

    [Fact]
    public async Task CheckAllAsync_WithObserver_CallsObserverMethods()
    {
        TestAsyncRuleObserver observer = new TestAsyncRuleObserver();
        IAsyncBusinessRule[] rules = new IAsyncBusinessRule[] { new AsyncRuleA(false), new AsyncRuleA(true) };
        BusinessRuleContext context = new BusinessRuleContext();

        try
        {
            await AsyncBusinessRuleChecker.CheckAllAsync(
                context, rules, observer: observer);
        }
        catch { }

        Assert.Equal(2, observer.BeforeEvaluateCount);
        Assert.Equal(2, observer.AfterEvaluateCount);
        Assert.Equal(1, observer.RuleBrokenCount);
    }

    [Fact]
    public async Task CheckAllAsync_WithCancellation_ThrowsOperationCanceled()
    {
        CancellationTokenSource cts = new CancellationTokenSource();
        IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create("SLOW")
            .WithMessage("Slow rule")
            .WhenAsync(async (ctx, ct) =>
            {
                await Task.Delay(5000, ct);
                return false;
            })
            .Build();

        BusinessRuleContext context = new BusinessRuleContext();

        cts.CancelAfter(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(
                context, [rule], cancellationToken: cts.Token));
    }

    [Fact]
    public async Task EvaluateAllAsync_ReturnsDetailedResult()
    {
        IAsyncBusinessRule[] rules = new IAsyncBusinessRule[]
        {
            new AsyncRuleA(false),
            new AsyncRuleA(true),
            new AsyncRuleA(false)
        };
        BusinessRuleContext context = new BusinessRuleContext();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(context, rules);

        Assert.Single(result.BrokenRules);
        Assert.Equal(2, result.PassedRules.Count);
        Assert.True(result.HasBrokenRules);
        Assert.False(result.AllPassed);
    }

    [Fact]
    public async Task EvaluateAllAsync_RunInParallel_Works()
    {
        IAsyncBusinessRule[] rules = new IAsyncBusinessRule[]
        {
            new AsyncRuleA(false),
            new AsyncRuleA(true),
            new AsyncRuleA(true)
        };
        BusinessRuleContext context = new BusinessRuleContext();

        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(
            context, rules, runInParallel: true);

        Assert.Equal(2, result.BrokenRules.Count);
        Assert.Single(result.PassedRules);
    }

    [Fact]
    public async Task CheckBySeverityAsync_FiltersCorrectly()
    {
        IAsyncBusinessRule errorRule = AsyncBusinessRuleBuilder.Create("ERROR")
            .WithMessage("Error rule")
            .WithSeverity(RuleSeverity.Error)
            .WhenAsync((ctx, ct) => Task.FromResult(true))
            .Build();

        IAsyncBusinessRule warningRule = AsyncBusinessRuleBuilder.Create("WARNING")
            .WithMessage("Warning rule")
            .WithSeverity(RuleSeverity.Warning)
            .WhenAsync((ctx, ct) => Task.FromResult(true))
            .Build();

        BusinessRuleContext context = new BusinessRuleContext();

        // Only check errors
        BusinessRuleValidationException ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckBySeverityAsync(
                context, [errorRule, warningRule], severities: RuleSeverity.Error));

        Assert.Single(ex.BrokenRules);
        Assert.Equal("ERROR", ex.BrokenRules.First().Code);
    }

    [Fact]
    public async Task CheckByCategoryAsync_FiltersCorrectly()
    {
        IAsyncBusinessRule checkoutRule = AsyncBusinessRuleBuilder.Create("CHECKOUT")
            .WithMessage("Checkout rule")
            .WithCategory("Checkout")
            .WhenAsync((ctx, ct) => Task.FromResult(true))
            .Build();

        IAsyncBusinessRule userRule = AsyncBusinessRuleBuilder.Create("USER")
            .WithMessage("User rule")
            .WithCategory("User")
            .WhenAsync((ctx, ct) => Task.FromResult(true))
            .Build();

        BusinessRuleContext context = new BusinessRuleContext();

        BusinessRuleValidationException ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckByCategoryAsync(
                context, [checkoutRule, userRule], "Checkout"));

        Assert.Single(ex.BrokenRules);
        Assert.Equal("CHECKOUT", ex.BrokenRules.First().Code);
    }

    private class TestAsyncRuleObserver : IAsyncRuleExecutionObserver
    {
        public int BeforeEvaluateCount { get; private set; }
        public int AfterEvaluateCount { get; private set; }
        public int RuleBrokenCount { get; private set; }

        public Task OnBeforeEvaluateAsync(IAsyncBusinessRule rule, CancellationToken cancellationToken = default)
        {
            BeforeEvaluateCount++;
            return Task.CompletedTask;
        }

        public Task OnAfterEvaluateAsync(IAsyncBusinessRule rule, BusinessRuleResult? result, CancellationToken cancellationToken = default)
        {
            AfterEvaluateCount++;
            return Task.CompletedTask;
        }

        public Task OnRuleBrokenAsync(IAsyncBusinessRule rule, BusinessRuleResult result, CancellationToken cancellationToken = default)
        {
            RuleBrokenCount++;
            return Task.CompletedTask;
        }
    }
}
