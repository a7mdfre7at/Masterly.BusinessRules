---
id: api-reference
title: "API Reference"
sidebar_label: "API Reference"
---

This reference lists the public types and members you use in application code, one package at a time. Each package uses a single namespace that matches its package name.

## Masterly.BusinessRules

The core package. It has no dependencies.

**Rules**

| Type or member | Purpose |
|----------------|---------|
| `IBusinessRule` | A sync rule: `IsBroken()`, `Check()` and `Evaluate()`, each also with a `BusinessRuleContext` overload. |
| `IAsyncBusinessRule` | An async rule: `IsBrokenAsync`, `CheckAsync` and `EvaluateAsync`. Each takes a context and an optional `CancellationToken`. |
| `IBusinessRule<TSubject>` / `IAsyncBusinessRule<TSubject>` | A rule that receives its subject at evaluation time, so one instance serves many subjects. |
| `IBusinessRuleMetadata` | Base of every rule interface: `Code`, `Message`, plus `Severity` (default `Error`), `Name`, `Description`, `Category` and `Tags`. |
| `BaseBusinessRule` / `BaseAsyncBusinessRule` | Base classes for your rules. Override `Code`, `Message` and `IsBroken` / `IsBrokenAsync`; override `Describe` to add result details. |
| `BaseBusinessRule<TSubject>` / `BaseAsyncBusinessRule<TSubject>` | Base classes for subject-typed rules. |
| `SimpleBusinessRule` | A sync rule made from a `Func<bool>`, a message and a code (default `"SIMPLE"`). |
| `RuleSeverity` | `Info`, `Warning`, `Error`. |
| `IComposedBusinessRule` / `IComposedAsyncBusinessRule` | Implemented by rules built from other rules: `Operator` and `Operands`. |
| `IDecoratedBusinessRule` / `IDecoratedAsyncBusinessRule` | Implemented by rules that wrap one other rule: `InnerRule`. |

**Building and combining**

| Type or member | Purpose |
|----------------|---------|
| `BusinessRuleBuilder` / `AsyncBusinessRuleBuilder` | Build a rule without a class: `Create(code)`, then `When(...)` / `WhenAsync(...)` and `Build()`. |
| `BusinessRuleBuilder<TSubject>` / `AsyncBusinessRuleBuilder<TSubject>` | The same for subject-typed rules. Conditions and details also receive the subject. |
| `BusinessRuleMetadataBuilder<TBuilder>` | Shared builder methods: `WithMessage`, `WithSeverity`, `WithName`, `WithDescription`, `WithCategory`, `WithTags`, `WithDetail`, `WithActual`, `WithExpected`, `WithRemediation`, `WithParameters`, `WithParameter<T>(name, fallback)`. |
| `.And(rule)` / `.Or(rule)` / `.Not()` | Combine rules. `And` is broken only when both are broken (code `A+B`); `Or` when either is (code `A\|B`); `Not` inverts (code `!A`). Also available on subject-typed rules. |
| `AndBusinessRule`, `OrBusinessRule`, `NotBusinessRule` | The node types `And`, `Or` and `Not` return. Async and `<TSubject>` versions exist. |
| `RuleGroup` / `AsyncRuleGroup` | Create a group from a code, a message and members: `All`, `Any`, `AtLeast(n, ...)`, `Exactly(n, ...)`. |
| `GroupBusinessRule` / `GroupAsyncBusinessRule` | An immutable group. `WithShortCircuit()` stops once the verdict is known; `WithSeverity()`; async `WithParallelEvaluation()`. |
| `RuleGroupQuantifier` | `All`, `Any`, `AtLeast`, `Exactly`. |
| `RuleOperator` | The `Operator` of a combined rule: `And`, `Or`, `Not`, `All`, `Any`, `AtLeast`, `Exactly`. |
| `CompositeBusinessRule` / `CompositeAsyncBusinessRule` | A list of rules, broken when any child is broken. Its code is `"CompositeRule"`. |
| `.For(subject)` | Bind a subject-typed rule to one subject. The result (`BoundBusinessRule<TSubject>` / `BoundAsyncBusinessRule<TSubject>`) is an ordinary rule. |
| `.Cached(duration)` / `.Cached(duration, keySelector)` | Cache the verdict for a time, once per rule or once per key. Returns `CachedBusinessRule` / `CachedAsyncBusinessRule`. |
| `CachedBusinessRule` / `CachedAsyncBusinessRule` | `InvalidateCache()` / `InvalidateCacheAsync()` (all keys or one key) and `CachesPerKey`. |
| `.When(condition)` | Evaluate the rule only when the condition is true (`ConditionalBusinessRule`). Sync takes `Func<bool>`; async takes `Func<BusinessRuleContext, bool>`. |
| `.WithSeverity(severity)` | Report the rule with another severity (`SeverityOverrideBusinessRule`). |
| `.ToAsync()` | Use a sync rule where an async rule is expected (`SyncToAsyncRuleAdapter`). Also works on subject-typed rules. |
| `AsyncToSyncRuleAdapter` | Use an async rule where a sync rule is expected. It blocks the calling thread. There is no extension method; construct it directly. |

