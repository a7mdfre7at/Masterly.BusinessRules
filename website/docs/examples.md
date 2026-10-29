---
id: examples
title: "Real-World Examples"
sidebar_label: "Real-World Examples"
---

The examples below combine several features in the way an application usually does. Each example is a complete service or controller. The rule classes it uses (such as `CartMustNotBeEmptyRule`) and the services it injects are your own.

## E-commerce checkout

A checkout runs cheap in-memory checks first, then slower checks that call other services. Run the slow checks in parallel to cut latency.

```csharp
public sealed class CheckoutService
{
    private readonly IAddressValidator _addressValidator;
    private readonly IPaymentService _paymentService;
    private readonly IInventoryService _inventoryService;
    private readonly IOrderRepository _orders;

    public CheckoutService(
        IAddressValidator addressValidator,
        IPaymentService paymentService,
        IInventoryService inventoryService,
        IOrderRepository orders)
    {
        _addressValidator = addressValidator;
        _paymentService = paymentService;
        _inventoryService = inventoryService;
        _orders = orders;
    }

    public async Task<Order> PlaceOrderAsync(
        Cart cart, Customer customer, PaymentDetails payment, Address address, CancellationToken cancellationToken)
    {
        // 1. Fast, in-memory checks. Throws before any remote call is made.
        BusinessRuleChecker.CheckAll(
            new CartMustNotBeEmptyRule(cart),
            new CustomerMustBeActiveRule(customer),
            new CustomerMustBeAdultRule(customer));

        // 2. Checks that call other services, evaluated at the same time.
        IAsyncBusinessRule[] remoteRules =
        {
            new AddressMustBeDeliverableRule(_addressValidator, address),
            new PaymentMustBeAuthorizedRule(_paymentService, payment),
            new StockMustCoverCartRule(_inventoryService, cart.Items),
        };

        AsyncRuleEvaluationOptions options = new AsyncRuleEvaluationOptions { RunInParallel = true };
        await AsyncBusinessRuleChecker.CheckAllAsync(new BusinessRuleContext(), remoteRules, options, cancellationToken);

        return await _orders.CreateAsync(cart, customer, payment, address, cancellationToken);
    }
}
```

What happens:

- If a sync rule is broken, `CheckAll` throws `BusinessRuleValidationException` with every broken rule. The remote checks never run.
- The three async rules run in parallel. `CheckAllAsync` waits for all of them, then throws one exception that lists every broken rule.

:::note

