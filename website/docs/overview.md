---
id: overview
title: "Overview"
sidebar_label: "Overview"
---

Masterly.BusinessRules is a business rule engine for .NET. You write each rule, such as "an order must contain at least one item", as a small named unit with a code and a message. You then check one rule or many, and you get back a clear list of every rule that failed.

Every feature works with both synchronous and asynchronous rules. The one exception is specifications: an expression tree has no async form, so you use `.ToAsync()` to run a specification in async code (see [Specifications and database queries](./specifications.md)). The core package has no dependencies. Five optional packages add text expressions, logging and tracing, dependency injection, feature management and test tools.

**What you can do**

- Write a rule as a small class, or build one inline from a lambda. See [Writing rules](./writing-rules.md) and [Building rules without classes](./building-rules.md).
- Write one stateless rule that validates whichever order you pass to it. See [Subject-typed rules](./subject-typed-rules.md).
- Combine rules with And, Or and Not, or with groups such as "all", "any" and "at least 2 of". See [Combining rules](./combining-rules.md).
- Check many rules in one call, and either collect every failure or stop at the first one. See [Running many rules](./running-many-rules.md).
- See exactly which part of a combined rule failed, as a printable tree. See [Understanding failures](./understanding-failures.md).
- Run a rule only when a condition holds, and cache the verdict of a slow rule. See [Conditional and cached rules](./conditional-and-cached-rules.md).
- Ship a new rule in shadow mode behind a feature flag, then enforce it for a percentage of callers. See [Feature flags and shadow rollout](./feature-flags.md).
- Give each tenant its own limits, such as a maximum order amount, without changing rule code. See [Per-tenant rules and parameters](./per-tenant-rules.md).
- Use the same rule to check one object in memory and to query the database for every violating row. See [Specifications and database queries](./specifications.md).
- Write a condition as text, such as `amount > @maxOrderAmount`, and see the values it was evaluated with. See [Rule expressions](./rule-expressions.md).
- Log, trace and measure every rule evaluation with `ILogger`, OpenTelemetry and metrics. See [Logging, tracing and metrics](./logging-tracing-metrics.md).
- Register your rules in a DI container and inject a runner, and test your rules with coverage, boundary and property checks. See [Dependency injection](./dependency-injection.md) and [Testing your rules](./testing.md).
