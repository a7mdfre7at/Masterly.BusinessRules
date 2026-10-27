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
- [Microsoft.FeatureManagement Adapter](#microsoftfeaturemanagement-adapter)
- [Rule Scopes and Externalized Parameters](#rule-scopes-and-externalized-parameters)
- [Rule Expressions](#rule-expressions)
- [Dependency Injection](#dependency-injection)
- [Rule Adapters](#rule-adapters)
- [Observers](#observers)
- [Diagnostics: Logging, Tracing and Metrics](#diagnostics-logging-tracing-and-metrics)
- [Evaluation Results](#evaluation-results)
- [Evaluation Ledger, Options and Fault Policy](#evaluation-ledger-options-and-fault-policy)
- [Rich Results](#rich-results)
- [Testing Utilities](#testing-utilities)
- [Testing Package: Coverage, Boundaries and Properties](#testing-package-coverage-boundaries-and-properties)
- [Real-World Examples](#real-world-examples)
- [API Reference](#api-reference)
- [License](#license)

---

## Features

- **Sync & Async Rules** - Full support for both synchronous and asynchronous business rules, with every feature mirrored on both sides (specifications are the one documented exception)
- **Fluent Builders** - Create rules inline without subclassing; `WithRuleFactory` lets a companion package choose the rule type `Build()` returns
- **Subject-Typed Rules** - `IBusinessRule<T>` / `IAsyncBusinessRule<T>` receive their subject at evaluation time, so one stateless, DI-friendly instance validates any number of subjects; `.For(subject)` binds back to the non-generic world, the checkers take a subject directly (`CheckAll(order, rules)`), `EvaluateEach(orders, rules)` runs a rule set over many subjects, and typed `And`/`Or`/`Not` and `BusinessRuleBuilder<T>` complete the picture
- **Specification Rules** - Write a rule once as an expression (`SpecificationBusinessRule<T>.BrokenWhen`), check it in memory with the typed checkers and ask the database for every violating row with `db.Orders.WhereBroken(rule)` / `WhereSatisfies(rule)`; `AllOf`/`AnyOf`/`Not` combine specifications into one translatable lambda (no `Invoke` nodes) and `.ToAsync()` is the async mirror
- **Logical Composition** - Combine rules with `And()`, `Or()`, `Not()` (operators combine the *broken* state of their operands; see the pinned truth table). The operators are real tree nodes (`AndBusinessRule`, `OrBusinessRule`, `NotBusinessRule` and their async twins) that keep their operands and forward the context
- **Rule Groups** - Unambiguous N-ary quantifiers `RuleGroup.All/Any/AtLeast/Exactly` (and `AsyncRuleGroup`) with opt-in short-circuit and (async) parallel evaluation; member failures travel through `BusinessRuleResult.Details`
- **Explain Trees** - `rule.Explain(context)` / `await rule.ExplainAsync(context)` return a navigable, printable tree (Passed/Broken/Skipped/CacheHit/NotEvaluated/Faulted per node, with timing) produced by a single evaluation; decorators from other packages join the tree through `IExplainableDecoratedBusinessRule`
- **Rule Metadata** - Name, description, category, tags, and severity
- **Caching** - Time-based caching for expensive rule evaluations, per instance or per cache key (`.Cached(ttl, ctx => tenant)`) so scoped rules cache per tenant
- **Conditional Execution** - Execute rules only when preconditions are met (`.When(condition)`)
- **Shadow Rollout and Feature Gates** - `rule.GatedBy(flag, provider, stableKey)` ships a rule dark: `Off`, `Shadow` (evaluated but never blocking; what would have failed lands on `result.ShadowResults` and the `OnShadowViolation` observer hook) or `Enforced`, ramped to a percentage of callers by a stable in-house FNV-1a bucket, all read from an `IRuleFeatureFlagProvider` on every evaluation so the rule class never changes
- **Rule Scopes and Externalized Parameters** - Stop hard-coding `10_000m`: declare `.WithParameter<decimal>("maxOrderAmount", fallback: 10_000m)` and read it in `.When((ctx, p) => ...)`; an `IRuleParameterProvider` supplies the value per `RuleScope` (`RuleScope.Tenant("acme").With("region", "EU")`), so the same checkout rules run with different limits for ACME without an `if` in rule code
- **Batch Validation** - Check multiple rules with fail-fast and parallel execution, or pass a `RuleEvaluationOptions` / `AsyncRuleEvaluationOptions` object
- **Evaluation Ledger** - Every batch result carries one `RuleEvaluationEntry` per rule (order, outcome, duration, fault), `TotalDuration`, `Slowest` and `Faulted`
- **Fault Policy and Severity Threshold** - Decide whether a throwing rule aborts the batch, is recorded, or counts as broken (`FaultPolicy`); make `CheckAll` throw only at or above a severity (`ThrowOn`), and re-severity any rule with `.WithSeverity()`
- **Rich Results** - Every result carries the rule's `Name`, `Category` and `Tags`; a `Describe(context)` hook (or the builders' `WithDetail`/`WithActual`/`WithExpected`/`WithRemediation`) attaches `Detail`, `Actual`, `Expected`, `Remediation` and a `Data` bag on the broken path only, and exceptions and results filter with `ByTag`/`ByCategory`: enough to build an RFC 7807 Problem Details response straight from the engine
- **Typed Context** - Strongly-typed data passing to rules: `BusinessRuleContext<T>.Data` for one object, `ContextKey<T>` for named values read with `context.Get(OrderKeys.Order)` (no cast, no string literal); a failed read throws `ContextKeyNotFoundException` listing the available keys or `ContextValueTypeMismatchException` naming both types, and the checkers wrap either in a `BusinessRuleContextException` that names the failing rule
- **Rule Adapters** - Convert between sync and async rules
- **Observers** - Monitor rule execution with per-rule and batch lifecycle callbacks (`OnBatchStarted`, `OnAfterEvaluate` with elapsed time, `OnRuleFaulted`, `OnRuleCancelled`, `OnBatchCompleted` or `OnBatchAborted`, `OnShadowViolation`, `OnShadowFault`) from `CheckAll` and the options-taking `EvaluateAll`
- **Testing Utilities** - `RuleTestHelper` assertions and factories for unit testing rules
- **Rule Expressions** (`Masterly.BusinessRules.Expressions`) - Write a condition as `amount > @maxOrderAmount && customer.Tier != 'VIP'`, parse it once and get an explanation with the bound values (`amount(12000) > @maxOrderAmount(10000) => true`); `WhenExpression(...)` on the builders returns an `ExpressionBusinessRule`, and the `IRuleConditionCompiler` seam keeps the grammar out of the core
- **Diagnostics** (`Masterly.BusinessRules.Diagnostics`) - Drop-in observers turn every rule evaluation into a structured `ILogger` line, a child span per rule under a batch span from the `Masterly.BusinessRules` `ActivitySource`, and the `business_rules.*` instruments of the `Masterly.BusinessRules` `Meter`; `rule.Traced()` does the same for a single `Check()`
- **Dependency Injection** (`Masterly.BusinessRules.DependencyInjection`) - `services.AddBusinessRules(assembly)` scans for rule types and registers them, a handler injects `IRuleRunner<Order>` and calls `rules.CheckAllAsync(order, ct)`, and `AddRuleSet("checkout", ...)` layers named rule sets per `RuleScope` into an injectable `IRuleRegistry` that disables, adds or replaces rules by code for one tenant
- **Microsoft.FeatureManagement Adapter** (`Masterly.BusinessRules.FeatureManagement`) - Plugs `IFeatureManager` / `IVariantFeatureManager` into the feature gate: a plain flag maps to `Enforced`/`Off`, a variant named `Shadow`, `Enforced-10` or `Off` (or carrying a `Mode`/`Percent` configuration) maps to the matching `RuleRollout`
- **Testing Package** (`Masterly.BusinessRules.Testing`) - Outcome coverage (`WithCoverage(session)`), a boundary finder that bisects to the exact flipping value, CsCheck-backed monotonicity and context-purity properties, and smoke-mode mutants
- **Zero-dependency core** - The five companion packages are opt-in; the core references none of them

---

## Installation

Every package ships from this repository with the same version. Install the core, then only the companions you use.

| Package | Purpose | Install |
|---------|---------|---------|
| `Masterly.BusinessRules` | The zero-dependency core: rules, builders, composition, groups, explain trees, checkers, context, caching, feature gates, parameters and specifications | `dotnet add package Masterly.BusinessRules` |
| `Masterly.BusinessRules.Expressions` | Textual rule conditions parsed once and explained with bound values | `dotnet add package Masterly.BusinessRules.Expressions` |
| `Masterly.BusinessRules.Diagnostics` | Structured `ILogger` observer, OpenTelemetry spans and `Meter` instruments for rule evaluation | `dotnet add package Masterly.BusinessRules.Diagnostics` |
| `Masterly.BusinessRules.DependencyInjection` | Assembly scanning, injectable rule runners and the scoped rule registry | `dotnet add package Masterly.BusinessRules.DependencyInjection` |
| `Masterly.BusinessRules.FeatureManagement` | `Microsoft.FeatureManagement` adapter supplying feature-gated rules with their rollout | `dotnet add package Masterly.BusinessRules.FeatureManagement` |
| `Masterly.BusinessRules.Testing` | Outcome coverage, boundary finder, CsCheck-backed rule properties and smoke-mode mutants for test projects | `dotnet add package Masterly.BusinessRules.Testing` |

Or via the Package Manager Console:

```powershell
Install-Package Masterly.BusinessRules
```

---

## Packages

`Masterly.BusinessRules` is the zero-dependency core. The companion packages plug into the seams the core exposes (observers, `IRuleConditionCompiler`, `WithRuleFactory`, `IExplainableDecoratedBusinessRule`, `IRuleFeatureFlagProvider`, the subject-typed rule interfaces); the core never references any of them.

| Package | Target | Dependencies beyond the core |
|---------|--------|------------------------------|
| `Masterly.BusinessRules` | `netstandard2.1` | none |
| `Masterly.BusinessRules.Expressions` | `netstandard2.1` | none |
| `Masterly.BusinessRules.Diagnostics` | `netstandard2.1` | `Microsoft.Extensions.Logging.Abstractions`, `System.Diagnostics.DiagnosticSource` |
| `Masterly.BusinessRules.DependencyInjection` | `netstandard2.1` | `Microsoft.Extensions.DependencyInjection.Abstractions` |
| `Masterly.BusinessRules.FeatureManagement` | `netstandard2.1` | `Microsoft.FeatureManagement` |
| `Masterly.BusinessRules.Testing` | `net8.0` (CsCheck ships no .NET Standard build, and the boundary finder uses `INumber<T>`) | `CsCheck` |

Each companion package is documented in its own section of this README.

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
- `Evaluate()` / `EvaluateAsync()`: `Evaluate()` (sync) is a pure factory that builds the broken result without evaluating the rule (it never returns null); `EvaluateAsync()` evaluates and returns the result when broken, or null when the rule passes

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

**Rule factory seam.** `BusinessRuleBuilder.WithRuleFactory(IBuiltRuleFactory)` and `AsyncBusinessRuleBuilder.WithRuleFactory(IAsyncBuiltRuleFactory)` let a sibling package decide which rule type `Build()` returns. At `Build()` the factory receives a `BuiltRuleDefinition`: the seven metadata members (the name defaults to the code), `ResolveParameters(context)` / `ResolveParametersAsync(context, ct)` for the `WithParameter<T>` declarations, and `CreateResult(rule, context)` that attaches the `WithDetail`/`WithActual`/`WithExpected`/`WithRemediation` details. A factory replaces any condition set so far and a later `When`/`WhenAsync` replaces the factory; a factory that returns null makes `Build()` throw `InvalidOperationException`. The Expressions package's `WhenExpression` uses it to return an `ExpressionBusinessRule`.

**The last condition wins.** On `BusinessRuleBuilder` and `AsyncBusinessRuleBuilder` every `When(...)` / `WhenAsync(...)` overload (parameterless, context, parameters, compiled text) replaces whatever condition was set before, so `When((ctx, p) => true).When(ctx => false)` builds a rule that never breaks. The subject-typed `BusinessRuleBuilder<T>` keeps its subject-only and context conditions side by side (the subject-only one answers `IsBroken(subject)`, the context one `IsBroken(subject, context)`), but a parameters condition replaces both and is replaced by either.

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
BusinessRuleChecker.CheckAll(order, overdue, big);
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
    new IBusinessRule[] { new Rule1(), new Rule2(), new Rule3() }
);
composite.Check();
```

---

## Structural Composition and Explain Trees

Every composed rule keeps its shape. The `And()`, `Or()` and `Not()` operators, the composites and the rule groups all implement `IComposedBusinessRule` (`Operator`, `Operands`), while `CachedBusinessRule`, `ConditionalBusinessRule`, `SeverityOverrideBusinessRule` and `FeatureGatedBusinessRule` implement `IDecoratedBusinessRule` (`InnerRule`); the async side mirrors this with `IComposedAsyncBusinessRule` and `IDecoratedAsyncBusinessRule`, and both adapters expose their wrapped rule as `InnerRule`. Tooling can therefore walk any rule tree without knowing the concrete node types.

**Rule groups** are unambiguous N-ary quantifiers stated in terms of how many members must **pass**:

| Group | Broken when |
|-------|-------------|
| `RuleGroup.All(code, message, members...)` | any member is broken (every member must pass) |
| `RuleGroup.Any(code, message, members...)` | every member is broken (at least one member must pass) |
| `RuleGroup.AtLeast(n, code, message, members...)` | fewer than `n` members pass |
| `RuleGroup.Exactly(n, code, message, members...)` | the number of passing members is not `n` |

Groups are immutable: `WithShortCircuit()`, `WithSeverity()` and (async only) `WithParallelEvaluation()` return a new instance. By default a group evaluates every member so that the group's `BusinessRuleResult.Details` lists every broken member; a short-circuiting group stops as soon as the verdict is decided and reports the remaining members as `NotEvaluated`. `WithParallelEvaluation()` starts every member at once and awaits all of them (so faults are always observed); it cannot be combined with `WithShortCircuit()`, and a member that observes cancellation throws before it populates a `CachedAsyncBusinessRule` or reaches an observer. Factories reject empty member lists, null members and thresholds outside `0..members.Count`.

**Member results survive the batch path.** A group or composite reports one result whose `Details` holds the broken members (nested groups nest their details). `BusinessRuleChecker.CheckAll`/`EvaluateAll` and their async twins evaluate composed rules exactly once and surface those details; `exception.Flatten()` (or `result.Flatten()`) yields only the leaf results.

**Explain trees.** `rule.Explain(context)` and `await asyncRule.ExplainAsync(context, ct)` evaluate the rule **exactly once** and return a `RuleExplanation` tree produced by that evaluation: no rule is evaluated a second time to build it. Every node carries the rule, its `RuleOutcome` (`Passed`, `Broken`, `Skipped`, `CacheHit`, `NotEvaluated`, `Faulted`), `IsBroken`, the `Operator` label (`ALL`, `AND`, `AT LEAST 2`, `CACHED`, `WHEN`, `ADAPTER`, ...), a `Reason` (`short-circuit`, `precondition not met`, `cache hit`, the captured exception), the `Duration` spent in that node and its `Children`. Third-party rules appear as a single leaf, except decorators that implement the public `IExplainableDecoratedBusinessRule` / `IExplainableDecoratedAsyncBusinessRule` seam (see below). `Explain(context, captureExceptions: true)` turns a throwing node into a `Faulted` node (treated as broken) instead of propagating; cancellation is never captured. Timing exists only inside the explain walk, so the plain `IsBroken`/`Check` path stays allocation-free.

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

**Custom decorators in the tree.** A decorator written outside the library implements `IExplainableDecoratedBusinessRule` (`IExplainableDecoratedAsyncBusinessRule` on the async side): an `ExplanationOperator` label and `EvaluateDecorated(DecoratedRuleEvaluation evaluation)` (`EvaluateDecoratedAsync(AsyncDecoratedRuleEvaluation, ct)`). The walker hands it an evaluation whose `EvaluateInner()` / `EvaluateInnerAsync(ct)` runs `InnerRule` through the library walker **at most once** (composed inner rules keep their `Details` and, while explaining, record their own subtree), and records the decorator as a node labelled `ExplanationOperator` with the inner rule as its child; an inner rule the decorator never evaluates is recorded as `NotEvaluated` with the reason `not evaluated by decorator`. `EvaluateDecorated` is the decorator's verdict on every walker path (`And`/`Or`/`Not` operands, groups, `Explain`), so implement the decorator's own `IsBroken` through it with the public constructor `new DecoratedRuleEvaluation(InnerRule, context)`. The Testing package's coverage and mutation decorators use this seam.

```csharp
public sealed class AuditedRule : IExplainableDecoratedBusinessRule
{
    public AuditedRule(IBusinessRule innerRule) => InnerRule = innerRule;

    public IBusinessRule InnerRule { get; }
    public string ExplanationOperator => "AUDITED";
    public string Code => InnerRule.Code;
    public string Message => InnerRule.Message;

    public BusinessRuleResult? EvaluateDecorated(DecoratedRuleEvaluation evaluation)
    {
        BusinessRuleResult? result = evaluation.EvaluateInner();   // evaluated once, subtree recorded when explaining
        AuditLog.Record(InnerRule.Code, result != null);
        return result;
    }

    public bool IsBroken() => EvaluateDecorated(new DecoratedRuleEvaluation(InnerRule, null)) != null;
    public bool IsBroken(BusinessRuleContext context) => EvaluateDecorated(new DecoratedRuleEvaluation(InnerRule, context)) != null;
    public void Check() => InnerRule.Check();
    public void Check(BusinessRuleContext context) => InnerRule.Check(context);
    public BusinessRuleResult Evaluate() => InnerRule.Evaluate();
    public BusinessRuleResult Evaluate(BusinessRuleContext context) => InnerRule.Evaluate(context);
}

// [BROKEN] AUDITED  KYC.INCOMPLETE  "All KYC checks must pass"  (1.0 ms)
//  └─ [BROKEN] ALL  KYC.INCOMPLETE  "All KYC checks must pass"  (0.9 ms)
//      ├─ [PASSED]  KYC.ID  (0.1 ms)
//      └─ ...
Console.WriteLine(new AuditedRule(kyc).Explain(context).ToText());
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

    // The limit lives in the context, so the context-less overload has nothing to compare against
    public override bool IsBroken() => throw new InvalidOperationException("TransactionLimitRule needs a context with a 'limit'.");

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

    public override bool IsBroken() => throw new InvalidOperationException("TransactionLimitRule reads the order and the limit from the context.");

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
- `CachedAsyncBusinessRule`: Wraps async rules with async-safe caching (a `SemaphoreSlim` guards the cache entries)

**Concurrency.** The lock (sync) or semaphore (async) guards only the cache lookups and stores; the inner rule runs outside it, so one slow evaluation never blocks cache hits or evaluations for other keys (other tenants keep being served while tenant A's miss is computed). Concurrent misses for the same key may each evaluate the inner rule; the last verdict stored wins. A verdict whose evaluation overlapped an `InvalidateCache` call is returned but not stored, so an invalidation is never undone by an evaluation that started before it.

**Captured faults are never cached.** `Explain(context, captureExceptions: true)` turns an inner exception into a `Faulted` node and a broken verdict; that verdict is returned to the explanation but never stored, so a diagnostic explain during a transient outage cannot make later `IsBroken`/`Check` calls report the rule as broken for the whole cache duration.

**Per instance or per key.** By default a cached rule holds **one verdict per instance**, whatever the context, so it must never wrap a rule whose verdict depends on the context (scoped parameters, a tenant id, a feature cohort). Pass a `cacheKeySelector` (`Func<BusinessRuleContext, string>`) to cache **one verdict per key** instead: `rule.Cached(ttl, ctx => ctx.Get(RuleParameters.ScopeKey).ToString())` keeps ACME's verdict apart from Globex's. `CachesPerKey` reports which mode an instance is in. The parameterless `IsBroken()` / `Check()` have no context to select a key from and always use a dedicated shared entry. Expired entries are purged whenever a new key is stored, so the cache holds at most one live entry per key seen within the duration; keep the key space bounded (tenants, regions), never per request. A selector that returns null throws `RuleConfigurationException` naming the rule's `Code`.

**Cache Invalidation:**
- `InvalidateCache()` / `InvalidateCacheAsync()`: Clears every entry (the shared one and all keys)
- `InvalidateCache(key)` / `InvalidateCacheAsync(key)`: Clears one key; other keys and the shared entry are untouched

**Extension Methods:**
- `.Cached(TimeSpan)`: Fluent syntax for adding per-instance caching; returns the concrete `CachedBusinessRule` / `CachedAsyncBusinessRule`, so `InvalidateCache()` / `InvalidateCacheAsync()` and `CachesPerKey` are reachable on both sides
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
- `ConditionalBusinessRule(innerRule, Func<bool> condition)`: Wraps a sync rule with a parameterless condition
- `ConditionalAsyncBusinessRule(innerRule, Func<BusinessRuleContext, bool> condition)`: Wraps an async rule with a synchronous, context-aware condition

**Extension Methods:**
- `IBusinessRule.When(Func<bool>)`: Returns a `ConditionalBusinessRule`
- `IAsyncBusinessRule.When(Func<BusinessRuleContext, bool>)`: Returns a `ConditionalAsyncBusinessRule`

When the condition is false the inner rule is never evaluated, and `Explain` records the decorator as `Skipped` (`WHEN`, reason `precondition not met`) with the inner rule as `NotEvaluated`. For a sync condition that needs the context, build the rule with `BusinessRuleBuilder.When(ctx => ...)` instead.

### Conditional Execution Examples

```csharp
// Sync rule, parameterless condition
ConditionalBusinessRule conditionalRule = new ConditionalBusinessRule(
    new PremiumFeatureRule(),
    () => user.IsPremium
);

// Async rule, context-aware condition
ConditionalAsyncBusinessRule conditionalAsyncRule = new ConditionalAsyncBusinessRule(
    new ExpensiveDatabaseRule(),
    ctx => ctx.Get<bool>("shouldValidate")
);

await conditionalAsyncRule.CheckAsync(context); // Only runs if condition is true

// Extension method syntax
IBusinessRule condExtRule = new PremiumRule().When(() => isPremiumEnabled);

IAsyncBusinessRule condExtAsync = new ValidationRule()
    .When(ctx => ctx.Get<bool>("validateEnabled"));
```

---

## Shadow Rollout and Feature-Gated Rules

Ship a new rule dark and ramp it from your flag provider without touching the rule class. `rule.GatedBy(flag, provider, stableKey)` wraps any rule (sync or async) in a `FeatureGatedBusinessRule` / `FeatureGatedAsyncBusinessRule` that asks an `IRuleFeatureFlagProvider` / `IAsyncRuleFeatureFlagProvider` for the flag's `RuleRollout` on **every evaluation**, so moving a rule from observe-only to blocking is a configuration flip. All seven metadata members are forwarded from the inner rule.

| `RuleRolloutMode` | Inner rule | Gated rule reports | Visible through |
|-------------------|------------|--------------------|-----------------|
| `Off` (or the caller is outside the rollout cohort) | not evaluated | passes | a `Skipped` `GATED` node in `Explain` |
| `Shadow` | evaluated; an exception it throws is caught (cancellation excepted) | passes, never throws | `observer.OnShadowViolation(rule, result)` and `result.ShadowResults`; a caught exception also `observer.OnShadowFault(rule, exception)` |
| `Enforced` | evaluated | its own verdict | like any other rule |

**Ramping.** A `RuleRollout` carries a `Percent` (0..100). A partial rollout needs a `stableKey` selector (a customer id, a tenant id) that `RuleRolloutBucket` hashes with in-house 32-bit FNV-1a into buckets 0..99; the caller is inside the cohort when its bucket is below the percent. The flag name is mixed into the hash, so different flags ramp over different customers, and a customer inside a 10% cohort stays inside when the rollout grows to 20%. A rollout at 100% never calls the stable key selector; a partial rollout without one throws `RuleConfigurationException`.

**Providers.** Pass the provider to `GatedBy(...)`, or store one on the context under `RuleFeatureFlags.ContextKey` (`RuleFeatureFlags.AsyncContextKey` for async providers): a provider on the context overrides the injected one for that evaluation, which lets a test or a canary pipeline force a rollout. Async gated rules try the async provider first, then the sync one. When no provider can be found anywhere the evaluation throws `RuleConfigurationException` naming the rule's `Code`. `InMemoryRuleFeatureFlagProvider` implements both interfaces, is thread-safe and can be flipped at any time; `RuleTestHelper.CreateFlagProvider(flag, mode, percent)` builds one for tests. The sibling package `Masterly.BusinessRules.FeatureManagement` adapts `Microsoft.FeatureManagement` flags and variants; see [Microsoft.FeatureManagement Adapter](#microsoftfeaturemanagement-adapter).

**Shadow results.** Both checkers collect what shadow-mode rules would have reported on `RuleEvaluationResult.ShadowResults` / `AsyncRuleEvaluationResult.ShadowResults` (`HasShadowResults`), whether the gated rule is a batch member or nested inside a group, a cache or an adapter. These results are not in `BrokenRules`, never make `CheckAll` throw and never affect `AllPassed`. The options-taking overloads additionally raise the observer hook `OnShadowViolation(rule, result)` / `OnShadowViolationAsync(rule, result, ct)` right after the rule's own callbacks (in a parallel async batch, once every rule has completed and before `OnBatchCompletedAsync`); it is a default interface member, so existing observers keep compiling.

**Shadow never blocks.** A dark-launched rule with a bug must not break checkout for its cohort, so in `Shadow` mode the gate catches any exception the inner rule throws (cancellation always propagates). The batch still passes; `ShadowResults` receives a stand-in result carrying the gated rule's metadata, the observer receives `OnShadowViolation` with that stand-in and then `OnShadowFault(rule, exception)` / `OnShadowFaultAsync(rule, exception, ct)` with the exception, and an explanation records the inner rule as a `Faulted` node under the passing `SHADOW` node. `Enforced` rules are unchanged: their faults follow the batch's `FaultPolicy`.

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

## Microsoft.FeatureManagement Adapter

`Masterly.BusinessRules.FeatureManagement` lets feature-gated rules read their `RuleRollout` from the same `Microsoft.FeatureManagement` flags, filters and variants as the rest of the application. `FeatureManagementRuleFeatureFlagProvider` implements both `IRuleFeatureFlagProvider` and `IAsyncRuleFeatureFlagProvider` over an `IVariantFeatureManager` (variants consulted) or a plain `IFeatureManager` (enabled state only), so one instance serves sync and async gated rules; the async decorator picks its async side.

```bash
dotnet add package Masterly.BusinessRules.FeatureManagement
```

**Mapping.** On every evaluation the provider asks for the flag's assigned variant first and the enabled state second:

| Microsoft.FeatureManagement says | `RuleRollout` reported |
|----------------------------------|------------------------|
| Variant listed in `FeatureManagementRolloutOptions.VariantRollouts` (`MapVariant("dark", RuleRollout.Shadow())`) | the mapped rollout |
| Variant whose configuration has a `Mode` value and an optional `Percent` value (0..100, default 100), or a scalar value such as `"Shadow-10"` | that mode and percent |
| Variant named after a `RuleRolloutMode`, optionally followed by a percent: `Shadow`, `Enforced-10`, `enforced:25%`, `Off` (case-insensitive; separators `-`, `:`, `_`, `/`, space) | that mode and percent; `Off` is always the canonical `RuleRollout.Off` |
| Variant that maps to none of the above | per `OnUnrecognizedVariant`: `Throw` (`RolloutVariantFormatException`, the default), `Off`, or `UseEnabledState` |
| No variant, flag enabled | `EnabledRollout` (`RuleRollout.Enforced()` by default; set `RuleRollout.Shadow()` to ship every boolean-gated rule dark) |
| No variant, flag disabled or unknown | `RuleRollout.Off` |

An assigned variant decides even when the flag is disabled (`allocation.default_when_disabled`), which is how `Microsoft.FeatureManagement` itself treats variants; give the disabled allocation an `Off` variant. `RolloutVariantConvention.TryParse(name)` / `TryRead(section)` expose the convention so configuration can be validated at startup.

**Two ramps, pick one.** The `Percent` of a variant is the in-house `RuleRolloutBucket` cohort over the gated rule's `stableKey`; a `Microsoft.Percentage` or `Microsoft.Targeting` filter ramps the flag inside `Microsoft.FeatureManagement`, and the provider then reports `Enforced`/`Off` at 100%. Use the variant percent **or** the filter, not both, or callers are bucketed twice. Set `TargetingContextSelector` to turn the evaluation context into an `ITargetingContext` (`new TargetingContext { UserId = ctx.Get<string>("customerId") }`) so the filter and the variant allocation see the same customer the stable key describes; without it the feature manager falls back to its own `ITargetingContextAccessor`.

**Sync side.** `Microsoft.FeatureManagement` is asynchronous only, so `GetRollout` blocks on `GetRolloutAsync` with `GetAwaiter().GetResult()`, exactly like `AsyncToSyncRuleAdapter`. Prefer async gated rules on request paths.

**Registration.** `services.AddBusinessRulesFeatureManagement(configure?, lifetime?)` registers `FeatureManagementRuleFeatureFlagProvider`, `IRuleFeatureFlagProvider` and `IAsyncRuleFeatureFlagProvider` (one instance behind all three) over the `IVariantFeatureManager` when one is registered and the `IFeatureManager` otherwise. Call `services.AddFeatureManagement(...)` first. The lifetime defaults to `Singleton`, matching `AddFeatureManagement`; pass `ServiceLifetime.Scoped` with `AddScopedFeatureManagement`. Registrations use `TryAdd`, so an existing provider registration wins.

### Microsoft.FeatureManagement Examples

```jsonc
// appsettings.json: ship dark at 10% of customers, then flip the allocation to "live"
{
  "feature_management": {
    "feature_flags": [
      {
        "id": "credit-score-v2",
        "enabled": true,
        "variants": [
          { "name": "dark", "configuration_value": { "Mode": "Shadow", "Percent": 10 } },
          { "name": "live", "configuration_value": { "Mode": "Enforced" } }
        ],
        "allocation": { "default_when_enabled": "dark", "default_when_disabled": "Off" }
      },
      { "id": "sanctions-check", "enabled": true }      // plain flag: Enforced when on, Off when off
    ]
  }
}
```

```csharp
services.AddFeatureManagement(configuration);
services.AddBusinessRulesFeatureManagement(options =>
{
    options.TargetingContextSelector = ctx => new TargetingContext { UserId = ctx.Get<string>("customerId") };
    options.OnUnrecognizedVariant = UnrecognizedVariantBehavior.Off;      // a variant meant for other code never blocks
});

// Gate the rules; the rule classes never change
IAsyncRuleFeatureFlagProvider flags = serviceProvider.GetRequiredService<IAsyncRuleFeatureFlagProvider>();
IAsyncBusinessRule creditScore = new NewCreditScoreRule(customer)       // the sync rule from the Feature Gate example
    .ToAsync()
    .GatedBy("credit-score-v2", flags, stableKey: ctx => ctx.Get<string>("customerId"));
IAsyncBusinessRule sanctions = new SanctionsCheckRule(api).GatedBy("sanctions-check", flags);

BusinessRuleContext context = RuleTestHelper.CreateContext(("customerId", customer.Id));
AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(context, new[] { creditScore, sanctions });
IReadOnlyCollection<BusinessRuleResult> shadow = result.ShadowResults;   // what credit-score-v2 would have blocked for the 10% cohort

// Without DI, or when a variant name does not follow the convention
FeatureManagementRuleFeatureFlagProvider provider = new FeatureManagementRuleFeatureFlagProvider(
    variantFeatureManager,
    new FeatureManagementRolloutOptions().MapVariant("canary", RuleRollout.Enforced(5)));

// Validate the convention at startup
if (!RolloutVariantConvention.TryParse(variantName, out RuleRollout rollout))
    throw new InvalidOperationException($"Variant '{variantName}' is not a rollout");
```

---

## Rule Scopes and Externalized Parameters

Multi-tenant SaaS wants "the same checkout rules, different limits for ACME" with no `if (tenant == "acme")` in rule code. Declare the limit as a **named parameter with a mandatory fallback**, read it from the `RuleParameters` handed to the `(ctx, p) => ...` condition, and let an `IRuleParameterProvider` supply the value for the **scope** of the evaluation. The provider is consulted on every evaluation, so changing a limit never rebuilds a rule; without any provider every parameter resolves to its fallback, so a parameterless `Check()` or an `And`/`Or` composition can never throw for a missing value.

**`RuleScope`** is an immutable set of key/value pairs (`RuleScope.Global`, `RuleScope.Tenant("acme")`, `.With("region", "EU")`) with value equality (pair order does not matter), `Specificity` (the pair count) and `AppliesTo(requested)`: a scope applies to a request when every one of its pairs is in the request, so `Tenant("acme")` applies to `Tenant("acme").With("region", "EU")` and `Global` applies to everything. A provider or a registry picks the most specific applying layer. `TenantId`, `TryGet(key)`, `ContainsKey(key)` and `ToString()` (`region=EU;tenant=acme`, keys sorted) complete the type.

**Providers.** `IRuleParameterProvider.TryGet<T>(name, scope, out value)` and the async twin `IAsyncRuleParameterProvider.TryGetAsync<T>(name, scope, ct)` returning `RuleParameterLookup<T>` (`Found`, `Value`, `Missing`, `Of(value)`, `GetValueOrFallback`). `InMemoryRuleParameterProvider` implements both, is thread-safe, layers values per scope (`Set(name, value)` for everybody, `Set(name, value, scope)` for a layer) and returns the most specific applying value; among equally specific layers the most recently set one wins. A value stored with another type than the one requested throws `InvalidCastException` naming the parameter, the scope and both types: a configuration error, never a silent fallback. `RuleTestHelper.CreateParameterProvider(name, value, scope?)` builds one for tests. The sibling DependencyInjection package hosts the scoped `RuleRegistry` (named rule sets with per-tenant overrides; see [Dependency Injection](#dependency-injection)); the Expressions package resolves `@maxOrderAmount` nodes through the same provider.

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

## Rule Expressions

The `Masterly.BusinessRules.Expressions` package lets a condition live as text: `amount > @maxOrderAmount && customer.Tier != 'VIP'` is parsed once into an immutable tree, evaluated against the `BusinessRuleContext` by a single interpreter, and explained with the **actual bound values**: `amount(12000) > @maxOrderAmount(10000) => true`. It depends on the core package only, targets `netstandard2.1`, and plugs into the core through the `IRuleConditionCompiler` / `IAsyncRuleConditionCompiler` seam, so the grammar never enters the core assembly and a team that prefers another parser can implement the seam instead.

```bash
dotnet add package Masterly.BusinessRules.Expressions
```

**One evaluator.** `RuleExpression.Parse(text, options)` builds the tree; `Evaluate(context)` / `EvaluateAsync(context, ...)` walk it, and `Explain(context, redact)` / `ExplainAsync(...)` run the same walk with a binding recorder, so the verdict and the explanation cannot disagree. `RuleExpressionExplanation` carries `Result`, `Steps` (`RuleExpressionStep`: `Kind` = `Binding` or `Operation`, `NodeText`, `BoundValue`, `Result`, in evaluation order; a short-circuited branch has no steps) and `Rendered`, where paths show `path(value)`, nested operations show `[result]` (or `[not evaluated]`) and the root ends in `=> result`. Values are formatted with `RuleExpressionOptions.Culture` (invariant by default). The `redact` delegate (`RuleExpressionRedactor`, `(name, value) => string?`) decides how every bound value is displayed and runs before the value is recorded, so masked PII never reaches `Steps` or `Rendered`.

**Binding.** A root identifier binds to the context entry of that name; `Data` binds to the typed data of a `BusinessRuleContext<T>` (`__typedData`) and falls back to a key named `Data`. `@name` is a parameter node resolved through the core parameter pipeline: declared builder parameters, then the provider stored under `RuleParameters.AsyncContextKey` / `RuleParameters.ContextKey`, then the injected async / sync `IRuleParameterProvider` (scope from `RuleParameters.ScopeKey`), then the context bag. An unbound identifier, parameter or member throws `RuleExpressionBindingException` with `Identifier` and the sorted `AvailableKeys` (context keys, or the readable members of the type); an operator meeting operands it cannot work on throws `RuleExpressionEvaluationException` (`NodeText`); malformed text throws `RuleExpressionParseException` with 1-based `Line` and `Column`.

**Rules.** `expression.ToRule(code, message, parameters?, severity?)` returns an `ExpressionBusinessRule` (a `BaseBusinessRule` whose `Description` is the expression text; the parameterless `IsBroken()` throws `InvalidOperationException` because an expression needs a context) and `ToAsyncRule(code, message, asyncParameters?, parameters?, severity?)` an `ExpressionAsyncBusinessRule`, which resolves every `@parameter` before the walk so async providers are awaited once per evaluation. Both are ordinary leaves to `And/Or/Not`, groups, decorators, the checkers and `Explain()`; `ExplainExpression(context, redact?)` / `ExplainExpressionAsync(...)` return the bound-value explanation. On the builders, `WhenExpression(text[, options])` or `WhenExpression(expression)` parses at builder time and installs the expression through the core `WithRuleFactory` seam, so `Build()` returns an `ExpressionBusinessRule` (async builder: `ExpressionAsyncBusinessRule`) that keeps every builder feature: name, description (the expression text when none is set), category, tags, the `WithDetail`/`WithActual`/`WithExpected`/`WithRemediation` details and the `WithParameter<T>` declarations feeding `@name`. Like every expression rule it needs a context (its parameterless `IsBroken()` throws `InvalidOperationException`), and a later `When`/`WhenAsync` call on the same builder replaces the expression. `RuleExpressionConditionCompiler` implements both seams for `When(compiler, text)` / `WhenAsync(compiler, text)` and for container registration (`Options`, `Parameters`, `AsyncParameters`; the async seam is implemented explicitly and also exposed as `CompileAsyncCondition`).

**Typed fast path.** `expression.Compile<T>()` returns `Func<BusinessRuleContext<T>, bool>`. Paths rooted at `Data` become real, statically resolved `MemberExpression` chains (null-safe), so a typo such as `Data.Itemz.Count` fails at compile time with `RuleExpressionParseException`; context identifiers, parameters and every operator call the same runtime helpers as the interpreter, so both paths return the same verdict.

**Safety.** `RuleExpressionOptions` is immutable (`Default`, `WithMaxLength`, `WithMaxDepth`, `WithMemberFilter`, `WithRequireRuleAccessibleAttribute`, `WithCulture`): `MaxLength` (default 4096 characters) and `MaxDepth` (default 64 levels) bound the parse; member access is restricted to public, non-indexed instance properties, further narrowed by `MemberFilter` (`Func<PropertyInfo, bool>`) and, when `RequireRuleAccessibleAttribute` is set, to properties (or types) marked `[RuleAccessible]`. Expressions cannot call arbitrary methods: the only functions are `startsWith`, `endsWith` and `contains`. **AOT and trimming:** the interpreter path (`Evaluate`, `Explain`, the rules, the builders and the condition compiler) emits no dynamic code and reads properties through `PropertyInfo`, so keep the members your expressions read from being trimmed; `Compile<T>()` builds an expression tree and needs dynamic code (on AOT, `LambdaExpression.Compile` falls back to the interpreter).

### Grammar

Precedence from loosest to tightest; keywords (`and`, `or`, `not`, `in`, `true`, `false`, `null`) are case-insensitive, identifiers and member names are matched ordinally first and case-insensitively when unambiguous.

| Construct | Syntax | Notes |
|-----------|--------|-------|
| Or | `a \|\| b`, `a or b` | Short-circuits; operands must be booleans |
| And | `a && b`, `a and b` | Short-circuits; operands must be booleans |
| Not | `!a`, `not a` | Operand must be a boolean |
| Comparison | `==` `!=` `~=` `<` `<=` `>` `>=` | `~=` is case-insensitive equality |
| Membership | `x in [a, b, c]`, `x in path` | List items may be any expression; `in` uses `==` equality |
| Additive | `a + b`, `a - b` | `+` also concatenates two strings |
| Multiplicative | `a * b`, `a / b`, `a % b` | |
| Unary minus | `-a` | |
| Member access | `customer.Address.City`, `items.Count`, `name.Length` | Null-safe: any null segment yields null |
| Functions | `name.startsWith('A')`, `endsWith(...)`, `contains(...)`; `/ci` suffix: `name.startsWith/ci('a')` | `contains` works on strings and collections |
| Literals | `12000`, `12.5`, `'VIP'`, `"VIP"`, `true`, `false`, `null` | Numbers are invariant-culture decimals (no exponent); strings accept `\'` `\"` `\\` `\n` `\r` `\t` |
| Identifiers | `amount`, `Data`, `@maxOrderAmount` | Context key, typed data, rule parameter |
| Grouping | `( ... )` | |

The root must evaluate to a boolean; anything else throws `RuleExpressionEvaluationException`.

### Coercion Rules

| Operands | Behaviour |
|----------|-----------|
| Number and number (any CLR numeric type, literals included) | Promoted to `decimal`; to `double` only when either side is `float` or `double`. Decimal division by zero and overflow throw `RuleExpressionEvaluationException`; `double` follows IEEE (`Infinity`, `NaN`): every ordering and `==` with a `NaN` operand is false and `!=` is true, so `x != x` detects a `NaN` and `score < threshold` never fires for one |
| Enum and string literal | Equal when the literal is the member name, ignoring case (`tier == 'gold'`); ordering an enum against a string throws |
| Enum and number | Compared and ordered by the underlying value (`tier > 1`) |
| Enum and enum | Same type: by value; different enum types are never equal and cannot be ordered |
| String and string | Ordinal for `==`, `!=` and ordering; `~=` and the `/ci` functions are ordinal-ignore-case |
| Null | `null == null` is true and `null != x` is true; every ordering comparison with a null side is false; arithmetic with null yields null; a function with a null target or argument is false; `x in null` is false |
| `in` | Each item compared with the `==` rules above (`2.0 in [1, 2]` is true, `tier in ['vip', 'gold']` matches by name) |
| `Count` / `Length` | A real property of that name wins; otherwise `Length` and `Count` on `string` and `Count` on `ICollection` |
| Other values | Same-type `IComparable` values (`DateTime`, `Guid`, ...) order through `CompareTo`; equality across unrelated types is false (not an error); ordering or arithmetic across unrelated types throws `RuleExpressionEvaluationException` |

### Expression Examples

```csharp
using Masterly.BusinessRules.Expressions;

// Parse once, reuse everywhere
RuleExpression expression = RuleExpression.Parse("amount > @maxOrderAmount && customer.Tier != 'VIP'", RuleExpressionOptions.Default);

BusinessRuleContext context = new BusinessRuleContext();
context.Set("amount", 12_000m);
context.Set("customer", customer);                   // customer.Tier == CustomerTier.Gold
context.Set("maxOrderAmount", 10_000m);              // @name falls back to the context bag

// Bound-value explanation, with PII redaction
RuleExpressionExplanation why = expression.Explain(context, redact: (name, value) => name == "customer.Ssn" ? "***" : value?.ToString());
// why.Rendered => "(amount(12000) > @maxOrderAmount(10000) [true]) && (customer.Tier(Gold) != 'VIP' [true]) => true"
// why.Steps    => amount = 12000, @maxOrderAmount = 10000, amount > @maxOrderAmount => True, customer.Tier = Gold, ...

// As rules
ExpressionBusinessRule rule = expression.ToRule("ORDER.TOO_LARGE", "Order exceeds the customer's limit", parameters: provider);
ExpressionAsyncBusinessRule asyncRule = expression.ToAsyncRule("ORDER.TOO_LARGE", "Order exceeds the customer's limit", asyncParameters: tenantSettings);
rule.Check(context);
await asyncRule.CheckAsync(context, cancellationToken: ct);

// On the builders: parameters declared on the builder feed @name, and Build() returns an ExpressionBusinessRule
IBusinessRule built = BusinessRuleBuilder.Create("ORDER.TOO_LARGE")
    .WithMessage("Order exceeds the customer's limit")
    .WithCategory("Orders")
    .WithParameter("maxOrderAmount", 10_000m)
    .WhenExpression("amount > @maxOrderAmount")
    .Build();
RuleExpressionExplanation builtWhy = ((ExpressionBusinessRule)built).ExplainExpression(context);   // @maxOrderAmount(10000)

// Through the core seam (for example rules loaded from configuration)
RuleExpressionConditionCompiler compiler = new RuleExpressionConditionCompiler(RuleExpressionOptions.Default, provider);
IBusinessRule fromConfig = BusinessRuleBuilder.Create(row.Code).WithMessage(row.Message).When(compiler, row.Condition).Build();

// Parse errors carry a position
RuleExpressionParseException error = Assert.Throws<RuleExpressionParseException>(() => RuleExpression.Parse("amount >"));
// error.Line == 1, error.Column == 9

// Unbound identifiers name what was available
// RuleExpressionBindingException: 'amuont' could not be bound; available: amount, customer, maxOrderAmount.

// Typed fast path: typos fail at compile time
Func<BusinessRuleContext<Order>, bool> isEmpty = RuleExpression.Parse("Data.Items.Count == 0").Compile<Order>();
bool empty = isEmpty(new BusinessRuleContext<Order>(order));

// Restrict what analysts can read
RuleExpressionOptions locked = RuleExpressionOptions.Default
    .WithMaxLength(500)
    .WithRequireRuleAccessibleAttribute(true);       // only [RuleAccessible] properties

public sealed class Customer
{
    [RuleAccessible] public CustomerTier Tier { get; set; }
    public string Ssn { get; set; }                   // not readable from an expression
}
```

---

## Dependency Injection

`Masterly.BusinessRules.DependencyInjection` wires the engine into `Microsoft.Extensions.DependencyInjection` (it depends on `Microsoft.Extensions.DependencyInjection.Abstractions` only). Subject-typed rules are stateless, so they can be discovered by assembly scanning and injected as a set; the scoped `RuleRegistry` from the [Rule Scopes](#rule-scopes-and-externalized-parameters) design lives here too.

```bash
dotnet add package Masterly.BusinessRules.DependencyInjection
```

**Scanning.** `services.AddBusinessRules(assembly, ...)` registers every concrete, closed class that implements `IBusinessRule<T>` or `IAsyncBusinessRule<T>` under each closed rule interface it implements, and, when you opt in with `IncludeUntypedRules = true` (default false), every class implementing the plain `IBusinessRule` / `IAsyncBusinessRule` under that interface. Untyped scanning is off by default because the usual plain rule captures its subject through the constructor (`new MinimumAgeRule(age)`), which the container cannot create: registering it would fail `ValidateOnBuild` (on by default in ASP.NET Core Development) or the first resolution of `IEnumerable<IBusinessRule>`. Opt in only when the plain rules of the scanned assemblies take injectable services, and exclude the rest with `TypeFilter`. Abstract classes and open generics are skipped, non-public types are opt-in (`IncludeNonPublicTypes`), types are visited in ordinal order of their full name so the registration order is deterministic, and registrations use `TryAddEnumerable`, so scanning the same assembly twice registers each rule once. The default lifetime is `Scoped` (it works with dependencies of any lifetime); set `Lifetime = ServiceLifetime.Singleton` when every dependency is a singleton. `IRuleRunner<>` is registered as an open generic with the same lifetime and `IRuleRegistry` as a singleton, both with `TryAdd`, so your own registration made earlier wins.

**`IRuleRunner<TSubject>`** resolves every `IBusinessRule<T>` and `IAsyncBusinessRule<T>` registered for the subject type (`Rules`, `AsyncRules`, in registration order) and runs them through the typed checker overloads, so the ledger, fault policy, `ThrowOn` threshold and observers behave exactly as for a direct `BusinessRuleChecker` call. `CheckAllAsync` / `EvaluateAllAsync` evaluate **both** lists: the sync rules adapted with `.For(subject).ToAsync()` first, then the async rules bound with `.For(subject)`. The sync `CheckAll` / `EvaluateAll` evaluate the sync rules only and throw `InvalidOperationException` (naming the codes) when async rules are registered for the subject, because a synchronous batch cannot run them without blocking; `HasAsyncRules` (on the `IRuleRunner<T>` interface) tells you in advance. `RuleRunner<T>` has a public constructor taking the two collections for tests and non-container code.

**`IRuleRegistry`.** Register rule *factories* under a set name, once for everybody (`RuleScope.Global`) and again for the scopes that need an override: `RuleRegistry.Create().AddSet(name, scope, set => ...)` or, in the container, `services.AddBusinessRules(o => o.AddRuleSet(name, scope, set => ...))` (the `(serviceProvider, set) => ...` overload runs when the registry is first resolved and can capture resolved services). Inside a layer, `set.Add(ctx => new Rule(ctx.Get(OrderKeys.Order)))` / `set.Add(rule)` add sync rules, `set.AddAsyncRule(...)` async rules (`Resolve` returns the sync list and `ResolveAsyncRules` the async list; the batch methods follow `IRuleRunner<T>`: `CheckAllAsync`/`EvaluateAllAsync` run the whole set, the sync rules adapted with `.ToAsync()` first and then the async rules, while `CheckAll`/`EvaluateAll` throw `InvalidOperationException` naming the codes when the resolved set has async rules instead of silently skipping them), `set.Disable(code)` removes every rule with that code from the layers resolved before this one, and `set.Replace(code, factory)` / `set.ReplaceAsyncRule(code, factory)` are `Disable` plus `Add`. `Build()` freezes the registry; the builder and every `RuleSetBuilder` reject mutation afterwards, so there are no mutation races. Resolution for a request scope (an explicit argument, never ambient state; `null` means `Global`) merges the layers whose scope `AppliesTo` the request from the **least to the most specific**, equally specific layers in **declaration order**; each layer first applies its disables to the rules merged so far and then appends its own rules, so a later or more specific layer always wins and a disable never reaches a layer resolved after it. Factories receive the context (a fresh one when you pass `null`), and the requested scope is the one parameterized rules resolve their values for: the batch methods store it under `RuleParameters.ScopeKey` for the duration of the call, replacing whatever the context held, and put the previous entry back afterwards, so one context reused for tenant A and then tenant B never resolves A's parameters for B (and a `null` scope with a tenant scope already in the context resolves global parameters, matching the global layers). `Resolve` / `ResolveAsyncRules` store the requested scope and leave it there, because the returned rules are evaluated later against that context. Nothing is cached (merging a handful of layers is cheap and a cache keyed by high-cardinality scopes is unbounded), an unknown set throws `RuleSetNotFoundException` (a `KeyNotFoundException` naming the set and listing `AvailableSets`), and a factory returning `null` throws `InvalidOperationException` naming the position, set and scope. `CheckAll` / `EvaluateAll` / `CheckAllAsync` / `EvaluateAllAsync` resolve and run the set in one call, with optional `RuleEvaluationOptions` / `AsyncRuleEvaluationOptions`.

### Dependency Injection Examples

```csharp
// 1. Scan an assembly: typed rules, untyped rules, IRuleRunner<T> and IRuleRegistry
services.AddBusinessRules(typeof(OrderMustHaveItemsRule).Assembly);

// or configure the scan
services.AddBusinessRules(o => o
    .AddAssemblyContaining<OrderMustHaveItemsRule>()
    .AddAssembly(typeof(PaymentRules).Assembly));          // Lifetime, IncludeNonPublicTypes, IncludeUntypedRules, TypeFilter are properties

// 2. Inject the runner; the handler never enumerates rules itself
public sealed class PlaceOrderHandler
{
    private readonly IRuleRunner<Order> _rules;

    public PlaceOrderHandler(IRuleRunner<Order> rules) => _rules = rules;

    public Task Handle(Order order, CancellationToken ct) => _rules.CheckAllAsync(order, ct);   // sync rules adapted, then async rules
}

RuleEvaluationResult result = runner.EvaluateAll(order, context);                                // sync rules only; throws if async rules exist
await runner.CheckAllAsync(order, context, new AsyncRuleEvaluationOptions { ThrowOn = RuleSeverity.Error }, ct);

// 3. A scoped registry: checkout rules for everybody, overridden for ACME
services.AddBusinessRules(o => o
    .AddRuleSet("checkout", set => set
        .Add(ctx => new CartMustHaveItemsRule(ctx.Get(OrderKeys.Order)))
        .Add(ctx => new OrderLimitRule(ctx.Get(OrderKeys.Order), parameters))
        .AddAsyncRule(ctx => new SanctionsCheckRule(ctx.Get(OrderKeys.Order), sanctionsApi)))
    .AddRuleSet("checkout", RuleScope.Tenant("acme"), (sp, set) => set
        .Disable("CART.EMPTY")                                                                  // removed from the global layer for ACME
        .Add(ctx => new AcmePurchaseOrderRequiredRule(ctx.Get(OrderKeys.Order), sp.GetRequiredService<IPurchaseOrderLookup>())))
    .AddRuleSet("checkout", RuleScope.Tenant("acme").With("region", "EU"), set => set
        .Replace("ORDER.LIMIT", ctx => new EuOrderLimitRule(ctx.Get(OrderKeys.Order)))));         // Disable + Add

public sealed class CheckoutService
{
    private readonly IRuleRegistry _registry;

    public CheckoutService(IRuleRegistry registry) => _registry = registry;

    public Task Checkout(Order order, string tenant, string region, CancellationToken ct)
    {
        BusinessRuleContext context = RuleTestHelper.CreateContext((OrderKeys.Order, order));
        RuleScope scope = RuleScope.Tenant(tenant).With("region", region);

        _registry.CheckAll("checkout", scope, context);                                         // CART.EMPTY gone, ORDER.LIMIT swapped, ACME.PO added
        return _registry.CheckAllAsync("checkout", scope, context, ct);                         // SANCTIONS.HIT
    }
}

// The same registry without a container
RuleRegistry registry = RuleRegistry.Create()
    .AddSet("checkout", set => set.Add(ctx => new CartMustHaveItemsRule(ctx.Get(OrderKeys.Order))))
    .AddSet("checkout", RuleScope.Tenant("acme"), set => set.Disable("CART.EMPTY"))
    .Build();                                                                                     // frozen: AddSet/Build now throw

IReadOnlyList<IBusinessRule> acmeRules = registry.Resolve("checkout", RuleScope.Tenant("acme"), context);   // empty for ACME
IReadOnlyList<IBusinessRule> globexRules = registry.Resolve("checkout", RuleScope.Tenant("globex"), context); // CART.EMPTY
registry.Resolve("checkuot");   // RuleSetNotFoundException: Rule registry has no set 'checkuot'. Available sets: checkout
```

Merge order, for a request `tenant=acme;region=EU` against layers declared as `Global`, `region=EU`, `tenant=acme`, `tenant=acme;region=EU`:

| Step | Layer | Specificity | Why this position |
|------|-------|-------------|-------------------|
| 1 | `Global` | 0 | least specific; applies to every request |
| 2 | `region=EU` | 1 | declared before the equally specific tenant layer |
| 3 | `tenant=acme` | 1 | declared after the region layer; its `Disable` can remove a rule the region layer added, not the reverse |
| 4 | `tenant=acme;region=EU` | 2 | most specific; a `Disable` here reaches every earlier layer |

---

## Rule Adapters

Rule adapters convert between sync and async rule interfaces. This is useful when you need to:

- Use a sync rule in an async-only context
- Use an async rule in legacy sync code

**Classes:**
- `SyncToAsyncRuleAdapter`: Wraps a sync rule to implement `IAsyncBusinessRule`
- `AsyncToSyncRuleAdapter`: Wraps an async rule to implement `IBusinessRule` (**Warning: Blocks!**)

**Extension Methods:**
- `.ToAsync()`: Convert sync to async (returns a `SyncToAsyncRuleAdapter`)
- There is deliberately no extension for the blocking direction: construct `AsyncToSyncRuleAdapter` explicitly (optionally with the `BusinessRuleContext` to use when the sync caller passes none)

> **Warning:** `AsyncToSyncRuleAdapter` blocks the calling thread using `.GetAwaiter().GetResult()`. Avoid using it on UI threads or ASP.NET request threads to prevent deadlocks.

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
IBusinessRule convertedSyncRule = new AsyncToSyncRuleAdapter(originalAsyncRule);

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
    - `OnShadowFault(IBusinessRule rule, Exception exception)`: Called after `OnShadowViolation` when the shadow-mode rule's inner rule threw (shadow mode never blocks, so the exception does not propagate)
    - `OnRuleCancelled(IBusinessRule rule, OperationCanceledException exception)`: Called when a rule's evaluation was cancelled, before the cancellation propagates
    - `OnBatchAborted(Exception exception)`: Called once, in place of `OnBatchCompleted`, when a started batch ends by an exception (a cancellation, a fault under `FaultPolicy.Throw`, a null rule)

- `IAsyncRuleExecutionObserver`: For async rule evaluation
  - Same methods with async signatures and CancellationToken (`OnBatchStartedAsync`, `OnAfterEvaluateAsync(rule, isBroken, elapsed, ct)`, `OnRuleFaultedAsync`, `OnBatchCompletedAsync`, `OnShadowViolationAsync`, `OnShadowFaultAsync`, `OnRuleCancelledAsync`, `OnBatchAbortedAsync`); the checker passes `CancellationToken.None` to the cancellation and abort hooks so they can finish their bookkeeping

Per rule the order is `OnBeforeEvaluate`, `OnAfterEvaluate(rule, result)`, `OnAfterEvaluate(rule, isBroken, elapsed)`, then `OnRuleBroken` when broken. A rule that throws gets `OnRuleFaulted` instead of the two `OnAfterEvaluate` calls (plus `OnRuleBroken` under `FaultPolicy.TreatAsBroken`); a cancelled rule gets `OnRuleCancelled` and nothing else (cancellation is never a fault). Every batch that raised `OnBatchStarted` ends with exactly one of `OnBatchCompleted` (raised before `CheckAll` throws its broken rules) or `OnBatchAborted` (the batch itself ended by an exception), so an observer that opens a span or a scope per batch can always close it.

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

## Diagnostics: Logging, Tracing and Metrics

The `Masterly.BusinessRules.Diagnostics` package plugs ready-made observers into the seams above so rule evaluation shows up in your logs, your traces and your metrics without a line of observer code. It depends only on `Microsoft.Extensions.Logging.Abstractions` and `System.Diagnostics.DiagnosticSource`; OpenTelemetry wiring is plain OTel (`AddSource` / `AddMeter`), no builder extension package is needed.

```bash
dotnet add package Masterly.BusinessRules.Diagnostics
```

**Logging.** `LoggingRuleExecutionObserver` / `LoggingAsyncRuleExecutionObserver` write one structured line per rule with the fields `RuleCode`, `RuleName`, `Category`, `Severity`, `Outcome` (`passed`, `broken`, `faulted`, `shadow`), `ElapsedMs` and, for a broken rule, `Message`. The messages are generated with `[LoggerMessage]`, so a disabled level costs one `IsEnabled` call and no allocation. The level is chosen by the rule's severity through `LoggingRuleObserverOptions.BrokenLevelBySeverity`; the defaults are conservative because an expected validation failure is not an application error: `Info` → `Debug`, `Warning` → `Information`, `Error` → `Warning`, a faulted rule → `Error` with the exception attached, batch start/completion → `Debug`, shadow violations → `Information`, and passed rules are not logged unless `LogPassedRules` is set. Map a severity to `LogLevel.None` to silence it. **The rule `Message` can carry domain data** (a customer id, an amount); set `IncludeMessage = false` to replace it with `[redacted]`. Event ids are on `RuleLogEvents` for filtering.

**Tracing.** `TracingRuleExecutionObserver` / `TracingAsyncRuleExecutionObserver` start one `BusinessRules.Batch` span per checker batch and one `BusinessRule.Evaluate` child span per rule from the `ActivitySource` named `Masterly.BusinessRules` (`BusinessRulesActivitySource.Name`), with the attributes `business_rule.code`, `business_rule.name`, `business_rule.category`, `business_rule.severity` and `business_rule.outcome` (`BusinessRuleSpanAttributes`). The span's display name is the rule code, so a trace reads as a batch with per-rule children; a faulted rule gets an error status and an OpenTelemetry `exception` event; a shadow violation becomes a `business_rule.shadow_violation` event on the batch span. The observer callbacks are deliberately **not** `async` methods, so the rule span is `Activity.Current` while the rule body runs and anything a rule starts itself (a repository query, an HTTP call) becomes a child of its rule span, in sequential and in parallel batches alike. The observers hold no per-rule state, so one instance can serve concurrent batches and the same rule instance can appear twice in a batch.

**Cardinality guard.** `TracingRuleObserverOptions.CodeTagSelector` decides which rules contribute a `code` tag; the default (`LeafCodeTagSelector`) unwraps decorators and adapters and returns `null` for composed rules (`A+B`, `A|B`, `!A`, groups, composites), so synthesised codes never become a tag or a metric series. `MessageAsEvent` (default false) adds the broken message as a `business_rule.broken` event; it is off because messages can carry domain data.

**Metrics.** The same observers record three instruments on the `Meter` named `Masterly.BusinessRules` (`BusinessRulesMetrics.MeterName`): the counter `business_rules.evaluations` tagged `code` (when selected), `category`, `severity`, `outcome`; the histogram `business_rules.evaluation.duration` in milliseconds tagged `outcome` only; and the counter `business_rules.batches` tagged `outcome` (`faulted` when any rule threw, else `broken`, else `passed`). A shadow violation is counted as an extra `outcome=shadow` evaluation beside the gated rule's own `passed` one. Set `RecordMetrics = false` to disable, or `Metrics = new BusinessRulesMetrics(meter)` to record on a meter you own (an `IMeterFactory` meter, a test meter).

**Single-rule path.** Batches are not the only path: a DDD aggregate calls `rule.Check()` directly. `rule.Traced()` wraps any rule in a `DiagnosticsBusinessRule` / `DiagnosticsAsyncBusinessRule` that records every `IsBroken` / `Check` (`IsBrokenAsync` / `EvaluateAsync` / `CheckAsync`) call as a span and a measurement, and `rule.Traced(logger)` adds the log line. The decorator forwards all seven metadata members, implements `IExplainableDecoratedBusinessRule` / `IExplainableDecoratedAsyncBusinessRule`, rethrows the inner rule's own `BusinessRuleValidationException` from `Check`, and leaves the sync `Evaluate()` factory untouched. On every walker path (a checker batch, an `And`/`Or` operand, a group member, `Explain`) it records around the walker's single evaluation of the inner rule, so a traced group keeps its member `Details` and `Explain` shows a `TRACED` node (`DiagnosticsText.TracedOperator`) with the group's subtree beneath it. Use the observer for batches and the decorator for single-rule calls; a traced rule inside a traced batch produces a nested span and a second measurement. Cancellation closes the span with `outcome=cancelled` and is neither counted nor logged.

**Aborted batches.** A batch that ends by an exception still closes every span it opened: a cancelled rule's span is finished from `OnRuleCancelled` with `outcome=cancelled`, a faulted rule's from `OnRuleFaulted`, and the batch span from `OnBatchAborted` with `business_rules.batch.outcome` `cancelled`, or `faulted` plus an error status and an `exception` event when a fault propagated under `FaultPolicy.Throw` (that faulted batch is also counted on the batch metric). Nothing stays `Activity.Current` on the caller's flow and no span is left unexported; cancellation is never counted. The span lookups walk past foreign activities a rule started and never stopped, so outcome tags and events still land on the library's own rule and batch spans.

### Diagnostics Examples

```csharp
// OpenTelemetry wiring is plain OTel
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(BusinessRulesActivitySource.Name))      // "Masterly.BusinessRules"
    .WithMetrics(m => m.AddMeter(BusinessRulesMetrics.MeterName));         // "Masterly.BusinessRules"

// Structured logging, one line per rule, level chosen by severity
LoggingRuleExecutionObserver logging = new LoggingRuleExecutionObserver(logger, new LoggingRuleObserverOptions
{
    LogPassedRules = false,
    BrokenLevelBySeverity = { [RuleSeverity.Info] = LogLevel.Debug, [RuleSeverity.Warning] = LogLevel.Information, [RuleSeverity.Error] = LogLevel.Warning }
});
BusinessRuleChecker.CheckAll(rules, context, new RuleEvaluationOptions { Observer = logging });
// warn: Business rule ORDER.NO_ITEMS (OrderMustHaveItemsRule) is broken: Order must contain at least one item. [Category=checkout Severity=Error Outcome=broken ElapsedMs=0.031]

// Spans and metrics for an async batch
TracingAsyncRuleExecutionObserver tracing = new TracingAsyncRuleExecutionObserver(new TracingRuleObserverOptions
{
    MessageAsEvent = false,
    CodeTagSelector = rule => rule is IComposedAsyncBusinessRule || rule is IComposedBusinessRule ? null : rule.Code
});
await AsyncBusinessRuleChecker.EvaluateAllAsync(context, rules, new AsyncRuleEvaluationOptions { Observer = tracing }, ct);
// Jaeger: BusinessRules.Batch
//           ├─ ORDER.NO_ITEMS        business_rule.outcome=broken
//           ├─ CUSTOMER.ACTIVE       business_rule.outcome=passed
//           └─ BusinessRule.Evaluate business_rule.outcome=passed   (a composed rule: no code tag)

// A child activity started inside a rule is parented to the rule span
public class StockAvailableRule(IInventory inventory) : BaseAsyncBusinessRule
{
    public override string Code => "ORDER.STOCK";
    public override string Message => "Every item must be in stock.";

    public override async Task<bool> IsBrokenAsync(BusinessRuleContext ctx, CancellationToken ct)
        => !await inventory.IsAvailableAsync(ctx.Get<Order>("order"), ct);   // the HttpClient span lands under ORDER.STOCK
}

// Single-rule path: the dominant DDD call site
IBusinessRule traced = new OrderMustHaveItemsRule(order).Traced();            // span + metrics
IBusinessRule tracedAndLogged = new OrderMustHaveItemsRule(order).Traced(logger);
traced.Check();

// Record on a meter you own (IMeterFactory, tests)
BusinessRulesMetrics metrics = new BusinessRulesMetrics(meterFactory.Create(BusinessRulesMetrics.MeterName));
TracingRuleObserverOptions owned = new TracingRuleObserverOptions { Metrics = metrics };
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

One options object per side (`RuleEvaluationOptions` for `BusinessRuleChecker`, `AsyncRuleEvaluationOptions` for `AsyncBusinessRuleChecker`) replaces the growing pile of boolean parameters. Every checker call, including the boolean overloads, runs through a single core that records an **evaluation ledger**: one `RuleEvaluationEntry` per submitted rule with its `Order` (submission index), `Outcome` (`Passed`, `Broken`, `Faulted`, or `NotEvaluated` after a fail-fast stop), `Result`, `Fault` and `Duration`. The result exposes the ledger as `Entries`, plus `Faulted`, `HasFaults`, `TotalDuration` (batch wall clock) and `Slowest`. Timing uses `Stopwatch.GetTimestamp()` tick math, so no `Stopwatch` is allocated.

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

"Transaction amount exceeds limit" is useless to the user who needs to know it was 5,200 against a 5,000 limit. Every `BusinessRuleResult` carries the rule's **`Name`, `Category` and `Tags`** (populated at every construction site: base classes, builders, decorators, adapters, composites, groups and the checkers), so an exception or a ledger can be filtered by category or tag and mapped to an RFC 7807 Problem Details payload without looking the rule up again. A rule can additionally attach rich violation details through a **`Describe(context)`** hook: a formatted `Detail` sentence, the `Actual` value that broke it, the `Expected` value or range, a `Remediation` hint and a free-form `Data` bag.

**How it works:**
- `RuleResultDetails` is an immutable fluent bag: start with `RuleResultDetails.Detail($"...")` or `RuleResultDetails.Empty`, then chain `.Actual(...)`, `.Expected(...)`, `.Remediation(...)`, `.With(key, value)`
- `BaseBusinessRule` and `BaseAsyncBusinessRule` expose `protected virtual RuleResultDetails Describe(BusinessRuleContext? context)`. It runs **only on the broken path** (never from `IsBroken`, never for a passing rule) and is guarded: an exception inside `Describe` degrades the details to `RuleResultDetails.Empty` so it can never mask the violation it describes. Cancellation propagates
- `IBusinessRule` has `Evaluate(BusinessRuleContext context)` (a default interface member falling back to `Evaluate()`), implemented explicitly on every in-library node, decorator, adapter and composite. `Check(context)`, `CompositeBusinessRule.EvaluateAll(context)` and the checkers route broken leaves through it, so `Describe` sees the same context the rule was evaluated with. Both `Evaluate` overloads stay pure factories: they build the result without re-evaluating the rule
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

## Testing Package: Coverage, Boundaries and Properties

`Masterly.BusinessRules.Testing` answers three questions `RuleTestHelper` cannot: which rules has the suite never seen both pass **and** fail, at exactly which value does a rule flip, and does a rule family obey its laws for every input, not just the three you thought of. It targets `net8.0` (CsCheck ships no .NET Standard build) and references the core plus `CsCheck`.

```bash
dotnet add package Masterly.BusinessRules.Testing
```

Everything lives in the `Masterly.BusinessRules.Testing` namespace.

**Outcome coverage.** `RuleCoverage.Begin()` starts a `RuleCoverageSession`. `.WithCoverage(session)` wraps an `IBusinessRule` in `CoverageBusinessRule`, or an `IAsyncBusinessRule` in `CoverageAsyncBusinessRule`. The decorators forward all seven metadata members and the verdict unchanged, and record one outcome per evaluation under the rule's `Code`:

- Sync rules record from both `IsBroken` overloads and both `Check` overloads. The `Evaluate()` factories don't evaluate anything, so they record nothing.
- Async rules record from `IsBrokenAsync`, `EvaluateAsync` and `CheckAsync`.
- A rule that throws or is cancelled records nothing.

Wrapping a rule registers its code, so a rule the suite never reached still shows up in the report. Wrap the operands **before** composing them: an `And`/`Or` node short-circuits, and only wrapped operands make the skipped operand visible. The decorator records outcomes itself rather than through observers, so the boolean `EvaluateAll` overloads are covered too. Both decorators implement the core `IExplainableDecoratedBusinessRule` / `IExplainableDecoratedAsyncBusinessRule` seam: inside `And`/`Or`/`Not` or a group they record through the same single evaluation, and `Explain` / `ExplainAsync` show a `COVERAGE` node with the wrapped rule's own explanation tree beneath it (one recorded outcome per explain call). This is outcome coverage per rule code; it makes no MC/DC claims.

`session.Report()` takes an immutable `RuleCoverageReport` snapshot:

- `Entries`: one `RuleCoverageEntry` per code, sorted ordinally, with `BrokenCount`, `PassedCount` and `IsFullyCovered`.
- `OutcomeCoverage`: observed outcomes divided by `2 × rules`. An empty report has `0.0`.
- `NeverBroken`, `NeverPassed` and `NeverEvaluated`: the codes missing each outcome.
- `ToText()`: a printable table.
- `AssertMinimum(outcomeCoverage)`: throws `RuleAssertionException` naming the uncovered rules.

Recording is thread-safe. Disposing the session stops recording; the report stays readable.

**Boundary finder.** `RuleBoundaryFinder.Find<T>(factory, from, to, step)` bisects the grid `from, from + step, …, to` over a factory-built rule. `FindAsync<T>(..., cancellationToken)` is the async twin. Both return a `RuleBoundary<T>`:

- `LastPassing` and `FirstBroken`: adjacent grid points, one `Step` apart.
- `Monotonicity`: whether the rule breaks above or below the boundary.
- `ProbeCount`: how many rules were built.

The finder takes a factory rather than a context key because rules usually receive their data through the constructor. `step` is mandatory because it sets the resolution of the answer. `T` is any `INumber<T>` (`decimal`, `int`, `double`, …). The sync finder evaluates `IsBroken()`. The async finder evaluates `IsBrokenAsync` with one empty context and checks the token before every probe.

After bisection, the finder probes `MonotonicitySampleCount` (32) evenly spread grid points. It throws `RuleAssertionException` naming the contradicting pair when a sample disagrees with the located boundary, and when the rule has the same verdict at both ends of the range.

**Rule properties.** `RuleProperties.For<T>(factory, generator)` takes a CsCheck `Gen<T>`. It returns `RuleProperties<T>`, or `AsyncRuleProperties<T>` for an async factory. Add laws fluently, then call `Verify(iterations, seed)` or `VerifyAsync(iterations, seed, cancellationToken)`:

- `MonotoneIn(Monotonicity.BrokenAbove | BrokenBelow, comparer?)`: for any two generated inputs `a ≤ b`, broken at `a` implies broken at `b` (`BrokenAbove`), or broken at `b` implies broken at `a` (`BrokenBelow`). Inputs that aren't comparable need an `IComparer<T>`.
- `ContextPure()` / `ContextPure(contextFactory)`: evaluating the rule doesn't add, remove or replace a key in the context. Writes are detected through `BusinessRuleContext.Keys` and the stored values. Mutation inside an object already stored in the context can't be detected.

Sampling uses CsCheck's generators and its size-based shrinking. It runs single-threaded from one seed, so `Verify(seed: 42)` replays the same inputs and finds the same counter-example every time. Without a seed, one is drawn and printed in the failure message.

How each outcome surfaces:

- A violated law throws `RuleAssertionException` with the shrunk counter-example, the seed, the shrink count and the law's own message. A rule that throws counts as a violation too.
- A factory that returns null, or a generator that can't produce a value, throws `InvalidOperationException`.
- Cancellation throws `OperationCanceledException`.

Neither of the last two is reported as a failed law. There are no deterministic or idempotent laws, because they don't hold for repository-backed async rules or for `CachedBusinessRule`.

> **CsCheck caveat:** in CsCheck 4.9.1, `Gen.Decimal[start, finish]` can't generate values when a bound is exactly `0m`; it fails with "Failing Where max count". `Verify` reports this as an `InvalidOperationException`. Use a bound such as `0.01m`, or build amounts from an integer generator: `Gen.Long[0L, 10_000_000L].Select(cents => cents / 100m)`.

**Smoke-mode mutants.** `.Invert()`, `.AlwaysPass()` and `.DowngradeSeverity()` (sync and async) wrap a rule so it misbehaves in a known way while keeping its `Code`, `Message` and other metadata:

- `InvertedBusinessRule` is broken exactly when the original passes.
- `AlwaysPassingBusinessRule` never breaks and never evaluates the original.
- `DowngradeSeverity` steps the severity down one level: `Error` → `Warning` → `Info`.

The inverted and always-passing mutants implement the same explain seam as the coverage decorators, so an explain tree shows an `INVERTED` node over the evaluated original, or an `ALWAYS PASSING` node whose original is `NotEvaluated`; `DowngradeSeverity` is the core `WithSeverity` decorator (a `SEVERITY` node).

If your suite still passes against a mutant, it hasn't pinned the rule down. This is a smoke test, not mutation testing: real rule bugs live inside `IsBroken`, and only a source-level mutator such as [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) reaches them.

### Testing Package Examples

```csharp
using CsCheck;
using Masterly.BusinessRules;
using Masterly.BusinessRules.Testing;

// Outcome coverage
using RuleCoverageSession coverage = RuleCoverage.Begin();
IBusinessRule Checks(Customer customer) =>
    new CustomerIsAdultRule(customer).WithCoverage(coverage).And(new CustomerIsActiveRule(customer).WithCoverage(coverage));

BusinessRuleChecker.EvaluateAll(Checks(inactiveMinor));   // both rules broken
BusinessRuleChecker.EvaluateAll(Checks(activeAdult));     // CUSTOMER.ADULT passes, so the And short-circuits

RuleCoverageReport report = coverage.Report();
Console.WriteLine(report.ToText());
// CUSTOMER.ADULT     broken ✔  passed ✔
// CUSTOMER.INACTIVE  broken ✔  passed ✘   <- never exercised on the happy path
// outcome coverage 75% (3 of 4 outcomes, 1 of 2 rules fully covered)
report.AssertMinimum(outcomeCoverage: 1.0);   // RuleAssertionException naming CUSTOMER.INACTIVE

// A rule whose verdict depends on the amount alone (the finder and the properties call the parameterless IsBroken)
public class DailyLimitRule : BaseBusinessRule
{
    private readonly decimal _amount;

    public DailyLimitRule(decimal amount) => _amount = amount;

    public override string Code => "DAILY.LIMIT";
    public override string Message => "The daily limit of 10 000 is exceeded.";
    public override bool IsBroken() => _amount > 10_000m;
}

// Boundary finder
RuleBoundary<decimal> boundary = RuleBoundaryFinder.Find<decimal>(
    amount => new DailyLimitRule(amount), from: 0m, to: 1_000_000m, step: 0.01m);
// boundary.LastPassing == 10000.00m, boundary.FirstBroken == 10000.01m
// boundary.ToString() == "passes up to 10000.00, breaks from 10000.01 (… probes)"

RuleBoundary<decimal> asyncBoundary = await RuleBoundaryFinder.FindAsync<decimal>(
    amount => new AsyncLimitRule(amount), 0m, 1_000_000m, 0.01m, cancellationToken);

// Rule properties
RuleProperties.For<decimal>(amount => new DailyLimitRule(amount), Gen.Decimal[0.01m, 100_000m])
    .MonotoneIn(Monotonicity.BrokenAbove)
    .ContextPure()
    .Verify(iterations: 500, seed: 42);

await RuleProperties.For<int>(age => new AsyncMinimumAgeRule(age), Gen.Int[0, 120])
    .MonotoneIn(Monotonicity.BrokenBelow)
    .VerifyAsync(iterations: 500, seed: 42, cancellationToken: cancellationToken);

// Smoke-mode mutant: the suite must notice that the rule now fires on the happy path
IBusinessRule mutant = new CustomerIsAdultRule(adultCustomer).Invert();
Assert.Throws<BusinessRuleValidationException>(() => BusinessRuleChecker.CheckAll(new[] { mutant }));
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

### Multi-Tenant Order Approval with an Audit Trail

```csharp
// One stateless rule instance per type; the subject arrives at evaluation time
public sealed class OrderMustHaveItemsRule : BaseBusinessRule<Order>
{
    public override string Code => "ORDER.NO_ITEMS";
    public override string Message => "Order must contain at least one item.";
    public override bool IsBroken(Order order) => order.Items.Count == 0;
}

// The tenant limit is a scoped parameter, not a constant
IBusinessRule<Order> withinLimit = BusinessRuleBuilder<Order>.Create("ORDER.TOO_LARGE")
    .WithMessage("Order exceeds the tenant limit.")
    .WithParameters(tenantLimits)                                    // an IRuleParameterProvider
    .WithParameter<decimal>("maxOrderAmount", fallback: 10_000m)
    .WithActual((order, ctx) => order.Total)
    .When((order, ctx, p) => order.Total > p.Get<decimal>("maxOrderAmount"))
    .Build();

public IReadOnlyList<string> Approve(Order order, string tenantId)
{
    BusinessRuleContext context = new BusinessRuleContext();
    context.Set(RuleParameters.ScopeKey, RuleScope.Tenant(tenantId));

    IBusinessRule approval = RuleGroup.All("ORDER.REJECTED", "The order cannot be placed",
        new OrderMustHaveItemsRule().For(order),
        withinLimit.For(order));

    // Explain is the evaluation: one walk yields the verdict and the audit trail
    RuleExplanation why = approval.Explain(context);
    _audit.Write(why.ToText());

    return why.BrokenLeaves().Select(leaf => $"{leaf.Code}: {leaf.Message}").ToList();
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
| `IExplainableDecoratedBusinessRule` / `IExplainableDecoratedAsyncBusinessRule` | Public seam for decorators written outside the library: `ExplanationOperator`, `EvaluateDecorated(DecoratedRuleEvaluation)` / `EvaluateDecoratedAsync(AsyncDecoratedRuleEvaluation, ct)`; the walkers and `Explain` treat them as decorator nodes instead of leaves |
| `IBuiltRuleFactory` / `IAsyncBuiltRuleFactory` | Builder seam: `Create(BuiltRuleDefinition)` decides the rule type `Build()` returns (set with `WithRuleFactory`) |
| `IRuleExecutionObserver` | Sync execution callbacks plus batch hooks `OnBatchStarted`, `OnAfterEvaluate(rule, isBroken, elapsed)`, `OnRuleFaulted`, `OnRuleCancelled`, `OnBatchCompleted`, `OnBatchAborted`, `OnShadowViolation`, `OnShadowFault` |
| `IAsyncRuleExecutionObserver` | Async execution callbacks plus the async batch hooks (`OnRuleCancelledAsync`, `OnBatchAbortedAsync` included), `OnShadowViolationAsync` and `OnShadowFaultAsync` |
| `IRuleFeatureFlagProvider` / `IAsyncRuleFeatureFlagProvider` | Supply a flag's `RuleRollout` to gated rules: `GetRollout(flag, context)` / `GetRolloutAsync(flag, context, ct)` |
| `IRuleParameterProvider` / `IAsyncRuleParameterProvider` | Supply named, scope-layered parameter values to built rules: `TryGet<T>(name, scope, out value)` / `TryGetAsync<T>(name, scope, ct)` returning `RuleParameterLookup<T>` |
| `IRuleConditionCompiler` / `IAsyncRuleConditionCompiler` | The Expressions seam: `Compile(expression)` returns the sync / async broken-condition delegate a built rule evaluates (`When(compiler, text)` / `WhenAsync(compiler, text)`) |

### Classes

| Class | Description |
|-------|-------------|
| `BaseBusinessRule` | Abstract base for sync rules; virtual `Evaluate()`/`Evaluate(context)` and the `protected virtual Describe(context)` hook |
| `BaseAsyncBusinessRule` | Abstract base for async rules; the `protected virtual Describe(context)` hook |
| `BusinessRuleBuilder` | Fluent builder for sync rules; `WithDetail`, `WithActual`, `WithExpected`, `WithRemediation`; `When((ctx, p) => ...)` with parameters, `When(compiler, expression)` and `WithRuleFactory(IBuiltRuleFactory)` |
| `AsyncBusinessRuleBuilder` | Fluent builder for async rules; `WithDetail`, `WithActual`, `WithExpected`, `WithRemediation`; `WhenAsync((ctx, p, ct) => ...)` with parameters, `WithParameters(asyncProvider)`, `WhenAsync(compiler, expression)` and `WithRuleFactory(IAsyncBuiltRuleFactory)` |
| `BuiltRuleDefinition` | The builder snapshot handed to a rule factory: the seven metadata members, `HasDetails`, `ResolveParameters(context)`, `ResolveParametersAsync(context, ct)`, `CreateResult(rule, context)` |
| `DecoratedRuleEvaluation` / `AsyncDecoratedRuleEvaluation` | One-shot evaluation of a custom decorator's inner rule: `InnerRule`, `Context`, `IsExplaining`, `IsInnerEvaluated`, `EvaluateInner()` / `EvaluateInnerAsync(ct)` (evaluates at most once) |
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
| `SyncToAsyncRuleAdapter` / `SyncToAsyncRuleAdapter<T>` | Adapts sync rules to async; the generic one bridges `IBusinessRule<T>` to `IAsyncBusinessRule<T>` without binding a subject |
| `AsyncToSyncRuleAdapter` | Adapts async rules to sync (blocking) |
| `BusinessRuleChecker` | Batch sync validation; options-taking `CheckAll`/`EvaluateAll` overloads; subject-typed `CheckAll(subject, rules)`, `EvaluateAll(subject, rules)` (also with the rules listed directly: `CheckAll(order, overdue, big)`), `CheckBySeverity/Category/Tags(subject, rules, ...)` and `EvaluateEach(subjects, rules)` |
| `AsyncBusinessRuleChecker` | Batch async validation; options-taking `CheckAllAsync`/`EvaluateAllAsync` overloads; `CheckByTagsAsync` (also `CheckByTagsAsync(context, rules, "pci", "gdpr")` without a token); subject-typed `CheckAllAsync([context,] subject, rules)`, `EvaluateAllAsync` (also `CheckAllAsync(context, order, ruleA, ruleB)`), `CheckBySeverity/Category/TagsAsync` (tokenless params forms too) and `EvaluateEachAsync`. Argument order: a context, when given, comes first on every async overload; the sync checker takes `(subject, rules, context)` and `IRuleRunner<T>` `(subject, context, ...)` |
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
| `.ToAsync()` | Convert sync to async (`SyncToAsyncRuleAdapter`; on an `IBusinessRule<T>`, `SyncToAsyncRuleAdapter<T>` so a typed sync rule joins typed async compositions and batches); the blocking direction has no extension, construct `AsyncToSyncRuleAdapter` |
| `.Cached(TimeSpan)` | Add per-instance caching |
| `.Cached(TimeSpan, cacheKeySelector)` | Add per-key caching (one verdict per tenant, region, ...) |
| `.When(condition)` | Add conditional execution: `Func<bool>` on `IBusinessRule`, `Func<BusinessRuleContext, bool>` on `IAsyncBusinessRule` |
| `.WithSeverity(RuleSeverity)` | Wrap in a severity-override decorator (sync and async) |
| `.GatedBy(flag, provider, stableKey?)` / `.GatedBy(flag, stableKey?)` | Wrap in a feature gate read from the provider on every evaluation (sync and async; the async form accepts a sync or an async provider, and the provider-less form resolves it from `RuleFeatureFlags.ContextKey`/`AsyncContextKey`) |
| `.Check(throwOn)` / `.Check(context, throwOn)` | Check one sync rule, throwing only at or above the severity |
| `.CheckAsync(context, throwOn, ct)` | Check one async rule, throwing only at or above the severity |

### Masterly.BusinessRules.FeatureManagement

| Type | Description |
|------|-------------|
| `FeatureManagementRuleFeatureFlagProvider` | `IRuleFeatureFlagProvider` + `IAsyncRuleFeatureFlagProvider` over `IVariantFeatureManager` (variants consulted) or `IFeatureManager` (enabled state only); `Options`, `SupportsVariants`; the sync side blocks on the async one |
| `FeatureManagementRolloutOptions` | `EnabledRollout` (default `Enforced`), `OnUnrecognizedVariant` (default `Throw`), `TargetingContextSelector`, `VariantRollouts`, `MapVariant(name, rollout)` / `MapVariant(name, mode, percent)`, `TryGetVariantRollout` |
| `UnrecognizedVariantBehavior` | `Throw`, `Off`, `UseEnabledState` |
| `RolloutVariantConvention` | `TryParse(name)` / `Parse(name)` for variant names (`Shadow`, `Enforced-10`, `Off`), `TryRead(section)` for `Mode`/`Percent` configuration; `ModeKey`, `PercentKey`, `Separators`, `PercentSuffix` |
| `RolloutVariantFormatException` | Derives from `FormatException`; `Flag`, `Variant`; thrown for an unmappable variant under `Throw` |
| `services.AddBusinessRulesFeatureManagement(configure?, lifetime?)` | Registers the provider as itself and as both provider interfaces over the registered feature manager (`TryAdd`; default `Singleton`) |

### Masterly.BusinessRules.DependencyInjection

| Member | Description |
|--------|-------------|
| `services.AddBusinessRules(params Assembly[])` / `services.AddBusinessRules(Action<BusinessRuleOptions>)` | Scan assemblies for `IBusinessRule<T>` / `IAsyncBusinessRule<T>` (and plain `IBusinessRule` / `IAsyncBusinessRule`) types and register them (`TryAddEnumerable`), the open generic `IRuleRunner<>` and the `IRuleRegistry` singleton (`TryAdd`) |
| `BusinessRuleOptions` | `Lifetime` (default `Scoped`), `IncludeNonPublicTypes`, `IncludeUntypedRules` (default false), `TypeFilter`, `AddAssembly` / `AddAssemblies` / `AddAssemblyContaining<T>`, `AddRuleSet(name[, scope], set => ...)` and `AddRuleSet(name[, scope], (serviceProvider, set) => ...)` |
| `IRuleRunner<TSubject>` / `RuleRunner<TSubject>` | The registered typed rules of one subject type (`Rules`, `AsyncRules`, `HasAsyncRules`): `CheckAll` / `EvaluateAll` run the sync rules (throw `InvalidOperationException` when async rules exist); `CheckAllAsync` / `EvaluateAllAsync` run the sync rules adapted, then the async rules; options overloads on both sides |
| `IRuleRegistry` / `RuleRegistry` | Named rule sets layered per `RuleScope`: `SetNames`, `Contains`, `Resolve(name, scope?, context?)`, `ResolveAsyncRules(...)`, `CheckAll` / `EvaluateAll` (throw when the set has async rules) / `CheckAllAsync` / `EvaluateAllAsync` (sync rules adapted, then async rules) with options; the requested scope drives layers and parameters and is restored on the context afterwards; least specific layer first, declaration order on ties; `RuleRegistry.Create()` starts a builder |
| `RuleRegistryBuilder` | `AddSet(name[, scope], configure)`, `LayerCount`, `Build()`; frozen after `Build` |
| `RuleSetBuilder` | One layer: `Add(factory)` / `Add(rule)`, `AddAsyncRule(factory)` / `AddAsyncRule(rule)`, `Disable(code)`, `Replace(code, factory)`, `ReplaceAsyncRule(code, factory)`, `Name`, `Scope`, `RuleCount`, `AsyncRuleCount`, `DisabledCodes`; frozen once its `AddSet` call returns |
| `RuleSetNotFoundException` | Derives from `KeyNotFoundException`; `SetName`, `AvailableSets`; thrown for an unknown set name |

### Diagnostics Package

| Type | Description |
|------|-------------|
| `LoggingRuleExecutionObserver` / `LoggingAsyncRuleExecutionObserver` | `IRuleExecutionObserver` / `IAsyncRuleExecutionObserver` writing one structured `ILogger` line per rule (`RuleCode`, `RuleName`, `Category`, `Severity`, `Outcome`, `ElapsedMs`, `Message`) plus batch, fault and shadow lines; `Options` |
| `LoggingRuleObserverOptions` | `LogPassedRules`, `PassedLevel`, `BrokenLevelBySeverity`, `DefaultBrokenLevel`, `BrokenLevelFor(severity)`, `FaultedLevel`, `LogBatches`, `BatchLevel`, `ShadowViolationLevel`, `IncludeMessage`, `RedactedMessage` |
| `RuleLogEvents` | The `EventId`s: `RuleBroken` (1), `RulePassed` (2), `RuleFaulted` (3), `BatchStarted` (4), `BatchCompleted` (5), `ShadowViolation` (6) |
| `TracingRuleExecutionObserver` / `TracingAsyncRuleExecutionObserver` | Observers producing a `BusinessRules.Batch` span with a `BusinessRule.Evaluate` child per rule and recording the metrics; callbacks complete synchronously so the rule span is `Activity.Current` inside the rule; `Options` |
| `TracingRuleObserverOptions` | `MessageAsEvent` (default false), `CodeTagSelector` (default `LeafCodeTagSelector`: null for composed rules), `RecordMetrics` (default true), `Metrics` (default `BusinessRulesMetrics.Default`) |
| `BusinessRulesActivitySource` | `Name` (`Masterly.BusinessRules`), `Source`, `RuleActivityName` (`BusinessRule.Evaluate`), `BatchActivityName` (`BusinessRules.Batch`) |
| `BusinessRuleSpanAttributes` | Attribute and event names: `business_rule.code`/`.name`/`.category`/`.severity`/`.outcome`/`.message`, `business_rule.broken`, `business_rule.shadow_violation`, `exception` (+ `exception.type`, `exception.message`), `business_rules.batch.rule_count`/`.broken_count`/`.faulted_count`/`.outcome` |
| `BusinessRulesMetrics` | `MeterName` (`Masterly.BusinessRules`), `Default`, `Meter`, the instruments `business_rules.evaluations` (`Counter<long>`; tags `code?`, `category`, `severity`, `outcome`), `business_rules.evaluation.duration` (`Histogram<double>`, ms; tag `outcome`), `business_rules.batches` (`Counter<long>`; tag `outcome`); `RecordEvaluation(rule, code, outcome, duration)`, `RecordBatch(outcome)`; constructor `(Meter)` for an owned meter |
| `RuleOutcomeTags` / `RuleSeverityTags` | Tag values `passed`, `broken`, `faulted`, `shadow`, `cancelled` (`ForVerdict`, `ForBatch`) and `info`, `warning`, `error` (`From`) |
| `DiagnosticsBusinessRule` / `DiagnosticsAsyncBusinessRule` | `IExplainableDecoratedBusinessRule` / `IExplainableDecoratedAsyncBusinessRule` recording every single-rule evaluation as a span, a measurement and (with a logger) a log line; `InnerRule`, `Options`, `IsLogging`, `ExplanationOperator` (`TRACED`); forwards all metadata, keeps a composed inner rule's `Details` and explanation subtree on walker paths, rethrows the inner `BusinessRuleValidationException` |
| `.Traced(options?)` / `.Traced(logger, options?, loggingOptions?)` | Extensions wrapping an `IBusinessRule` / `IAsyncBusinessRule` in the diagnostics decorator |

### Expressions Package

| Type | Description |
|------|-------------|
| `RuleExpression` | `Parse(text[, options])`, `TryParse`, `Text`, `Options`, `Identifiers`, `ParameterNames`, `Depth`; `Evaluate(context, parameters?)`, `EvaluateAsync(context, asyncParameters?, parameters?, ct)`, `Explain(context, redact?, parameters?)`, `ExplainAsync(...)`, `ToRule(code, message, parameters?, severity?)`, `ToAsyncRule(...)`, `Compile<T>([parameters])`; constants `DataIdentifier` (`Data`) and `TypedDataKey` (`__typedData`) |
| `RuleExpressionOptions` | Immutable: `Default`, `MaxLength` (4096), `MaxDepth` (64), `MemberFilter`, `RequireRuleAccessibleAttribute`, `Culture` (invariant) and the matching `With...` methods |
| `RuleAccessibleAttribute` | `[RuleAccessible]` on a property or type: the only members readable when `RequireRuleAccessibleAttribute` is set |
| `ExpressionBusinessRule` / `ExpressionAsyncBusinessRule` | `BaseBusinessRule` / `BaseAsyncBusinessRule` over a `RuleExpression`, created by `ToRule`/`ToAsyncRule`, the constructors or `WhenExpression(...).Build()` (then carrying the builder's name, description, category, tags, details and parameters): `Expression`, `Parameters` (async: `AsyncParameters`), `Description` = expression text unless configured, `ExplainExpression(context, redact?)` / `ExplainExpressionAsync(context, redact?, ct)`; the sync parameterless `IsBroken()` throws `InvalidOperationException` (`ContextRequiredMessage`) |
| `RuleExpressionExplanation` | `Expression`, `Result`, `Steps`, `Rendered` (`ToString()`) |
| `RuleExpressionStep` / `RuleExpressionStepKind` | `Kind` (`Binding`, `Operation`), `NodeText`, `BoundValue`, `Result` |
| `RuleExpressionRedactor` | `delegate string? (string name, object? value)` deciding how a bound value is displayed |
| `RuleExpressionParseException` | Malformed text, unknown typed member or exceeded limit; `Line`, `Column` (1-based) |
| `RuleExpressionBindingException` | Unbound identifier, parameter or member; `Identifier`, `AvailableKeys` (sorted) |
| `RuleExpressionEvaluationException` | Operator applied to unsupported operands, non-boolean root, decimal division by zero or overflow; `NodeText` |
| `RuleExpressionConditionCompiler` | `IRuleConditionCompiler` + `IAsyncRuleConditionCompiler` (explicit; also `CompileAsyncCondition`); `Options`, `Parameters`, `AsyncParameters` |
| `.WhenExpression(text[, options])` / `.WhenExpression(expression)` | Extensions on `BusinessRuleBuilder` and `AsyncBusinessRuleBuilder`; parse at builder time, builder-declared parameters feed `@name`, and `Build()` returns an `ExpressionBusinessRule` / `ExpressionAsyncBusinessRule` (through `WithRuleFactory`) |

### Testing Package

| Type | Description |
|------|-------------|
| `RuleCoverage` | `Begin()` starts a `RuleCoverageSession` |
| `RuleCoverageSession` | `IDisposable`; `Register(rule)`, `Record(rule, isBroken)`, `Report()`, `Reset()`, `RuleCount`, `IsEnded`; thread-safe; disposing it stops recording |
| `CoverageBusinessRule` / `CoverageAsyncBusinessRule` | `IExplainableDecoratedBusinessRule` / `IExplainableDecoratedAsyncBusinessRule` recording each verdict per `Code` (a `COVERAGE` node in explain trees); forward all seven metadata members; `InnerRule`, `Session`, `EvaluateDecorated` / `EvaluateDecoratedAsync` |
| `.WithCoverage(session)` | Extensions on `IBusinessRule` / `IAsyncBusinessRule` returning the coverage decorator |
| `RuleCoverageReport` | `Entries`, `RuleCount`, `FullyCoveredCount`, `CoveredOutcomes`, `TotalOutcomes`, `OutcomeCoverage`, `IsComplete`, `NeverBroken`, `NeverPassed`, `NeverEvaluated`, `Find(code)`, `AssertMinimum(outcomeCoverage)` (throws `RuleAssertionException`), `ToText()` |
| `RuleCoverageEntry` | `Code`, `Name`, `BrokenCount`, `PassedCount`, `SeenBroken`, `SeenPassed`, `WasEvaluated`, `IsFullyCovered`, `CoveredOutcomes`; `OutcomesPerRule` (2) |
| `RuleBoundaryFinder` | `Find<T>(factory, from, to, step)`, `FindAsync<T>(factory, from, to, step, ct)` for `T : INumber<T>`; `MonotonicitySampleCount` (32) |
| `RuleBoundary<T>` | `LastPassing`, `FirstBroken`, `Monotonicity`, `ProbeCount`, `Step`; `ToString()` renders `passes up to …, breaks from …` |
| `Monotonicity` | `BrokenAbove`, `BrokenBelow` |
| `RuleProperties` | `For<T>(Func<T, IBusinessRule>, Gen<T>)` returns `RuleProperties<T>`; `For<T>(Func<T, IAsyncBusinessRule>, Gen<T>)` returns `AsyncRuleProperties<T>`; `DefaultIterations` (100) |
| `RuleProperties<T>` / `AsyncRuleProperties<T>` | `MonotoneIn(monotonicity, comparer?)`, `ContextPure()`, `ContextPure(contextFactory)`, `LawCount`, `LawNames`, `Verify(iterations, seed?)` / `VerifyAsync(iterations, seed?, ct)` |
| `InvertedBusinessRule` / `InvertedAsyncBusinessRule` | Smoke-mode mutant, broken exactly when the inner rule passes; keeps all metadata; an `INVERTED` explain node |
| `AlwaysPassingBusinessRule` / `AlwaysPassingAsyncBusinessRule` | Smoke-mode mutant that never breaks and never evaluates the inner rule; an `ALWAYS PASSING` explain node over a `NotEvaluated` inner rule |
| `.Invert()`, `.AlwaysPass()`, `.DowngradeSeverity()` | Extensions on `IBusinessRule` / `IAsyncBusinessRule`; `DowngradeSeverity` returns a severity-override decorator one level lower (`Info` is the floor) |

### Packages

| Package | Contents |
|---------|----------|
| `Masterly.BusinessRules` | The core: every type listed above; no dependencies |
| `Masterly.BusinessRules.Diagnostics` | `ILogger` observers, OpenTelemetry tracing/metrics observers and the `Traced()` decorators (see [Diagnostics: Logging, Tracing and Metrics](#diagnostics-logging-tracing-and-metrics)) |
| `Masterly.BusinessRules.Expressions` | Rule expression parser, expression rules and bound-value explanations (see [Rule Expressions](#rule-expressions)) |
| `Masterly.BusinessRules.DependencyInjection` | `IServiceCollection` registration, rule runners and the scoped rule registry (see [Dependency Injection](#dependency-injection)) |
| `Masterly.BusinessRules.Testing` | Outcome coverage, boundary finder, CsCheck-backed rule properties and smoke-mode mutants (see [Testing Package: Coverage, Boundaries and Properties](#testing-package-coverage-boundaries-and-properties)) |
| `Masterly.BusinessRules.FeatureManagement` | `IRuleFeatureFlagProvider` / `IAsyncRuleFeatureFlagProvider` over `Microsoft.FeatureManagement` |

---

## License

MIT - use it freely in your projects.
