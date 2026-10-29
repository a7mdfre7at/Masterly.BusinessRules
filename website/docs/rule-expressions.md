---
id: rule-expressions
title: "Rule Expressions"
---

The `Masterly.BusinessRules.Expressions` package lets you write a rule's condition as text, such as `amount > @maxOrderAmount && customer.Tier != 'VIP'`. Use it when conditions live in configuration, a database or an admin screen, and when you want to explain a verdict with the actual values that produced it.

```bash
dotnet add package Masterly.BusinessRules.Expressions
```

The package depends only on the core package and targets `netstandard2.1`.

```csharp
using Masterly.BusinessRules;
using Masterly.BusinessRules.Expressions;

// customer comes from your application; customer.Tier is CustomerTier.Gold
RuleExpression expression = RuleExpression.Parse("amount > @maxOrderAmount && customer.Tier != 'VIP'");

BusinessRuleContext context = new BusinessRuleContext();
context.Set("amount", 12_000m);
context.Set("customer", customer);
context.Set("maxOrderAmount", 10_000m);

ExpressionBusinessRule rule = expression.ToRule("ORDER.TOO_LARGE", "Order exceeds the customer's limit.");

bool isBroken = rule.IsBroken(context);   // true
rule.Check(context);                      // throws BusinessRuleValidationException
```

The text describes when the rule is **broken**, like every condition in this library. A `RuleExpression` is immutable, so parse it once and reuse it.

Names in the text bind to the context like this:

- `amount` reads the context entry with the key `amount`. Context keys are case-sensitive.
- `customer.Tier` reads public properties. A `null` anywhere in the path gives `null` instead of throwing.
- `Data` reads the typed data of a `BusinessRuleContext<T>`. Without typed data, it reads a context entry named `Data`.
- `@maxOrderAmount` is a parameter. Parameters are described below.

**Turning text into a rule.** You have three options. Text is parsed when you call `RuleExpression.Parse` or `WhenExpression`, so malformed text fails when you create the rule, not when it first runs.

| You want | Use | You get |
|----------|-----|---------|
| A sync rule | `expression.ToRule(code, message, parameters?, severity?)` | `ExpressionBusinessRule` |
| An async rule | `expression.ToAsyncRule(code, message, asyncParameters?, parameters?, severity?)` | `ExpressionAsyncBusinessRule` |
| Builder features (name, category, tags, details, declared parameters) | `.WhenExpression(text)` on `BusinessRuleBuilder` or `AsyncBusinessRuleBuilder` | `Build()` returns an `ExpressionBusinessRule` or `ExpressionAsyncBusinessRule` |

```csharp
IBusinessRule orderLimit = BusinessRuleBuilder.Create("ORDER.TOO_LARGE")
    .WithMessage("Order exceeds the customer's limit.")
    .WithCategory("Orders")
    .WithParameter("maxOrderAmount", 10_000m)
    .WhenExpression("amount > @maxOrderAmount")
    .Build();
```

Expression rules work everywhere other rules do: combined rules, groups, wrappers and the checkers. Their `Description` is the expression text unless you set one.

:::note

An expression rule always needs a context. `IsBroken()` and `Check()` without a context throw `InvalidOperationException`, and so does a checker or combined rule that evaluates the rule without one. Always pass a `BusinessRuleContext`.

:::

:::note

On a builder, the last condition wins. A `When` or `WhenAsync` call after `WhenExpression` replaces the expression, and the other way round.

:::

**Explaining a verdict.** An explanation shows the value bound to every name and the result of every step:

```csharp
// rule and context are from the first example in this section
RuleExpressionExplanation why = rule.ExplainExpression(context);

Console.WriteLine(why.Rendered);
// (amount(12000) > @maxOrderAmount(10000) [true]) && (customer.Tier(Gold) != 'VIP' [true]) => true
```

How to read `Rendered`:

- `amount(12000)`: the value bound to that name.
- `[true]` after a sub-expression: its result. `[not evaluated]` means short-circuiting skipped it.
- `=> true` at the end: the verdict.

