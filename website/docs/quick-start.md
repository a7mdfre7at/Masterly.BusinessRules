---
id: quick-start
title: "Quick Start"
description: "Define two rules, check one, then check both together. Your first working rule in five minutes."
---

This example defines two rules for an order, checks one of them, and then checks both together.

**1. Define a rule.** Derive from `BaseBusinessRule` and supply a code, a message and the condition. A rule is *broken* when `IsBroken()` returns `true`. Write the condition that describes the violation, not the valid state.

```csharp title="OrderRules.cs"
using Masterly.BusinessRules;

// Order and Customer are your own domain classes.
// Here Order.Items is a List<OrderLine> and Customer.IsActive is a bool.
public sealed class OrderMustHaveItemsRule : BaseBusinessRule
{
    private readonly Order _order;

    public OrderMustHaveItemsRule(Order order)
    {
        _order = order;
    }

    public override string Code => "ORDER.NO_ITEMS";

    public override string Message => "An order must contain at least one item.";

    public override bool IsBroken() => _order.Items.Count == 0;
}

public sealed class CustomerMustBeActiveRule : BaseBusinessRule
{
    private readonly Customer _customer;

    public CustomerMustBeActiveRule(Customer customer)
    {
        _customer = customer;
    }

    public override string Code => "CUSTOMER.INACTIVE";

    public override string Message => "The customer account must be active.";

    public override bool IsBroken() => !_customer.IsActive;
}
```

**2. Check one rule.** `IsBroken()` answers the question. `Check()` throws a `BusinessRuleValidationException` when the rule is broken and does nothing when it passes.

```csharp title="Check one rule"
// order comes from your application
OrderMustHaveItemsRule rule = new OrderMustHaveItemsRule(order);

bool isBroken = rule.IsBroken();

rule.Check();
```

**3. Check several rules.** `BusinessRuleChecker.CheckAll` evaluates every rule you pass. If any are broken, it throws one exception that lists all of them.

```csharp title="Check several rules"
// order and customer come from your application
try
{
    BusinessRuleChecker.CheckAll(
        new OrderMustHaveItemsRule(order),
        new CustomerMustBeActiveRule(customer));
}
catch (BusinessRuleValidationException exception)
{
    foreach (BusinessRuleResult broken in exception.BrokenRules)
    {
        Console.WriteLine($"{broken.Code} ({broken.Severity}): {broken.Message}");
    }
}
```

With an empty order and an inactive customer, this prints:

```text title="Console output"
ORDER.NO_ITEMS (Error): An order must contain at least one item.
CUSTOMER.INACTIVE (Error): The customer account must be active.
```

**Next steps**

- To get the results without an exception, call `BusinessRuleChecker.EvaluateAll` instead. See [Check all or evaluate all](./running-many-rules.md#check-all-or-evaluate-all).
- For rules that call a database or a web service, see [Async rules](./writing-rules.md#async-rules).
- To combine rules, see [Combining Rules](./combining-rules.md).
