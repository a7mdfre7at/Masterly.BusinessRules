---
id: running-many-rules
title: "Running Many Rules"
---

Most operations need more than one rule. `BusinessRuleChecker` runs a batch of sync rules and `AsyncBusinessRuleChecker` runs a batch of async rules. You can stop at the first problem, collect every problem, or decide per severity what blocks the operation.

## Check all or evaluate all

Use `CheckAll` when a broken rule should stop the operation. Use `EvaluateAll` when you want every outcome back, for example to return all errors to an API client.

```csharp
// order, customer and context come from your application
List<IBusinessRule> rules = new List<IBusinessRule>
{
    new OrderMustHaveItemsRule(order),
    new OrderTotalWithinCreditLimitRule(order, customer),
    new ShippingAddressRequiredRule(order)
};

// Throws BusinessRuleValidationException when any rule is broken
BusinessRuleChecker.CheckAll(rules, context: context);

// Never throws for broken rules; returns every outcome
RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rules, context);
```

| Method | Broken rules | Returns |
|--------|--------------|---------|
| `CheckAll` / `CheckAllAsync` | Throws `BusinessRuleValidationException` | Nothing |
| `EvaluateAll` / `EvaluateAllAsync` | Never throws | `RuleEvaluationResult` / `AsyncRuleEvaluationResult` |

You can pass the rules as a list or one by one:

```csharp
// order and context come from your application
BusinessRuleChecker.CheckAll(context, new OrderMustHaveItemsRule(order), new ShippingAddressRequiredRule(order));
```

To stop at the first broken rule, pass `stopOnFirstFailure: true`. The remaining rules are not evaluated.

```csharp
// rules and context are from the first example in this section
BusinessRuleChecker.CheckAll(rules, stopOnFirstFailure: true, context: context);
```

The async checker works the same way. It takes the context first and accepts a cancellation token. It can also run the rules in parallel, which helps when rules call a database or a remote service.

```csharp
// order, context, cancellationToken, inventoryService and paymentService come from your application
List<IAsyncBusinessRule> asyncRules = new List<IAsyncBusinessRule>
{
    new ItemsInStockRule(order, inventoryService),
    new PaymentMethodValidRule(order, paymentService)
};

await AsyncBusinessRuleChecker.CheckAllAsync(context, asyncRules, cancellationToken: cancellationToken);

AsyncRuleEvaluationResult asyncResult = await AsyncBusinessRuleChecker.EvaluateAllAsync(
    context, asyncRules, runInParallel: true, cancellationToken: cancellationToken);
```

