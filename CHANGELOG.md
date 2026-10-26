## [3.0.0] - Unreleased

### Breaking Changes

- `And()`, `Or()` and `Not()` now return the structural nodes `AndBusinessRule`, `OrBusinessRule`, `NotBusinessRule` (async: `AndAsyncBusinessRule`, `OrAsyncBusinessRule`, `NotAsyncBusinessRule`) instead of a `SimpleBusinessRule` / delegate `CompositeAsyncBusinessRule`. The truth tables, synthesised codes (`A+B`, `A|B`, `!A`) and messages are unchanged; `Name` now reports the node type name (for example `AndBusinessRule`), `Description` is a short operator description, and the runtime type of a composed rule is different
- Sync `And()`/`Or()`/`Not()` now forward the context passed to `IsBroken(context)`/`Check(context)` to their operands; previously they always called the parameterless `IsBroken()`
- `CompositeBusinessRule` and `CompositeAsyncBusinessRule` implement `IComposedBusinessRule` / `IComposedAsyncBusinessRule` with `Operator = RuleOperator.All`. `IsBroken` / `IsBrokenAsync` now evaluate every child (previously they stopped at the first broken child); `Check`/`EvaluateAll` already did
- `BusinessRuleChecker.CheckAll`/`EvaluateAll` evaluate in-library composed and decorated rules through a single internal evaluation instead of `IsBroken()` followed by `Evaluate()`; the reported result for a group or composite now carries its broken members in `Details`
- `SyncToAsyncRuleAdapter` and `AsyncToSyncRuleAdapter` evaluate the wrapped rule through its `Evaluate`/`EvaluateAsync` path so member details flow across the adapter; the blocking behaviour of `AsyncToSyncRuleAdapter` is unchanged

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
- README gains the "Structural Composition and Explain Trees" section; the "Rule Composition" section now points to `RuleGroup.All/Any` as the unambiguous forms and documents that sync operators forward the context

### Fixed

- `CachedBusinessRule.Check()`/`Check(context)` and `CachedAsyncBusinessRule.EvaluateAsync`/`CheckAsync` rebuilt a bare result after evaluating the inner rule and so dropped the `Details` of a cached group or composite; they now report the result of the single inner evaluation, so `AsyncBusinessRuleChecker` surfaces member details through a cached rule. A served cache hit still reports a result without details because only the verdict is cached
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