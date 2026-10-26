## [3.0.0] - Unreleased

### Breaking Changes

- `And()`, `Or()` and `Not()` now return the structural nodes `AndBusinessRule`, `OrBusinessRule`, `NotBusinessRule` (async: `AndAsyncBusinessRule`, `OrAsyncBusinessRule`, `NotAsyncBusinessRule`) instead of a `SimpleBusinessRule` / delegate `CompositeAsyncBusinessRule`. The truth tables, synthesised codes (`A+B`, `A|B`, `!A`) and messages are unchanged; `Name` now reports the node type name (for example `AndBusinessRule`), `Description` is a short operator description, and the runtime type of a composed rule is different
- Sync `And()`/`Or()`/`Not()` now forward the context passed to `IsBroken(context)`/`Check(context)` to their operands; previously they always called the parameterless `IsBroken()`
- `CompositeBusinessRule` and `CompositeAsyncBusinessRule` implement `IComposedBusinessRule` / `IComposedAsyncBusinessRule` with `Operator = RuleOperator.All`. `IsBroken` / `IsBrokenAsync` now evaluate every child (previously they stopped at the first broken child); `Check`/`EvaluateAll` already did
- `BusinessRuleChecker.CheckAll`/`EvaluateAll` evaluate in-library composed and decorated rules through a single internal evaluation instead of `IsBroken()` followed by `Evaluate()`; the reported result for a group or composite now carries its broken members in `Details`
- `SyncToAsyncRuleAdapter` and `AsyncToSyncRuleAdapter` evaluate the wrapped rule through its `Evaluate`/`EvaluateAsync` path so member details flow across the adapter; the blocking behaviour of `AsyncToSyncRuleAdapter` is unchanged
- `IBusinessRuleMetadata` is now a real interface (`Code`, `Message`, and defaulted `Severity`, `Name`, `Description`, `Category`, `Tags`) and `IBusinessRule`, `IAsyncBusinessRule`, `IBusinessRule<TSubject>` and `IAsyncBusinessRule<TSubject>` extend it; the seven metadata members moved from the two non-generic interfaces to the base. Implementors are source-compatible (the members and their defaults are unchanged), but the interface hierarchy is different and code that enumerated the members of `IBusinessRule` by reflection sees them on the base interface
- The subject-typed checker overloads `CheckAll<T>(T subject, IEnumerable<IBusinessRule<T>> rules, ...)` / `EvaluateAll<T>(...)` (and the async twins) win overload resolution over the non-generic overloads when a bare `null` literal is passed as the context (for example `EvaluateAll(rules, null)` or `CheckAll(rules, null, options)`), because the rule collection then binds to the generic subject parameter; such calls throw `ArgumentNullException` for `rules`. Omit the context or type the literal (`(BusinessRuleContext?)null`); a typed variable is unaffected

### Added