- An async call without a context runs the rules against a new, empty `BusinessRuleContext`.
- The async checker accepts only async rules. Convert a sync rule with `.ToAsync()` to run it in the same batch (see [Mixing sync and async rules](./conditional-and-cached-rules.md#mixing-sync-and-async-rules)).
- Subject-typed rules use the same methods on `BusinessRuleChecker.For(subject)` and `AsyncBusinessRuleChecker.For(subject)` (see [Subject-Typed Rules](./subject-typed-rules.md)).
- For fault handling, severity thresholds and observers, pass an options object instead of the boolean parameters (see [Options](#options)).

## Filtering by severity, category or tags

Use the filter methods to check only part of a rule set, for example only the payment rules on the payment step. Each method selects the matching rules and then behaves like `CheckAll`: it throws when any selected rule is broken.

```csharp
// rules, asyncRules, context and cancellationToken are from the examples in "Check all or evaluate all"
BusinessRuleChecker.CheckBySeverity(rules, RuleSeverity.Error);
BusinessRuleChecker.CheckByCategory(rules, "Payment");
BusinessRuleChecker.CheckByTags(rules, "checkout", "pricing");

await AsyncBusinessRuleChecker.CheckBySeverityAsync(context, asyncRules, cancellationToken, RuleSeverity.Error);
await AsyncBusinessRuleChecker.CheckByCategoryAsync(context, asyncRules, "Payment", cancellationToken);
await AsyncBusinessRuleChecker.CheckByTagsAsync(context, asyncRules, cancellationToken, "checkout");
```

| Method | A rule is selected when |
|--------|-------------------------|
| `CheckBySeverity` | Its `Severity` is one of the listed values (an exact match, not "at or above") |
| `CheckByCategory` | Its `Category` equals the given value (case-sensitive) |
| `CheckByTags` | It has at least one of the given tags (case-insensitive) |

:::note

The sync filter methods take no context, so the selected rules run without one. If your rules read the context, filter the list yourself and call `CheckAll` with the context.

:::

To filter the outcomes of a batch instead of the rules, see [Reading the result](#reading-the-result).

## Reading the result

`EvaluateAll` and `EvaluateAllAsync` return a result that holds every broken rule. Use it to decide what to do next, or to build an error response.

```csharp
// rules and context come from your application
RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rules, context);

if (result.HasErrors)
{
    foreach (BusinessRuleResult error in result.Errors)
    {
        Console.WriteLine($"{error.Code}: {error.Message}");
    }
}
```

The sync and async results have the same members:

| Member | What it gives you |
|--------|-------------------|
| `BrokenRules` | Every broken result, in the order you submitted the rules |
| `PassedRules` | The rules that passed |
| `HasBrokenRules` / `AllPassed` | Whether any rule is broken |
| `Errors`, `Warnings`, `Infos` | Broken results at that severity |
| `HasErrors`, `HasWarnings` | Whether any broken result has that severity |
| `GetBySeverity(severity)` | Broken results at exactly that severity |
| `ByCategory(category)` | Broken results whose rule has that category (case-sensitive) |
| `ByTag(tag)` | Broken results whose rule has that tag (case-insensitive) |
| `ShadowResults`, `HasShadowResults` | What shadow-mode rules would have reported; never counted as broken (see [Seeing what shadow rules would have blocked](./feature-flags.md#seeing-what-shadow-rules-would-have-blocked)) |

When you are ready to stop the operation, turn the result into an exception:

| Method | Throws when |
|--------|-------------|
| `ThrowIfBroken()` | Any rule is broken |
| `ThrowIfErrors()` | An `Error` rule is broken |
| `ThrowIfAtLeast(severity)` | A rule at or above `severity` is broken |

All three throw `BusinessRuleValidationException`. You get the same exception from `CheckAll`, so one `catch` block covers both styles:

```csharp
// rules and context come from your application
try
{
    BusinessRuleChecker.CheckAll(rules, context: context);
}
catch (BusinessRuleValidationException exception)
{
    foreach (BusinessRuleResult broken in exception.BrokenRules)
    {
        Console.WriteLine($"{broken.Code}: {broken.Message}");
    }
}
```

| Exception member | What it gives you |
|------------------|-------------------|
| `BrokenRules` | The broken results that caused the throw |
| `CollectedRules` | Broken results below the severity threshold (see [Warnings that do not block](#warnings-that-do-not-block)) |
| `Entries` | The evaluation ledger of the batch (see [The evaluation ledger](#the-evaluation-ledger)) |
| `ByCategory(category)`, `ByTag(tag)` | Filters over `BrokenRules` |
| `Flatten()` | The leaf results, with the members of groups and other combined rules expanded |

:::note

`AllPassed` means "no broken results". A rule that faulted under `FaultPolicy.Record`, or was skipped because `StopOnFirstFailure` stopped the batch, is neither broken nor passed. It does not appear in `PassedRules`, and `AllPassed` can still be true. Check `HasFaults` as well when you record faults.

:::

## Options

Pass an options object when the boolean parameters are not enough. The options add a fault policy, a severity threshold and an observer. Use `RuleEvaluationOptions` with `BusinessRuleChecker` and `AsyncRuleEvaluationOptions` with `AsyncBusinessRuleChecker`.

```csharp
// rules, asyncRules, context and cancellationToken come from your application
RuleEvaluationOptions options = new RuleEvaluationOptions
{
    StopOnFirstFailure = true,
    OnFault = FaultPolicy.Record,
    ThrowOn = RuleSeverity.Error
};

BusinessRuleChecker.CheckAll(rules, context, options);

AsyncRuleEvaluationOptions asyncOptions = new AsyncRuleEvaluationOptions
{
    RunInParallel = true,
    OnFault = FaultPolicy.TreatAsBroken
};

AsyncRuleEvaluationResult asyncResult = await AsyncBusinessRuleChecker.EvaluateAllAsync(
    context, asyncRules, asyncOptions, cancellationToken);
```

| Option | Default | Sync | Async | Effect |
|--------|---------|:----:|:-----:|--------|
| `StopOnFirstFailure` | `false` | Yes | Yes | Stop at the first broken rule. The remaining rules are recorded as `NotEvaluated` |
| `OnFault` | `FaultPolicy.Throw` | Yes | Yes | What happens when a rule throws (see [When a rule throws](#when-a-rule-throws)) |
| `ThrowOn` | `RuleSeverity.Info` | Yes | Yes | The lowest severity that makes `CheckAll` throw. The default means every broken rule throws. `EvaluateAll` ignores it |
| `Observer` | `null` | Yes | Yes | Receives per-rule and per-batch callbacks (see [Observers](./logging-tracing-metrics.md#observers)) |
| `RunInParallel` | `false` | No | Yes | Evaluate the rules concurrently |

Keep these rules in mind:

- The argument order differs. Sync: `(rules, context, options)`. Async: `(context, rules, options, cancellationToken)`.
- In `CheckAll`, only a broken rule at or above `ThrowOn` stops a `StopOnFirstFailure` batch. A broken warning does not stop the search for an error. In `EvaluateAll`, any broken rule stops it.
- `StopOnFirstFailure` wins over `RunInParallel`. When both are true, the batch runs one rule at a time.
- In a parallel batch, the results are still in submission order.
- The `EvaluateAll` overloads without options have no observer. Pass options to get observer callbacks from `EvaluateAll`.
- Passing `null` options throws `ArgumentNullException`.

:::note

Parallel rules share one `BusinessRuleContext`, and the context is not thread-safe. Rules that run in parallel may read the context but should not write to it.

:::

## When a rule throws

A rule can throw: a database is down, a value is missing. `OnFault` decides whether that exception stops the batch or is recorded with the other outcomes.

```csharp
// rules and context come from your application
RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rules, context, new RuleEvaluationOptions
{
    OnFault = FaultPolicy.Record
});

foreach (RuleEvaluationEntry faulted in result.Faulted)
{
    Console.WriteLine($"{faulted.Code} threw {faulted.Fault?.GetType().Name}");
}
```

| `FaultPolicy` | Behaviour |
|---------------|-----------|
| `Throw` (default) | The exception leaves the checker. The batch stops and no result is returned |
| `Record` | The rule is recorded as `Faulted` with its exception. It counts as neither broken nor passed, and the batch continues |
| `TreatAsBroken` | Like `Record`, and the rule is also reported as broken with its own `Code`, `Message` and `Severity`. The fault blocks like any other violation |

- Cancellation (`OperationCanceledException`) always propagates, whatever the policy.
- There is no "treat as passed" policy. A fault never turns into a pass.
- Under `Record`, `CheckAll` does not throw because of a fault. Use `EvaluateAll` and check `HasFaults`, or watch faults with an observer.
- Under `TreatAsBroken`, if reading the rule's `Code` or `Message` also throws, the broken result uses the rule's type name as its code and `Error` as its severity.
- The observer's `OnRuleFaulted` callback runs under every policy.

:::note

Inside a batch, a rule that fails to read the context (a missing key or a value of the wrong type) is reported as a `BusinessRuleContextException` that names the rule's `Code`. The original exception is its `InnerException`. Catch `BusinessRuleContextException`, not `ContextKeyNotFoundException`, around a checker call. The context exceptions are listed in [Passing data with a context](./writing-rules.md#passing-data-with-a-context).

:::

## Warnings that do not block

Not every broken rule should stop the operation. A missing phone number might deserve a warning, while an exceeded credit limit must block the order. Set a severity threshold: rules below it are reported but do not throw.

```csharp
// order, customer and context come from your application
IBusinessRule phoneRule = new PhoneNumberRequiredRule(customer).WithSeverity(RuleSeverity.Warning);

List<IBusinessRule> checkoutRules = new List<IBusinessRule>
{
    phoneRule,
    new OrderTotalWithinCreditLimitRule(order, customer)
};

RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(checkoutRules, context);

foreach (BusinessRuleResult warning in result.Warnings)
{
    Console.WriteLine($"Warning {warning.Code}: {warning.Message}");
}

result.ThrowIfAtLeast(RuleSeverity.Error);   // only the credit-limit rule can throw
```

Severities are ordered `Info` < `Warning` < `Error`. Rules built on `BaseBusinessRule` or `BaseAsyncBusinessRule` default to `Error`. A threshold throws for broken rules at or above it.

You can apply a threshold in three places:

| Where | How |
|-------|-----|
| A batch | `CheckAll` / `CheckAllAsync` with `ThrowOn` in the options |
| A result | `result.ThrowIfAtLeast(severity)` (or `ThrowIfErrors()` for `Error`) |
| A single rule | `rule.Check(throwOn)` or `rule.Check(context, throwOn)`. Async: `await rule.CheckAsync(context, throwOn, cancellationToken)`, which always takes a context. |

When a threshold throws, the exception's `BrokenRules` holds the results at or above it and `CollectedRules` holds the broken results below it:

```csharp
// checkoutRules and context are from the previous example
try
{
    BusinessRuleChecker.CheckAll(checkoutRules, context, new RuleEvaluationOptions { ThrowOn = RuleSeverity.Error });
}
catch (BusinessRuleValidationException exception)
{
    IReadOnlyCollection<BusinessRuleResult> errors = exception.BrokenRules;       // Error
    IReadOnlyCollection<BusinessRuleResult> warnings = exception.CollectedRules;  // Info and Warning
}
```

:::note

When `CheckAll` does not throw, the broken warnings are not returned anywhere. If you need them, use `EvaluateAll` and then `ThrowIfAtLeast`, as in the first example.

:::

**Changing a rule's severity.** `rule.WithSeverity(RuleSeverity.Warning)` reports a rule at a new severity without subclassing it.

- The verdict, `Code`, `Message` and other metadata stay the same.
- Filters and thresholds see the new severity.
- It works on sync and async rules. In an explain tree it appears as a `SEVERITY` node.

## The evaluation ledger

Every checker call records a ledger: one entry per submitted rule, in submission order. Use it to see what ran, how long each rule took, and which rules threw.

```csharp
// rules and context come from your application
RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rules, context, new RuleEvaluationOptions
{
    OnFault = FaultPolicy.Record
});

foreach (RuleEvaluationEntry entry in result.Entries)
{
    Console.WriteLine($"{entry.Order}: {entry.Code} {entry.Outcome} in {entry.Duration.TotalMilliseconds:F2} ms");
}

RuleEvaluationEntry? slowest = result.Slowest;
Console.WriteLine($"Slowest rule: {slowest?.Code}; batch took {result.TotalDuration.TotalMilliseconds:F2} ms");
```

On the result:

| Member | What it gives you |
|--------|-------------------|
| `Entries` | One entry per submitted rule, in submission order |
| `Faulted` / `HasFaults` | The entries of rules that threw |
| `TotalDuration` | Wall-clock time of the whole batch |
| `Slowest` | The entry with the longest duration (the earliest one on a tie), or `null` for an empty batch |

On each entry:

| Member | What it gives you |
|--------|-------------------|
| `Order` | The rule's position in the batch, starting at 0 |
| `Code` | The rule's code |
| `Outcome` | `Passed`, `Broken`, `Faulted`, or `NotEvaluated` (skipped after `StopOnFirstFailure` stopped the batch) |
| `Result` | The broken result, or `null` |
| `Fault` | The exception the rule threw, or `null` |
| `Duration` | Time spent on the rule; zero when it was not evaluated |
| `IsBroken` / `IsFaulted` | Whether the entry has a `Result` / a `Fault` |
| `Rule` / `AsyncRule` | The rule itself (one of the two is set, depending on the checker) |

- A `BusinessRuleValidationException` thrown by `CheckAll` or `ThrowIfAtLeast` carries the same ledger in `exception.Entries`.
- Under `FaultPolicy.TreatAsBroken`, a faulted entry has the outcome `Faulted` but also a `Result`, so `IsBroken` is true.
- Under the default `FaultPolicy.Throw`, a fault stops the batch, so there is no ledger to read.
- In a parallel batch the rule durations overlap, so they can add up to more than `TotalDuration`.