**Running**

| Type or member | Purpose |
|----------------|---------|
| `BusinessRuleChecker.CheckAll(...)` | Evaluate every rule and throw `BusinessRuleValidationException` if any is broken. |
| `BusinessRuleChecker.EvaluateAll(...)` | Evaluate every rule and return a `RuleEvaluationResult`. Does not throw for broken rules. |
| `BusinessRuleChecker.CheckBySeverity` / `CheckByCategory` / `CheckByTags` | Check only the rules that match a severity, a category or any of the tags. |
| `BusinessRuleChecker.For(subject)` | Return a `SubjectRuleChecker<TSubject>` bound to the subject, with `CheckAll`, `EvaluateAll`, `CheckBySeverity`, `CheckByCategory` and `CheckByTags` over subject-typed rules. `AsyncBusinessRuleChecker.For(subject)` returns the async `AsyncSubjectRuleChecker<TSubject>`. |
| `BusinessRuleChecker.EvaluateEach(subjects, rules)` | Evaluate subject-typed rules for each subject. Returns a `SubjectEvaluationCollection<TSubject>`. |
| `AsyncBusinessRuleChecker` | The async versions: `CheckAllAsync`, `EvaluateAllAsync`, `CheckBySeverityAsync`, `CheckByCategoryAsync`, `CheckByTagsAsync`, `EvaluateEachAsync`. |
| `RuleEvaluationOptions` / `AsyncRuleEvaluationOptions` | `StopOnFirstFailure`, `OnFault` (default `Throw`), `ThrowOn` (default `Info`, used by `CheckAll` only) and `Observer`. Async adds `RunInParallel`. |
| `FaultPolicy` | What to do when a rule throws: `Throw`, `Record` or `TreatAsBroken`. Cancellation always propagates. |
| `.Check(throwOn)` / `.Check(context, throwOn)` / `.CheckAsync(context, throwOn)` | Check one rule and throw only when its severity is at or above `throwOn`. |
| `.Explain(context, captureExceptions)` / `.ExplainAsync(context, captureExceptions, token)` | Evaluate once and return a `RuleExplanation` tree. All arguments except the async context are optional. |
| `IRuleExecutionObserver` / `IAsyncRuleExecutionObserver` | Receive callbacks while a batch runs: before and after each rule, broken, faulted and cancelled rules, batch start and end, shadow results. |

:::note

On the async checker, the context always comes first: `AsyncBusinessRuleChecker.For(subject).CheckAllAsync(context, rules)`. The sync checker takes it after the rules: `BusinessRuleChecker.For(subject).CheckAll(rules, context)`.

:::

**Results**