- `IComposedBusinessRule` / `IComposedAsyncBusinessRule` (`RuleOperator Operator`, `Operands`) and `IDecoratedBusinessRule` / `IDecoratedAsyncBusinessRule` (`InnerRule`): the walker interfaces implemented by every And/Or/Not node, group and composite, and by the cached and conditional decorators. Both adapters expose their wrapped rule through an `InnerRule` property typed as the adapted side
- `RuleGroup.All/Any/AtLeast/Exactly` and `AsyncRuleGroup.All/Any/AtLeast/Exactly` returning the immutable `GroupBusinessRule` / `GroupAsyncBusinessRule` with `WithShortCircuit()`, `WithSeverity()` and (async) `WithParallelEvaluation()`; parallel plus short-circuit is rejected with `InvalidOperationException`; parallel groups await every member so faults are observed; empty member lists, null members and out-of-range thresholds are rejected at construction
- `RuleGroupQuantifier`, `RuleOperator` and `RuleOutcome` enums
- `RuleExplanation` tree node (`Rule`/`AsyncRule`, `Code`, `Message`, `Severity`, `Outcome`, `IsBroken`, `Operator`, `Reason`, `Duration`, `Fault`, `Children`, `BrokenLeaves()`, `ToText()`)
- `rule.Explain(context = null, captureExceptions = false)` and `asyncRule.ExplainAsync(context, [captureExceptions], cancellationToken)` extension methods: one real evaluation, trace as by-product; decorators report `Skipped` / `CacheHit`, short-circuited operands report `NotEvaluated`, throwing nodes report `Faulted` only when `captureExceptions` is true (cancellation always propagates); third-party rules are single leaves; adapters expand into the other side's subtree
- `BusinessRuleResult.Details` plus the constructor overload `BusinessRuleResult(code, message, severity, IEnumerable<BusinessRuleResult> details)`; `Details` is empty for the existing constructor
- `BusinessRuleResult.Flatten()` and `BusinessRuleValidationException.Flatten()` returning the leaf results of nested details
- Evaluation ledger: `RuleEvaluationEntry` (`Order`, `Rule`/`AsyncRule`, `Code`, `Outcome`, `Result`, `Fault`, `Duration`, `IsBroken`, `IsFaulted`) and, on both `RuleEvaluationResult` and `AsyncRuleEvaluationResult`, `Entries`, `Faulted`, `HasFaults`, `TotalDuration`, `Slowest`, `ThrowIfAtLeast(severity)` plus a second constructor `(entries, totalDuration)` that derives `BrokenRules`/`PassedRules` from the ledger; results built with the existing constructor have an empty ledger. Timing uses `Stopwatch.GetTimestamp()` tick math (no `Stopwatch` allocation)
- `RuleEvaluationOptions` (`StopOnFirstFailure`, `OnFault`, `ThrowOn`, `Observer`) and `AsyncRuleEvaluationOptions` (same plus `RunInParallel`; fail-fast still wins over parallel) with options-taking `CheckAll`/`EvaluateAll` and `CheckAllAsync`/`EvaluateAllAsync` overloads on both checkers; the existing boolean overloads are kept and now route through the same core
- `FaultPolicy` (`Throw`, `Record`, `TreatAsBroken`; no `TreatAsPassed`): a rule that throws now either propagates after `OnRuleFaulted` (default, pre-3.0 behaviour), is recorded as a `Faulted` ledger entry that is neither broken nor passed, or is additionally reported as a broken result built from the rule's `Code`/`Message`/`Severity` (a throwing `Code` or `Message` getter falls back to the type name; the ledger entry reads `Code` on demand so it never masks the fault). Cancellation always propagates. Faults are isolated per task in parallel async batches
- Severity threshold: `RuleEvaluationOptions.ThrowOn`/`AsyncRuleEvaluationOptions.ThrowOn` make `CheckAll`/`CheckAllAsync` throw only for broken rules at or above the severity; lower-severity broken rules are returned on the new `BusinessRuleValidationException.CollectedRules` and the ledger on `BusinessRuleValidationException.Entries` (new constructor overload `(brokenRules, collectedRules, entries)`). With fail-fast, only a rule at or above `ThrowOn` stops a `CheckAll` batch. `EvaluateAll` ignores `ThrowOn` and never throws for broken rules
- `rule.Check(throwOn)`, `rule.Check(context, throwOn)` and `asyncRule.CheckAsync(context, throwOn, ct)` extension methods applying the severity threshold to a single rule
- `SeverityOverrideBusinessRule` / `SeverityOverrideAsyncBusinessRule` decorators and the `rule.WithSeverity(severity)` extensions (both sides): report a different severity while forwarding every other metadata member, the verdict and member `Details`; implement `IDecoratedBusinessRule`/`IDecoratedAsyncBusinessRule` and appear as a `SEVERITY` node in explain trees
- Observer batch lifecycle hooks as default interface members on `IRuleExecutionObserver` (`OnBatchStarted(ruleCount)`, `OnRuleFaulted(rule, exception)`, `OnAfterEvaluate(rule, isBroken, elapsed)`, `OnBatchCompleted(result)`) and `IAsyncRuleExecutionObserver` (`OnBatchStartedAsync`, `OnRuleFaultedAsync`, `OnAfterEvaluateAsync(rule, isBroken, elapsed, ct)`, `OnBatchCompletedAsync`); existing observers keep compiling
- Rich results, increment 1: `BusinessRuleResult.Name`, `Category` and `Tags`, populated at every construction site (base classes, builders, decorators, adapters, And/Or/Not nodes, groups, composites, the checkers' `TreatAsBroken` stand-in and the walkers' captured-fault result). Results built with the existing `(code, message, severity)` constructors report the code as `Name`, an empty category and no tags. New constructors `(code, message, severity, name, category, tags)` and the full `(code, message, severity, name, category, tags, details, memberDetails, formatProvider)`; `BusinessRuleResult.HasTag(tag)` (case-insensitive) and `BusinessRuleResult.WithSeverity(severity)` (copy keeping every other member)
- Rich results, increment 1: `ByTag(tag)` (case-insensitive) and `ByCategory(category)` (ordinal) filters on `BusinessRuleValidationException`, `RuleEvaluationResult` and `AsyncRuleEvaluationResult`
- Rich results, increment 2: `RuleResultDetails`, an immutable fluent bag (`RuleResultDetails.Detail($"...")`, `.Actual(value)`, `.Expected(value)`, `.Remediation(text)`, `.With(key, value)`, `Empty`, `IsEmpty`, `FormatDetail(provider)`) and the matching `BusinessRuleResult.Detail`, `Actual`, `Expected`, `Remediation` and `Data` members plus the `BusinessRuleResult(code, message, severity, RuleResultDetails details)` constructor. `Detail` is a `FormattableString` formatted when the result is built, with the new `BusinessRuleContext.FormatProvider` when set and the invariant culture otherwise; `Actual`/`Expected` are `object?` and serialise by runtime type; `Message` stays a finished string (no templating)
- Rich results, increment 2: `protected virtual RuleResultDetails Describe(BusinessRuleContext? context)` on `BaseBusinessRule` and `BaseAsyncBusinessRule`, called only on the broken path (never from `IsBroken`/`IsBrokenAsync`, never for a passing rule) and guarded so that an exception inside it degrades to `RuleResultDetails.Empty` instead of masking the violation; cancellation propagates. `BaseBusinessRule.Evaluate()` is now virtual and `BaseBusinessRule.Evaluate(context)` calls `Describe(context)`; `Evaluate()` calls `Describe(null)`
- Rich results, increment 2: `IBusinessRule.Evaluate(BusinessRuleContext context)` as a default interface member falling back to `Evaluate()`, declared explicitly on `AndBusinessRule`, `OrBusinessRule`, `NotBusinessRule`, `GroupBusinessRule`, `CompositeBusinessRule`, `CachedBusinessRule`, `ConditionalBusinessRule`, `SeverityOverrideBusinessRule` and `AsyncToSyncRuleAdapter` so callers holding the concrete types see it. `Check(context)`, `CompositeBusinessRule.Check(context)`/`EvaluateAll(context)`, the sync walker (and therefore `BusinessRuleChecker`, groups, composites and And/Or/Not) route broken leaves through the context overload
- Typed context keys: `ContextKey<T>` (`readonly struct`, `Name`, `ValueType`, `IEquatable<ContextKey<T>>` with ordinal name equality, `==`/`!=`, null and blank names rejected) and the typed `BusinessRuleContext` overloads `Get(ContextKey<T>)`, `TryGet(ContextKey<T>, out T)`, `Set(ContextKey<T>, T)`, `ContainsKey(ContextKey<T>)` and `Remove(ContextKey<T>)`, all delegating to the string API through `key.Name` so typed and string access interoperate; `BusinessRuleContext.Keys` (`IReadOnlyCollection<string>`, a read-only live view of the stored key names)
- Descriptive context exceptions: `ContextKeyNotFoundException` (derives from `KeyNotFoundException`; `Key`, `AvailableKeys`; message `Context has no key 'ordr'. Available keys: customer, limit, order`, or `The context is empty.`) and `ContextValueTypeMismatchException` (derives from `InvalidCastException`; `Key`, `ActualType` (null for a stored null), `RequestedType`; message `Key 'order' holds Int32, requested Order`), both thrown by `BusinessRuleContext.Get<T>(string)` and therefore by the typed `Get`
- `BusinessRuleContextException` (`RuleCode`, `Key`, original exception as `InnerException`): `BusinessRuleChecker` and `AsyncBusinessRuleChecker` wrap a `ContextKeyNotFoundException` / `ContextValueTypeMismatchException` thrown while evaluating a rule into it, with the message `Rule LIMIT.EXCEEDED failed to read context: ...`. The wrapped exception is what propagates under `FaultPolicy.Throw`, what the ledger entry's `Fault` holds under `Record`/`TreatAsBroken`, and what `OnRuleFaulted`/`OnRuleFaultedAsync` receive; a throwing `Code` getter falls back to the rule's type name; other exceptions, bare `KeyNotFoundException`s and already-wrapped exceptions propagate as the same instance; cancellation is never wrapped
- `RuleTestHelper.CreateContext` overloads taking one to four typed `(ContextKey<T>, T)` pairs of different value types, e.g. `RuleTestHelper.CreateContext((OrderKeys.Order, order), (OrderKeys.Limit, 1000m))`
- Rich results, increment 2: `WithDetail(Func<ctx, FormattableString>)`, `WithActual(Func<ctx, object?>)`, `WithExpected(Func<ctx, object?>)`/`WithExpected(object?)`, `WithRemediation(string)`/`WithRemediation(Func<ctx, string>)` on `BusinessRuleBuilder` and `AsyncBusinessRuleBuilder`; the functions run only on the broken path, receive the evaluation context (an empty context on the parameterless `Evaluate()` path) and degrade to empty details as a whole when any of them throws
- Subject-typed rules: `IBusinessRule<in TSubject>` (`IsBroken(subject)`, `IsBroken(subject, context)`, `Evaluate(subject)` and the default member `Evaluate(subject, context)`) and `IAsyncBusinessRule<in TSubject>` (`IsBrokenAsync(subject, context, ct)`, `EvaluateAsync(subject, context, ct)`), both extending `IBusinessRuleMetadata` and neither deriving from the non-generic interfaces, so no member ever throws for lack of a subject; value-type subjects are supported and the reserved `__typedData` context key is not involved
- `BaseBusinessRule<TSubject>` / `BaseAsyncBusinessRule<TSubject>`: abstract `Code`, `Message` and `IsBroken(subject)` / `IsBrokenAsync(subject, context, ct)`, virtual metadata, `IsBroken(subject, context)` defaulting to the subject-only overload, `Evaluate(subject[, context])` / `EvaluateAsync` factories and the guarded `protected virtual Describe(subject, context)` hook
- `BoundBusinessRule<TSubject>` / `BoundAsyncBusinessRule<TSubject>` (`Rule`, `Subject`) and the `rule.For(subject)` extensions: the only bridge from a typed rule to `IBusinessRule` / `IAsyncBusinessRule`; forward all seven metadata members, evaluate the typed rule against the bound subject, pass the context through, and work with every decorator, operator, checker and `Explain` (as a leaf). A null subject is handed to the rule as-is
- Subject-typed checker overloads on `BusinessRuleChecker`: `CheckAll(subject, rules[, context])`, `CheckAll(subject, rules, [context,] options)`, `EvaluateAll(subject, rules[, context])`, `EvaluateAll(subject, rules, [context,] options)`, `CheckBySeverity(subject, rules, severities)`, `CheckByCategory(subject, rules, category)`, `CheckByTags(subject, rules, tags)` and `EvaluateEach(subjects, rules[, context][, options])`. Every overload binds the rules with `.For(subject)` and runs the existing core, so the ledger, fault policy, `ThrowOn`, fail-fast and observer callbacks are unchanged; observers and ledger entries see the `BoundBusinessRule<T>` instances. A null rule set throws `ArgumentNullException` and a null rule inside it the same positional `ArgumentException` as the non-generic batch
- Subject-typed overloads on `AsyncBusinessRuleChecker`: `CheckAllAsync([context,] subject, rules[, options], ct)`, `EvaluateAllAsync([context,] subject, rules[, options], ct)`, `CheckBySeverityAsync(context, subject, rules, ct, severities)`, `CheckByCategoryAsync(context, subject, rules, category, ct)`, `CheckByTagsAsync(context, subject, rules, ct, tags)` and `EvaluateEachAsync([context,] subjects, rules[, options], ct)`; the overloads without a context evaluate against a fresh context, and `EvaluateEachAsync` evaluates subjects sequentially while each subject's batch honours `RunInParallel`
- `AsyncBusinessRuleChecker.CheckByTagsAsync(context, rules, ct, tags)` for non-generic async rules, mirroring the sync `CheckByTags`
- `SubjectEvaluation<TSubject>` (`Index`, `Subject`, `Result`, `AllPassed`, `HasBrokenRules`, `BrokenRules`) and `SubjectEvaluationCollection<TSubject>` (an `IReadOnlyList<SubjectEvaluation<TSubject>>` with `AllPassed`, `HasBrokenRules`, `Broken`, `Passed`, `BrokenRules`, `TotalDuration`), plus `AsyncSubjectEvaluation<TSubject>` / `AsyncSubjectEvaluationCollection<TSubject>` over `AsyncRuleEvaluationResult`: the result of `EvaluateEach` / `EvaluateEachAsync`. Subjects are reported by position, never keyed by equality, so duplicates and value types are reported separately; an empty batch reports `AllPassed`
- `BusinessRuleBuilder<TSubject>` / `AsyncBusinessRuleBuilder<TSubject>`: `When(Func<T, bool>)`, `When(Func<T, ctx, bool>)` / `WhenAsync` with `(subject)`, `(subject, ctx)` or `(subject, ctx, ct)`, and subject-aware `WithDetail`, `WithActual`, `WithExpected`, `WithRemediation` overloads taking `(subject, context)` that take precedence over the inherited context-only ones; `Build()` returns `IBusinessRule<TSubject>` / `IAsyncBusinessRule<TSubject>` and throws `InvalidOperationException` without a condition
- `BusinessRuleMetadataBuilder<TBuilder>`: the shared generic base of all four builders holding `WithMessage`, `WithSeverity`, `WithName`, `WithDescription`, `WithCategory`, `WithTags`, `WithDetail`, `WithActual`, `WithExpected` and `WithRemediation`, each returning the concrete builder; it cannot be derived from outside the library
- Typed composition: `rule.And(other)`, `rule.Or(other)` and `rule.Not()` on `IBusinessRule<TSubject>` / `IAsyncBusinessRule<TSubject>` returning `AndBusinessRule<TSubject>`, `OrBusinessRule<TSubject>`, `NotBusinessRule<TSubject>` and their async twins (`Operator`, `Operands`), with the same truth tables, short-circuit, synthesised codes (`A+B`, `A|B`, `!A`) and messages as the non-generic nodes; the subject, the context and (async) the cancellation token are forwarded to every operand
- Shadow rollout and feature-gated rules: `FeatureGatedBusinessRule` / `FeatureGatedAsyncBusinessRule` decorators and the `rule.GatedBy(flag, provider, stableKey)` / `rule.GatedBy(flag, stableKey)` extensions (both sides; the async form accepts an `IAsyncRuleFeatureFlagProvider`, an `IRuleFeatureFlagProvider` or a provider implementing both). The rollout is read from the provider on every evaluation: `Off` (or a caller outside the cohort) passes without evaluating the inner rule, `Shadow` evaluates the inner rule but always passes and reports the violation, `Enforced` forwards the verdict. All seven metadata members are forwarded; both decorators implement `IDecoratedBusinessRule` / `IDecoratedAsyncBusinessRule` and appear as `GATED` (`Skipped` when closed) or `SHADOW` nodes in explain trees. The sync parameterless `IsBroken()` / `Check()` evaluate the inner rule un-gated and never consult the provider, so context-less composition cannot throw
- `RuleRolloutMode` (`Off`, `Shadow`, `Enforced`), the `RuleRollout` readonly struct (`Mode`, `Percent` 0..100, `IsOff`, `IsComplete`, `Includes(bucket)`, `Off`, `Shadow(percent)`, `Enforced(percent)`, value equality; `default` is `Off`) and `RuleRolloutBucket` (`Compute(flag, stableKey)` into buckets 0..99 with in-house 32-bit FNV-1a over `flag:stableKey`, `Hash(text)`, `BucketCount`); a rollout at 100% never calls the stable key selector, a partial rollout without a selector (or whose selector returns null) throws `RuleConfigurationException`
- `IRuleFeatureFlagProvider.GetRollout(flag, context)` and `IAsyncRuleFeatureFlagProvider.GetRolloutAsync(flag, context, ct)`; providers are resolved per evaluation as `RuleFeatureFlags.AsyncContextKey` (async decorator only), then `RuleFeatureFlags.ContextKey`, then the injected async provider, then the injected sync provider, and a missing provider throws `RuleConfigurationException` naming the rule's `Code`
- `RuleFeatureFlags` (`ContextKey`, `AsyncContextKey`, `ProviderKeyName`, `AsyncProviderKeyName`), `RuleConfigurationException` (derives from `InvalidOperationException`; `RuleCode`), the thread-safe `InMemoryRuleFeatureFlagProvider` implementing both provider interfaces (`Set(flag, rollout)`, `Set(flag, mode, percent)`, `Remove`, `TryGet`, `Count`, `DefaultRollout`; unknown flags report `Off`) and `RuleTestHelper.CreateFlagProvider(flag, mode, percent = 100)`
- `RuleEvaluationResult.ShadowResults` / `AsyncRuleEvaluationResult.ShadowResults` and `HasShadowResults`, populated by every checker overload evaluated with a context (the sync checker without a context evaluates gated rules un-gated) with the results shadow-mode gated rules would have reported, including gated rules nested in groups, caches and adapters; they are not in `BrokenRules`, do not affect `AllPassed` and never make `CheckAll` or `ThrowIfBroken` throw. New constructors `(entries, totalDuration, shadowResults)` on both result types; the existing constructors report no shadow results
- Observer hooks `IRuleExecutionObserver.OnShadowViolation(rule, result)` and `IAsyncRuleExecutionObserver.OnShadowViolationAsync(rule, result, ct)` as default interface members, raised by the options-taking checker overloads after the rule's own callbacks (in a parallel async batch once every rule has completed, before `OnBatchCompletedAsync`); a gated rule of the other hierarchy reached through an adapter is handed to the observer wrapped in `AsyncToSyncRuleAdapter` / `SyncToAsyncRuleAdapter`

