# Masterly.BusinessRules.Testing

Test utilities for business rules: outcome coverage (which rules your tests saw both broken and passing), a boundary finder, CsCheck-backed property checks and smoke-test mutants.

This is a companion package of [Masterly.BusinessRules](https://www.nuget.org/packages/Masterly.BusinessRules), a composable business-rule engine for .NET.

[![NuGet](https://img.shields.io/nuget/v/Masterly.BusinessRules.Testing?style=flat-square)](https://www.nuget.org/packages/Masterly.BusinessRules.Testing)

## Install

Add it to your test project:

```bash
dotnet add package Masterly.BusinessRules.Testing
```

- **Target frameworks:** `net8.0`, `net10.0` (your test project must target .NET 8 or later)
- **Dependencies:** `Masterly.BusinessRules`, [CsCheck](https://github.com/AnthonyLloyd/CsCheck) 4.9.1 on `net8.0` and 5.0.0 on `net10.0`

## Example

```csharp
using Masterly.BusinessRules;
using Masterly.BusinessRules.Testing;

public sealed class OrderTotalLimitRule : BaseBusinessRule
{
    private readonly decimal _orderTotal;

    public OrderTotalLimitRule(decimal orderTotal) => _orderTotal = orderTotal;

    public override string Code => "ORDER.TOTAL_LIMIT";

    public override string Message => "The order total must not exceed 10,000.";

    public override bool IsBroken() => _orderTotal > 10_000m;
}

public static class OrderRuleCoverage
{
    public static void Run()
    {
        using RuleCoverageSession coverage = RuleCoverage.Begin();

        BusinessRuleChecker.EvaluateAll(new OrderTotalLimitRule(12_500m).WithCoverage(coverage));   // broken
        BusinessRuleChecker.EvaluateAll(new OrderTotalLimitRule(900m).WithCoverage(coverage));      // passed

        RuleCoverageReport report = coverage.Report();
        report.AssertMinimum(1.0);   // throws RuleAssertionException when a rule was never broken or never passed
    }
}
```

`report.ToText()` prints a per-rule table you can write to your test output.

## Documentation

The boundary finder, property checks, mutants and the CsCheck version notes are covered in the full documentation: <https://github.com/a7mdfre7at/Masterly.BusinessRules#readme>
