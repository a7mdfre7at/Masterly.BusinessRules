---
id: subject-typed-rules
title: "Subject-Typed Rules"
description: "Stateless rules that receive their subject at evaluation time: one instance validates every order."
---

A subject-typed rule receives the object it validates (the *subject*) when you evaluate it, not in its constructor. One instance can therefore validate every order in your system. This makes the rule a good fit for dependency injection: register it once as a singleton, inject its repositories, and reuse it.

```csharp
// The subject-typed version of the Quick Start rule
public sealed class OrderItemsRequiredRule : BaseBusinessRule<Order>
{
    public override string Code => "ORDER.NO_ITEMS";
    public override string Message => "Order must contain at least one item.";

    public override bool IsBroken(Order order) => order.Items.Count == 0;
}

// order comes from your application
IBusinessRule<Order> rule = new OrderItemsRequiredRule();
bool isBroken = rule.IsBroken(order);
```

**Base classes.** Derive from `BaseBusinessRule<T>` for sync rules and `BaseAsyncBusinessRule<T>` for async rules. Value types work as subjects too.

| Base class | Override | Optional overrides |
|------------|----------|--------------------|
| `BaseBusinessRule<T>` | `Code`, `Message`, `IsBroken(subject)` | `IsBroken(subject, context)`, which calls `IsBroken(subject)` by default |
| `BaseAsyncBusinessRule<T>` | `Code`, `Message`, `IsBrokenAsync(subject, context, ct)` | None |

