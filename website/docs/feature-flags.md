---
id: feature-flags
title: "Feature Flags and Shadow Rollout"
sidebar_label: "Feature Flags"
---

A feature flag lets you ship a new rule switched off or in observe-only mode, then turn it on from configuration. You do not change the rule class. Use it when a new rule could block real customers and you want to see what it would do first.

```csharp
// customer comes from your application; CreditScoreRule is one of your IBusinessRule classes
InMemoryRuleFeatureFlagProvider flags = new InMemoryRuleFeatureFlagProvider()
    .Set("credit-score-v2", RuleRolloutMode.Shadow);

IBusinessRule creditScore = new CreditScoreRule(customer)
    .GatedBy("credit-score-v2", flags);

BusinessRuleContext context = new BusinessRuleContext();
RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(context, creditScore);

// Shadow mode never blocks: result.AllPassed is true.
// A violation is reported in result.ShadowResults instead.
```

Later, switch the flag to `Enforced` and the same rule starts blocking:

```csharp
// flags is the provider from the example above
flags.Set("credit-score-v2", RuleRolloutMode.Enforced);
```

## Gating a rule

Call `GatedBy(flag, provider, stableKey)` on any sync or async rule. The gated rule keeps the inner rule's code, message, severity and other metadata. It asks the provider for the flag's rollout on **every evaluation**, so a flag change takes effect on the next call.

A rollout has a mode (`RuleRolloutMode`) and a percent:

| Mode | Is the inner rule evaluated? | What the gated rule reports |
|------|------------------------------|-----------------------------|
| `Off` | No | Passes |
| `Shadow` | Yes | Always passes. A violation goes to `ShadowResults` and the observer instead. |
| `Enforced` | Yes | The inner rule's own result |

A rollout at 0% counts as `Off`. A caller outside the rollout percentage is also treated as `Off`.

**Ramping by percentage.** To roll a rule out to part of your traffic, give the flag a percent and pass a `stableKey`. The stable key identifies the caller, for example by customer id or tenant id.

```csharp
// flags is the provider from the first example; customer and sanctionsApi come from your application;
// SanctionsCheckRule is one of your IAsyncBusinessRule classes
flags.Set("sanctions-check", RuleRolloutMode.Enforced, percent: 10);

IAsyncBusinessRule sanctions = new SanctionsCheckRule(sanctionsApi)
    .GatedBy("sanctions-check", flags, stableKey: ctx => ctx.Get<string>("customerId"));

BusinessRuleContext context = new BusinessRuleContext();
context.Set("customerId", customer.Id);
```

How the percentage works:

- The same key always gets the same answer, so a customer is either in the cohort or not, on every request.
- A customer inside a 10% cohort stays inside when you grow the rollout to 20%.
- Different flags pick different customers, because the flag name is part of the calculation.
- At 100% the stable key is never called.
- A rollout between 1% and 99% without a `stableKey` throws `RuleConfigurationException`. So does a stable key that returns `null`.
- A percent outside 0 to 100 throws `ArgumentOutOfRangeException` when you create the rollout.

**Where the provider comes from.** You usually pass the provider to `GatedBy`. You can also put one on the context. It then overrides the injected provider for that evaluation, which is useful in tests and canary pipelines.

```csharp
// context is the BusinessRuleContext for the current request. Force the rule on for this request only:
context.Set(RuleFeatureFlags.ContextKey, RuleTestHelper.CreateFlagProvider("credit-score-v2", RuleRolloutMode.Enforced));
```

- Sync providers implement `IRuleFeatureFlagProvider`. Async providers implement `IAsyncRuleFeatureFlagProvider`; on the context they go under `RuleFeatureFlags.AsyncContextKey`.
- An async gated rule accepts either kind of provider. It looks in this order: an async provider on the context (`RuleFeatureFlags.AsyncContextKey`), a sync provider on the context (`RuleFeatureFlags.ContextKey`), the async provider you passed to `GatedBy`, then the sync one. The first one found decides.
- `GatedBy(flag, stableKey)` without a provider is allowed. The provider must then be on the context.
- If no provider is found, the evaluation throws `RuleConfigurationException` naming the rule's code.

`InMemoryRuleFeatureFlagProvider` implements both interfaces and is thread-safe, so you can change flags while the application runs. A flag you never set is `Off`. To change that, pass a default rollout to its constructor.

:::note

Always pass a context to a sync gated rule. Without one, for example `rule.IsBroken()` or `BusinessRuleChecker.EvaluateAll(rule)`, the rule ignores its flag and runs as if `Enforced`, so a shadow rule blocks. Async rules always have a context.

:::

:::note

