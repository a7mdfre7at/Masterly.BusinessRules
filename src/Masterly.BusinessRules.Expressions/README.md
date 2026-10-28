# Masterly.BusinessRules.Expressions

Write a business rule's condition as text, such as `amount > @maxOrderAmount`, and explain every verdict with the values that produced it. Use it when conditions live in configuration, a database or an admin screen.

This is a companion package of [Masterly.BusinessRules](https://www.nuget.org/packages/Masterly.BusinessRules), a composable business-rule engine for .NET.

[![NuGet](https://img.shields.io/nuget/v/Masterly.BusinessRules.Expressions?style=flat-square)](https://www.nuget.org/packages/Masterly.BusinessRules.Expressions)

## Install

```bash
dotnet add package Masterly.BusinessRules.Expressions
```

- **Target framework:** `netstandard2.1` (.NET Core 3.0+, .NET 5+)
- **Dependencies:** `Masterly.BusinessRules` only

## Example

```csharp
using Masterly.BusinessRules;
using Masterly.BusinessRules.Expressions;

// Parse once and reuse: a RuleExpression is immutable, and bad text throws here, not at evaluation.
RuleExpression expression = RuleExpression.Parse("amount > maxOrderAmount");
ExpressionBusinessRule rule = expression.ToRule("ORDER.TOO_LARGE", "Order exceeds the limit.");

BusinessRuleContext context = new BusinessRuleContext();
context.Set("amount", 12_000m);
context.Set("maxOrderAmount", 10_000m);

bool isBroken = rule.IsBroken(context);   // true: the text describes when the rule is broken
rule.Check(context);                      // throws BusinessRuleValidationException
```

An expression rule always needs a context. It works everywhere other rules do: composition, groups, decorators and the batch checkers. `BusinessRuleBuilder.Create(code).WhenExpression(text)` gives you the builder's name, category, tags, details and declared `@parameters` on top.

## Documentation

Expression syntax, comparisons, parameters, safety limits and explanations are covered in the full documentation: <https://github.com/a7mdfre7at/Masterly.BusinessRules#readme>