| Type or member | Purpose |
|----------------|---------|
| `RuleEvaluationResult` / `AsyncRuleEvaluationResult` | A batch result: `AllPassed`, `HasBrokenRules`, `BrokenRules`, `PassedRules`, `Errors` / `Warnings` / `Infos`, `ByTag`, `ByCategory`, `ShadowResults`, `ThrowIfBroken()`, `ThrowIfErrors()`, `ThrowIfAtLeast(severity)`. |
| `RuleEvaluationResult.Entries` | The ledger: one `RuleEvaluationEntry` per rule, in order. `Faulted`, `HasFaults`, `TotalDuration` and `Slowest` summarize it. |
| `RuleEvaluationEntry` | One rule's line in the ledger: `Order`, `Code`, `Outcome`, `Result`, `Fault`, `Duration`. |
| `RuleOutcome` | `Passed`, `Broken`, `Skipped`, `CacheHit`, `NotEvaluated`, `Faulted`. |
| `BusinessRuleResult` | One broken rule: `Code`, `Message`, `Severity`, `Name`, `Category`, `Tags`, `Detail`, `Actual`, `Expected`, `Remediation`, `Data`, and `Details` (broken members of a group). `Flatten()`, `HasTag()`, `WithSeverity()`. |
| `RuleResultDetails` | What `Describe` returns: `RuleResultDetails.Detail($"...")`, then `.Actual()`, `.Expected()`, `.Remediation()`, `.With(key, value)`. |
| `RuleExplanation` | A node of an explain tree: `Code`, `Outcome`, `IsBroken`, `Reason`, `Duration`, `Children`, `BrokenLeaves()`, `ToText()`. |
| `SubjectEvaluationCollection<TSubject>` / `SubjectEvaluation<TSubject>` | Results of `EvaluateEach`, one per subject in input order: `AllPassed`, `Broken`, `Passed`, `BrokenRules`. Async versions exist. |
| `BusinessRuleValidationException` | Thrown when rules are broken: `BrokenRules`, `CollectedRules` (broken rules below `ThrowOn`), `Entries`, `Flatten()`, `ByTag`, `ByCategory`. |
| `BusinessRuleContextException` | Thrown by the checkers when a rule cannot read the context: `RuleCode`, `Key`, original exception as `InnerException`. |
| `RuleConfigurationException` | A rule is set up wrongly, for example a gated rule with no provider or a cache key selector that returned `null`. `RuleCode`. |

**Context and parameters**

| Type or member | Purpose |
|----------------|---------|
| `BusinessRuleContext` | A key/value bag passed to rules: `Get`, `TryGet`, `Set`, `ContainsKey`, `Remove` (by string or `ContextKey<T>`), `Keys`, `Count`, `Clear`, `FormatProvider`. Not thread-safe. |
| `BusinessRuleContext<T>` | A context that also carries one typed `Data` object. |
| `ContextKey<T>` | A typed key (`Name`, `ValueType`), so reads need no cast and no string literal. |
| `ContextKeyNotFoundException` / `ContextValueTypeMismatchException` | Thrown by `Get`. They name the key, and list the available keys or the stored and requested types. |
| `RuleScope` | Who an evaluation is for: `Global`, `Tenant(id)`, `.With(key, value)`, `TenantId`, `IsGlobal`, `Specificity`, `TryGet(key)`, `ContainsKey(key)`, `Pairs`, `AppliesTo(requested)`. |
| `RuleParameters` | The resolved values passed to a `(ctx, p)` condition: `Get<T>`, `TryGet<T>`, `Contains`, `Names`, `Scope`. Holds the reserved context keys `ScopeKey`, `ContextKey`, `AsyncContextKey`. |
| `IRuleParameterProvider` / `IAsyncRuleParameterProvider` | Supply parameter values per scope: `TryGet<T>(name, scope, out value)` / `TryGetAsync<T>(name, scope, token)`. |
| `InMemoryRuleParameterProvider` | A thread-safe provider: `Set(name, value)`, `Set(name, value, scope)`, `Remove`, `Count`. The most specific matching scope wins. |
| `RuleParameterLookup<T>` | What `TryGetAsync` returns: `Found`, `Value`, `GetValueOrFallback(fallback)`; `Of(value)` and `Missing` to create one. |

**Rollout**

| Type or member | Purpose |
|----------------|---------|
| `.GatedBy(flag, provider, stableKey)` | Put a rule behind a feature flag. The provider and stable key are optional; without a provider, the rule looks for one on the context. |
| `FeatureGatedBusinessRule` / `FeatureGatedAsyncBusinessRule` | The gated rule: `Flag`, `Provider` (async also `AsyncProvider`), `HasStableKey`. |
| `RuleRollout` | A flag's state: `Off`, `Shadow(percent)`, `Enforced(percent)` (percent defaults to 100); `Mode`, `Percent`, `IsOff`, `IsComplete`. |
| `RuleRolloutMode` | `Off`, `Shadow`, `Enforced`. |
| `IRuleFeatureFlagProvider` / `IAsyncRuleFeatureFlagProvider` | Return a flag's `RuleRollout`: `GetRollout(flag, context)` / `GetRolloutAsync(flag, context, token)`. |
| `InMemoryRuleFeatureFlagProvider` | A thread-safe provider you can change at any time: `Set(flag, rollout)`, `Set(flag, mode, percent)`, `Remove`, `TryGet`, `DefaultRollout`. |
| `RuleFeatureFlags` | Context keys `ContextKey` / `AsyncContextKey` that override the provider for one evaluation. |
| `RuleRolloutBucket` | `Compute(flag, stableKey)` returns the bucket (0 to 99) a key falls into for a percentage rollout. |