- `Directory.Build.props` at the repository root now holds the shared package version (`3.0.0`), authors, repository, licence, icon and the packed README/LICENSE/icon files for every project, so sibling packages ship with one version number
- `publish.yml` packs and pushes every packable project under `src/` (`dotnet pack` + `dotnet nuget push --skip-duplicate`) and reads the release version from `Directory.Build.props`
- `CompositionTruthTableTests` pins the full `And()`/`Or()`/`Not()` truth tables on both the sync and async side

### Changed

- `src/Masterly.BusinessRules/Masterly.BusinessRules.csproj` keeps only its `TargetFramework`, `Description` and `PackageTags`; all other package metadata is inherited from `Directory.Build.props`
- `And()`/`Or()`/`Not()`, groups, composites, cached and conditional decorators, both adapters and the sync checker route evaluation through one internal `EvaluateCore` per node so that `Check`, `Evaluate`, the checkers and `Explain` share a single evaluation path; `Stopwatch` timing and explanation nodes are allocated only inside an `Explain` walk
- `SimpleBusinessRule` is no longer used by the composition operators but remains available as a public type
- Observers now fire from `EvaluateAll`/`EvaluateAllAsync` when called through the options-taking overloads (`RuleEvaluationOptions.Observer` / `AsyncRuleEvaluationOptions.Observer`); previously only `CheckAll`/`CheckAllAsync` raised observer callbacks. The boolean `EvaluateAll` overloads still have no observer. `OnBatchCompleted` is raised before `CheckAll` throws and is not raised when a fault propagates under `FaultPolicy.Throw`
- Every `BusinessRuleChecker`/`AsyncBusinessRuleChecker` overload, including the existing boolean ones, now runs through one core and returns or carries a populated ledger; a null `rules` collection or a null rule inside the batch is rejected with an `ArgumentNullException`/`ArgumentException` instead of a `NullReferenceException`. The async checker evaluates every rule through the same walker as the sync checker
- `RuleOutcome.NotEvaluated` is also reported for rules a fail-fast batch never reached, and `RuleOutcome.Faulted` for rules recorded under `FaultPolicy.Record`/`TreatAsBroken`
- `CachedBusinessRule.Evaluate()`/`Evaluate(context)` and `ConditionalBusinessRule.Evaluate(context)` forward to the inner rule's factory, and `SeverityOverrideBusinessRule`/`SeverityOverrideAsyncBusinessRule` copy the inner result with `BusinessRuleResult.WithSeverity` instead of rebuilding it, so metadata and `Describe` details survive every decorator. A served `CachedBusinessRule` cache hit now reports the inner rule's `Evaluate(context)` factory result (metadata and `Describe` details, no member details); a served `CachedAsyncBusinessRule` cache hit reports the forwarded metadata only
- `RuleFaultResult` (the `FaultPolicy.TreatAsBroken` stand-in) carries the faulted rule's `Name`, `Category` and `Tags`; when a metadata getter throws, the fallback reports the rule's type name as both `Code` and `Name`
- A rule whose `Name`, `Category` or `Tags` getter returns null (a contract violation older, nullable-oblivious rules can commit) is reported with the code as `Name`, an empty category and no tags; result construction through the base classes, the walkers and the checkers never throws for it
- Passing a bare `null` literal as the fourth argument of `new BusinessRuleResult(code, message, severity, null)` is now ambiguous between the `IEnumerable<BusinessRuleResult>` and `RuleResultDetails` overloads and must be typed; both overloads throw `ArgumentNullException` for null
- README gains the "Rich Results" section; the "Evaluation Results" section lists `ByTag`/`ByCategory` and the API response example includes `Name`, `Category` and `Detail`
- `BusinessRuleContext.Get<T>(string)` now throws `ContextKeyNotFoundException` instead of a bare `KeyNotFoundException` for a missing key and `ContextValueTypeMismatchException` instead of a bare `InvalidCastException` for a stored value of another type; both derive from the previous exception types so existing `catch` blocks keep working. A null key is rejected with `ArgumentNullException`. A stored null is returned as null when `T` allows null (unchanged) and reported as a type mismatch (`holds null`) when `T` is a non-nullable value type instead of a `NullReferenceException`
- `BusinessRuleChecker.CheckAll`/`EvaluateAll` and `AsyncBusinessRuleChecker.CheckAllAsync`/`EvaluateAllAsync` surface a rule's context read failure as `BusinessRuleContextException` (original exception as `InnerException`) instead of letting the bare `KeyNotFoundException`/`InvalidCastException` propagate; code that caught those types around a checker call should catch `BusinessRuleContextException` (or inspect `InnerException`)
- README gains the "Typed Context Keys" section; the "Context and Data Passing" section lists `Keys` and the descriptive exceptions, and the "Testing Utilities" section lists the typed `CreateContext` overloads
- README gains the "Structural Composition and Explain Trees" section; the "Rule Composition" section now points to `RuleGroup.All/Any` as the unambiguous forms and documents that sync operators forward the context
- `BusinessRuleBuilder` and `AsyncBusinessRuleBuilder` now derive from `BusinessRuleMetadataBuilder<TBuilder>`; their public surface, defaults (`Name` falls back to the code, default messages unchanged) and behaviour are unchanged
- The And/Or/Not nodes of every hierarchy read their synthesised code separators, message prefixes and descriptions from one internal constant set; the strings are unchanged
- README gains the "Subject-Typed Rules" section; the Features bullet list and the API Reference table list the typed interfaces, bases, builders, bound adapters, typed nodes, subject evaluations and the typed checker overloads
- `BusinessRuleChecker` and `AsyncBusinessRuleChecker` attach an internal shadow-violation sink to the batch context for the duration of a batch (kept beside the key/value store, so `Keys`, `Count`, `ContainsKey` and the available-keys list of a `ContextKeyNotFoundException` are unaffected) and detach it when the batch completes, throws or is cancelled; a nested checker call on the same context shares the outer sink
- README gains the "Shadow Rollout and Feature-Gated Rules" section (including the interaction with `CachedBusinessRule`: cache inside the gate, never outside); the "Observers", "Evaluation Results" and "Testing Utilities" sections, the Features bullet list and the API Reference table list the new hooks, properties, types and the `.GatedBy()` extensions

