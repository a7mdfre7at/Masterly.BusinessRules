# Masterly.BusinessRules

A clean, composable, and extensible business rule engine for .NET.

<img src="https://raw.githubusercontent.com/a7mdfre7at/Masterly.BusinessRules/master/repo_image.png" width="200" height="180">

[![Nuget](https://img.shields.io/nuget/v/Masterly.BusinessRules?style=flat-square)](https://www.nuget.org/packages/Masterly.BusinessRules) ![Nuget](https://img.shields.io/nuget/dt/Masterly.BusinessRules?style=flat-square) ![GitHub last commit](https://img.shields.io/github/last-commit/a7mdfre7at/Masterly.BusinessRules?style=flat-square) ![GitHub](https://img.shields.io/github/license/a7mdfre7at/Masterly.BusinessRules) [![Build](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/build.yml/badge.svg?branch=master)](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/build.yml) [![CodeQL Analysis](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/codeql.yml/badge.svg?branch=master)](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/codeql.yml) [![Publish to NuGet](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/publish.yml/badge.svg?branch=master)](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/publish.yml)


## Give a Star! :star:

If you like or are using this project please give it a star. Thanks!

---

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Creating Rules](#creating-rules)
- [Fluent Builders](#fluent-builders)
- [Subject-Typed Rules](#subject-typed-rules)
- [Rule Metadata](#rule-metadata)
- [Rule Composition](#rule-composition)
- [Structural Composition and Explain Trees](#structural-composition-and-explain-trees)
- [Batch Validation](#batch-validation)
- [Context and Data Passing](#context-and-data-passing)
- [Typed Context Keys](#typed-context-keys)
- [Caching](#caching)
- [Conditional Execution](#conditional-execution)
- [Rule Adapters](#rule-adapters)
- [Observers](#observers)
- [Evaluation Results](#evaluation-results)
- [Evaluation Ledger, Options and Fault Policy](#evaluation-ledger-options-and-fault-policy)
- [Rich Results](#rich-results)
- [Testing Utilities](#testing-utilities)
- [Real-World Examples](#real-world-examples)
- [API Reference](#api-reference)
- [License](#license)

---

## Features

- **Sync & Async Rules** - Full support for both synchronous and asynchronous business rules
- **Fluent Builders** - Create rules inline without subclassing
- **Subject-Typed Rules** - `IBusinessRule<T>` / `IAsyncBusinessRule<T>` receive their subject at evaluation time, so one stateless, DI-friendly instance validates any number of subjects; `.For(subject)` binds back to the non-generic world, the checkers take a subject directly (`CheckAll(order, rules)`), `EvaluateEach(orders, rules)` runs a rule set over many subjects, and typed `And`/`Or`/`Not` and `BusinessRuleBuilder<T>` complete the picture
- **Logical Composition** - Combine rules with `And()`, `Or()`, `Not()` (operators combine the *broken* state of their operands; see the pinned truth table). The operators are real tree nodes that keep their operands and forward the context
- **Rule Groups** - Unambiguous N-ary quantifiers `RuleGroup.All/Any/AtLeast/Exactly` with opt-in short-circuit and (async) parallel evaluation; member failures travel through `BusinessRuleResult.Details`
- **Explain Trees** - `rule.Explain(context)` / `await rule.ExplainAsync(context)` return a navigable, printable tree (Passed/Broken/Skipped/CacheHit/NotEvaluated/Faulted per node, with timing) produced by a single evaluation
- **Rule Metadata** - Name, description, category, tags, and severity
- **Caching** - Time-based caching for expensive rule evaluations
- **Conditional Execution** - Execute rules only when preconditions are met
- **Rule Adapters** - Convert between sync and async rules
- **Observers** - Monitor rule execution with per-rule and batch lifecycle callbacks (`OnBatchStarted`, `OnAfterEvaluate` with elapsed time, `OnRuleFaulted`, `OnBatchCompleted`) from both `CheckAll` and `EvaluateAll`
- **Batch Validation** - Check multiple rules with fail-fast and parallel execution, or pass a `RuleEvaluationOptions` / `AsyncRuleEvaluationOptions` object
- **Evaluation Ledger** - Every batch result carries one `RuleEvaluationEntry` per rule (order, outcome, duration, fault), `TotalDuration`, `Slowest` and `Faulted`
- **Fault Policy and Severity Threshold** - Decide whether a throwing rule aborts the batch, is recorded, or counts as broken (`FaultPolicy`); make `CheckAll` throw only at or above a severity (`ThrowOn`), and re-severity any rule with `.WithSeverity()`
- **Rich Results** - Every result carries the rule's `Name`, `Category` and `Tags`; a `Describe(context)` hook (or the builders' `WithDetail`/`WithActual`/`WithExpected`/`WithRemediation`) attaches `Detail`, `Actual`, `Expected`, `Remediation` and a `Data` bag on the broken path only, and exceptions and results filter with `ByTag`/`ByCategory`: enough to build an RFC 7807 Problem Details response straight from the engine
- **Typed Context** - Strongly-typed data passing to rules: `BusinessRuleContext<T>.Data` for one object, `ContextKey<T>` for named values read with `context.Get(OrderKeys.Order)` (no cast, no string literal); a failed read throws `ContextKeyNotFoundException` listing the available keys or `ContextValueTypeMismatchException` naming both types, and the checkers wrap either in a `BusinessRuleContextException` that names the failing rule
- **Testing Utilities** - Helper methods for unit testing rules

---

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package Masterly.BusinessRules
```

Or via Package Manager Console:

```powershell
Install-Package Masterly.BusinessRules
```

---

## Quick Start

```csharp
// 1. Define a rule
public class OrderMustHaveItemsRule(Order _order) : BaseBusinessRule
{
    public override string Code => "ORDER.NO_ITEMS";
    public override string Message => "Order must contain at least one item.";
    public override bool IsBroken() => !_order.Items.Any();
}

// 2. Check the rule
new OrderMustHaveItemsRule(order).Check();

// 3. Or use the batch checker
BusinessRuleChecker.CheckAll(
    new OrderMustHaveItemsRule(order),
    new CustomerMustBeActiveRule(order.Customer)
);
```

---

## Creating Rules

Business rules encapsulate validation logic in a reusable, testable unit. Each rule has:

- **Code**: A unique identifier for the rule (e.g., "ORDER.NO_ITEMS")
- **Message**: A human-readable description of why the rule is broken
- **IsBroken()**: The validation logic that returns `true` if the rule is violated

The library provides two base classes:
- `BaseBusinessRule` for synchronous rules
- `BaseAsyncBusinessRule` for rules that need async operations (database queries, API calls)

Rules can be checked in three ways:
- `IsBroken()` / `IsBrokenAsync()`: Returns a boolean indicating if the rule is violated
- `Check()` / `CheckAsync()`: Throws `BusinessRuleValidationException` if the rule is broken
- `Evaluate()` / `EvaluateAsync()`: Returns a `BusinessRuleResult` object (or null if not broken)

### Sync Rule Example

```csharp
public class CustomerMustBeActiveRule : BaseBusinessRule
{
    private readonly Customer _customer;

    public CustomerMustBeActiveRule(Customer customer)
    {
        _customer = customer;
    }

    public override string Code => "CUSTOMER.INACTIVE";
    public override string Message => "Customer must be active to place orders.";
    public override bool IsBroken() => !_customer.IsActive;
}

// Using primary constructor (C# 12+)
public class CustomerMustBeActiveRule(Customer _customer) : BaseBusinessRule
{
    public override string Code => "CUSTOMER.INACTIVE";
    public override string Message => "Customer must be active to place orders.";
    public override bool IsBroken() => !_customer.IsActive;
}

// Usage
CustomerMustBeActiveRule rule = new CustomerMustBeActiveRule(customer);

if (rule.IsBroken())
    Console.WriteLine($"Rule violated: {rule.Message}");

rule.Check(); // Throws BusinessRuleValidationException if broken

BusinessRuleResult result = rule.Evaluate();
```

### Async Rule Example

```csharp
public class EmailMustBeUniqueRule(IUserRepository _repository, string _email) : BaseAsyncBusinessRule
{
    public override string Code => "EMAIL.DUPLICATE";
    public override string Message => $"Email '{_email}' is already registered.";

    public override async Task<bool> IsBrokenAsync(
        BusinessRuleContext context,
        CancellationToken cancellationToken = default)
    {
        return await _repository.EmailExistsAsync(_email, cancellationToken);
    }
}

// Usage
EmailMustBeUniqueRule rule = new EmailMustBeUniqueRule(repository, "user@example.com");
BusinessRuleContext context = new BusinessRuleContext();

bool isBroken = await rule.IsBrokenAsync(context);
await rule.CheckAsync(context);
BusinessRuleResult? result = await rule.EvaluateAsync(context);
```

---

## Fluent Builders

Fluent builders let you create rules inline without defining a class. This is useful for:

- Simple, one-off validation rules
- Rules defined at runtime based on configuration
- Quick prototyping

The builders support all rule metadata (name, description, category, tags, severity) and provide a chainable API.

- `BusinessRuleBuilder`: Creates synchronous rules with `When()` condition
- `AsyncBusinessRuleBuilder`: Creates asynchronous rules with `WhenAsync()` condition

### Sync Builder Example

```csharp
// Simple rule
IBusinessRule ageRule = BusinessRuleBuilder.Create("AGE.INVALID")
    .WithMessage("Age must be 18 or older")
    .When(() => age < 18)
    .Build();

// With full metadata
IBusinessRule fullAgeRule = BusinessRuleBuilder.Create("AGE.INVALID")
    .WithName("Age Validation Rule")
    .WithMessage("Age must be 18 or older")
    .WithDescription("Validates that the user is at least 18 years old")
    .WithSeverity(RuleSeverity.Error)
    .WithCategory("User Validation")
    .WithTags("user", "age", "compliance")
    .When(() => age < 18)
    .Build();

// With context access
IBusinessRule limitRule = BusinessRuleBuilder.Create("LIMIT.EXCEEDED")
    .WithMessage("Transaction limit exceeded")
    .When(ctx => ctx.Get<decimal>("amount") > ctx.Get<decimal>("limit"))
    .Build();
```

### Async Builder Example

```csharp
IAsyncBusinessRule emailRule = AsyncBusinessRuleBuilder.Create("EMAIL.DUPLICATE")
    .WithMessage("Email already exists")
    .WithSeverity(RuleSeverity.Error)
    .WhenAsync(async (ctx, ct) => await repository.EmailExistsAsync(email, ct))
    .Build();

// Without cancellation token
IAsyncBusinessRule dataRule = AsyncBusinessRuleBuilder.Create("DATA.INVALID")
    .WithMessage("External data validation failed")
    .WhenAsync(async ctx => await externalService.ValidateAsync(ctx.Get<string>("data")))
    .Build();
```

---

## Subject-Typed Rules

The rules above capture their subject in the constructor, which welds one instance to one `Order`. A *subject-typed* rule receives the subject when it is evaluated instead: `IBusinessRule<TSubject>` exposes `IsBroken(subject)`, `IsBroken(subject, context)` and the `Evaluate(subject[, context])` factory, and `IAsyncBusinessRule<TSubject>` exposes `IsBrokenAsync(subject, context, ct)` and `EvaluateAsync(subject, context, ct)`. The rule holds no per-subject state, so a single instance can be registered as a singleton (with injected repositories), discovered by assembly scanning and reused for every subject. Value-type subjects are supported.

The typed interfaces do **not** derive from `IBusinessRule` / `IAsyncBusinessRule`, so no member can ever throw for lack of a subject. The only bridge back to the non-generic hierarchy is `.For(subject)`, which returns a `BoundBusinessRule<T>` / `BoundAsyncBusinessRule<T>` that forwards all seven metadata members and works with every decorator (`.Cached()`, `.When()`, `.WithSeverity()`), operator and checker. The checkers also accept the subject directly: every typed overload binds the rules with `.For(subject)` and runs the same core as the non-generic overloads, so the ledger, fault policy, severity threshold and observers behave identically (observers see the bound rules). `EvaluateEach` evaluates a rule set against many subjects and reports them by position.

All four interfaces (`IBusinessRule`, `IAsyncBusinessRule`, `IBusinessRule<T>`, `IAsyncBusinessRule<T>`) extend `IBusinessRuleMetadata`, which declares `Code` and `Message` and gives `Severity`, `Name`, `Description`, `Category` and `Tags` default implementations.

### Subject-Typed Rule Examples

```csharp
// A stateless rule: the subject arrives at evaluation time
public sealed class OrderMustHaveItemsRule : BaseBusinessRule<Order>
{
    public override string Code => "ORDER.NO_ITEMS";
    public override string Message => "Order must contain at least one item.";
    public override bool IsBroken(Order order) => order.Items.Count == 0;
}

// An async rule with an injected dependency, registered once and reused for every user
public sealed class EmailMustBeUniqueRule : BaseAsyncBusinessRule<User>
{
    private readonly IUserRepository _repository;

    public EmailMustBeUniqueRule(IUserRepository repository)
    {
        _repository = repository;
    }

    public override string Code => "EMAIL.DUPLICATE";
    public override string Message => "Email address is already in use.";

    public override Task<bool> IsBrokenAsync(User user, BusinessRuleContext context, CancellationToken cancellationToken = default)
    {
        return _repository.EmailExistsAsync(user.Email, cancellationToken);
    }
}

// Bind to a subject: the bound rule is an ordinary IBusinessRule
IBusinessRule bound = new OrderMustHaveItemsRule().For(order);
IBusinessRule cached = bound.Cached(TimeSpan.FromMinutes(1));
bound.Check();

// Typed checkers: the subject is passed once, the rules are bound for you
IBusinessRule<Order>[] rules = { new OrderMustHaveItemsRule(), new OrderTotalMustBePositiveRule() };
BusinessRuleChecker.CheckAll(order, rules, context);
RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(order, rules);
BusinessRuleChecker.CheckBySeverity(order, rules, RuleSeverity.Error);
BusinessRuleChecker.CheckByCategory(order, rules, "Orders");
BusinessRuleChecker.CheckByTags(order, rules, "checkout");

// Many subjects: one batch per subject, reported by position
SubjectEvaluationCollection<Order> each = BusinessRuleChecker.EvaluateEach(orders, rules);
if (each.HasBrokenRules)
{
    foreach (SubjectEvaluation<Order> evaluation in each.Broken)
    {
        Console.WriteLine($"Order #{evaluation.Index}: {evaluation.BrokenRules.Count} broken rule(s)");
    }
}

// Async twins
await AsyncBusinessRuleChecker.CheckAllAsync(context, user, asyncRules, cancellationToken);
AsyncSubjectEvaluationCollection<User> eachUser = await AsyncBusinessRuleChecker.EvaluateEachAsync(users, asyncRules, cancellationToken);

// Typed builders share the metadata surface of the non-generic builders and add subject-aware details
IBusinessRule<Order> big = BusinessRuleBuilder<Order>.Create("ORDER.TOO_LARGE")
    .WithMessage("Order has too many items.")
    .WithActual((o, ctx) => o.Items.Count)
    .WithExpected(100)
    .When(o => o.Items.Count > 100)
    .Build();

// Typed And/Or/Not forward the subject and the context to their operands
IBusinessRule<Order> combined = big.And(new OrderMustHaveItemsRule());
bool isBroken = combined.IsBroken(order, context);
```

---

## Rule Metadata

Every rule carries metadata that enables filtering, categorization, and documentation. Only `Code` and `Message` are required - all other metadata properties have default interface implementations and are truly optional.

| Property | Description | Default |
|----------|-------------|---------|
| `Code` | Unique identifier for the rule | **Required** |
| `Message` | Human-readable violation message | **Required** |
| `Severity` | Importance level: `Info`, `Warning`, or `Error` | `Error` |
| `Name` | Display name for the rule | Type name |
| `Description` | Detailed explanation of what the rule validates | Empty |
| `Category` | Grouping category (e.g., "Payment", "User") | Empty |
| `Tags` | List of tags for flexible filtering | Empty |

**Severity Levels:**
- `Info`: Informational - rule is broken but not critical
- `Warning`: Should be addressed but doesn't block the operation
- `Error`: Must be fixed, blocks the operation

Metadata enables powerful filtering with `CheckBySeverity()`, `CheckByCategory()`, and `CheckByTags()`.

### Metadata Example

```csharp
public class PaymentValidationRule : BaseBusinessRule
{
    public override string Code => "PAYMENT.INVALID";
    public override string Message => "Payment validation failed.";
    public override string Name => "Payment Validation";
    public override string Description => "Validates payment details before processing";
    public override RuleSeverity Severity => RuleSeverity.Error;
    public override string Category => "Payment";
    public override IReadOnlyList<string> Tags => new[] { "payment", "checkout" };

    public override bool IsBroken() => /* validation logic */;
}

// Filter by metadata
BusinessRuleChecker.CheckBySeverity(rules, RuleSeverity.Error);
BusinessRuleChecker.CheckByCategory(rules, "Payment");
BusinessRuleChecker.CheckByTags(rules, "checkout", "payment");
```

---

## Rule Composition

Rule composition allows you to combine multiple rules using logical operators. This creates complex validation logic from simple, reusable building blocks.

**Operators:**

The operators combine the **broken** state of their operands, not the "passes" state. A rule is broken when `IsBroken()` returns `true`, and the composed rule's `IsBroken()` is computed as follows:

- `And()`: broken **only when both** operands are broken (`first.IsBroken() && second.IsBroken()`). If either operand passes, the composed rule passes.
- `Or()`: broken **when either** operand is broken (`first.IsBroken() || second.IsBroken()`). It passes only when both operands pass.
- `Not()`: inverts the operand's broken state (`!rule.IsBroken()`).

The synthesised code of a composed rule is `A+B` for `And`, `A|B` for `Or` and `!A` for `Not`.

| `first` | `second` | `first.And(second)` | `first.Or(second)` |
|---------|----------|---------------------|--------------------|
| passes  | passes   | passes              | passes             |
| passes  | broken   | passes              | **broken**         |
| broken  | passes   | passes              | **broken**         |
| broken  | broken   | **broken**          | **broken**         |

| `rule`  | `rule.Not()` |
|---------|--------------|
| passes  | **broken**   |
| broken  | passes       |

In practice this means: use `Or()` when **every** operand must hold ("broken if any check fails"), and `And()` when it is enough that **one** operand holds ("broken only if all checks fail"). These semantics are pinned by `CompositionTruthTableTests` in the test suite and are kept unchanged for backwards compatibility. If you find them confusing, prefer the unambiguous quantifier-style groups described in [Structural Composition and Explain Trees](#structural-composition-and-explain-trees): `RuleGroup.All` is broken when any member is broken, `RuleGroup.Any` only when every member is broken.

Composition works with both sync and async rules. You can chain operators to create complex expressions like `rule1.And(rule2.Or(rule3.Not()))`. Since 3.0 the operators return real tree nodes (`AndBusinessRule`, `OrBusinessRule`, `NotBusinessRule` and their async twins) that keep their operands, forward the evaluation context (and, on the async side, the cancellation token) to them and short-circuit on the first operand when its result already decides the outcome.

**CompositeBusinessRule** and **CompositeAsyncBusinessRule** provide containers for grouping multiple rules that are evaluated together; a composite is broken when **any** of its inner rules is broken and evaluates every inner rule so that each broken one is reported.

### Composition Examples

```csharp
// OR - broken when EITHER check fails (both must hold)
IBusinessRule rule = new CustomerIsAdultRule(customer)
    .Or(new CustomerIsActiveRule(customer));

// AND - broken only when BOTH checks fail (one holding is enough)
IBusinessRule paymentRule = new HasNoCreditCardRule(customer)
    .And(new HasNoBankAccountRule(customer));

// NOT - invert the broken state
IBusinessRule notBlockedRule = new IsBlockedRule(customer).Not();

// Complex composition: (adult-or-active broken) AND (vip-check broken)
IBusinessRule accessRule = new CustomerIsAdultRule(customer)
    .Or(new CustomerIsActiveRule(customer))
    .And(new CustomerIsNotVIPRule(customer));

// Async composition
IAsyncBusinessRule asyncRule = new EmailUniqueRule(repo, email)
    .And(new UsernameUniqueRule(repo, username));
await asyncRule.CheckAsync(context);

// Composite container
CompositeBusinessRule composite = new CompositeBusinessRule(
    new Rule1(), new Rule2(), new Rule3()
);
composite.Check();
```

---

## Structural Composition and Explain Trees

Every composed rule keeps its shape. The `And()`, `Or()` and `Not()` operators, the composites and the new rule groups all implement `IComposedBusinessRule` (`Operator`, `Operands`), while `CachedBusinessRule` and `ConditionalBusinessRule` implement `IDecoratedBusinessRule` (`InnerRule`); the async side mirrors this with `IComposedAsyncBusinessRule` and `IDecoratedAsyncBusinessRule`, and both adapters expose their wrapped rule as `InnerRule`. Tooling can therefore walk any rule tree without knowing the concrete node types.

**Rule groups** are unambiguous N-ary quantifiers stated in terms of how many members must **pass**:

| Group | Broken when |
|-------|-------------|
| `RuleGroup.All(code, message, members...)` | any member is broken (every member must pass) |
| `RuleGroup.Any(code, message, members...)` | every member is broken (at least one member must pass) |
| `RuleGroup.AtLeast(n, code, message, members...)` | fewer than `n` members pass |
| `RuleGroup.Exactly(n, code, message, members...)` | the number of passing members is not `n` |

Groups are immutable: `WithShortCircuit()`, `WithSeverity()` and (async only) `WithParallelEvaluation()` return a new instance. By default a group evaluates every member so that the group's `BusinessRuleResult.Details` lists every broken member; a short-circuiting group stops as soon as the verdict is decided and reports the remaining members as `NotEvaluated`. `WithParallelEvaluation()` starts every member at once and awaits all of them (so faults are always observed); it cannot be combined with `WithShortCircuit()`, and a member that observes cancellation throws before it populates a `CachedAsyncBusinessRule` or reaches an observer. Factories reject empty member lists, null members and thresholds outside `0..members.Count`.

**Member results survive the batch path.** A group or composite reports one result whose `Details` holds the broken members (nested groups nest their details). `BusinessRuleChecker.CheckAll`/`EvaluateAll` and their async twins evaluate composed rules exactly once and surface those details; `exception.Flatten()` (or `result.Flatten()`) yields only the leaf results.

**Explain trees.** `rule.Explain(context)` and `await asyncRule.ExplainAsync(context, ct)` evaluate the rule **exactly once** and return a `RuleExplanation` tree produced by that evaluation: no rule is evaluated a second time to build it. Every node carries the rule, its `RuleOutcome` (`Passed`, `Broken`, `Skipped`, `CacheHit`, `NotEvaluated`, `Faulted`), `IsBroken`, the `Operator` label (`ALL`, `AND`, `AT LEAST 2`, `CACHED`, `WHEN`, `ADAPTER`, ...), a `Reason` (`short-circuit`, `precondition not met`, `cache hit`, the captured exception), the `Duration` spent in that node and its `Children`. Third-party rules appear as a single leaf. `Explain(context, captureExceptions: true)` turns a throwing node into a `Faulted` node (treated as broken) instead of propagating; cancellation is never captured. Timing exists only inside the explain walk, so the plain `IsBroken`/`Check` path stays allocation-free.

### Structural Composition Examples

```csharp
IBusinessRule payment = RuleGroup.Any("PAYMENT.NONE_VALID", "At least one payment method must be valid", card, bank, credit);
IBusinessRule kyc     = RuleGroup.All("KYC.INCOMPLETE", "All KYC checks must pass", idRule, addressRule, sanctionsRule).WithShortCircuit();
IBusinessRule tier    = RuleGroup.AtLeast(2, "TIER.INSUFFICIENT", "Need two of three signals", r1, r2, r3);
IAsyncBusinessRule any = AsyncRuleGroup.Any("PAYMENT.NONE_VALID", "...", a1, a2).WithParallelEvaluation();

// Walk any rule tree
if (kyc is IComposedBusinessRule composed)
{
    Console.WriteLine($"{composed.Operator} over {composed.Operands.Count} operands");
}

// Member results survive the batch path
try
{
    BusinessRuleChecker.CheckAll(context, kyc, payment);
}
catch (BusinessRuleValidationException ex)
{
    foreach (BusinessRuleResult group in ex.BrokenRules)
    {
        Console.WriteLine($"{group.Code}: {group.Details.Count} member(s) broken");
    }

    IEnumerable<BusinessRuleResult> leaves = ex.Flatten();   // KYC.ADDRESS, CARD, BANK, ...
}

// Build a result with details yourself
BusinessRuleResult result = new BusinessRuleResult(code, message, severity, details: memberResults);

// Single evaluation, trace as by-product
RuleExplanation why = kyc.Explain(context);
RuleExplanation whyAsync = await any.ExplainAsync(context, cancellationToken);
Console.WriteLine(why.ToText());
// [BROKEN] ALL  KYC.INCOMPLETE  "All KYC checks must pass"  (0.9 ms)
//  ├─ [PASSED]  KYC.ID  (0.1 ms)
//  ├─ [BROKEN]  KYC.ADDRESS  "Address must be verified"  (0.2 ms)
//  └─ [NOT EVALUATED]  KYC.SANCTIONS  reason: short-circuit

foreach (RuleExplanation leaf in why.BrokenLeaves())
{
    Console.WriteLine($"{leaf.Code}: {leaf.Message}");
}

// Capture faults instead of propagating them
RuleExplanation tolerant = kyc.Explain(context, captureExceptions: true);
```

---

## Batch Validation

Batch validation allows checking multiple rules at once. The library provides two checker classes:

- `BusinessRuleChecker`: For synchronous rules
- `AsyncBusinessRuleChecker`: For asynchronous rules

**Features:**
- **Fail-Fast Mode** (`stopOnFirstFailure: true`): Stops checking after the first broken rule, improving performance when you only need to know if validation failed
- **Parallel Execution** (`runInParallel: true`): Evaluates async rules concurrently for better performance with I/O-bound rules
- **Observer Support**: Receive callbacks during rule evaluation for logging, metrics, etc.
- **Filtering**: Check only rules matching specific severity, category, or tags
- **Options Objects**: `RuleEvaluationOptions` / `AsyncRuleEvaluationOptions` replace the boolean parameters and add a fault policy, a severity threshold and observer callbacks from `EvaluateAll` (see [Evaluation Ledger, Options and Fault Policy](#evaluation-ledger-options-and-fault-policy))

Note: Fail-fast and parallel execution cannot be combined - fail-fast takes precedence.

### Batch Validation Examples

```csharp
// Basic batch checking
BusinessRuleChecker.CheckAll(rule1, rule2, rule3);

await AsyncBusinessRuleChecker.CheckAllAsync(context, asyncRule1, asyncRule2);

// With context
BusinessRuleContext context = new BusinessRuleContext();
context.Set("userId", 123);
BusinessRuleChecker.CheckAll(context, rule1, rule2);

// Fail-fast mode
BusinessRuleChecker.CheckAll(rules, stopOnFirstFailure: true);

await AsyncBusinessRuleChecker.CheckAllAsync(
    context, rules,
    stopOnFirstFailure: true
);

// Parallel execution (async only)
await AsyncBusinessRuleChecker.CheckAllAsync(
    context, rules,
    runInParallel: true
);

// Filter by criteria
BusinessRuleChecker.CheckBySeverity(rules, RuleSeverity.Error);
BusinessRuleChecker.CheckByCategory(rules, "Payment");
BusinessRuleChecker.CheckByTags(rules, "checkout");

await AsyncBusinessRuleChecker.CheckBySeverityAsync(
    context, rules, default, RuleSeverity.Error
);

// Options object (fault policy, severity threshold, observer)
BusinessRuleChecker.CheckAll(rules, context, new RuleEvaluationOptions
{
    StopOnFirstFailure = true,
    OnFault = FaultPolicy.Record,
    ThrowOn = RuleSeverity.Error
});

await AsyncBusinessRuleChecker.CheckAllAsync(context, asyncRules, new AsyncRuleEvaluationOptions
{
    RunInParallel = true,
    OnFault = FaultPolicy.TreatAsBroken
}, cancellationToken);
```

---

## Context and Data Passing

`BusinessRuleContext` is a key-value store for passing data to rules during evaluation. This is especially useful for:

- Sharing data between multiple rules
- Passing runtime configuration
- Providing services or dependencies

**Features:**
- `Set(key, value)`: Store a value
- `Get<T>(key)`: Retrieve a typed value; throws `ContextKeyNotFoundException` (a `KeyNotFoundException` that lists the available keys) when the key is missing and `ContextValueTypeMismatchException` (an `InvalidCastException` that names the stored and requested types) when the value is of another type
- `TryGet<T>(key, out value)`: Safe retrieval; `false` for a missing key or a type mismatch, never throws
- `ContainsKey(key)`: Check existence
- `Remove(key)`: Remove a value
- `Keys`: Read-only live view of the stored key names
- Every method has a `ContextKey<T>` overload; see [Typed Context Keys](#typed-context-keys)

**Typed Context:** `BusinessRuleContext<T>` provides strongly-typed access to a data object via the `Data` property, while still supporting the key-value store for additional data.

### Context Examples

```csharp
// Basic context
BusinessRuleContext context = new BusinessRuleContext();
context.Set("userId", 123);
context.Set("role", "admin");
context.Set("limit", 1000.00m);

int userId = context.Get<int>("userId");
if (context.TryGet<string>("role", out string role))
    Console.WriteLine($"Role: {role}");

// Using context in rules
public class TransactionLimitRule : BaseBusinessRule
{
    private readonly decimal _amount;

    public TransactionLimitRule(decimal amount) => _amount = amount;

    public override string Code => "TRANSACTION.LIMIT_EXCEEDED";
    public override string Message => "Transaction amount exceeds limit.";

    public override bool IsBroken(BusinessRuleContext context)
    {
        decimal limit = context.Get<decimal>("limit");
        return _amount > limit;
    }
}

// Typed context
public record OrderContext(Order Order, Customer Customer);

BusinessRuleContext<OrderContext> typedContext = new BusinessRuleContext<OrderContext>(
    new OrderContext(order, customer)
);

Order order = typedContext.Data.Order;
typedContext.Set("additionalInfo", "extra data"); // Still works
```

---

## Typed Context Keys

`context.Get<Order>("ordr")` compiles, and fails ten frames deep with a bare `KeyNotFoundException`. `ContextKey<T>` binds the key name and the value type into one symbol: declare the keys once as `static readonly` fields, share them between the code that fills the context and the rules that read it, and `context.Get(OrderKeys.Order)` infers `T` with no cast and no string literal. The typed overloads delegate to the string API through `key.Name`, so typed and string access to the same key interoperate and `BusinessRuleContext<T>`'s `Data` slot is untouched.

**Features:**
- `ContextKey<T>(name)`: Immutable, equatable key (ordinal name comparison, null/blank names rejected); `Name`, `ValueType`
- `Get(key)`, `TryGet(key, out value)`, `Set(key, value)`, `ContainsKey(key)`, `Remove(key)`: typed overloads of every `BusinessRuleContext` member
- `Keys`: Read-only live view of the stored key names (also what the failure messages list)
- `ContextKeyNotFoundException`: `Context has no key 'ordr'. Available keys: customer, limit, order`; derives from `KeyNotFoundException` and exposes `Key` and `AvailableKeys`
- `ContextValueTypeMismatchException`: `Key 'order' holds Int32, requested Order`; derives from `InvalidCastException` and exposes `Key`, `ActualType` (null when the stored value is null) and `RequestedType`
- `BusinessRuleContextException`: thrown by `BusinessRuleChecker` / `AsyncBusinessRuleChecker` when a rule in the batch fails to read the context: `Rule LIMIT.EXCEEDED failed to read context: Context has no key 'limit'. Available keys: order`; exposes `RuleCode`, `Key` and the original exception as `InnerException`. Under `FaultPolicy.Record` / `TreatAsBroken` the wrapped exception is what the ledger entry's `Fault` and the observer's `OnRuleFaulted` receive. Exceptions that are not context reads propagate untouched

### Typed Context Key Examples

```csharp
// Declare the keys once
public static class OrderKeys
{
    public static readonly ContextKey<Order> Order = new ContextKey<Order>("order");
    public static readonly ContextKey<decimal> Limit = new ContextKey<decimal>("limit");
}

// Fill the context
BusinessRuleContext context = new BusinessRuleContext();
context.Set(OrderKeys.Order, order);
context.Set(OrderKeys.Limit, 1000m);

// Read it in a rule: T is inferred from the key
public class TransactionLimitRule : BaseBusinessRule
{
    public override string Code => "LIMIT.EXCEEDED";
    public override string Message => "Transaction amount exceeds limit.";

    public override bool IsBroken(BusinessRuleContext context)
    {
        Order order = context.Get(OrderKeys.Order);
        decimal limit = context.Get(OrderKeys.Limit);
        return order.Total > limit;
    }
}

// Safe read and existence checks
if (context.TryGet(OrderKeys.Limit, out decimal limit))
    Console.WriteLine($"Limit: {limit}");

bool hasOrder = context.ContainsKey(OrderKeys.Order);
IReadOnlyCollection<string> keys = context.Keys;   // "order", "limit"

// Descriptive failures
try
{
    context.Get<Order>("ordr");
}
catch (ContextKeyNotFoundException ex)              // still a KeyNotFoundException
{
    // Context has no key 'ordr'. Available keys: limit, order
    Console.WriteLine($"{ex.Key} missing; have {string.Join(", ", ex.AvailableKeys)}");
}

// The checkers name the failing rule and keep the original exception
try
{
    BusinessRuleChecker.CheckAll(new[] { new TransactionLimitRule() }, context: new BusinessRuleContext());
}
catch (BusinessRuleContextException ex)
{
    // Rule LIMIT.EXCEEDED failed to read context: Context has no key 'order'. The context is empty.
    Console.WriteLine($"{ex.RuleCode} could not read '{ex.Key}': {ex.InnerException!.Message}");
}

// Tests: typed pairs of different types in one call (up to four; call Set for more)
BusinessRuleContext testContext = RuleTestHelper.CreateContext((OrderKeys.Order, order), (OrderKeys.Limit, 1000m));
```

---

## Caching

Caching wraps a rule and stores its result for a specified duration. Subsequent calls return the cached result without re-executing the rule logic. This is useful for:

- Expensive database queries
- External API calls
- Complex calculations

**Classes:**
- `CachedBusinessRule`: Wraps sync rules with thread-safe caching
- `CachedAsyncBusinessRule`: Wraps async rules with async-safe caching (uses SemaphoreSlim)

**Cache Invalidation:**
- `InvalidateCache()`: Clears the sync cache
- `InvalidateCacheAsync()`: Clears the async cache

**Extension Methods:**
- `.WithCache(TimeSpan)`: Fluent syntax for adding caching

### Caching Examples

```csharp
// Sync caching
CachedBusinessRule cachedRule = new CachedBusinessRule(
    new ExpensiveValidationRule(),
    TimeSpan.FromMinutes(5)
);

bool result1 = cachedRule.IsBroken(); // Executes rule
bool result2 = cachedRule.IsBroken(); // Returns cached result

cachedRule.InvalidateCache(); // Clear cache

// Async caching
CachedAsyncBusinessRule cachedAsyncRule = new CachedAsyncBusinessRule(
    new DatabaseValidationRule(),
    TimeSpan.FromMinutes(10)
);

bool result = await cachedAsyncRule.IsBrokenAsync(context);
await cachedAsyncRule.InvalidateCacheAsync();

// Extension method syntax
IBusinessRule cachedExtRule = new ExpensiveRule().WithCache(TimeSpan.FromMinutes(5));
IAsyncBusinessRule cachedExtAsync = new DatabaseRule().WithCache(TimeSpan.FromSeconds(30));
```

---

## Conditional Execution

Conditional execution wraps a rule and only evaluates it when a precondition is met. If the condition returns `false`, the rule is considered not broken (passes automatically). This is useful for:

- Feature flags
- User-specific validation
- Optional validation steps

**Classes:**
- `ConditionalBusinessRule`: Wraps sync rules with a sync condition
- `ConditionalAsyncBusinessRule`: Wraps async rules with a sync condition

**Extension Methods:**
- `.WithCondition(Func<bool>)`: Simple condition
- `.WithCondition(Func<BusinessRuleContext, bool>)`: Context-aware condition
- `.WithCondition(Func<BusinessRuleContext, CancellationToken, Task<bool>>)`: Async condition

### Conditional Execution Examples

```csharp
// Simple condition
ConditionalBusinessRule conditionalRule = new ConditionalBusinessRule(
    new PremiumFeatureRule(),
    () => user.IsPremium
);

// Context-aware condition
ConditionalBusinessRule contextRule = new ConditionalBusinessRule(
    new AdvancedValidationRule(),
    ctx => ctx.Get<bool>("enableAdvancedValidation")
);

// Async rule with condition
ConditionalAsyncBusinessRule conditionalAsyncRule = new ConditionalAsyncBusinessRule(
    new ExpensiveDatabaseRule(),
    ctx => ctx.Get<bool>("shouldValidate")
);

await conditionalAsyncRule.CheckAsync(context); // Only runs if condition is true

// Extension method syntax
IBusinessRule condExtRule = new PremiumRule().WithCondition(() => isPremiumEnabled);

IAsyncBusinessRule condExtAsync = new ValidationRule()
    .WithCondition(ctx => ctx.Get<bool>("validateEnabled"));

IAsyncBusinessRule asyncCondition = new ComplexRule()
    .WithCondition(async (ctx, ct) =>
        await featureService.IsEnabledAsync("validation", ct));
```

---

## Rule Adapters

Rule adapters convert between sync and async rule interfaces. This is useful when you need to:

- Use a sync rule in an async-only context
- Use an async rule in legacy sync code

**Classes:**
- `SyncToAsyncRuleAdapter`: Wraps a sync rule to implement `IAsyncBusinessRule`
- `AsyncToSyncRuleAdapter`: Wraps an async rule to implement `IBusinessRule` (**Warning: Blocks!**)

**Extension Methods:**
- `.ToAsync()`: Convert sync to async
- `.ToSyncRule()`: Convert async to sync (blocking)

> **Warning:** `ToSyncRule()` and `AsyncToSyncRuleAdapter` block the calling thread using `.GetAwaiter().GetResult()`. Avoid using in UI threads or ASP.NET request threads to prevent deadlocks.

### Adapter Examples

```csharp
// Sync to Async
IBusinessRule syncRule = new CustomerIsAdultRule(customer);
IAsyncBusinessRule convertedAsyncRule = syncRule.ToAsync();

// Or using adapter directly
SyncToAsyncRuleAdapter syncToAsyncAdapter = new SyncToAsyncRuleAdapter(syncRule);

await convertedAsyncRule.CheckAsync(context);

// Async to Sync (WARNING: Blocks!)
IAsyncBusinessRule originalAsyncRule = new EmailMustBeUniqueRule(repo, email);
IBusinessRule convertedSyncRule = originalAsyncRule.ToSyncRule();

// Or using adapter directly
AsyncToSyncRuleAdapter asyncToSyncAdapter = new AsyncToSyncRuleAdapter(originalAsyncRule);

convertedSyncRule.Check(); // Blocks until async operation completes
```

---

## Observers

Observers receive callbacks during rule evaluation, enabling:

- Logging and debugging
- Performance metrics
- Audit trails
- Custom error handling

**Interfaces:**
- `IRuleExecutionObserver`: For sync rule evaluation
  - `OnBeforeEvaluate(IBusinessRule rule)`: Called before evaluation
  - `OnAfterEvaluate(IBusinessRule rule, BusinessRuleResult? result)`: Called after (result is null if passed)
  - `OnRuleBroken(IBusinessRule rule, BusinessRuleResult result)`: Called only when broken
  - Batch lifecycle hooks with empty default implementations (existing observers keep compiling):
    - `OnBatchStarted(int ruleCount)`: Called once before the first rule
    - `OnAfterEvaluate(IBusinessRule rule, bool isBroken, TimeSpan elapsed)`: Called after each rule with the time the checker measured
    - `OnRuleFaulted(IBusinessRule rule, Exception exception)`: Called when a rule throws, before the `FaultPolicy` is applied
    - `OnBatchCompleted(RuleEvaluationResult result)`: Called once with the result and its ledger, before `CheckAll` throws

- `IAsyncRuleExecutionObserver`: For async rule evaluation
  - Same methods with async signatures and CancellationToken (`OnBatchStartedAsync`, `OnAfterEvaluateAsync(rule, isBroken, elapsed, ct)`, `OnRuleFaultedAsync`, `OnBatchCompletedAsync`)

Per rule the order is `OnBeforeEvaluate`, `OnAfterEvaluate(rule, result)`, `OnAfterEvaluate(rule, isBroken, elapsed)`, then `OnRuleBroken` when broken. A rule that throws gets `OnRuleFaulted` instead of the two `OnAfterEvaluate` calls (plus `OnRuleBroken` under `FaultPolicy.TreatAsBroken`). `OnBatchCompleted` is not raised when a fault propagates under `FaultPolicy.Throw`.

Observers are passed to `BusinessRuleChecker.CheckAll()` / `AsyncBusinessRuleChecker.CheckAllAsync()` via the `observer` parameter, or to any options-taking overload through `RuleEvaluationOptions.Observer` / `AsyncRuleEvaluationOptions.Observer`. Observers fire from `EvaluateAll` / `EvaluateAllAsync` only through the options-taking overloads; the boolean `EvaluateAll` overloads have no observer parameter.

### Observer Examples

```csharp
// Sync observer
public class LoggingObserver : IRuleExecutionObserver
{
    public void OnBeforeEvaluate(IBusinessRule rule)
    {
        Console.WriteLine($"[START] Evaluating: {rule.Code}");
    }

    public void OnAfterEvaluate(IBusinessRule rule, BusinessRuleResult? result)
    {
        string status = result == null ? "PASSED" : "FAILED";
        Console.WriteLine($"[{status}] {rule.Code}");
    }

    public void OnRuleBroken(IBusinessRule rule, BusinessRuleResult result)
    {
        Console.WriteLine($"[BROKEN] {result.Code}: {result.Message}");
    }
}

// Usage
BusinessRuleChecker.CheckAll(rules, observer: new LoggingObserver());

// Async observer
public class AsyncLoggingObserver : IAsyncRuleExecutionObserver
{
    public async Task OnBeforeEvaluateAsync(IAsyncBusinessRule rule, CancellationToken ct)
    {
        await logger.LogAsync($"Evaluating: {rule.Code}");
    }

    public async Task OnAfterEvaluateAsync(IAsyncBusinessRule rule, BusinessRuleResult? result, CancellationToken ct)
    {
        await logger.LogAsync($"Result: {result?.Code ?? "passed"}");
    }

    public async Task OnRuleBrokenAsync(IAsyncBusinessRule rule, BusinessRuleResult result, CancellationToken ct)
    {
        await logger.LogAsync($"Broken: {result.Message}");
    }
}

// Usage
await AsyncBusinessRuleChecker.CheckAllAsync(
    context, rules, observer: new AsyncLoggingObserver()
);
```

---

## Evaluation Results

Evaluation methods return detailed results without throwing exceptions. This is useful when you need to:

- Collect all validation errors at once
- Filter or process errors programmatically
- Return errors to an API client

**Classes:**
- `RuleEvaluationResult`: Results from sync evaluation
- `AsyncRuleEvaluationResult`: Results from async evaluation

**Properties:**
- `BrokenRules`: List of `BusinessRuleResult` for broken rules
- `PassedRules`: List of rules that passed
- `HasBrokenRules`: True if any rules are broken
- `AllPassed`: True if all rules passed
- `Errors` / `Warnings` / `Infos` and `HasErrors` / `HasWarnings`: Broken rules by severity
- `Entries`, `Faulted`, `HasFaults`, `TotalDuration`, `Slowest`: The evaluation ledger (see [Evaluation Ledger, Options and Fault Policy](#evaluation-ledger-options-and-fault-policy))

**Methods:**
- `GetBySeverity(RuleSeverity)`: Filter broken rules by severity
- `ByTag(tag)` / `ByCategory(category)`: Filter broken rules by the rule's tags (case-insensitive) or category (see [Rich Results](#rich-results))
- `ThrowIfBroken()`, `ThrowIfErrors()`, `ThrowIfAtLeast(RuleSeverity)`: Turn the result into a `BusinessRuleValidationException`; broken rules below the threshold travel on `exception.CollectedRules`

### Evaluation Examples

```csharp
// Sync evaluation
RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rules);

if (result.HasBrokenRules)
{
    foreach (BusinessRuleResult error in result.BrokenRules)
        Console.WriteLine($"{error.Code}: {error.Message}");
}

if (result.AllPassed)
    Console.WriteLine("Validation successful!");

// Filter results
IEnumerable<BusinessRuleResult> paymentErrors = result.BrokenRules.Where(r => r.Code.StartsWith("PAYMENT"));
IEnumerable<BusinessRuleResult> errors = result.Errors;
IEnumerable<BusinessRuleResult> warnings = result.GetBySeverity(RuleSeverity.Warning);

// Throw only for errors; warnings stay reachable on the exception
result.ThrowIfAtLeast(RuleSeverity.Error);

// Async evaluation
AsyncRuleEvaluationResult asyncResult = await AsyncBusinessRuleChecker.EvaluateAllAsync(
    context, rules
);

// Parallel async evaluation
AsyncRuleEvaluationResult parallelResult = await AsyncBusinessRuleChecker.EvaluateAllAsync(
    context, rules, runInParallel: true
);

// API response example (see Rich Results for Name, Category, Tags, Detail, Actual, Expected, Remediation)
if (result.HasBrokenRules)
{
    return BadRequest(new {
        errors = result.BrokenRules.Select(r => new {
            code = r.Code,
            name = r.Name,
            category = r.Category,
            message = r.Message,
            detail = r.Detail,
            severity = r.Severity.ToString()
        })
    });
}
```

---

## Evaluation Ledger, Options and Fault Policy

One options object per side (`RuleEvaluationOptions` for `BusinessRuleChecker`, `AsyncRuleEvaluationOptions` for `AsyncBusinessRuleChecker`) replaces the growing pile of boolean parameters. Every checker call, including the existing boolean overloads, now runs through a single core that records an **evaluation ledger**: one `RuleEvaluationEntry` per submitted rule with its `Order` (submission index), `Outcome` (`Passed`, `Broken`, `Faulted`, or `NotEvaluated` after a fail-fast stop), `Result`, `Fault` and `Duration`. The result exposes the ledger as `Entries`, plus `Faulted`, `HasFaults`, `TotalDuration` (batch wall clock) and `Slowest`. Timing uses `Stopwatch.GetTimestamp()` tick math, so no `Stopwatch` is allocated.

**Options:**

| Option | Default | Effect |
|--------|---------|--------|
| `StopOnFirstFailure` | `false` | Stop at the first broken rule; the remaining rules are recorded as `NotEvaluated`. In `CheckAll` only a rule at or above `ThrowOn` stops the batch |
| `OnFault` | `FaultPolicy.Throw` | What happens when a rule throws (see below) |
| `ThrowOn` | `RuleSeverity.Info` | The lowest severity that makes `CheckAll` throw. Honoured by `CheckAll` only; `EvaluateAll` never throws for broken rules |
| `Observer` | `null` | Receives every callback from both `CheckAll` and `EvaluateAll` |
| `RunInParallel` (async only) | `false` | Evaluate concurrently; fail-fast still wins and forces sequential evaluation |

**Fault policy.** Cancellation (`OperationCanceledException`) always propagates and is never subject to the policy.

| `FaultPolicy` | Behaviour |
|---------------|-----------|
| `Throw` | The exception propagates after `OnRuleFaulted`; no result or ledger is produced (pre-3.0 behaviour) |
| `Record` | The entry is `Faulted` with the exception; the rule is neither broken nor passed and the batch continues. `CheckAll` does not throw for a recorded fault by itself, so use it with `EvaluateAll` or an observer |
| `TreatAsBroken` | As `Record`, plus a broken result built from the rule's `Code`/`Message`/`Severity` is added, so the fault blocks like a violation. A `Code` or `Message` getter that throws falls back to the rule's type name without masking the original fault |

There is deliberately no `TreatAsPassed`: a fault is never silently turned into a pass.

**Severity threshold.** `CheckAll` with `ThrowOn = RuleSeverity.Error` throws only when an error-level rule is broken; broken warnings and infos ride along on `exception.CollectedRules` and the full ledger on `exception.Entries`. `result.ThrowIfAtLeast(severity)` does the same on a result, and the `rule.Check(context, throwOn)` / `await rule.CheckAsync(context, throwOn, ct)` extensions apply the threshold to a single rule. `rule.WithSeverity(RuleSeverity.Warning)` wraps any rule in a `SeverityOverrideBusinessRule` (async: `SeverityOverrideAsyncBusinessRule`) that reports the new severity while forwarding every other metadata member, the verdict and member `Details`; it appears as a `SEVERITY` node in an explain tree.

### Ledger and Options Examples

```csharp
// Record faults instead of aborting the batch, and inspect the ledger
RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rules, context, new RuleEvaluationOptions
{
    OnFault = FaultPolicy.Record
});

foreach (RuleEvaluationEntry entry in result.Entries)
    Console.WriteLine($"{entry.Order}: {entry.Code} {entry.Outcome} in {entry.Duration.TotalMilliseconds:F2} ms");

if (result.HasFaults)
    Console.WriteLine($"{result.Faulted.First().Code} threw {result.Faulted.First().Fault!.GetType().Name}");

Console.WriteLine($"Slowest rule: {result.Slowest?.Code}, batch took {result.TotalDuration.TotalMilliseconds:F2} ms");

// Throw only at Error; warnings are collected on the exception
try
{
    BusinessRuleChecker.CheckAll(rules, context, new RuleEvaluationOptions { ThrowOn = RuleSeverity.Error });
}
catch (BusinessRuleValidationException ex)
{
    IReadOnlyCollection<BusinessRuleResult> errors = ex.BrokenRules;        // at or above the threshold
    IReadOnlyCollection<BusinessRuleResult> warnings = ex.CollectedRules;   // below the threshold
    IReadOnlyList<RuleEvaluationEntry> ledger = ex.Entries;
}

// Re-severity a rule without subclassing it
IBusinessRule softened = new EmailFormatRule(email).WithSeverity(RuleSeverity.Warning);
softened.Check(context, throwOn: RuleSeverity.Error);   // broken as a warning => returns silently

// Async: parallel, faults count as broken, observer sees every rule and the batch
AsyncRuleEvaluationResult asyncResult = await AsyncBusinessRuleChecker.EvaluateAllAsync(context, asyncRules, new AsyncRuleEvaluationOptions
{
    RunInParallel = true,
    OnFault = FaultPolicy.TreatAsBroken,
    Observer = new MetricsObserver()
}, cancellationToken);

asyncResult.ThrowIfAtLeast(RuleSeverity.Error);
```

---

## Rich Results

"Transaction amount exceeds limit" is useless to the user who needs to know it was 5,200 against a 5,000 limit. Every `BusinessRuleResult` now carries the rule's **`Name`, `Category` and `Tags`** (populated at every construction site: base classes, builders, decorators, adapters, composites, groups and the checkers), so an exception or a ledger can be filtered by category or tag and mapped to an RFC 7807 Problem Details payload without looking the rule up again. A rule can additionally attach rich violation details through a **`Describe(context)`** hook: a formatted `Detail` sentence, the `Actual` value that broke it, the `Expected` value or range, a `Remediation` hint and a free-form `Data` bag.

**How it works:**
- `RuleResultDetails` is an immutable fluent bag: start with `RuleResultDetails.Detail($"...")` or `RuleResultDetails.Empty`, then chain `.Actual(...)`, `.Expected(...)`, `.Remediation(...)`, `.With(key, value)`
- `BaseBusinessRule` and `BaseAsyncBusinessRule` expose `protected virtual RuleResultDetails Describe(BusinessRuleContext? context)`. It runs **only on the broken path** (never from `IsBroken`, never for a passing rule) and is guarded: an exception inside `Describe` degrades the details to `RuleResultDetails.Empty` so it can never mask the violation it describes. Cancellation propagates
- `IBusinessRule` gains `Evaluate(BusinessRuleContext context)` (a default interface member falling back to `Evaluate()`), implemented explicitly on every in-library node, decorator, adapter and composite. `Check(context)`, `CompositeBusinessRule.EvaluateAll(context)` and the checkers route broken leaves through it, so `Describe` sees the same context the rule was evaluated with. Both `Evaluate` overloads stay pure factories: they build the result without re-evaluating the rule
- `Detail` is written as an interpolated string (`FormattableString`) and formatted when the result is built with `BusinessRuleContext.FormatProvider` when set, otherwise the invariant culture. `Actual`/`Expected` stay `object?` and are serialised by their runtime type. `Message` remains a finished string: no templating
- Decorators keep the inner rule's details: `Cached`/`Conditional` forward `Evaluate(context)` to the inner rule, `WithSeverity()` copies the inner result with the new severity (`BusinessRuleResult.WithSeverity`), and composed results carry the member results in `Details`
- Filters: `BusinessRuleValidationException`, `RuleEvaluationResult` and `AsyncRuleEvaluationResult` expose `ByTag(tag)` (case-insensitive) and `ByCategory(category)` (ordinal); `BusinessRuleResult.HasTag(tag)` is the per-result predicate

### Rich Results Examples

```csharp
// A rule that describes what broke it
public class TransactionLimitRule : BaseBusinessRule
{
    private readonly decimal _amount;
    private readonly decimal _limit;

    public TransactionLimitRule(decimal amount, decimal limit)
    {
        _amount = amount;
        _limit = limit;
    }

    public override string Code => "LIMIT.EXCEEDED";
    public override string Message => "Transaction limit exceeded";
    public override string Category => "Payments";
    public override IReadOnlyList<string> Tags => new[] { "customer-facing" };

    public override bool IsBroken() => _amount > _limit;

    // Runs only when the rule is broken; a throwing Describe degrades to RuleResultDetails.Empty
    protected override RuleResultDetails Describe(BusinessRuleContext? context) => RuleResultDetails
        .Detail($"Amount {_amount:C} exceeds limit {_limit:C}")      // formatted with context.FormatProvider (default: invariant)
        .Actual(_amount)
        .Expected($"<= {_limit}")
        .Remediation("Reduce the amount or request a limit increase.")
        .With("currency", "USD");
}

// The same with the fluent builder: every detail function receives the context and runs only on the broken path
IBusinessRule rule = BusinessRuleBuilder.Create("LIMIT.EXCEEDED")
    .WithMessage("Transaction limit exceeded")
    .WithCategory("Payments")
    .WithTags("customer-facing")
    .When(ctx => ctx.Get<decimal>("amount") > ctx.Get<decimal>("limit"))
    .WithDetail(ctx => $"Amount {ctx.Get<decimal>("amount"):C} exceeds limit {ctx.Get<decimal>("limit"):C}")
    .WithActual(ctx => ctx.Get<decimal>("amount"))
    .WithExpected(ctx => $"<= {ctx.Get<decimal>("limit")}")
    .WithRemediation("Split the transaction.")
    .Build();

// Per-request culture for the Detail sentence
BusinessRuleContext context = new BusinessRuleContext();
context.Set("amount", 5200m);
context.Set("limit", 5000m);
context.FormatProvider = CultureInfo.GetCultureInfo("de-DE");

try
{
    BusinessRuleChecker.CheckAll(context, rule, otherRule);
}
catch (BusinessRuleValidationException ex)
{
    // Filter without a rule lookup
    IEnumerable<BusinessRuleResult> paymentErrors = ex.ByCategory("Payments");
    IEnumerable<BusinessRuleResult> customerFacing = ex.ByTag("customer-facing");

    // Everything needed for a 422 Problem Details payload comes straight out of the engine
    return UnprocessableEntity(new ProblemDetails
    {
        Title = "One or more business rules were violated.",
        Status = 422,
        Extensions =
        {
            ["errors"] = ex.BrokenRules.Select(r => new
            {
                code = r.Code,
                name = r.Name,
                category = r.Category,
                tags = r.Tags,
                message = r.Message,
                detail = r.Detail,              // "Amount 5.200,00 € exceeds limit 5.000,00 €"
                actual = r.Actual,              // 5200 (serialised by runtime type)
                expected = r.Expected,          // "<= 5000"
                remediation = r.Remediation,
                data = r.Data
            })
        }
    });
}

// Describe sees the context the rule was checked with, and Evaluate(context) is a factory you can call directly
BusinessRuleResult result = rule.Evaluate(context);          // same result Check(context) would throw with

// Decorators keep the details; a severity override only changes the severity
IBusinessRule softened = new TransactionLimitRule(5200m, 5000m).WithSeverity(RuleSeverity.Warning);
BusinessRuleResult warning = softened.Evaluate();            // Severity = Warning, Actual = 5200, Remediation kept
```

---

## Testing Utilities

`RuleTestHelper` provides static methods for unit testing business rules:

**Assertion Methods:**
- `AssertBroken(rule)`: Assert the rule is broken
- `AssertBroken(rule, context)`: Assert with context
- `AssertNotBroken(rule)`: Assert the rule passes
- `AssertNotBroken(rule, context)`: Assert with context
- `AssertBrokenAsync(rule)`: Async assertion for broken
- `AssertNotBrokenAsync(rule)`: Async assertion for passing

**Factory Methods:**
- `CreateContext(params (string, object)[])`: Create context with key-value pairs
- `CreateContext((ContextKey<T1>, T1), ...)`: Create context from one to four typed key/value pairs of different types
- `CreateContext<T>(T data)`: Create typed context
- `CreateBrokenRule(code, message)`: Create a rule that always fails
- `CreatePassingRule(code, message)`: Create a rule that always passes
- `CreateBrokenAsyncRule(code, message)`: Async rule that always fails
- `CreatePassingAsyncRule(code, message)`: Async rule that always passes

All assertion methods throw `RuleAssertionException` on failure.

### Testing Examples

```csharp
public class CustomerRulesTests
{
    [Fact]
    public void CustomerMustBeActive_WhenInactive_IsBroken()
    {
        Customer customer = new Customer { IsActive = false };
        CustomerMustBeActiveRule rule = new CustomerMustBeActiveRule(customer);

        RuleTestHelper.AssertBroken(rule);
    }

    [Fact]
    public void CustomerMustBeActive_WhenActive_Passes()
    {
        Customer customer = new Customer { IsActive = true };
        CustomerMustBeActiveRule rule = new CustomerMustBeActiveRule(customer);

        RuleTestHelper.AssertNotBroken(rule);
    }

    [Fact]
    public void Rule_WithContext_EvaluatesCorrectly()
    {
        TransactionLimitRule rule = new TransactionLimitRule(500m);
        BusinessRuleContext context = RuleTestHelper.CreateContext(("limit", 1000m));

        RuleTestHelper.AssertNotBroken(rule, context);
    }

    [Fact]
    public async Task AsyncRule_WhenBroken_ThrowsAssertion()
    {
        IAsyncBusinessRule rule = RuleTestHelper.CreateBrokenAsyncRule();

        await RuleTestHelper.AssertBrokenAsync(rule);
    }
}

// Factory methods
IBusinessRule brokenRule = RuleTestHelper.CreateBrokenRule("TEST.ERROR", "Test error");
IBusinessRule passingRule = RuleTestHelper.CreatePassingRule();
BusinessRuleContext context = RuleTestHelper.CreateContext(
    ("userId", 123),
    ("role", "admin")
);
BusinessRuleContext typedContext = RuleTestHelper.CreateContext(
    (OrderKeys.Order, order),
    (OrderKeys.Limit, 1000m)
);
```

---

## Real-World Examples

### E-Commerce Checkout

```csharp
public class CheckoutService
{
    public async Task<CheckoutResult> ProcessCheckoutAsync(
        Cart cart, Customer customer, PaymentDetails payment, Address address)
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set("cart", cart);

        // Fast sync validations first
        BusinessRuleChecker.CheckAll(
            new CartMustHaveItemsRule(cart),
            new CustomerMustBeActiveRule(customer),
            new CustomerMustBeAdultRule(customer)
        );

        // Async validations in parallel
        await AsyncBusinessRuleChecker.CheckAllAsync(
            context,
            new IAsyncBusinessRule[]
            {
                new AddressValidationRule(_addressValidator, address),
                new PaymentValidationRule(_paymentService, payment),
                new InventoryAvailabilityRule(_inventoryService, cart.Items)
            },
            runInParallel: true
        );

        return await CreateOrderAsync(cart, customer, payment, address);
    }
}
```

### User Registration

```csharp
public async Task<User> RegisterUserAsync(RegistrationRequest request)
{
    // Sync validations
    IBusinessRule passwordRule = BusinessRuleBuilder.Create("PASSWORD.WEAK")
        .WithMessage("Password must be at least 8 characters")
        .When(() => request.Password.Length < 8)
        .Build();

    BusinessRuleChecker.CheckAll(passwordRule);

    // Async uniqueness checks
    IAsyncBusinessRule emailRule = AsyncBusinessRuleBuilder.Create("EMAIL.EXISTS")
        .WithMessage($"Email '{request.Email}' is already registered")
        .WhenAsync(async (ctx, ct) => await _repo.EmailExistsAsync(request.Email, ct))
        .Build();

    await AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), emailRule);

    return await CreateUserAsync(request);
}
```

### API Controller Validation

```csharp
[HttpPost]
public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
{
    AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(
        new BusinessRuleContext(),
        new IAsyncBusinessRule[]
        {
            new ProductExistsRule(_productService, request.ProductId),
            new InventoryRule(_inventoryService, request.ProductId, request.Quantity)
        },
        runInParallel: true
    );

    if (result.HasBrokenRules)
    {
        return BadRequest(new {
            errors = result.BrokenRules.Select(r => new {
                code = r.Code,
                message = r.Message
            })
        });
    }

    Order order = await _orderService.CreateAsync(request);
    return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
}
```

---

## API Reference

### Core Interfaces

| Interface | Description |
|-----------|-------------|
| `IBusinessRule` | Sync rule: `IsBroken()`, `Check()`, `Evaluate()`, `Evaluate(context)` (default member falling back to `Evaluate()`) |
| `IAsyncBusinessRule` | Async rule: `IsBrokenAsync()`, `CheckAsync()`, `EvaluateAsync()` |
| `IBusinessRule<TSubject>` | Subject-typed sync rule: `IsBroken(subject)`, `IsBroken(subject, context)`, `Evaluate(subject[, context])`; bind with `.For(subject)` |
| `IAsyncBusinessRule<TSubject>` | Subject-typed async rule: `IsBrokenAsync(subject, context, ct)`, `EvaluateAsync(subject, context, ct)`; bind with `.For(subject)` |
| `IBusinessRuleMetadata` | Base of all four rule interfaces: `Code`, `Message`, and defaulted `Severity`, `Name`, `Description`, `Category`, `Tags` |
| `IComposedBusinessRule` / `IComposedAsyncBusinessRule` | A rule computed from operands: `Operator`, `Operands` (And/Or/Not nodes, groups, composites) |
| `IDecoratedBusinessRule` / `IDecoratedAsyncBusinessRule` | A rule wrapping one other rule: `InnerRule` (cached, conditional and severity-override decorators) |
| `IRuleExecutionObserver` | Sync execution callbacks plus batch hooks `OnBatchStarted`, `OnAfterEvaluate(rule, isBroken, elapsed)`, `OnRuleFaulted`, `OnBatchCompleted` |
| `IAsyncRuleExecutionObserver` | Async execution callbacks plus the async batch hooks |

### Classes

| Class | Description |
|-------|-------------|
| `BaseBusinessRule` | Abstract base for sync rules; virtual `Evaluate()`/`Evaluate(context)` and the `protected virtual Describe(context)` hook |
| `BaseAsyncBusinessRule` | Abstract base for async rules; the `protected virtual Describe(context)` hook |
| `BusinessRuleBuilder` | Fluent builder for sync rules; `WithDetail`, `WithActual`, `WithExpected`, `WithRemediation` |
| `AsyncBusinessRuleBuilder` | Fluent builder for async rules; `WithDetail`, `WithActual`, `WithExpected`, `WithRemediation` |
| `BaseBusinessRule<TSubject>` / `BaseAsyncBusinessRule<TSubject>` | Abstract bases for subject-typed rules; `protected virtual Describe(subject, context)` hook |
| `BusinessRuleBuilder<TSubject>` / `AsyncBusinessRuleBuilder<TSubject>` | Fluent builders for subject-typed rules; `When(subject[, context])` / `WhenAsync(subject[, context[, ct]])` and subject-aware `WithDetail`/`WithActual`/`WithExpected`/`WithRemediation` |
| `BusinessRuleMetadataBuilder<TBuilder>` | Shared base of the four builders: `WithMessage`, `WithSeverity`, `WithName`, `WithDescription`, `WithCategory`, `WithTags` and the context-only detail methods |
| `BoundBusinessRule<TSubject>` / `BoundAsyncBusinessRule<TSubject>` | A typed rule bound to one subject (`Rule`, `Subject`); returned by `.For(subject)`, forwards all metadata |
| `AndBusinessRule<TSubject>` / `OrBusinessRule<TSubject>` / `NotBusinessRule<TSubject>` (+ async twins) | Typed structural nodes returned by the typed `.And()`/`.Or()`/`.Not()`; forward subject and context to their `Operands` |
| `SubjectEvaluation<TSubject>` / `AsyncSubjectEvaluation<TSubject>` | One subject of an `EvaluateEach` batch: `Index`, `Subject`, `Result`, `AllPassed`, `HasBrokenRules`, `BrokenRules` |
| `SubjectEvaluationCollection<TSubject>` / `AsyncSubjectEvaluationCollection<TSubject>` | Positional `IReadOnlyList` returned by `EvaluateEach`: `AllPassed`, `HasBrokenRules`, `Broken`, `Passed`, `BrokenRules`, `TotalDuration` |
| `AndBusinessRule` / `AndAsyncBusinessRule` | Structural node returned by `.And()` (operator `And`) |
| `OrBusinessRule` / `OrAsyncBusinessRule` | Structural node returned by `.Or()` (operator `Or`) |
| `NotBusinessRule` / `NotAsyncBusinessRule` | Structural node returned by `.Not()` (operator `Not`) |
| `RuleGroup` / `AsyncRuleGroup` | Factories: `All`, `Any`, `AtLeast(n)`, `Exactly(n)` |
| `GroupBusinessRule` / `GroupAsyncBusinessRule` | Immutable N-ary quantifier; `WithShortCircuit()`, `WithSeverity()`, async `WithParallelEvaluation()` |
| `RuleGroupQuantifier` | `All`, `Any`, `AtLeast`, `Exactly` |
| `RuleOperator` | `And`, `Or`, `Not`, `All`, `Any`, `AtLeast`, `Exactly` |
| `RuleOutcome` | `Passed`, `Broken`, `Skipped`, `CacheHit`, `NotEvaluated`, `Faulted` |
| `RuleExplanation` | Explanation tree node: `Outcome`, `IsBroken`, `Operator`, `Reason`, `Duration`, `Children`, `BrokenLeaves()`, `ToText()` |
| `CompositeBusinessRule` | Container for multiple sync rules (operator `All`) |
| `CompositeAsyncBusinessRule` | Container for multiple async rules (operator `All`) |
| `CachedBusinessRule` | Time-based caching for sync rules |
| `CachedAsyncBusinessRule` | Time-based caching for async rules |
| `ConditionalBusinessRule` | Conditional execution for sync rules |
| `ConditionalAsyncBusinessRule` | Conditional execution for async rules |
| `SeverityOverrideBusinessRule` / `SeverityOverrideAsyncBusinessRule` | Decorator reporting a different severity (`.WithSeverity()`) |
| `SyncToAsyncRuleAdapter` | Adapts sync rules to async |
| `AsyncToSyncRuleAdapter` | Adapts async rules to sync (blocking) |
| `BusinessRuleChecker` | Batch sync validation; options-taking `CheckAll`/`EvaluateAll` overloads; subject-typed `CheckAll(subject, rules)`, `EvaluateAll(subject, rules)`, `CheckBySeverity/Category/Tags(subject, rules, ...)` and `EvaluateEach(subjects, rules)` |
| `AsyncBusinessRuleChecker` | Batch async validation; options-taking `CheckAllAsync`/`EvaluateAllAsync` overloads; `CheckByTagsAsync`; subject-typed `CheckAllAsync([context,] subject, rules)`, `EvaluateAllAsync`, `CheckBySeverity/Category/TagsAsync` and `EvaluateEachAsync` |
| `RuleEvaluationOptions` / `AsyncRuleEvaluationOptions` | `StopOnFirstFailure`, `OnFault`, `ThrowOn`, `Observer` (async: `RunInParallel`) |
| `FaultPolicy` | `Throw`, `Record`, `TreatAsBroken` |
| `RuleEvaluationEntry` | Ledger line: `Order`, `Rule`/`AsyncRule`, `Code`, `Outcome`, `Result`, `Fault`, `Duration` |
| `BusinessRuleResult` | Immutable result with `Code`, `Message`, `Severity`, `Name`, `Category`, `Tags`, rich `Detail`/`Actual`/`Expected`/`Remediation`/`Data`, and `Details` (broken members of a composed rule); `Flatten()`, `HasTag()`, `WithSeverity()` |
| `RuleResultDetails` | Immutable fluent bag returned by `Describe`: `Detail($"...")`, `.Actual()`, `.Expected()`, `.Remediation()`, `.With(key, value)`, `Empty` |
| `RuleEvaluationResult` | Batch sync evaluation result with ledger (`Entries`, `Faulted`, `HasFaults`, `TotalDuration`, `Slowest`, `ThrowIfAtLeast`) and `ByTag`/`ByCategory` filters |
| `AsyncRuleEvaluationResult` | Batch async evaluation result with the same ledger surface and filters |
| `BusinessRuleValidationException` | Exception when rules are broken; `CollectedRules` (below threshold), `Entries` (ledger), `Flatten()` yields the leaf results, `ByTag`/`ByCategory` filter them |
| `BusinessRuleContext` / `BusinessRuleContext<T>` | Key-value bag passed to rules: `Get`/`TryGet`/`Set`/`ContainsKey`/`Remove` by string or `ContextKey<T>`, `Keys`, `Count`, `Clear`, `FormatProvider`; the generic form adds `Data` |
| `ContextKey<T>` | Typed context key (`Name`, `ValueType`); equatable by ordinal name |
| `ContextKeyNotFoundException` | `KeyNotFoundException` from `Get` naming the missing `Key` and the `AvailableKeys` |
| `ContextValueTypeMismatchException` | `InvalidCastException` from `Get` naming the `Key`, `ActualType` and `RequestedType` |
| `BusinessRuleContextException` | Thrown by the checkers when a rule fails to read the context: `RuleCode`, `Key`, original exception as `InnerException` |
| `RuleTestHelper` | Testing assertions and factories, including `CreateContext` from typed key/value pairs |

### Extension Methods

| Method | Description |
|--------|-------------|
| `.And(rule)` | Broken only when **both** rules are broken (code `A+B`) |
| `.Or(rule)` | Broken when **either** rule is broken (code `A\|B`) |
| `.Not()` | Inverts the rule's broken state (code `!A`) |
| `.For(subject)` | Bind a subject-typed rule to one subject, returning an ordinary `IBusinessRule` / `IAsyncBusinessRule` |
| `.And(rule)` / `.Or(rule)` / `.Not()` on `IBusinessRule<T>` / `IAsyncBusinessRule<T>` | Typed composition with the same truth tables and codes; subject and context are forwarded to the operands |
| `.Explain(context?, captureExceptions?)` | Evaluate once and return the `RuleExplanation` tree (sync) |
| `.ExplainAsync(context, [captureExceptions], ct)` | Evaluate once and return the `RuleExplanation` tree (async) |
| `.ToAsync()` | Convert sync to async |
| `.ToSyncRule()` | Convert async to sync (blocking) |
| `.WithCache(TimeSpan)` | Add caching |
| `.WithCondition(Func)` | Add conditional execution |
| `.WithSeverity(RuleSeverity)` | Wrap in a severity-override decorator (sync and async) |
| `.Check(throwOn)` / `.Check(context, throwOn)` | Check one sync rule, throwing only at or above the severity |
| `.CheckAsync(context, throwOn, ct)` | Check one async rule, throwing only at or above the severity |

---

## License

MIT - use it freely in your projects.