The explanation also has `Result` (the verdict) and `Steps` (the bindings and operations in evaluation order). It comes from the same evaluation that produces the verdict, so the two can never disagree. Values are formatted with `RuleExpressionOptions.Culture`, which is the invariant culture by default.

| To explain | Call |
|------------|------|
| A `RuleExpression` | `Explain(context)` or `ExplainAsync(context, ...)` |
| An `ExpressionBusinessRule` | `ExplainExpression(context)` |
| An `ExpressionAsyncBusinessRule` | `ExplainExpressionAsync(context, redact, cancellationToken)` |

To keep personal data out of explanations, pass a redactor. It receives each path as written (`customer.Ssn`, or `@maxOrderAmount` with the `@`) and its value, and returns the text to show:

```csharp
// rule and context are from the first example in this section
RuleExpressionExplanation masked = rule.ExplainExpression(
    context,
    redact: (name, value) => name == "customer.Ssn" ? "***" : value?.ToString());
```

The redactor runs before anything is recorded, so the real value never reaches `Steps` or `Rendered`.

:::note

When you pass a redactor, it formats every value, not only the masked ones. Its string is shown as is and `Culture` is not applied, so return a readable string for the values you do not mask.

:::

**Parameters (`@name`).** A parameter reads a named setting, such as a per-tenant limit. The first source that has a value wins:

1. Parameters declared on the builder with `WithParameter` (rules built with `WhenExpression`).
2. A provider stored in the context under `RuleParameters.AsyncContextKey` (async evaluation only), then under `RuleParameters.ContextKey`.
3. The provider you passed to `ToRule`, `ToAsyncRule`, `Evaluate` or the condition compiler. An async provider is asked before a sync one.
4. The context entry with the same name, without the `@`.

If no source has a value, evaluation throws `RuleExpressionBindingException`. Providers read the scope stored under `RuleParameters.ScopeKey`, as in the rest of the library.

```csharp
// expression and context are from the first example; tenantSettings is an IAsyncRuleParameterProvider;
// cancellationToken comes from your application
ExpressionAsyncBusinessRule asyncRule = expression.ToAsyncRule(
    "ORDER.TOO_LARGE",
    "Order exceeds the customer's limit.",
    asyncParameters: tenantSettings);

await asyncRule.CheckAsync(context, cancellationToken);
```

An async rule resolves all its parameters before it evaluates the text, so each async provider is awaited once per evaluation.

**Errors.** Each failure has its own exception type:

| Exception | Thrown when | Useful members |
|-----------|-------------|----------------|
| `RuleExpressionParseException` | The text is malformed, exceeds a safety limit, or `Compile<T>` meets an unknown `Data` member | `Line`, `Column` (1-based) |
| `RuleExpressionBindingException` | A name, parameter or property cannot be bound during evaluation | `Identifier`, `AvailableKeys` (sorted) |
| `RuleExpressionEvaluationException` | An operator cannot handle its operands, the result is not a boolean, or decimal arithmetic divides by zero or overflows | `NodeText` |

```csharp
// RuleExpression.Parse("amount >") throws RuleExpressionParseException with Line 1, Column 9.
// A typo in a context key throws at evaluation:
// RuleExpressionBindingException: 'amuont' could not be bound; available: amount, customer, maxOrderAmount.

// row is a rule definition loaded from your database
if (!RuleExpression.TryParse(row.Condition, RuleExpressionOptions.Default, out RuleExpression? parsed))
{
    // reject the definition
}
```

`TryParse` returns `false` instead of throwing a parse error. Binding errors cannot be caught at parse time, because the parser does not know your context. To check names up front, read the parsed expression's `Identifiers` (the root names) and `ParameterNames` (without the `@`).

**Advanced: condition compiler and compiled delegates.** Most applications need neither of these. Use the condition compiler when you register it as a service, and `Compile<T>` when you want a typed delegate.