### Fixed

- `CachedBusinessRule.Check()`/`Check(context)` and `CachedAsyncBusinessRule.EvaluateAsync`/`CheckAsync` rebuilt a bare result after evaluating the inner rule and so dropped the `Details` of a cached group or composite; they now report the result of the single inner evaluation, so `AsyncBusinessRuleChecker` surfaces member details through a cached rule. A served cache hit still reports a result without details because only the verdict is cached
- `AsyncBusinessRuleChecker.CheckBySeverityAsync` and `CheckByCategoryAsync` (non-generic) ignored their `cancellationToken` and evaluated the filtered rules with a default token; they now forward it like `CheckByTagsAsync` and the subject-typed filter overloads, so a cancelled token propagates `OperationCanceledException`
- The rule builders handed the built rule a live view of the builder's tag list, so a later `WithTags` call on the same builder changed the `Tags` of a rule already built; `Build()` now snapshots the tags on every builder
- README named the sync-to-async extension `.ToAsyncRule()`; the method is `.ToAsync()` and the "Rule Adapters" section and the API Reference table now say so
- README "Rule Composition" documented `And()` as "broken if either rule is broken" and `Or()` as "broken only if both are broken", which is the opposite of what the code does and has always done. The code semantics are unchanged and now pinned by tests: `And()` is broken only when **both** operands are broken, `Or()` is broken when **either** operand is broken, `Not()` inverts the broken state. The README, the Features bullet and the API Reference table now describe the actual behaviour.

## [2.0.0] - 2025-01-01

### Breaking Changes

- `IBusinessRule` and `IAsyncBusinessRule` now require additional metadata properties: `Name`, `Description`, `Category`, and `Tags`
- Existing rule implementations must be updated to provide these properties (base classes provide defaults)

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
- `HasBrokenRules`, `HasPassedRules`, `AllRulesPassed`, `AllRulesBroken` properties
- `GetBrokenByCode()`, `GetBrokenBySeverity()` filtering methods

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
- `CheckByTags()` - Filter rules by tags

#### Extension Methods
- `ToAsyncRule()` - Convert sync rule to async rule
- `ToSyncRule()` - Convert async rule to sync rule (blocking)
- `WithCache()` - Add caching to any rule
- `WithCondition()` - Add conditional execution to any rule

### Changed

- Comprehensive XML documentation added to all public APIs
- All interfaces now inherit from `IBusinessRuleMetadata` for consistent metadata