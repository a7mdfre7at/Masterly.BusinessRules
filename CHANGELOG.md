## [3.0.0] - 2026-10-28

Major release: composed rules become real trees you can explain, batches record an evaluation ledger, and five opt-in companion packages ship beside the zero-dependency core. Assemblies compiled against 2.x must be recompiled.

Version 3 turns composed rules into real trees, adds an evaluation ledger, subject-typed rules, specifications, feature-gated rules and scoped parameters to the core, and ships five opt-in companion packages (Diagnostics, Expressions, DependencyInjection, FeatureManagement, Testing) from the same repository and version. The core keeps zero dependencies.

Upgrading from 2.x: follow the step-by-step guide in the README, [Upgrading from 2.x](https://github.com/a7mdfre7at/Masterly.BusinessRules#upgrading-from-2x).

### Breaking Changes

- `And()`, `Or()` and `Not()` still return `IBusinessRule` / `IAsyncBusinessRule`, but the rule they produce at run time is now a structural node, `AndBusinessRule`, `OrBusinessRule` or `NotBusinessRule` (async: `AndAsyncBusinessRule`, `OrAsyncBusinessRule`, `NotAsyncBusinessRule`), instead of a `SimpleBusinessRule` / delegate `CompositeAsyncBusinessRule`. This is a behaviour change, not a binary break. The truth tables, synthesised codes (`A+B`, `A|B`, `!A`) and messages are unchanged; `Name` reports the node type name (for example `AndBusinessRule`) and `Description` is a short operator description, so code that type-checks a composed rule (`is SimpleBusinessRule`) or reads its `Name` / `Description` sees different values
- Sync `And()`/`Or()`/`Not()` forward the context passed to `IsBroken(context)`/`Check(context)` to their operands; previously they always called the parameterless `IsBroken()`
- `CompositeBusinessRule` and `CompositeAsyncBusinessRule` implement `IComposedBusinessRule` / `IComposedAsyncBusinessRule` with `Operator = RuleOperator.All`, and `IsBroken` / `IsBrokenAsync` evaluate every child (previously they stopped at the first broken child; `Check`/`EvaluateAll` already did)
- `BusinessRuleChecker.CheckAll`/`EvaluateAll` evaluate in-library composed and decorated rules through a single internal evaluation instead of `IsBroken()` followed by `Evaluate()`, so the reported result for a group or composite carries its broken members in `Details`
- `SyncToAsyncRuleAdapter` and `AsyncToSyncRuleAdapter` evaluate the wrapped rule through its `Evaluate`/`EvaluateAsync` path so member details flow across the adapter; the blocking behaviour of `AsyncToSyncRuleAdapter` is unchanged
- `IBusinessRuleMetadata` is a real interface (`Code`, `Message`, and defaulted `Severity`, `Name`, `Description`, `Category`, `Tags`) extended by `IBusinessRule`, `IAsyncBusinessRule`, `IBusinessRule<TSubject>` and `IAsyncBusinessRule<TSubject>`; the seven metadata members moved from the two non-generic interfaces to the base. Implementors are source-compatible, but code that enumerated the members of `IBusinessRule` by reflection sees them on the base interface
- The checkers surface a rule's context read failure as `BusinessRuleContextException` (original exception as `InnerException`) instead of letting a bare `KeyNotFoundException`/`InvalidCastException` propagate; code that caught those types around a checker call should catch `BusinessRuleContextException` or inspect `InnerException`
- `BusinessRuleContext.Get<T>(string)` throws `ContextKeyNotFoundException` / `ContextValueTypeMismatchException` instead of a bare `KeyNotFoundException` / `InvalidCastException` (both derive from the previous types, so existing `catch` blocks keep working); a stored null requested as a non-nullable value type is a type mismatch instead of a `NullReferenceException`, and a null key throws `ArgumentNullException`
- `new BusinessRuleResult(code, message, severity, null)` is ambiguous between the new `IEnumerable<BusinessRuleResult>` and `RuleResultDetails` overloads and must be typed; both overloads throw `ArgumentNullException` for null
- **Binary break: recompile assemblies built against 2.x.** The seven metadata members (`Code`, `Message`, `Severity`, `Name`, `Description`, `Category`, `Tags`) moved from `IBusinessRule` / `IAsyncBusinessRule` to their new base `IBusinessRuleMetadata`, and `asyncRule.Cached(duration)` returns `CachedAsyncBusinessRule` instead of `IAsyncBusinessRule`. Source code compiles unchanged, but IL compiled against 2.x (for example a shared rules package) that reads rule metadata through `IBusinessRule` / `IAsyncBusinessRule` or calls the async `Cached(duration)` throws `MissingMethodException` at run time until it is rebuilt against 3.0.0. These are the only binary breaks: `And`/`Or`/`Not` keep their declared return types, the sync `Cached(duration)` already returned `CachedBusinessRule`, and a 2.x-compiled builder chain still runs although the `With*` methods are now declared on `BusinessRuleMetadataBuilder<TBuilder>`
- `asyncRule.Cached(duration)` returns `CachedAsyncBusinessRule` (like the sync `Cached()`), so `InvalidateCacheAsync` and `CachesPerKey` are reachable; source-compatible, but a binary break (see above)
- `BusinessRuleBuilder.When(...)`: the last `When` call wins across all four overloads (parameterless, context, parameters, compiled text), as `AsyncBusinessRuleBuilder.WhenAsync` already did. In 2.x a `When(() => ...)` and a `When(ctx => ...)` set on the same builder coexisted (the parameterless one answered `IsBroken()`, the context one `IsBroken(context)`), and an earlier parameters condition silently outlived a later `When`; set one condition per rule

### Added

#### Core (`Masterly.BusinessRules`)

**Structural composition and explain trees**

- `IComposedBusinessRule` / `IComposedAsyncBusinessRule` (`RuleOperator Operator`, `Operands`) and `IDecoratedBusinessRule` / `IDecoratedAsyncBusinessRule` (`InnerRule`): the walker interfaces implemented by every And/Or/Not node, group and composite, and by the cached, conditional, severity-override and feature-gated decorators. Both adapters expose their wrapped rule through an `InnerRule` property typed as the adapted side
- `RuleGroup.All/Any/AtLeast/Exactly` and `AsyncRuleGroup.All/Any/AtLeast/Exactly` returning the immutable `GroupBusinessRule` / `GroupAsyncBusinessRule` with `WithShortCircuit()`, `WithSeverity()` and (async) `WithParallelEvaluation()`; parallel plus short-circuit throws `InvalidOperationException`, parallel groups await every member so faults are observed, and empty member lists, null members and out-of-range thresholds are rejected at construction
- `RuleGroupQuantifier`, `RuleOperator` and `RuleOutcome` enums
- `RuleExplanation` tree node (`Rule`/`AsyncRule`, `Code`, `Message`, `Severity`, `Outcome`, `IsBroken`, `Operator`, `Reason`, `Duration`, `Fault`, `Children`, `BrokenLeaves()`, `ToText()`) and the `rule.Explain(context = null, captureExceptions = false)` / `asyncRule.ExplainAsync(context, [captureExceptions], ct)` extensions: one real evaluation with the trace as a by-product; decorators report `Skipped` / `CacheHit`, short-circuited operands `NotEvaluated`, throwing nodes `Faulted` only when `captureExceptions` is true (cancellation always propagates), third-party rules are single leaves and adapters expand into the other side's subtree
- `IExplainableDecoratedBusinessRule` / `IExplainableDecoratedAsyncBusinessRule` (`ExplanationOperator`, `EvaluateDecorated(DecoratedRuleEvaluation)` / `EvaluateDecoratedAsync(AsyncDecoratedRuleEvaluation, ct)`): the public seam that lets a decorator written outside the library take part in the walk. The walkers route such a decorator through `EvaluateDecorated` on every path (composition operands, groups, `Explain`) and record it as a node labelled `ExplanationOperator` whose child is the inner rule's own subtree; an inner rule the decorator never evaluates is recorded as `NotEvaluated` with the reason `not evaluated by decorator`
- `DecoratedRuleEvaluation` / `AsyncDecoratedRuleEvaluation` (`InnerRule`, `Context`, `IsExplaining`, `IsInnerEvaluated`, `EvaluateInner()` / `EvaluateInnerAsync(ct)`): the one-shot evaluation handed to that seam, evaluating the inner rule through the library walker at most once (composed inner rules keep their `Details`); the public constructor lets a decorator implement its own `IsBroken` through `EvaluateDecorated`
- `BusinessRuleResult.Details`, the constructor `BusinessRuleResult(code, message, severity, IEnumerable<BusinessRuleResult> details)` and `Flatten()` on `BusinessRuleResult` and `BusinessRuleValidationException`, returning the leaf results of nested details

**Evaluation ledger, options and fault policy**

- `RuleEvaluationEntry` (`Order`, `Rule`/`AsyncRule`, `Code`, `Outcome`, `Result`, `Fault`, `Duration`, `IsBroken`, `IsFaulted`) and, on `RuleEvaluationResult` and `AsyncRuleEvaluationResult`, `Entries`, `Faulted`, `HasFaults`, `TotalDuration`, `Slowest`, `ThrowIfAtLeast(severity)` and the constructor `(entries, totalDuration)` that derives `BrokenRules`/`PassedRules` from the ledger. Timing uses `Stopwatch.GetTimestamp()` tick math (no `Stopwatch` allocation)
- `RuleEvaluationOptions` (`StopOnFirstFailure`, `OnFault`, `ThrowOn`, `Observer`) and `AsyncRuleEvaluationOptions` (same plus `RunInParallel`; fail-fast wins over parallel) with options-taking `CheckAll`/`EvaluateAll` and `CheckAllAsync`/`EvaluateAllAsync` overloads. These overloads are generic (`CheckAll<TOptions>(rules[, context], TOptions options) where TOptions : RuleEvaluationOptions`, the async ones constrained to `AsyncRuleEvaluationOptions`), so a bare `null` or `default` argument cannot select them and 2.x calls such as `CheckAll(rules, default)` or `CheckAllAsync(context, rules, default)` bind exactly as before; both options classes are unsealed for that constraint. A null options instance throws `ArgumentNullException`
- `FaultPolicy` (`Throw` default, `Record`, `TreatAsBroken`; deliberately no `TreatAsPassed`): a throwing rule propagates after `OnRuleFaulted`, is recorded as a `Faulted` entry that is neither broken nor passed, or is additionally reported as a broken result built from its `Code`/`Message`/`Severity` (a throwing `Code` or `Message` getter falls back to the type name). Cancellation always propagates; faults are isolated per task in parallel async batches
- Severity threshold: `ThrowOn` makes `CheckAll`/`CheckAllAsync` throw only for broken rules at or above the severity; lower-severity broken rules travel on `BusinessRuleValidationException.CollectedRules` and the ledger on `BusinessRuleValidationException.Entries` (constructor `(brokenRules, collectedRules, entries)`). With fail-fast, only a rule at or above `ThrowOn` stops a `CheckAll` batch; `EvaluateAll` ignores `ThrowOn`
- `rule.Check(throwOn)`, `rule.Check(context, throwOn)` and `asyncRule.CheckAsync(context, throwOn, ct)` apply the threshold to a single rule
- `SeverityOverrideBusinessRule` / `SeverityOverrideAsyncBusinessRule` and the `rule.WithSeverity(severity)` extensions: report a different severity while forwarding every other metadata member, the verdict and member `Details`; a `SEVERITY` node in explain trees
- Cancellation and abort hooks as default interface members: `OnRuleCancelled(rule, exception)` (a rule threw `OperationCanceledException`; never reported as a fault) and `OnBatchAborted(exception)`, raised once in place of `OnBatchCompleted` when a started batch ends by an exception (a cancellation, a fault propagating under `FaultPolicy.Throw`, a null rule), so an observer that opens a span or a scope per batch can always close it; async twins `OnRuleCancelledAsync` / `OnBatchAbortedAsync`, called with `CancellationToken.None`
- Observer batch hooks as default interface members: `OnBatchStarted(ruleCount)`, `OnRuleFaulted(rule, exception)`, `OnAfterEvaluate(rule, isBroken, elapsed)`, `OnBatchCompleted(result)` and the async `OnBatchStartedAsync`, `OnRuleFaultedAsync`, `OnAfterEvaluateAsync(rule, isBroken, elapsed, ct)`, `OnBatchCompletedAsync`; existing observers keep compiling

**Rich results**

- `BusinessRuleResult.Name`, `Category` and `Tags`, populated at every construction site; constructors `(code, message, severity, name, category, tags)` and `(code, message, severity, name, category, tags, details, memberDetails, formatProvider)`; `HasTag(tag)` (case-insensitive) and `WithSeverity(severity)`
- `ByTag(tag)` (case-insensitive) and `ByCategory(category)` (ordinal) filters on `BusinessRuleValidationException`, `RuleEvaluationResult` and `AsyncRuleEvaluationResult`
- `RuleResultDetails`, an immutable fluent bag (`Detail($"...")`, `.Actual(value)`, `.Expected(value)`, `.Remediation(text)`, `.With(key, value)`, `Empty`, `IsEmpty`, `FormatDetail(provider)`), the matching `BusinessRuleResult.Detail`, `Actual`, `Expected`, `Remediation`, `Data` members, the constructor `(code, message, severity, RuleResultDetails details)` and `BusinessRuleContext.FormatProvider` (the `Detail` sentence is formatted with it, invariant culture otherwise)
- `protected virtual RuleResultDetails Describe(BusinessRuleContext? context)` on `BaseBusinessRule` and `BaseAsyncBusinessRule`, called only on the broken path and guarded so an exception inside it degrades to `RuleResultDetails.Empty`; `BaseBusinessRule.Evaluate()` is virtual and `Evaluate(context)` calls `Describe(context)`
- `IBusinessRule.Evaluate(BusinessRuleContext context)` as a default interface member falling back to `Evaluate()`, declared explicitly on every in-library node, decorator and adapter; `Check(context)`, the composites and the sync walker route broken leaves through it
- `WithDetail`, `WithActual`, `WithExpected` and `WithRemediation` on the builders; the functions run only on the broken path, receive the evaluation context and degrade to empty details as a whole when any of them throws

**Typed context keys**

- `ContextKey<T>` (`readonly struct`; `Name`, `ValueType`, ordinal equality, null and blank names rejected) with typed `Get`, `TryGet`, `Set`, `ContainsKey` and `Remove` overloads on `BusinessRuleContext`, and `BusinessRuleContext.Keys` (a read-only live view)
- `ContextKeyNotFoundException` (`Key`, `AvailableKeys`; `Context has no key 'ordr'. Available keys: customer, limit, order`) and `ContextValueTypeMismatchException` (`Key`, `ActualType`, `RequestedType`; `Key 'order' holds Int32, requested Order`)
- `BusinessRuleContextException` (`RuleCode`, `Key`, original as `InnerException`; `Rule LIMIT.EXCEEDED failed to read context: ...`): what propagates under `FaultPolicy.Throw`, what the ledger `Fault` holds under `Record`/`TreatAsBroken` and what `OnRuleFaulted` receives; other exceptions propagate as the same instance and cancellation is never wrapped
- `RuleTestHelper.CreateContext` overloads taking one to four typed `(ContextKey<T>, T)` pairs

**Subject-typed rules**

- `IBusinessRule<in TSubject>` (`IsBroken(subject)`, `IsBroken(subject, context)`, `Evaluate(subject)`, default `Evaluate(subject, context)`) and `IAsyncBusinessRule<in TSubject>` (`IsBrokenAsync(subject, context, ct)`, `EvaluateAsync(subject, context, ct)`), neither deriving from the non-generic interfaces; value-type subjects are supported
- `BaseBusinessRule<TSubject>` / `BaseAsyncBusinessRule<TSubject>` with the guarded `protected virtual Describe(subject, context)` hook
- `BoundBusinessRule<TSubject>` / `BoundAsyncBusinessRule<TSubject>` (`Rule`, `Subject`) returned by `rule.For(subject)`: the only bridge to `IBusinessRule` / `IAsyncBusinessRule`, forwarding all metadata and working with every decorator, operator, checker and `Explain` (as a leaf)
- `BusinessRuleChecker.For(subject)` returning `SubjectRuleChecker<TSubject>` (`Subject`, `CheckAll(rules[, context][, options])`, `EvaluateAll(...)`, `CheckBySeverity`, `CheckByCategory`, `CheckByTags`) and `AsyncBusinessRuleChecker.For(subject)` returning `AsyncSubjectRuleChecker<TSubject>` (`Subject`, `CheckAllAsync([context,] rules[, options], ct)`, `EvaluateAllAsync(...)`, `CheckBySeverityAsync(context, rules, ct, params severities)`, `CheckByCategoryAsync(context, rules, category, ct)`, `CheckByTagsAsync(context, rules, ct, params tags)`): batch checks over subject-typed rules, e.g. `BusinessRuleChecker.For(order).CheckAll(rules)`. Each method binds the rules with `.For(subject)` and runs the same core as the untyped checkers. The static checkers have no subject-first overloads, so no 2.x call can bind to a typed one
- Params forms on the subject checkers for rules of different classes: `For(subject).CheckAll(rule, params others)` / `EvaluateAll(...)` and the async `For(subject).CheckAllAsync(context, rule, params others)` / `EvaluateAllAsync(...)`, e.g. `BusinessRuleChecker.For(order).CheckAll(new OrderIsOverdueRule(), new OrderTooLargeRule())`
- `BusinessRuleChecker.EvaluateEach(subjects, rules[, context][, options])` and `AsyncBusinessRuleChecker.EvaluateEachAsync([context,] subjects, rules[, options], ct)`, evaluating one rule set against many subjects
- `AsyncBusinessRuleChecker.CheckByTagsAsync(context, rules, ct, params tags)` for non-generic async rules. Like `CheckBySeverityAsync`, the async filters take the token before the tags or severities; there is no token-free form, because it would be ambiguous with a positional `default` token
- `typedRule.ToAsync()` and `SyncToAsyncRuleAdapter<TSubject>`: bridge an `IBusinessRule<T>` to `IAsyncBusinessRule<T>` without binding a subject, so a typed sync rule joins typed async compositions (`asyncTyped.And(syncTyped.ToAsync())`) and `IEnumerable<IAsyncBusinessRule<T>>` batches; a specification still binds to its own `ToAsync()`
- `SubjectEvaluation<TSubject>` / `SubjectEvaluationCollection<TSubject>` and `AsyncSubjectEvaluation<TSubject>` / `AsyncSubjectEvaluationCollection<TSubject>` (`Index`, `Subject`, `Result`, `AllPassed`, `HasBrokenRules`, `Broken`, `Passed`, `BrokenRules`, `TotalDuration`), reporting subjects by position
- `BusinessRuleBuilder<TSubject>` / `AsyncBusinessRuleBuilder<TSubject>` with subject-aware conditions and detail overloads, and `BusinessRuleMetadataBuilder<TBuilder>`, the shared base of all four builders (it cannot be derived from outside the library)
- Typed composition: `And`, `Or` and `Not` on `IBusinessRule<TSubject>` / `IAsyncBusinessRule<TSubject>` returning `AndBusinessRule<TSubject>`, `OrBusinessRule<TSubject>`, `NotBusinessRule<TSubject>` and their async twins, with the same truth tables and codes as the non-generic nodes

**Shadow rollout and feature-gated rules**

- `FeatureGatedBusinessRule` / `FeatureGatedAsyncBusinessRule` and the `rule.GatedBy(flag, provider, stableKey)` / `rule.GatedBy(flag, stableKey)` extensions: the rollout is read from the provider on every evaluation; `Off` (or a caller outside the cohort) passes without evaluating the inner rule, `Shadow` evaluates it but always passes and reports the violation, `Enforced` forwards the verdict; `GATED` / `SHADOW` explain nodes. Shadow mode never blocks: an exception the inner rule throws (cancellation excepted) is caught, reported on `ShadowResults` with a stand-in result, through `OnShadowViolation` and then the new `OnShadowFault(rule, exception)` / `OnShadowFaultAsync` hook, and recorded as a `Faulted` node under the passing `SHADOW` node, so a dark-launched rule with a bug can never fail the batch. The sync parameterless `IsBroken()` / `Check()` evaluate un-gated
- `RuleRolloutMode` (`Off`, `Shadow`, `Enforced`), `RuleRollout` (`Mode`, `Percent`, `IsOff`, `IsComplete`, `Includes(bucket)`, `Off`, `Shadow(percent)`, `Enforced(percent)`) and `RuleRolloutBucket` (in-house 32-bit FNV-1a over `flag:stableKey` into buckets 0..99)
- `IRuleFeatureFlagProvider.GetRollout(flag, context)` / `IAsyncRuleFeatureFlagProvider.GetRolloutAsync(flag, context, ct)`, `RuleFeatureFlags` (`ContextKey`, `AsyncContextKey`), `RuleConfigurationException` (`RuleCode`), the thread-safe `InMemoryRuleFeatureFlagProvider` and `RuleTestHelper.CreateFlagProvider(flag, mode, percent)`
- `ShadowResults` / `HasShadowResults` on both result types (constructors `(entries, totalDuration, shadowResults)`), never counted as broken, and the observer hooks `OnShadowViolation(rule, result)` / `OnShadowViolationAsync(rule, result, ct)`

**Specification rules**

- `ISpecificationRule<TSubject>` (`BrokenExpression`, `SatisfiedExpression`) and the abstract `SpecificationBusinessRule<TSubject>` (override `BrokenWhen`; read once, compiled lazily, both `IsBroken` overloads sealed so the in-memory and translated verdicts cannot diverge)
- `WhereBroken(rule)` / `WhereSatisfies(rule)` on `IQueryable<TSubject>`, accepting only `ISpecificationRule<TSubject>` so an untranslatable composition is a compile-time error
- Satisfaction-stated combinators `AllOf` / `AnyOf` / `Not` returning `AllOfSpecificationRule<T>` / `AnyOfSpecificationRule<T>` / `NotSpecificationRule<T>` over `ComposedSpecificationRule<T>`, spliced onto one parameter (no `Invoke` nodes); codes `ALL(A,B)`, `ANY(A,B)`, `!A`
- `spec.ToAsync()` returning `AsyncSpecificationRuleAdapter<TSubject>`: the whole async side of specifications, the one deliberate exception to the sync/async mirror rule

**Rule scopes, externalized parameters and per-key caching**

- `RuleScope` (immutable ordinal key/value pairs with order-insensitive value equality: `Global`, `Tenant(id)`, `With(key, value)`, `Specificity`, `IsGlobal`, `TenantId`, `TryGet`, `ContainsKey`, `Pairs`, `AppliesTo(requested)`, `ToString()` as `region=EU;tenant=acme`)
- `IRuleParameterProvider.TryGet<T>(name, scope, out value)` / `IAsyncRuleParameterProvider.TryGetAsync<T>(name, scope, ct)` returning `RuleParameterLookup<T>`, the thread-safe `InMemoryRuleParameterProvider` (most specific applying scope wins, latest set wins on ties) and `RuleTestHelper.CreateParameterProvider(name, value, scope?)`
- `WithParameters(provider)` and `WithParameter<T>(name, fallback)` on all four builders, `WithParameters(IAsyncRuleParameterProvider)` on the async builders, and the parameter-receiving conditions `When((ctx, p) => ...)`, `When((subject, ctx, p) => ...)`, `WhenAsync((ctx, p, ct) => ...)`, `WhenAsync((subject, ctx, p, ct) => ...)`; parameters resolve on every evaluation for the scope under `RuleParameters.ScopeKey`, and a reserved key holding a value of another type throws `ContextValueTypeMismatchException`
- `RuleParameters` (`Get<T>`, `TryGet<T>`, `Contains`, `Names`, `Count`, `Scope`, `Empty`; reserved keys `ContextKey`, `AsyncContextKey`, `ScopeKey`)
- Per-key caching: `rule.Cached(duration, cacheKeySelector)` (both sides, returning the concrete `CachedBusinessRule` / `CachedAsyncBusinessRule`) caches one verdict per selected key; `CachesPerKey`, `InvalidateCache(key)` / `InvalidateCacheAsync(key, ct)`; a selector returning null throws `RuleConfigurationException`

**Extension seams for the companion packages**

- `IRuleConditionCompiler.Compile(expression)` / `IAsyncRuleConditionCompiler.Compile(expression)`, consumed by `BusinessRuleBuilder.When(compiler, expression)` and `AsyncBusinessRuleBuilder.WhenAsync(compiler, expression)`, which compile at builder time
- `IBuiltRuleFactory` / `IAsyncBuiltRuleFactory` and `BusinessRuleBuilder.WithRuleFactory(factory)` / `AsyncBusinessRuleBuilder.WithRuleFactory(factory)`: let a package choose the rule type `Build()` returns. The factory receives a `BuiltRuleDefinition` (the seven metadata members, `HasDetails`, `ResolveParameters(context)`, `ResolveParametersAsync(context, ct)`, `CreateResult(rule, context)` with the builder's details); a factory replaces any condition set so far, a later `When`/`WhenAsync` replaces the factory, and a factory returning null makes `Build()` throw `InvalidOperationException`

#### Masterly.BusinessRules.Expressions

Namespace `Masterly.BusinessRules.Expressions`; `netstandard2.1`; depends on the core only.

- `RuleExpression.Parse(text[, options])` / `TryParse` parse a textual broken-condition once into an immutable tree (`Text`, `Options`, `Identifiers`, `ParameterNames`, `Depth`) evaluated by a single interpreter (`Evaluate(context, parameters?)`, `EvaluateAsync(context, asyncParameters?, parameters?, ct)`). Grammar: `||`/`or`, `&&`/`and` (short-circuit), `!`/`not`, `==` `!=` `~=` `<` `<=` `>` `>=`, `in [..]` and `in path`, `+ - * / %`, unary `-`, null-safe member paths, `startsWith`/`endsWith`/`contains` with an optional `/ci` suffix, literals, `Data` bound to `__typedData` and `@name` parameters. The README grammar and coercion tables are pinned by parameterised tests
- `Explain(context, redact?, parameters?)` / `ExplainAsync(...)` return a `RuleExpressionExplanation` (`Expression`, `Result`, `Steps`, `Rendered`) from the same walk that produces the verdict, e.g. `amount(12000) > @maxOrderAmount(10000) => true`; `RuleExpressionStep` (`Kind`, `NodeText`, `BoundValue`, `Result`), `RuleExpressionStepKind` and the `RuleExpressionRedactor` delegate that masks values before they are recorded
- `ExpressionBusinessRule` / `ExpressionAsyncBusinessRule` from `ToRule(code, message, parameters?, severity?)` / `ToAsyncRule(code, message, asyncParameters?, parameters?, severity?)` or their constructors, with `Expression`, `Parameters` / `AsyncParameters`, `Description` = expression text and `ExplainExpression` / `ExplainExpressionAsync`; the sync parameterless `IsBroken()` throws `InvalidOperationException` (`ContextRequiredMessage`). `@name` resolves through declared builder parameters, then the providers under `RuleParameters.AsyncContextKey` / `ContextKey`, then the injected providers, then the context bag
- `WhenExpression(text[, options])` / `WhenExpression(expression)` on `BusinessRuleBuilder` and `AsyncBusinessRuleBuilder` (`RuleExpressionBuilderExtensions`), parsing at builder time. They install the expression through the core `WithRuleFactory` seam, so `Build()` returns an `ExpressionBusinessRule` / `ExpressionAsyncBusinessRule` carrying the builder's name, description (the expression text when none is set), category, tags, violation details and `WithParameter<T>` declarations
- `RuleExpressionConditionCompiler` implementing `IRuleConditionCompiler` and (explicitly, also `CompileAsyncCondition`) `IAsyncRuleConditionCompiler`; `Options`, `Parameters`, `AsyncParameters`
- The typed fast path `Compile<T>([parameters])` returning `Func<BusinessRuleContext<T>, bool>` with `Data` paths compiled to null-safe `MemberExpression` chains (a missing member throws `RuleExpressionParseException`). An `in [..]` list compiles to a short-circuiting chain that computes the value once and stops at the first match, exactly like the interpreter
- Floating-point comparisons follow IEEE 754 on both paths: every ordering and `==` with a `NaN` operand is false and `!=` is true (`x != x` detects `NaN`; `score < threshold` never fires for one)
- `RuleExpressionOptions` (immutable; `Default`, `MaxLength` 4096, `MaxDepth` 64, `MemberFilter`, `RequireRuleAccessibleAttribute`, `Culture`, `With...`), `[RuleAccessible]`, and the errors `RuleExpressionParseException` (`Line`, `Column`), `RuleExpressionBindingException` (`Identifier`, `AvailableKeys`) and `RuleExpressionEvaluationException` (`NodeText`). The interpreter path emits no dynamic code

#### Masterly.BusinessRules.Diagnostics

Namespace `Masterly.BusinessRules.Diagnostics`; `netstandard2.1`; depends on Microsoft.Extensions.Logging.Abstractions 10.0.14 and System.Diagnostics.DiagnosticSource 10.0.14.

- `LoggingRuleExecutionObserver` / `LoggingAsyncRuleExecutionObserver`: one structured `ILogger` line per rule (`RuleCode`, `RuleName`, `Category`, `Severity`, `Outcome`, `ElapsedMs`, `Message`) plus batch, fault and shadow lines, generated with `[LoggerMessage]`; `RuleLogEvents` (`RuleBroken` 1 … `ShadowViolation` 6) and `LoggingRuleObserverOptions` (`LogPassedRules`, `PassedLevel`, `BrokenLevelBySeverity`, `DefaultBrokenLevel`, `BrokenLevelFor`, `FaultedLevel`, `LogBatches`, `BatchLevel`, `ShadowViolationLevel`, `IncludeMessage`, `RedactedMessage`)
- `TracingRuleExecutionObserver` / `TracingAsyncRuleExecutionObserver`: a `BusinessRules.Batch` span with a `BusinessRule.Evaluate` child per rule from the `ActivitySource` `Masterly.BusinessRules` (`BusinessRulesActivitySource`), attributes and events in `BusinessRuleSpanAttributes`, and the rule span as `Activity.Current` while the rule runs; `TracingRuleObserverOptions` (`MessageAsEvent`, `CodeTagSelector` with the default `LeafCodeTagSelector`, `RecordMetrics`, `Metrics`)
- `BusinessRulesMetrics` on the `Meter` `Masterly.BusinessRules`: `business_rules.evaluations`, `business_rules.evaluation.duration` (ms) and `business_rules.batches`; `RuleOutcomeTags` and `RuleSeverityTags`
- `DiagnosticsBusinessRule` / `DiagnosticsAsyncBusinessRule` and the `rule.Traced(options?)` / `rule.Traced(logger, options?, loggingOptions?)` extensions for the single-rule `Check()` path. They implement `IExplainableDecoratedBusinessRule` / `IExplainableDecoratedAsyncBusinessRule` (`ExplanationOperator` = `DiagnosticsText.TracedOperator`, `TRACED`), so on walker paths (checker batches, operands, group members, `Explain`) a traced composed rule keeps its member `Details` and its explanation subtree
- The tracing observers close every span they open: a cancelled rule's span from `OnRuleCancelled` (`outcome=cancelled`, not counted) and the batch span from `OnBatchAborted` (`cancelled`, or `faulted` with an error status, an `exception` event and a faulted batch measurement), so neither span stays `Activity.Current` nor goes unexported when a batch is cancelled or a fault propagates under `FaultPolicy.Throw`. The span lookups skip foreign activities a rule left open and finished spans

#### Masterly.BusinessRules.DependencyInjection

Namespace `Masterly.BusinessRules.DependencyInjection`; `netstandard2.1`; depends on Microsoft.Extensions.DependencyInjection.Abstractions 10.0.14.

- `services.AddBusinessRules(params Assembly[])` / `services.AddBusinessRules(Action<BusinessRuleOptions>)`: deterministic assembly scanning for `IBusinessRule<T>` / `IAsyncBusinessRule<T>` (and, with `IncludeUntypedRules = true`, plain `IBusinessRule` / `IAsyncBusinessRule`; off by default because a plain rule that takes its subject in the constructor cannot be built by the container and would fail `ValidateOnBuild`) types registered with `TryAddEnumerable`, plus `IRuleRunner<>` and `IRuleRegistry` with `TryAdd`; `BusinessRuleOptions` (`Lifetime`, `IncludeNonPublicTypes`, `IncludeUntypedRules`, `TypeFilter`, `AddAssembly`, `AddAssemblies`, `AddAssemblyContaining<T>`, `Assemblies`, `AddRuleSet`, `RuleSetCount`)
- `IRuleRunner<TSubject>` / `RuleRunner<TSubject>` (`Rules`, `AsyncRules`, `HasAsyncRules` (on the interface), `CheckAll`, `EvaluateAll`, `CheckAllAsync`, `EvaluateAllAsync`), projecting onto the checkers (`BusinessRuleChecker.For(subject)` on the sync side, rules bound with `.For(subject)` on the async side)
- The scoped rule registry: `RuleRegistry.Create()` → `RuleRegistryBuilder` (`AddSet`, `LayerCount`, `Build`), `RuleSetBuilder` (`Add`, `AddAsyncRule`, `Disable`, `Replace`, `ReplaceAsyncRule`, `Name`, `Scope`, `RuleCount`, `AsyncRuleCount`, `DisabledCodes`) and `IRuleRegistry` / `RuleRegistry` (`SetNames`, `Contains`, `Resolve`, `ResolveAsyncRules`, `CheckAll`, `EvaluateAll`, `CheckAllAsync`, `EvaluateAllAsync`); layers merge from the least to the most specific applying scope, ties in declaration order
- `RuleSetNotFoundException` (`SetName`, `AvailableSets`)
- Registry batch semantics match `IRuleRunner<T>`: `CheckAllAsync` / `EvaluateAllAsync` run the whole resolved set (sync rules adapted with `ToAsync()`, then async rules) and `CheckAll` / `EvaluateAll` throw `InvalidOperationException` naming the codes when the set has async rules. The requested scope (null = `Global`) chooses the layers and is the scope parameters resolve for: the batch methods store it under `RuleParameters.ScopeKey` for the call and restore the context's previous entry afterwards, so a context reused across tenants never leaks one tenant's parameters into another's evaluation; `Resolve` / `ResolveAsyncRules` store it and leave it for the later evaluation

#### Masterly.BusinessRules.FeatureManagement

Namespace `Masterly.BusinessRules.FeatureManagement`; `netstandard2.1`; depends on Microsoft.FeatureManagement 4.8.0.

- `FeatureManagementRuleFeatureFlagProvider` implementing `IRuleFeatureFlagProvider` and `IAsyncRuleFeatureFlagProvider` over an `IVariantFeatureManager` (variants first, then the enabled state) or a plain `IFeatureManager`; `Options`, `SupportsVariants`; the sync side blocks on the async one
- `FeatureManagementRolloutOptions` (`EnabledRollout`, `OnUnrecognizedVariant`, `TargetingContextSelector`, `VariantRollouts`, `MapVariant`, `TryGetVariantRollout`), `UnrecognizedVariantBehavior` (`Throw`, `Off`, `UseEnabledState`), `RolloutVariantConvention` (`Parse`, `TryParse`, `TryRead`, `ModeKey`, `PercentKey`) and `RolloutVariantFormatException` (`Flag`, `Variant`)
- `services.AddBusinessRulesFeatureManagement(configure?, lifetime?)` registering one provider instance as itself and both provider interfaces with `TryAdd`

#### Masterly.BusinessRules.Testing

Namespace `Masterly.BusinessRules.Testing`; `net8.0` and `net10.0` (CsCheck ships no .NET Standard build and the boundary finder uses `INumber<T>`); depends on CsCheck 4.9.1 on `net8.0` and CsCheck 5.0.0 on `net10.0` (CsCheck 5 ships only `net10.0`+ assets and fixes the zero-bound `Gen.Decimal` failure).

- Outcome coverage: `RuleCoverage.Begin()` → `RuleCoverageSession` (`Register`, `Record`, `Report`, `Reset`, `RuleCount`, `IsEnded`), `rule.WithCoverage(session)` → `CoverageBusinessRule` / `CoverageAsyncBusinessRule`, `RuleCoverageReport` (`Entries`, `OutcomeCoverage`, `NeverBroken`, `NeverPassed`, `NeverEvaluated`, `Find`, `ToText`, `AssertMinimum`) and `RuleCoverageEntry`. The decorators implement the core `IExplainableDecoratedBusinessRule` / `IExplainableDecoratedAsyncBusinessRule` seam: they record through the single walker evaluation inside compositions and groups, and appear as `COVERAGE` nodes over the wrapped rule's own explanation tree
- Boundary finder: `RuleBoundaryFinder.Find<T>(factory, from, to, step)` / `FindAsync<T>(..., ct)` for `T : INumber<T>`, returning `RuleBoundary<T>` (`LastPassing`, `FirstBroken`, `Monotonicity`, `ProbeCount`, `Step`; `ToString()` renders `passes up to 10000.00, breaks from 10000.01 (… probes)`) and throwing `RuleAssertionException` for a non-monotone or flat range; `Monotonicity` (`BrokenAbove`, `BrokenBelow`)
- Rule properties: `RuleProperties.For<T>(factory, Gen<T>)` → `RuleProperties<T>` / `AsyncRuleProperties<T>` with `MonotoneIn(monotonicity, comparer?)`, `ContextPure()` / `ContextPure(contextFactory)`, `LawCount`, `LawNames` and seeded, reproducible `Verify(iterations, seed?)` / `VerifyAsync(iterations, seed?, ct)` with CsCheck shrinking
- Smoke-mode mutants: `rule.Invert()` (`InvertedBusinessRule` / `InvertedAsyncBusinessRule`, an `INVERTED` explain node), `rule.AlwaysPass()` (`AlwaysPassingBusinessRule` / `AlwaysPassingAsyncBusinessRule`, an `ALWAYS PASSING` node over a `NotEvaluated` inner rule) and `rule.DowngradeSeverity()`

### Changed

- Dependencies bumped to their latest stable versions: Microsoft.Extensions.Logging.Abstractions, System.Diagnostics.DiagnosticSource and Microsoft.Extensions.DependencyInjection.Abstractions 10.0.14 (all still `netstandard2.0`-compatible), CsCheck 5.0.0 on `net10.0`
- `Masterly.BusinessRules.Testing` multi-targets `net8.0` and `net10.0`; `net8.0` consumers keep CsCheck 4.9.1
- Repository build: test projects target `net10.0` (.NET 9 is out of support) on xunit.v3 4.x and Microsoft.Testing.Platform via `global.json`, coverage moved from `coverlet.collector` to `coverlet.MTP`, `global.json` pins SDK 10.0.100 (`rollForward: latestFeature`), and the GitHub Actions moved to checkout v7, setup-dotnet v6, cache v6, codeql-action v4 and codecov-action v7. Build runs are grouped per branch (superseded pull-request runs are cancelled), Codecov uploads use a token, and a repository-local `nuget.config` pins `nuget.org` as the only package source
- And/Or/Not nodes, groups, composites, the in-library decorators, both adapters and the checkers route evaluation through one internal `EvaluateCore` per node, so `Check`, `Evaluate`, the checkers and `Explain` share a single evaluation path; timing and explanation nodes are allocated only inside an `Explain` walk
- `SimpleBusinessRule` is no longer used by the composition operators but remains available as a public type
- Observers fire from `EvaluateAll`/`EvaluateAllAsync` through the options-taking overloads; the boolean `EvaluateAll` overloads still have no observer. `OnBatchCompleted` is raised before `CheckAll` throws; a batch that ends by an exception (a fault propagating under `FaultPolicy.Throw`, a cancellation, a null rule) raises `OnBatchAborted` instead
- `CachedBusinessRule` / `CachedAsyncBusinessRule` hold their lock / semaphore only around the cache lookups and stores and evaluate the inner rule outside it, so one slow key no longer blocks cache hits and evaluations for every other key; a verdict computed across an `InvalidateCache` call is returned but not stored. A verdict that rests on an exception captured by `Explain(captureExceptions: true)` is never cached, so a diagnostic explain can no longer make later `IsBroken`/`Check` calls report the rule as broken for the cache duration
- Every checker overload, including the boolean ones, runs through one core and returns or carries a populated ledger; a null `rules` collection or a null rule inside the batch is rejected with `ArgumentNullException`/`ArgumentException` instead of a `NullReferenceException`; the async checker evaluates every rule through the same walker as the sync checker
- `RuleOutcome.NotEvaluated` is reported for rules a fail-fast batch never reached, and `RuleOutcome.Faulted` for rules recorded under `FaultPolicy.Record`/`TreatAsBroken`
- `CachedBusinessRule.Evaluate()`/`Evaluate(context)` and `ConditionalBusinessRule.Evaluate(context)` forward to the inner rule's factory, and the severity overrides copy the inner result with `BusinessRuleResult.WithSeverity`, so metadata and `Describe` details survive every decorator. A served sync cache hit reports the inner `Evaluate(context)` result; a served async cache hit reports the forwarded metadata only
- `RuleFaultResult` (the `TreatAsBroken` stand-in) carries the faulted rule's `Name`, `Category` and `Tags`; a rule whose `Name`, `Category` or `Tags` getter returns null is reported with the code as `Name`, an empty category and no tags
- `BusinessRuleBuilder` and `AsyncBusinessRuleBuilder` derive from `BusinessRuleMetadataBuilder<TBuilder>`; their source surface and defaults (`Name` falls back to the code) are otherwise unchanged, but the `With*` methods are now declared on the base class (2.x-compiled callers still resolve them). `BusinessRuleBuilder.When(...)`: the last condition set wins (see Breaking Changes); `BusinessRuleBuilder<T>.When(...)` keeps the subject-only and context conditions side by side, but a parameters condition replaces both and is replaced by either. `AsyncBusinessRuleBuilder.WhenAsync(...)`: the last condition set wins across the context-only and parameter-receiving overloads
- The checkers attach an internal shadow-violation sink to the batch context for the duration of a batch (beside the key/value store, so `Keys`, `Count`, `ContainsKey` and the available-keys list are unaffected)
- Packaging: all six packages share one version and their metadata from `Directory.Build.props` (each `src/*.csproj` keeps only `TargetFramework`, `Description`, `PackageTags` and its own references), and every package ships its XML documentation (IntelliSense for consumers)
- Packaging: every package has a `.snupkg` symbol package on nuget.org, with SourceLink to the exact GitHub commit, deterministic builds and untracked sources embedded
- Packaging: the licence is the SPDX expression `MIT` (`PackageLicenseExpression`) instead of a packed `LICENSE.txt`; every package carries a copyright (`Copyright (c) 2025-2026 Ahmad Omar Al-Freihat`) and release notes that link to this changelog
- Packaging: each companion package ships its own short README (purpose, install command, target framework, dependencies, one example and a link to the full documentation) instead of the core README; the core package ships the full README
- The subject-typed async `And` / `Or` nodes (`AndAsyncBusinessRule<T>`, `OrAsyncBusinessRule<T>`) build their `Message` with the same prefix as the typed sync nodes (`Both rules must be satisfied: A AND B`, `At least one rule must be satisfied: A OR B`); the non-generic async nodes keep their 2.x message (`A AND B`)
- Release pipeline (`publish.yml`): it publishes only a successful `Build` of a push to `master` (a manual run only from `master`, after running the tests), checks out the commit that Build tested, installs the same SDKs as the build, pushes the core package first and then each companion package (`--skip-duplicate`, symbols included), and creates the GitHub Release `v{Version}` with every `.nupkg` / `.snupkg` and this version's CHANGELOG section as the body. A re-run uploads only missing release assets and never replaces existing ones, and two publish runs never overlap
- README rewritten for v3: Features, Installation (all six packages with their install commands), a Packages table with targets and dependencies, a section per feature and per companion package, the custom-decorator and rule-factory seams, a v3 real-world example, an API Reference table per package and an Upgrading from 2.x guide

### Fixed

- `CachedBusinessRule.Check()`/`Check(context)` and `CachedAsyncBusinessRule.EvaluateAsync`/`CheckAsync` dropped the `Details` of a cached group or composite; they now report the result of the single inner evaluation (a served cache hit still reports no member details because only the verdict is cached)
- `AsyncBusinessRuleChecker.CheckBySeverityAsync` and `CheckByCategoryAsync` (non-generic) ignored their `cancellationToken`; they now forward it, so a cancelled token propagates `OperationCanceledException`
- The rule builders handed the built rule a live view of the builder's tag list, so a later `WithTags` call changed the `Tags` of a rule already built; `Build()` now snapshots the tags
- README named the sync-to-async extension `.ToAsyncRule()`; the method is `.ToAsync()`. README also listed a `.ToSyncRule()` extension that does not exist (`AsyncToSyncRuleAdapter` is constructed directly) and a `.WithCondition(...)` extension family whose real name is `.When(...)` with one condition shape per side
- README "Rule Composition" documented `And()` as "broken if either rule is broken" and `Or()` as "broken only if both are broken", the opposite of what the code does and has always done. The code is unchanged and now pinned by tests: `And()` is broken only when both operands are broken, `Or()` when either is, `Not()` inverts

## [2.0.0] - 2026-01-01

### Breaking Changes

- `IBusinessRule` now declares `IsBroken(BusinessRuleContext)` and `Check(BusinessRuleContext)`; implementations that do not derive from `BaseBusinessRule` must add them (`BaseBusinessRule` implements both)
- `IBusinessRule` and `IAsyncBusinessRule` gain the metadata properties `Name`, `Description`, `Category` and `Tags`, and `Severity` becomes a default interface member (default `Error`); all four have default implementations, so existing rules keep compiling

### Fixed

- Async `And()` and `Or()` composition logic was inverted - now correctly evaluates conditions

### Added

#### Fluent Builders
- `BusinessRuleBuilder` - Create sync rules inline without subclassing
- `AsyncBusinessRuleBuilder` - Create async rules inline without subclassing

#### Rule Metadata
- `Name` property for human-readable rule names
- `Description` property for detailed rule documentation
- `Category` property for grouping related rules
- `Tags` property for flexible rule categorization

#### Caching
- `CachedBusinessRule` - Wraps sync rules with time-based caching
- `CachedAsyncBusinessRule` - Wraps async rules with time-based caching

#### Conditional Execution
- `ConditionalBusinessRule` - Execute rules only when a precondition is met
- `ConditionalAsyncBusinessRule` - Async variant with async preconditions

#### Rule Adapters
- `SyncToAsyncRuleAdapter` - Adapt sync rules for use in async contexts
- `AsyncToSyncRuleAdapter` - Adapt async rules for use in sync contexts (blocking)

#### Observers
- `IRuleExecutionObserver` - Receive callbacks during sync rule evaluation
- `IAsyncRuleExecutionObserver` - Receive callbacks during async rule evaluation

#### Typed Context
- `BusinessRuleContext<T>` - Strongly-typed context for passing data to rules

#### Evaluation Results
- `RuleEvaluationResult` - Detailed results from sync batch evaluation
- `AsyncRuleEvaluationResult` - Detailed results from async batch evaluation
- `BrokenRules`, `PassedRules`, `HasBrokenRules`, `AllPassed`, `HasErrors` and `HasWarnings` properties
- `Errors`, `Warnings` and `Infos` and the `GetBySeverity()` filter
- `ThrowIfBroken()` and `ThrowIfErrors()`

#### Testing Utilities
- `RuleTestHelper.AssertBroken()` - Assert a rule is broken
- `RuleTestHelper.AssertNotBroken()` - Assert a rule passes
- `RuleTestHelper.AssertBrokenAsync()` - Async assertion for broken rules
- `RuleTestHelper.AssertNotBrokenAsync()` - Async assertion for passing rules
- `RuleTestHelper.CreateContext()` - Factory for test contexts
- `RuleTestHelper.CreateBrokenRule()` - Factory for always-broken test rules
- `RuleTestHelper.CreatePassingRule()` - Factory for always-passing test rules
- `RuleAssertionException` - Exception for test assertion failures

#### Batch Validation Enhancements
- `stopOnFirstFailure` parameter for fail-fast behavior
- `runInParallel` parameter for parallel async rule execution
- `observer` parameter for rule execution monitoring
- `BusinessRuleChecker.EvaluateAll()` - Evaluate without throwing
- `AsyncBusinessRuleChecker.EvaluateAllAsync()` - Async evaluate without throwing
- `CheckBySeverity()` / `CheckBySeverityAsync()` - Filter rules by severity
- `CheckByCategory()` / `CheckByCategoryAsync()` - Filter rules by category
- `CheckByTags()` - Filter rules by tags (sync only)
- More `params` batch overloads: `CheckAll(stopOnFirstFailure, params rules)`, `CheckAll(context, stopOnFirstFailure, params rules)`, `CheckAllAsync([context,] [stopOnFirstFailure,] params rules)` and `EvaluateAllAsync([context,] params rules)`, plus `CheckAllAsync(rules, ...)` and `EvaluateAllAsync(rules, ...)` without a context

#### Extension Methods
- `ToAsync()` - Convert a sync rule to an async rule (there is no sync conversion extension; construct `AsyncToSyncRuleAdapter` directly)
- `Cached(duration)` - Add time-based caching to any rule
- `When(condition)` - Evaluate a rule only when a precondition holds

### Changed

- Comprehensive XML documentation added to all public APIs
- `IBusinessRule` and `IAsyncBusinessRule` declare the metadata members (`Severity`, `Name`, `Description`, `Category`, `Tags`) with default implementations, so only `Code` and `Message` are required