**Specifications**

| Type or member | Purpose |
|----------------|---------|
| `SpecificationBusinessRule<TSubject>` | Base class for a rule written as an expression tree: override `BrokenWhen`. |
| `ISpecificationRule<TSubject>` | A subject-typed rule with `BrokenExpression` and `SatisfiedExpression`. |
| `.AllOf(others)` / `.AnyOf(others)` / `.Not()` | Combine specifications into one expression. Codes `ALL(A,B)`, `ANY(A,B)`, `!A`. |
| `AllOfSpecificationRule<TSubject>`, `AnyOfSpecificationRule<TSubject>`, `NotSpecificationRule<TSubject>` | The types these combinators return. They share the base `ComposedSpecificationRule<TSubject>`. |
| `query.WhereBroken(spec)` / `query.WhereSatisfies(spec)` | Filter an `IQueryable<T>` with a specification, so the database does the work. |
| `.ToAsync()` | Use a specification as an `IAsyncBusinessRule<TSubject>` (`AsyncSpecificationRuleAdapter<TSubject>`). |

**Testing helpers**

| Type or member | Purpose |
|----------------|---------|
| `RuleTestHelper.AssertBroken` / `AssertNotBroken` | Assert a rule's verdict, with or without a context. Async versions: `AssertBrokenAsync`, `AssertNotBrokenAsync`. |
| `RuleTestHelper.CreateContext(...)` | Create a context from `(key, value)` pairs, from typed `(ContextKey<T>, value)` pairs or from a data object. |
| `RuleTestHelper.CreateBrokenRule` / `CreatePassingRule` | Stub rules for tests. Async versions: `CreateBrokenAsyncRule`, `CreatePassingAsyncRule`. |
| `RuleTestHelper.CreateFlagProvider(flag, mode, percent)` | An `InMemoryRuleFeatureFlagProvider` with one flag set. |
| `RuleTestHelper.CreateParameterProvider(name, value, scope)` | An `InMemoryRuleParameterProvider` with one value set. `scope` is optional. |
| `RuleAssertionException` | Thrown when a test assertion fails. |

**Advanced extension points**

You only need these to write your own wrappers or plug in another condition language.

| Type or member | Purpose |
|----------------|---------|
| `IExplainableDecoratedBusinessRule` / `IExplainableDecoratedAsyncBusinessRule` | Implement for a custom wrapper that should appear as its own node in explain trees. |
| `DecoratedRuleEvaluation` / `AsyncDecoratedRuleEvaluation` | Passed to a custom wrapper. `EvaluateInner()` / `EvaluateInnerAsync()` runs the inner rule, at most once. |
| `IRuleConditionCompiler` / `IAsyncRuleConditionCompiler` | Turn condition text into a delegate for `When(compiler, text)` / `WhenAsync(compiler, text)`. |
| `IBuiltRuleFactory` / `IAsyncBuiltRuleFactory` | Decide which rule type `Build()` returns. Set with `WithRuleFactory(...)`. |
| `BuiltRuleDefinition` | What a factory receives: the builder's metadata, `ResolveParameters`, `ResolveParametersAsync`, `CreateResult`. |

## Masterly.BusinessRules.Expressions

| Type or member | Purpose |
|----------------|---------|
| `RuleExpression` | A parsed text condition: `Parse(text)`, `TryParse`, `Evaluate`, `EvaluateAsync`, `Explain`, `ExplainAsync`, `ToRule(code, message)`, `ToAsyncRule(code, message)`, `Compile<T>()`. |
| `.WhenExpression(text)` | Builder extension for `BusinessRuleBuilder` and `AsyncBusinessRuleBuilder`. Parses when you call it; `Build()` then returns an expression rule. |
| `ExpressionBusinessRule` / `ExpressionAsyncBusinessRule` | A rule over a `RuleExpression`: `Expression`, `ExplainExpression(context)` / `ExplainExpressionAsync(context)`. `Description` defaults to the expression text. |
| `RuleExpressionOptions` | Immutable limits and settings: `Default`, `MaxLength` (4096), `MaxDepth` (64), `MemberFilter`, `RequireRuleAccessibleAttribute`, `Culture` (invariant), each with a `With...` method. |
| `RuleAccessibleAttribute` | Marks the properties or types an expression may read when `RequireRuleAccessibleAttribute` is on. |
| `RuleExpressionExplanation` | The result of `Explain`: `Result`, `Steps` and the `Rendered` text. |
| `RuleExpressionStep` / `RuleExpressionStepKind` | One step of an explanation: `Kind` (`Binding` or `Operation`), `NodeText`, `BoundValue`, `Result`. |
| `RuleExpressionRedactor` | A delegate `(name, value)` that controls how a bound value appears in an explanation. |
| `RuleExpressionConditionCompiler` | The compiler to pass to `When(compiler, text)` / `WhenAsync(compiler, text)`. |
| `RuleExpressionParseException` | The text is malformed or exceeds a limit: `Line`, `Column`. |
| `RuleExpressionBindingException` | An identifier, parameter or member cannot be found: `Identifier`, `AvailableKeys`. |
| `RuleExpressionEvaluationException` | An operation failed at run time, for example on wrong operand types or division by zero: `NodeText`. |

