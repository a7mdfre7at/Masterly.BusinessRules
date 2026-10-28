# Masterly.BusinessRules

A composable business rule engine for .NET.

<img src="https://raw.githubusercontent.com/a7mdfre7at/Masterly.BusinessRules/master/repo_image.png" alt="Masterly.BusinessRules logo" width="200" height="180">

[![Nuget](https://img.shields.io/nuget/v/Masterly.BusinessRules?style=flat-square)](https://www.nuget.org/packages/Masterly.BusinessRules) ![Nuget](https://img.shields.io/nuget/dt/Masterly.BusinessRules?style=flat-square) ![GitHub last commit](https://img.shields.io/github/last-commit/a7mdfre7at/Masterly.BusinessRules?style=flat-square) ![GitHub](https://img.shields.io/github/license/a7mdfre7at/Masterly.BusinessRules) [![Build](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/build.yml/badge.svg?branch=master)](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/build.yml) [![CodeQL Analysis](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/codeql.yml/badge.svg?branch=master)](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/codeql.yml) [![Publish to NuGet](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/publish.yml/badge.svg?branch=master)](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/publish.yml)


## Table of Contents

- [Overview](#overview)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Writing Rules](#writing-rules)
  - [Sync rules](#sync-rules)
  - [Async rules](#async-rules)
  - [Three ways to run a rule](#three-ways-to-run-a-rule)
  - [Rule metadata](#rule-metadata)
  - [Passing data with a context](#passing-data-with-a-context)
  - [Typed context keys](#typed-context-keys)
- [Building Rules Without Classes](#building-rules-without-classes)
  - [Sync builder](#sync-builder)
  - [Async builder](#async-builder)
- [Subject-Typed Rules](#subject-typed-rules)
- [Combining Rules](#combining-rules)
  - [And, Or and Not](#and-or-and-not)
  - [Rule groups](#rule-groups)
  - [Composite rules](#composite-rules)
- [Understanding Failures](#understanding-failures)
  - [Explain trees](#explain-trees)
  - [Rich results](#rich-results)
- [Running Many Rules](#running-many-rules)
  - [Check all or evaluate all](#check-all-or-evaluate-all)
  - [Filtering by severity, category or tags](#filtering-by-severity-category-or-tags)
  - [Reading the result](#reading-the-result)
  - [Options](#options)
  - [When a rule throws](#when-a-rule-throws)
  - [Warnings that do not block](#warnings-that-do-not-block)
  - [The evaluation ledger](#the-evaluation-ledger)
- [Conditional and Cached Rules](#conditional-and-cached-rules)
  - [Run a rule only when a condition holds](#run-a-rule-only-when-a-condition-holds)
  - [Cache expensive rules](#cache-expensive-rules)
  - [Mixing sync and async rules](#mixing-sync-and-async-rules)
- [Feature Flags and Shadow Rollout](#feature-flags-and-shadow-rollout)
  - [Gating a rule](#gating-a-rule)
  - [Seeing what shadow rules would have blocked](#seeing-what-shadow-rules-would-have-blocked)
  - [Using Microsoft.FeatureManagement](#using-microsoftfeaturemanagement)
- [Per-Tenant Rules and Parameters](#per-tenant-rules-and-parameters)
  - [Rule scopes](#rule-scopes)
  - [Externalized parameters](#externalized-parameters)
  - [Rule registries](#rule-registries)
- [Specifications and Database Queries](#specifications-and-database-queries)
- [Rule Expressions](#rule-expressions)
  - [Expression syntax](#expression-syntax)
  - [How values are compared](#how-values-are-compared)
  - [Safety limits](#safety-limits)
- [Logging, Tracing and Metrics](#logging-tracing-and-metrics)
  - [Observers](#observers)
  - [Structured logging](#structured-logging)
  - [OpenTelemetry tracing and metrics](#opentelemetry-tracing-and-metrics)
- [Dependency Injection](#dependency-injection)
- [Testing Your Rules](#testing-your-rules)
  - [Built-in test helpers](#built-in-test-helpers)
  - [Rule coverage](#rule-coverage)
  - [Finding a rule's boundary](#finding-a-rules-boundary)
  - [Property-based checks](#property-based-checks)
  - [Smoke-test mutants](#smoke-test-mutants)
- [Real-World Examples](#real-world-examples)
  - [E-commerce checkout](#e-commerce-checkout)
  - [User registration](#user-registration)
  - [API controller validation](#api-controller-validation)
  - [Multi-tenant order approval with an audit trail](#multi-tenant-order-approval-with-an-audit-trail)
- [Upgrading from 2.x](#upgrading-from-2x)
- [API Reference](#api-reference)
  - [Masterly.BusinessRules](#masterlybusinessrules-1)
  - [Masterly.BusinessRules.Expressions](#masterlybusinessrulesexpressions)
  - [Masterly.BusinessRules.Diagnostics](#masterlybusinessrulesdiagnostics)
  - [Masterly.BusinessRules.DependencyInjection](#masterlybusinessrulesdependencyinjection)
  - [Masterly.BusinessRules.FeatureManagement](#masterlybusinessrulesfeaturemanagement)
  - [Masterly.BusinessRules.Testing](#masterlybusinessrulestesting)
- [License](#license)

---

## Overview

Masterly.BusinessRules is a business rule engine for .NET. You write each rule, such as "an order must contain at least one item", as a small named unit with a code and a message. You then check one rule or many, and you get back a clear list of every rule that failed.

Every feature works with both synchronous and asynchronous rules. The one exception is specifications: an expression tree has no async form, so you use `.ToAsync()` to run a specification in async code (see [Specifications and database queries](#specifications-and-database-queries)). The core package has no dependencies. Five optional packages add text expressions, logging and tracing, dependency injection, feature management and test tools.

**What you can do**

- Write a rule as a small class, or build one inline from a lambda. See [Writing rules](#writing-rules) and [Building rules without classes](#building-rules-without-classes).
- Write one stateless rule that validates whichever order you pass to it. See [Subject-typed rules](#subject-typed-rules).
- Combine rules with And, Or and Not, or with groups such as "all", "any" and "at least 2 of". See [Combining rules](#combining-rules).
- Check many rules in one call, and either collect every failure or stop at the first one. See [Running many rules](#running-many-rules).
- See exactly which part of a combined rule failed, as a printable tree. See [Understanding failures](#understanding-failures).
- Run a rule only when a condition holds, and cache the verdict of a slow rule. See [Conditional and cached rules](#conditional-and-cached-rules).
- Ship a new rule in shadow mode behind a feature flag, then enforce it for a percentage of callers. See [Feature flags and shadow rollout](#feature-flags-and-shadow-rollout).
- Give each tenant its own limits, such as a maximum order amount, without changing rule code. See [Per-tenant rules and parameters](#per-tenant-rules-and-parameters).
- Use the same rule to check one object in memory and to query the database for every violating row. See [Specifications and database queries](#specifications-and-database-queries).
- Write a condition as text, such as `amount > @maxOrderAmount`, and see the values it was evaluated with. See [Rule expressions](#rule-expressions).
- Log, trace and measure every rule evaluation with `ILogger`, OpenTelemetry and metrics. See [Logging, tracing and metrics](#logging-tracing-and-metrics).
- Register your rules in a DI container and inject a runner, and test your rules with coverage, boundary and property checks. See [Dependency injection](#dependency-injection) and [Testing your rules](#testing-your-rules).

---

## Installation

Install the core package first. Then add only the companion packages you need. All six packages ship together with the same version number.

| Package | What it is for | Target framework | Extra dependencies |
|---------|----------------|------------------|--------------------|
| `Masterly.BusinessRules` | The core: rules, builders, composition, batch checking, context, caching, feature gates, parameters and specifications | `netstandard2.1` | None |
| `Masterly.BusinessRules.Expressions` | Rule conditions written as text, with explanations that show the bound values | `netstandard2.1` | None |
| `Masterly.BusinessRules.Diagnostics` | Structured `ILogger` logging, OpenTelemetry spans and metrics for rule evaluation | `netstandard2.1` | `Microsoft.Extensions.Logging.Abstractions` 10.0.14, `System.Diagnostics.DiagnosticSource` 10.0.14 |
| `Masterly.BusinessRules.DependencyInjection` | Assembly scanning, injectable rule runners and per-tenant rule sets | `netstandard2.1` | `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.14 |
| `Masterly.BusinessRules.FeatureManagement` | Drives feature-gated rules from `Microsoft.FeatureManagement` flags and variants | `netstandard2.1` | `Microsoft.FeatureManagement` 4.8.0 |
| `Masterly.BusinessRules.Testing` | Outcome coverage, boundary finder, property checks and mutants for your test projects | `net8.0`, `net10.0` | `CsCheck` 4.9.1 on .NET 8 and 9, 5.0.0 on .NET 10 |

Every companion package also depends on the core package. The versions shown are the minimum versions each package references.

```bash
dotnet add package Masterly.BusinessRules
dotnet add package Masterly.BusinessRules.Expressions
dotnet add package Masterly.BusinessRules.Diagnostics
dotnet add package Masterly.BusinessRules.DependencyInjection
dotnet add package Masterly.BusinessRules.FeatureManagement
dotnet add package Masterly.BusinessRules.Testing
```

Or, in the Visual Studio Package Manager Console, run `Install-Package Masterly.BusinessRules`, and the same for each companion package you need.

> **Note:** `netstandard2.1` runs on .NET Core 3.0 or later and on .NET 5 or later. It does not run on .NET Framework. The Testing package needs a test project that targets .NET 8 or later.

---

## Quick Start

This example defines two rules for an order, checks one of them, and then checks both together.

**1. Define a rule.** Derive from `BaseBusinessRule` and supply a code, a message and the condition. A rule is *broken* when `IsBroken()` returns `true`. Write the condition that describes the violation, not the valid state.

```csharp
using Masterly.BusinessRules;

// Order and Customer are your own domain classes.
// Here Order.Items is a List<OrderLine> and Customer.IsActive is a bool.
public sealed class OrderMustHaveItemsRule : BaseBusinessRule
{
    private readonly Order _order;

    public OrderMustHaveItemsRule(Order order)
    {
        _order = order;
    }

    public override string Code => "ORDER.NO_ITEMS";

    public override string Message => "An order must contain at least one item.";

    public override bool IsBroken() => _order.Items.Count == 0;
}

public sealed class CustomerMustBeActiveRule : BaseBusinessRule
{
    private readonly Customer _customer;

    public CustomerMustBeActiveRule(Customer customer)
    {
        _customer = customer;
    }

    public override string Code => "CUSTOMER.INACTIVE";

    public override string Message => "The customer account must be active.";

    public override bool IsBroken() => !_customer.IsActive;
}
```

**2. Check one rule.** `IsBroken()` answers the question. `Check()` throws a `BusinessRuleValidationException` when the rule is broken and does nothing when it passes.

```csharp
// order comes from your application
OrderMustHaveItemsRule rule = new OrderMustHaveItemsRule(order);

bool isBroken = rule.IsBroken();

rule.Check();
```

**3. Check several rules.** `BusinessRuleChecker.CheckAll` evaluates every rule you pass. If any are broken, it throws one exception that lists all of them.

```csharp
// order and customer come from your application
try
{
    BusinessRuleChecker.CheckAll(
        new OrderMustHaveItemsRule(order),
        new CustomerMustBeActiveRule(customer));
}
catch (BusinessRuleValidationException exception)
{
    foreach (BusinessRuleResult broken in exception.BrokenRules)
    {
        Console.WriteLine($"{broken.Code} ({broken.Severity}): {broken.Message}");
    }
}
```

With an empty order and an inactive customer, this prints:

```text
ORDER.NO_ITEMS (Error): An order must contain at least one item.
CUSTOMER.INACTIVE (Error): The customer account must be active.
```

**Next steps**

- To get the results without an exception, call `BusinessRuleChecker.EvaluateAll` instead. See [Check all or evaluate all](#check-all-or-evaluate-all).
- For rules that call a database or a web service, see [Async rules](#async-rules).
- To combine rules, see [Combining Rules](#combining-rules).

---

## Writing Rules

A rule is a small class that answers one question: is this business rule broken? You give it a code, a message and a condition. The rule is broken when the condition returns `true`.

Pick a base class based on what the condition needs:

| Base class | Use it when | You override |
|------------|-------------|--------------|
| `BaseBusinessRule` | The rule can decide from data already in memory. | `Code`, `Message`, `IsBroken()` |
| `BaseAsyncBusinessRule` | The rule needs I/O, such as a database query or an HTTP call. | `Code`, `Message`, `IsBrokenAsync(context, cancellationToken)` |

All core types live in the `Masterly.BusinessRules` namespace. To build a rule from a lambda instead of a class, see [Building rules without classes](#building-rules-without-classes).

Later examples use rule classes such as `ShippingAddressRequiredRule` or `CustomerMustBeAdultRule` without showing them. They stand for your own rules, written as shown in this section.

### Sync rules

Derive from `BaseBusinessRule` when the rule can decide without waiting for I/O. Pass the data it checks through the constructor.

```csharp
using Masterly.BusinessRules;

public sealed class OrderMinimumTotalRule : BaseBusinessRule
{
    private readonly Order _order;

    public OrderMinimumTotalRule(Order order)
    {
        _order = order;
    }

    public override string Code => "ORDER.BELOW_MINIMUM";

    public override string Message => "Order total must be at least 10.00.";

    public override bool IsBroken() => _order.Total < 10.00m;
}

// order comes from your application
OrderMinimumTotalRule rule = new OrderMinimumTotalRule(order);

rule.Check(); // throws BusinessRuleValidationException when the total is below 10.00
```

- `IsBroken()` returns `true` when the rule is violated. It is the only method you must implement.
- Sync rules also have `IsBroken(BusinessRuleContext context)`. By default it ignores the context and calls `IsBroken()`. Override it when the rule reads its data from a context. See [Passing data with a context](#passing-data-with-a-context).
- C# 12 primary constructors work too: `public sealed class OrderMinimumTotalRule(Order order) : BaseBusinessRule`.

### Async rules

Derive from `BaseAsyncBusinessRule` when the rule has to wait for I/O. You implement `IsBrokenAsync` and pass the cancellation token on to your own calls.

```csharp
using System.Threading;
using System.Threading.Tasks;
using Masterly.BusinessRules;

public sealed class EmailMustBeUniqueRule : BaseAsyncBusinessRule
{
    private readonly IUserRepository _users;
    private readonly string _email;

    public EmailMustBeUniqueRule(IUserRepository users, string email)
    {
        _users = users;
        _email = email;
    }

    public override string Code => "USER.EMAIL_TAKEN";

    public override string Message => "This email address is already registered.";

    public override async Task<bool> IsBrokenAsync(
        BusinessRuleContext context,
        CancellationToken cancellationToken = default)
    {
        return await _users.EmailExistsAsync(_email, cancellationToken);
    }
}

// users (your IUserRepository), email and cancellationToken come from your application
EmailMustBeUniqueRule rule = new EmailMustBeUniqueRule(users, email);

await rule.CheckAsync(new BusinessRuleContext(), cancellationToken);
```

- Async rules always take a context. There is no overload without one. Pass `new BusinessRuleContext()` when you have no data to share.
- The async batch checker creates an empty context for you when you call it without one.
- Sync and async rules are separate type hierarchies. A sync rule is not an `IAsyncBusinessRule`. To run a sync rule next to async ones, convert it with `rule.ToAsync()` (see [Mixing sync and async rules](#mixing-sync-and-async-rules)).

### Three ways to run a rule

Every rule can be run in three ways. Choose based on what you want back: a yes/no answer, an exception, or a result object.

```csharp
// customer comes from your application; CustomerMustBeActiveRule is the rule from "Quick Start"
CustomerMustBeActiveRule rule = new CustomerMustBeActiveRule(customer);

bool isBroken = rule.IsBroken();                                  // Is broken: a yes/no answer

BusinessRuleResult? failure = isBroken ? rule.Evaluate() : null;  // Evaluate: the failure as data

rule.Check();                                                     // Check: throws when the rule is broken
```

| Style | Sync | Async | Returns | Use it when |
|-------|------|-------|---------|-------------|
| Is broken | `IsBroken()`, `IsBroken(context)` | `IsBrokenAsync(context, ct)` | `bool` (`true` means broken) | You only need a yes/no answer, for example to branch. |
| Check | `Check()`, `Check(context)` | `CheckAsync(context, ct)` | Nothing. Throws `BusinessRuleValidationException` when broken. | A broken rule must stop the operation. The exception's `BrokenRules` holds the result. |
| Evaluate | `Evaluate()`, `Evaluate(context)` | `EvaluateAsync(context, ct)` | Sync: `BusinessRuleResult`. Async: `BusinessRuleResult?`, `null` when the rule passes. | You want the failure as data (code, message, severity) instead of an exception. |

> **Note:** Sync `Evaluate()` does not run the rule. It only builds the "broken" result from the rule's metadata, so it never returns `null`, even for a rule that passes. Call it after `IsBroken()` returned `true`: `BusinessRuleResult? result = rule.IsBroken() ? rule.Evaluate() : null;`. Async `EvaluateAsync` is different: it runs the rule and returns `null` when the rule passes.

- `Check()` throws for a broken rule of any severity, including `Info`.
- To throw only at or above a severity, pass a threshold: `rule.Check(RuleSeverity.Error)`, `rule.Check(context, RuleSeverity.Error)` or `rule.CheckAsync(context, RuleSeverity.Error, ct)`. A broken rule below the threshold returns without throwing.
- The overloads without a context call `IsBroken()`. The overloads with a context call `IsBroken(context)`.
- To run many rules at once and collect every failure, see [Running many rules](#running-many-rules).

### Rule metadata

Every rule carries metadata that identifies and classifies it. Only `Code` and `Message` are required. Override the other properties when you want to filter rules, group them, or show more detail in a UI or a log.

```csharp
using System.Collections.Generic;
using Masterly.BusinessRules;

public sealed class PaymentAmountMustBePositiveRule : BaseBusinessRule
{
    private readonly Payment _payment;

    public PaymentAmountMustBePositiveRule(Payment payment)
    {
        _payment = payment;
    }

    public override string Code => "PAYMENT.AMOUNT_NOT_POSITIVE";
    public override string Message => "Payment amount must be greater than zero.";
    public override RuleSeverity Severity => RuleSeverity.Error;
    public override string Name => "Payment amount is positive";
    public override string Description => "Rejects zero or negative payments before they reach the gateway.";
    public override string Category => "Payment";
    public override IReadOnlyList<string> Tags => new[] { "payment", "checkout" };

    public override bool IsBroken() => _payment.Amount <= 0m;
}

// rules is an IEnumerable<IBusinessRule> from your application
BusinessRuleChecker.CheckBySeverity(rules, RuleSeverity.Error);
BusinessRuleChecker.CheckByCategory(rules, "Payment");
BusinessRuleChecker.CheckByTags(rules, "checkout");
```

| Property | Required | Default | Purpose |
|----------|----------|---------|---------|
| `Code` | Yes | None | Stable identifier, such as `"ORDER.NO_ITEMS"`. Use it in API error responses and logs. |
| `Message` | Yes | None | Text that explains why the rule is broken. |
| `Severity` | No | `RuleSeverity.Error` | How serious a violation is. See the levels below. |
| `Name` | No | The class name | Display name. |
| `Description` | No | Empty string | What the rule checks. It stays on the rule and is not copied to the result. |
| `Category` | No | Empty string | One group the rule belongs to, such as `"Payment"`. |
| `Tags` | No | Empty list | Free labels for filtering. |

Severity levels, from lowest to highest:

| Level | Meaning |
|-------|---------|
| `Info` | For information only. |
| `Warning` | Should be looked at, but need not block the operation. |
| `Error` | Must be fixed. Blocks the operation. |

Using the metadata:

- `CheckBySeverity`, `CheckByCategory` and `CheckByTags` check only the rules that match. The sync versions run the rules without a context. [Filtering by severity, category or tags](#filtering-by-severity-category-or-tags) explains how each one matches.
- A broken rule's result copies its `Code`, `Message`, `Severity`, `Name`, `Category` and `Tags`.
- To give an existing rule a different severity without changing its class, wrap it: `rule.WithSeverity(RuleSeverity.Warning)`.

> **Note:** Severity is a label. It does not change what `Check()` does: a plain `Check()` throws for every broken rule. Pass a severity threshold (see [Three ways to run a rule](#three-ways-to-run-a-rule)) to let `Info` and `Warning` violations through.

### Passing data with a context

A `BusinessRuleContext` passes data to rules when you run them, instead of through their constructors. Use it when several rules need the same data, or when a value is known only at run time, such as the current user or a configured limit.

```csharp
using System;
using Masterly.BusinessRules;

public sealed class OrderWithinCreditLimitRule : BaseBusinessRule
{
    public override string Code => "ORDER.CREDIT_LIMIT_EXCEEDED";

    public override string Message => "Order total exceeds the customer's credit limit.";

    // This rule reads everything from the context, so running it without one is a programming error.
    public override bool IsBroken() =>
        throw new InvalidOperationException("OrderWithinCreditLimitRule needs a context with 'order' and 'creditLimit'.");

    public override bool IsBroken(BusinessRuleContext context)
    {
        Order order = context.Get<Order>("order");
        decimal creditLimit = context.Get<decimal>("creditLimit");
        return order.Total > creditLimit;
    }
}

// order and customer come from your application
BusinessRuleContext context = new BusinessRuleContext();
context.Set("order", order);
context.Set("creditLimit", customer.CreditLimit);

BusinessRuleChecker.CheckAll(context, new OrderWithinCreditLimitRule(), new CustomerMustBeActiveRule(customer));
```

The context is a key-value store with string keys:

| Member | What it does |
|--------|--------------|
| `Set(key, value)` | Stores a value. Replaces any value already stored under the key. |
| `Get<T>(key)` | Returns the value. Throws `ContextKeyNotFoundException` when the key is missing and `ContextValueTypeMismatchException` when the value is not a `T`. |
| `TryGet<T>(key, out value)` | Returns `false` instead of throwing when the key is missing or the value is not a `T`. |
| `ContainsKey(key)` | Returns `true` when the key exists. |
| `Remove(key)` | Removes the key. Returns `true` when it was there. |
| `Clear()` | Removes every key. |
| `Count`, `Keys` | The number of keys, and a read-only live view of their names. |

- Keys are case-sensitive.
- A `null` key throws `ArgumentNullException`.
- `Get<T>` returns a stored `null` when `T` allows null. Asking for a stored `null` as a non-nullable value type, such as `Get<int>`, throws `ContextValueTypeMismatchException`.
- `TryGet` returns `false` for a stored `null`.
- The context is not thread-safe. Fill it before you run the rules, and do not write to it while rules run in parallel.

> **Note:** When you run a sync rule without a context (`rule.Check()`, or a checker call without one), the library calls the parameterless `IsBroken()`. That is why a context-only rule should throw a clear exception there, as in the example, instead of returning a guess.

**One typed object instead of many keys.** `BusinessRuleContext<T>` wraps one data object and exposes it as `Data`. A rule reads it by checking the context's type:

```csharp
public sealed record CheckoutData(Order Order, Customer Customer);

// Inside a rule class:
public override bool IsBroken(BusinessRuleContext context)
{
    if (context is BusinessRuleContext<CheckoutData> checkout)
    {
        return checkout.Data.Order.Total > checkout.Data.Customer.CreditLimit;
    }

    throw new InvalidOperationException("This rule needs a BusinessRuleContext<CheckoutData>.");
}

// Where you run the rules (order and customer come from your application):
BusinessRuleContext<CheckoutData> checkoutContext =
    new BusinessRuleContext<CheckoutData>(new CheckoutData(order, customer));
```

- `T` must be a reference type, such as a class or a record.
- `Data` is set once, in the constructor.
- A typed context is still a normal context. `Set`, `Get` and the other members keep working for extra values.
- The typed context also stores `Data` under the reserved key `"__typedData"`. You will see it in `Keys` and `Count`. Do not use that key for your own values.

**When a read fails.** Every failed read, by string key or typed key, throws one of these exceptions:

| Exception | Thrown when | Useful properties |
|-----------|-------------|-------------------|
| `ContextKeyNotFoundException` | `Get` asks for a key that does not exist. | `Key`, `AvailableKeys` (sorted) |
| `ContextValueTypeMismatchException` | `Get` asks for the wrong type. | `Key`, `ActualType` (`null` when the stored value is `null`), `RequestedType` |
| `BusinessRuleContextException` | A rule run by `BusinessRuleChecker` or `AsyncBusinessRuleChecker` fails to read the context. | `RuleCode`, `Key`, `InnerException` (the original exception) |

- `ContextKeyNotFoundException` derives from `KeyNotFoundException` and `ContextValueTypeMismatchException` from `InvalidCastException`, so existing `catch` blocks still work.
- The message names the key and lists the available keys, for example `Context has no key 'ordr'. Available keys: creditLimit, order`.
- Only the batch checkers wrap a failed read in `BusinessRuleContextException`. When you call `rule.Check(context)` or `rule.IsBroken(context)` yourself, you get the original exception.
- The checkers wrap only context read failures. Any other exception from a rule propagates unchanged.
- When the checker's fault policy is `FaultPolicy.Record` or `FaultPolicy.TreatAsBroken`, the checker does not rethrow it. The wrapped exception is recorded in the evaluation ledger and passed to observers instead. See [When a rule throws](#when-a-rule-throws).

### Typed context keys

String keys are easy to mistype, and the compiler cannot check the type you ask for. A `ContextKey<T>` joins a key name and its value type into one symbol. Declare each key once, then use it both where you fill the context and in the rules that read it.

```csharp
using System;
using Masterly.BusinessRules;

public static class CheckoutKeys
{
    public static readonly ContextKey<Order> Order = new ContextKey<Order>("order");
    public static readonly ContextKey<decimal> CreditLimit = new ContextKey<decimal>("creditLimit");
}

// The rule from "Passing data with a context", rewritten with typed keys
public sealed class OrderWithinCreditLimitRule : BaseBusinessRule
{
    public override string Code => "ORDER.CREDIT_LIMIT_EXCEEDED";

    public override string Message => "Order total exceeds the customer's credit limit.";

    public override bool IsBroken() =>
        throw new InvalidOperationException("OrderWithinCreditLimitRule needs a context.");

    public override bool IsBroken(BusinessRuleContext context)
    {
        Order order = context.Get(CheckoutKeys.Order); // T is inferred from the key
        decimal creditLimit = context.Get(CheckoutKeys.CreditLimit);
        return order.Total > creditLimit;
    }
}

// order and customer come from your application
BusinessRuleContext context = new BusinessRuleContext();
context.Set(CheckoutKeys.Order, order);
context.Set(CheckoutKeys.CreditLimit, customer.CreditLimit);

BusinessRuleChecker.CheckAll(context, new OrderWithinCreditLimitRule());
```

- `Get`, `TryGet`, `Set`, `ContainsKey` and `Remove` all accept a `ContextKey<T>`.
- A typed key stores its value under its `Name`. Typed and string access to the same key are interchangeable: `context.Get<Order>("order")` reads the same value.
- `ContainsKey(key)` checks the name only, not the type of the stored value.
- The constructor rejects a `null` name (`ArgumentNullException`) and an empty or white-space name (`ArgumentException`).
- Two keys are equal when their names are equal (case-sensitive).
- In tests, `RuleTestHelper.CreateContext((CheckoutKeys.Order, order), (CheckoutKeys.CreditLimit, 1000m))` builds a context from up to four typed pairs. Call `Set` for more.

> **Note:** Always create a key with its constructor. `default(ContextKey<T>)` has no name, and using it throws `ArgumentNullException`.

---

## Building Rules Without Classes

Builders let you create a rule inline, without writing a class. Use them for small one-off rules, rules assembled from configuration at run time, or quick prototypes.

There are two builders. `BusinessRuleBuilder` creates synchronous rules and `AsyncBusinessRuleBuilder` creates asynchronous ones. Both share the same metadata methods.

### Sync builder

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
| `When((ctx, parameters) => ...)` | The rule also reads scoped parameters (see [Externalized parameters](#externalized-parameters)). |

Rules for the builder:

- **One condition per rule.** Each `When` call replaces the previous one. `When((ctx, p) => true).When(ctx => false)` builds a rule that never breaks.
- **A condition is required.** `Build()` throws `InvalidOperationException` if you never called `When`.
- **Details are optional.** `WithDetail`, `WithActual`, `WithExpected` and `WithRemediation` add details to the result. They run only when the rule is broken. If one of them throws, the result has no details, but the rule still reports as broken. See [Rich results](#rich-results).

> **Note:** A rule built with `When(ctx => ...)` still works when you call `IsBroken()` or `Check()` without a context. It then receives an empty context, so `ctx.Get<T>(...)` throws `ContextKeyNotFoundException`. Pass a context, or use `ctx.TryGet` in the condition.

### Async builder

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

> **Advanced:** Package authors can plug in a text condition with `When(compiler, text)` / `WhenAsync(compiler, text)`, or choose the rule type that `Build()` returns with `WithRuleFactory(...)`. The Expressions package uses these for `WhenExpression` (see [Rule Expressions](#rule-expressions)).

---

## Subject-Typed Rules

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

Both base classes default `Severity` to `Error`, `Name` to the class name, and `Description`, `Category` and `Tags` to empty. Override `Describe(subject, context)` to attach details to a broken result (see [Rich results](#rich-results)).

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

The DependencyInjection package can find and register these rules for you (see [Dependency Injection](#dependency-injection)).

**Binding a subject with `.For(subject)`.** A typed rule is not an `IBusinessRule`. Call `.For(subject)` to get an ordinary `IBusinessRule` (or `IAsyncBusinessRule`) bound to that subject. The bound rule keeps the rule's code, message and other metadata. It works with every wrapper, operator, checker and `Explain`.

```csharp
// order and context come from your application
IBusinessRule bound = new OrderItemsRequiredRule().For(order);

bound.Check(context);
IBusinessRule cached = bound.Cached(TimeSpan.FromMinutes(1));
RuleExplanation explanation = bound.Explain(context);
```

> **Note:** `.For(order)` returns a new rule each time. A cache added with `.Cached(...)` belongs to that bound instance, so it caches the verdict for that one subject.

**Checking a subject directly.** You do not have to bind rules yourself. The checkers have overloads that take the subject and a list of typed rules, and bind them for you. Options, fault policy, severity threshold and observers work exactly as in the untyped overloads.

```csharp
// order and context come from your application; OrderTotalMustBePositiveRule is another BaseBusinessRule<Order>
IBusinessRule<Order>[] rules = { new OrderItemsRequiredRule(), new OrderTotalMustBePositiveRule() };

BusinessRuleChecker.CheckAll(order, rules, context); // throws when any rule is broken
RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(order, rules);

// Rules of different classes can be passed directly
BusinessRuleChecker.CheckAll(order, new OrderItemsRequiredRule(), new OrderTotalMustBePositiveRule());
```

| You want to | Sync | Async |
|-------------|------|-------|
| Check and throw | `CheckAll(order, rules, context)` | `CheckAllAsync(context, order, rules, cancellationToken)` |
| Get the result | `EvaluateAll(order, rules, context)` | `EvaluateAllAsync(context, order, rules, cancellationToken)` |
| Check many subjects | `EvaluateEach(orders, rules, context)` | `EvaluateEachAsync(context, orders, rules, cancellationToken)` |

The context and the options are optional in every call. `CheckBySeverity`, `CheckByCategory` and `CheckByTags` also have subject overloads.

- The sync overloads take the context after the rules. The async overloads take it first.
- `CheckByTags` runs the rules that have at least one of the tags. Tag matching ignores case. `CheckByCategory` matches the category exactly.
- A `null` entry in the rule list throws `ArgumentException`.

```csharp
// customer, customerRepository, context and cancellationToken come from your application
IAsyncBusinessRule<Customer>[] customerRules = { new CustomerEmailMustBeUniqueRule(customerRepository) };

await AsyncBusinessRuleChecker.CheckAllAsync(context, customer, customerRules, cancellationToken);
```

> **Note:** If you pass a bare `null` as the context to an untyped checker, for example `EvaluateAll(rules, null)`, C# picks the typed overload and treats your rules as the subject. The call then throws `ArgumentNullException`. Leave the context out, or write `(BusinessRuleContext?)null`.

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

> **Note:** Unlike the untyped builder, `BusinessRuleBuilder<T>` keeps a subject-only condition and a context condition at the same time:
> - `IsBroken(order)` uses `When(o => ...)`.
> - `IsBroken(order, context)` uses `When((o, ctx) => ...)`.
> - If you set only one of them, both calls use it.
> - A parameters condition `When((o, ctx, p) => ...)` replaces both.

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

`And` and `Or` stop early: they skip the second operand when the first one already decides the result. They follow the same truth table as the untyped operators in [And, Or and Not](#and-or-and-not).

> **Note:** A typed combination is a single rule. In an `Explain` tree it shows as one node, and its result does not list which operand failed. If you need that detail, bind first and combine the bound rules: `highValueRule.For(order).And(firstOrderRule.For(order))`.

---

## Combining Rules

Real validation is rarely a single check. You can build one rule out of several in three ways: the `And`, `Or` and `Not` operators, rule groups, and composite rules. A combined rule is itself a rule, so you can check it, nest it or pass it to a checker like any other rule.

### And, Or and Not

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

> **Note:** `And` is broken **only when both** operands are broken. `a.And(b)` does **not** mean "a and b must both pass". For "every rule must pass", use `Or`, or better, `RuleGroup.All` (see [Rule groups](#rule-groups)), which says what it means.

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

### Rule groups

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

> **Note:** A group cannot be both parallel and short-circuiting. Calling `WithShortCircuit()` on a parallel group, or `WithParallelEvaluation()` on a short-circuiting one, throws `InvalidOperationException`.

> **Note:** The members of a parallel group share one `BusinessRuleContext`, and the context is not thread-safe. They may read from it but should not write to it.

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

> **Note:** The sync `Evaluate()` on a group, or on an `And`, `Or` or `Not`, does not run the members. It only builds the group's own result, with empty `Details`. To get the member failures, use `Check`, the checkers (`CheckAll`, `EvaluateAll`) or, on async rules, `EvaluateAsync`.

> **Advanced:** Every combined rule exposes its structure for your own tooling. `And`, `Or` and `Not` nodes, groups and composites implement `IComposedBusinessRule` (`Operator`, `Operands`). The cached, conditional, severity-override and feature-gated wrappers implement `IDecoratedBusinessRule` (`InnerRule`). Async rules have `IComposedAsyncBusinessRule` and `IDecoratedAsyncBusinessRule`.

### Composite rules

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

> **Note:** The async composite reports failures differently from the sync one. `CheckAsync` throws with a single result, code `CompositeAsyncRule`, whose `Details` holds the broken rules. Call `exception.Flatten()` to get each rule.

> **Note:** `CompositeAsyncBusinessRule` also has a constructor that builds a single rule from a delegate: `new CompositeAsyncBusinessRule(isBrokenAsync, message, code)`. The message comes **before** the code in that constructor.

---

## Understanding Failures

When a rule fails, you usually want two answers. Which part of a combined rule failed? And what exactly was wrong? Explain trees answer the first question. Rich results answer the second.

### Explain trees

Call `Explain` to see why a combined rule passed or failed. It runs the rule once and returns a tree with one node for each rule it visited. Use it in logs, support tools and tests.

```csharp
// context and the three KYC rules come from your application
GroupBusinessRule kyc = RuleGroup.All(
        "KYC.INCOMPLETE",
        "All KYC checks must pass",
        identityVerified,
        addressVerified,
        sanctionsCleared)
    .WithShortCircuit();

RuleExplanation explanation = kyc.Explain(context);
Console.WriteLine(explanation.ToText());
```

The output looks like this (durations vary):

```text
[BROKEN] ALL  KYC.INCOMPLETE  "All KYC checks must pass"  (0.9 ms)
 ├─ [PASSED]  KYC.ID  (0.1 ms)
 ├─ [BROKEN]  KYC.ADDRESS  "Address must be verified"  (0.2 ms)
 └─ [NOT EVALUATED]  KYC.SANCTIONS  reason: short-circuit
```

Each line reads `[OUTCOME] OPERATOR  CODE  "message"  (duration)  reason: ...`. The message appears only on broken nodes. The duration appears only on nodes that actually ran.

Both sync and async rules can be explained:

| Call | Notes |
|------|-------|
| `rule.Explain(context, captureExceptions)` | Both parameters are optional. The context defaults to `null`. |
| `await asyncRule.ExplainAsync(context, cancellationToken)` | The context is required. A `null` context throws `ArgumentNullException`. |
| `await asyncRule.ExplainAsync(context, captureExceptions, cancellationToken)` | Same, with exception capture. |

Each node has an `Outcome`:

| Outcome | Meaning | Counts as broken |
|---------|---------|------------------|
| `Passed` | The rule ran and passed. | No |
| `Broken` | The rule ran and is broken. | Yes |
| `Skipped` | A wrapper did not run the rule: a `.When(...)` precondition was false, or a feature gate was closed. | No |
| `CacheHit` | A cached rule returned its stored verdict without running. | The cached verdict |
| `NotEvaluated` | The rule was skipped by a short-circuit, or a custom wrapper never ran it. | No |
| `Faulted` | The rule threw, and you passed `captureExceptions: true`. | Yes |

A node also gives you:

- `Code`, `Message` and `Severity` of the rule.
- `IsBroken`: whether the node counted as broken for its parent.
- `Operator`: the label of a combined or wrapped node, such as `AND`, `OR`, `NOT`, `ALL`, `ANY`, `AT LEAST 2`, `EXACTLY 1`, `CACHED`, `WHEN`, `SEVERITY`, `GATED`, `SHADOW` or `ADAPTER`. It is empty for a plain rule.
- `Reason`: why the outcome happened, such as `short-circuit`, `precondition not met` or `cache hit`.
- `Duration`, `Fault` (the captured exception) and `Children`.
- `BrokenLeaves()`: the deepest broken nodes, which are usually the rules you need to report.

```csharp
// explanation is from the example above
foreach (RuleExplanation failure in explanation.BrokenLeaves())
{
    Console.WriteLine($"{failure.Code}: {failure.Message}"); // KYC.ADDRESS: Address must be verified
}
```

Your own rule classes, and any rule that is not a combination or a library wrapper, appear as a single node.

> **Note:** `Explain` is a real evaluation, not a dry run. Each rule runs exactly once, and wrappers behave as they do in `Check`. For example, a cached rule can serve or store its verdict.

**Capturing exceptions.** By default, an exception thrown by a rule propagates out of `Explain`. Pass `captureExceptions: true` to record it in the tree instead:

```csharp
// kyc and context are from the first example in this section
RuleExplanation explanation = kyc.Explain(context, captureExceptions: true);
```

- The throwing rule becomes a `Faulted` node. Its `Fault` holds the exception, and its `Reason` reads `ExceptionType: message`.
- A faulted node counts as broken for its parent, and the rest of the tree is still evaluated.
- Cancellation (`OperationCanceledException`) is never captured. It always propagates.
- A cached rule never stores a verdict that rests on a captured exception.

> **Advanced:** A wrapper you write yourself shows up as a single node. To show it as its own node with the inner rule's subtree below it, implement `IExplainableDecoratedBusinessRule` (async: `IExplainableDecoratedAsyncBusinessRule`). Give it an `ExplanationOperator` label and implement `EvaluateDecorated(evaluation)`, calling `evaluation.EvaluateInner()` to run the inner rule.

### Rich results

A message such as "Transaction limit exceeded" says that something failed, not what. A rich result also says which value broke the rule, what was expected and how to fix it.

```csharp
public sealed class TransactionLimitRule : BaseBusinessRule
{
    private readonly decimal _amount;
    private readonly decimal _limit;

    public TransactionLimitRule(decimal amount, decimal limit)
    {
        _amount = amount;
        _limit = limit;
    }

    public override string Code => "LIMIT.EXCEEDED";

    public override string Message => "Transaction limit exceeded.";

    public override string Category => "Payments";

    public override IReadOnlyList<string> Tags => new[] { "customer-facing" };

    public override bool IsBroken() => _amount > _limit;

    // Runs only when the rule is broken
    protected override RuleResultDetails Describe(BusinessRuleContext? context) => RuleResultDetails
        .Detail($"Amount {_amount:N2} exceeds the limit of {_limit:N2}.")
        .Actual(_amount)
        .Expected($"<= {_limit}")
        .Remediation("Reduce the amount or request a limit increase.")
        .With("currency", "USD");
}
```

Every `BusinessRuleResult` carries these members:

| Member | Content |
|--------|---------|
| `Code`, `Message`, `Severity` | The rule's metadata. |
| `Name`, `Category`, `Tags` | The rule's metadata, so you can filter results without looking the rule up again. |
| `Detail` | A formatted sentence about this violation, or `null`. |
| `Actual`, `Expected` | The value that broke the rule and the expected value or range, or `null`. |
| `Remediation` | A hint on how to fix the violation, or `null`. |
| `Data` | Extra key/value pairs. Empty by default. |
| `Details` | The broken members of a combined rule. Empty for a plain rule. |

**Describing a violation.** Override `Describe(context)` on `BaseBusinessRule` or `BaseAsyncBusinessRule` to fill in the details. Subject-typed rules override `Describe(subject, context)`.

- Start with `RuleResultDetails.Detail($"...")` or `RuleResultDetails.Empty`. Then chain `.Actual(...)`, `.Expected(...)`, `.Remediation(...)` and `.With(key, value)`. Each call returns a new object.
- The library calls `Describe` only when the rule is broken. It passes the same context the rule was evaluated with.
- If `Describe` throws, the result is still reported, just without details. A bug in `Describe` never hides the violation. Cancellation still propagates.
- `Detail` is an interpolated string. It is formatted with `context.FormatProvider` when you set one, and with the invariant culture otherwise. `Message` is a plain string and is never formatted.
- `Actual` and `Expected` are stored as you pass them (`object?`).
- Wrappers keep the details. `.Cached(...)` and `.When(...)` return the inner rule's result, and `.WithSeverity(...)` changes only the severity.

> **Note:** On a sync rule, `Evaluate()` and `Evaluate(context)` build the result, including `Describe`, without checking whether the rule is broken. Call them only after `IsBroken` returned `true` (see [Three ways to run a rule](#three-ways-to-run-a-rule)).

To format the detail sentence for the caller's culture, set the provider on the context:

```csharp
BusinessRuleContext context = new BusinessRuleContext();
context.FormatProvider = CultureInfo.GetCultureInfo("de-DE");

IBusinessRule limit = new TransactionLimitRule(5200m, 5000m);
BusinessRuleResult result = limit.Evaluate(context);
// result.Detail: "Amount 5.200,00 exceeds the limit of 5.000,00."
```

**Details with the builder.** Builder rules get the same details from `WithDetail`, `WithActual`, `WithExpected` and `WithRemediation`. Each one takes a function of the context. `WithExpected` and `WithRemediation` also accept a fixed value.

```csharp
IBusinessRule limit = BusinessRuleBuilder.Create("LIMIT.EXCEEDED")
    .WithMessage("Transaction limit exceeded.")
    .WithCategory("Payments")
    .WithTags("customer-facing")
    .When(ctx => ctx.Get<decimal>("amount") > ctx.Get<decimal>("limit"))
    .WithDetail(ctx => $"Amount {ctx.Get<decimal>("amount"):N2} exceeds the limit of {ctx.Get<decimal>("limit"):N2}.")
    .WithActual(ctx => ctx.Get<decimal>("amount"))
    .WithExpected(ctx => $"<= {ctx.Get<decimal>("limit")}")
    .WithRemediation("Split the transaction.")
    .Build();
```

- The functions run only when the rule is broken. If one throws, the result is reported without details.
- When the rule runs without a context, the functions receive an empty context.
- The builder has no equivalent of `.With(key, value)`. To fill `Data`, write a rule class with `Describe`.

**Filtering results.** The exception and the evaluation results let you pick out results by category or tag:

```csharp
// rules (a List<IBusinessRule>) and context come from your application
try
{
    BusinessRuleChecker.CheckAll(rules, context: context);
}
catch (BusinessRuleValidationException exception)
{
    IEnumerable<BusinessRuleResult> paymentErrors = exception.ByCategory("Payments");
    IEnumerable<BusinessRuleResult> customerFacing = exception.ByTag("customer-facing");
}
```

- `ByCategory(category)` matches exactly and is case-sensitive.
- `ByTag(tag)` ignores case.
- Both exist on `BusinessRuleValidationException`, on the `EvaluateAll` result and on the `EvaluateAllAsync` result.
- `result.HasTag(tag)` checks a single result and also ignores case.

> **Note:** The filters look only at the top-level results. Groups and other combined rules have no category or tags, so a broken rule inside a group is not matched. Filter the flattened results instead: `exception.Flatten().Where(result => result.HasTag("customer-facing"))`.

**Returning Problem Details.** A result holds everything an RFC 7807 Problem Details response needs. This ASP.NET Core helper turns the exception into a 422 response body:

```csharp
private static ProblemDetails ToProblemDetails(BusinessRuleValidationException exception)
{
    ProblemDetails problem = new ProblemDetails
    {
        Title = "One or more business rules were violated.",
        Status = StatusCodes.Status422UnprocessableEntity,
    };

    problem.Extensions["errors"] = exception.Flatten()
        .Select(result => new
        {
            code = result.Code,
            category = result.Category,
            message = result.Message,
            detail = result.Detail,           // "Amount 5,200.00 exceeds the limit of 5,000.00."
            actual = result.Actual,           // 5200
            expected = result.Expected,       // "<= 5000"
            remediation = result.Remediation,
            data = result.Data,
        })
        .ToList();

    return problem;
}
```

In a controller, catch the exception and return `UnprocessableEntity(ToProblemDetails(exception))`. `Flatten()` makes sure that each broken rule inside a group gets its own entry.

---

## Running Many Rules

Most operations need more than one rule. `BusinessRuleChecker` runs a batch of sync rules and `AsyncBusinessRuleChecker` runs a batch of async rules. You can stop at the first problem, collect every problem, or decide per severity what blocks the operation.

### Check all or evaluate all

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
- The async checker accepts only async rules. Convert a sync rule with `.ToAsync()` to run it in the same batch (see [Mixing sync and async rules](#mixing-sync-and-async-rules)).
- Subject-typed rules have matching overloads that take the subject first (see [Subject-Typed Rules](#subject-typed-rules)).
- For fault handling, severity thresholds and observers, pass an options object instead of the boolean parameters (see [Options](#options)).

### Filtering by severity, category or tags

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

> **Note:** The sync filter methods take no context, so the selected rules run without one. If your rules read the context, filter the list yourself and call `CheckAll` with the context.

To filter the outcomes of a batch instead of the rules, see [Reading the result](#reading-the-result).

### Reading the result

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
| `ShadowResults`, `HasShadowResults` | What shadow-mode rules would have reported; never counted as broken (see [Seeing what shadow rules would have blocked](#seeing-what-shadow-rules-would-have-blocked)) |

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

> **Note:** `AllPassed` means "no broken results". A rule that faulted under `FaultPolicy.Record`, or was skipped because `StopOnFirstFailure` stopped the batch, is neither broken nor passed. It does not appear in `PassedRules`, and `AllPassed` can still be true. Check `HasFaults` as well when you record faults.

### Options

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
| `Observer` | `null` | Yes | Yes | Receives per-rule and per-batch callbacks (see [Observers](#observers)) |
| `RunInParallel` | `false` | No | Yes | Evaluate the rules concurrently |

Keep these rules in mind:

- The argument order differs. Sync: `(rules, context, options)`. Async: `(context, rules, options, cancellationToken)`.
- In `CheckAll`, only a broken rule at or above `ThrowOn` stops a `StopOnFirstFailure` batch. A broken warning does not stop the search for an error. In `EvaluateAll`, any broken rule stops it.
- `StopOnFirstFailure` wins over `RunInParallel`. When both are true, the batch runs one rule at a time.
- In a parallel batch, the results are still in submission order.
- The `EvaluateAll` overloads without options have no observer. Pass options to get observer callbacks from `EvaluateAll`.
- Passing `null` options throws `ArgumentNullException`.

> **Note:** Parallel rules share one `BusinessRuleContext`, and the context is not thread-safe. Rules that run in parallel may read the context but should not write to it.

### When a rule throws

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

> **Note:** Inside a batch, a rule that fails to read the context (a missing key or a value of the wrong type) is reported as a `BusinessRuleContextException` that names the rule's `Code`. The original exception is its `InnerException`. Catch `BusinessRuleContextException`, not `ContextKeyNotFoundException`, around a checker call. The context exceptions are listed in [Passing data with a context](#passing-data-with-a-context).

### Warnings that do not block

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

> **Note:** When `CheckAll` does not throw, the broken warnings are not returned anywhere. If you need them, use `EvaluateAll` and then `ThrowIfAtLeast`, as in the first example.

**Changing a rule's severity.** `rule.WithSeverity(RuleSeverity.Warning)` reports a rule at a new severity without subclassing it.

- The verdict, `Code`, `Message` and other metadata stay the same.
- Filters and thresholds see the new severity.
- It works on sync and async rules. In an explain tree it appears as a `SEVERITY` node.

### The evaluation ledger

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

---

## Conditional and Cached Rules

These wrappers change *when* a rule runs, not *what* it checks. Use them to skip a rule that does not apply, to avoid repeating an expensive check, or to run a sync rule where async code expects an async one (and the other way round).

Each wrapper keeps the inner rule's `Code`, `Message`, `Severity` and other metadata. You can pass a wrapped rule anywhere you pass the original.

### Run a rule only when a condition holds

Use `.When(...)` on a rule when it only applies in some situations. For example, a credit-limit check only matters for orders paid by invoice.

```csharp
// order comes from your application
IBusinessRule creditLimit = BusinessRuleBuilder.Create("ORDER.CREDIT_LIMIT_EXCEEDED")
    .WithMessage("The order exceeds the customer's credit limit.")
    .When(() => order.Total > order.Customer.CreditLimit)
    .Build();

IBusinessRule creditLimitForInvoices = creditLimit.When(() => order.PaysByInvoice);

creditLimitForInvoices.Check(); // runs the credit check only for invoice orders
```

For async rules, the condition receives the context:

```csharp
// fraudService, context and cancellationToken come from your application
IAsyncBusinessRule fraudCheck = AsyncBusinessRuleBuilder.Create("ORDER.FRAUD_SUSPECTED")
    .WithMessage("The order was flagged for a fraud review.")
    .WhenAsync(async (ctx, ct) => await fraudService.IsSuspiciousAsync(ctx.Get<Order>("order"), ct))
    .Build();

IAsyncBusinessRule fraudCheckForLargeOrders = fraudCheck.When(ctx => ctx.Get<Order>("order").Total > 1000m);

await fraudCheckForLargeOrders.CheckAsync(context, cancellationToken);
```

What happens on each call:

| Condition | Result |
|-----------|--------|
| `true` | The inner rule runs and its verdict is returned. |
| `false` | The inner rule does not run. The rule counts as passing, so `Check` does not throw. |

The condition has a different type for sync and async rules:

| Rule | Method | Condition type |
|------|--------|----------------|
| Sync | `rule.When(...)` | `Func<bool>` (no context) |
| Async | `asyncRule.When(...)` | `Func<BusinessRuleContext, bool>` |

- The condition runs on every call. Its result is never cached.
- The condition is always synchronous, even for async rules.
- `.When(...)` returns `IBusinessRule` / `IAsyncBusinessRule`. If you need the concrete type, construct `new ConditionalBusinessRule(rule, condition)` or `new ConditionalAsyncBusinessRule(rule, condition)` yourself.
- A subject-typed rule needs its subject bound first: `rule.For(order).When(...)`.
- In an explain tree, a skipped rule shows as `Skipped` (operator `WHEN`, reason `precondition not met`), and the inner rule shows as `NotEvaluated`.

> **Note:** Do not confuse the two `When` methods.
> - On a builder, `When(...)` sets the condition under which the rule is *broken*.
> - On a built rule, `.When(...)` sets a precondition that decides whether the rule *runs at all*.
>
> A sync precondition cannot read the context. If you need one that does, put the check into the rule's own condition: `.When(ctx => ctx.Get<bool>("paysByInvoice") && ...)`.

### Cache expensive rules

Use `.Cached(duration)` when a rule is slow (a database query, a remote call) and its answer does not change often. The first call runs the rule. Later calls within the duration return the stored verdict without running it.

```csharp
// paymentGateway and context come from your application
IAsyncBusinessRule gatewayDown = AsyncBusinessRuleBuilder.Create("PAYMENT.GATEWAY_DOWN")
    .WithMessage("Payments are unavailable right now.")
    .WhenAsync(async (ctx, ct) => !await paymentGateway.IsHealthyAsync(ct))
    .Build();

CachedAsyncBusinessRule cachedGatewayDown = gatewayDown.Cached(TimeSpan.FromSeconds(30));

bool first = await cachedGatewayDown.IsBrokenAsync(context);  // calls the gateway
bool second = await cachedGatewayDown.IsBrokenAsync(context); // served from the cache
```

`.Cached(...)` works on sync and async rules. It returns the concrete `CachedBusinessRule` or `CachedAsyncBusinessRule`, so the invalidation methods are available on the result. You can also use the constructors: `new CachedBusinessRule(rule, duration)` or `new CachedBusinessRule(rule, duration, cacheKeySelector)`, and the same with `CachedAsyncBusinessRule`.

**One verdict per key.** By default a cached rule stores **one verdict per instance**, whatever context you pass. That is wrong for a rule whose verdict depends on the context, such as a tenant, a region or scoped parameters: tenant ACME would get tenant Globex's answer. Pass a key selector to store one verdict per key instead:

```csharp
// accountOnHold is a rule from your application whose verdict depends only on the tenant
BusinessRuleContext acmeContext = new BusinessRuleContext();
acmeContext.Set(RuleParameters.ScopeKey, RuleScope.Tenant("acme"));

BusinessRuleContext globexContext = new BusinessRuleContext();
globexContext.Set(RuleParameters.ScopeKey, RuleScope.Tenant("globex"));

CachedBusinessRule perTenant = accountOnHold.Cached(
    TimeSpan.FromMinutes(5),
    ctx => ctx.TryGet(RuleParameters.ScopeKey, out RuleScope scope) ? scope.ToString() : RuleScope.Global.ToString());
    // keys: "tenant=acme", "tenant=globex", or "(global)" when the context has no scope

perTenant.IsBroken(acmeContext);   // evaluates for acme
perTenant.IsBroken(globexContext); // evaluates for globex, in a separate entry
perTenant.IsBroken(acmeContext);   // served from acme's entry

perTenant.InvalidateCache("tenant=acme"); // drops acme's entry only
```

- The key must capture everything the verdict depends on. Only the verdict is stored, not the inputs that produced it.
- Keep the key space small and bounded: tenants or regions, never one key per request. Expired entries are removed only when a new key is stored.
- `CachesPerKey` tells you which mode an instance uses.
- The sync `IsBroken()` and `Check()` without a context always use one shared entry, separate from the per-key entries.

> **Note:** The selector runs on every call that has a context, so it must handle a context without a scope, as the `TryGet` above does. `ctx.Get(RuleParameters.ScopeKey)` would throw `ContextKeyNotFoundException` there. A selector that returns `null` throws `RuleConfigurationException`.

**Clearing the cache.** Clear entries when you know the underlying data changed, instead of waiting for them to expire.

| Sync | Async | Clears |
|------|-------|--------|
| `InvalidateCache()` | `InvalidateCacheAsync()` | Every entry: the shared one and all keys |
| `InvalidateCache(key)` | `InvalidateCacheAsync(key)` | One key. Other keys and the shared entry stay. An unknown key is ignored. |

A `null` key throws `ArgumentNullException`. The async methods accept an optional `CancellationToken`.

**What is and is not cached.** The cache stores only the verdict (broken or passing), and only until the duration expires. These are **never** stored:

- An exception thrown by the inner rule. The next call runs the rule again.
- A cancelled async evaluation.
- A verdict from `Explain(context, captureExceptions: true)` when the inner rule faulted. A diagnostic explain during an outage cannot leave the rule "broken" for the whole duration.
- A verdict computed while you call `InvalidateCache`. The caller still gets it, but it is not stored.

What a broken result contains depends on where it came from:

| Source | Result |
|--------|--------|
| Fresh evaluation | The inner rule's full result, including member `Details` |
| Cache hit (sync) | The inner rule's `Evaluate(context)` result: metadata and `Describe` details, but no member details |
| Cache hit (async) | The rule's metadata only |

Other behaviour to know:

- The cache is thread-safe. A slow evaluation for one key does not block cache hits or evaluations for other keys. Two calls that miss the same key at the same time may both run the inner rule; the verdict stored last wins.
- In an explain tree, a served entry shows as `CacheHit` (operator `CACHED`), and the inner rule shows as `NotEvaluated`.
- A subject-typed rule needs its subject bound first: `rule.For(order).Cached(...)`.

> **Note:** Wrapper order matters. `rule.When(condition).Cached(ttl)` caches the skipped "pass" too, and serves it for the whole duration even after the condition becomes true. Write `rule.Cached(ttl).When(condition)` so the condition is checked on every call. The same applies to feature gates: put the cache inside the gate, as in `rule.Cached(ttl).GatedBy(...)` (see [Gating a rule](#gating-a-rule)).

### Mixing sync and async rules

Use `.ToAsync()` when async code needs to run a sync rule. A common case is checking sync and async rules together in one async batch.

```csharp
// customer, emailMustBeUnique (an IAsyncBusinessRule) and context come from your application
IBusinessRule customerIsAdult = BusinessRuleBuilder.Create("CUSTOMER.UNDERAGE")
    .WithMessage("The customer must be at least 18 years old.")
    .When(() => customer.Age < 18)
    .Build();

await AsyncBusinessRuleChecker.CheckAllAsync(context, customerIsAdult.ToAsync(), emailMustBeUnique);
```

- The sync rule runs on the calling thread. The cancellation token is checked once, before the rule starts.
- The context you pass to the async call reaches the sync rule.
- A subject-typed rule works too: `IBusinessRule<T>.ToAsync()` returns an `IAsyncBusinessRule<T>`, with no subject bound.
- A specification has its own `ToAsync()`, covered in [Specifications and Database Queries](#specifications-and-database-queries).
- `.ToAsync()` returns `IAsyncBusinessRule`. Construct `new SyncToAsyncRuleAdapter(rule)` if you need the concrete type.

**Running an async rule from sync code.** There is deliberately no extension method for this direction. Construct `AsyncToSyncRuleAdapter` yourself:

```csharp
// emailMustBeUnique (an IAsyncBusinessRule) comes from your application
IBusinessRule emailMustBeUniqueSync = new AsyncToSyncRuleAdapter(emailMustBeUnique);

emailMustBeUniqueSync.Check(); // blocks until the async rule completes
```

- The optional second constructor argument is the context to use when the sync caller passes none. Without it, the adapter creates one empty context when you construct it and reuses that context on every call.
- When the caller passes a context, the adapter uses that one.
- The async rule always receives `CancellationToken.None`, so a call through the adapter cannot be cancelled.

> **Note:** `AsyncToSyncRuleAdapter` blocks the calling thread until the async rule finishes. On a thread with a synchronization context (a UI thread, classic ASP.NET) this can deadlock. Elsewhere it holds a thread-pool thread for the whole call. Prefer making the caller async and calling `.ToAsync()` on the sync rules instead.

---

## Feature Flags and Shadow Rollout

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

### Gating a rule

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

> **Note:** Always pass a context to a sync gated rule. Without one, for example `rule.IsBroken()` or `BusinessRuleChecker.EvaluateAll(rule)`, the rule ignores its flag and runs as if `Enforced`, so a shadow rule blocks. Async rules always have a context.

> **Note:** Put a cache inside the gate, never outside it. `rule.Cached(ttl).GatedBy(...)` caches the inner rule's result and still checks the flag on every call. `rule.GatedBy(...).Cached(ttl)` caches the gate's answer, so a flag change has no effect until the cache expires. If the result depends on the caller, cache per key, as shown in [Cache expensive rules](#cache-expensive-rules).

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

### Seeing what shadow rules would have blocked

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

> **Note:** Shadow results are collected only when the rule runs through a checker: `BusinessRuleChecker`, `AsyncBusinessRuleChecker`, or the dependency injection runner and registry, which use them. If you call `IsBroken(context)` on a shadow rule directly, it returns `false` and the violation is not recorded anywhere.

### Using Microsoft.FeatureManagement

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

> **Note:** An assigned variant wins even when the flag is disabled, because Microsoft.FeatureManagement still assigns the `default_when_disabled` variant. Point that allocation at an `Off` variant, as in the example above.

> **Note:** Ramp by percentage in one place only.
> - A variant's `Percent` picks callers by the rule's `stableKey`.
> - A `Microsoft.Percentage` or `Microsoft.Targeting` filter picks callers inside Microsoft.FeatureManagement.
>
> If you use both, each caller is filtered twice. With a targeting filter, set `TargetingContextSelector` so the filter sees the same customer as your stable key.

> **Note:** Microsoft.FeatureManagement is async only. The provider's sync `GetRollout` blocks on the async call. Prefer async gated rules on request paths.

To validate variant names or configuration at startup, call `RolloutVariantConvention.TryParse(name, out RuleRollout rollout)` or `RolloutVariantConvention.TryRead(section, out RuleRollout rollout)`. Without dependency injection, create the provider directly: `new FeatureManagementRuleFeatureFlagProvider(variantFeatureManager, options)`.

---

## Per-Tenant Rules and Parameters

Multi-tenant applications often need the same rules with different limits, or different rules, per tenant. You can do both without writing `if (tenant == "acme")` in your rules. A scope says who an evaluation is for, parameters supply per-scope values, and a registry supplies per-scope rule sets.

### Rule scopes

A `RuleScope` describes who an evaluation is for: a tenant, a region, or any other key/value pairs. You put it on the context. Parameter providers and registries use it to pick the right values and rules.

```csharp
RuleScope acmeEu = RuleScope.Tenant("acme").With("region", "EU");

BusinessRuleContext context = new BusinessRuleContext();
context.Set(RuleParameters.ScopeKey, acmeEu);
```

How scopes behave:

- `RuleScope.Global` has no pairs. It is used when the context has no scope.
- `RuleScope.Tenant(id)` is shorthand for `With("tenant", id)`. `TenantId` reads it back.
- Scopes are immutable. `With` returns a new scope, and replaces the value if the key already exists.
- Two scopes are equal when they have the same pairs, in any order. Keys and values are case-sensitive, so `Tenant("ACME")` is not `Tenant("acme")`.
- A scope **applies to** a request when all of its pairs are in the request. `Tenant("acme")` applies to `Tenant("acme").With("region", "EU")`. `Global` applies to everything.
- When several scopes apply, the one with more pairs (higher `Specificity`) is more specific and wins.
- `ToString()` gives a stable text form such as `region=EU;tenant=acme`, with keys sorted.

> **Note:** The value under `RuleParameters.ScopeKey` must be a `RuleScope`. A tenant id stored there as a plain string throws `ContextValueTypeMismatchException` (checkers report it wrapped in `BusinessRuleContextException`). The library does not silently fall back to the global scope.

### Externalized parameters

An externalized parameter is a named value, such as an order limit, that a rule reads at evaluation time instead of hard-coding it. A provider supplies the value for the current scope. Use it when limits differ per tenant, or change without a redeploy.

```csharp
// order comes from your application
InMemoryRuleParameterProvider parameters = new InMemoryRuleParameterProvider()
    .Set("maxOrderAmount", 10_000m)                                                    // everybody
    .Set("maxOrderAmount", 50_000m, RuleScope.Tenant("acme"))                          // ACME
    .Set("maxOrderAmount", 70_000m, RuleScope.Tenant("acme").With("region", "EU"));    // ACME in the EU

IBusinessRule orderLimit = BusinessRuleBuilder.Create("ORDER.TOO_LARGE")
    .WithMessage("Order amount exceeds the limit.")
    .WithParameters(parameters)
    .WithParameter<decimal>("maxOrderAmount", fallback: 10_000m)
    .When((ctx, p) => order.Total > p.Get<decimal>("maxOrderAmount"))
    .Build();

// With order.Total = 60 000:
BusinessRuleContext acmeEu = new BusinessRuleContext();
acmeEu.Set(RuleParameters.ScopeKey, RuleScope.Tenant("acme").With("region", "EU"));
bool brokenForAcmeEu = orderLimit.IsBroken(acmeEu);   // false: the limit is 70 000

BusinessRuleContext globex = new BusinessRuleContext();
globex.Set(RuleParameters.ScopeKey, RuleScope.Tenant("globex"));
bool brokenForGlobex = orderLimit.IsBroken(globex);   // true: Globex has no value, so the global 10 000 applies
```

**Declaring parameters.** All four builders have `WithParameters(provider)` and `WithParameter<T>(name, fallback)`.

- The fallback is mandatory. It is used when no provider is set, or when the provider has no value for the scope. So a rule with parameters never throws for a missing value, even when you evaluate it without a context.
- Each name can be declared once per builder. Declaring it twice throws `ArgumentException`.
- Values are looked up on every evaluation. Changing a value in the provider never requires rebuilding the rule.

**Reading parameters.** The condition receives the resolved values as `RuleParameters`:

| Builder | Condition overload |
|---------|--------------------|
| `BusinessRuleBuilder` | `When((ctx, p) => ...)` |
| `BusinessRuleBuilder<T>` | `When((subject, ctx, p) => ...)` |
| `AsyncBusinessRuleBuilder` | `WhenAsync((ctx, p, ct) => ...)` |
| `AsyncBusinessRuleBuilder<T>` | `WhenAsync((subject, ctx, p, ct) => ...)` |

`p.Get<T>(name)` returns a value. It throws `KeyNotFoundException` for a name you did not declare (the message lists the declared names), and `InvalidCastException` for the wrong type. `p.TryGet<T>`, `p.Contains`, `p.Names` and `p.Scope` are also available.

**Providers.** A provider implements `IRuleParameterProvider` (`TryGet<T>(name, scope, out value)`) or `IAsyncRuleParameterProvider` (`TryGetAsync<T>(name, scope, cancellationToken)`, which returns a `RuleParameterLookup<T>`).

- The async builders also accept an async provider, and prefer it over a sync one.
- `InMemoryRuleParameterProvider` implements both interfaces and is thread-safe. It returns the value of the most specific scope that applies. Among equally specific scopes, the value set most recently wins.
- A value stored with a different type than the one you request throws `InvalidCastException`, naming the parameter, the scope and both types. This is treated as a configuration error, not as a reason to use the fallback.
- A provider stored on the context under `RuleParameters.ContextKey` (`RuleParameters.AsyncContextKey` for async) overrides the builder's provider for that evaluation. Use it for tests or per-request overrides, not as your main wiring.

To read values from your own configuration, implement the interface:

```csharp
public sealed class ConfigurationParameterProvider : IRuleParameterProvider
{
    private readonly IReadOnlyDictionary<string, decimal> _limits;

    public ConfigurationParameterProvider(IReadOnlyDictionary<string, decimal> limits)
    {
        _limits = limits;
    }

    public bool TryGet<T>(string name, RuleScope? scope, out T value)
    {
        string key = scope?.TenantId is string tenantId ? $"{tenantId}:{name}" : name;

        if (_limits.TryGetValue(key, out decimal limit) && limit is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;   // the Try pattern: the value is ignored when the method returns false
        return false;       // no value: the rule uses its fallback
    }
}
```

**Class-based rules.** Inject the provider through the constructor and read the scope from the context yourself:

```csharp
public sealed class OrderLimitRule : BaseBusinessRule
{
    private const string LimitName = "maxOrderAmount";
    private const decimal DefaultLimit = 10_000m;

    private readonly Order _order;
    private readonly IRuleParameterProvider _parameters;

    public OrderLimitRule(Order order, IRuleParameterProvider parameters)
    {
        _order = order;
        _parameters = parameters;
    }

    public override string Code => "ORDER.TOO_LARGE";

    public override string Message => "Order amount exceeds the limit.";

    public override bool IsBroken() => _order.Total > GetLimit(null);

    public override bool IsBroken(BusinessRuleContext context) => _order.Total > GetLimit(context);

    protected override RuleResultDetails Describe(BusinessRuleContext? context)
        => RuleResultDetails.Detail($"Order total {_order.Total} exceeds {GetLimit(context)}")
            .Actual(_order.Total)
            .Expected(GetLimit(context));

    private decimal GetLimit(BusinessRuleContext? context)
    {
        RuleScope? scope = null;
        if (context != null && context.TryGet(RuleParameters.ScopeKey, out RuleScope stored))
        {
            scope = stored;
        }

        return _parameters.TryGet(LimitName, scope, out decimal limit) ? limit : DefaultLimit;
    }
}
```

Put the scoped value in the result details (`Expected`), not in `Message`. The message stays a fixed string.

> **Note:** A cached rule keeps one result for everybody by default, which is wrong when the limit depends on the scope. Cache it per scope with a key selector that also handles a context without a scope. [Cache expensive rules](#cache-expensive-rules) shows one.

### Rule registries

A rule registry holds named rule sets: a base version for everybody, plus overrides for specific scopes. Use it when tenants need different rules, not only different values. The registry is part of the `Masterly.BusinessRules.DependencyInjection` package; see [Dependency Injection](#dependency-injection) to install it.

```csharp
// order and parameters come from your application. Your rule classes:
// CartMustHaveItemsRule (code CART.EMPTY), OrderLimitRule (ORDER.TOO_LARGE),
// AcmePurchaseOrderRequiredRule and EuOrderLimitRule
RuleRegistry registry = RuleRegistry.Create()
    .AddSet("checkout", set => set
        .Add(ctx => new CartMustHaveItemsRule(ctx.Get(OrderKeys.Order)))
        .Add(ctx => new OrderLimitRule(ctx.Get(OrderKeys.Order), parameters)))
    .AddSet("checkout", RuleScope.Tenant("acme"), set => set
        .Disable("CART.EMPTY")
        .Add(ctx => new AcmePurchaseOrderRequiredRule(ctx.Get(OrderKeys.Order))))
    .AddSet("checkout", RuleScope.Tenant("acme").With("region", "EU"), set => set
        .Replace("ORDER.TOO_LARGE", ctx => new EuOrderLimitRule(ctx.Get(OrderKeys.Order))))
    .Build();

BusinessRuleContext context = new BusinessRuleContext();
context.Set(OrderKeys.Order, order);

// For ACME in the EU: CART.EMPTY is removed, the order limit is replaced, the ACME rule is added
registry.CheckAll("checkout", RuleScope.Tenant("acme").With("region", "EU"), context);

public static class OrderKeys
{
    public static readonly ContextKey<Order> Order = new ContextKey<Order>("order");
}
```

**Building a set.** Each `AddSet(name, scope, ...)` call adds one layer to a set. Without a scope, the layer is global. Inside a layer:

| Method | Effect |
|--------|--------|
| `Add(ctx => rule)` or `Add(rule)` | Adds a sync rule |
| `AddAsyncRule(ctx => rule)` or `AddAsyncRule(rule)` | Adds an async rule |
| `Disable(code)` | Removes every rule with that code that earlier layers added |
| `Replace(code, ctx => rule)` or `ReplaceAsyncRule(code, ctx => rule)` | `Disable`, then `Add` |

`Build()` freezes the registry. Adding a set or calling `Build()` again afterwards throws `InvalidOperationException`.

**How layers merge.** For a requested scope, the registry takes every layer whose scope applies to it and merges them in this order:

- Less specific layers first, more specific layers last.
- Layers with the same specificity in the order you declared them.

Each layer first applies its `Disable` calls to the rules merged so far, then adds its own rules. So a later layer can remove or replace what an earlier layer added, never the other way round.

For example, suppose you also add a `region=EU` layer, declared before the ACME layer. A request for `tenant=acme;region=EU` then merges four layers:

| Step | Layer | Why it is in this position |
|------|-------|----------------------------|
| 1 | `Global` | Least specific; applies to every request |
| 2 | `region=EU` | Same specificity as `tenant=acme`, declared first |
| 3 | `tenant=acme` | Declared after `region=EU`, so its `Disable` can remove the region layer's rules |
| 4 | `tenant=acme;region=EU` | Most specific; its `Disable` reaches every earlier layer |

**Running a set.** Pass the set name, the scope and the context. A `null` scope means `Global`.

- `CheckAllAsync` and `EvaluateAllAsync` run the whole set: the sync rules first (adapted to async), then the async rules.
- `CheckAll` and `EvaluateAll` run sync rules only. If the resolved set has async rules, they throw `InvalidOperationException` listing the codes, rather than skipping them.
- Every batch method has an overload that takes `RuleEvaluationOptions` or `AsyncRuleEvaluationOptions`.
- `Resolve` and `ResolveAsyncRules` return the rules without running them.
- An unknown set name throws `RuleSetNotFoundException`, a `KeyNotFoundException` that lists `AvailableSets`.
- A factory that returns `null` throws `InvalidOperationException` naming the set, the scope and the position.
- Nothing is cached. The layers are merged again on every call.

**The scope and parameters.** The requested scope is also the scope that parameterized rules read their values for.

- The batch methods store the requested scope on the context under `RuleParameters.ScopeKey` for the duration of the call, then restore what was there before. You can safely reuse one context for tenant A and then tenant B.
- The requested scope replaces any scope already on the context. A `null` scope therefore resolves global parameters.
- `Resolve` and `ResolveAsyncRules` store the scope and leave it there, because you evaluate the returned rules later against that context.
- If you pass a `null` context, the registry creates a new one for the call.

> **Note:** Every factory in every applying layer runs, even for a rule that a later layer disables. `Disable` matches on the rule's code, so it removes the rule after the factory has created it. A global factory that reads a context value therefore runs for ACME too, and that value must be present.

**With dependency injection.** Register the same sets with `AddRuleSet` inside `AddBusinessRules`, then inject `IRuleRegistry`. [Dependency Injection](#dependency-injection) shows a complete example.

---

## Specifications and Database Queries

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
- `Code`, `Message` and `Severity` work as on any other rule, so a specification can go straight into `BusinessRuleChecker.CheckAll(order, overdue)`.
- `WhereBroken` returns the rows that break the rule. `WhereSatisfies` returns the rows that pass it.
- If you need the raw predicates, read `BrokenExpression` or `SatisfiedExpression`.
- If `BrokenWhen` returns `null`, the first use throws `RuleConfigurationException`.

> **Note:** A specification ignores the `BusinessRuleContext`. Both `IsBroken` overloads are sealed so the in-memory verdict always matches the database query. Everything the rule needs must come from the subject or from the expression itself.

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

> **Note:** When a provider translates the query, values such as `DateTime.UtcNow` are usually evaluated by the database with its own clock. In memory, your server's clock is used. Keep this in mind for rules near a date boundary.

**Async.** There is no async specification, because an expression tree has no async form. Call `.ToAsync()` to use a specification in async batches, groups and `AsyncBusinessRuleChecker` calls:

```csharp
// overdue is from the first example; order, context and cancellationToken come from your application
IAsyncBusinessRule<Order> asyncOverdue = overdue.ToAsync();

await AsyncBusinessRuleChecker.CheckAllAsync(context, order, new[] { asyncOverdue }, cancellationToken);
```

**Existence checks are not specifications.** "The customer must exist" or "the email must not already be taken" need a repository call, not a predicate over the row. Write them as an async builder rule:

```csharp
// customers is your repository; asyncOverdue is from the previous example;
// order, context and cancellationToken come from your application
IAsyncBusinessRule<Order> customerMustExist = AsyncBusinessRuleBuilder<Order>.Create("ORDER.CUSTOMER_MISSING")
    .WithMessage("Order references a customer that does not exist.")
    .WhenAsync(async (o, ctx, ct) => !await customers.ExistsAsync(o.CustomerId, ct))
    .Build();

await AsyncBusinessRuleChecker.CheckAllAsync(context, order, new[] { asyncOverdue, customerMustExist }, cancellationToken);
```

---

## Rule Expressions

The `Masterly.BusinessRules.Expressions` package lets you write a rule's condition as text, such as `amount > @maxOrderAmount && customer.Tier != 'VIP'`. Use it when conditions live in configuration, a database or an admin screen, and when you want to explain a verdict with the actual values that produced it.

```bash
dotnet add package Masterly.BusinessRules.Expressions
```

The package depends only on the core package and targets `netstandard2.1`.

```csharp
using Masterly.BusinessRules;
using Masterly.BusinessRules.Expressions;

// customer comes from your application; customer.Tier is CustomerTier.Gold
RuleExpression expression = RuleExpression.Parse("amount > @maxOrderAmount && customer.Tier != 'VIP'");

BusinessRuleContext context = new BusinessRuleContext();
context.Set("amount", 12_000m);
context.Set("customer", customer);
context.Set("maxOrderAmount", 10_000m);

ExpressionBusinessRule rule = expression.ToRule("ORDER.TOO_LARGE", "Order exceeds the customer's limit.");

bool isBroken = rule.IsBroken(context);   // true
rule.Check(context);                      // throws BusinessRuleValidationException
```

The text describes when the rule is **broken**, like every condition in this library. A `RuleExpression` is immutable, so parse it once and reuse it.

Names in the text bind to the context like this:

- `amount` reads the context entry with the key `amount`. Context keys are case-sensitive.
- `customer.Tier` reads public properties. A `null` anywhere in the path gives `null` instead of throwing.
- `Data` reads the typed data of a `BusinessRuleContext<T>`. Without typed data, it reads a context entry named `Data`.
- `@maxOrderAmount` is a parameter. Parameters are described below.

**Turning text into a rule.** You have three options. Text is parsed when you call `RuleExpression.Parse` or `WhenExpression`, so malformed text fails when you create the rule, not when it first runs.

| You want | Use | You get |
|----------|-----|---------|
| A sync rule | `expression.ToRule(code, message, parameters?, severity?)` | `ExpressionBusinessRule` |
| An async rule | `expression.ToAsyncRule(code, message, asyncParameters?, parameters?, severity?)` | `ExpressionAsyncBusinessRule` |
| Builder features (name, category, tags, details, declared parameters) | `.WhenExpression(text)` on `BusinessRuleBuilder` or `AsyncBusinessRuleBuilder` | `Build()` returns an `ExpressionBusinessRule` or `ExpressionAsyncBusinessRule` |

```csharp
IBusinessRule orderLimit = BusinessRuleBuilder.Create("ORDER.TOO_LARGE")
    .WithMessage("Order exceeds the customer's limit.")
    .WithCategory("Orders")
    .WithParameter("maxOrderAmount", 10_000m)
    .WhenExpression("amount > @maxOrderAmount")
    .Build();
```

Expression rules work everywhere other rules do: combined rules, groups, wrappers and the checkers. Their `Description` is the expression text unless you set one.

> **Note:** An expression rule always needs a context. `IsBroken()` and `Check()` without a context throw `InvalidOperationException`, and so does a checker or combined rule that evaluates the rule without one. Always pass a `BusinessRuleContext`.

> **Note:** On a builder, the last condition wins. A `When` or `WhenAsync` call after `WhenExpression` replaces the expression, and the other way round.

**Explaining a verdict.** An explanation shows the value bound to every name and the result of every step:

```csharp
// rule and context are from the first example in this section
RuleExpressionExplanation why = rule.ExplainExpression(context);

Console.WriteLine(why.Rendered);
// (amount(12000) > @maxOrderAmount(10000) [true]) && (customer.Tier(Gold) != 'VIP' [true]) => true
```

How to read `Rendered`:

- `amount(12000)`: the value bound to that name.
- `[true]` after a sub-expression: its result. `[not evaluated]` means short-circuiting skipped it.
- `=> true` at the end: the verdict.

The explanation also has `Result` (the verdict) and `Steps` (the bindings and operations in evaluation order). It comes from the same evaluation that produces the verdict, so the two can never disagree. Values are formatted with `RuleExpressionOptions.Culture`, which is the invariant culture by default.

| To explain | Call |
|------------|------|
| A `RuleExpression` | `Explain(context)` or `ExplainAsync(context, ...)` |
| An `ExpressionBusinessRule` | `ExplainExpression(context)` |
| An `ExpressionAsyncBusinessRule` | `ExplainExpressionAsync(context, redact, cancellationToken)` |

To keep personal data out of explanations, pass a redactor. It receives each path as written (`customer.Ssn`, or `@maxOrderAmount` with the `@`) and its value, and returns the text to show:

```csharp
// rule and context are from the first example in this section
RuleExpressionExplanation masked = rule.ExplainExpression(
    context,
    redact: (name, value) => name == "customer.Ssn" ? "***" : value?.ToString());
```

The redactor runs before anything is recorded, so the real value never reaches `Steps` or `Rendered`.

> **Note:** When you pass a redactor, it formats every value, not only the masked ones. Its string is shown as is and `Culture` is not applied, so return a readable string for the values you do not mask.

**Parameters (`@name`).** A parameter reads a named setting, such as a per-tenant limit. The first source that has a value wins:

1. Parameters declared on the builder with `WithParameter` (rules built with `WhenExpression`).
2. A provider stored in the context under `RuleParameters.AsyncContextKey` (async evaluation only), then under `RuleParameters.ContextKey`.
3. The provider you passed to `ToRule`, `ToAsyncRule`, `Evaluate` or the condition compiler. An async provider is asked before a sync one.
4. The context entry with the same name, without the `@`.

If no source has a value, evaluation throws `RuleExpressionBindingException`. Providers read the scope stored under `RuleParameters.ScopeKey`, as in the rest of the library.

```csharp
// expression and context are from the first example; tenantSettings is an IAsyncRuleParameterProvider;
// cancellationToken comes from your application
ExpressionAsyncBusinessRule asyncRule = expression.ToAsyncRule(
    "ORDER.TOO_LARGE",
    "Order exceeds the customer's limit.",
    asyncParameters: tenantSettings);

await asyncRule.CheckAsync(context, cancellationToken);
```

An async rule resolves all its parameters before it evaluates the text, so each async provider is awaited once per evaluation.

**Errors.** Each failure has its own exception type:

| Exception | Thrown when | Useful members |
|-----------|-------------|----------------|
| `RuleExpressionParseException` | The text is malformed, exceeds a safety limit, or `Compile<T>` meets an unknown `Data` member | `Line`, `Column` (1-based) |
| `RuleExpressionBindingException` | A name, parameter or property cannot be bound during evaluation | `Identifier`, `AvailableKeys` (sorted) |
| `RuleExpressionEvaluationException` | An operator cannot handle its operands, the result is not a boolean, or decimal arithmetic divides by zero or overflows | `NodeText` |

```csharp
// RuleExpression.Parse("amount >") throws RuleExpressionParseException with Line 1, Column 9.
// A typo in a context key throws at evaluation:
// RuleExpressionBindingException: 'amuont' could not be bound; available: amount, customer, maxOrderAmount.

// row is a rule definition loaded from your database
if (!RuleExpression.TryParse(row.Condition, RuleExpressionOptions.Default, out RuleExpression? parsed))
{
    // reject the definition
}
```

`TryParse` returns `false` instead of throwing a parse error. Binding errors cannot be caught at parse time, because the parser does not know your context. To check names up front, read the parsed expression's `Identifiers` (the root names) and `ParameterNames` (without the `@`).

**Advanced: condition compiler and compiled delegates.** Most applications need neither of these. Use the condition compiler when you register it as a service, and `Compile<T>` when you want a typed delegate.

*Rules loaded from configuration.* `RuleExpressionConditionCompiler` plugs text into the plain `When(compiler, text)` builder overload. This is handy when you register the compiler in a container:

```csharp
// row is a rule definition loaded from your database; parameters is an IRuleParameterProvider
RuleExpressionConditionCompiler compiler = new RuleExpressionConditionCompiler(RuleExpressionOptions.Default, parameters);

IBusinessRule fromConfig = BusinessRuleBuilder.Create(row.Code)
    .WithMessage(row.Message)
    .When(compiler, row.Condition)
    .Build();
```

The result is an ordinary built rule, not an `ExpressionBusinessRule`, so it has no `ExplainExpression`. Its `@parameters` come from the compiler's providers, not from `WithParameter` on the builder. Prefer `WhenExpression` unless you need the compiler as a service. `When(compiler, text)` also compiles the text when you call it, so bad text throws `RuleExpressionParseException` while you build the rule, not when it first runs.

*Typed delegates with `Compile<T>`.* When the subject has a fixed type, `Compile<T>()` turns the expression into a delegate over `BusinessRuleContext<T>`. Paths that start with `Data` are checked against `T` when you compile, so a typo fails early:

```csharp
// order comes from your application
Func<BusinessRuleContext<Order>, bool> hasNoItems = RuleExpression.Parse("Data.Items.Count == 0").Compile<Order>();

bool isEmpty = hasNoItems(new BusinessRuleContext<Order>(order));

// Throws RuleExpressionParseException from Compile<Order>(), because Order has no Itemz property:
// RuleExpression.Parse("Data.Itemz.Count == 0").Compile<Order>();
```

- Compile once and keep the delegate.
- The delegate returns the same verdict as `Evaluate`. Other names and `@parameters` still bind at evaluation time.
- `Data` paths stay null-safe in the compiled delegate: a `null` anywhere in the path gives `null`, as in `Evaluate`.
- Pass a provider for `@parameters` with `Compile<Order>(parameters)`.
- `T` must be a reference type.
- The delegate gives no explanation. Use `Explain` for that.

> **Note:** `Compile<T>()` builds an expression tree, so it needs dynamic code. Under native AOT it falls back to the slower interpreted form. Everything else in this package emits no dynamic code, but it reads properties through reflection, so keep the members your expressions read from being trimmed.

### Expression syntax

The table lists constructs from the loosest binding to the tightest.

| Construct | Syntax | Notes |
|-----------|--------|-------|
| Or | `a \|\| b`, `a or b` | Short-circuits; both sides must be booleans |
| And | `a && b`, `a and b` | Short-circuits; both sides must be booleans |
| Not | `!a`, `not a` | The operand must be a boolean |
| Comparison | `==` `!=` `~=` `<` `<=` `>` `>=` | `~=` is case-insensitive equality; comparisons do not chain |
| Membership | `x in [a, b, c]`, `x in path` | List items can be any expression; items are compared with `==` |
| Additive | `a + b`, `a - b` | `+` also joins two strings |
| Multiplicative | `a * b`, `a / b`, `a % b` | |
| Unary minus | `-a` | |
| Member access | `customer.Address.City`, `items.Count`, `name.Length` | A `null` segment gives `null` |
| Functions | `name.startsWith('A')`, `name.endsWith('son')`, `tags.contains('rush')` | Add `/ci` for case-insensitive: `name.startsWith/ci('a')`; `contains` works on strings and collections |
| Literals | `12000`, `12.5`, `'VIP'`, `"VIP"`, `true`, `false`, `null` | Numbers are decimals with `.` as the separator and no exponent; strings accept `\'` `\"` `\\` `\n` `\r` `\t` |
| Names | `amount`, `Data`, `@maxOrderAmount` | Context entry, typed data, parameter |
| Grouping | `( ... )` | |

- Keywords (`and`, `or`, `not`, `in`, `true`, `false`, `null`) and function names are case-insensitive.
- Context keys and `Data` are case-sensitive. Property names match exactly first, then ignoring case when only one property matches.
- The whole expression must produce a boolean. Anything else throws `RuleExpressionEvaluationException`.
- The only functions are `startsWith`, `endsWith` and `contains`. You cannot call other methods.

> **Note:** `not` and `!` bind more loosely than comparisons, unlike in C#. `!a == b` means `!(a == b)`. Add parentheses when you mean `(!a) == b`.

### How values are compared

| Operands | Behaviour |
|----------|-----------|
| Two numbers (any numeric type, literals included) | Compared as `decimal`, or as `double` when either side is `float` or `double` |
| Decimal division by zero or overflow | Throws `RuleExpressionEvaluationException` |
| `NaN` (`double` or `float`) | Every ordering and `==` is false, and `!=` is true. So `x != x` detects `NaN`, and `score < threshold` never fires for it |
| Enum and string | Equal when the string is the member name, ignoring case (`tier == 'gold'`). Ordering an enum against a string throws |
| Enum and number | Compared and ordered by the underlying value (`tier > 1`) |
| Two enums | Same type: compared by value. Different types: never equal, and ordering throws |
| Two strings | Ordinal for `==`, `!=` and ordering. `~=` and the `/ci` functions ignore case |
| `null` | `null == null` is true and `null != x` is true. Ordering with a `null` side is false. Arithmetic with `null` gives `null`. A function with a `null` target or argument is false. `x in null` is false |
| `in` | Each item is compared with the `==` rules above, so `2.0 in [1, 2]` is true and `tier in ['vip', 'gold']` matches by name. The right side must be a list or collection; a string throws |
| `Count` / `Length` | A readable property with that name wins. Otherwise `Length` and `Count` work on strings, and `Count` on any `ICollection` |
| Other values | Same-type `IComparable` values (`DateTime`, `Guid`, ...) are ordered with `CompareTo`. Values of unrelated types are never equal; ordering or arithmetic across them throws |

### Safety limits

Expressions often come from people outside your codebase. `RuleExpressionOptions` limits what a parsed expression can do. Pass the options to `Parse`, `WhenExpression(text, options)` or the `RuleExpressionConditionCompiler` constructor.

```csharp
using Masterly.BusinessRules.Expressions;

// row is a rule definition loaded from your database
RuleExpressionOptions options = RuleExpressionOptions.Default
    .WithMaxLength(500)
    .WithMaxDepth(16)
    .WithRequireRuleAccessibleAttribute(true);

RuleExpression condition = RuleExpression.Parse(row.Condition, options);

public sealed class Customer
{
    [RuleAccessible]
    public CustomerTier Tier { get; set; }

    public string Ssn { get; set; } = string.Empty;   // not readable from an expression
}
```

| Option | Default | Effect |
|--------|---------|--------|
| `WithMaxLength(n)` | 4096 characters | Longer text throws `RuleExpressionParseException` |
| `WithMaxDepth(n)` | 64 levels | Deeper nesting throws `RuleExpressionParseException` |
| `WithMemberFilter(filter)` | none | A `Func<PropertyInfo, bool>` that must return `true` for every property an expression reads |
| `WithRequireRuleAccessibleAttribute(true)` | `false` | Only properties marked `[RuleAccessible]`, or declared on a type marked `[RuleAccessible]`, can be read |
| `WithCulture(culture)` | invariant culture | Formats values in explanations |

- Options are immutable. Each `With...` method returns a new instance.
- `WithMaxLength` and `WithMaxDepth` throw `ArgumentOutOfRangeException` for values below 1.
- When you set both a member filter and `RequireRuleAccessibleAttribute`, a property must pass both.
- Reading a blocked property throws `RuleExpressionBindingException`, which lists the properties that are readable.

Some limits always apply, whatever the options. An expression can read only public, non-static properties without indexer parameters. It cannot read fields, call methods other than the three built-in functions, or change any value.

---

## Logging, Tracing and Metrics

You can watch every rule a checker evaluates. Write your own observer for custom needs such as an audit trail. Or install `Masterly.BusinessRules.Diagnostics` to get ready-made logging, OpenTelemetry tracing and metrics.

### Observers

An observer receives callbacks while `BusinessRuleChecker` or `AsyncBusinessRuleChecker` evaluates a batch. Use one when you want to react to each rule's outcome without changing the rules themselves.

```csharp
// IAuditLog is your own service.
public sealed class AuditObserver : IRuleExecutionObserver
{
    private readonly IAuditLog _auditLog;

    public AuditObserver(IAuditLog auditLog)
    {
        _auditLog = auditLog;
    }

    public void OnBeforeEvaluate(IBusinessRule rule)
    {
    }

    public void OnAfterEvaluate(IBusinessRule rule, BusinessRuleResult? result)
    {
    }

    public void OnRuleBroken(IBusinessRule rule, BusinessRuleResult result)
    {
        _auditLog.Write($"{result.Code}: {result.Message}");
    }

    // Optional hook: also receives the time the checker measured.
    public void OnAfterEvaluate(IBusinessRule rule, bool isBroken, TimeSpan elapsed)
    {
        _auditLog.Write($"{rule.Code} took {elapsed.TotalMilliseconds} ms");
    }
}

// rules, context and auditLog come from your application
RuleEvaluationOptions options = new RuleEvaluationOptions { Observer = new AuditObserver(auditLog) };
RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rules, context, options);
```

You must implement `OnBeforeEvaluate`, `OnAfterEvaluate(rule, result)` and `OnRuleBroken`. Every other hook has an empty default implementation, so you override only the ones you need.

| Hook | Required | When it fires |
|------|----------|---------------|
| `OnBeforeEvaluate(rule)` | Yes | Before each rule. |
| `OnAfterEvaluate(rule, result)` | Yes | After a rule that did not throw. `result` is `null` when the rule passed. |
| `OnRuleBroken(rule, result)` | Yes | After the two `OnAfterEvaluate` calls, only when the rule is broken. |
| `OnBatchStarted(ruleCount)` | No | Once, before the first rule. |
| `OnAfterEvaluate(rule, isBroken, elapsed)` | No | Right after `OnAfterEvaluate(rule, result)`, with the measured duration. |
| `OnRuleFaulted(rule, exception)` | No | When a rule throws, before the fault policy is applied. Replaces both `OnAfterEvaluate` calls. |
| `OnRuleCancelled(rule, exception)` | No | When a rule throws `OperationCanceledException`. Nothing else fires for that rule. |
| `OnShadowViolation(rule, result)` | No | When a shadow-mode feature-gated rule found a violation it did not report as broken. |
| `OnShadowFault(rule, exception)` | No | After `OnShadowViolation`, when the shadow-mode rule threw. The exception does not propagate. |
| `OnBatchCompleted(result)` | No | Once, at the end, with the result and its ledger. Fires before `CheckAll` throws its broken rules. |
| `OnBatchAborted(exception)` | No | Once, instead of `OnBatchCompleted`, when the batch ends by an exception (a cancellation, a fault under `FaultPolicy.Throw`, or a `null` rule). |

A few rules about the order:

- A faulted rule gets `OnRuleFaulted`. Under `FaultPolicy.TreatAsBroken` it then also gets `OnRuleBroken`.
- Cancellation is never reported as a fault.
- Every batch that raised `OnBatchStarted` ends with exactly one of `OnBatchCompleted` or `OnBatchAborted`. If you open a scope or a span per batch, you can always close it.

For async rules, implement `IAsyncRuleExecutionObserver`. It has the same hooks with an `Async` suffix and a `CancellationToken`, for example `OnRuleBrokenAsync(rule, result, cancellationToken)`. `OnBatchCompletedAsync` receives an `AsyncRuleEvaluationResult`.

You attach an observer in one of two ways:

- Set `Observer` on `RuleEvaluationOptions` or `AsyncRuleEvaluationOptions`, then call any overload that takes options.
- Pass the `observer` parameter of `CheckAll` / `CheckAllAsync`, for example `BusinessRuleChecker.CheckAll(rules, observer: new AuditObserver(auditLog))`.

> **Note:** `EvaluateAll` / `EvaluateAllAsync` call observers only through their options-taking overloads. The overloads without options have no observer.

> **Note:** The async checker passes `CancellationToken.None` to `OnRuleCancelledAsync` and `OnBatchAbortedAsync`, so your cleanup code still runs after the caller's token was cancelled.

### Structured logging

`LoggingRuleExecutionObserver` writes one structured `ILogger` line per rule. Use it when you want rule outcomes in your application logs without writing an observer.

```bash
dotnet add package Masterly.BusinessRules.Diagnostics
```

```csharp
using Masterly.BusinessRules;
using Masterly.BusinessRules.Diagnostics;

// logger is an ILogger (for example ILogger<CheckoutService>); rules and context come from your application
LoggingRuleExecutionObserver logging = new LoggingRuleExecutionObserver(logger);

BusinessRuleChecker.CheckAll(rules, context, new RuleEvaluationOptions { Observer = logging });
// warn: Business rule ORDER.NO_ITEMS (OrderMustHaveItemsRule) is broken: An order must contain at least one item.
//       [Category= Severity=Error Outcome=broken ElapsedMs=0.031]
```

For async batches, use `LoggingAsyncRuleExecutionObserver`. It takes the same logger and options.

Each rule line carries these fields: `RuleCode`, `RuleName`, `Category`, `Severity`, `Outcome` and `ElapsedMs`. A broken rule also carries `Message`.

The defaults are conservative, because an expected validation failure is not an application error:

| What happened | Default level | Option |
|---------------|---------------|--------|
| Broken rule, severity `Info` | `Debug` | `BrokenLevelBySeverity` |
| Broken rule, severity `Warning` | `Information` | `BrokenLevelBySeverity` |
| Broken rule, severity `Error` | `Warning` | `BrokenLevelBySeverity` |
| Broken rule, severity not in the map | `Warning` | `DefaultBrokenLevel` |
| Rule threw (exception attached) | `Error` | `FaultedLevel` |
| Rule passed | Not logged | `LogPassedRules` (default `false`), `PassedLevel` (default `Debug`) |
| Batch started and completed | `Debug` | `LogBatches` (default `true`), `BatchLevel` |
| Shadow-mode violation | `Information` | `ShadowViolationLevel` |

Change the defaults with `LoggingRuleObserverOptions`:

```csharp
// logger is the ILogger from the previous example
LoggingRuleObserverOptions options = new LoggingRuleObserverOptions
{
    LogPassedRules = true,
    IncludeMessage = false,
    BrokenLevelBySeverity =
    {
        [RuleSeverity.Info] = LogLevel.None,      // silence Info rules
        [RuleSeverity.Error] = LogLevel.Error
    }
};

LoggingRuleExecutionObserver logging = new LoggingRuleExecutionObserver(logger, options);
```

- Map a severity to `LogLevel.None` to silence it.
- Cancelled rules and aborted batches are not logged.
- Each message has a fixed event id in `RuleLogEvents`, so you can filter or alert on it: `RuleBroken` (1), `RulePassed` (2), `RuleFaulted` (3), `BatchStarted` (4), `BatchCompleted` (5), `ShadowViolation` (6).

> **Note:** A rule `Message` can contain domain data, such as a customer id or an amount. Set `IncludeMessage = false` to write `[redacted]` in its place.

### OpenTelemetry tracing and metrics

`TracingRuleExecutionObserver` turns each batch into a span with one child span per rule, and records metrics at the same time. Use `TracingAsyncRuleExecutionObserver` for async batches. Use them when you export telemetry with OpenTelemetry, or with any other `ActivityListener` / `MeterListener`.

Both the `ActivitySource` and the `Meter` are named `Masterly.BusinessRules`. Subscribe to them with plain OpenTelemetry calls; this package needs no OpenTelemetry extension of its own:

```csharp
// Program.cs, using the OpenTelemetry.Extensions.Hosting package and your exporters;
// builder is the WebApplicationBuilder in Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(BusinessRulesActivitySource.Name))
    .WithMetrics(metrics => metrics.AddMeter(BusinessRulesMetrics.MeterName));
```

Then attach the observer to a batch:

```csharp
// rules, context and cancellationToken come from your application
TracingAsyncRuleExecutionObserver tracing = new TracingAsyncRuleExecutionObserver();

AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(
    context, rules, new AsyncRuleEvaluationOptions { Observer = tracing }, cancellationToken);

// BusinessRules.Batch
//   ├─ ORDER.NO_ITEMS          business_rule.outcome=broken
//   ├─ CUSTOMER.ACTIVE         business_rule.outcome=passed
//   └─ BusinessRule.Evaluate   business_rule.outcome=passed   (a combined rule: no code)
```

The observers keep no per-rule state. One instance can serve many concurrent batches, so you can share a single instance.

**Spans**

| Span | Created | Attributes |
|------|---------|------------|
| `BusinessRules.Batch` | Once per checker batch. | `business_rules.batch.rule_count`, `business_rules.batch.broken_count`, `business_rules.batch.faulted_count`, `business_rules.batch.outcome` |
| `BusinessRule.Evaluate` | Once per rule, as a child of the batch span. Its display name is the rule code. | `business_rule.code`, `business_rule.name`, `business_rule.category`, `business_rule.severity`, `business_rule.outcome` |

- `business_rule.outcome` is `passed`, `broken`, `faulted` or `cancelled`.
- `business_rules.batch.outcome` is `faulted` when any rule threw, otherwise `broken` when any rule is broken, otherwise `passed`. An aborted batch gets `cancelled` or `faulted`.
- A rule that threw gets an error status and an `exception` event (`exception.type`, `exception.message`). A batch with faulted rules, or one aborted by a fault, gets an error status too.
- A shadow-mode violation becomes a `business_rule.shadow_violation` event on the batch span.
- Every span is closed, even when a rule is cancelled or the batch ends by an exception.

The rule span is `Activity.Current` while the rule runs. Anything the rule starts itself, such as an `HttpClient` call or a database query, becomes a child of that rule's span. This holds for sequential and parallel batches.

**Metrics**

| Instrument | Type | Unit | Tags |
|------------|------|------|------|
| `business_rules.evaluations` | Counter | | `code` (see the note below), `category`, `severity`, `outcome` |
| `business_rules.evaluation.duration` | Histogram | `ms` | `outcome` |
| `business_rules.batches` | Counter | | `outcome` |

- `severity` is `info`, `warning` or `error`.
- A shadow violation adds one extra evaluation with `outcome=shadow`, next to the gated rule's own `passed` evaluation.
- Cancelled rules and cancelled batches are not counted.
- A batch aborted by a fault is counted with `outcome=faulted`.

> **Note:** By default, combined rules (`And`, `Or`, `Not`, groups, composites) get no `code` tag, and their span keeps the name `BusinessRule.Evaluate`. Their generated codes would otherwise create a new metric series for every combination. Wrapped rules (cached, gated and so on) keep the code of the rule they wrap.

**Options**

Configure the observers with `TracingRuleObserverOptions`:

| Option | Default | Effect |
|--------|---------|--------|
| `CodeTagSelector` | `LeafCodeTagSelector` | Returns the `code` tag for a rule, or `null` for no tag. If your selector throws, the rule gets no tag and the batch continues. |
| `MessageAsEvent` | `false` | Adds a `business_rule.broken` event with the rule message to each broken rule's span. Off by default because messages can carry domain data. |
| `RecordMetrics` | `true` | Set to `false` to record spans only. |
| `Metrics` | `BusinessRulesMetrics.Default` | The instruments to record on. |

To record on a meter you own, for example one from `IMeterFactory` or a test meter, pass your own `BusinessRulesMetrics`:

```csharp
// meterFactory is the IMeterFactory from your container
BusinessRulesMetrics metrics = new BusinessRulesMetrics(meterFactory.Create(BusinessRulesMetrics.MeterName));

TracingRuleExecutionObserver tracing = new TracingRuleExecutionObserver(new TracingRuleObserverOptions { Metrics = metrics });
```

If you give your meter a different name, add that name to `AddMeter`.

**Tracing a single rule with `Traced()`**

Observers only see checker batches. A domain aggregate often calls `rule.Check()` directly. Wrap such a rule with `Traced()` to get the same span and measurements:

```csharp
// order and logger come from your application
IBusinessRule rule = new OrderMustHaveItemsRule(order).Traced();          // span + metrics
IBusinessRule logged = new OrderMustHaveItemsRule(order).Traced(logger);  // span + metrics + log line

rule.Check();
```

- Sync rules record every `IsBroken` and `Check` call. Async rules record `IsBrokenAsync`, `EvaluateAsync` and `CheckAsync`.
- The sync `Evaluate()` records nothing. It stays the plain result factory it is on every sync rule.
- `Check()` still throws the inner rule's `BusinessRuleValidationException`. The span records the rule as broken.
- A cancelled call closes the span with `outcome=cancelled`. It is not counted or logged.
- Both overloads accept `TracingRuleObserverOptions`. `Traced(logger, ...)` also accepts `LoggingRuleObserverOptions`.
- `Traced()` keeps the inner rule's metadata, verdict and, for a group or other combined rule, the member failures in `Details`. In `Explain()` it appears as a `TRACED` node with the inner rule's tree beneath it.
- Bind a subject-typed rule first: `new OrderItemsRequiredRule().For(order).Traced()`.

> **Note:** Use the observer for batches and `Traced()` for single-rule calls. A traced rule inside a traced batch produces a nested span and is counted twice.

> **Note:** A faulted rule gets a duration sample only when something listens to the `Masterly.BusinessRules` activity source.

---

## Dependency Injection

`Masterly.BusinessRules.DependencyInjection` registers your rules in `Microsoft.Extensions.DependencyInjection`. Use it when you want the container to find your rules by assembly scanning and inject them into your handlers as one runner.

```bash
dotnet add package Masterly.BusinessRules.DependencyInjection
```

```csharp
using Masterly.BusinessRules;
using Masterly.BusinessRules.DependencyInjection;

// Program.cs (builder is your WebApplicationBuilder): register every subject-typed rule in the assembly,
// such as OrderItemsRequiredRule and CustomerEmailMustBeUniqueRule from "Subject-Typed Rules"
builder.Services.AddBusinessRules(typeof(OrderItemsRequiredRule).Assembly);

// The handler never lists rules itself.
public sealed class PlaceOrderHandler
{
    private readonly IRuleRunner<Order> _rules;

    public PlaceOrderHandler(IRuleRunner<Order> rules)
    {
        _rules = rules;
    }

    public Task HandleAsync(Order order, CancellationToken cancellationToken)
    {
        return _rules.CheckAllAsync(order, cancellationToken);   // runs every Order rule
    }
}
```

Scanned rules can take services through their constructor, as `CustomerEmailMustBeUniqueRule` does.

**What scanning registers**

- Every concrete, closed class that implements `IBusinessRule<T>` or `IAsyncBusinessRule<T>`, under each of those interfaces it implements.
- `IRuleRunner<T>` for every subject type, with the same lifetime as the rules.
- `IRuleRegistry`, as a singleton.

Scanning skips abstract classes and open generics. Registration order is the same on every run, so rules always run in the same order. Scanning the same assembly twice registers each rule once. If you registered your own `IRuleRunner<>` or `IRuleRegistry` before calling `AddBusinessRules`, yours is kept.

**Options**

Pass a callback to configure the scan:

```csharp
// builder is the WebApplicationBuilder in Program.cs
builder.Services.AddBusinessRules(options =>
{
    options.AddAssemblyContaining<OrderItemsRequiredRule>()
           .AddAssembly(typeof(PaymentRules).Assembly);
    options.TypeFilter = type => type.Namespace != "MyShop.Rules.Experimental";
});
```

| Option | Default | Effect |
|--------|---------|--------|
| `AddAssembly`, `AddAssemblies`, `AddAssemblyContaining<T>()` | none | The assemblies to scan. |
| `Lifetime` | `Scoped` | Lifetime of the rules and of `IRuleRunner<T>`. `Scoped` works with dependencies of any lifetime. Use `Singleton` only when every dependency is a singleton. |
| `IncludeNonPublicTypes` | `false` | Also scan internal and other non-public classes. |
| `IncludeUntypedRules` | `false` | Also register classes that implement the plain `IBusinessRule` / `IAsyncBusinessRule`. |
| `TypeFilter` | `null` | Return `false` to skip a type. |
| `AddRuleSet(...)` | none | Adds a layer to a named rule set in `IRuleRegistry` (see below). |

> **Note:** Leave `IncludeUntypedRules` off unless your plain rules take only injectable services. A typical plain rule receives its subject in the constructor (`new OrderMustHaveItemsRule(order)` from the Quick Start), which the container cannot create. Registering it fails at startup under `ValidateOnBuild` (on by default in ASP.NET Core Development), or on the first resolution of `IEnumerable<IBusinessRule>`. If you turn it on, use `TypeFilter` to exclude such rules.

**Running rules with `IRuleRunner<T>`**

`IRuleRunner<T>` holds every rule registered for subject type `T`, in registration order: `Rules` holds the sync rules and `AsyncRules` the async ones. It runs them through the normal checkers, so the ledger, fault policy, `ThrowOn` and observers behave exactly as with a direct checker call.

| Method | Runs | Notes |
|--------|------|-------|
| `CheckAllAsync`, `EvaluateAllAsync` | Sync and async rules | The sync rules run first, then the async rules. |
| `CheckAll`, `EvaluateAll` | Sync rules only | Throws `InvalidOperationException`, naming the codes, when async rules are registered for `T`. |

Each method has overloads that take a `BusinessRuleContext` and evaluation options:

```csharp
// runner, order, context and cancellationToken come from your application
AsyncRuleEvaluationOptions options = new AsyncRuleEvaluationOptions { ThrowOn = RuleSeverity.Error };
await runner.CheckAllAsync(order, context, options, cancellationToken);

if (!runner.HasAsyncRules)
{
    RuleEvaluationResult result = runner.EvaluateAll(order, context);
}
```

> **Note:** A synchronous batch cannot run async rules without blocking, so `CheckAll` and `EvaluateAll` throw instead of silently skipping them. Check `HasAsyncRules` if you are not sure.

In tests or in code without a container, create the runner directly from two rule collections: `new RuleRunner<Order>(syncRules, asyncRules)`.

**Named rule sets with `AddRuleSet`**

A rule set is a named list of rule factories in `IRuleRegistry`. Use one when rules need values from the context, or when some tenants need different rules. Each `AddRuleSet` call adds a layer for a scope. A layer for a more specific scope can disable, replace or add rules; [Rule registries](#rule-registries) explains how layers merge.

```csharp
using Masterly.BusinessRules;
using Masterly.BusinessRules.DependencyInjection;
using Microsoft.Extensions.DependencyInjection; // for GetRequiredService

// builder is the WebApplicationBuilder in Program.cs; OrderKeys.Order is a ContextKey<Order>;
// ISanctionsApi is registered as a singleton
builder.Services.AddBusinessRules(options => options
    .AddRuleSet("checkout", set => set
        .Add(context => new CartMustHaveItemsRule(context.Get(OrderKeys.Order))))
    .AddRuleSet("checkout", (services, set) => set
        .AddAsyncRule(context => new SanctionsCheckRule(services.GetRequiredService<ISanctionsApi>())))
    .AddRuleSet("checkout", RuleScope.Tenant("acme"), set => set
        .Disable("CART.EMPTY")));

public sealed class CheckoutService
{
    private readonly IRuleRegistry _registry;

    public CheckoutService(IRuleRegistry registry)
    {
        _registry = registry;
    }

    public Task CheckoutAsync(Order order, string tenantId, CancellationToken cancellationToken)
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(OrderKeys.Order, order);

        return _registry.CheckAllAsync("checkout", RuleScope.Tenant(tenantId), context, cancellationToken);
    }
}
```

- The registry is built the first time it is resolved. The `(services, set)` overload runs at that moment and can resolve services.
- The registry's batch methods behave like `IRuleRunner<T>`: `CheckAllAsync` / `EvaluateAllAsync` run the whole set (sync rules first), while `CheckAll` / `EvaluateAll` throw `InvalidOperationException` when the resolved set contains async rules.
- Rule sets from several `AddBusinessRules` calls are combined into one registry.
- Errors, scope handling and the merge order are the same as without a container. See [Rule registries](#rule-registries).

> **Note:** `IRuleRegistry` is a singleton, so the `services` passed to `(services, set)` is the root provider. Resolve only singletons there. A scoped service resolved there lives as long as the application, or throws when scope validation is on.

---

## Testing Your Rules

The library gives you two levels of test support. `RuleTestHelper` in the core package covers everyday assertions. The optional `Masterly.BusinessRules.Testing` package adds rule coverage, boundary search, property-based checks and smoke-test mutants.

The examples in this section test these two rules:

```csharp
using Masterly.BusinessRules;

public class OrderTotalLimitRule : BaseBusinessRule
{
    private readonly decimal _orderTotal;

    public OrderTotalLimitRule(decimal orderTotal) => _orderTotal = orderTotal;

    public override string Code => "ORDER.TOTAL_LIMIT";
    public override string Message => "The order total must not exceed 10,000.";
    public override bool IsBroken() => _orderTotal > 10_000m;
}

public class CustomerMinimumAgeRule : BaseBusinessRule
{
    private readonly int _age;

    public CustomerMinimumAgeRule(int age) => _age = age;

    public override string Code => "CUSTOMER.MINIMUM_AGE";
    public override string Message => "The customer must be at least 18.";
    public override bool IsBroken() => _age < 18;
}
```

### Built-in test helpers

`RuleTestHelper` ships in the core package, so you need nothing extra to use it. Its assertions work with any test framework: a failed assertion throws `RuleAssertionException`, and your test runner reports that as a failed test.

```csharp
using Masterly.BusinessRules;
using Xunit;

public class OrderTotalLimitRuleTests
{
    [Fact]
    public void OrderTotalLimit_WhenTotalIsAboveLimit_IsBroken()
    {
        OrderTotalLimitRule rule = new OrderTotalLimitRule(orderTotal: 12_500m);

        RuleTestHelper.AssertBroken(rule);
    }

    [Fact]
    public void OrderTotalLimit_WhenTotalEqualsLimit_Passes()
    {
        OrderTotalLimitRule rule = new OrderTotalLimitRule(orderTotal: 10_000m);

        RuleTestHelper.AssertNotBroken(rule);
    }
}
```

**Assertions**

| Method | Succeeds when |
|--------|---------------|
| `AssertBroken(rule)`, `AssertBroken(rule, context)` | The rule is broken. |
| `AssertNotBroken(rule)`, `AssertNotBroken(rule, context)` | The rule passes. |
| `AssertBrokenAsync(rule, context, cancellationToken)` | The async rule is broken. |
| `AssertNotBrokenAsync(rule, context, cancellationToken)` | The async rule passes. |

- The async assertions return a `Task`, so `await` them.
- `context` and `cancellationToken` are optional on the async assertions. Without a context, they use an empty one.
- The failure message names the rule's `Code`. When the rule was expected to pass, it also includes the rule's `Message`.

**Factories**

| Method | Returns |
|--------|---------|
| `CreateContext(("key", value), ...)` | A `BusinessRuleContext` holding the string-keyed values. |
| `CreateContext((key, value), ...)` | A context holding one to four `ContextKey<T>` values. The value types can differ. |
| `CreateContext(data)` | A typed `BusinessRuleContext<T>` that wraps `data` (a class). |
| `CreateBrokenRule(code, message)`, `CreatePassingRule(code, message)` | A sync rule that is always broken, or always passes. |
| `CreateBrokenAsyncRule(code, message)`, `CreatePassingAsyncRule(code, message)` | The async equivalents. |
| `CreateFlagProvider(flag, mode, percent)` | An `InMemoryRuleFeatureFlagProvider` with one flag set. |
| `CreateParameterProvider(name, value, scope)` | An `InMemoryRuleParameterProvider` with one value set. |

The factory arguments have defaults:

- `code` and `message` are optional. The default codes are `"TEST_BROKEN"`, `"TEST_PASSING"`, `"TEST_BROKEN_ASYNC"` and `"TEST_PASSING_ASYNC"`.
- `percent` defaults to 100, so every caller is in the rollout. Flags you never set are `Off`.
- `scope` defaults to the global scope. Parameters you never set resolve to the fallback the rule declares.
- Both providers work with sync and async rules. Call their `Set` method to add more flags or values.

Use the stand-in rules when you test code that combines or runs rules. Use the contexts and providers when the rule reads data or configuration:

```csharp
// Stand-ins for a test that only cares how many rules fail
IBusinessRule blocked = RuleTestHelper.CreateBrokenRule("ORDER.BLOCKED", "The order is blocked.");
IBusinessRule approved = RuleTestHelper.CreatePassingRule();

// A rule that reads its input from the context
IBusinessRule creditLimitRule = BusinessRuleBuilder.Create("ORDER.CREDIT_LIMIT")
    .WithMessage("The order exceeds the customer's credit limit.")
    .When(context => context.Get<decimal>("orderTotal") > context.Get<decimal>("creditLimit"))
    .Build();

BusinessRuleContext context = RuleTestHelper.CreateContext(("orderTotal", 800m), ("creditLimit", 500m));
RuleTestHelper.AssertBroken(creditLimitRule, context);

// A feature-gated rule whose flag is off is skipped
InMemoryRuleFeatureFlagProvider flags =
    RuleTestHelper.CreateFlagProvider("strict-credit-check", RuleRolloutMode.Off);
IBusinessRule gatedRule = creditLimitRule.GatedBy("strict-credit-check", flags);
RuleTestHelper.AssertNotBroken(gatedRule, context);

// A rule with a configurable limit: the provider overrides the fallback of 10,000
InMemoryRuleParameterProvider limits = RuleTestHelper.CreateParameterProvider("maxOrderTotal", 500m);
IBusinessRule configurableLimitRule = BusinessRuleBuilder.Create("ORDER.CONFIGURABLE_LIMIT")
    .WithMessage("The order total exceeds the configured limit.")
    .WithParameter("maxOrderTotal", 10_000m)
    .WithParameters(limits)
    .When((ctx, parameters) => ctx.Get<decimal>("orderTotal") > parameters.Get<decimal>("maxOrderTotal"))
    .Build();
RuleTestHelper.AssertBroken(configurableLimitRule, context);
```

> **Note:** Pass a context when you test a sync feature-gated rule. Without a context, the rule ignores its flag and always evaluates the inner rule. See [Gating a rule](#gating-a-rule).

### Rule coverage

Rule coverage tells you which rules your tests have seen both broken and passing. Use it to find rules that your suite only exercises on one path, such as a limit that no test ever exceeds.

Install the testing package in your test project:

```bash
dotnet add package Masterly.BusinessRules.Testing
```

The package targets `net8.0` and `net10.0`, so your test project must target .NET 8 or later. It depends on [CsCheck](https://github.com/AnthonyLloyd/CsCheck): version 4.9.1 when your test project targets .NET 8 or 9, and 5.0.0 when it targets .NET 10 or later. All its types live in the `Masterly.BusinessRules.Testing` namespace.

```csharp
using System;
using Masterly.BusinessRules;
using Masterly.BusinessRules.Testing;

using RuleCoverageSession coverage = RuleCoverage.Begin();

IBusinessRule[] CheckoutRules(decimal orderTotal, int customerAge) => new[]
{
    new OrderTotalLimitRule(orderTotal).WithCoverage(coverage),
    new CustomerMinimumAgeRule(customerAge).WithCoverage(coverage),
};

BusinessRuleChecker.EvaluateAll(CheckoutRules(orderTotal: 12_500m, customerAge: 30)); // limit broken
BusinessRuleChecker.EvaluateAll(CheckoutRules(orderTotal: 900m, customerAge: 42));    // both pass

RuleCoverageReport report = coverage.Report();
Console.WriteLine(report.ToText());
// CUSTOMER.MINIMUM_AGE  broken ✘  passed ✔   <- never exercised on the broken path
// ORDER.TOTAL_LIMIT     broken ✔  passed ✔
// outcome coverage 75% (3 of 4 outcomes, 1 of 2 rules fully covered)

report.AssertMinimum(1.0); // throws RuleAssertionException: Outcome coverage 75% is below the required 100%. Never broken: CUSTOMER.MINIMUM_AGE.
```

**How outcomes are recorded**

`.WithCoverage(session)` wraps a sync or async rule. The wrapper keeps the rule's verdict, `Code`, `Message` and other metadata. Each evaluation records one outcome, broken or passed, under the rule's `Code`.

- Sync rules record from `IsBroken` and `Check`, with or without a context. They also record when a checker, group or combined rule evaluates them.
- `Evaluate()` on a sync rule only builds a result object, so it records nothing.
- Async rules record from `IsBrokenAsync`, `EvaluateAsync` and `CheckAsync`.
- An evaluation that throws or is cancelled records nothing.
- Wrapping a rule registers its code. A rule your tests never reach still appears in the report, as "never evaluated".
- Rules that share a `Code` share one entry.
- In `Explain()` the wrapper appears as a `COVERAGE` node with the rule's own tree beneath it. Each explain call records one outcome.

> **Note:** Wrap each operand before you combine rules with `And`, `Or` or a group. A combined rule can skip operands once its result is known. Only wrapped operands show up in the report as skipped.

**Reading the report**

`session.Report()` returns a snapshot. Later evaluations do not change a report you already took.

| Member | What it tells you |
|--------|-------------------|
| `Entries` | One entry per rule code, sorted by code, with `BrokenCount`, `PassedCount` and `IsFullyCovered`. |
| `OutcomeCoverage` | Observed outcomes divided by two outcomes per rule, from 0.0 to 1.0. An empty report is 0.0. |
| `IsComplete` | `true` when every rule was seen both broken and passing. |
| `NeverBroken`, `NeverPassed`, `NeverEvaluated` | The codes missing each outcome. |
| `Find(code)` | The entry for one code, or `null`. |
| `ToText()` | A printable table, as shown above. |
| `AssertMinimum(outcomeCoverage)` | Throws `RuleAssertionException` listing the uncovered rules when coverage is below the minimum. |

`AssertMinimum` takes a value from 0.0 to 1.0. Any other value throws `ArgumentOutOfRangeException`.

**Session lifetime**

- Recording is thread-safe, so tests that run in parallel can share one session.
- `Reset()` clears every entry, including rules that were wrapped but never evaluated.
- Disposing the session stops recording. Reports you already took stay readable.

> **Advanced:** To record outcomes from your own wrapper, call `session.Register(rule)` and `session.Record(rule, isBroken)` directly.

### Finding a rule's boundary

The boundary finder searches a numeric range for the exact value where a rule flips between passing and broken. Use it to pin down a limit, such as "orders above 10,000 are rejected", to the smallest unit you care about.

```csharp
using Masterly.BusinessRules.Testing;
using Xunit;

RuleBoundary<decimal> boundary = RuleBoundaryFinder.Find<decimal>(
    orderTotal => new OrderTotalLimitRule(orderTotal),
    from: 0m,
    to: 1_000_000m,
    step: 0.01m);

Assert.Equal(10_000.00m, boundary.LastPassing);
Assert.Equal(10_000.01m, boundary.FirstBroken);
Assert.Equal(Monotonicity.BrokenAbove, boundary.Monotonicity);
// boundary.ToString(): "passes up to 10000.00, breaks from 10000.01 (61 probes)"
```

**How the search works**

- You pass a factory that builds one rule for one input value. The finder takes a factory because rules usually receive their data through the constructor.
- The finder searches the values `from`, `from + step`, `from + 2 × step`, and so on up to `to`. It builds only a few dozen rules, even for a large range.
- `step` is required. It sets the precision of the answer: `LastPassing` and `FirstBroken` are always exactly one step apart.
- The input can be any number type, such as `int`, `long`, `decimal` or `double`.

**The result**

| Member | Meaning |
|--------|---------|
| `LastPassing` | The passing value next to the boundary. |
| `FirstBroken` | The broken value next to the boundary. |
| `Monotonicity` | `BrokenAbove` when larger values break (a maximum). `BrokenBelow` when smaller values break (a minimum). |
| `ProbeCount` | How many rules the finder built and evaluated. |
| `Step` | The step you passed. |

For the minimum-age rule, `Find<int>(age => new CustomerMinimumAgeRule(age), 0, 120, 1)` returns `BrokenBelow`, with `LastPassing` 18 and `FirstBroken` 17.

**Sync and async**

- `Find` calls the parameterless `IsBroken()`. The rule must get its input from the factory, not from a context.
- `FindAsync(factory, from, to, step, cancellationToken)` calls `IsBrokenAsync` with an empty context. It checks the token before every probe and throws `OperationCanceledException` when the token is cancelled.

**What throws**

| Exception | When |
|-----------|------|
| `RuleAssertionException` | The rule has the same verdict at `from` and `to`, so the range holds no boundary. |
| `RuleAssertionException` | The rule flips more than once in the range. The message names the two values that contradict each other. |
| `ArgumentException` or `ArgumentOutOfRangeException` | `from` is not below `to`, a value is not finite, the step is zero or negative, or the step is larger than the range or too fine for it. |
| `InvalidOperationException` | The factory returned `null`. |

> **Note:** The finder samples the range to confirm that the rule flips only once. A rule that misbehaves only in a narrow band can still slip through.

### Property-based checks

Property checks generate many random inputs and verify that a rule obeys a law for every one of them. Use them to catch inputs you would not think to write by hand. The inputs come from a [CsCheck](https://github.com/AnthonyLloyd/CsCheck) generator.

```csharp
using CsCheck;
using Masterly.BusinessRules.Testing;

RuleProperties.For<decimal>(orderTotal => new OrderTotalLimitRule(orderTotal), Gen.Decimal[0.01m, 100_000m])
    .MonotoneIn(Monotonicity.BrokenAbove)
    .ContextPure()
    .Verify(iterations: 500, seed: 42);
```

**Laws**

| Law | What it checks |
|-----|----------------|
| `MonotoneIn(Monotonicity.BrokenAbove)` | For any two inputs `a ≤ b`: if the rule is broken at `a`, it is also broken at `b`. |
| `MonotoneIn(Monotonicity.BrokenBelow)` | For any two inputs `a ≤ b`: if the rule is broken at `b`, it is also broken at `a`. |
| `ContextPure()` | Evaluating the rule does not add, remove or replace a context entry. It starts from an empty context. |
| `ContextPure(contextFactory)` | The same check, starting from a context that your factory builds. |

- `MonotoneIn` needs inputs that can be compared. For other types, pass an `IComparer<T>` as the second argument. Without one, `MonotoneIn` throws `InvalidOperationException`.
- `ContextPure` compares the context's keys and stored values before and after. It cannot see a change made inside an object that is already stored in the context.
- On sync rules, `MonotoneIn` calls the parameterless `IsBroken()`, and `ContextPure` calls `IsBroken(context)`.
- Laws run in the order you add them. The first law that fails stops the run.

There are no "deterministic" or "idempotent" laws, because they do not hold for rules that call a repository or for cached rules.

**Running the checks**

- `Verify(iterations, seed)` runs the checks for a sync rule. `iterations` defaults to 100, and `seed` is optional.
- For an async rule, pass a factory that returns an `IAsyncBusinessRule`. Then call `VerifyAsync(iterations, seed, cancellationToken)`.
- The same seed always produces the same inputs and the same counter-example. Without a seed, a random one is chosen and printed in the failure message, so you can replay the run.
- When a law fails, CsCheck shrinks the input to a smaller counter-example before it is reported.

```csharp
using CsCheck;
using Masterly.BusinessRules;           // for ToAsync
using Masterly.BusinessRules.Testing;

// cancellationToken comes from your test framework
await RuleProperties.For<int>(age => new CustomerMinimumAgeRule(age).ToAsync(), Gen.Int[0, 120])
    .MonotoneIn(Monotonicity.BrokenBelow)
    .VerifyAsync(iterations: 500, seed: 42, cancellationToken: cancellationToken);
```

**What throws**

| Exception | When |
|-----------|------|
| `RuleAssertionException` | A law is violated. A rule that throws also counts as a violation. The message shows the counter-example, the seed, the shrink count and what went wrong. |
| `InvalidOperationException` | You called `Verify` before adding a law, the rule or context factory returned `null`, or the generator could not produce a value. |
| `ArgumentOutOfRangeException` | `iterations` is zero or negative. |
| `OperationCanceledException` | The token passed to `VerifyAsync` was cancelled. |

Only `RuleAssertionException` means the rule broke a law. The other exceptions point to a problem in the test setup.

> **Note:** On .NET 8 and 9 the package uses CsCheck 4.9.1, where `Gen.Decimal[start, finish]` fails when a bound is exactly `0m`. `Verify` reports this as `InvalidOperationException` ("Failing Where max count"). Use a bound such as `0.01m`, or build amounts from whole cents: `Gen.Long[0L, 10_000_000L].Select(cents => cents / 100m)`. CsCheck 5 (.NET 10) does not have this problem.

### Smoke-test mutants

A mutant is a copy of a rule that misbehaves in a known way. Run your assertions against a mutant: if they still pass, they do not really pin the rule down.

```csharp
using Masterly.BusinessRules;
using Masterly.BusinessRules.Testing;
using Xunit;

IBusinessRule rule = new OrderTotalLimitRule(orderTotal: 12_500m);

// The real rule satisfies the assertion
RuleTestHelper.AssertBroken(rule);

// The same assertion must catch each mutant
Assert.Throws<RuleAssertionException>(() => RuleTestHelper.AssertBroken(rule.AlwaysPass()));
Assert.Throws<RuleAssertionException>(() => RuleTestHelper.AssertBroken(rule.Invert()));
```

| Mutant | Behaviour |
|--------|-----------|
| `Invert()` | Broken exactly when the original rule passes. |
| `AlwaysPass()` | Never broken. It never evaluates the original rule. |
| `DowngradeSeverity()` | Same verdict, one severity level lower: `Error` becomes `Warning`, `Warning` becomes `Info`, and `Info` stays `Info`. |

- All three work on both `IBusinessRule` and `IAsyncBusinessRule`.
- Every mutant keeps the original rule's `Code`, `Message` and other metadata, so the code under test cannot tell it apart from the original.
- Use `DowngradeSeverity()` to check that tests which depend on severity notice the change. For example: "errors block checkout, but warnings do not".
- In `Explain()`, `Invert()` appears as an `INVERTED` node and `AlwaysPass()` as an `ALWAYS PASSING` node whose original rule is `NotEvaluated`. `DowngradeSeverity()` appears as a `SEVERITY` node.

> **Note:** Mutants are a smoke test, not mutation testing. They change a rule's verdict from the outside, so they cannot find bugs inside your `IsBroken` logic. For that, use a source-level mutation tool such as [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/).

---

## Real-World Examples

The examples below combine several features in the way an application usually does. Each example is a complete service or controller. The rule classes it uses (such as `CartMustNotBeEmptyRule`) and the services it injects are your own.

### E-commerce checkout

A checkout runs cheap in-memory checks first, then slower checks that call other services. Run the slow checks in parallel to cut latency.

```csharp
public sealed class CheckoutService
{
    private readonly IAddressValidator _addressValidator;
    private readonly IPaymentService _paymentService;
    private readonly IInventoryService _inventoryService;
    private readonly IOrderRepository _orders;

    public CheckoutService(
        IAddressValidator addressValidator,
        IPaymentService paymentService,
        IInventoryService inventoryService,
        IOrderRepository orders)
    {
        _addressValidator = addressValidator;
        _paymentService = paymentService;
        _inventoryService = inventoryService;
        _orders = orders;
    }

    public async Task<Order> PlaceOrderAsync(
        Cart cart, Customer customer, PaymentDetails payment, Address address, CancellationToken cancellationToken)
    {
        // 1. Fast, in-memory checks. Throws before any remote call is made.
        BusinessRuleChecker.CheckAll(
            new CartMustNotBeEmptyRule(cart),
            new CustomerMustBeActiveRule(customer),
            new CustomerMustBeAdultRule(customer));

        // 2. Checks that call other services, evaluated at the same time.
        IAsyncBusinessRule[] remoteRules =
        {
            new AddressMustBeDeliverableRule(_addressValidator, address),
            new PaymentMustBeAuthorizedRule(_paymentService, payment),
            new StockMustCoverCartRule(_inventoryService, cart.Items),
        };

        AsyncRuleEvaluationOptions options = new AsyncRuleEvaluationOptions { RunInParallel = true };
        await AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), remoteRules, options, cancellationToken);

        return await _orders.CreateAsync(cart, customer, payment, address, cancellationToken);
    }
}
```

What happens:

- If a sync rule is broken, `CheckAll` throws `BusinessRuleValidationException` with every broken rule. The remote checks never run.
- The three async rules run in parallel. `CheckAllAsync` waits for all of them, then throws one exception that lists every broken rule.

> **Note:** Rules running in parallel share one context, so they should only read from it. `StopOnFirstFailure = true` turns parallel evaluation off. See [Options](#options).

### User registration

Registration has one rule you can check right away and one that needs a database call. Build both with the fluent builders, so you do not need a class per rule.

```csharp
public sealed class RegistrationService
{
    private const int MinimumPasswordLength = 8;
    private readonly IUserRepository _users;

    public RegistrationService(IUserRepository users)
    {
        _users = users;
    }

    public async Task<User> RegisterAsync(RegistrationRequest request, CancellationToken cancellationToken)
    {
        IBusinessRule passwordTooShort = BusinessRuleBuilder.Create("PASSWORD.TOO_SHORT")
            .WithMessage($"Password must be at least {MinimumPasswordLength} characters.")
            .When(() => request.Password.Length < MinimumPasswordLength)
            .Build();

        passwordTooShort.Check();

        IAsyncBusinessRule emailTaken = AsyncBusinessRuleBuilder.Create("USER.EMAIL_TAKEN")
            .WithMessage("This email address is already registered.")
            .WhenAsync((context, token) => _users.EmailExistsAsync(request.Email, token))
            .Build();

        await emailTaken.CheckAsync(new BusinessRuleContext(), cancellationToken);

        return await _users.CreateAsync(request, cancellationToken);
    }
}
```

- `Check()` and `CheckAsync()` throw `BusinessRuleValidationException` when the rule is broken.
- Async rules always take a `BusinessRuleContext`. Pass an empty one when the rule does not read from it.
- To report several problems at once instead of stopping at the first, put the rules in one `BusinessRuleChecker.CheckAll` call.

### API controller validation

In an API you usually want to turn broken rules into an error response instead of an exception. `EvaluateAllAsync` returns the broken rules and lets you shape the response yourself.

```csharp
using Masterly.BusinessRules;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly IProductService _products;
    private readonly IInventoryService _inventory;
    private readonly IOrderService _orders;

    public OrdersController(IProductService products, IInventoryService inventory, IOrderService orders)
    {
        _products = products;
        _inventory = inventory;
        _orders = orders;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        IAsyncBusinessRule[] rules =
        {
            new ProductMustExistRule(_products, request.ProductId),
            new StockMustCoverQuantityRule(_inventory, request.ProductId, request.Quantity),
        };

        AsyncRuleEvaluationOptions options = new AsyncRuleEvaluationOptions { RunInParallel = true };
        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(
            new BusinessRuleContext(), rules, options, cancellationToken);

        if (result.HasBrokenRules)
        {
            ProblemDetails problem = new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "The order breaks one or more business rules.",
            };
            problem.Extensions["errors"] = result.BrokenRules
                .SelectMany(broken => broken.Flatten())
                .Select(failure => new { code = failure.Code, message = failure.Message })
                .ToList();

            return UnprocessableEntity(problem);
        }

        OrderDto order = await _orders.CreateAsync(request, cancellationToken);
        return Created($"/api/v1/orders/{order.Id}", order);
    }
}
```

- `EvaluateAllAsync` does not throw for broken rules. Check `HasBrokenRules` or `AllPassed`, then read `BrokenRules`.
- A rule that throws an exception still fails the call by default. Set `OnFault` on the options to record faults instead.
- `Flatten()` gives each broken rule inside a group its own entry.
- For a fuller error body (detail, actual, expected, remediation), see [Rich results](#rich-results).

### Multi-tenant order approval with an audit trail

This example approves an order against a per-tenant limit and keeps a readable record of why. It uses a subject-typed rule, a scoped parameter, a rule group and `Explain`.

```csharp
// OrderItemsRequiredRule is the rule from "Subject-Typed Rules": one instance serves every order.

public sealed class OrderApprovalService
{
    private const string LimitParameter = "maxOrderAmount";
    private const decimal DefaultLimit = 10_000m;

    private readonly IBusinessRule<Order> _withinTenantLimit;
    private readonly IAuditLog _audit;

    // tenantLimits holds the per-tenant values, for example an InMemoryRuleParameterProvider
    // filled with .Set("maxOrderAmount", 50_000m, RuleScope.Tenant("acme")).
    public OrderApprovalService(IRuleParameterProvider tenantLimits, IAuditLog audit)
    {
        _audit = audit;
        _withinTenantLimit = BusinessRuleBuilder<Order>.Create("ORDER.TOO_LARGE")
            .WithMessage("Order exceeds the tenant limit.")
            .WithParameters(tenantLimits)
            .WithParameter(LimitParameter, DefaultLimit)
            .WithActual((order, context) => order.Total)
            .When((order, context, parameters) => order.Total > parameters.Get<decimal>(LimitParameter))
            .Build();
    }

    public IReadOnlyList<string> Approve(Order order, string tenantId)
    {
        // The scope on the context decides which tenant's limit applies.
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(RuleParameters.ScopeKey, RuleScope.Tenant(tenantId));

        IBusinessRule approval = RuleGroup.All("ORDER.REJECTED", "The order cannot be placed.",
            new OrderItemsRequiredRule().For(order),
            _withinTenantLimit.For(order));

        // One evaluation returns both the verdict and the audit trail.
        RuleExplanation explanation = approval.Explain(context);
        _audit.Write(explanation.ToText());

        return explanation.BrokenLeaves()
            .Select(leaf => $"{leaf.Code}: {leaf.Message}")
            .ToList();
    }
}
```

- `.For(order)` binds a subject-typed rule to one order, so it can join a group with other rules.
- A tenant without its own value gets the fallback, `10_000m`.
- `Explain` evaluates the rule once. `ToText()` renders the whole tree, and `BrokenLeaves()` returns only the rules that actually failed.
- If a rule throws, `Explain` rethrows by default. Pass `captureExceptions: true` to record the fault in the tree instead.

---

## Upgrading from 2.x

Most 2.x code compiles against 3.0 without changes. A few behaviours changed, and every assembly built against 2.x must be rebuilt.

**Recompile: 3.0 is a binary break.** Rebuild every assembly that references Masterly.BusinessRules, including your own shared rule libraries. You do not need to change source code for this step.

If you skip it, code compiled against 2.x fails at run time with `MissingMethodException`. The reason: some members moved to new base types.

- `Code`, `Message`, `Severity`, `Name`, `Description`, `Category` and `Tags` moved from `IBusinessRule` / `IAsyncBusinessRule` to the new base interface `IBusinessRuleMetadata`.
- The builders' `WithMessage`, `WithSeverity`, `WithName`, `WithDescription`, `WithCategory` and `WithTags` moved to the new base class `BusinessRuleMetadataBuilder<TBuilder>`.
- `asyncRule.Cached(duration)` now returns `CachedAsyncBusinessRule` instead of `IAsyncBusinessRule`.

> **Note:** If you use a third-party package that was built against 2.x, you need a version of it built against 3.0.

**Behaviour changes.** After rebuilding, review the changes below. Each row says who needs to act.

| What changed | What you need to do | Who is affected |
|--------------|---------------------|-----------------|
| A builder keeps only the **last** `When(...)` call. In 2.x, `When(() => ...)` and `When(ctx => ...)` on the same builder both stayed active. | Give each built rule exactly one condition. | Code that called `When` more than once on one `BusinessRuleBuilder`. |
| Sync `And`, `Or` and `Not` pass the context to their operands. In 2.x they always called the parameterless `IsBroken()`. | Check that operands which read the context still give the verdict you expect. | Combined rules whose `IsBroken(context)` differs from `IsBroken()`. |
| `CompositeBusinessRule.IsBroken` and `CompositeAsyncBusinessRule.IsBrokenAsync` evaluate every child. In 2.x they stopped at the first broken child. | If you need to stop early, use `RuleGroup.All(...).WithShortCircuit()`. | Composites with slow children or children that have side effects. |
| `And`, `Or` and `Not` return new node types (`AndBusinessRule` and so on). The result's `Name` is now the node type name, and its `Description` describes the operator. | Do not cast combined rules to `SimpleBusinessRule` or `CompositeAsyncBusinessRule`, and do not rely on their `Name`. Codes and messages are unchanged. | Code that inspects the type or `Name` of a combined rule. |
| The checkers wrap a failure to read the context in `BusinessRuleContextException`. The original exception is its `InnerException`. | Catch `BusinessRuleContextException` instead of `KeyNotFoundException` or `InvalidCastException` around checker calls. | Code that catches context errors around `CheckAll` / `EvaluateAll`. |
| `BusinessRuleContext.Get<T>` throws `ContextKeyNotFoundException` or `ContextValueTypeMismatchException`. Both derive from the old exception types, so existing `catch` blocks still work. A stored `null` read as a non-nullable value type is now a type mismatch, and a `null` key throws `ArgumentNullException`. | Usually nothing. Review code that relied on a `NullReferenceException` for a stored `null`. | Code that reads a stored `null` as a value type. |
| A bare `null` context now selects the subject-typed checker overloads. `EvaluateAll(rules, null)` and `CheckAll(rules, null, options)` throw `ArgumentNullException`. | Leave the context out, or type the literal: `(BusinessRuleContext?)null`. A typed variable is fine. | Calls that pass the `null` literal as the context. |
| `new BusinessRuleResult(code, message, severity, null)` no longer compiles: the call is ambiguous between two new overloads. Both throw for `null`. | Use the three-argument constructor. | Code that constructs `BusinessRuleResult` directly. |
| The metadata members are declared on `IBusinessRuleMetadata`. | Look them up on the base interface. | Code that finds rule members by reflection. |
| Smaller fixes you might notice: a null rule in a batch throws `ArgumentException` instead of `NullReferenceException`; `CheckBySeverityAsync` and `CheckByCategoryAsync` honour their cancellation token; a built rule no longer sees later `WithTags` calls on its builder; group and composite results carry their broken members in `Details`. | Usually nothing. | Code or tests that depended on the old behaviour. |

> **Note:** `And` and `Or` behave exactly as they did in 2.x, but the 2.x README described them the wrong way round. `And` is broken only when **both** operands are broken. `Or` is broken when **either** operand is broken. If you wrote rules based on the old README, check them.

For the full list of changes, see the [CHANGELOG](https://github.com/a7mdfre7at/Masterly.BusinessRules/blob/master/CHANGELOG.md).

---

## API Reference

This reference lists the public types and members you use in application code, one package at a time. Each package uses a single namespace that matches its package name.

### Masterly.BusinessRules

The core package. It has no dependencies.

**Rules**

| Type or member | Purpose |
|----------------|---------|
| `IBusinessRule` | A sync rule: `IsBroken()`, `Check()` and `Evaluate()`, each also with a `BusinessRuleContext` overload. |
| `IAsyncBusinessRule` | An async rule: `IsBrokenAsync`, `CheckAsync` and `EvaluateAsync`. Each takes a context and an optional `CancellationToken`. |
| `IBusinessRule<TSubject>` / `IAsyncBusinessRule<TSubject>` | A rule that receives its subject at evaluation time, so one instance serves many subjects. |
| `IBusinessRuleMetadata` | Base of every rule interface: `Code`, `Message`, plus `Severity` (default `Error`), `Name`, `Description`, `Category` and `Tags`. |
| `BaseBusinessRule` / `BaseAsyncBusinessRule` | Base classes for your rules. Override `Code`, `Message` and `IsBroken` / `IsBrokenAsync`; override `Describe` to add result details. |
| `BaseBusinessRule<TSubject>` / `BaseAsyncBusinessRule<TSubject>` | Base classes for subject-typed rules. |
| `SimpleBusinessRule` | A sync rule made from a `Func<bool>`, a message and a code (default `"SIMPLE"`). |
| `RuleSeverity` | `Info`, `Warning`, `Error`. |
| `IComposedBusinessRule` / `IComposedAsyncBusinessRule` | Implemented by rules built from other rules: `Operator` and `Operands`. |
| `IDecoratedBusinessRule` / `IDecoratedAsyncBusinessRule` | Implemented by rules that wrap one other rule: `InnerRule`. |

**Building and combining**

| Type or member | Purpose |
|----------------|---------|
| `BusinessRuleBuilder` / `AsyncBusinessRuleBuilder` | Build a rule without a class: `Create(code)`, then `When(...)` / `WhenAsync(...)` and `Build()`. |
| `BusinessRuleBuilder<TSubject>` / `AsyncBusinessRuleBuilder<TSubject>` | The same for subject-typed rules. Conditions and details also receive the subject. |
| `BusinessRuleMetadataBuilder<TBuilder>` | Shared builder methods: `WithMessage`, `WithSeverity`, `WithName`, `WithDescription`, `WithCategory`, `WithTags`, `WithDetail`, `WithActual`, `WithExpected`, `WithRemediation`, `WithParameters`, `WithParameter<T>(name, fallback)`. |
| `.And(rule)` / `.Or(rule)` / `.Not()` | Combine rules. `And` is broken only when both are broken (code `A+B`); `Or` when either is (code `A\|B`); `Not` inverts (code `!A`). Also available on subject-typed rules. |
| `AndBusinessRule`, `OrBusinessRule`, `NotBusinessRule` | The node types `And`, `Or` and `Not` return. Async and `<TSubject>` versions exist. |
| `RuleGroup` / `AsyncRuleGroup` | Create a group from a code, a message and members: `All`, `Any`, `AtLeast(n, ...)`, `Exactly(n, ...)`. |
| `GroupBusinessRule` / `GroupAsyncBusinessRule` | An immutable group. `WithShortCircuit()` stops once the verdict is known; `WithSeverity()`; async `WithParallelEvaluation()`. |
| `RuleGroupQuantifier` | `All`, `Any`, `AtLeast`, `Exactly`. |
| `RuleOperator` | The `Operator` of a combined rule: `And`, `Or`, `Not`, `All`, `Any`, `AtLeast`, `Exactly`. |
| `CompositeBusinessRule` / `CompositeAsyncBusinessRule` | A list of rules, broken when any child is broken. Its code is `"CompositeRule"`. |
| `.For(subject)` | Bind a subject-typed rule to one subject. The result (`BoundBusinessRule<TSubject>` / `BoundAsyncBusinessRule<TSubject>`) is an ordinary rule. |
| `.Cached(duration)` / `.Cached(duration, keySelector)` | Cache the verdict for a time, once per rule or once per key. Returns `CachedBusinessRule` / `CachedAsyncBusinessRule`. |
| `CachedBusinessRule` / `CachedAsyncBusinessRule` | `InvalidateCache()` / `InvalidateCacheAsync()` (all keys or one key) and `CachesPerKey`. |
| `.When(condition)` | Evaluate the rule only when the condition is true (`ConditionalBusinessRule`). Sync takes `Func<bool>`; async takes `Func<BusinessRuleContext, bool>`. |
| `.WithSeverity(severity)` | Report the rule with another severity (`SeverityOverrideBusinessRule`). |
| `.ToAsync()` | Use a sync rule where an async rule is expected (`SyncToAsyncRuleAdapter`). Also works on subject-typed rules. |
| `AsyncToSyncRuleAdapter` | Use an async rule where a sync rule is expected. It blocks the calling thread. There is no extension method; construct it directly. |

**Running**

| Type or member | Purpose |
|----------------|---------|
| `BusinessRuleChecker.CheckAll(...)` | Evaluate every rule and throw `BusinessRuleValidationException` if any is broken. |
| `BusinessRuleChecker.EvaluateAll(...)` | Evaluate every rule and return a `RuleEvaluationResult`. Does not throw for broken rules. |
| `BusinessRuleChecker.CheckBySeverity` / `CheckByCategory` / `CheckByTags` | Check only the rules that match a severity, a category or any of the tags. |
| `BusinessRuleChecker.EvaluateEach(subjects, rules)` | Evaluate subject-typed rules for each subject. Returns a `SubjectEvaluationCollection<TSubject>`. |
| `AsyncBusinessRuleChecker` | The async versions: `CheckAllAsync`, `EvaluateAllAsync`, `CheckBySeverityAsync`, `CheckByCategoryAsync`, `CheckByTagsAsync`, `EvaluateEachAsync`. |
| `RuleEvaluationOptions` / `AsyncRuleEvaluationOptions` | `StopOnFirstFailure`, `OnFault` (default `Throw`), `ThrowOn` (default `Info`, used by `CheckAll` only) and `Observer`. Async adds `RunInParallel`. |
| `FaultPolicy` | What to do when a rule throws: `Throw`, `Record` or `TreatAsBroken`. Cancellation always propagates. |
| `.Check(throwOn)` / `.Check(context, throwOn)` / `.CheckAsync(context, throwOn)` | Check one rule and throw only when its severity is at or above `throwOn`. |
| `.Explain(context, captureExceptions)` / `.ExplainAsync(context, captureExceptions, token)` | Evaluate once and return a `RuleExplanation` tree. All arguments except the async context are optional. |
| `IRuleExecutionObserver` / `IAsyncRuleExecutionObserver` | Receive callbacks while a batch runs: before and after each rule, broken, faulted and cancelled rules, batch start and end, shadow results. |

> **Note:** On the async checker, the context always comes first: `CheckAllAsync(context, subject, rules)`. The sync subject-typed overloads take it last: `CheckAll(subject, rules, context)`.

**Results**

| Type or member | Purpose |
|----------------|---------|
| `RuleEvaluationResult` / `AsyncRuleEvaluationResult` | A batch result: `AllPassed`, `HasBrokenRules`, `BrokenRules`, `PassedRules`, `Errors` / `Warnings` / `Infos`, `ByTag`, `ByCategory`, `ShadowResults`, `ThrowIfBroken()`, `ThrowIfErrors()`, `ThrowIfAtLeast(severity)`. |
| `RuleEvaluationResult.Entries` | The ledger: one `RuleEvaluationEntry` per rule, in order. `Faulted`, `HasFaults`, `TotalDuration` and `Slowest` summarize it. |
| `RuleEvaluationEntry` | One rule's line in the ledger: `Order`, `Code`, `Outcome`, `Result`, `Fault`, `Duration`. |
| `RuleOutcome` | `Passed`, `Broken`, `Skipped`, `CacheHit`, `NotEvaluated`, `Faulted`. |
| `BusinessRuleResult` | One broken rule: `Code`, `Message`, `Severity`, `Name`, `Category`, `Tags`, `Detail`, `Actual`, `Expected`, `Remediation`, `Data`, and `Details` (broken members of a group). `Flatten()`, `HasTag()`, `WithSeverity()`. |
| `RuleResultDetails` | What `Describe` returns: `RuleResultDetails.Detail($"...")`, then `.Actual()`, `.Expected()`, `.Remediation()`, `.With(key, value)`. |
| `RuleExplanation` | A node of an explain tree: `Code`, `Outcome`, `IsBroken`, `Reason`, `Duration`, `Children`, `BrokenLeaves()`, `ToText()`. |
| `SubjectEvaluationCollection<TSubject>` / `SubjectEvaluation<TSubject>` | Results of `EvaluateEach`, one per subject in input order: `AllPassed`, `Broken`, `Passed`, `BrokenRules`. Async versions exist. |
| `BusinessRuleValidationException` | Thrown when rules are broken: `BrokenRules`, `CollectedRules` (broken rules below `ThrowOn`), `Entries`, `Flatten()`, `ByTag`, `ByCategory`. |
| `BusinessRuleContextException` | Thrown by the checkers when a rule cannot read the context: `RuleCode`, `Key`, original exception as `InnerException`. |
| `RuleConfigurationException` | A rule is set up wrongly, for example a gated rule with no provider or a cache key selector that returned `null`. `RuleCode`. |

**Context and parameters**

| Type or member | Purpose |
|----------------|---------|
| `BusinessRuleContext` | A key/value bag passed to rules: `Get`, `TryGet`, `Set`, `ContainsKey`, `Remove` (by string or `ContextKey<T>`), `Keys`, `Count`, `Clear`, `FormatProvider`. Not thread-safe. |
| `BusinessRuleContext<T>` | A context that also carries one typed `Data` object. |
| `ContextKey<T>` | A typed key (`Name`, `ValueType`), so reads need no cast and no string literal. |
| `ContextKeyNotFoundException` / `ContextValueTypeMismatchException` | Thrown by `Get`. They name the key, and list the available keys or the stored and requested types. |
| `RuleScope` | Who an evaluation is for: `Global`, `Tenant(id)`, `.With(key, value)`, `TenantId`, `IsGlobal`, `Specificity`, `TryGet(key)`, `ContainsKey(key)`, `Pairs`, `AppliesTo(requested)`. |
| `RuleParameters` | The resolved values passed to a `(ctx, p)` condition: `Get<T>`, `TryGet<T>`, `Contains`, `Names`, `Scope`. Holds the reserved context keys `ScopeKey`, `ContextKey`, `AsyncContextKey`. |
| `IRuleParameterProvider` / `IAsyncRuleParameterProvider` | Supply parameter values per scope: `TryGet<T>(name, scope, out value)` / `TryGetAsync<T>(name, scope, token)`. |
| `InMemoryRuleParameterProvider` | A thread-safe provider: `Set(name, value)`, `Set(name, value, scope)`, `Remove`, `Count`. The most specific matching scope wins. |
| `RuleParameterLookup<T>` | What `TryGetAsync` returns: `Found`, `Value`, `GetValueOrFallback(fallback)`; `Of(value)` and `Missing` to create one. |

**Rollout**

| Type or member | Purpose |
|----------------|---------|
| `.GatedBy(flag, provider, stableKey)` | Put a rule behind a feature flag. The provider and stable key are optional; without a provider, the rule looks for one on the context. |
| `FeatureGatedBusinessRule` / `FeatureGatedAsyncBusinessRule` | The gated rule: `Flag`, `Provider` (async also `AsyncProvider`), `HasStableKey`. |
| `RuleRollout` | A flag's state: `Off`, `Shadow(percent)`, `Enforced(percent)` (percent defaults to 100); `Mode`, `Percent`, `IsOff`, `IsComplete`. |
| `RuleRolloutMode` | `Off`, `Shadow`, `Enforced`. |
| `IRuleFeatureFlagProvider` / `IAsyncRuleFeatureFlagProvider` | Return a flag's `RuleRollout`: `GetRollout(flag, context)` / `GetRolloutAsync(flag, context, token)`. |
| `InMemoryRuleFeatureFlagProvider` | A thread-safe provider you can change at any time: `Set(flag, rollout)`, `Set(flag, mode, percent)`, `Remove`, `TryGet`, `DefaultRollout`. |
| `RuleFeatureFlags` | Context keys `ContextKey` / `AsyncContextKey` that override the provider for one evaluation. |
| `RuleRolloutBucket` | `Compute(flag, stableKey)` returns the bucket (0 to 99) a key falls into for a percentage rollout. |

**Specifications**

| Type or member | Purpose |
|----------------|---------|
| `SpecificationBusinessRule<TSubject>` | Base class for a rule written as an expression tree: override `BrokenWhen`. |
| `ISpecificationRule<TSubject>` | A subject-typed rule with `BrokenExpression` and `SatisfiedExpression`. |
| `.AllOf(others)` / `.AnyOf(others)` / `.Not()` | Combine specifications into one expression. Codes `ALL(A,B)`, `ANY(A,B)`, `!A`. |
| `AllOfSpecificationRule<TSubject>`, `AnyOfSpecificationRule<TSubject>`, `NotSpecificationRule<TSubject>` | The types these combinators return. They share the base `ComposedSpecificationRule<TSubject>`. |
| `query.WhereBroken(spec)` / `query.WhereSatisfies(spec)` | Filter an `IQueryable<T>` with a specification, so the database does the work. |
| `.ToAsync()` | Use a specification as an `IAsyncBusinessRule<TSubject>` (`AsyncSpecificationRuleAdapter<TSubject>`). |

**Testing helpers**

| Type or member | Purpose |
|----------------|---------|
| `RuleTestHelper.AssertBroken` / `AssertNotBroken` | Assert a rule's verdict, with or without a context. Async versions: `AssertBrokenAsync`, `AssertNotBrokenAsync`. |
| `RuleTestHelper.CreateContext(...)` | Create a context from `(key, value)` pairs, from typed `(ContextKey<T>, value)` pairs or from a data object. |
| `RuleTestHelper.CreateBrokenRule` / `CreatePassingRule` | Stub rules for tests. Async versions: `CreateBrokenAsyncRule`, `CreatePassingAsyncRule`. |
| `RuleTestHelper.CreateFlagProvider(flag, mode, percent)` | An `InMemoryRuleFeatureFlagProvider` with one flag set. |
| `RuleTestHelper.CreateParameterProvider(name, value, scope)` | An `InMemoryRuleParameterProvider` with one value set. `scope` is optional. |
| `RuleAssertionException` | Thrown when a test assertion fails. |

**Advanced extension points**

You only need these to write your own wrappers or plug in another condition language.

| Type or member | Purpose |
|----------------|---------|
| `IExplainableDecoratedBusinessRule` / `IExplainableDecoratedAsyncBusinessRule` | Implement for a custom wrapper that should appear as its own node in explain trees. |
| `DecoratedRuleEvaluation` / `AsyncDecoratedRuleEvaluation` | Passed to a custom wrapper. `EvaluateInner()` / `EvaluateInnerAsync()` runs the inner rule, at most once. |
| `IRuleConditionCompiler` / `IAsyncRuleConditionCompiler` | Turn condition text into a delegate for `When(compiler, text)` / `WhenAsync(compiler, text)`. |
| `IBuiltRuleFactory` / `IAsyncBuiltRuleFactory` | Decide which rule type `Build()` returns. Set with `WithRuleFactory(...)`. |
| `BuiltRuleDefinition` | What a factory receives: the builder's metadata, `ResolveParameters`, `ResolveParametersAsync`, `CreateResult`. |

### Masterly.BusinessRules.Expressions

| Type or member | Purpose |
|----------------|---------|
| `RuleExpression` | A parsed text condition: `Parse(text)`, `TryParse`, `Evaluate`, `EvaluateAsync`, `Explain`, `ExplainAsync`, `ToRule(code, message)`, `ToAsyncRule(code, message)`, `Compile<T>()`. |
| `.WhenExpression(text)` | Builder extension for `BusinessRuleBuilder` and `AsyncBusinessRuleBuilder`. Parses when you call it; `Build()` then returns an expression rule. |
| `ExpressionBusinessRule` / `ExpressionAsyncBusinessRule` | A rule over a `RuleExpression`: `Expression`, `ExplainExpression(context)` / `ExplainExpressionAsync(context)`. `Description` defaults to the expression text. |
| `RuleExpressionOptions` | Immutable limits and settings: `Default`, `MaxLength` (4096), `MaxDepth` (64), `MemberFilter`, `RequireRuleAccessibleAttribute`, `Culture` (invariant), each with a `With...` method. |
| `RuleAccessibleAttribute` | Marks the properties or types an expression may read when `RequireRuleAccessibleAttribute` is on. |
| `RuleExpressionExplanation` | The result of `Explain`: `Result`, `Steps` and the `Rendered` text. |
| `RuleExpressionStep` / `RuleExpressionStepKind` | One step of an explanation: `Kind` (`Binding` or `Operation`), `NodeText`, `BoundValue`, `Result`. |
| `RuleExpressionRedactor` | A delegate `(name, value)` that controls how a bound value appears in an explanation. |
| `RuleExpressionConditionCompiler` | The compiler to pass to `When(compiler, text)` / `WhenAsync(compiler, text)`. |
| `RuleExpressionParseException` | The text is malformed or exceeds a limit: `Line`, `Column`. |
| `RuleExpressionBindingException` | An identifier, parameter or member cannot be found: `Identifier`, `AvailableKeys`. |
| `RuleExpressionEvaluationException` | An operation failed at run time, for example on wrong operand types or division by zero: `NodeText`. |

> **Note:** `ExpressionBusinessRule.IsBroken()` without a context throws `InvalidOperationException`. Always pass a context.

### Masterly.BusinessRules.Diagnostics

| Type or member | Purpose |
|----------------|---------|
| `LoggingRuleExecutionObserver` / `LoggingAsyncRuleExecutionObserver` | Observers that write one structured `ILogger` entry per rule, plus batch, fault and shadow entries. |
| `LoggingRuleObserverOptions` | `LogPassedRules`, `PassedLevel`, `BrokenLevelBySeverity`, `DefaultBrokenLevel`, `FaultedLevel`, `LogBatches`, `BatchLevel`, `ShadowViolationLevel`, `IncludeMessage`. |
| `RuleLogEvents` | The `EventId` values: `RuleBroken` (1), `RulePassed` (2), `RuleFaulted` (3), `BatchStarted` (4), `BatchCompleted` (5), `ShadowViolation` (6). |
| `TracingRuleExecutionObserver` / `TracingAsyncRuleExecutionObserver` | Observers that create a `BusinessRules.Batch` span with a `BusinessRule.Evaluate` child per rule, and record metrics. |
| `TracingRuleObserverOptions` | `MessageAsEvent` (default false), `CodeTagSelector`, `RecordMetrics` (default true), `Metrics`. |
| `.Traced()` / `.Traced(logger)` | Wrap a single rule so each evaluation creates a span and a measurement, and with a logger also a log entry. Returns `DiagnosticsBusinessRule` / `DiagnosticsAsyncBusinessRule`. |
| `BusinessRulesActivitySource` | The `ActivitySource` to subscribe to: `Name` (`Masterly.BusinessRules`), `Source`, `RuleActivityName`, `BatchActivityName`. |
| `BusinessRuleSpanAttributes` | The span attribute and event names, such as `business_rule.code` and `business_rule.outcome`. |
| `BusinessRulesMetrics` | The `Meter` (`Masterly.BusinessRules`) with `business_rules.evaluations`, `business_rules.evaluation.duration` (ms) and `business_rules.batches`. `Default` is shared; pass your own `Meter` to the constructor to own it. |
| `RuleOutcomeTags` / `RuleSeverityTags` | Tag values used on spans and metrics: `passed`, `broken`, `faulted`, `shadow`, `cancelled`; `info`, `warning`, `error`. |

### Masterly.BusinessRules.DependencyInjection

| Type or member | Purpose |
|----------------|---------|
| `services.AddBusinessRules(assemblies)` / `services.AddBusinessRules(options => ...)` | Scan assemblies for rule types and register them, `IRuleRunner<T>` and `IRuleRegistry`. |
| `BusinessRuleOptions` | `Lifetime` (default `Scoped`), `IncludeNonPublicTypes`, `IncludeUntypedRules` (default false), `TypeFilter`, `AddAssembly`, `AddAssemblies`, `AddAssemblyContaining<T>()`, `AddRuleSet(...)`. |
| `IRuleRunner<TSubject>` | Inject to run every registered rule for one subject type: `CheckAll`, `EvaluateAll`, `CheckAllAsync`, `EvaluateAllAsync`; `Rules`, `AsyncRules`, `HasAsyncRules`. |
| `IRuleRegistry` / `RuleRegistry` | Named rule sets with per-scope overrides: `SetNames`, `Contains`, `Resolve`, `ResolveAsyncRules`, `CheckAll`, `EvaluateAll`, `CheckAllAsync`, `EvaluateAllAsync`. `RuleRegistry.Create()` starts a builder. |
| `RuleRegistryBuilder` | `AddSet(name, configure)` and `AddSet(name, scope, configure)`, then `Build()`. |
| `RuleSetBuilder` | One layer of a set: `Add`, `AddAsyncRule`, `Disable(code)`, `Replace(code, factory)`, `ReplaceAsyncRule(code, factory)`. |
| `RuleSetNotFoundException` | An unknown set name was requested: `SetName`, `AvailableSets`. |

> **Note:** Plain `IBusinessRule` / `IAsyncBusinessRule` classes are scanned only when `IncludeUntypedRules = true`. The sync `CheckAll` / `EvaluateAll` on a runner or registry throw `InvalidOperationException` when async rules are registered; use the async methods instead.

### Masterly.BusinessRules.FeatureManagement

| Type or member | Purpose |
|----------------|---------|
| `services.AddBusinessRulesFeatureManagement(configure, lifetime)` | Register the provider below for both provider interfaces. Microsoft.FeatureManagement must also be registered. Both arguments are optional; `lifetime` defaults to `Singleton`. |
| `FeatureManagementRuleFeatureFlagProvider` | Reads rollouts from `IVariantFeatureManager` (variants supported) or `IFeatureManager` (on/off only): `Options`, `SupportsVariants`. |
| `FeatureManagementRolloutOptions` | `EnabledRollout` (default `Enforced`), `OnUnrecognizedVariant` (default `Throw`), `TargetingContextSelector`, `MapVariant(name, rollout)`, `MapVariant(name, mode, percent)` (percent defaults to 100). |
| `UnrecognizedVariantBehavior` | What to do with a variant name that does not map to a rollout: `Throw`, `Off`, `UseEnabledState`. |
| `RolloutVariantConvention` | Parses variant names such as `Shadow`, `Enforced-10` and `Off` (`Parse`, `TryParse`), and `Mode` / `Percent` configuration (`TryRead`). |
| `RolloutVariantFormatException` | Thrown for a variant that cannot be mapped when the behavior is `Throw`: `Flag`, `Variant`. |

> **Note:** The provider's sync `GetRollout` blocks on the async feature manager. Prefer async rules when you gate with this provider.

### Masterly.BusinessRules.Testing

This package targets `net8.0` and `net10.0`.

| Type or member | Purpose |
|----------------|---------|
| `RuleCoverage.Begin()` | Start a `RuleCoverageSession` that records which outcomes each rule produced. |
| `RuleCoverageSession` | `Register(rule)`, `Record(rule, isBroken)`, `Report()`, `Reset()`, `RuleCount`, `IsEnded`. Thread-safe. Disposing it stops recording. |
| `.WithCoverage(session)` | Wrap a rule so every verdict is recorded in the session (`CoverageBusinessRule` / `CoverageAsyncBusinessRule`). |
| `RuleCoverageReport` | `OutcomeCoverage`, `IsComplete`, `NeverBroken`, `NeverPassed`, `NeverEvaluated`, `Find(code)`, `ToText()`. `AssertMinimum(coverage)` throws `RuleAssertionException` below the minimum. |
| `RuleCoverageEntry` | One rule's counts: `Code`, `BrokenCount`, `PassedCount`, `SeenBroken`, `SeenPassed`, `IsFullyCovered`. |
| `RuleBoundaryFinder.Find` / `FindAsync` | Find where a numeric rule switches from passing to broken, for any numeric `T` (`INumber<T>`). |
| `RuleBoundary<T>` | `LastPassing`, `FirstBroken`, `Monotonicity`, `ProbeCount`, `Step`. |
| `Monotonicity` | `BrokenAbove`, `BrokenBelow`. |
| `RuleProperties.For(factory, generator)` | Start property-based checks for a rule over CsCheck-generated inputs. Returns `RuleProperties<T>` or `AsyncRuleProperties<T>`. |
| `RuleProperties<T>` / `AsyncRuleProperties<T>` | Declare laws with `MonotoneIn(monotonicity)` and `ContextPure()`, then run `Verify(iterations, seed)` / `VerifyAsync(...)`. |
| `.Invert()` / `.AlwaysPass()` / `.DowngradeSeverity()` | Mutants that check whether your tests notice a broken rule: an inverted verdict, a rule that never breaks, a severity one level lower (`Info` is the lowest). |
| `InvertedBusinessRule`, `AlwaysPassingBusinessRule` | The mutant types. Async versions exist. |

If this library helps you, please give it a star on GitHub.

---

## License

MIT - use it freely in your projects. See [LICENSE.txt](https://github.com/a7mdfre7at/Masterly.BusinessRules/blob/master/LICENSE.txt).
