---
id: writing-rules
title: "Writing Rules"
description: "Sync and async rule classes, the three ways to run a rule, metadata, and passing data with a context."
---

A rule is a small class that answers one question: is this business rule broken? You give it a code, a message and a condition. The rule is broken when the condition returns `true`.

Pick a base class based on what the condition needs:

| Base class | Use it when | You override |
|------------|-------------|--------------|
| `BaseBusinessRule` | The rule can decide from data already in memory. | `Code`, `Message`, `IsBroken()` |
| `BaseAsyncBusinessRule` | The rule needs I/O, such as a database query or an HTTP call. | `Code`, `Message`, `IsBrokenAsync(context, cancellationToken)` |

All core types live in the `Masterly.BusinessRules` namespace. To build a rule from a lambda instead of a class, see [Building rules without classes](./building-rules.md).

Later examples use rule classes such as `ShippingAddressRequiredRule` or `CustomerMustBeAdultRule` without showing them. They stand for your own rules, written as shown in this section.

## Sync rules

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

## Async rules

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
- Sync and async rules are separate type hierarchies. A sync rule is not an `IAsyncBusinessRule`. To run a sync rule next to async ones, convert it with `rule.ToAsync()` (see [Mixing sync and async rules](./conditional-and-cached-rules.md#mixing-sync-and-async-rules)).

## Three ways to run a rule

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

:::note

Sync `Evaluate()` does not run the rule. It only builds the "broken" result from the rule's metadata, so it never returns `null`, even for a rule that passes. Call it after `IsBroken()` returned `true`: `BusinessRuleResult? result = rule.IsBroken() ? rule.Evaluate() : null;`. Async `EvaluateAsync` is different: it runs the rule and returns `null` when the rule passes.

:::

- `Check()` throws for a broken rule of any severity, including `Info`.
- To throw only at or above a severity, pass a threshold: `rule.Check(RuleSeverity.Error)`, `rule.Check(context, RuleSeverity.Error)` or `rule.CheckAsync(context, RuleSeverity.Error, ct)`. A broken rule below the threshold returns without throwing.
- The overloads without a context call `IsBroken()`. The overloads with a context call `IsBroken(context)`.
- To run many rules at once and collect every failure, see [Running many rules](./running-many-rules.md).

## Rule metadata

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

- `CheckBySeverity`, `CheckByCategory` and `CheckByTags` check only the rules that match. The sync versions run the rules without a context. [Filtering by severity, category or tags](./running-many-rules.md#filtering-by-severity-category-or-tags) explains how each one matches.
- A broken rule's result copies its `Code`, `Message`, `Severity`, `Name`, `Category` and `Tags`.
- To give an existing rule a different severity without changing its class, wrap it: `rule.WithSeverity(RuleSeverity.Warning)`.

:::note

Severity is a label. It does not change what `Check()` does: a plain `Check()` throws for every broken rule. Pass a severity threshold (see [Three ways to run a rule](#three-ways-to-run-a-rule)) to let `Info` and `Warning` violations through.

:::

## Passing data with a context

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

:::note

When you run a sync rule without a context (`rule.Check()`, or a checker call without one), the library calls the parameterless `IsBroken()`. That is why a context-only rule should throw a clear exception there, as in the example, instead of returning a guess.

:::

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
- When the checker's fault policy is `FaultPolicy.Record` or `FaultPolicy.TreatAsBroken`, the checker does not rethrow it. The wrapped exception is recorded in the evaluation ledger and passed to observers instead. See [When a rule throws](./running-many-rules.md#when-a-rule-throws).

## Typed context keys

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

:::note

Always create a key with its constructor. `default(ContextKey<T>)` has no name, and using it throws `ArgumentNullException`.

:::
