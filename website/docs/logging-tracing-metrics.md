---
id: logging-tracing-metrics
title: "Logging, Tracing and Metrics"
description: "Observers, structured ILogger logging, OpenTelemetry spans and Meter metrics for rule evaluation."
sidebar_label: "Logging and Tracing"
---

You can watch every rule a checker evaluates. Write your own observer for custom needs such as an audit trail. Or install `Masterly.BusinessRules.Diagnostics` to get ready-made logging, OpenTelemetry tracing and metrics.

:::tip[What needs which package]

- [Observers](#observers) are part of the core package.
- [Structured logging](#structured-logging) and [OpenTelemetry tracing and metrics](#opentelemetry-tracing-and-metrics) need `Masterly.BusinessRules.Diagnostics` (`netstandard2.1`).

:::

## Observers

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

:::note

`EvaluateAll` / `EvaluateAllAsync` call observers only through their options-taking overloads. The overloads without options have no observer.

:::

:::note

The async checker passes `CancellationToken.None` to `OnRuleCancelledAsync` and `OnBatchAbortedAsync`, so your cleanup code still runs after the caller's token was cancelled.

:::

## Structured logging

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

:::note

A rule `Message` can contain domain data, such as a customer id or an amount. Set `IncludeMessage = false` to write `[redacted]` in its place.

:::

## OpenTelemetry tracing and metrics

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

:::note

By default, combined rules (`And`, `Or`, `Not`, groups, composites) get no `code` tag, and their span keeps the name `BusinessRule.Evaluate`. Their generated codes would otherwise create a new metric series for every combination. Wrapped rules (cached, gated and so on) keep the code of the rule they wrap.

:::

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

:::note

Use the observer for batches and `Traced()` for single-rule calls. A traced rule inside a traced batch produces a nested span and is counted twice.

:::

:::note

A faulted rule gets a duration sample only when something listens to the `Masterly.BusinessRules` activity source.

:::