*Rules loaded from configuration.* `RuleExpressionConditionCompiler` plugs text into the plain `When(compiler, text)` builder overload. This is handy when you register the compiler in a container:

```csharp
// row is a rule definition loaded from your database; parameters is an IRuleParameterProvider
RuleExpressionConditionCompiler compiler = new RuleExpressionConditionCompiler(RuleExpressionOptions.Default, parameters);

IBusinessRule fromConfig = BusinessRuleBuilder.Create(row.Code)
    .WithMessage(row.Message)
    .When(compiler, row.Condition)
    .Build();
```

The result is an ordinary built rule, not an `ExpressionBusinessRule`, so it has no `ExplainExpression`. Its `@parameters` come from the compiler's providers, not from `WithParameter` on the builder. Prefer `WhenExpression` unless you need the compiler as a service. `When(compiler, text)` also compiles the text when you call it, so bad text throws `RuleExpressionParseException` while you build the rule, not when it first runs.

*Typed delegates with `Compile<T>`.* When the subject has a fixed type, `Compile<T>()` turns the expression into a delegate over `BusinessRuleContext<T>`. Paths that start with `Data` are checked against `T` when you compile, so a typo fails early:

```csharp
// order comes from your application
Func<BusinessRuleContext<Order>, bool> hasNoItems = RuleExpression.Parse("Data.Items.Count == 0").Compile<Order>();

bool isEmpty = hasNoItems(new BusinessRuleContext<Order>(order));

// Throws RuleExpressionParseException from Compile<Order>(), because Order has no Itemz property:
// RuleExpression.Parse("Data.Itemz.Count == 0").Compile<Order>();
```

- Compile once and keep the delegate.
- The delegate returns the same verdict as `Evaluate`. Other names and `@parameters` still bind at evaluation time.
- `Data` paths stay null-safe in the compiled delegate: a `null` anywhere in the path gives `null`, as in `Evaluate`.
- Pass a provider for `@parameters` with `Compile<Order>(parameters)`.
- `T` must be a reference type.
- The delegate gives no explanation. Use `Explain` for that.

:::note

`Compile<T>()` builds an expression tree, so it needs dynamic code. Under native AOT it falls back to the slower interpreted form. Everything else in this package emits no dynamic code, but it reads properties through reflection, so keep the members your expressions read from being trimmed.

:::

## Expression syntax

The table lists constructs from the loosest binding to the tightest.

| Construct | Syntax | Notes |
|-----------|--------|-------|
| Or | `a \|\| b`, `a or b` | Short-circuits; both sides must be booleans |
| And | `a && b`, `a and b` | Short-circuits; both sides must be booleans |
| Not | `!a`, `not a` | The operand must be a boolean |
| Comparison | `==` `!=` `~=` `<` `<=` `>` `>=` | `~=` is case-insensitive equality; comparisons do not chain |
| Membership | `x in [a, b, c]`, `x in path` | List items can be any expression; items are compared with `==` |
| Additive | `a + b`, `a - b` | `+` also joins two strings |
| Multiplicative | `a * b`, `a / b`, `a % b` | |
| Unary minus | `-a` | |
| Member access | `customer.Address.City`, `items.Count`, `name.Length` | A `null` segment gives `null` |
| Functions | `name.startsWith('A')`, `name.endsWith('son')`, `tags.contains('rush')` | Add `/ci` for case-insensitive: `name.startsWith/ci('a')`; `contains` works on strings and collections |
| Literals | `12000`, `12.5`, `'VIP'`, `"VIP"`, `true`, `false`, `null` | Numbers are decimals with `.` as the separator and no exponent; strings accept `\'` `\"` `\\` `\n` `\r` `\t` |
| Names | `amount`, `Data`, `@maxOrderAmount` | Context entry, typed data, parameter |
| Grouping | `( ... )` | |

