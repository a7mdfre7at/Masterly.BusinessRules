namespace Masterly.BusinessRules.UnitTests;

public class BusinessRuleCheckerEnhancedTests
{
    [Fact]
    public void CheckAll_WithIBusinessRule_Works()
    {
        IBusinessRule[] rules = [new RuleA(false), new RuleA(false)];

        // Should not throw
        BusinessRuleChecker.CheckAll(rules);
    }

    [Fact]
    public void CheckAll_WithContext_PassesContext()
    {
        RuleWithContextSupport rule = new RuleWithContextSupport();
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set("shouldBeBroken", true);

        Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckAll(context, rule));
    }

    [Fact]
    public void CheckAll_StopOnFirstFailure_StopsEarly()
    {
        int evaluationCount = 0;

        IBusinessRule CreateCountingRule(bool broken)
        {
            return BusinessRuleBuilder.Create($"RULE_{evaluationCount}")
                .WithMessage("Counting rule")
                .When(() =>
                {
                    evaluationCount++;
                    return broken;
                })
                .Build();
        }

        IBusinessRule[] rules = new[]
        {
            CreateCountingRule(false),
            CreateCountingRule(true),  // This one breaks
            CreateCountingRule(true)   // This should not be evaluated
        };

        try
        {
            BusinessRuleChecker.CheckAll(rules, stopOnFirstFailure: true);
        }
        catch (BusinessRuleValidationException ex)
        {
            Assert.Single(ex.BrokenRules);
        }

        Assert.Equal(2, evaluationCount); // Only first two evaluated
    }

    [Fact]
    public void CheckAll_WithObserver_CallsObserverMethods()
    {
        TestRuleObserver observer = new TestRuleObserver();
        IBusinessRule[] rules = [new RuleA(false), new RuleA(true)];

        try
        {
            BusinessRuleChecker.CheckAll(rules, observer: observer);
        }
        catch { }

        Assert.Equal(2, observer.BeforeEvaluateCount);
        Assert.Equal(2, observer.AfterEvaluateCount);
        Assert.Equal(1, observer.RuleBrokenCount);
    }

    [Fact]
    public void EvaluateAll_ReturnsDetailedResult()
    {
        IBusinessRule[] rules = [new RuleA(false), new RuleA(true), new RuleA(false)];

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rules);

        Assert.Single(result.BrokenRules);
        Assert.Equal(2, result.PassedRules.Count);
        Assert.True(result.HasBrokenRules);
        Assert.False(result.AllPassed);
    }

    [Fact]
    public void EvaluateAll_WithContext_Works()
    {
        RuleWithContextSupport rule = new RuleWithContextSupport();
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set("shouldBeBroken", true);

        RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(context, rule);

        Assert.Single(result.BrokenRules);
    }

    [Fact]
    public void CheckBySeverity_FiltersCorrectly()
    {
        IBusinessRule errorRule = BusinessRuleBuilder.Create("ERROR")
            .WithMessage("Error rule")
            .WithSeverity(RuleSeverity.Error)
            .When(() => true)
            .Build();

        IBusinessRule warningRule = BusinessRuleBuilder.Create("WARNING")
            .WithMessage("Warning rule")
            .WithSeverity(RuleSeverity.Warning)
            .When(() => true)
            .Build();

        // Only check warnings - should throw for warning rule
        Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckBySeverity([errorRule, warningRule], RuleSeverity.Warning));
    }

    [Fact]
    public void CheckByCategory_FiltersCorrectly()
    {
        IBusinessRule checkoutRule = BusinessRuleBuilder.Create("CHECKOUT")
            .WithMessage("Checkout rule")
            .WithCategory("Checkout")
            .When(() => true)
            .Build();

        IBusinessRule userRule = BusinessRuleBuilder.Create("USER")
            .WithMessage("User rule")
            .WithCategory("User")
            .When(() => true)
            .Build();

        // Only check Checkout category
        BusinessRuleValidationException ex = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckByCategory([checkoutRule, userRule], "Checkout"));

        Assert.Single(ex.BrokenRules);
        Assert.Equal("CHECKOUT", ex.BrokenRules.First().Code);
    }

    [Fact]
    public void CheckByTags_FiltersCorrectly()
    {
        IBusinessRule criticalRule = BusinessRuleBuilder.Create("CRITICAL")
            .WithMessage("Critical rule")
            .WithTags("critical", "important")
            .When(() => true)
            .Build();

        IBusinessRule normalRule = BusinessRuleBuilder.Create("NORMAL")
            .WithMessage("Normal rule")
            .WithTags("normal")
            .When(() => true)
            .Build();

        // Only check critical tag
        BusinessRuleValidationException ex = Assert.Throws<BusinessRuleValidationException>(
            () => BusinessRuleChecker.CheckByTags([criticalRule, normalRule], "critical"));

        Assert.Single(ex.BrokenRules);
        Assert.Equal("CRITICAL", ex.BrokenRules.First().Code);
    }

    private class TestRuleObserver : IRuleExecutionObserver
    {
        public int BeforeEvaluateCount { get; private set; }
        public int AfterEvaluateCount { get; private set; }
        public int RuleBrokenCount { get; private set; }

        public void OnBeforeEvaluate(IBusinessRule rule) => BeforeEvaluateCount++;
        public void OnAfterEvaluate(IBusinessRule rule, BusinessRuleResult? result) => AfterEvaluateCount++;
        public void OnRuleBroken(IBusinessRule rule, BusinessRuleResult result) => RuleBrokenCount++;
    }
}
