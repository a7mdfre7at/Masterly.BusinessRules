# Masterly.BusinessRules

A clean, composable, and extensible business rule engine for .NET.

<img src="https://raw.githubusercontent.com/a7mdfre7at/Masterly.BusinessRules/master/repo_image.png" width="200" height="180">

[![Nuget](https://img.shields.io/nuget/v/Masterly.BusinessRules?style=flat-square)](https://www.nuget.org/packages/Masterly.BusinessRules) ![Nuget](https://img.shields.io/nuget/dt/Masterly.BusinessRules?style=flat-square) ![GitHub last commit](https://img.shields.io/github/last-commit/a7mdfre7at/Masterly.BusinessRules?style=flat-square) ![GitHub](https://img.shields.io/github/license/a7mdfre7at/Masterly.BusinessRules) [![Build](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/build.yml/badge.svg?branch=master)](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/build.yml) [![CodeQL Analysis](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/codeql.yml/badge.svg?branch=master)](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/codeql.yml) [![Publish to NuGet](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/publish.yml/badge.svg?branch=master)](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/publish.yml)


## Give a Star! :star:

If you like or are using this project please give it a star. Thanks!

---

## ✨ Features

- Sync & Async rule support
- Encapsulated rule logic
- Composite & logical rule composition (AND, OR, NOT)
- Business rule context with data passing
- Dependency Injection support
- Easy-to-use checker APIs
- Custom exception for broken rules

---

## 🚀 Quick Start

### 1. Define a business rule:
```csharp
public class OrderMustHaveItemsRule(Order _order) : BaseBusinessRule
{
    public override string Code => "ORDER.NO_ITEMS";
    public override string Message => "Order must contain at least one item.";
    public override bool IsBroken() => !_order.Items.Any();
}
```

### 2. Check the rule:
```csharp
new OrderMustHaveItemsRule(order).Check();
```

### 3. Or use the composite checker:
```csharp
BusinessRuleChecker.CheckAll(
    new OrderMustHaveItemsRule(order),
    new CustomerMustBeActiveRule(order.Customer)
);
```

### 4. Async variant:
```csharp
await AsyncBusinessRuleChecker.CheckAllAsync(context,
    new ProductMustBeAvailableRule(productService),
    new PaymentMustBeVerifiedRule(paymentService)
);
```

---

## 🔄 Real-World Example: Checkout Flow

### Scenario
You're building a checkout system. Before placing an order, the following rules must be satisfied:
- The cart must contain at least one item.
- The customer must be active.
- The customer must be at least 18 years old.
- The shipping address must be valid.

### Rule Definitions
```csharp
public class CartMustHaveItemsRule(Cart _cart) : BaseBusinessRule
{
    public override string Code => "CART.EMPTY";
    public override string Message => "Cart must have at least one item.";
    public override bool IsBroken() => !_cart.Items.Any();
}

public class CustomerMustBeActiveRule(Customer _customer) : BaseBusinessRule
{
    public override string Code => "CUSTOMER.INACTIVE";
    public override string Message => "Customer must be active.";
    public override bool IsBroken() => !_customer.IsActive;
}

public class CustomerMustBeAdultRule(Customer _customer) : BaseBusinessRule
{
    public override string Code => "CUSTOMER.UNDERAGE";
    public override string Message => "Customer must be at least 18 years old.";
    public override bool IsBroken() => _customer.Age < 18;
}

public class ShippingAddressMustBeValidRule(IAddressValidator _validator, Address _address) : BaseAsyncBusinessRule
{
    public override string Code => "ADDRESS.INVALID";
    public override string Message => "Shipping address is invalid.";

    public override async Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
    {
        return !await _validator.IsValidAsync(_address);
    }
}
```

### Using the Rules in the Checkout Flow
```csharp
// Sync rules
BusinessRuleChecker.CheckAll(
    new CartMustHaveItemsRule(cart),
    new CustomerMustBeActiveRule(customer),
    new CustomerMustBeAdultRule(customer)
);

// Async rule
await AsyncBusinessRuleChecker.CheckAllAsync(
    new BusinessRuleContext(),
    new ShippingAddressMustBeValidRule(addressValidator, order.ShippingAddress)
);
```

---

## 🧩 Composition
```csharp
var rule = new CustomerIsAdultRule(customer)
                .And(new CustomerIsActiveRule(customer));

rule.Check();
```

Async composition is supported using `And()`, `Or()`, and `Not()` as well:
```csharp
await rule1.And(rule2).CheckAsync(context);
```

---

## 📚 Usage Details

This guide provides a comprehensive breakdown of how to use each part of the `Masterly.BusinessRules` library in your .NET projects. Whether you're dealing with synchronous or asynchronous validations, composing multiple rules, or integrating with DI, this section covers it all with practical, real-world examples.

Each component of the framework can be used as follows:


- `IBusinessRule` and `IAsyncBusinessRule` define contracts for both sync and async rules.

```csharp
public class AgeMustBeAtLeast18Rule(int _age) : IBusinessRule
{
    public string Code => "AGE_UNDER_18";
    public string Message => "User must be at least 18 years old.";
    public RuleSeverity Severity => RuleSeverity.Error;
    public bool IsBroken() => _age < 18;

    public void Check()
    {
        if (IsBroken()) throw new BusinessRuleValidationException([Evaluate()]);
    }

    public BusinessRuleResult Evaluate() => new(Code, Message, Severity);
}
```

- Implement these interfaces when creating your own rules or inherit from `BaseBusinessRule` / `BaseAsyncBusinessRule` for a simpler experience.

```csharp
public class EmailMustBeValidRule(string _email) : BaseBusinessRule
{
    public override string Code => "INVALID_EMAIL";
    public override string Message => "Email address is not valid.";
    public override bool IsBroken() => string.IsNullOrWhiteSpace(_email) || !_email.Contains("@");
}

public class UserMustExistAsyncRule(IUserService _userService, Guid _userId) : BaseAsyncBusinessRule
{
    public override string Code => "USER_NOT_FOUND";
    public override string Message => "User does not exist.";

    public override async Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
    {
        var exists = await _userService.ExistsAsync(_userId);
        return !exists;
    }
}
```
- `BaseBusinessRule`: Use this as a base for sync rules. You only need to override `Code`, `Message`, and `IsBroken()`.
- `BaseAsyncBusinessRule`: Same pattern, but for async rules using `IsBrokenAsync()`.
- `BusinessRuleResult`: Immutable result structure returned from evaluation.
- `BusinessRuleValidationException`: Custom exception thrown when rules are broken.
- `BusinessRuleContext`: Lightweight context object used to pass runtime data to async rules.

### Composition

```csharp
var rule = new EmailMustBeValidRule(email)
    .And(new AgeMustBeAtLeast18Rule(age));

rule.Check();

var asyncRule = rule1.Or(rule2.Not());
await asyncRule.CheckAsync(new BusinessRuleContext());

var composite = new CompositeBusinessRule(new IBusinessRule[]
{
    new AgeMustBeAtLeast18Rule(age),
    new EmailMustBeValidRule(email)
});

composite.Check();
```
- `CompositeBusinessRule`: Accepts multiple `IBusinessRule` implementations and checks them together.
- `CompositeAsyncBusinessRule`: Evaluates async rule conditions based on a composed delegate.
- `BusinessRuleExtensions`:
  - `And()`, `Or()`, `Not()` for combining rules logically.
- `AsyncBusinessRuleExtensions`:
  - Same as above but for `IAsyncBusinessRule`.

### Infrastructure

```csharp
BusinessRuleChecker.CheckAll(
    new EmailMustBeValidRule(user.Email),
    new AgeMustBeAtLeast18Rule(user.Age)
);

await AsyncBusinessRuleChecker.CheckAllAsync(
    new BusinessRuleContext(),
    new UserMustExistAsyncRule(userService, user.Id)
);
```

- `BusinessRuleChecker.CheckAll(...)`: Evaluates all sync rules, throws if any broken.
- `AsyncBusinessRuleChecker.CheckAllAsync(...)`: Same for async rules.


## 📝 License
MIT — use it freely in your projects.
