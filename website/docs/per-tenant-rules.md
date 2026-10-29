---
id: per-tenant-rules
title: "Per-Tenant Rules and Parameters"
sidebar_label: "Per-Tenant Rules"
---

Multi-tenant applications often need the same rules with different limits, or different rules, per tenant. You can do both without writing `if (tenant == "acme")` in your rules. A scope says who an evaluation is for, parameters supply per-scope values, and a registry supplies per-scope rule sets.

## Rule scopes

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

:::note

The value under `RuleParameters.ScopeKey` must be a `RuleScope`. A tenant id stored there as a plain string throws `ContextValueTypeMismatchException` (checkers report it wrapped in `BusinessRuleContextException`). The library does not silently fall back to the global scope.

:::

## Externalized parameters

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

:::note

A cached rule keeps one result for everybody by default, which is wrong when the limit depends on the scope. Cache it per scope with a key selector that also handles a context without a scope. [Cache expensive rules](./conditional-and-cached-rules.md#cache-expensive-rules) shows one.

:::

## Rule registries

A rule registry holds named rule sets: a base version for everybody, plus overrides for specific scopes. Use it when tenants need different rules, not only different values. The registry is part of the `Masterly.BusinessRules.DependencyInjection` package; see [Dependency Injection](./dependency-injection.md) to install it.

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

:::note

Every factory in every applying layer runs, even for a rule that a later layer disables. `Disable` matches on the rule's code, so it removes the rule after the factory has created it. A global factory that reads a context value therefore runs for ACME too, and that value must be present.

:::

**With dependency injection.** Register the same sets with `AddRuleSet` inside `AddBusinessRules`, then inject `IRuleRegistry`. [Dependency Injection](./dependency-injection.md) shows a complete example.
