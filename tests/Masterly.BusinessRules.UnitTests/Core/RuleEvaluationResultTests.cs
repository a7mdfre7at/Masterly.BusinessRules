using System.Threading.Tasks;

namespace Masterly.BusinessRules.UnitTests;

public class RuleEvaluationResultTests
{
    [Fact]
    public void AllPassed_WhenNoBrokenRules_ReturnsTrue()
    {
        RuleEvaluationResult result = new RuleEvaluationResult(
            brokenRules: Array.Empty<BusinessRuleResult>(),
            passedRules: new[] { new RuleA(false) });

        Assert.True(result.AllPassed);
        Assert.False(result.HasBrokenRules);
    }

    [Fact]
    public void HasBrokenRules_WhenRulesBroken_ReturnsTrue()
    {
        RuleEvaluationResult result = new RuleEvaluationResult(
            brokenRules: new[] { new BusinessRuleResult("CODE", "Message", RuleSeverity.Error) },
            passedRules: Array.Empty<IBusinessRule>());

        Assert.True(result.HasBrokenRules);
        Assert.False(result.AllPassed);
    }

    [Fact]
    public void GetBySeverity_FiltersCorrectly()
    {
        RuleEvaluationResult result = new RuleEvaluationResult(
            brokenRules: new[]
            {
                new BusinessRuleResult("ERR1", "Error 1", RuleSeverity.Error),
                new BusinessRuleResult("WARN1", "Warning 1", RuleSeverity.Warning),
                new BusinessRuleResult("ERR2", "Error 2", RuleSeverity.Error),
                new BusinessRuleResult("INFO1", "Info 1", RuleSeverity.Info)
            },
            passedRules: Array.Empty<IBusinessRule>());

        Assert.Equal(2, result.GetBySeverity(RuleSeverity.Error).Count());
        Assert.Single(result.GetBySeverity(RuleSeverity.Warning));
        Assert.Single(result.GetBySeverity(RuleSeverity.Info));
    }

    [Fact]
    public void Errors_ReturnsOnlyErrors()
    {
        RuleEvaluationResult result = new RuleEvaluationResult(
            brokenRules: new[]
            {
                new BusinessRuleResult("ERR", "Error", RuleSeverity.Error),
                new BusinessRuleResult("WARN", "Warning", RuleSeverity.Warning)
            },
            passedRules: Array.Empty<IBusinessRule>());

        Assert.Single(result.Errors);
        Assert.Equal("ERR", result.Errors.First().Code);
    }

    [Fact]
    public void Warnings_ReturnsOnlyWarnings()
    {
        RuleEvaluationResult result = new RuleEvaluationResult(
            brokenRules: new[]
            {
                new BusinessRuleResult("ERR", "Error", RuleSeverity.Error),
                new BusinessRuleResult("WARN", "Warning", RuleSeverity.Warning)
            },
            passedRules: Array.Empty<IBusinessRule>());

        Assert.Single(result.Warnings);
        Assert.Equal("WARN", result.Warnings.First().Code);
    }

    [Fact]
    public void HasErrors_WhenErrorsExist_ReturnsTrue()
    {
        RuleEvaluationResult result = new RuleEvaluationResult(
            brokenRules: new[]
            {
                new BusinessRuleResult("ERR", "Error", RuleSeverity.Error)
            },
            passedRules: Array.Empty<IBusinessRule>());

        Assert.True(result.HasErrors);
    }

    [Fact]
    public void HasWarnings_WhenWarningsExist_ReturnsTrue()
    {
        RuleEvaluationResult result = new RuleEvaluationResult(
            brokenRules: new[]
            {
                new BusinessRuleResult("WARN", "Warning", RuleSeverity.Warning)
            },
            passedRules: Array.Empty<IBusinessRule>());

        Assert.True(result.HasWarnings);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void ThrowIfBroken_WhenBrokenRulesExist_Throws()
    {
        RuleEvaluationResult result = new RuleEvaluationResult(
            brokenRules: new[]
            {
                new BusinessRuleResult("CODE", "Message", RuleSeverity.Warning)
            },
            passedRules: Array.Empty<IBusinessRule>());

        Assert.Throws<BusinessRuleValidationException>(() => result.ThrowIfBroken());
    }

    [Fact]
    public void ThrowIfBroken_WhenNoBrokenRules_DoesNotThrow()
    {
        RuleEvaluationResult result = new RuleEvaluationResult(
            brokenRules: Array.Empty<BusinessRuleResult>(),
            passedRules: new[] { new RuleA(false) });

        // Should not throw
        result.ThrowIfBroken();
    }

    [Fact]
    public void ThrowIfErrors_WhenOnlyWarnings_DoesNotThrow()
    {
        RuleEvaluationResult result = new RuleEvaluationResult(
            brokenRules: new[]
            {
                new BusinessRuleResult("WARN", "Warning", RuleSeverity.Warning)
            },
            passedRules: Array.Empty<IBusinessRule>());

        // Should not throw (only warnings, no errors)
        result.ThrowIfErrors();
    }

    [Fact]
    public void ThrowIfErrors_WhenErrorsExist_Throws()
    {
        RuleEvaluationResult result = new RuleEvaluationResult(
            brokenRules: new[]
            {
                new BusinessRuleResult("ERR", "Error", RuleSeverity.Error),
                new BusinessRuleResult("WARN", "Warning", RuleSeverity.Warning)
            },
            passedRules: Array.Empty<IBusinessRule>());

        BusinessRuleValidationException ex = Assert.Throws<BusinessRuleValidationException>(() => result.ThrowIfErrors());
        Assert.Single(ex.BrokenRules); // Only the error, not the warning
    }
}

public class AsyncRuleEvaluationResultTests
{
    [Fact]
    public void AllPassed_WhenNoBrokenRules_ReturnsTrue()
    {
        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(
            brokenRules: Array.Empty<BusinessRuleResult>(),
            passedRules: new[] { new AsyncRuleA(false) });

        Assert.True(result.AllPassed);
        Assert.False(result.HasBrokenRules);
    }

    [Fact]
    public void ThrowIfErrors_WhenOnlyWarnings_DoesNotThrow()
    {
        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(
            brokenRules: new[]
            {
                new BusinessRuleResult("WARN", "Warning", RuleSeverity.Warning)
            },
            passedRules: Array.Empty<IAsyncBusinessRule>());

        // Should not throw
        result.ThrowIfErrors();
    }

    [Fact]
    public void ThrowIfErrors_WhenErrorsExist_Throws()
    {
        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(
            brokenRules: new[]
            {
                new BusinessRuleResult("ERR", "Error", RuleSeverity.Error)
            },
            passedRules: Array.Empty<IAsyncBusinessRule>());

        Assert.Throws<BusinessRuleValidationException>(() => result.ThrowIfErrors());
    }

    [Fact]
    public void Errors_ReturnsOnlyErrors()
    {
        AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(
            brokenRules: new[]
            {
                new BusinessRuleResult("ERR", "Error", RuleSeverity.Error),
                new BusinessRuleResult("WARN", "Warning", RuleSeverity.Warning),
                new BusinessRuleResult("INFO", "Info", RuleSeverity.Info)
            },
            passedRules: Array.Empty<IAsyncBusinessRule>());

        Assert.Single(result.Errors);
        Assert.Single(result.Warnings);
        Assert.Single(result.Infos);
    }
}
