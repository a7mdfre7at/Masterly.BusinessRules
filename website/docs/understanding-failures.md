---
id: understanding-failures
title: "Understanding Failures"
---

When a rule fails, you usually want two answers. Which part of a combined rule failed? And what exactly was wrong? Explain trees answer the first question. Rich results answer the second.

## Explain trees

Call `Explain` to see why a combined rule passed or failed. It runs the rule once and returns a tree with one node for each rule it visited. Use it in logs, support tools and tests.

```csharp
// context and the three KYC rules come from your application
GroupBusinessRule kyc = RuleGroup.All(
        "KYC.INCOMPLETE",
        "All KYC checks must pass",
        identityVerified,
        addressVerified,
        sanctionsCleared)
    .WithShortCircuit();

RuleExplanation explanation = kyc.Explain(context);
Console.WriteLine(explanation.ToText());
```

The output looks like this (durations vary):

```text
[BROKEN] ALL  KYC.INCOMPLETE  "All KYC checks must pass"  (0.9 ms)
 ├─ [PASSED]  KYC.ID  (0.1 ms)
 ├─ [BROKEN]  KYC.ADDRESS  "Address must be verified"  (0.2 ms)
 └─ [NOT EVALUATED]  KYC.SANCTIONS  reason: short-circuit
```

Each line reads `[OUTCOME] OPERATOR  CODE  "message"  (duration)  reason: ...`. The message appears only on broken nodes. The duration appears only on nodes that actually ran.

Both sync and async rules can be explained:

| Call | Notes |
|------|-------|
| `rule.Explain(context, captureExceptions)` | Both parameters are optional. The context defaults to `null`. |
| `await asyncRule.ExplainAsync(context, cancellationToken)` | The context is required. A `null` context throws `ArgumentNullException`. |
| `await asyncRule.ExplainAsync(context, captureExceptions, cancellationToken)` | Same, with exception capture. |

Each node has an `Outcome`:

| Outcome | Meaning | Counts as broken |
|---------|---------|------------------|
| `Passed` | The rule ran and passed. | No |
| `Broken` | The rule ran and is broken. | Yes |
| `Skipped` | A wrapper did not run the rule: a `.When(...)` precondition was false, or a feature gate was closed. | No |
| `CacheHit` | A cached rule returned its stored verdict without running. | The cached verdict |
| `NotEvaluated` | The rule was skipped by a short-circuit, or a custom wrapper never ran it. | No |
| `Faulted` | The rule threw, and you passed `captureExceptions: true`. | Yes |

A node also gives you:

- `Code`, `Message` and `Severity` of the rule.
- `IsBroken`: whether the node counted as broken for its parent.
- `Operator`: the label of a combined or wrapped node, such as `AND`, `OR`, `NOT`, `ALL`, `ANY`, `AT LEAST 2`, `EXACTLY 1`, `CACHED`, `WHEN`, `SEVERITY`, `GATED`, `SHADOW` or `ADAPTER`. It is empty for a plain rule.
- `Reason`: why the outcome happened, such as `short-circuit`, `precondition not met` or `cache hit`.
- `Duration`, `Fault` (the captured exception) and `Children`.
- `BrokenLeaves()`: the deepest broken nodes, which are usually the rules you need to report.

```csharp
// explanation is from the example above
foreach (RuleExplanation failure in explanation.BrokenLeaves())
{
    Console.WriteLine($"{failure.Code}: {failure.Message}"); // KYC.ADDRESS: Address must be verified
}
```

Your own rule classes, and any rule that is not a combination or a library wrapper, appear as a single node.

:::note

`Explain` is a real evaluation, not a dry run. Each rule runs exactly once, and wrappers behave as they do in `Check`. For example, a cached rule can serve or store its verdict.

:::

**Capturing exceptions.** By default, an exception thrown by a rule propagates out of `Explain`. Pass `captureExceptions: true` to record it in the tree instead:

```csharp
// kyc and context are from the first example in this section
RuleExplanation explanation = kyc.Explain(context, captureExceptions: true);
```

- The throwing rule becomes a `Faulted` node. Its `Fault` holds the exception, and its `Reason` reads `ExceptionType: message`.
- A faulted node counts as broken for its parent, and the rest of the tree is still evaluated.
- Cancellation (`OperationCanceledException`) is never captured. It always propagates.
- A cached rule never stores a verdict that rests on a captured exception.

:::info[Advanced]

A wrapper you write yourself shows up as a single node. To show it as its own node with the inner rule's subtree below it, implement `IExplainableDecoratedBusinessRule` (async: `IExplainableDecoratedAsyncBusinessRule`). Give it an `ExplanationOperator` label and implement `EvaluateDecorated(evaluation)`, calling `evaluation.EvaluateInner()` to run the inner rule.

:::

## Rich results

A message such as "Transaction limit exceeded" says that something failed, not what. A rich result also says which value broke the rule, what was expected and how to fix it.

```csharp
public sealed class TransactionLimitRule : BaseBusinessRule
{
    private readonly decimal _amount;
    private readonly decimal _limit;

    public TransactionLimitRule(decimal amount, decimal limit)
    {
        _amount = amount;
        _limit = limit;
    }

    public override string Code => "LIMIT.EXCEEDED";

    public override string Message => "Transaction limit exceeded.";

    public override string Category => "Payments";

    public override IReadOnlyList<string> Tags => new[] { "customer-facing" };

    public override bool IsBroken() => _amount > _limit;

    // Runs only when the rule is broken
    protected override RuleResultDetails Describe(BusinessRuleContext? context) => RuleResultDetails
        .Detail($"Amount {_amount:N2} exceeds the limit of {_limit:N2}.")
        .Actual(_amount)
        .Expected($"<= {_limit}")
        .Remediation("Reduce the amount or request a limit increase.")
        .With("currency", "USD");
}
```