Both base classes default `Severity` to `Error`, `Name` to the class name, and `Description`, `Category` and `Tags` to empty. Override `Describe(subject, context)` to attach details to a broken result (see [Rich results](./understanding-failures.md#rich-results)).

An async rule with an injected dependency looks like this:

```csharp
public sealed class CustomerEmailMustBeUniqueRule : BaseAsyncBusinessRule<Customer>
{
    private readonly ICustomerRepository _customers;

    public CustomerEmailMustBeUniqueRule(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public override string Code => "CUSTOMER.EMAIL_DUPLICATE";
    public override string Message => "Email address is already in use.";

    public override Task<bool> IsBrokenAsync(Customer customer, BusinessRuleContext context, CancellationToken cancellationToken = default)
    {
        return _customers.EmailExistsAsync(customer.Email, cancellationToken);
    }
}
```

The DependencyInjection package can find and register these rules for you (see [Dependency Injection](./dependency-injection.md)).

**Binding a subject with `.For(subject)`.** A typed rule is not an `IBusinessRule`. Call `.For(subject)` to get an ordinary `IBusinessRule` (or `IAsyncBusinessRule`) bound to that subject. The bound rule keeps the rule's code, message and other metadata. It works with every wrapper, operator, checker and `Explain`.

```csharp
// order and context come from your application
IBusinessRule bound = new OrderItemsRequiredRule().For(order);

bound.Check(context);
IBusinessRule cached = bound.Cached(TimeSpan.FromMinutes(1));
RuleExplanation explanation = bound.Explain(context);
```

:::note

`.For(order)` returns a new rule each time. A cache added with `.Cached(...)` belongs to that bound instance, so it caches the verdict for that one subject.

:::

**Checking a subject directly.** You do not have to bind rules yourself. `BusinessRuleChecker.For(subject)` and `AsyncBusinessRuleChecker.For(subject)` return a checker bound to that subject. Its methods take a list of typed rules and bind them for you. Options, fault policy, severity threshold and observers work exactly as in the untyped overloads.

```csharp
// order and context come from your application; OrderTotalMustBePositiveRule is another BaseBusinessRule<Order>
IBusinessRule<Order>[] rules = { new OrderItemsRequiredRule(), new OrderTotalMustBePositiveRule() };

BusinessRuleChecker.For(order).CheckAll(rules, context); // throws when any rule is broken
RuleEvaluationResult result = BusinessRuleChecker.For(order).EvaluateAll(rules);

// Rules of different classes can be passed directly
BusinessRuleChecker.For(order).CheckAll(new OrderItemsRequiredRule(), new OrderTotalMustBePositiveRule());
```

| You want to | Sync | Async |
|-------------|------|-------|
| Check and throw | `For(order).CheckAll(rules, context)` | `For(order).CheckAllAsync(context, rules, cancellationToken)` |
| Get the result | `For(order).EvaluateAll(rules, context)` | `For(order).EvaluateAllAsync(context, rules, cancellationToken)` |
| Check many subjects | `EvaluateEach(orders, rules, context)` | `EvaluateEachAsync(context, orders, rules, cancellationToken)` |

The context and the options are optional in every call. `CheckBySeverity`, `CheckByCategory` and `CheckByTags` are on `For(subject)` too.

- The sync overloads take the context after the rules. The async overloads take it first.
- `CheckByTags` runs the rules that have at least one of the tags. Tag matching ignores case. `CheckByCategory` matches the category exactly.
- A `null` entry in the rule list throws `ArgumentException`.

```csharp
// customer, customerRepository, context and cancellationToken come from your application
IAsyncBusinessRule<Customer>[] customerRules = { new CustomerEmailMustBeUniqueRule(customerRepository) };

await AsyncBusinessRuleChecker.For(customer).CheckAllAsync(context, customerRules, cancellationToken);
```

:::note

Subject-typed batches live behind `For(subject)` on purpose. A 2.x-style call that passes a positional `null` or `default` after the rules, such as `EvaluateAll(rules, null)`, still means the context, the flag or the cancellation token, exactly as in 2.x.

:::

**Validating many subjects with `EvaluateEach`.** Use `EvaluateEach` to run one rule set against a list of subjects, such as every line of an import file. Each result carries the position of its subject.

```csharp
// orders comes from your application; rules is the array from the previous example
SubjectEvaluationCollection<Order> evaluations = BusinessRuleChecker.EvaluateEach(orders, rules);

if (evaluations.HasBrokenRules)
{
    foreach (SubjectEvaluation<Order> evaluation in evaluations.Broken)
    {
        Console.WriteLine($"Order #{evaluation.Index}: {evaluation.BrokenRules.Count} broken rule(s)");
    }
}
```

- Each subject gets its own batch. `Index` is the subject's position in the input, starting at 0.
- The collection offers `AllPassed`, `HasBrokenRules`, `Broken`, `Passed`, `BrokenRules` (across all subjects) and `TotalDuration`.
- `EvaluateEach` reports broken rules. It does not throw for them, so the `ThrowOn` option is ignored.
- `StopOnFirstFailure` stops the current subject's batch only. The next subject is still evaluated.
- A context you pass is shared by every subject's batch.
- `EvaluateEachAsync` evaluates the subjects one after another and returns an `AsyncSubjectEvaluationCollection<T>`.

**Typed builders.** `BusinessRuleBuilder<T>` and `AsyncBusinessRuleBuilder<T>` build typed rules inline. They have the same metadata methods as the untyped builders. Their conditions and detail functions also receive the subject.

```csharp
IBusinessRule<Order> tooManyItemsRule = BusinessRuleBuilder<Order>.Create("ORDER.TOO_MANY_ITEMS")
    .WithMessage("Order has too many items.")
    .WithActual((o, ctx) => o.Items.Count)
    .WithExpected(100)
    .When(o => o.Items.Count > 100)
    .Build();

// orderRepository comes from your application
IAsyncBusinessRule<Order> duplicateReferenceRule = AsyncBusinessRuleBuilder<Order>.Create("ORDER.DUPLICATE")
    .WithMessage("An order with this reference already exists.")
    .WhenAsync((o, ctx, ct) => orderRepository.ReferenceExistsAsync(o.Reference, ct))
    .Build();
```

- A subject-aware detail function, such as `WithActual((o, ctx) => ...)`, wins over a context-only one set on the same builder.
- `Build()` throws `InvalidOperationException` when no condition is set.
- `AsyncBusinessRuleBuilder<T>` keeps only the last `WhenAsync` call.
- The typed builders have no `WithRuleFactory` and no text-condition `When`.

:::note

Unlike the untyped builder, `BusinessRuleBuilder<T>` keeps a subject-only condition and a context condition at the same time:
- `IsBroken(order)` uses `When(o => ...)`.
- `IsBroken(order, context)` uses `When((o, ctx) => ...)`.
- If you set only one of them, both calls use it.
- A parameters condition `When((o, ctx, p) => ...)` replaces both.

:::

**Combining typed rules.** `And`, `Or` and `Not` work on typed rules and return typed rules. The combined rule passes the subject and the context to both operands. A typed sync rule can join async combined rules and batches through `.ToAsync()`.

```csharp
// order and context come from your application; duplicateReferenceRule is from the previous example
IBusinessRule<Order> highValueRule = BusinessRuleBuilder<Order>.Create("ORDER.HIGH_VALUE")
    .WithMessage("Order total is above 5,000.")
    .When(o => o.Total > 5000m)
    .Build();

IBusinessRule<Order> firstOrderRule = BusinessRuleBuilder<Order>.Create("ORDER.FIRST_ORDER")
    .WithMessage("Customer has no previous orders.")
    .When(o => o.Customer.OrderCount == 0)
    .Build();

// Broken only when both are broken: a high-value first order needs review
IBusinessRule<Order> needsReviewRule = highValueRule.And(firstOrderRule);
bool needsReview = needsReviewRule.IsBroken(order, context);

// Combine a sync typed rule with an async typed rule
IAsyncBusinessRule<Order> holdOrderRule = duplicateReferenceRule.Or(needsReviewRule.ToAsync());
```

| Operator | Broken when | Code |
|----------|-------------|------|
| `a.And(b)` | Both `a` and `b` are broken | `A+B` |
| `a.Or(b)` | `a` or `b` is broken | `A\|B` |
| `a.Not()` | `a` passes | `!A` |

`And` and `Or` stop early: they skip the second operand when the first one already decides the result. They follow the same truth table as the untyped operators in [And, Or and Not](./combining-rules.md#and-or-and-not).

:::note

A typed combination is a single rule. In an `Explain` tree it shows as one node, and its result does not list which operand failed. If you need that detail, bind first and combine the bound rules: `highValueRule.For(order).And(firstOrderRule.For(order))`.

:::
