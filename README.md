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
- [Packages](#packages)
- [Quick Start](#quick-start)
- [Creating Rules](#creating-rules)
- [Fluent Builders](#fluent-builders)
- [Subject-Typed Rules](#subject-typed-rules)
- [Specification Rules and IQueryable Translation](#specification-rules-and-iqueryable-translation)
- [Rule Metadata](#rule-metadata)
- [Rule Composition](#rule-composition)
- [Structural Composition and Explain Trees](#structural-composition-and-explain-trees)
- [Batch Validation](#batch-validation)
- [Context and Data Passing](#context-and-data-passing)
- [Typed Context Keys](#typed-context-keys)
- [Caching](#caching)
- [Conditional Execution](#conditional-execution)
- [Shadow Rollout and Feature-Gated Rules](#shadow-rollout-and-feature-gated-rules)
- [Rule Scopes and Externalized Parameters](#rule-scopes-and-externalized-parameters)
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
- **Specification Rules** - Write a rule once as an expression (`SpecificationBusinessRule<T>.BrokenWhen`), check it in memory with the typed checkers and ask the database for every violating row with `db.Orders.WhereBroken(rule)` / `WhereSatisfies(rule)`; `AllOf`/`AnyOf`/`Not` combine specifications into one translatable lambda (no `Invoke` nodes) and `.ToAsync()` is the async mirror
- **Logical Composition** - Combine rules with `And()`, `Or()`, `Not()` (operators combine the *broken* state of their operands; see the pinned truth table). The operators are real tree nodes that keep their operands and forward the context
- **Rule Groups** - Unambiguous N-ary quantifiers `RuleGroup.All/Any/AtLeast/Exactly` with opt-in short-circuit and (async) parallel evaluation; member failures travel through `BusinessRuleResult.Details`
- **Explain Trees** - `rule.Explain(context)` / `await rule.ExplainAsync(context)` return a navigable, printable tree (Passed/Broken/Skipped/CacheHit/NotEvaluated/Faulted per node, with timing) produced by a single evaluation
- **Rule Metadata** - Name, description, category, tags, and severity
- **Caching** - Time-based caching for expensive rule evaluations, per instance or per cache key (`.Cached(ttl, ctx => tenant)`) so scoped rules cache per tenant
- **Conditional Execution** - Execute rules only when preconditions are met
- **Shadow Rollout and Feature Gates** - `rule.GatedBy(flag, provider, stableKey)` ships a rule dark: `Off`, `Shadow` (evaluated but never blocking; what would have failed lands on `result.ShadowResults` and the `OnShadowViolation` observer hook) or `Enforced`, ramped to a percentage of callers by a stable in-house FNV-1a bucket, all read from an `IRuleFeatureFlagProvider` on every evaluation so the rule class never changes
- **Rule Scopes and Externalized Parameters** - Stop hard-coding `10_000m`: declare `.WithParameter<decimal>("maxOrderAmount", fallback: 10_000m)` and read it in `.When((ctx, p) => ...)`; an `IRuleParameterProvider` supplies the value per `RuleScope` (`RuleScope.Tenant("acme").With("region", "EU")`), so the same checkout rules run with different limits for ACME without an `if` in rule code. `IRuleConditionCompiler` / `IAsyncRuleConditionCompiler` are the seams the Expressions package plugs textual conditions into
- **Rule Adapters** - Convert between sync and async rules
- **Observers** - Monitor rule execution with per-rule and batch lifecycle callbacks (`OnBatchStarted`, `OnAfterEvaluate` with elapsed time, `OnRuleFaulted`, `OnBatchCompleted`) from both `CheckAll` and `EvaluateAll`
- **Batch Validation** - Check multiple rules with fail-fast and parallel execution, or pass a `RuleEvaluationOptions` / `AsyncRuleEvaluationOptions` object
- **Evaluation Ledger** - Every batch result carries one `RuleEvaluationEntry` per rule (order, outcome, duration, fault), `TotalDuration`, `Slowest` and `Faulted`
- **Fault Policy and Severity Threshold** - Decide whether a throwing rule aborts the batch, is recorded, or counts as broken (`FaultPolicy`); make `CheckAll` throw only at or above a severity (`ThrowOn`), and re-severity any rule with `.WithSeverity()`
- **Rich Results** - Every result carries the rule's `Name`, `Category` and `Tags`; a `Describe(context)` hook (or the builders' `WithDetail`/`WithActual`/`WithExpected`/`WithRemediation`) attaches `Detail`, `Actual`, `Expected`, `Remediation` and a `Data` bag on the broken path only, and exceptions and results filter with `ByTag`/`ByCategory`: enough to build an RFC 7807 Problem Details response straight from the engine
- **Typed Context** - Strongly-typed data passing to rules: `BusinessRuleContext<T>.Data` for one object, `ContextKey<T>` for named values read with `context.Get(OrderKeys.Order)` (no cast, no string literal); a failed read throws `ContextKeyNotFoundException` listing the available keys or `ContextValueTypeMismatchException` naming both types, and the checkers wrap either in a `BusinessRuleContextException` that names the failing rule
- **Testing Utilities** - Helper methods for unit testing rules
- **Companion Packages** - Diagnostics (structured logging, OpenTelemetry spans and metrics), Expressions (textual conditions with bound-value explanations), DependencyInjection (assembly scanning, rule runners, scoped registry), Testing (outcome coverage, boundary finder, CsCheck properties) and FeatureManagement (Microsoft.FeatureManagement adapter) ship as separate opt-in packages, so the core stays dependency-free

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

## Packages

`Masterly.BusinessRules` is the zero-dependency core. Companion packages ship from the same repository, with the same version, and plug into the seams the core exposes (observers, `IRuleConditionCompiler`, `IRuleFeatureFlagProvider`, the subject-typed rule interfaces). Install only what you use; the core never references any of them.

| Package | Purpose | Dependencies beyond the core |
|---------|---------|------------------------------|
| `Masterly.BusinessRules.Diagnostics` | Structured `ILogger` observer, OpenTelemetry `ActivitySource` spans and `Meter` instruments for rule evaluation | `Microsoft.Extensions.Logging.Abstractions`, `System.Diagnostics.DiagnosticSource` |
| `Masterly.BusinessRules.Expressions` | Textual rule conditions parsed once and explained with bound values, plugged in through `IRuleConditionCompiler` / `IAsyncRuleConditionCompiler` | none |
| `Masterly.BusinessRules.DependencyInjection` | Assembly scanning, injectable rule runners and the scoped rule registry | `Microsoft.Extensions.DependencyInjection.Abstractions` |
| `Masterly.BusinessRules.Testing` | Outcome coverage, boundary finder and CsCheck-backed rule properties; targets `net8.0` because CsCheck ships no .NET Standard build | `CsCheck` |
| `Masterly.BusinessRules.FeatureManagement` | `Microsoft.FeatureManagement` adapter supplying feature-gated rules with their `RuleRollout` | `Microsoft.FeatureManagement` |

```bash
dotnet add package Masterly.BusinessRules.Diagnostics
dotnet add package Masterly.BusinessRules.Expressions
dotnet add package Masterly.BusinessRules.DependencyInjection
dotnet add package Masterly.BusinessRules.Testing
dotnet add package Masterly.BusinessRules.FeatureManagement
```

All companion packages target `netstandard2.1` except where the table says otherwise. Each one is documented in its own section of this README.

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

## Specification Rules and IQueryable Translation

A *specification rule* is a subject-typed rule whose verdict is written once as an expression tree and used twice: compiled for in-memory evaluation and handed to a LINQ provider to find every row that violates it. Derive from `SpecificationBusinessRule<T>` and override `BrokenWhen` instead of `IsBroken`; the expression is read once, compiled lazily on first use, and the same `Code`, `Message` and `Severity` feed both the exception from `CheckAll` and the query from `WhereBroken`. Both `IsBroken` overloads are sealed, so the in-memory verdict and the translated predicate can never disagree (the context is therefore ignored).

`ISpecificationRule<T>` makes the polarity explicit: `BrokenExpression` selects the violators, `SatisfiedExpression` selects the compliant rows (`NOT(brokenExpression)` over the same parameter). There is no ambiguous `ToExpression()`.

**Combinators are stated in terms of satisfaction**, matching the Specification-pattern vocabulary rather than the broken-state algebra of `And`/`Or`:

| Combinator | Satisfied when | Broken when | Broken expression | Code |
|------------|----------------|-------------|-------------------|------|
| `a.AllOf(b, c)` | every operand is satisfied | **any** operand is broken | `a \|\| b \|\| c` | `ALL(A,B,C)` |
| `a.AnyOf(b, c)` | any operand is satisfied | **every** operand is broken | `a && b && c` | `ANY(A,B,C)` |
| `a.Not()` | `a` is broken | `a` is satisfied | `a.SatisfiedExpression` | `!A` |

Each combinator splices the operands' bodies onto a single parameter with an internal `ParameterReplacer` visitor, so the result is always **one lambda over one parameter with no `Invoke` nodes**, which every LINQ provider can translate. `Not().Not()` returns the original expressions rather than piling up negations.

**Only specifications translate.** `WhereBroken` and `WhereSatisfies` accept `ISpecificationRule<T>` and nothing else, so combining a specification with the typed `.And()`/`.Or()`/`.Not()`, or binding it with `.For(subject)` (the only way to reach `.Cached()`, `.When()`, `.WithSeverity()` and `.GatedBy()`, which exist on the non-generic hierarchies), yields an ordinary rule that still evaluates in memory but is a **compile-time error** to pass to a query. A composition can therefore never silently lose its translatability. The spec `Not()` takes precedence over the typed `Not()` when the receiver is typed as a specification.

**Clocks.** Anything inside `BrokenWhen` that a provider evaluates server-side, such as `DateTime.UtcNow`, runs on the database clock when translated and on the local clock in memory; keep that in mind for rules that straddle midnight.

**Async.** Specifications are the one deliberate exception to the library's "everything exists on both sides" rule: an expression has no asynchronous form, so there is no `IAsyncSpecificationRule`. `rule.ToAsync()` returns an `AsyncSpecificationRuleAdapter<T>` (an `IAsyncBusinessRule<T>` forwarding all metadata and both expressions) that joins async batches, groups and `AsyncBusinessRuleChecker` calls.

**Existence checks** such as "the customer must exist" or "the email must not already exist" are an `AsyncBusinessRuleBuilder<T>` with a repository call, not a specification; a specification describes the row itself. The recipe is three builder calls (see the end of the examples below).

### Specification Examples

```csharp
// Written once: the expression is the rule
public sealed class OrderIsOverdueRule : SpecificationBusinessRule<Order>
{
    public override string Code => "ORDER.OVERDUE";
    public override string Message => "Order is past its due date.";
    public override Expression<Func<Order, bool>> BrokenWhen => o => o.DueDate < DateTime.UtcNow && !o.IsPaid;
}

public sealed class OrderIsBigRule : SpecificationBusinessRule<Order>
{
    public override string Code => "ORDER.BIG";
    public override string Message => "Order total exceeds the approval limit.";
    public override Expression<Func<Order, bool>> BrokenWhen => o => o.Total > 10_000m;
}

OrderIsOverdueRule overdue = new OrderIsOverdueRule();
OrderIsBigRule big = new OrderIsBigRule();

// In memory: the typed checker, with the same Code/Message/Severity as everywhere else
BusinessRuleChecker.CheckAll(order, new ISpecificationRule<Order>[] { overdue, big });
bool isOverdue = overdue.IsBroken(order);                     // compiled once, reused

// In the database: the same expression becomes the WHERE clause
IQueryable<Order> violators = db.Orders.WhereBroken(overdue);   // WHERE DueDate < now AND NOT IsPaid
IQueryable<Order> valid     = db.Orders.WhereSatisfies(overdue); // WHERE NOT (DueDate < now AND NOT IsPaid)

// Combinators stay translatable: one lambda, no Invoke nodes
ISpecificationRule<Order> combo = overdue.AllOf(big).AnyOf(new OrderIsFlaggedRule());
IQueryable<Order> needsAttention = db.Orders.WhereBroken(combo);  // WHERE (overdue OR big) AND flagged
List<Order> toReview = await needsAttention.OrderBy(o => o.DueDate).Take(50).ToListAsync();

// Explicit polarity on the expressions themselves
Expression<Func<Order, bool>> brokenPredicate = combo.BrokenExpression;
Expression<Func<Order, bool>> satisfiedPredicate = combo.SatisfiedExpression;  // NOT(brokenPredicate)

// Minimal async mirror: an adapter, not a second hierarchy
IAsyncBusinessRule<Order> asyncOverdue = overdue.ToAsync();
await AsyncBusinessRuleChecker.CheckAllAsync(context, order, new[] { asyncOverdue }, cancellationToken);

// Still an ordinary typed rule: binds, decorates and composes, but the result is no longer translatable
IBusinessRule bound = overdue.For(order).Cached(TimeSpan.FromMinutes(1));
IBusinessRule<Order> notTranslatable = overdue.And(big);
// db.Orders.WhereBroken(notTranslatable);   // does not compile: WhereBroken accepts ISpecificationRule<T> only

// Existence-check recipe: a repository call is not an expression over the row, so it is an async builder rule
IAsyncBusinessRule<Order> customerMustExist = AsyncBusinessRuleBuilder<Order>.Create("ORDER.CUSTOMER_MISSING")
    .WithMessage("Order references a customer that does not exist.")
    .WhenAsync(async (o, ctx, ct) => !await customers.ExistsAsync(o.CustomerId, ct))
    .Build();
await AsyncBusinessRuleChecker.CheckAllAsync(context, order, new[] { asyncOverdue, customerMustExist }, cancellationToken);
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

Every composed rule keeps its shape. The `And()`, `Or()` and `Not()` operators, the composites and the new rule groups all implement `IComposedBusinessRule` (`Operator`, `Operands`), while `CachedBusinessRule`, `ConditionalBusinessRule`, `SeverityOverrideBusinessRule` and `FeatureGatedBusinessRule` implement `IDecoratedBusinessRule` (`InnerRule`); the async side mirrors this with `IComposedAsyncBusinessRule` and `IDecoratedAsyncBusinessRule`, and both adapters expose their wrapped rule as `InnerRule`. Tooling can therefore walk any rule tree without knowing the concrete node types.

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

**Per instance or per key.** By default a cached rule holds **one verdict per instance**, whatever the context, so it must never wrap a rule whose verdict depends on the context (scoped parameters, a tenant id, a feature cohort). Pass a `cacheKeySelector` (`Func<BusinessRuleContext, string>`) to cache **one verdict per key** instead: `rule.Cached(ttl, ctx => ctx.Get(RuleParameters.ScopeKey).ToString())` keeps ACME's verdict apart from Globex's. `CachesPerKey` reports which mode an instance is in. The parameterless `IsBroken()` / `Check()` have no context to select a key from and always use a dedicated shared entry. Expired entries are purged whenever a new key is stored, so the cache holds at most one live entry per key seen within the duration; keep the key space bounded (tenants, regions), never per request. A selector that returns null throws `RuleConfigurationException` naming the rule's `Code`.

**Cache Invalidation:**
- `InvalidateCache()` / `InvalidateCacheAsync()`: Clears every entry (the shared one and all keys)
- `InvalidateCache(key)` / `InvalidateCacheAsync(key)`: Clears one key; other keys and the shared entry are untouched

**Extension Methods:**
- `.Cached(TimeSpan)`: Fluent syntax for adding per-instance caching
- `.Cached(TimeSpan, cacheKeySelector)`: Fluent syntax for adding per-key caching

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
IBusinessRule cachedExtRule = new ExpensiveRule().Cached(TimeSpan.FromMinutes(5));
IAsyncBusinessRule cachedExtAsync = new DatabaseRule().Cached(TimeSpan.FromSeconds(30));

// Per-key caching: a rule reading scoped parameters is cached per tenant
CachedBusinessRule perTenant = orderLimitRule.Cached(TimeSpan.FromMinutes(5), ctx => ctx.Get(RuleParameters.ScopeKey).ToString());
perTenant.IsBroken(acmeContext);     // evaluates for ACME
perTenant.IsBroken(globexContext);   // evaluates for Globex, separate entry
perTenant.IsBroken(acmeContext);     // ACME cache hit
perTenant.InvalidateCache("tenant=acme");   // drop one key only
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

## Shadow Rollout and Feature-Gated Rules

Ship a new rule dark and ramp it from your flag provider without touching the rule class. `rule.GatedBy(flag, provider, stableKey)` wraps any rule (sync or async) in a `FeatureGatedBusinessRule` / `FeatureGatedAsyncBusinessRule` that asks an `IRuleFeatureFlagProvider` / `IAsyncRuleFeatureFlagProvider` for the flag's `RuleRollout` on **every evaluation**, so moving a rule from observe-only to blocking is a configuration flip. All seven metadata members are forwarded from the inner rule.

| `RuleRolloutMode` | Inner rule | Gated rule reports | Visible through |
|-------------------|------------|--------------------|-----------------|
| `Off` (or the caller is outside the rollout cohort) | not evaluated | passes | a `Skipped` `GATED` node in `Explain` |
| `Shadow` | evaluated | passes | `observer.OnShadowViolation(rule, result)` and `result.ShadowResults` |
| `Enforced` | evaluated | its own verdict | like any other rule |

**Ramping.** A `RuleRollout` carries a `Percent` (0..100). A partial rollout needs a `stableKey` selector (a customer id, a tenant id) that `RuleRolloutBucket` hashes with in-house 32-bit FNV-1a into buckets 0..99; the caller is inside the cohort when its bucket is below the percent. The flag name is mixed into the hash, so different flags ramp over different customers, and a customer inside a 10% cohort stays inside when the rollout grows to 20%. A rollout at 100% never calls the stable key selector; a partial rollout without one throws `RuleConfigurationException`.

**Providers.** Pass the provider to `GatedBy(...)`, or store one on the context under `RuleFeatureFlags.ContextKey` (`RuleFeatureFlags.AsyncContextKey` for async providers): a provider on the context overrides the injected one for that evaluation, which lets a test or a canary pipeline force a rollout. Async gated rules try the async provider first, then the sync one. When no provider can be found anywhere the evaluation throws `RuleConfigurationException` naming the rule's `Code`. `InMemoryRuleFeatureFlagProvider` implements both interfaces, is thread-safe and can be flipped at any time; `RuleTestHelper.CreateFlagProvider(flag, mode, percent)` builds one for tests. An adapter for `Microsoft.FeatureManagement` is planned as the sibling package `Masterly.BusinessRules.FeatureManagement`.

**Shadow results.** Both checkers collect what shadow-mode rules would have reported on `RuleEvaluationResult.ShadowResults` / `AsyncRuleEvaluationResult.ShadowResults` (`HasShadowResults`), whether the gated rule is a batch member or nested inside a group, a cache or an adapter. These results are not in `BrokenRules`, never make `CheckAll` throw and never affect `AllPassed`. The options-taking overloads additionally raise the observer hook `OnShadowViolation(rule, result)` / `OnShadowViolationAsync(rule, result, ct)` right after the rule's own callbacks (in a parallel async batch, once every rule has completed and before `OnBatchCompletedAsync`); it is a default interface member, so existing observers keep compiling.

**A gate needs a context.** The sync `IsBroken()` / `Check()` without a context, and therefore a context-less `BusinessRuleChecker` call or an `And`/`Or` composition evaluated without a context, evaluate the inner rule **un-gated** and never consult the provider, so composition can never throw for a missing provider. Pass a context (`CheckAll(context, rules)`) to gate. The async checkers always evaluate with a context.

**Interaction with `CachedBusinessRule`.** Put the cache *inside* the gate, never outside: `rule.Cached(ttl).GatedBy(...)` caches the expensive inner verdict while the rollout is still decided on every call, whereas `rule.GatedBy(...).Cached(ttl)` caches the gate's pass/fail and freezes a flag flip (or a shadow pass) for the whole cache duration. Remember that `CachedBusinessRule` holds one verdict per instance by default, so a cached inner rule serves the same verdict to every cohort member; pass a `cacheKeySelector` (`rule.Cached(ttl, ctx => tenant)`) when the inner verdict depends on the caller.

**Explain.** A closed gate is a `Skipped` `GATED` node whose `Reason` says why (`flag 'credit-score-v2' is off` or `flag 'credit-score-v2' excludes bucket 57 (10%)`) with the inner rule as a `NotEvaluated` child; an enforced evaluation is a `GATED` node; a shadow evaluation is a `SHADOW` node that reports `Passed` with the reason `shadow violation` and the broken inner rule as its child; an un-gated evaluation (no context) is a `GATED` node with the reason `no context: evaluated un-gated`.

### Feature Gate Examples

```csharp
// Ship dark: the rule runs, violations are observed, nobody is blocked
InMemoryRuleFeatureFlagProvider flags = new InMemoryRuleFeatureFlagProvider()
    .Set("credit-score-v2", RuleRolloutMode.Shadow)
    .Set("sanctions-check", RuleRolloutMode.Enforced, percent: 10);

IBusinessRule gated = new NewCreditScoreRule(customer)
    .GatedBy("credit-score-v2", flags, stableKey: ctx => ctx.Get<string>("customerId"));

IAsyncBusinessRule ramped = new SanctionsCheckRule(api)
    .GatedBy("sanctions-check", flags, stableKey: ctx => ctx.Get<string>("customerId"));

BusinessRuleContext context = RuleTestHelper.CreateContext(("customerId", customer.Id));
RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(new[] { gated, existingRule }, context, new RuleEvaluationOptions
{
    Observer = new ShadowMetricsObserver()
});

foreach (BusinessRuleResult wouldHaveFailed in result.ShadowResults)
    Console.WriteLine($"[SHADOW] {wouldHaveFailed.Code}: {wouldHaveFailed.Message}");

// Ramp and enforce from the provider; the rule class is untouched
flags.Set("credit-score-v2", RuleRolloutMode.Enforced, percent: 10);   // blocks 10% of customers
flags.Set("credit-score-v2", RuleRolloutMode.Enforced);                // blocks everybody

// Override per request through the context (tests, canary traffic)
context.Set(RuleFeatureFlags.ContextKey, RuleTestHelper.CreateFlagProvider("credit-score-v2", RuleRolloutMode.Enforced));

// The observer hook is a default interface member
public sealed class ShadowMetricsObserver : IRuleExecutionObserver
{
    public void OnBeforeEvaluate(IBusinessRule rule) { }
    public void OnAfterEvaluate(IBusinessRule rule, BusinessRuleResult? result) { }
    public void OnRuleBroken(IBusinessRule rule, BusinessRuleResult result) { }

    public void OnShadowViolation(IBusinessRule rule, BusinessRuleResult result)
        => Console.WriteLine($"{rule.Code} would have failed: {result.Message}");
}

// Cache inside the gate, never outside
IBusinessRule right = new ExpensiveRule().Cached(TimeSpan.FromMinutes(5)).GatedBy("expensive-v2", flags);
IBusinessRule wrong = new ExpensiveRule().GatedBy("expensive-v2", flags).Cached(TimeSpan.FromMinutes(5)); // freezes the flag for 5 minutes

// Explain shows the gate decision and its reason
RuleExplanation explanation = gated.Explain(context);
Console.WriteLine($"{explanation.Operator} {explanation.Outcome} {explanation.Reason}");
// Enforced and broken:  GATED Broken
// Shadow and broken:    SHADOW Passed shadow violation
// Flag off:             GATED Skipped flag 'credit-score-v2' is off
```

---

## Rule Scopes and Externalized Parameters

Multi-tenant SaaS wants "the same checkout rules, different limits for ACME" with no `if (tenant == "acme")` in rule code. Declare the limit as a **named parameter with a mandatory fallback**, read it from the `RuleParameters` handed to the `(ctx, p) => ...` condition, and let an `IRuleParameterProvider` supply the value for the **scope** of the evaluation. The provider is consulted on every evaluation, so changing a limit never rebuilds a rule; without any provider every parameter resolves to its fallback, so a parameterless `Check()` or an `And`/`Or` composition can never throw for a missing value.

**`RuleScope`** is an immutable set of key/value pairs (`RuleScope.Global`, `RuleScope.Tenant("acme")`, `.With("region", "EU")`) with value equality (pair order does not matter), `Specificity` (the pair count) and `AppliesTo(requested)`: a scope applies to a request when every one of its pairs is in the request, so `Tenant("acme")` applies to `Tenant("acme").With("region", "EU")` and `Global` applies to everything. A provider or a registry picks the most specific applying layer. `TenantId`, `TryGet(key)`, `ContainsKey(key)` and `ToString()` (`region=EU;tenant=acme`, keys sorted) complete the type.

**Providers.** `IRuleParameterProvider.TryGet<T>(name, scope, out value)` and the async twin `IAsyncRuleParameterProvider.TryGetAsync<T>(name, scope, ct)` returning `RuleParameterLookup<T>` (`Found`, `Value`, `Missing`, `Of(value)`, `GetValueOrFallback`). `InMemoryRuleParameterProvider` implements both, is thread-safe, layers values per scope (`Set(name, value)` for everybody, `Set(name, value, scope)` for a layer) and returns the most specific applying value; among equally specific layers the most recently set one wins. A value stored with another type than the one requested throws `InvalidCastException` naming the parameter, the scope and both types: a configuration error, never a silent fallback. `RuleTestHelper.CreateParameterProvider(name, value, scope?)` builds one for tests. The sibling DependencyInjection package will bind a provider to `IConfiguration` / `IOptionsMonitor` and host the scoped `RuleRegistry` (named rule sets with per-tenant overrides); the Expressions package resolves `@maxOrderAmount` nodes through the same provider.

**Wiring.** All four builders take `.WithParameters(provider)` and `.WithParameter<T>(name, fallback)` (a name may be declared once per builder). The sync builders add `When((ctx, p) => ...)` / `When((subject, ctx, p) => ...)`; the async builders add `WhenAsync((ctx, p, ct) => ...)` / `WhenAsync((subject, ctx, p, ct) => ...)` (the token is part of the signature because a two-parameter `(ctx, p)` lambda would be ambiguous with `(ctx, ct)`) and also accept an `IAsyncRuleParameterProvider`, which they prefer over a sync one. The scope of an evaluation is read from the context under `RuleParameters.ScopeKey`; absent means the global scope. A provider stored on the context under `RuleParameters.ContextKey` (`RuleParameters.AsyncContextKey` for async providers) **overrides** the injected one for that evaluation; it is an optional override for tests and request pipelines, not the primary wiring, so the context never becomes a service locator. A reserved key that is present but holds a value of another type (a tenant name stored as a plain string under `RuleParameters.ScopeKeyName`, say) throws `ContextValueTypeMismatchException` (wrapped by the checkers into `BusinessRuleContextException`) rather than silently resolving the global scope. `RuleParameters` exposes `Get<T>(name)` (throws `KeyNotFoundException` listing the declared names, or `InvalidCastException`), `TryGet<T>`, `Contains`, `Names`, `Count` and the resolved `Scope`.

**Class-based rules** take the provider through their constructor and call `TryGet` in `IsBroken(context)`; the scoped value belongs in `Describe` (`Expected`), since `Message` stays a finished string.

**Caching.** A cached rule holds one verdict per instance by default, which is wrong for a scoped rule; cache it per key with `rule.Cached(ttl, ctx => ctx.Get(RuleParameters.ScopeKey).ToString())` (see [Caching](#caching)).

**Condition compiler seam.** `IRuleConditionCompiler.Compile(expression)` returns the `Func<BusinessRuleContext, bool>` a built rule evaluates, and `IAsyncRuleConditionCompiler.Compile(expression)` the async `Func<BusinessRuleContext, CancellationToken, Task<bool>>`; `BusinessRuleBuilder.When(compiler, "amount > 10000")` and `AsyncBusinessRuleBuilder.WhenAsync(compiler, "...")` compile at builder time so a malformed expression fails before the first evaluation. Core never parses text; the `Masterly.BusinessRules.Expressions` package implements the seam.

### Scoped Parameter Examples

```csharp
// One provider, layered values: 10 000 for everybody, 50 000 for ACME, 70 000 for ACME in the EU
InMemoryRuleParameterProvider parameters = new InMemoryRuleParameterProvider()
    .Set("maxOrderAmount", 10_000m)
    .Set("maxOrderAmount", 50_000m, RuleScope.Tenant("acme"))
    .Set("maxOrderAmount", 70_000m, RuleScope.Tenant("acme").With("region", "EU"));

// The rule declares the parameter with a fallback and reads it from p; no tenant logic inside
IBusinessRule orderLimit = BusinessRuleBuilder.Create("ORDER.TOO_LARGE")
    .WithMessage("Order amount exceeds the limit.")
    .WithParameters(parameters)
    .WithParameter<decimal>("maxOrderAmount", fallback: 10_000m)
    .When((ctx, p) => ctx.Get<decimal>("amount") > p.Get<decimal>("maxOrderAmount"))
    .Build();

// The scope travels on the context
BusinessRuleContext acmeEu = RuleTestHelper.CreateContext(("amount", 60_000m));
acmeEu.Set(RuleParameters.ScopeKey, RuleScope.Tenant("acme").With("region", "EU"));
orderLimit.IsBroken(acmeEu);                                   // false: 60 000 <= 70 000

BusinessRuleContext globex = RuleTestHelper.CreateContext(("amount", 60_000m));
globex.Set(RuleParameters.ScopeKey, RuleScope.Tenant("globex"));
orderLimit.IsBroken(globex);                                   // true: falls back to the global 10 000

orderLimit.IsBroken();                                         // parameterless: injected provider, global scope, never throws

// Subject-typed and async forms
IBusinessRule<Order> typed = BusinessRuleBuilder<Order>.Create("ORDER.TOO_LARGE")
    .WithParameters(parameters)
    .WithParameter<decimal>("maxOrderAmount", 10_000m)
    .When((order, ctx, p) => order.Total > p.Get<decimal>("maxOrderAmount"))
    .Build();

IAsyncBusinessRule asyncRule = AsyncBusinessRuleBuilder.Create("ORDER.TOO_LARGE")
    .WithParameters(parameters)                                // sync or async provider; async is preferred
    .WithParameter<decimal>("maxOrderAmount", 10_000m)
    .WhenAsync((ctx, p, ct) => Task.FromResult(ctx.Get<decimal>("amount") > p.Get<decimal>("maxOrderAmount")))
    .Build();

// Override the provider per request (tests, canary traffic)
acmeEu.Set(RuleParameters.ContextKey, RuleTestHelper.CreateParameterProvider("maxOrderAmount", 1m));

// Class-based rule: inject the provider, show the scoped limit in the details
public sealed class OrderLimitRule : BaseBusinessRule
{
    private const string LimitName = "maxOrderAmount";
    private const decimal DefaultLimit = 10_000m;
    private readonly Order _order;
    private readonly IRuleParameterProvider _parameters;

    public OrderLimitRule(Order order, IRuleParameterProvider parameters) { _order = order; _parameters = parameters; }

    public override string Code => "ORDER.TOO_LARGE";
    public override string Message => "Order amount exceeds the limit.";

    public override bool IsBroken() => _order.Total > Limit(null);
    public override bool IsBroken(BusinessRuleContext context) => _order.Total > Limit(context);

    protected override RuleResultDetails Describe(BusinessRuleContext? context)
        => RuleResultDetails.Detail($"Order total {_order.Total} exceeds {Limit(context)}").Actual(_order.Total).Expected(Limit(context));

    private decimal Limit(BusinessRuleContext? context)
    {
        RuleScope? scope = context != null && context.TryGet(RuleParameters.ScopeKey, out RuleScope s) ? s : null;
        return _parameters.TryGet(LimitName, scope, out decimal limit) ? limit : DefaultLimit;
    }
}

// A custom provider bound to configuration
public sealed class ConfigurationParameterProvider : IRuleParameterProvider
{
    public bool TryGet<T>(string name, RuleScope? scope, out T value)
        => _settings.TryResolve(name, scope?.TenantId, out value);   // your lookup; return false for unknown names
}

// Condition compiler seam (implemented by Masterly.BusinessRules.Expressions)
IBusinessRule fromText = BusinessRuleBuilder.Create("ORDER.TOO_LARGE").When(compiler, "amount > @maxOrderAmount").Build();
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
    - `OnShadowViolation(IBusinessRule rule, BusinessRuleResult result)`: Called when a shadow-mode gated rule observed a violation it did not report as broken (see [Shadow Rollout and Feature-Gated Rules](#shadow-rollout-and-feature-gated-rules))

- `IAsyncRuleExecutionObserver`: For async rule evaluation
  - Same methods with async signatures and CancellationToken (`OnBatchStartedAsync`, `OnAfterEvaluateAsync(rule, isBroken, elapsed, ct)`, `OnRuleFaultedAsync`, `OnBatchCompletedAsync`, `OnShadowViolationAsync`)

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
- `ShadowResults`, `HasShadowResults`: What shadow-mode gated rules would have reported; not counted as broken (see [Shadow Rollout and Feature-Gated Rules](#shadow-rollout-and-feature-gated-rules))

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
- `CreateFlagProvider(flag, mode, percent)`: An `InMemoryRuleFeatureFlagProvider` (sync and async) with one flag set; other flags are `Off`
- `CreateParameterProvider(name, value, scope?)`: An `InMemoryRuleParameterProvider` (sync and async) with one value set, globally or for a `RuleScope`; other parameters resolve to the rule's fallback

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
| `ISpecificationRule<TSubject>` | Subject-typed rule with an explicit-polarity expression pair: `BrokenExpression`, `SatisfiedExpression`; the only input `WhereBroken`/`WhereSatisfies` accept |
| `IBusinessRuleMetadata` | Base of all four rule interfaces: `Code`, `Message`, and defaulted `Severity`, `Name`, `Description`, `Category`, `Tags` |
| `IComposedBusinessRule` / `IComposedAsyncBusinessRule` | A rule computed from operands: `Operator`, `Operands` (And/Or/Not nodes, groups, composites) |
| `IDecoratedBusinessRule` / `IDecoratedAsyncBusinessRule` | A rule wrapping one other rule: `InnerRule` (cached, conditional, severity-override and feature-gated decorators) |
| `IRuleExecutionObserver` | Sync execution callbacks plus batch hooks `OnBatchStarted`, `OnAfterEvaluate(rule, isBroken, elapsed)`, `OnRuleFaulted`, `OnBatchCompleted`, `OnShadowViolation` |
| `IAsyncRuleExecutionObserver` | Async execution callbacks plus the async batch hooks and `OnShadowViolationAsync` |
| `IRuleFeatureFlagProvider` / `IAsyncRuleFeatureFlagProvider` | Supply a flag's `RuleRollout` to gated rules: `GetRollout(flag, context)` / `GetRolloutAsync(flag, context, ct)` |
| `IRuleParameterProvider` / `IAsyncRuleParameterProvider` | Supply named, scope-layered parameter values to built rules: `TryGet<T>(name, scope, out value)` / `TryGetAsync<T>(name, scope, ct)` returning `RuleParameterLookup<T>` |
| `IRuleConditionCompiler` / `IAsyncRuleConditionCompiler` | The Expressions seam: `Compile(expression)` returns the sync / async broken-condition delegate a built rule evaluates (`When(compiler, text)` / `WhenAsync(compiler, text)`) |

### Classes

| Class | Description |
|-------|-------------|
| `BaseBusinessRule` | Abstract base for sync rules; virtual `Evaluate()`/`Evaluate(context)` and the `protected virtual Describe(context)` hook |
| `BaseAsyncBusinessRule` | Abstract base for async rules; the `protected virtual Describe(context)` hook |
| `BusinessRuleBuilder` | Fluent builder for sync rules; `WithDetail`, `WithActual`, `WithExpected`, `WithRemediation`; `When((ctx, p) => ...)` with parameters and `When(compiler, expression)` |
| `AsyncBusinessRuleBuilder` | Fluent builder for async rules; `WithDetail`, `WithActual`, `WithExpected`, `WithRemediation`; `WhenAsync((ctx, p, ct) => ...)` with parameters, `WithParameters(asyncProvider)` and `WhenAsync(compiler, expression)` |
| `BaseBusinessRule<TSubject>` / `BaseAsyncBusinessRule<TSubject>` | Abstract bases for subject-typed rules; `protected virtual Describe(subject, context)` hook |
| `BusinessRuleBuilder<TSubject>` / `AsyncBusinessRuleBuilder<TSubject>` | Fluent builders for subject-typed rules; `When(subject[, context[, p]])` / `WhenAsync(subject[, context[, ct]])` and `WhenAsync((subject, ctx, p, ct) => ...)`, subject-aware `WithDetail`/`WithActual`/`WithExpected`/`WithRemediation` |
| `BusinessRuleMetadataBuilder<TBuilder>` | Shared base of the four builders: `WithMessage`, `WithSeverity`, `WithName`, `WithDescription`, `WithCategory`, `WithTags`, the context-only detail methods, `WithParameters(provider)` and `WithParameter<T>(name, fallback)` |
| `RuleScope` | Immutable key/value scope with value equality: `Global`, `Tenant(id)`, `With(key, value)`, `Specificity`, `IsGlobal`, `TenantId`, `TryGet`, `ContainsKey`, `AppliesTo(requested)`, `Pairs` |
| `RuleParameters` | The resolved parameters handed to a `(ctx, p)` condition: `Get<T>`, `TryGet<T>`, `Contains`, `Names`, `Count`, `Scope`, `Empty`; reserved context keys `ContextKey`, `AsyncContextKey`, `ScopeKey` |
| `RuleParameterLookup<T>` | Readonly struct returned by async providers: `Found`, `Value`, `Missing`, `Of(value)`, `GetValueOrFallback(fallback)` |
| `InMemoryRuleParameterProvider` | Thread-safe provider implementing both parameter interfaces with scope layering: `Set(name, value[, scope])`, `Remove(name[, scope])`, `Count`; most specific applying scope wins |
| `BoundBusinessRule<TSubject>` / `BoundAsyncBusinessRule<TSubject>` | A typed rule bound to one subject (`Rule`, `Subject`); returned by `.For(subject)`, forwards all metadata |
| `AndBusinessRule<TSubject>` / `OrBusinessRule<TSubject>` / `NotBusinessRule<TSubject>` (+ async twins) | Typed structural nodes returned by the typed `.And()`/`.Or()`/`.Not()`; forward subject and context to their `Operands` |
| `SpecificationBusinessRule<TSubject>` | Abstract base for expression rules: override `BrokenWhen`; `BrokenExpression`/`SatisfiedExpression` cached, `IsBroken` sealed and lazily compiled; `Describe(subject, context)` hook inherited |
| `AllOfSpecificationRule<TSubject>` / `AnyOfSpecificationRule<TSubject>` / `NotSpecificationRule<TSubject>` | Specification nodes returned by `.AllOf()`/`.AnyOf()`/`.Not()` (`Operator`, `Operands`); one spliced lambda, codes `ALL(A,B)`, `ANY(A,B)`, `!A` |
| `ComposedSpecificationRule<TSubject>` | Shared base of the three specification nodes (cannot be derived from outside the library) |
| `AsyncSpecificationRuleAdapter<TSubject>` | `IAsyncBusinessRule<TSubject>` over a specification (`InnerRule`, `BrokenExpression`, `SatisfiedExpression`); returned by `.ToAsync()` |
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
| `CachedBusinessRule` | Time-based caching for sync rules, per instance or per key (`cacheKeySelector`): `CachesPerKey`, `InvalidateCache()`, `InvalidateCache(key)` |
| `CachedAsyncBusinessRule` | Time-based caching for async rules, per instance or per key (`cacheKeySelector`): `CachesPerKey`, `InvalidateCacheAsync()`, `InvalidateCacheAsync(key)` |
| `ConditionalBusinessRule` | Conditional execution for sync rules |
| `ConditionalAsyncBusinessRule` | Conditional execution for async rules |
| `SeverityOverrideBusinessRule` / `SeverityOverrideAsyncBusinessRule` | Decorator reporting a different severity (`.WithSeverity()`) |
| `FeatureGatedBusinessRule` / `FeatureGatedAsyncBusinessRule` | Decorator gating a rule behind a feature flag (`.GatedBy()`): `Flag`, `Provider` (async also `AsyncProvider`), `HasStableKey`; `Off`/`Shadow`/`Enforced` and percentage ramping read from the provider on every evaluation |
| `RuleRollout` | Readonly struct returned by providers: `Mode`, `Percent`, `IsOff`, `IsComplete`, `Includes(bucket)`; factories `Off`, `Shadow(percent)`, `Enforced(percent)` |
| `RuleRolloutMode` | `Off`, `Shadow`, `Enforced` |
| `RuleRolloutBucket` | In-house FNV-1a bucketing: `Compute(flag, stableKey)` (0..99), `Hash(text)`, `BucketCount` |
| `RuleFeatureFlags` | Reserved context keys overriding the injected provider: `ContextKey` (sync), `AsyncContextKey` (async) |
| `InMemoryRuleFeatureFlagProvider` | Thread-safe provider implementing both provider interfaces: `Set(flag, rollout)`, `Set(flag, mode, percent)`, `Remove`, `TryGet`, `DefaultRollout` |
| `RuleConfigurationException` | Thrown when a gated rule has no provider, a partial rollout has no stable key, or a cache key selector returns null: `RuleCode` |
| `SyncToAsyncRuleAdapter` | Adapts sync rules to async |
| `AsyncToSyncRuleAdapter` | Adapts async rules to sync (blocking) |
| `BusinessRuleChecker` | Batch sync validation; options-taking `CheckAll`/`EvaluateAll` overloads; subject-typed `CheckAll(subject, rules)`, `EvaluateAll(subject, rules)`, `CheckBySeverity/Category/Tags(subject, rules, ...)` and `EvaluateEach(subjects, rules)` |
| `AsyncBusinessRuleChecker` | Batch async validation; options-taking `CheckAllAsync`/`EvaluateAllAsync` overloads; `CheckByTagsAsync`; subject-typed `CheckAllAsync([context,] subject, rules)`, `EvaluateAllAsync`, `CheckBySeverity/Category/TagsAsync` and `EvaluateEachAsync` |
| `RuleEvaluationOptions` / `AsyncRuleEvaluationOptions` | `StopOnFirstFailure`, `OnFault`, `ThrowOn`, `Observer` (async: `RunInParallel`) |
| `FaultPolicy` | `Throw`, `Record`, `TreatAsBroken` |
| `RuleEvaluationEntry` | Ledger line: `Order`, `Rule`/`AsyncRule`, `Code`, `Outcome`, `Result`, `Fault`, `Duration` |
| `BusinessRuleResult` | Immutable result with `Code`, `Message`, `Severity`, `Name`, `Category`, `Tags`, rich `Detail`/`Actual`/`Expected`/`Remediation`/`Data`, and `Details` (broken members of a composed rule); `Flatten()`, `HasTag()`, `WithSeverity()` |
| `RuleResultDetails` | Immutable fluent bag returned by `Describe`: `Detail($"...")`, `.Actual()`, `.Expected()`, `.Remediation()`, `.With(key, value)`, `Empty` |
| `RuleEvaluationResult` | Batch sync evaluation result with ledger (`Entries`, `Faulted`, `HasFaults`, `TotalDuration`, `Slowest`, `ThrowIfAtLeast`), `ShadowResults`/`HasShadowResults` and `ByTag`/`ByCategory` filters |
| `AsyncRuleEvaluationResult` | Batch async evaluation result with the same ledger surface, shadow results and filters |
| `BusinessRuleValidationException` | Exception when rules are broken; `CollectedRules` (below threshold), `Entries` (ledger), `Flatten()` yields the leaf results, `ByTag`/`ByCategory` filter them |
| `BusinessRuleContext` / `BusinessRuleContext<T>` | Key-value bag passed to rules: `Get`/`TryGet`/`Set`/`ContainsKey`/`Remove` by string or `ContextKey<T>`, `Keys`, `Count`, `Clear`, `FormatProvider`; the generic form adds `Data` |
| `ContextKey<T>` | Typed context key (`Name`, `ValueType`); equatable by ordinal name |
| `ContextKeyNotFoundException` | `KeyNotFoundException` from `Get` naming the missing `Key` and the `AvailableKeys` |
| `ContextValueTypeMismatchException` | `InvalidCastException` from `Get` naming the `Key`, `ActualType` and `RequestedType` |
| `BusinessRuleContextException` | Thrown by the checkers when a rule fails to read the context: `RuleCode`, `Key`, original exception as `InnerException` |
| `RuleTestHelper` | Testing assertions and factories, including `CreateContext` from typed key/value pairs, `CreateFlagProvider(flag, mode, percent)` and `CreateParameterProvider(name, value, scope?)` |

### Extension Methods

| Method | Description |
|--------|-------------|
| `.And(rule)` | Broken only when **both** rules are broken (code `A+B`) |
| `.Or(rule)` | Broken when **either** rule is broken (code `A\|B`) |
| `.Not()` | Inverts the rule's broken state (code `!A`) |
| `.For(subject)` | Bind a subject-typed rule to one subject, returning an ordinary `IBusinessRule` / `IAsyncBusinessRule` |
| `.And(rule)` / `.Or(rule)` / `.Not()` on `IBusinessRule<T>` / `IAsyncBusinessRule<T>` | Typed composition with the same truth tables and codes; subject and context are forwarded to the operands |
| `.AllOf(others)` / `.AnyOf(others)` / `.Not()` on `ISpecificationRule<T>` | Satisfaction-stated specification combinators producing one translatable lambda (codes `ALL(A,B)`, `ANY(A,B)`, `!A`) |
| `.ToAsync()` on `ISpecificationRule<T>` | Adapt a specification to `IAsyncBusinessRule<T>` (the whole async side of specifications) |
| `queryable.WhereBroken(spec)` / `queryable.WhereSatisfies(spec)` | Append one `Where` with the specification's `BrokenExpression` / `SatisfiedExpression`; accept `ISpecificationRule<T>` only |
| `.Explain(context?, captureExceptions?)` | Evaluate once and return the `RuleExplanation` tree (sync) |
| `.ExplainAsync(context, [captureExceptions], ct)` | Evaluate once and return the `RuleExplanation` tree (async) |
| `.ToAsync()` | Convert sync to async |
| `.ToSyncRule()` | Convert async to sync (blocking) |
| `.Cached(TimeSpan)` | Add per-instance caching |
| `.Cached(TimeSpan, cacheKeySelector)` | Add per-key caching (one verdict per tenant, region, ...) |
| `.WithCondition(Func)` | Add conditional execution |
| `.WithSeverity(RuleSeverity)` | Wrap in a severity-override decorator (sync and async) |
| `.GatedBy(flag, provider, stableKey?)` / `.GatedBy(flag, stableKey?)` | Wrap in a feature gate read from the provider on every evaluation (sync and async; the async form accepts a sync or an async provider, and the provider-less form resolves it from `RuleFeatureFlags.ContextKey`/`AsyncContextKey`) |
| `.Check(throwOn)` / `.Check(context, throwOn)` | Check one sync rule, throwing only at or above the severity |
| `.CheckAsync(context, throwOn, ct)` | Check one async rule, throwing only at or above the severity |

### Packages

| Package | Contents |
|---------|----------|
| `Masterly.BusinessRules` | The core: every type listed above; no dependencies |
| `Masterly.BusinessRules.Diagnostics` | `ILogger` observer and OpenTelemetry tracing/metrics observers and decorators |
| `Masterly.BusinessRules.Expressions` | Rule expression parser, expression rules and bound-value explanations |
| `Masterly.BusinessRules.DependencyInjection` | `IServiceCollection` registration, rule runners and the scoped rule registry |
| `Masterly.BusinessRules.Testing` | Outcome coverage, boundary finder and CsCheck-backed rule properties |
| `Masterly.BusinessRules.FeatureManagement` | `IRuleFeatureFlagProvider` / `IAsyncRuleFeatureFlagProvider` over `Microsoft.FeatureManagement` |

---

## License

MIT - use it freely in your projects.