- Keywords (`and`, `or`, `not`, `in`, `true`, `false`, `null`) and function names are case-insensitive.
- Context keys and `Data` are case-sensitive. Property names match exactly first, then ignoring case when only one property matches.
- The whole expression must produce a boolean. Anything else throws `RuleExpressionEvaluationException`.
- The only functions are `startsWith`, `endsWith` and `contains`. You cannot call other methods.

:::note

`not` and `!` bind more loosely than comparisons, unlike in C#. `!a == b` means `!(a == b)`. Add parentheses when you mean `(!a) == b`.

:::

## How values are compared

| Operands | Behaviour |
|----------|-----------|
| Two numbers (any numeric type, literals included) | Compared as `decimal`, or as `double` when either side is `float` or `double` |
| Decimal division by zero or overflow | Throws `RuleExpressionEvaluationException` |
| `NaN` (`double` or `float`) | Every ordering and `==` is false, and `!=` is true. So `x != x` detects `NaN`, and `score < threshold` never fires for it |
| Enum and string | Equal when the string is the member name, ignoring case (`tier == 'gold'`). Ordering an enum against a string throws |
| Enum and number | Compared and ordered by the underlying value (`tier > 1`) |
| Two enums | Same type: compared by value. Different types: never equal, and ordering throws |
| Two strings | Ordinal for `==`, `!=` and ordering. `~=` and the `/ci` functions ignore case |
| `null` | `null == null` is true and `null != x` is true. Ordering with a `null` side is false. Arithmetic with `null` gives `null`. A function with a `null` target or argument is false. `x in null` is false |
| `in` | Each item is compared with the `==` rules above, so `2.0 in [1, 2]` is true and `tier in ['vip', 'gold']` matches by name. The right side must be a list or collection; a string throws |
| `Count` / `Length` | A readable property with that name wins. Otherwise `Length` and `Count` work on strings, and `Count` on any `ICollection` |
| Other values | Same-type `IComparable` values (`DateTime`, `Guid`, ...) are ordered with `CompareTo`. Values of unrelated types are never equal; ordering or arithmetic across them throws |

## Safety limits

Expressions often come from people outside your codebase. `RuleExpressionOptions` limits what a parsed expression can do. Pass the options to `Parse`, `WhenExpression(text, options)` or the `RuleExpressionConditionCompiler` constructor.

```csharp
using Masterly.BusinessRules.Expressions;

// row is a rule definition loaded from your database
RuleExpressionOptions options = RuleExpressionOptions.Default
    .WithMaxLength(500)
    .WithMaxDepth(16)
    .WithRequireRuleAccessibleAttribute(true);

RuleExpression condition = RuleExpression.Parse(row.Condition, options);

public sealed class Customer
{
    [RuleAccessible]
    public CustomerTier Tier { get; set; }

    public string Ssn { get; set; } = string.Empty;   // not readable from an expression
}
```

| Option | Default | Effect |
|--------|---------|--------|
| `WithMaxLength(n)` | 4096 characters | Longer text throws `RuleExpressionParseException` |
| `WithMaxDepth(n)` | 64 levels | Deeper nesting throws `RuleExpressionParseException` |
| `WithMemberFilter(filter)` | none | A `Func<PropertyInfo, bool>` that must return `true` for every property an expression reads |
| `WithRequireRuleAccessibleAttribute(true)` | `false` | Only properties marked `[RuleAccessible]`, or declared on a type marked `[RuleAccessible]`, can be read |
| `WithCulture(culture)` | invariant culture | Formats values in explanations |

- Options are immutable. Each `With...` method returns a new instance.
- `WithMaxLength` and `WithMaxDepth` throw `ArgumentOutOfRangeException` for values below 1.
- When you set both a member filter and `RequireRuleAccessibleAttribute`, a property must pass both.
- Reading a blocked property throws `RuleExpressionBindingException`, which lists the properties that are readable.

Some limits always apply, whatever the options. An expression can read only public, non-static properties without indexer parameters. It cannot read fields, call methods other than the three built-in functions, or change any value.
