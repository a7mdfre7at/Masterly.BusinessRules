---
id: testing
title: "Testing Your Rules"
sidebar_label: "Testing Your Rules"
---

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

## Built-in test helpers

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

:::note

Pass a context when you test a sync feature-gated rule. Without a context, the rule ignores its flag and always evaluates the inner rule. See [Gating a rule](./feature-flags.md#gating-a-rule).

:::

## Rule coverage

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

:::note

Wrap each operand before you combine rules with `And`, `Or` or a group. A combined rule can skip operands once its result is known. Only wrapped operands show up in the report as skipped.

:::

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

:::info[Advanced]

To record outcomes from your own wrapper, call `session.Register(rule)` and `session.Record(rule, isBroken)` directly.

:::

## Finding a rule's boundary

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

:::note

The finder samples the range to confirm that the rule flips only once. A rule that misbehaves only in a narrow band can still slip through.

:::

## Property-based checks

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

:::note

On .NET 8 and 9 the package uses CsCheck 4.9.1, where `Gen.Decimal[start, finish]` fails when a bound is exactly `0m`. `Verify` reports this as `InvalidOperationException` ("Failing Where max count"). Use a bound such as `0.01m`, or build amounts from whole cents: `Gen.Long[0L, 10_000_000L].Select(cents => cents / 100m)`. CsCheck 5 (.NET 10) does not have this problem.

:::

## Smoke-test mutants

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

:::note

Mutants are a smoke test, not mutation testing. They change a rule's verdict from the outside, so they cannot find bugs inside your `IsBroken` logic. For that, use a source-level mutation tool such as [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/).

:::
