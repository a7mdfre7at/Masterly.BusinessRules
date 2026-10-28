# Masterly.BusinessRules.DependencyInjection

Registers business rules in `Microsoft.Extensions.DependencyInjection`: assembly scanning, an injectable `IRuleRunner<T>` per subject type, and a scoped `IRuleRegistry` for per-tenant rule sets.

This is a companion package of [Masterly.BusinessRules](https://www.nuget.org/packages/Masterly.BusinessRules), a composable business-rule engine for .NET.

[![NuGet](https://img.shields.io/nuget/v/Masterly.BusinessRules.DependencyInjection?style=flat-square)](https://www.nuget.org/packages/Masterly.BusinessRules.DependencyInjection)

## Install

```bash
dotnet add package Masterly.BusinessRules.DependencyInjection
```

- **Target framework:** `netstandard2.1` (.NET Core 3.0+, .NET 5+)
- **Dependencies:** `Masterly.BusinessRules`, `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.14

## Example

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Masterly.BusinessRules;
using Masterly.BusinessRules.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

public sealed class Order
{
    public List<string> Items { get; } = new List<string>();
}

// A subject-typed rule: scanning finds it, and it can take services through its constructor.
public sealed class OrderItemsRequiredRule : BaseBusinessRule<Order>
{
    public override string Code => "ORDER.NO_ITEMS";

    public override string Message => "Order must contain at least one item.";

    public override bool IsBroken(Order order) => order.Items.Count == 0;
}

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
        return _rules.CheckAllAsync(order, cancellationToken);   // throws BusinessRuleValidationException when broken
    }
}

public static class Registration
{
    public static IServiceCollection AddOrderRules(this IServiceCollection services)
    {
        return services.AddBusinessRules(typeof(OrderItemsRequiredRule).Assembly);
    }
}
```

Scanning registers every concrete `IBusinessRule<T>` / `IAsyncBusinessRule<T>`, an `IRuleRunner<T>` per subject type and the `IRuleRegistry`. It is deterministic, and untyped rules are scanned only when you set `IncludeUntypedRules = true`.

## Documentation

Scan options, lifetimes, the rule registry and scoped parameters are covered in the full documentation: <https://github.com/a7mdfre7at/Masterly.BusinessRules#readme>
