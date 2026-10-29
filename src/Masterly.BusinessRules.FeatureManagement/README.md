# Masterly.BusinessRules.FeatureManagement

Drives feature-gated business rules from `Microsoft.FeatureManagement` flags and variants, so a rule can be off, in shadow mode for a percentage of users, or enforced, using the same flags as the rest of your application.

This is a companion package of [Masterly.BusinessRules](https://www.nuget.org/packages/Masterly.BusinessRules), a composable business-rule engine for .NET.

[![NuGet](https://img.shields.io/nuget/v/Masterly.BusinessRules.FeatureManagement?style=flat-square)](https://www.nuget.org/packages/Masterly.BusinessRules.FeatureManagement)

## Install

```bash
dotnet add package Masterly.BusinessRules.FeatureManagement
```

- **Target framework:** `netstandard2.1` (.NET Core 3.0+, .NET 5+)
- **Dependencies:** `Masterly.BusinessRules`, `Microsoft.FeatureManagement` 4.8.0

## Example

```csharp
using Masterly.BusinessRules;
using Masterly.BusinessRules.FeatureManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;

public static class Registration
{
    // Registers one provider as IRuleFeatureFlagProvider and IAsyncRuleFeatureFlagProvider.
    public static IServiceCollection AddRuleFeatureFlags(this IServiceCollection services)
    {
        services.AddFeatureManagement();
        return services.AddBusinessRulesFeatureManagement();
    }
}

public sealed class CheckoutRules
{
    private readonly IAsyncRuleFeatureFlagProvider _flags;

    public CheckoutRules(IAsyncRuleFeatureFlagProvider flags)
    {
        _flags = flags;
    }

    // The flag decides per evaluation whether the rule is off, shadowed or enforced.
    // The stable key keeps each customer in the same rollout bucket.
    public IAsyncBusinessRule CreditScore(IAsyncBusinessRule creditScoreRule)
    {
        return creditScoreRule.GatedBy("credit-score-v2", _flags, stableKey: context => context.Get<string>("customerId"));
    }
}
```

A plain flag maps to enforced when on and off when off. A variant whose configuration is `{ "Mode": "Shadow", "Percent": 10 }` runs the rule in shadow mode for 10% of keys: violations are reported to observers and `ShadowResults`, but never block.

## Documentation

Variant conventions, targeting, lifetimes and shadow results are covered in the full documentation: <https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/feature-flags#using-microsoftfeaturemanagement>
