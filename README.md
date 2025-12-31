# Masterly.BusinessRules

A clean, composable, and extensible business rule engine for .NET.

<img src="https://raw.githubusercontent.com/a7mdfre7at/Masterly.BusinessRules/master/repo_image.png" width="200" height="180">

[![Nuget](https://img.shields.io/nuget/v/Masterly.BusinessRules?style=flat-square)](https://www.nuget.org/packages/Masterly.BusinessRules) ![Nuget](https://img.shields.io/nuget/dt/Masterly.BusinessRules?style=flat-square) ![GitHub last commit](https://img.shields.io/github/last-commit/a7mdfre7at/Masterly.BusinessRules?style=flat-square) ![GitHub](https://img.shields.io/github/license/a7mdfre7at/Masterly.BusinessRules) [![Build](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/build.yml/badge.svg?branch=master)](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/build.yml) [![CodeQL Analysis](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/codeql.yml/badge.svg?branch=master)](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/codeql.yml) [![Publish to NuGet](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/publish.yml/badge.svg?branch=master)](https://github.com/a7mdfre7at/Masterly.BusinessRules/actions/workflows/publish.yml)


## Give a Star! :star:

If you like or are using this project please give it a star. Thanks!

---

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Creating Rules](#creating-rules)
- [Fluent Builders](#fluent-builders)
- [Rule Metadata](#rule-metadata)
- [Rule Composition](#rule-composition)
- [Batch Validation](#batch-validation)
- [Context and Data Passing](#context-and-data-passing)
- [Caching](#caching)
- [Conditional Execution](#conditional-execution)
- [Rule Adapters](#rule-adapters)
- [Observers](#observers)
- [Evaluation Results](#evaluation-results)
- [Testing Utilities](#testing-utilities)
- [Real-World Examples](#real-world-examples)
- [API Reference](#api-reference)
- [License](#license)

---

## Features

- **Sync & Async Rules** - Full support for both synchronous and asynchronous business rules
- **Fluent Builders** - Create rules inline without subclassing
- **Logical Composition** - Combine rules with `And()`, `Or()`, `Not()`
- **Rule Metadata** - Name, description, category, tags, and severity
- **Caching** - Time-based caching for expensive rule evaluations
- **Conditional Execution** - Execute rules only when preconditions are met
- **Rule Adapters** - Convert between sync and async rules
- **Observers** - Monitor rule execution with callbacks
- **Batch Validation** - Check multiple rules with fail-fast and parallel execution
- **Typed Context** - Strongly-typed data passing to rules
- **Testing Utilities** - Helper methods for unit testing rules

---

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package Masterly.BusinessRules
```

Or via Package Manager Console:

```powershell
Install-Package Masterly.BusinessRules
```

---

## Quick Start

```csharp
// 1. Define a rule
public class OrderMustHaveItemsRule(Order _order) : BaseBusinessRule
{
    public override string Code => "ORDER.NO_ITEMS";
    public override string Message => "Order must contain at least one item.";
    public override bool IsBroken() => !_order.Items.Any();
}

// 2. Check the rule
new OrderMustHaveItemsRule(order).Check();

// 3. Or use the batch checker
BusinessRuleChecker.CheckAll(
    new OrderMustHaveItemsRule(order),
    new CustomerMustBeActiveRule(order.Customer)
);
```

---

## Creating Rules

Business rules encapsulate validation logic in a reusable, testable unit. Each rule has:

- **Code**: A unique identifier for the rule (e.g., "ORDER.NO_ITEMS")
- **Message**: A human-readable description of why the rule is broken
- **IsBroken()**: The validation logic that returns `true` if the rule is violated

The library provides two base classes:
- `BaseBusinessRule` for synchronous rules
- `BaseAsyncBusinessRule` for rules that need async operations (database queries, API calls)

Rules can be checked in three ways:
- `IsBroken()` / `IsBrokenAsync()`: Returns a boolean indicating if the rule is violated
- `Check()` / `CheckAsync()`: Throws `BusinessRuleValidationException` if the rule is broken
- `Evaluate()` / `EvaluateAsync()`: Returns a `BusinessRuleResult` object (or null if not broken)

### Sync Rule Example

```csharp
public class CustomerMustBeActiveRule : BaseBusinessRule
{
    private readonly Customer _customer;

    public CustomerMustBeActiveRule(Customer customer)
    {
        _customer = customer;
    }

    public override string Code => "CUSTOMER.INACTIVE";
    public override string Message => "Customer must be active to place orders.";
    public override bool IsBroken() => !_customer.IsActive;
}

// Using primary constructor (C# 12+)
public class CustomerMustBeActiveRule(Customer _customer) : BaseBusinessRule
{
    public override string Code => "CUSTOMER.INACTIVE";
    public override string Message => "Customer must be active to place orders.";
    public override bool IsBroken() => !_customer.IsActive;
}

// Usage
CustomerMustBeActiveRule rule = new CustomerMustBeActiveRule(customer);

if (rule.IsBroken())
    Console.WriteLine($"Rule violated: {rule.Message}");

rule.Check(); // Throws BusinessRuleValidationException if broken

BusinessRuleResult result = rule.Evaluate();
```

### Async Rule Example

```csharp
public class EmailMustBeUniqueRule(IUserRepository _repository, string _email) : BaseAsyncBusinessRule
{
    public override string Code => "EMAIL.DUPLICATE";
    public override string Message => $"Email '{_email}' is already registered.";

    public override async Task<bool> IsBrokenAsync(
        BusinessRuleContext context,
        CancellationToken cancellationToken = default)
    {
        return await _repository.EmailExistsAsync(_email, cancellationToken);
    }
}

// Usage
EmailMustBeUniqueRule rule = new EmailMustBeUniqueRule(repository, "user@example.com");
BusinessRuleContext context = new BusinessRuleContext();

bool isBroken = await rule.IsBrokenAsync(context);
await rule.CheckAsync(context);
BusinessRuleResult? result = await rule.EvaluateAsync(context);
```

---

## Fluent Builders

Fluent builders let you create rules inline without defining a class. This is useful for:

- Simple, one-off validation rules
- Rules defined at runtime based on configuration
- Quick prototyping

The builders support all rule metadata (name, description, category, tags, severity) and provide a chainable API.

- `BusinessRuleBuilder`: Creates synchronous rules with `When()` condition
- `AsyncBusinessRuleBuilder`: Creates asynchronous rules with `WhenAsync()` condition

### Sync Builder Example

```csharp
// Simple rule
IBusinessRule ageRule = BusinessRuleBuilder.Create("AGE.INVALID")
    .WithMessage("Age must be 18 or older")
    .When(() => age < 18)
    .Build();

// With full metadata
IBusinessRule fullAgeRule = BusinessRuleBuilder.Create("AGE.INVALID")
    .WithName("Age Validation Rule")
    .WithMessage("Age must be 18 or older")
    .WithDescription("Validates that the user is at least 18 years old")
    .WithSeverity(RuleSeverity.Error)
    .WithCategory("User Validation")
    .WithTags("user", "age", "compliance")
    .When(() => age < 18)
    .Build();

// With context access
IBusinessRule limitRule = BusinessRuleBuilder.Create("LIMIT.EXCEEDED")
    .WithMessage("Transaction limit exceeded")
    .When(ctx => ctx.Get<decimal>("amount") > ctx.Get<decimal>("limit"))
    .Build();
```

### Async Builder Example

```csharp
IAsyncBusinessRule emailRule = AsyncBusinessRuleBuilder.Create("EMAIL.DUPLICATE")
    .WithMessage("Email already exists")
    .WithSeverity(RuleSeverity.Error)
    .WhenAsync(async (ctx, ct) => await repository.EmailExistsAsync(email, ct))
    .Build();

// Without cancellation token
IAsyncBusinessRule dataRule = AsyncBusinessRuleBuilder.Create("DATA.INVALID")
    .WithMessage("External data validation failed")
    .WhenAsync(async ctx => await externalService.ValidateAsync(ctx.Get<string>("data")))
    .Build();
```

---

## Rule Metadata

Every rule carries metadata that enables filtering, categorization, and documentation. Only `Code` and `Message` are required - all other metadata properties have default interface implementations and are truly optional.

| Property | Description | Default |
|----------|-------------|---------|
| `Code` | Unique identifier for the rule | **Required** |
| `Message` | Human-readable violation message | **Required** |
| `Severity` | Importance level: `Info`, `Warning`, or `Error` | `Error` |
| `Name` | Display name for the rule | Type name |
| `Description` | Detailed explanation of what the rule validates | Empty |
| `Category` | Grouping category (e.g., "Payment", "User") | Empty |
| `Tags` | List of tags for flexible filtering | Empty |

**Severity Levels:**
- `Info`: Informational - rule is broken but not critical
- `Warning`: Should be addressed but doesn't block the operation
- `Error`: Must be fixed, blocks the operation

Metadata enables powerful filtering with `CheckBySeverity()`, `CheckByCategory()`, and `CheckByTags()`.

### Metadata Example

```csharp
public class PaymentValidationRule : BaseBusinessRule
{
    public override string Code => "PAYMENT.INVALID";
    public override string Message => "Payment validation failed.";
    public override string Name => "Payment Validation";
    public override string Description => "Validates payment details before processing";
    public override RuleSeverity Severity => RuleSeverity.Error;
    public override string Category => "Payment";
    public override IReadOnlyList<string> Tags => new[] { "payment", "checkout" };

    public override bool IsBroken() => /* validation logic */;
}

// Filter by metadata
BusinessRuleChecker.CheckBySeverity(rules, RuleSeverity.Error);
BusinessRuleChecker.CheckByCategory(rules, "Payment");
BusinessRuleChecker.CheckByTags(rules, "checkout", "payment");
```

---

## Rule Composition

Rule composition allows you to combine multiple rules using logical operators. This creates complex validation logic from simple, reusable building blocks.

**Operators:**
- `And()`: Both rules must pass (composite is broken if EITHER rule is broken)
- `Or()`: At least one rule must pass (composite is broken only if BOTH are broken)
- `Not()`: Inverts the rule's result

Composition works with both sync and async rules. You can chain operators to create complex expressions like `rule1.And(rule2.Or(rule3.Not()))`.

**CompositeBusinessRule** and **CompositeAsyncBusinessRule** provide containers for grouping multiple rules that are evaluated together.

### Composition Examples

```csharp
// AND - both rules must pass
IBusinessRule rule = new CustomerIsAdultRule(customer)
    .And(new CustomerIsActiveRule(customer));

// OR - at least one must pass
IBusinessRule paymentRule = new HasCreditCardRule(customer)
    .Or(new HasBankAccountRule(customer));

// NOT - invert the result
IBusinessRule notBlockedRule = new IsBlockedRule(customer).Not();

// Complex composition
IBusinessRule accessRule = new CustomerIsAdultRule(customer)
    .And(new CustomerIsActiveRule(customer))
    .Or(new CustomerIsVIPRule(customer));

// Async composition
IAsyncBusinessRule asyncRule = new EmailUniqueRule(repo, email)
    .And(new UsernameUniqueRule(repo, username));
await asyncRule.CheckAsync(context);

// Composite container
CompositeBusinessRule composite = new CompositeBusinessRule(
    new Rule1(), new Rule2(), new Rule3()
);
composite.Check();
```

---

## Batch Validation

Batch validation allows checking multiple rules at once. The library provides two checker classes:

- `BusinessRuleChecker`: For synchronous rules
- `AsyncBusinessRuleChecker`: For asynchronous rules

**Features:**
- **Fail-Fast Mode** (`stopOnFirstFailure: true`): Stops checking after the first broken rule, improving performance when you only need to know if validation failed
- **Parallel Execution** (`runInParallel: true`): Evaluates async rules concurrently for better performance with I/O-bound rules
- **Observer Support**: Receive callbacks during rule evaluation for logging, metrics, etc.
- **Filtering**: Check only rules matching specific severity, category, or tags

Note: Fail-fast and parallel execution cannot be combined - fail-fast takes precedence.

### Batch Validation Examples

```csharp
// Basic batch checking
BusinessRuleChecker.CheckAll(rule1, rule2, rule3);

await AsyncBusinessRuleChecker.CheckAllAsync(context, asyncRule1, asyncRule2);

// With context
BusinessRuleContext context = new BusinessRuleContext();
context.Set("userId", 123);
BusinessRuleChecker.CheckAll(context, rule1, rule2);

// Fail-fast mode
BusinessRuleChecker.CheckAll(rules, stopOnFirstFailure: true);

await AsyncBusinessRuleChecker.CheckAllAsync(
    context, rules,
    stopOnFirstFailure: true
);

// Parallel execution (async only)
await AsyncBusinessRuleChecker.CheckAllAsync(
    context, rules,
    runInParallel: true
);

// Filter by criteria
BusinessRuleChecker.CheckBySeverity(rules, RuleSeverity.Error);
BusinessRuleChecker.CheckByCategory(rules, "Payment");
BusinessRuleChecker.CheckByTags(rules, "checkout");

await AsyncBusinessRuleChecker.CheckBySeverityAsync(
    context, rules, default, RuleSeverity.Error
);
```

---

## Context and Data Passing

`BusinessRuleContext` is a key-value store for passing data to rules during evaluation. This is especially useful for:

- Sharing data between multiple rules
- Passing runtime configuration
- Providing services or dependencies

**Features:**
- `Set(key, value)`: Store a value
- `Get<T>(key)`: Retrieve a typed value
- `TryGet<T>(key, out value)`: Safe retrieval
- `ContainsKey(key)`: Check existence
- `Remove(key)`: Remove a value

**Typed Context:** `BusinessRuleContext<T>` provides strongly-typed access to a data object via the `Data` property, while still supporting the key-value store for additional data.

### Context Examples

```csharp
// Basic context
BusinessRuleContext context = new BusinessRuleContext();
context.Set("userId", 123);
context.Set("role", "admin");
context.Set("limit", 1000.00m);

int userId = context.Get<int>("userId");
if (context.TryGet<string>("role", out string role))
    Console.WriteLine($"Role: {role}");

// Using context in rules
public class TransactionLimitRule : BaseBusinessRule
{
    private readonly decimal _amount;

    public TransactionLimitRule(decimal amount) => _amount = amount;

    public override string Code => "TRANSACTION.LIMIT_EXCEEDED";
    public override string Message => "Transaction amount exceeds limit.";

    public override bool IsBroken(BusinessRuleContext context)
    {
        decimal limit = context.Get<decimal>("limit");
        return _amount > limit;
    }
}

// Typed context
public record OrderContext(Order Order, Customer Customer);

BusinessRuleContext<OrderContext> typedContext = new BusinessRuleContext<OrderContext>(
    new OrderContext(order, customer)
);

Order order = typedContext.Data.Order;
typedContext.Set("additionalInfo", "extra data"); // Still works
```

---

## Caching

Caching wraps a rule and stores its result for a specified duration. Subsequent calls return the cached result without re-executing the rule logic. This is useful for:

- Expensive database queries
- External API calls
- Complex calculations

**Classes:**
- `CachedBusinessRule`: Wraps sync rules with thread-safe caching
- `CachedAsyncBusinessRule`: Wraps async rules with async-safe caching (uses SemaphoreSlim)

**Cache Invalidation:**
- `InvalidateCache()`: Clears the sync cache
- `InvalidateCacheAsync()`: Clears the async cache

**Extension Methods:**
- `.WithCache(TimeSpan)`: Fluent syntax for adding caching

### Caching Examples

```csharp
// Sync caching
CachedBusinessRule cachedRule = new CachedBusinessRule(
    new ExpensiveValidationRule(),
    TimeSpan.FromMinutes(5)
);

bool result1 = cachedRule.IsBroken(); // Executes rule
bool result2 = cachedRule.IsBroken(); // Returns cached result

cachedRule.InvalidateCache(); // Clear cache

// Async caching
CachedAsyncBusinessRule cachedAsyncRule = new CachedAsyncBusinessRule(
    new DatabaseValidationRule(),
    TimeSpan.FromMinutes(10)
);

bool result = await cachedAsyncRule.IsBrokenAsync(context);
await cachedAsyncRule.InvalidateCacheAsync();

// Extension method syntax
IBusinessRule cachedExtRule = new ExpensiveRule().WithCache(TimeSpan.FromMinutes(5));
IAsyncBusinessRule cachedExtAsync = new DatabaseRule().WithCache(TimeSpan.FromSeconds(30));
```

---

## Conditional Execution

Conditional execution wraps a rule and only evaluates it when a precondition is met. If the condition returns `false`, the rule is considered not broken (passes automatically). This is useful for:

- Feature flags
- User-specific validation
- Optional validation steps

**Classes:**
- `ConditionalBusinessRule`: Wraps sync rules with a sync condition
- `ConditionalAsyncBusinessRule`: Wraps async rules with a sync condition

**Extension Methods:**
- `.WithCondition(Func<bool>)`: Simple condition
- `.WithCondition(Func<BusinessRuleContext, bool>)`: Context-aware condition
- `.WithCondition(Func<BusinessRuleContext, CancellationToken, Task<bool>>)`: Async condition

### Conditional Execution Examples

```csharp
// Simple condition
ConditionalBusinessRule conditionalRule = new ConditionalBusinessRule(
    new PremiumFeatureRule(),
    () => user.IsPremium
);

// Context-aware condition
ConditionalBusinessRule contextRule = new ConditionalBusinessRule(
    new AdvancedValidationRule(),
    ctx => ctx.Get<bool>("enableAdvancedValidation")
);

// Async rule with condition
ConditionalAsyncBusinessRule conditionalAsyncRule = new ConditionalAsyncBusinessRule(
    new ExpensiveDatabaseRule(),
    ctx => ctx.Get<bool>("shouldValidate")
);

await conditionalAsyncRule.CheckAsync(context); // Only runs if condition is true

// Extension method syntax
IBusinessRule condExtRule = new PremiumRule().WithCondition(() => isPremiumEnabled);

IAsyncBusinessRule condExtAsync = new ValidationRule()
    .WithCondition(ctx => ctx.Get<bool>("validateEnabled"));

IAsyncBusinessRule asyncCondition = new ComplexRule()
    .WithCondition(async (ctx, ct) =>
        await featureService.IsEnabledAsync("validation", ct));
```

---

## Rule Adapters

Rule adapters convert between sync and async rule interfaces. This is useful when you need to:

- Use a sync rule in an async-only context
- Use an async rule in legacy sync code

**Classes:**
- `SyncToAsyncRuleAdapter`: Wraps a sync rule to implement `IAsyncBusinessRule`
- `AsyncToSyncRuleAdapter`: Wraps an async rule to implement `IBusinessRule` (**Warning: Blocks!**)

**Extension Methods:**
- `.ToAsyncRule()`: Convert sync to async
- `.ToSyncRule()`: Convert async to sync (blocking)

> **Warning:** `ToSyncRule()` and `AsyncToSyncRuleAdapter` block the calling thread using `.GetAwaiter().GetResult()`. Avoid using in UI threads or ASP.NET request threads to prevent deadlocks.

### Adapter Examples

```csharp
// Sync to Async
IBusinessRule syncRule = new CustomerIsAdultRule(customer);
IAsyncBusinessRule convertedAsyncRule = syncRule.ToAsyncRule();

// Or using adapter directly
SyncToAsyncRuleAdapter syncToAsyncAdapter = new SyncToAsyncRuleAdapter(syncRule);

await convertedAsyncRule.CheckAsync(context);

// Async to Sync (WARNING: Blocks!)
IAsyncBusinessRule originalAsyncRule = new EmailMustBeUniqueRule(repo, email);
IBusinessRule convertedSyncRule = originalAsyncRule.ToSyncRule();

// Or using adapter directly
AsyncToSyncRuleAdapter asyncToSyncAdapter = new AsyncToSyncRuleAdapter(originalAsyncRule);

convertedSyncRule.Check(); // Blocks until async operation completes
```

---

## Observers

Observers receive callbacks during rule evaluation, enabling:

- Logging and debugging
- Performance metrics
- Audit trails
- Custom error handling

**Interfaces:**
- `IRuleExecutionObserver`: For sync rule evaluation
  - `OnBeforeEvaluate(IBusinessRule rule)`: Called before evaluation
  - `OnAfterEvaluate(IBusinessRule rule, BusinessRuleResult? result)`: Called after (result is null if passed)
  - `OnRuleBroken(IBusinessRule rule, BusinessRuleResult result)`: Called only when broken

- `IAsyncRuleExecutionObserver`: For async rule evaluation
  - Same methods with async signatures and CancellationToken

Observers are passed to `BusinessRuleChecker.CheckAll()` or `AsyncBusinessRuleChecker.CheckAllAsync()` via the `observer` parameter.

### Observer Examples

```csharp
// Sync observer
public class LoggingObserver : IRuleExecutionObserver
{
    public void OnBeforeEvaluate(IBusinessRule rule)
    {
        Console.WriteLine($"[START] Evaluating: {rule.Code}");
    }

    public void OnAfterEvaluate(IBusinessRule rule, BusinessRuleResult? result)
    {
        string status = result == null ? "PASSED" : "FAILED";
        Console.WriteLine($"[{status}] {rule.Code}");
    }

    public void OnRuleBroken(IBusinessRule rule, BusinessRuleResult result)
    {
        Console.WriteLine($"[BROKEN] {result.Code}: {result.Message}");
    }
}

// Usage
BusinessRuleChecker.CheckAll(rules, observer: new LoggingObserver());

// Async observer
public class AsyncLoggingObserver : IAsyncRuleExecutionObserver
{
    public async Task OnBeforeEvaluateAsync(IAsyncBusinessRule rule, CancellationToken ct)
    {
        await logger.LogAsync($"Evaluating: {rule.Code}");
    }

    public async Task OnAfterEvaluateAsync(IAsyncBusinessRule rule, BusinessRuleResult? result, CancellationToken ct)
    {
        await logger.LogAsync($"Result: {result?.Code ?? "passed"}");
    }

    public async Task OnRuleBrokenAsync(IAsyncBusinessRule rule, BusinessRuleResult result, CancellationToken ct)
    {
        await logger.LogAsync($"Broken: {result.Message}");
    }
}

// Usage
await AsyncBusinessRuleChecker.CheckAllAsync(
    context, rules, observer: new AsyncLoggingObserver()
);
```

---

## Evaluation Results

Evaluation methods return detailed results without throwing exceptions. This is useful when you need to:

- Collect all validation errors at once
- Filter or process errors programmatically
- Return errors to an API client

**Classes:**
- `RuleEvaluationResult`: Results from sync evaluation
- `AsyncRuleEvaluationResult`: Results from async evaluation

**Properties:**
- `BrokenRules`: List of `BusinessRuleResult` for broken rules
- `PassedRules`: List of rules that passed
- `HasBrokenRules`: True if any rules are broken
- `HasPassedRules`: True if any rules passed
- `AllRulesPassed`: True if all rules passed
- `AllRulesBroken`: True if all rules are broken

**Filtering Methods:**
- `GetBrokenByCode(string)`: Filter by code (contains match)
- `GetBrokenBySeverity(RuleSeverity)`: Filter by severity

### Evaluation Examples

```csharp
// Sync evaluation
RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rules);

if (result.HasBrokenRules)
{
    foreach (BusinessRuleResult error in result.BrokenRules)
        Console.WriteLine($"{error.Code}: {error.Message}");
}

if (result.AllRulesPassed)
    Console.WriteLine("Validation successful!");

// Filter results
IEnumerable<BusinessRuleResult> paymentErrors = result.GetBrokenByCode("PAYMENT");
IEnumerable<BusinessRuleResult> errors = result.GetBrokenBySeverity(RuleSeverity.Error);
IEnumerable<BusinessRuleResult> warnings = result.GetBrokenBySeverity(RuleSeverity.Warning);

// Async evaluation
AsyncRuleEvaluationResult asyncResult = await AsyncBusinessRuleChecker.EvaluateAllAsync(
    context, rules
);

// Parallel async evaluation
AsyncRuleEvaluationResult parallelResult = await AsyncBusinessRuleChecker.EvaluateAllAsync(
    context, rules, runInParallel: true
);

// API response example
if (result.HasBrokenRules)
{
    return BadRequest(new {
        errors = result.BrokenRules.Select(r => new {
            code = r.Code,
            message = r.Message,
            severity = r.Severity.ToString()
        })
    });
}
```

---

## Testing Utilities

`RuleTestHelper` provides static methods for unit testing business rules:

**Assertion Methods:**
- `AssertBroken(rule)`: Assert the rule is broken
- `AssertBroken(rule, context)`: Assert with context
- `AssertNotBroken(rule)`: Assert the rule passes
- `AssertNotBroken(rule, context)`: Assert with context
- `AssertBrokenAsync(rule)`: Async assertion for broken
- `AssertNotBrokenAsync(rule)`: Async assertion for passing

**Factory Methods:**
- `CreateContext(params (string, object)[])`: Create context with key-value pairs
- `CreateContext<T>(T data)`: Create typed context
- `CreateBrokenRule(code, message)`: Create a rule that always fails
- `CreatePassingRule(code, message)`: Create a rule that always passes
- `CreateBrokenAsyncRule(code, message)`: Async rule that always fails
- `CreatePassingAsyncRule(code, message)`: Async rule that always passes

All assertion methods throw `RuleAssertionException` on failure.

### Testing Examples

```csharp
public class CustomerRulesTests
{
    [Fact]
    public void CustomerMustBeActive_WhenInactive_IsBroken()
    {
        Customer customer = new Customer { IsActive = false };
        CustomerMustBeActiveRule rule = new CustomerMustBeActiveRule(customer);

        RuleTestHelper.AssertBroken(rule);
    }

    [Fact]
    public void CustomerMustBeActive_WhenActive_Passes()
    {
        Customer customer = new Customer { IsActive = true };
        CustomerMustBeActiveRule rule = new CustomerMustBeActiveRule(customer);

        RuleTestHelper.AssertNotBroken(rule);
    }

    [Fact]
    public void Rule_WithContext_EvaluatesCorrectly()
    {
        TransactionLimitRule rule = new TransactionLimitRule(500m);
        BusinessRuleContext context = RuleTestHelper.CreateContext(("limit", 1000m));

        RuleTestHelper.AssertNotBroken(rule, context);
    }

    [Fact]
    public async Task AsyncRule_WhenBroken_ThrowsAssertion()
    {
        IAsyncBusinessRule rule = RuleTestHelper.CreateBrokenAsyncRule();

        await RuleTestHelper.AssertBrokenAsync(rule);
    }
}

// Factory methods
IBusinessRule brokenRule = RuleTestHelper.CreateBrokenRule("TEST.ERROR", "Test error");
IBusinessRule passingRule = RuleTestHelper.CreatePassingRule();
BusinessRuleContext context = RuleTestHelper.CreateContext(
    ("userId", 123),
    ("role", "admin")
);
```

---

## Real-World Examples

### E-Commerce Checkout

```csharp
public class CheckoutService
{
    public async Task<CheckoutResult> ProcessCheckoutAsync(
        Cart cart, Customer customer, PaymentDetails payment, Address address)
    {
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set("cart", cart);

        // Fast sync validations first
        BusinessRuleChecker.CheckAll(
            new CartMustHaveItemsRule(cart),
            new CustomerMustBeActiveRule(customer),
            new CustomerMustBeAdultRule(customer)
        );

        // Async validations in parallel
        await AsyncBusinessRuleChecker.CheckAllAsync(
            context,
            new IAsyncBusinessRule[]
            {
                new AddressValidationRule(_addressValidator, address),
                new PaymentValidationRule(_paymentService, payment),
                new InventoryAvailabilityRule(_inventoryService, cart.Items)
            },
            runInParallel: true
        );

        return await CreateOrderAsync(cart, customer, payment, address);
    }
}
```

### User Registration

```csharp
public async Task<User> RegisterUserAsync(RegistrationRequest request)
{
    // Sync validations
    IBusinessRule passwordRule = BusinessRuleBuilder.Create("PASSWORD.WEAK")
        .WithMessage("Password must be at least 8 characters")
        .When(() => request.Password.Length < 8)
        .Build();

    BusinessRuleChecker.CheckAll(passwordRule);

    // Async uniqueness checks
    IAsyncBusinessRule emailRule = AsyncBusinessRuleBuilder.Create("EMAIL.EXISTS")
        .WithMessage($"Email '{request.Email}' is already registered")
        .WhenAsync(async (ctx, ct) => await _repo.EmailExistsAsync(request.Email, ct))
        .Build();

    await AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), emailRule);

    return await CreateUserAsync(request);
}
```

### API Controller Validation

```csharp
[HttpPost]
public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
{
    AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(
        new BusinessRuleContext(),
        new IAsyncBusinessRule[]
        {
            new ProductExistsRule(_productService, request.ProductId),
            new InventoryRule(_inventoryService, request.ProductId, request.Quantity)
        },
        runInParallel: true
    );

    if (result.HasBrokenRules)
    {
        return BadRequest(new {
            errors = result.BrokenRules.Select(r => new {
                code = r.Code,
                message = r.Message
            })
        });
    }

    Order order = await _orderService.CreateAsync(request);
    return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
}
```

---

## API Reference

### Core Interfaces

| Interface | Description |
|-----------|-------------|
| `IBusinessRule` | Sync rule: `IsBroken()`, `Check()`, `Evaluate()` |
| `IAsyncBusinessRule` | Async rule: `IsBrokenAsync()`, `CheckAsync()`, `EvaluateAsync()` |
| `IBusinessRuleMetadata` | Metadata: Name, Description, Category, Tags, Severity |
| `IRuleExecutionObserver` | Sync execution callbacks |
| `IAsyncRuleExecutionObserver` | Async execution callbacks |

### Classes

| Class | Description |
|-------|-------------|
| `BaseBusinessRule` | Abstract base for sync rules |
| `BaseAsyncBusinessRule` | Abstract base for async rules |
| `BusinessRuleBuilder` | Fluent builder for sync rules |
| `AsyncBusinessRuleBuilder` | Fluent builder for async rules |
| `CompositeBusinessRule` | Container for multiple sync rules |
| `CompositeAsyncBusinessRule` | Container for multiple async rules |
| `CachedBusinessRule` | Time-based caching for sync rules |
| `CachedAsyncBusinessRule` | Time-based caching for async rules |
| `ConditionalBusinessRule` | Conditional execution for sync rules |
| `ConditionalAsyncBusinessRule` | Conditional execution for async rules |
| `SyncToAsyncRuleAdapter` | Adapts sync rules to async |
| `AsyncToSyncRuleAdapter` | Adapts async rules to sync (blocking) |
| `BusinessRuleChecker` | Batch sync validation |
| `AsyncBusinessRuleChecker` | Batch async validation |
| `BusinessRuleResult` | Immutable result with Code, Message, Severity |
| `RuleEvaluationResult` | Batch sync evaluation result |
| `AsyncRuleEvaluationResult` | Batch async evaluation result |
| `BusinessRuleValidationException` | Exception when rules are broken |
| `RuleTestHelper` | Testing assertions and factories |

### Extension Methods

| Method | Description |
|--------|-------------|
| `.And(rule)` | Combine with AND logic |
| `.Or(rule)` | Combine with OR logic |
| `.Not()` | Invert rule result |
| `.ToAsyncRule()` | Convert sync to async |
| `.ToSyncRule()` | Convert async to sync (blocking) |
| `.WithCache(TimeSpan)` | Add caching |
| `.WithCondition(Func)` | Add conditional execution |

---

## License

MIT - use it freely in your projects.