:::note

`ExpressionBusinessRule.IsBroken()` without a context throws `InvalidOperationException`. Always pass a context.

:::

## Masterly.BusinessRules.Diagnostics

| Type or member | Purpose |
|----------------|---------|
| `LoggingRuleExecutionObserver` / `LoggingAsyncRuleExecutionObserver` | Observers that write one structured `ILogger` entry per rule, plus batch, fault and shadow entries. |
| `LoggingRuleObserverOptions` | `LogPassedRules`, `PassedLevel`, `BrokenLevelBySeverity`, `DefaultBrokenLevel`, `FaultedLevel`, `LogBatches`, `BatchLevel`, `ShadowViolationLevel`, `IncludeMessage`. |
| `RuleLogEvents` | The `EventId` values: `RuleBroken` (1), `RulePassed` (2), `RuleFaulted` (3), `BatchStarted` (4), `BatchCompleted` (5), `ShadowViolation` (6). |
| `TracingRuleExecutionObserver` / `TracingAsyncRuleExecutionObserver` | Observers that create a `BusinessRules.Batch` span with a `BusinessRule.Evaluate` child per rule, and record metrics. |
| `TracingRuleObserverOptions` | `MessageAsEvent` (default false), `CodeTagSelector`, `RecordMetrics` (default true), `Metrics`. |
| `.Traced()` / `.Traced(logger)` | Wrap a single rule so each evaluation creates a span and a measurement, and with a logger also a log entry. Returns `DiagnosticsBusinessRule` / `DiagnosticsAsyncBusinessRule`. |
| `BusinessRulesActivitySource` | The `ActivitySource` to subscribe to: `Name` (`Masterly.BusinessRules`), `Source`, `RuleActivityName`, `BatchActivityName`. |
| `BusinessRuleSpanAttributes` | The span attribute and event names, such as `business_rule.code` and `business_rule.outcome`. |
| `BusinessRulesMetrics` | The `Meter` (`Masterly.BusinessRules`) with `business_rules.evaluations`, `business_rules.evaluation.duration` (ms) and `business_rules.batches`. `Default` is shared; pass your own `Meter` to the constructor to own it. |
| `RuleOutcomeTags` / `RuleSeverityTags` | Tag values used on spans and metrics: `passed`, `broken`, `faulted`, `shadow`, `cancelled`; `info`, `warning`, `error`. |

## Masterly.BusinessRules.DependencyInjection

| Type or member | Purpose |
|----------------|---------|
| `services.AddBusinessRules(assemblies)` / `services.AddBusinessRules(options => ...)` | Scan assemblies for rule types and register them, `IRuleRunner<T>` and `IRuleRegistry`. |
| `BusinessRuleOptions` | `Lifetime` (default `Scoped`), `IncludeNonPublicTypes`, `IncludeUntypedRules` (default false), `TypeFilter`, `AddAssembly`, `AddAssemblies`, `AddAssemblyContaining<T>()`, `AddRuleSet(...)`. |
| `IRuleRunner<TSubject>` | Inject to run every registered rule for one subject type: `CheckAll`, `EvaluateAll`, `CheckAllAsync`, `EvaluateAllAsync`; `Rules`, `AsyncRules`, `HasAsyncRules`. |
| `IRuleRegistry` / `RuleRegistry` | Named rule sets with per-scope overrides: `SetNames`, `Contains`, `Resolve`, `ResolveAsyncRules`, `CheckAll`, `EvaluateAll`, `CheckAllAsync`, `EvaluateAllAsync`. `RuleRegistry.Create()` starts a builder. |
| `RuleRegistryBuilder` | `AddSet(name, configure)` and `AddSet(name, scope, configure)`, then `Build()`. |
| `RuleSetBuilder` | One layer of a set: `Add`, `AddAsyncRule`, `Disable(code)`, `Replace(code, factory)`, `ReplaceAsyncRule(code, factory)`. |
| `RuleSetNotFoundException` | An unknown set name was requested: `SetName`, `AvailableSets`. |

