---
id: conditional-and-cached-rules
title: "Conditional and Cached Rules"
description: "Run a rule only when a precondition holds, cache slow verdicts, and mix sync and async rules."
---

These wrappers change *when* a rule runs, not *what* it checks. Use them to skip a rule that does not apply, to avoid repeating an expensive check, or to run a sync rule where async code expects an async one (and the other way round).

Each wrapper keeps the inner rule's `Code`, `Message`, `Severity` and other metadata. You can pass a wrapped rule anywhere you pass the original.

## Run a rule only when a condition holds

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

:::note

Do not confuse the two `When` methods.
- On a builder, `When(...)` sets the condition under which the rule is *broken*.
- On a built rule, `.When(...)` sets a precondition that decides whether the rule *runs at all*.

A sync precondition cannot read the context. If you need one that does, put the check into the rule's own condition: `.When(ctx => ctx.Get<bool>("paysByInvoice") && ...)`.

:::

## Cache expensive rules

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

:::note

The selector runs on every call that has a context, so it must handle a context without a scope, as the `TryGet` above does. `ctx.Get(RuleParameters.ScopeKey)` would throw `ContextKeyNotFoundException` there. A selector that returns `null` throws `RuleConfigurationException`.

:::

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

:::note

Wrapper order matters. `rule.When(condition).Cached(ttl)` caches the skipped "pass" too, and serves it for the whole duration even after the condition becomes true. Write `rule.Cached(ttl).When(condition)` so the condition is checked on every call. The same applies to feature gates: put the cache inside the gate, as in `rule.Cached(ttl).GatedBy(...)` (see [Gating a rule](./feature-flags.md#gating-a-rule)).

:::

## Mixing sync and async rules

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
- A specification has its own `ToAsync()`, covered in [Specifications and Database Queries](./specifications.md).
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

:::note

`AsyncToSyncRuleAdapter` blocks the calling thread until the async rule finishes. On a thread with a synchronization context (a UI thread, classic ASP.NET) this can deadlock. Elsewhere it holds a thread-pool thread for the whole call. Prefer making the caller async and calling `.ToAsync()` on the sync rules instead.

:::
