---
id: combining-rules
title: "Combining Rules"
description: "Combine rules with And, Or and Not, with All / Any / AtLeast / Exactly groups, and with composites."
---

Real validation is rarely a single check. You can build one rule out of several in three ways: the `And`, `Or` and `Not` operators, rule groups, and composite rules. A combined rule is itself a rule, so you can check it, nest it or pass it to a checker like any other rule.

## And, Or and Not

The operators join two rules into one. Use them for small, two-part conditions.

```csharp
// customer comes from your application
IBusinessRule eligibility = new CustomerMustBeAdultRule(customer)
    .Or(new CustomerMustBeActiveRule(customer));

eligibility.Check(); // throws when the customer is under age, inactive, or both
```

The operators combine the *broken* state of their operands, not the "passes" state. This is easy to get backwards, so check the table:

| `first` | `second` | `first.And(second)` | `first.Or(second)` |
|---------|----------|---------------------|--------------------|
| passes  | passes   | passes              | passes             |
| passes  | broken   | passes              | **broken**         |
| broken  | passes   | passes              | **broken**         |
| broken  | broken   | **broken**          | **broken**         |

`rule.Not()` is broken when `rule` passes, and passes when `rule` is broken.

:::note

`And` is broken **only when both** operands are broken. `a.And(b)` does **not** mean "a and b must both pass". For "every rule must pass", use `Or`, or better, `RuleGroup.All` (see [Rule groups](#rule-groups)), which says what it means.

:::

In practice:

- Use `Or` when **both** checks must hold. It is broken as soon as either one fails.
- Use `And` when **one** passing check is enough. It is broken only when both fail.
- Use `Not` to invert a rule.

```csharp
// The customer needs at least one payment method.
// Each rule is broken when that payment method is missing.
IBusinessRule paymentMethod = new CreditCardOnFileRule(customer)
    .And(new BankAccountOnFileRule(customer));

// Broken when the customer is active, passes when the customer is inactive
IBusinessRule mustBeInactive = new CustomerMustBeActiveRule(customer).Not();
```

What else you should know:

- You can chain and nest the operators: `a.Or(b).And(c.Not())`.
- The operators work on sync rules, async rules and subject-typed rules, with the same truth table.
- To combine a sync rule with an async rule, convert the sync one first: `asyncRule.Or(syncRule.ToAsync())`.
- The combined rule passes your context (and, on async rules, the cancellation token) to both operands.
- Evaluation stops early. `Or` skips the second operand when the first is broken. `And` skips it when the first passes.
- The combined rule gets a generated code: `A+B` for `And`, `A|B` for `Or` and `!A` for `Not`.
- Its severity is always `Error`, and it has no category or tags. Wrap it to change the severity: `eligibility.WithSeverity(RuleSeverity.Warning)`.
- When an `And` or `Or` is broken, its result lists the broken operands in `Details`. See "Reading member failures" in [Rule groups](#rule-groups).

## Rule groups

A rule group combines any number of rules and says how many of them must pass. Use a group when you have more than two rules, or when the requirement is "all of", "any of" or "at least n of".

```csharp
// order and customer come from your application
GroupBusinessRule checkout = RuleGroup.All(
    "CHECKOUT.INVALID",
    "The order cannot be placed.",
    new OrderMustHaveItemsRule(order),
    new ShippingAddressRequiredRule(order),
    new CustomerMustBeActiveRule(customer));

checkout.Check(); // throws when any member is broken
```

There are four kinds of group:

| Factory | Passes when | Broken when |
|---------|-------------|-------------|
| `RuleGroup.All(code, message, members)` | every member passes | any member is broken |
| `RuleGroup.Any(code, message, members)` | at least one member passes | every member is broken |
| `RuleGroup.AtLeast(n, code, message, members)` | `n` or more members pass | fewer than `n` members pass |
| `RuleGroup.Exactly(n, code, message, members)` | exactly `n` members pass | any other number of members pass |

- Pass the members as separate arguments or as any `IEnumerable<IBusinessRule>`.
- For async rules, use `AsyncRuleGroup` with the same four factories.
- The factories validate their input:
  - `ArgumentNullException` when the code, the message or the member list is null.
  - `ArgumentException` when the member list is empty or contains a null rule.
  - `ArgumentOutOfRangeException` when `n` is negative or larger than the number of members.

**Options.** Groups are immutable. Each option returns a new group, so keep the return value.

| Method | Available on | What it does |
|--------|--------------|--------------|
| `WithShortCircuit()` | sync and async | Stops as soon as the verdict is known. The remaining members do not run, so they are not in the result's `Details`. An explain tree shows them as `NotEvaluated` with the reason `short-circuit`. |
| `WithSeverity(severity)` | sync and async | Sets the group's severity. The default is `Error`. |
| `WithParallelEvaluation()` | async only | Starts every member at once and waits for all of them to finish. |

By default a group runs every member, so its result lists every broken member.

```csharp
// The three async rules, context and cancellationToken come from your application
GroupAsyncBusinessRule registration = AsyncRuleGroup.All(
        "REGISTRATION.INVALID",
        "The registration cannot be completed.",
        emailMustBeUnique,
        usernameMustBeUnique,
        domainMustBeAllowed)
    .WithParallelEvaluation();

await registration.CheckAsync(context, cancellationToken);
```

:::note

A group cannot be both parallel and short-circuiting. Calling `WithShortCircuit()` on a parallel group, or `WithParallelEvaluation()` on a short-circuiting one, throws `InvalidOperationException`.

:::

:::note

The members of a parallel group share one `BusinessRuleContext`, and the context is not thread-safe. They may read from it but should not write to it.

:::

**Reading member failures.** A broken group reports one result with the group's own code and message. The broken members are in that result's `Details`. A group nested inside another group nests its details too. Call `Flatten()` to get only the individual broken rules.

```csharp
// checkout is the group from above; context comes from your application
try
{
    BusinessRuleChecker.CheckAll(context, checkout);
}
catch (BusinessRuleValidationException exception)
{
    foreach (BusinessRuleResult group in exception.BrokenRules)
    {
        Console.WriteLine($"{group.Code}: {group.Details.Count} member(s) broken");
    }

    // The individual broken rules, for example ORDER.NO_ITEMS and CUSTOMER.INACTIVE
    foreach (BusinessRuleResult failure in exception.Flatten())
    {
        Console.WriteLine($"{failure.Code}: {failure.Message}");
    }
}
```

- `exception.Flatten()` flattens every broken result. `result.Flatten()` flattens one result.
- The same applies to `And` and `Or` results, and to `EvaluateAll` results.
- To build a result with members yourself, for example in a custom rule, use `new BusinessRuleResult(code, message, severity, memberResults)`.

:::note

The sync `Evaluate()` on a group, or on an `And`, `Or` or `Not`, does not run the members. It only builds the group's own result, with empty `Details`. To get the member failures, use `Check`, the checkers (`CheckAll`, `EvaluateAll`) or, on async rules, `EvaluateAsync`.

:::

:::info[Advanced]

Every combined rule exposes its structure for your own tooling. `And`, `Or` and `Not` nodes, groups and composites implement `IComposedBusinessRule` (`Operator`, `Operands`). The cached, conditional, severity-override and feature-gated wrappers implement `IDecoratedBusinessRule` (`InnerRule`). Async rules have `IComposedAsyncBusinessRule` and `IDecoratedAsyncBusinessRule`.

:::

## Composite rules

A composite bundles a list of rules into one rule that is broken when any of them is broken. It always runs every rule, so you get every failure in one call.

```csharp
// order comes from your application
CompositeBusinessRule orderRules = new CompositeBusinessRule(new IBusinessRule[]
{
    new OrderMustHaveItemsRule(order),
    new ShippingAddressRequiredRule(order),
    new OrderTotalWithinLimitRule(order),
});

IReadOnlyCollection<BusinessRuleResult> failures = orderRules.EvaluateAll();
foreach (BusinessRuleResult failure in failures)
{
    Console.WriteLine($"{failure.Code}: {failure.Message}");
}

orderRules.Check(); // throws, listing every broken rule in BrokenRules
```

- `EvaluateAll()` and `EvaluateAll(context)` return the results of the broken rules. The collection is empty when every rule passes.
- `Check()` and `Check(context)` throw `BusinessRuleValidationException`. Its `BrokenRules` holds each broken rule directly.
- A composite with no rules never breaks.
- The composite's code is fixed (`CompositeRule`), its severity is `Error` and it never short-circuits. If you need your own code, message or severity, use `RuleGroup.All` instead. It is broken under the same condition.

For async rules, use `CompositeAsyncBusinessRule` and `EvaluateAllAsync(context, cancellationToken)`.

:::note

The async composite reports failures differently from the sync one. `CheckAsync` throws with a single result, code `CompositeAsyncRule`, whose `Details` holds the broken rules. Call `exception.Flatten()` to get each rule.

:::

:::note

`CompositeAsyncBusinessRule` also has a constructor that builds a single rule from a delegate: `new CompositeAsyncBusinessRule(isBrokenAsync, message, code)`. The message comes **before** the code in that constructor.

:::
