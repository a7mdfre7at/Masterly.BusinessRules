---
id: building-rules
title: "Building Rules Without Classes"
sidebar_label: "Building Rules"
---

Builders let you create a rule inline, without writing a class. Use them for small one-off rules, rules assembled from configuration at run time, or quick prototypes.

There are two builders. `BusinessRuleBuilder` creates synchronous rules and `AsyncBusinessRuleBuilder` creates asynchronous ones. Both share the same metadata methods.

## Sync builder

Start with `Create(code)`, describe the rule, set the condition with `When(...)` and call `Build()`. The condition returns `true` when the rule is **broken**.

```csharp
// order comes from your application
IBusinessRule minimumTotalRule = BusinessRuleBuilder.Create("ORDER.BELOW_MINIMUM")
    .WithMessage("Order total must be at least 10.00.")
    .When(() => order.Total < 10.00m)
    .Build();

minimumTotalRule.Check(); // throws BusinessRuleValidationException when broken
```

You can set every metadata field on the builder:

```csharp
// order comes from your application
IBusinessRule minimumTotalRule = BusinessRuleBuilder.Create("ORDER.BELOW_MINIMUM")
    .WithName("Minimum order total")
    .WithMessage("Order total must be at least 10.00.")
    .WithDescription("Rejects orders below the minimum checkout amount.")
    .WithSeverity(RuleSeverity.Error)
    .WithCategory("Checkout")
    .WithTags("orders", "checkout")
    .When(() => order.Total < 10.00m)
    .Build();
```

If you leave a field out, the built rule uses these defaults:

| Field | Default |
|-------|---------|
| `Name` | The rule code |
| `Message` | `"Business rule violated."` |
| `Severity` | `RuleSeverity.Error` |
| `Description`, `Category` | Empty string |
| `Tags` | Empty. Each `WithTags` call adds to the list. |

To read values at evaluation time, use the overload that receives the `BusinessRuleContext`:

```csharp
// order and customer come from your application
IBusinessRule creditLimitRule = BusinessRuleBuilder.Create("CREDIT.LIMIT_EXCEEDED")
    .WithMessage("Order total exceeds the customer's credit limit.")
    .When(ctx => ctx.Get<decimal>("orderTotal") > ctx.Get<decimal>("creditLimit"))
    .Build();

BusinessRuleContext context = new BusinessRuleContext();
context.Set("orderTotal", order.Total);
context.Set("creditLimit", customer.CreditLimit);

bool isBroken = creditLimitRule.IsBroken(context);
```

`When` has three overloads:

| Condition | Use it when |
|-----------|-------------|
| `When(() => ...)` | The values are captured in the lambda. |
| `When(ctx => ...)` | The values come from the context. |
| `When((ctx, parameters) => ...)` | The rule also reads scoped parameters (see [Externalized parameters](./per-tenant-rules.md#externalized-parameters)). |

Rules for the builder:

- **One condition per rule.** Each `When` call replaces the previous one. `When((ctx, p) => true).When(ctx => false)` builds a rule that never breaks.
- **A condition is required.** `Build()` throws `InvalidOperationException` if you never called `When`.
- **Details are optional.** `WithDetail`, `WithActual`, `WithExpected` and `WithRemediation` add details to the result. They run only when the rule is broken. If one of them throws, the result has no details, but the rule still reports as broken. See [Rich results](./understanding-failures.md#rich-results).

:::note

A rule built with `When(ctx => ...)` still works when you call `IsBroken()` or `Check()` without a context. It then receives an empty context, so `ctx.Get<T>(...)` throws `ContextKeyNotFoundException`. Pass a context, or use `ctx.TryGet` in the condition.

:::

## Async builder

Use `AsyncBusinessRuleBuilder` when the condition needs I/O, such as a database or HTTP call. The condition returns a `Task<bool>` and can receive a cancellation token.

```csharp
// order, orderRepository and cancellationToken come from your application
IAsyncBusinessRule duplicateOrderRule = AsyncBusinessRuleBuilder.Create("ORDER.DUPLICATE")
    .WithMessage("An order with this reference already exists.")
    .WithCategory("Checkout")
    .WhenAsync((ctx, ct) => orderRepository.ReferenceExistsAsync(order.Reference, ct))
    .Build();

BusinessRuleContext context = new BusinessRuleContext();
await duplicateOrderRule.CheckAsync(context, cancellationToken);
```

`WhenAsync` has three overloads:

| Condition | Use it when |
|-----------|-------------|
| `WhenAsync(ctx => ...)` | You do not need the cancellation token. |
| `WhenAsync((ctx, ct) => ...)` | You pass the token to your I/O calls. |
| `WhenAsync((ctx, parameters, ct) => ...)` | The rule also reads scoped parameters. |

The async builder follows the same rules as the sync builder, with these differences:

- The condition always receives a context. There is no parameterless form.
- The default message is `"Async business rule violated."`.
- `EvaluateAsync` returns `null` when the rule passes.
- `WithParameters` also accepts an `IAsyncRuleParameterProvider`.

:::info[Advanced]

Package authors can plug in a text condition with `When(compiler, text)` / `WhenAsync(compiler, text)`, or choose the rule type that `Build()` returns with `WithRuleFactory(...)`. The Expressions package uses these for `WhenExpression` (see [Rule Expressions](./rule-expressions.md)).

:::
