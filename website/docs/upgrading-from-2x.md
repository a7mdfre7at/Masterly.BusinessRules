---
id: upgrading-from-2x
title: "Upgrading from 2.x"
sidebar_label: "Upgrading from 2.x"
---

Most 2.x code compiles against 3.0 without changes. A few behaviours changed, and every assembly built against 2.x must be rebuilt.

**Recompile: 3.0 is a binary break.** Rebuild every assembly that references Masterly.BusinessRules, including your own shared rule libraries. You do not need to change source code for this step.

If you skip it, code compiled against 2.x fails at run time with `MissingMethodException`. The reason: some members moved to new base types.

- `Code`, `Message`, `Severity`, `Name`, `Description`, `Category` and `Tags` moved from `IBusinessRule` / `IAsyncBusinessRule` to the new base interface `IBusinessRuleMetadata`.
- The builders' `WithMessage`, `WithSeverity`, `WithName`, `WithDescription`, `WithCategory` and `WithTags` moved to the new base class `BusinessRuleMetadataBuilder<TBuilder>`.
- `asyncRule.Cached(duration)` now returns `CachedAsyncBusinessRule` instead of `IAsyncBusinessRule`.

:::note

If you use a third-party package that was built against 2.x, you need a version of it built against 3.0.

:::

**Behaviour changes.** After rebuilding, review the changes below. Each row says who needs to act.

| What changed | What you need to do | Who is affected |
|--------------|---------------------|-----------------|
| A builder keeps only the **last** `When(...)` call. In 2.x, `When(() => ...)` and `When(ctx => ...)` on the same builder both stayed active. | Give each built rule exactly one condition. | Code that called `When` more than once on one `BusinessRuleBuilder`. |
| Sync `And`, `Or` and `Not` pass the context to their operands. In 2.x they always called the parameterless `IsBroken()`. | Check that operands which read the context still give the verdict you expect. | Combined rules whose `IsBroken(context)` differs from `IsBroken()`. |
| `CompositeBusinessRule.IsBroken` and `CompositeAsyncBusinessRule.IsBrokenAsync` evaluate every child. In 2.x they stopped at the first broken child. | If you need to stop early, use `RuleGroup.All(...).WithShortCircuit()`. | Composites with slow children or children that have side effects. |
| `And`, `Or` and `Not` return new node types (`AndBusinessRule` and so on). The result's `Name` is now the node type name, and its `Description` describes the operator. | Do not cast combined rules to `SimpleBusinessRule` or `CompositeAsyncBusinessRule`, and do not rely on their `Name`. Codes and messages are unchanged. | Code that inspects the type or `Name` of a combined rule. |
| The checkers wrap a failure to read the context in `BusinessRuleContextException`. The original exception is its `InnerException`. | Catch `BusinessRuleContextException` instead of `KeyNotFoundException` or `InvalidCastException` around checker calls. | Code that catches context errors around `CheckAll` / `EvaluateAll`. |
| `BusinessRuleContext.Get<T>` throws `ContextKeyNotFoundException` or `ContextValueTypeMismatchException`. Both derive from the old exception types, so existing `catch` blocks still work. A stored `null` read as a non-nullable value type is now a type mismatch, and a `null` key throws `ArgumentNullException`. | Usually nothing. Review code that relied on a `NullReferenceException` for a stored `null`. | Code that reads a stored `null` as a value type. |
| `new BusinessRuleResult(code, message, severity, null)` no longer compiles: the call is ambiguous between two new overloads. Both throw for `null`. | Use the three-argument constructor. | Code that constructs `BusinessRuleResult` directly. |
| The metadata members are declared on `IBusinessRuleMetadata`. | Look them up on the base interface. | Code that finds rule members by reflection. |
| Smaller fixes you might notice: a null rule in a batch throws `ArgumentException` instead of `NullReferenceException`; `CheckBySeverityAsync` and `CheckByCategoryAsync` honour their cancellation token; a built rule no longer sees later `WithTags` calls on its builder; group and composite results carry their broken members in `Details`. | Usually nothing. | Code or tests that depended on the old behaviour. |

:::note

`And` and `Or` behave exactly as they did in 2.x, but the 2.x README described them the wrong way round. `And` is broken only when **both** operands are broken. `Or` is broken when **either** operand is broken. If you wrote rules based on the old README, check them.

:::

For the full list of changes, see the [CHANGELOG](https://github.com/a7mdfre7at/Masterly.BusinessRules/blob/master/CHANGELOG.md).
