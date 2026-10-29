---
id: dependency-injection
title: "Dependency Injection"
---

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

:::note

Leave `IncludeUntypedRules` off unless your plain rules take only injectable services. A typical plain rule receives its subject in the constructor (`new OrderMustHaveItemsRule(order)` from the Quick Start), which the container cannot create. Registering it fails at startup under `ValidateOnBuild` (on by default in ASP.NET Core Development), or on the first resolution of `IEnumerable<IBusinessRule>`. If you turn it on, use `TypeFilter` to exclude such rules.

:::

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

:::note

A synchronous batch cannot run async rules without blocking, so `CheckAll` and `EvaluateAll` throw instead of silently skipping them. Check `HasAsyncRules` if you are not sure.

:::

In tests or in code without a container, create the runner directly from two rule collections: `new RuleRunner<Order>(syncRules, asyncRules)`.

**Named rule sets with `AddRuleSet`**

A rule set is a named list of rule factories in `IRuleRegistry`. Use one when rules need values from the context, or when some tenants need different rules. Each `AddRuleSet` call adds a layer for a scope. A layer for a more specific scope can disable, replace or add rules; [Rule registries](./per-tenant-rules.md#rule-registries) explains how layers merge.

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
- Errors, scope handling and the merge order are the same as without a container. See [Rule registries](./per-tenant-rules.md#rule-registries).

:::note

`IRuleRegistry` is a singleton, so the `services` passed to `(services, set)` is the root provider. Resolve only singletons there. A scoped service resolved there lives as long as the application, or throws when scope validation is on.

:::
