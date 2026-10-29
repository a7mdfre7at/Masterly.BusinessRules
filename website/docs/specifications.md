---
id: specifications
title: "Specifications and Database Queries"
sidebar_label: "Specifications"
---

A specification is a subject-typed rule whose condition is an expression tree. You write the condition once and use it in two places: to check one object in memory, and as a `Where` clause that finds every row breaking the rule in your database. Use one when the rule looks only at the row itself and you also want to query for the rows that break it, such as "overdue orders".

```csharp
using System;
using System.Linq;
using System.Linq.Expressions;
using Masterly.BusinessRules;

public sealed class OrderIsOverdueRule : SpecificationBusinessRule<Order>
{
    public override string Code => "ORDER.OVERDUE";

    public override string Message => "Order is past its due date.";

    public override Expression<Func<Order, bool>> BrokenWhen => order => order.DueDate < DateTime.UtcNow && !order.IsPaid;
}
```

```csharp
// order comes from your application; db is your EF Core DbContext
OrderIsOverdueRule overdue = new OrderIsOverdueRule();

bool isOverdue = overdue.IsBroken(order);                            // in memory
IQueryable<Order> overdueOrders = db.Orders.WhereBroken(overdue);    // WHERE DueDate < now AND NOT IsPaid
IQueryable<Order> onTimeOrders = db.Orders.WhereSatisfies(overdue);  // WHERE NOT (DueDate < now AND NOT IsPaid)
```

How a specification behaves:

- You override `BrokenWhen` instead of `IsBroken`. Like every rule, the condition is true when the rule is **broken**.
- In memory, the expression is compiled on first use and the compiled delegate is reused.
- `Code`, `Message` and `Severity` work as on any other rule, so a specification can go straight into `BusinessRuleChecker.For(order).CheckAll(overdue)`.
- `WhereBroken` returns the rows that break the rule. `WhereSatisfies` returns the rows that pass it.
- If you need the raw predicates, read `BrokenExpression` or `SatisfiedExpression`.
- If `BrokenWhen` returns `null`, the first use throws `RuleConfigurationException`.

:::note

A specification ignores the `BusinessRuleContext`. Both `IsBroken` overloads are sealed so the in-memory verdict always matches the database query. Everything the rule needs must come from the subject or from the expression itself.

:::

**Combining specifications.** Specifications have their own combinators. They are named by when the combination is *satisfied*, as in the classic Specification pattern:

| Combinator | Satisfied when | Broken when | Code |
|------------|----------------|-------------|------|
| `a.AllOf(b, c)` | every operand is satisfied | any operand is broken | `ALL(A,B,C)` |
| `a.AnyOf(b, c)` | at least one operand is satisfied | every operand is broken | `ANY(A,B,C)` |
| `a.Not()` | `a` is broken | `a` is satisfied | `!A` |

```csharp
using Masterly.BusinessRules;
using Microsoft.EntityFrameworkCore; // for ToListAsync

// overdue and db (your EF Core DbContext) are from the previous example;
// OrderIsBigRule and OrderIsFlaggedRule are specifications like OrderIsOverdueRule;
// cancellationToken comes from your application
OrderIsBigRule big = new OrderIsBigRule();
OrderIsFlaggedRule flagged = new OrderIsFlaggedRule();

ISpecificationRule<Order> needsReview = overdue.AllOf(big).AnyOf(flagged);   // broken when (overdue OR big) AND flagged

List<Order> toReview = await db.Orders
    .WhereBroken(needsReview)
    .OrderBy(o => o.DueDate)
    .Take(50)
    .ToListAsync(cancellationToken);
```

A combinator still produces a single lambda over a single parameter, so any LINQ provider can translate it. A combined specification always has `Error` severity and an empty category. Its message joins the operands' messages.

**Keeping a query translatable.** `WhereBroken` and `WhereSatisfies` accept only specifications. You can use a specification like any other subject-typed rule, but these results are no longer specifications:

- Combining it with the general typed-rule `.And()`, `.Or()` or `.Not()`.
- Binding it to a subject with `.For(order)`, which you need for `.Cached()`, `.When()`, `.WithSeverity()` and `.GatedBy()`.

Those results still work in memory. Passing them to `WhereBroken` is a compile-time error, so a query can never silently lose its translation. To stay translatable, use `AllOf`, `AnyOf` and `Not`. When the variable's type is a specification, `.Not()` picks the specification version.

:::note

When a provider translates the query, values such as `DateTime.UtcNow` are usually evaluated by the database with its own clock. In memory, your server's clock is used. Keep this in mind for rules near a date boundary.

:::

**Async.** There is no async specification, because an expression tree has no async form. Call `.ToAsync()` to use a specification in async batches, groups and `AsyncBusinessRuleChecker` calls:

```csharp
// overdue is from the first example; order, context and cancellationToken come from your application
IAsyncBusinessRule<Order> asyncOverdue = overdue.ToAsync();

await AsyncBusinessRuleChecker.For(order).CheckAllAsync(context, new[] { asyncOverdue }, cancellationToken);
```

**Existence checks are not specifications.** "The customer must exist" or "the email must not already be taken" need a repository call, not a predicate over the row. Write them as an async builder rule:

```csharp
// customers is your repository; asyncOverdue is from the previous example;
// order, context and cancellationToken come from your application
IAsyncBusinessRule<Order> customerMustExist = AsyncBusinessRuleBuilder<Order>.Create("ORDER.CUSTOMER_MISSING")
    .WithMessage("Order references a customer that does not exist.")
    .WhenAsync(async (o, ctx, ct) => !await customers.ExistsAsync(o.CustomerId, ct))
    .Build();

await AsyncBusinessRuleChecker.For(order).CheckAllAsync(context, new[] { asyncOverdue, customerMustExist }, cancellationToken);
```