Every `BusinessRuleResult` carries these members:

| Member | Content |
|--------|---------|
| `Code`, `Message`, `Severity` | The rule's metadata. |
| `Name`, `Category`, `Tags` | The rule's metadata, so you can filter results without looking the rule up again. |
| `Detail` | A formatted sentence about this violation, or `null`. |
| `Actual`, `Expected` | The value that broke the rule and the expected value or range, or `null`. |
| `Remediation` | A hint on how to fix the violation, or `null`. |
| `Data` | Extra key/value pairs. Empty by default. |
| `Details` | The broken members of a combined rule. Empty for a plain rule. |

**Describing a violation.** Override `Describe(context)` on `BaseBusinessRule` or `BaseAsyncBusinessRule` to fill in the details. Subject-typed rules override `Describe(subject, context)`.

- Start with `RuleResultDetails.Detail($"...")` or `RuleResultDetails.Empty`. Then chain `.Actual(...)`, `.Expected(...)`, `.Remediation(...)` and `.With(key, value)`. Each call returns a new object.
- The library calls `Describe` only when the rule is broken. It passes the same context the rule was evaluated with.
- If `Describe` throws, the result is still reported, just without details. A bug in `Describe` never hides the violation. Cancellation still propagates.
- `Detail` is an interpolated string. It is formatted with `context.FormatProvider` when you set one, and with the invariant culture otherwise. `Message` is a plain string and is never formatted.
- `Actual` and `Expected` are stored as you pass them (`object?`).
- Wrappers keep the details. `.Cached(...)` and `.When(...)` return the inner rule's result, and `.WithSeverity(...)` changes only the severity.

:::note

On a sync rule, `Evaluate()` and `Evaluate(context)` build the result, including `Describe`, without checking whether the rule is broken. Call them only after `IsBroken` returned `true` (see [Three ways to run a rule](./writing-rules.md#three-ways-to-run-a-rule)).

:::

To format the detail sentence for the caller's culture, set the provider on the context:

```csharp
BusinessRuleContext context = new BusinessRuleContext();
context.FormatProvider = CultureInfo.GetCultureInfo("de-DE");

IBusinessRule limit = new TransactionLimitRule(5200m, 5000m);
BusinessRuleResult result = limit.Evaluate(context);
// result.Detail: "Amount 5.200,00 exceeds the limit of 5.000,00."
```

**Details with the builder.** Builder rules get the same details from `WithDetail`, `WithActual`, `WithExpected` and `WithRemediation`. Each one takes a function of the context. `WithExpected` and `WithRemediation` also accept a fixed value.

```csharp
IBusinessRule limit = BusinessRuleBuilder.Create("LIMIT.EXCEEDED")
    .WithMessage("Transaction limit exceeded.")
    .WithCategory("Payments")
    .WithTags("customer-facing")
    .When(ctx => ctx.Get<decimal>("amount") > ctx.Get<decimal>("limit"))
    .WithDetail(ctx => $"Amount {ctx.Get<decimal>("amount"):N2} exceeds the limit of {ctx.Get<decimal>("limit"):N2}.")
    .WithActual(ctx => ctx.Get<decimal>("amount"))
    .WithExpected(ctx => $"<= {ctx.Get<decimal>("limit")}")
    .WithRemediation("Split the transaction.")
    .Build();
```

- The functions run only when the rule is broken. If one throws, the result is reported without details.
- When the rule runs without a context, the functions receive an empty context.
- The builder has no equivalent of `.With(key, value)`. To fill `Data`, write a rule class with `Describe`.

**Filtering results.** The exception and the evaluation results let you pick out results by category or tag:

```csharp
// rules (a List<IBusinessRule>) and context come from your application
try
{
    BusinessRuleChecker.CheckAll(rules, context: context);
}
catch (BusinessRuleValidationException exception)
{
    IEnumerable<BusinessRuleResult> paymentErrors = exception.ByCategory("Payments");
    IEnumerable<BusinessRuleResult> customerFacing = exception.ByTag("customer-facing");
}
```

- `ByCategory(category)` matches exactly and is case-sensitive.
- `ByTag(tag)` ignores case.
- Both exist on `BusinessRuleValidationException`, on the `EvaluateAll` result and on the `EvaluateAllAsync` result.
- `result.HasTag(tag)` checks a single result and also ignores case.

:::note

The filters look only at the top-level results. Groups and other combined rules have no category or tags, so a broken rule inside a group is not matched. Filter the flattened results instead: `exception.Flatten().Where(result => result.HasTag("customer-facing"))`.

:::

**Returning Problem Details.** A result holds everything an RFC 7807 Problem Details response needs. This ASP.NET Core helper turns the exception into a 422 response body:

```csharp
private static ProblemDetails ToProblemDetails(BusinessRuleValidationException exception)
{
    ProblemDetails problem = new ProblemDetails
    {
        Title = "One or more business rules were violated.",
        Status = StatusCodes.Status422UnprocessableEntity,
    };

    problem.Extensions["errors"] = exception.Flatten()
        .Select(result => new
        {
            code = result.Code,
            category = result.Category,
            message = result.Message,
            detail = result.Detail,           // "Amount 5,200.00 exceeds the limit of 5,000.00."
            actual = result.Actual,           // 5200
            expected = result.Expected,       // "<= 5000"
            remediation = result.Remediation,
            data = result.Data,
        })
        .ToList();

    return problem;
}
```

In a controller, catch the exception and return `UnprocessableEntity(ToProblemDetails(exception))`. `Flatten()` makes sure that each broken rule inside a group gets its own entry.