:::note

Plain `IBusinessRule` / `IAsyncBusinessRule` classes are scanned only when `IncludeUntypedRules = true`. The sync `CheckAll` / `EvaluateAll` on a runner or registry throw `InvalidOperationException` when async rules are registered; use the async methods instead.

:::

## Masterly.BusinessRules.FeatureManagement

| Type or member | Purpose |
|----------------|---------|
| `services.AddBusinessRulesFeatureManagement(configure, lifetime)` | Register the provider below for both provider interfaces. Microsoft.FeatureManagement must also be registered. Both arguments are optional; `lifetime` defaults to `Singleton`. |
| `FeatureManagementRuleFeatureFlagProvider` | Reads rollouts from `IVariantFeatureManager` (variants supported) or `IFeatureManager` (on/off only): `Options`, `SupportsVariants`. |
| `FeatureManagementRolloutOptions` | `EnabledRollout` (default `Enforced`), `OnUnrecognizedVariant` (default `Throw`), `TargetingContextSelector`, `MapVariant(name, rollout)`, `MapVariant(name, mode, percent)` (percent defaults to 100). |
| `UnrecognizedVariantBehavior` | What to do with a variant name that does not map to a rollout: `Throw`, `Off`, `UseEnabledState`. |
| `RolloutVariantConvention` | Parses variant names such as `Shadow`, `Enforced-10` and `Off` (`Parse`, `TryParse`), and `Mode` / `Percent` configuration (`TryRead`). |
| `RolloutVariantFormatException` | Thrown for a variant that cannot be mapped when the behavior is `Throw`: `Flag`, `Variant`. |

:::note

The provider's sync `GetRollout` blocks on the async feature manager. Prefer async rules when you gate with this provider.

:::

## Masterly.BusinessRules.Testing

This package targets `net8.0` and `net10.0`.

| Type or member | Purpose |
|----------------|---------|
| `RuleCoverage.Begin()` | Start a `RuleCoverageSession` that records which outcomes each rule produced. |
| `RuleCoverageSession` | `Register(rule)`, `Record(rule, isBroken)`, `Report()`, `Reset()`, `RuleCount`, `IsEnded`. Thread-safe. Disposing it stops recording. |
| `.WithCoverage(session)` | Wrap a rule so every verdict is recorded in the session (`CoverageBusinessRule` / `CoverageAsyncBusinessRule`). |
| `RuleCoverageReport` | `OutcomeCoverage`, `IsComplete`, `NeverBroken`, `NeverPassed`, `NeverEvaluated`, `Find(code)`, `ToText()`. `AssertMinimum(coverage)` throws `RuleAssertionException` below the minimum. |
| `RuleCoverageEntry` | One rule's counts: `Code`, `BrokenCount`, `PassedCount`, `SeenBroken`, `SeenPassed`, `IsFullyCovered`. |
| `RuleBoundaryFinder.Find` / `FindAsync` | Find where a numeric rule switches from passing to broken, for any numeric `T` (`INumber<T>`). |
| `RuleBoundary<T>` | `LastPassing`, `FirstBroken`, `Monotonicity`, `ProbeCount`, `Step`. |
| `Monotonicity` | `BrokenAbove`, `BrokenBelow`. |
| `RuleProperties.For(factory, generator)` | Start property-based checks for a rule over CsCheck-generated inputs. Returns `RuleProperties<T>` or `AsyncRuleProperties<T>`. |
| `RuleProperties<T>` / `AsyncRuleProperties<T>` | Declare laws with `MonotoneIn(monotonicity)` and `ContextPure()`, then run `Verify(iterations, seed)` / `VerifyAsync(...)`. |
| `.Invert()` / `.AlwaysPass()` / `.DowngradeSeverity()` | Mutants that check whether your tests notice a broken rule: an inverted verdict, a rule that never breaks, a severity one level lower (`Info` is the lowest). |
| `InvertedBusinessRule`, `AlwaysPassingBusinessRule` | The mutant types. Async versions exist. |