Put a cache inside the gate, never outside it. `rule.Cached(ttl).GatedBy(...)` caches the inner rule's result and still checks the flag on every call. `rule.GatedBy(...).Cached(ttl)` caches the gate's answer, so a flag change has no effect until the cache expires. If the result depends on the caller, cache per key, as shown in [Cache expensive rules](./conditional-and-cached-rules.md#cache-expensive-rules).

:::

**Explaining a gate.** `Explain(context)` shows what the gate decided:

| Situation | Operator | Outcome | Reason |
|-----------|----------|---------|--------|
| Enforced | `GATED` | The inner rule's outcome | (none) |
| Shadow, inner rule broken | `SHADOW` | `Passed` | `shadow violation` |
| Flag off | `GATED` | `Skipped` | `flag 'credit-score-v2' is off` |
| Caller outside the cohort | `GATED` | `Skipped` | `flag 'sanctions-check' excludes this caller (10%)` (the exact text names the caller's bucket) |
| Sync rule, no context | `GATED` | The inner rule's outcome | `no context: evaluated un-gated` |

```csharp
// creditScore and context are from the first example in this section
RuleExplanation explanation = creditScore.Explain(context);
Console.WriteLine($"{explanation.Operator} {explanation.Outcome} {explanation.Reason}");
// SHADOW Passed shadow violation
```

## Seeing what shadow rules would have blocked

In shadow mode a rule runs but never blocks. You read what it would have reported from the batch result, or from an observer as each violation happens.

```csharp
// creditScore is the shadow rule from above; existingRule and context come from your application
RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(
    new[] { creditScore, existingRule },
    context,
    new RuleEvaluationOptions { Observer = new ShadowMetricsObserver() });

foreach (BusinessRuleResult wouldHaveFailed in result.ShadowResults)
{
    Console.WriteLine($"[SHADOW] {wouldHaveFailed.Code}: {wouldHaveFailed.Message}");
}

public sealed class ShadowMetricsObserver : IRuleExecutionObserver
{
    public void OnBeforeEvaluate(IBusinessRule rule) { }

    public void OnAfterEvaluate(IBusinessRule rule, BusinessRuleResult? result) { }

    public void OnRuleBroken(IBusinessRule rule, BusinessRuleResult result) { }

    public void OnShadowViolation(IBusinessRule rule, BusinessRuleResult result)
    {
        Console.WriteLine($"{rule.Code} would have failed: {result.Message}");
    }
}
```

What you can rely on:

- `ShadowResults` and `HasShadowResults` exist on both `RuleEvaluationResult` and `AsyncRuleEvaluationResult`.
- Shadow results are never in `BrokenRules`. They never make `CheckAll` throw and never change `AllPassed`.
- A gated rule nested inside a group, a cache or an adapter is still collected.
- The observer hooks are `OnShadowViolation` and, on async observers, `OnShadowViolationAsync`. They have empty default implementations, so existing observers keep compiling. Override only what you need.
- The hooks are raised only by checker calls that are given an observer, usually through `RuleEvaluationOptions.Observer` or `AsyncRuleEvaluationOptions.Observer`.
- In a parallel async batch, the shadow hooks run after every rule has finished.

**A shadow rule that throws.** A buggy rule in shadow mode must not break checkout, so the gate catches its exception. Cancellation is the only exception that still propagates. When the inner rule throws:

- The batch still passes.
- `ShadowResults` gets a stand-in result carrying the gated rule's code and message.
- The observer receives `OnShadowViolation` with that stand-in, then `OnShadowFault(rule, exception)` (`OnShadowFaultAsync` on async observers).
- `Explain` shows the inner rule as a `Faulted` node under the passing `SHADOW` node.

Enforced rules get no such protection. Their exceptions follow the batch's fault policy.

:::note

Shadow results are collected only when the rule runs through a checker: `BusinessRuleChecker`, `AsyncBusinessRuleChecker`, or the dependency injection runner and registry, which use them. If you call `IsBroken(context)` on a shadow rule directly, it returns `false` and the violation is not recorded anywhere.

:::

## Using Microsoft.FeatureManagement

The `Masterly.BusinessRules.FeatureManagement` package reads rollouts from `Microsoft.FeatureManagement`. Your rules then use the same flags, filters and variants as the rest of your application.

```bash
dotnet add package Masterly.BusinessRules.FeatureManagement
```

Register Microsoft.FeatureManagement and the adapter:

```csharp
using Masterly.BusinessRules;
using Masterly.BusinessRules.FeatureManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

// services is your IServiceCollection
services.AddFeatureManagement();
services.AddBusinessRulesFeatureManagement(options =>
{
    options.TargetingContextSelector = ctx => new TargetingContext { UserId = ctx.Get<string>("customerId") };
    options.OnUnrecognizedVariant = UnrecognizedVariantBehavior.Off;
});
```

Then gate your rules with the registered provider:

```csharp
// flags is the IAsyncRuleFeatureFlagProvider injected from the container;
// customer, sanctionsApi and cancellationToken come from your application
IAsyncBusinessRule creditScore = new CreditScoreRule(customer)
    .ToAsync()
    .GatedBy("credit-score-v2", flags, stableKey: ctx => ctx.Get<string>("customerId"));

IAsyncBusinessRule sanctions = new SanctionsCheckRule(sanctionsApi)
    .GatedBy("sanctions-check", flags);

BusinessRuleContext context = new BusinessRuleContext();
context.Set("customerId", customer.Id);

AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(
    context,
    new[] { creditScore, sanctions },
    cancellationToken: cancellationToken);
```

This configuration ships `credit-score-v2` in shadow mode to 10% of customers. Change the allocation to `live` to enforce it for everybody.

```jsonc
{
  "feature_management": {
    "feature_flags": [
      {
        "id": "credit-score-v2",
        "enabled": true,
        "variants": [
          { "name": "dark", "configuration_value": { "Mode": "Shadow", "Percent": 10 } },
          { "name": "live", "configuration_value": { "Mode": "Enforced" } },
          { "name": "Off" }
        ],
        "allocation": { "default_when_enabled": "dark", "default_when_disabled": "Off" }
      },
      { "id": "sanctions-check", "enabled": true }   // plain flag: Enforced when on, Off when off
    ]
  }
}
```

**Registration.** `AddBusinessRulesFeatureManagement(configure, lifetime)` registers one provider instance as `FeatureManagementRuleFeatureFlagProvider`, `IRuleFeatureFlagProvider` and `IAsyncRuleFeatureFlagProvider`.

- It uses `IVariantFeatureManager` when one is registered, so variants are read. Otherwise it uses `IFeatureManager`, which only knows on and off.
- Microsoft.FeatureManagement must be registered too. Without it, resolving the provider throws `InvalidOperationException`.
- The lifetime defaults to `Singleton`, which matches `AddFeatureManagement`. Pass `ServiceLifetime.Scoped` when you use `AddScopedFeatureManagement`.
- Registrations use `TryAdd`, so a provider you registered earlier wins. If you call the method twice, only the first call's options are used.

**How a flag becomes a rollout.** On each evaluation the provider reads the flag's assigned variant first, then whether the flag is enabled:

| Microsoft.FeatureManagement returns | Rollout |
|-------------------------------------|---------|
| A variant you mapped with `options.MapVariant("canary", RuleRollout.Enforced(5))` | The mapped rollout |
| A variant whose configuration has `Mode` and an optional `Percent` (default 100) | That mode and percent |
| A variant whose configuration value or name is a mode with an optional percent: `Shadow`, `Enforced-10`, `enforced:25%`, `Off` | That mode and percent |
| Any other variant | Depends on `OnUnrecognizedVariant` (see below) |
| No variant, flag enabled | `EnabledRollout`, which defaults to `Enforced` at 100% |
| No variant, flag disabled or unknown | `Off` |

Variant names and modes are case-insensitive. Between the mode and the percent you can use `-`, `:`, `_`, `/` or a space.

`OnUnrecognizedVariant` decides what happens with a variant that does not describe a rollout:

- `Throw` (the default) throws `RolloutVariantFormatException`, which names the flag and the variant.
- `Off` treats the rule as off. Use it when other code shares the flag's variants.
- `UseEnabledState` ignores the variant and uses the flag's on/off state.

Set `options.EnabledRollout = RuleRollout.Shadow()` if you want every plain on/off flag to run its rules in shadow mode.

:::note

An assigned variant wins even when the flag is disabled, because Microsoft.FeatureManagement still assigns the `default_when_disabled` variant. Point that allocation at an `Off` variant, as in the example above.

:::

:::note

Ramp by percentage in one place only.
- A variant's `Percent` picks callers by the rule's `stableKey`.
- A `Microsoft.Percentage` or `Microsoft.Targeting` filter picks callers inside Microsoft.FeatureManagement.

If you use both, each caller is filtered twice. With a targeting filter, set `TargetingContextSelector` so the filter sees the same customer as your stable key.

:::

:::note

Microsoft.FeatureManagement is async only. The provider's sync `GetRollout` blocks on the async call. Prefer async gated rules on request paths.

:::

To validate variant names or configuration at startup, call `RolloutVariantConvention.TryParse(name, out RuleRollout rollout)` or `RolloutVariantConvention.TryRead(section, out RuleRollout rollout)`. Without dependency injection, create the provider directly: `new FeatureManagementRuleFeatureFlagProvider(variantFeatureManager, options)`.