Rules running in parallel share one context, so they should only read from it. `StopOnFirstFailure = true` turns parallel evaluation off. See [Options](./running-many-rules.md#options).

:::

## User registration

Registration has one rule you can check right away and one that needs a database call. Build both with the fluent builders, so you do not need a class per rule.

```csharp
public sealed class RegistrationService
{
    private const int MinimumPasswordLength = 8;
    private readonly IUserRepository _users;

    public RegistrationService(IUserRepository users)
    {
        _users = users;
    }

    public async Task<User> RegisterAsync(RegistrationRequest request, CancellationToken cancellationToken)
    {
        IBusinessRule passwordTooShort = BusinessRuleBuilder.Create("PASSWORD.TOO_SHORT")
            .WithMessage($"Password must be at least {MinimumPasswordLength} characters.")
            .When(() => request.Password.Length < MinimumPasswordLength)
            .Build();

        passwordTooShort.Check();

        IAsyncBusinessRule emailTaken = AsyncBusinessRuleBuilder.Create("USER.EMAIL_TAKEN")
            .WithMessage("This email address is already registered.")
            .WhenAsync((context, token) => _users.EmailExistsAsync(request.Email, token))
            .Build();

        await emailTaken.CheckAsync(new BusinessRuleContext(), cancellationToken);

        return await _users.CreateAsync(request, cancellationToken);
    }
}
```

- `Check()` and `CheckAsync()` throw `BusinessRuleValidationException` when the rule is broken.
- Async rules always take a `BusinessRuleContext`. Pass an empty one when the rule does not read from it.
- To report several problems at once instead of stopping at the first, put the rules in one `BusinessRuleChecker.CheckAll` call.

## API controller validation

In an API you usually want to turn broken rules into an error response instead of an exception. `EvaluateAllAsync` returns the broken rules and lets you shape the response yourself.

```csharp
using Masterly.BusinessRules;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly IProductService _products;
    private readonly IInventoryService _inventory;
    private readonly IOrderService _orders;

    public OrdersController(IProductService products, IInventoryService inventory, IOrderService orders)
    {
        _products = products;
        _inventory = inventory;
        _orders = orders;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        IAsyncBusinessRule[] rules =
        {
            new ProductMustExistRule(_products, request.ProductId),
            new StockMustCoverQuantityRule(_inventory, request.ProductId, request.Quantity),
        };

        AsyncRuleEvaluationOptions options = new AsyncRuleEvaluationOptions { RunInParallel = true };
        AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(
            new BusinessRuleContext(), rules, options, cancellationToken);

        if (result.HasBrokenRules)
        {
            ProblemDetails problem = new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "The order breaks one or more business rules.",
            };
            problem.Extensions["errors"] = result.BrokenRules
                .SelectMany(broken => broken.Flatten())
                .Select(failure => new { code = failure.Code, message = failure.Message })
                .ToList();

            return UnprocessableEntity(problem);
        }

        OrderDto order = await _orders.CreateAsync(request, cancellationToken);
        return Created($"/api/v1/orders/{order.Id}", order);
    }
}
```

- `EvaluateAllAsync` does not throw for broken rules. Check `HasBrokenRules` or `AllPassed`, then read `BrokenRules`.
- A rule that throws an exception still fails the call by default. Set `OnFault` on the options to record faults instead.
- `Flatten()` gives each broken rule inside a group its own entry.
- For a fuller error body (detail, actual, expected, remediation), see [Rich results](./understanding-failures.md#rich-results).

## Multi-tenant order approval with an audit trail

This example approves an order against a per-tenant limit and keeps a readable record of why. It uses a subject-typed rule, a scoped parameter, a rule group and `Explain`.

```csharp
// OrderItemsRequiredRule is the rule from "Subject-Typed Rules": one instance serves every order.

public sealed class OrderApprovalService
{
    private const string LimitParameter = "maxOrderAmount";
    private const decimal DefaultLimit = 10_000m;

    private readonly IBusinessRule<Order> _withinTenantLimit;
    private readonly IAuditLog _audit;

    // tenantLimits holds the per-tenant values, for example an InMemoryRuleParameterProvider
    // filled with .Set("maxOrderAmount", 50_000m, RuleScope.Tenant("acme")).
    public OrderApprovalService(IRuleParameterProvider tenantLimits, IAuditLog audit)
    {
        _audit = audit;
        _withinTenantLimit = BusinessRuleBuilder<Order>.Create("ORDER.TOO_LARGE")
            .WithMessage("Order exceeds the tenant limit.")
            .WithParameters(tenantLimits)
            .WithParameter(LimitParameter, DefaultLimit)
            .WithActual((order, context) => order.Total)
            .When((order, context, parameters) => order.Total > parameters.Get<decimal>(LimitParameter))
            .Build();
    }

    public IReadOnlyList<string> Approve(Order order, string tenantId)
    {
        // The scope on the context decides which tenant's limit applies.
        BusinessRuleContext context = new BusinessRuleContext();
        context.Set(RuleParameters.ScopeKey, RuleScope.Tenant(tenantId));

        IBusinessRule approval = RuleGroup.All("ORDER.REJECTED", "The order cannot be placed.",
            new OrderItemsRequiredRule().For(order),
            _withinTenantLimit.For(order));

        // One evaluation returns both the verdict and the audit trail.
        RuleExplanation explanation = approval.Explain(context);
        _audit.Write(explanation.ToText());

        return explanation.BrokenLeaves()
            .Select(leaf => $"{leaf.Code}: {leaf.Message}")
            .ToList();
    }
}
```

- `.For(order)` binds a subject-typed rule to one order, so it can join a group with other rules.
- A tenant without its own value gets the fallback, `10_000m`.
- `Explain` evaluates the rule once. `ToText()` renders the whole tree, and `BrokenLeaves()` returns only the rules that actually failed.
- If a rule throws, `Explain` rethrows by default. Pass `captureExceptions: true` to record the fault in the tree instead.
