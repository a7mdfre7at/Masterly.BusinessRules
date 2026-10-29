# Masterly.BusinessRules

A composable business rule engine for .NET.

![Masterly.BusinessRules logo](https://raw.githubusercontent.com/a7mdfre7at/Masterly.BusinessRules/master/repo_image.png)

[![Nuget](https://img.shields.io/nuget/v/Masterly.BusinessRules?style=flat-square)](https://www.nuget.org/packages/Masterly.BusinessRules) ![Nuget](https://img.shields.io/nuget/dt/Masterly.BusinessRules?style=flat-square) ![GitHub last commit](https://img.shields.io/github/last-commit/a7mdfre7at/Masterly.BusinessRules?style=flat-square) ![GitHub](https://img.shields.io/github/license/a7mdfre7at/Masterly.BusinessRules) [![Build](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/build.yml/badge.svg?branch=master)](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/build.yml) [![CodeQL Analysis](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/codeql.yml/badge.svg?branch=master)](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/codeql.yml) [![Publish to NuGet](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/publish.yml/badge.svg?branch=master)](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/publish.yml) [![Docs](https://img.shields.io/badge/docs-github.io-512bd4?style=flat-square)](https://a7mdfre7at.github.io/Masterly.BusinessRules/)

**Full documentation: [a7mdfre7at.github.io/Masterly.BusinessRules](https://a7mdfre7at.github.io/Masterly.BusinessRules/)**

## Overview

Masterly.BusinessRules is a business rule engine for .NET. You write each rule, such as "an order must contain at least one item", as a small named unit with a code and a message. You then check one rule or many, and you get back a clear list of every rule that failed.

Every feature works with both synchronous and asynchronous rules. The core package has no dependencies. Five optional packages add text expressions, logging and tracing, dependency injection, feature management and test tools.

**What you can do**

- Write a rule as a small class, or build one inline from a lambda. See [Writing rules](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/writing-rules) and [Building rules without classes](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/building-rules).
- Write one stateless rule that validates whichever order you pass to it. See [Subject-typed rules](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/subject-typed-rules).
- Combine rules with And, Or and Not, or with groups such as "all", "any" and "at least 2 of". See [Combining rules](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/combining-rules).
- Check many rules in one call, and either collect every failure or stop at the first one. See [Running many rules](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/running-many-rules).
- See exactly which part of a combined rule failed, as a printable tree. See [Understanding failures](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/understanding-failures).
- Run a rule only when a condition holds, and cache the verdict of a slow rule. See [Conditional and cached rules](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/conditional-and-cached-rules).
- Ship a new rule in shadow mode behind a feature flag, then enforce it for a percentage of callers. See [Feature flags and shadow rollout](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/feature-flags).
- Give each tenant its own limits, such as a maximum order amount, without changing rule code. See [Per-tenant rules and parameters](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/per-tenant-rules).
- Use the same rule to check one object in memory and to query the database for every violating row. See [Specifications and database queries](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/specifications).
- Write a condition as text, such as `amount > @maxOrderAmount`, and see the values it was evaluated with. See [Rule expressions](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/rule-expressions).
- Log, trace and measure every rule evaluation with `ILogger`, OpenTelemetry and metrics. See [Logging, tracing and metrics](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/logging-tracing-metrics).
- Register your rules in a DI container and inject a runner, and test your rules with coverage, boundary and property checks. See [Dependency injection](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/dependency-injection) and [Testing your rules](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/testing).

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

- To get the results without an exception, call `BusinessRuleChecker.EvaluateAll` instead. See [Check all or evaluate all](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/running-many-rules#check-all-or-evaluate-all).
- For rules that call a database or a web service, see [Async rules](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/writing-rules#async-rules).
- To combine rules, see [Combining Rules](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/combining-rules).

---

## Documentation

| Topic | Guide |
|-------|-------|
| Getting started | [Overview](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/overview), [Installation](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/installation), [Quick start](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/quick-start) |
| Writing rules | [Rule classes and context](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/writing-rules), [Builders](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/building-rules), [Subject-typed rules](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/subject-typed-rules), [Combining rules](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/combining-rules) |
| Running rules | [Running many rules](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/running-many-rules), [Understanding failures](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/understanding-failures), [Conditional and cached rules](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/conditional-and-cached-rules) |
| Advanced topics | [Feature flags and shadow rollout](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/feature-flags), [Per-tenant rules and parameters](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/per-tenant-rules), [Specifications](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/specifications), [Rule expressions](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/rule-expressions) |
| Integrations | [Logging, tracing and metrics](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/logging-tracing-metrics), [Dependency injection](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/dependency-injection), [Testing your rules](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/testing) |
| Examples | [Real-world examples](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/examples) |
| Reference | [API reference](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/api-reference), [Upgrading from 2.x](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/upgrading-from-2x), [Changelog](https://github.com/a7mdfre7at/Masterly.BusinessRules/blob/master/CHANGELOG.md) |

**Upgrading from 2.x?** Most code compiles against 3.0 without changes, but 3.0 is a binary break: rebuild every assembly that references Masterly.BusinessRules. A few behaviours changed too. See [Upgrading from 2.x](https://a7mdfre7at.github.io/Masterly.BusinessRules/docs/upgrading-from-2x).

If this library helps you, please give it a star on GitHub.

---

## License

MIT - use it freely in your projects. See [LICENSE.txt](https://github.com/a7mdfre7at/Masterly.BusinessRules/blob/master/LICENSE.txt).
