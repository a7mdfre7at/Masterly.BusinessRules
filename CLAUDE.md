# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

`Masterly.BusinessRules` is a composable business-rule engine for .NET, shipped as six NuGet packages from one solution (`Masterly.BusinessRules.sln`) at one shared version (currently 3.0.0). The core is zero-dependency; five sibling packages are opt-in.

| Package (`src/`) | Target | Depends on | Purpose |
|------------------|--------|------------|---------|
| `Masterly.BusinessRules` | `netstandard2.1` | nothing | The core engine |
| `Masterly.BusinessRules.Expressions` | `netstandard2.1` | core | Text conditions (`RuleExpression`), bound-value explanations, `WhenExpression` |
| `Masterly.BusinessRules.Diagnostics` | `netstandard2.1` | core, `Microsoft.Extensions.Logging.Abstractions` 10.0.14, `System.Diagnostics.DiagnosticSource` 10.0.14 | `ILogger` / tracing observers, `Meter` instruments, `.Traced()` |
| `Masterly.BusinessRules.DependencyInjection` | `netstandard2.1` | core, `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.14 | `AddBusinessRules` scanning, `IRuleRunner<T>`, scoped `RuleRegistry` |
| `Masterly.BusinessRules.FeatureManagement` | `netstandard2.1` | core, `Microsoft.FeatureManagement` 4.8.0 | `IRuleFeatureFlagProvider` over `IFeatureManager` / `IVariantFeatureManager` |
| `Masterly.BusinessRules.Testing` | `net8.0`, `net10.0` | core, `CsCheck` 4.9.1 (`net8.0`) / 5.0.0 (`net10.0`) | Outcome coverage, boundary finder, CsCheck properties, smoke mutants |

Testing is not `netstandard2.1` on purpose: CsCheck has no .NET Standard build and the boundary finder constrains `T : INumber<T>` (static abstract interface members). It multi-targets `net8.0` and `net10.0` because CsCheck 5 ships only `net10.0`+ assets; the csproj picks CsCheck 4.9.1 or 5.0.0 per framework. Every test project targets `net10.0` (the Testing test project also `net8.0`, so both CsCheck lines are exercised), uses xunit.v3 4.x and has `ImplicitUsings`; `src/` has none.

Tests run on Microsoft.Testing.Platform, not VSTest: the root `global.json` sets `"test": { "runner": "Microsoft.Testing.Platform" }`, which the .NET 10 SDK requires for xunit.v3 4.x. Consequences: `dotnet test` needs `--solution`/`--project`, VSTest options (`--filter`, `--collect`, `--logger`) do not exist, and coverage comes from `coverlet.MTP` (`--coverlet`), not `coverlet.collector`. CI installs SDK 10.0.x plus the 8.0.x runtime.

## Code rules for `src/`

Default C# language version for `netstandard2.1` (C# 8): no records, init-only setters, file-scoped namespaces or target-typed `new`. Block-scoped namespaces, explicit types (never `var`), explicit access modifiers, XML `<summary>` on every public member, no magic strings (constants live in `*Text` classes such as `RuleExplanationText`, `RuleCompositionText`, `RuleCoverageText`). Each package has one flat namespace regardless of folder (`Masterly.BusinessRules`, `Masterly.BusinessRules.Expressions`, ...); folders are organisational only.

## Commands

```bash
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --solution Masterly.BusinessRules.sln -c Release --no-build      # all test projects
dotnet test --project tests/Masterly.BusinessRules.Expressions.UnitTests       # one package's tests
dotnet test --project tests/Masterly.BusinessRules.UnitTests --filter-class "*.CachedRuleTests"     # one class
dotnet test --project tests/Masterly.BusinessRules.UnitTests --filter-method "*.CachedRuleTests.CachedBusinessRule_CachesResult"   # one test
dotnet test --solution Masterly.BusinessRules.sln --coverlet --coverlet-output-format cobertura     # coverage, what CI runs; files land in TestResults/
dotnet pack Masterly.BusinessRules.sln -c Release -o artifacts                 # all six .nupkg files (artifacts/ is gitignored)
```

There is no linter step. `.editorconfig` only silences Roslynator `RCS1194`. A repo-local `nuget.config` clears the sources and pins `nuget.org`; do not change it. The whole solution, tests included, builds with zero warnings: pass `TestContext.Current.CancellationToken` to every call that accepts a token (xUnit1051). `Directory.Build.props` turns on `GenerateDocumentationFile` for every non-`*.UnitTests` project, so a `cref` that does not resolve is a `src/` warning.

## Release process

A successful `Build` workflow on `master` triggers `publish.yml`, which reads `<Version>` from the root `Directory.Build.props`, packs every packable project under `src/`, pushes the core package first and then the others (`--skip-duplicate`, `.snupkg` symbols alongside), and creates the GitHub Release `v{Version}` with the packages as assets and that version's `CHANGELOG.md` section as the body (a re-run only uploads missing assets). Bumping `<Version>` plus a `CHANGELOG.md` entry is therefore the release act; do not bump it casually. `Directory.Build.props` also supplies authors, repository, the `MIT` licence expression, copyright, release-notes link, icon, symbols/SourceLink settings, `Nullable` and the packed `README.md` (`$(PackageReadmeSource)`, overridden per companion package) / `repo_image.png` for every project; each `src/*.csproj` keeps only `TargetFramework`, `Description` and `PackageTags` (plus its own package references). Test projects set `IsPackable=false`.

## Core architecture

### The sync/async split is total

Every concept exists twice, as parallel hierarchies that do not share a base type: `IBusinessRule`/`BaseBusinessRule` vs `IAsyncBusinessRule`/`BaseAsyncBusinessRule`, `BusinessRuleBuilder` vs `AsyncBusinessRuleBuilder`, `BusinessRuleChecker` vs `AsyncBusinessRuleChecker`, `RuleGroup` vs `AsyncRuleGroup`, every decorator and node (`Cached`, `Conditional`, `SeverityOverride`, `FeatureGated`, `Composite`, `And/Or/Not`, `Group` + `AsyncBusinessRule` twin), the options/result/observer types, the walkers. Bridges: `SyncToAsyncRuleAdapter` (`.ToAsync()`), its typed twin `SyncToAsyncRuleAdapter<T>` (`IBusinessRule<T>.ToAsync()`, no subject binding) and `AsyncToSyncRuleAdapter` (constructed directly, blocks via `GetAwaiter().GetResult()` by design; there is no `ToSyncRule`). **When adding a feature, add it to both sides and mirror the tests.**

The one deliberate exception is **specifications**: an expression tree has no async form, so there is no `IAsyncSpecificationRule`; `ISpecificationRule<T>.ToAsync()` returns `AsyncSpecificationRuleAdapter<T>` and that adapter is the whole async side.

### Rule contract and metadata

A rule is broken when its condition is true. Styles: `IsBroken`, `Evaluate` (sync: a pure result factory that never evaluates; async: returns null when passing) and `Check` (throws `BusinessRuleValidationException` with `BrokenRules`). `IBusinessRuleMetadata` is the base of all four rule interfaces: abstract `Code`/`Message`, DIM defaults for `Severity`, `Name`, `Description`, `Category`, `Tags`. Decorators and adapters forward all seven members; keep that invariant. Sync rules have `IsBroken()` and `IsBroken(context)` (base: context overload calls the parameterless one); async rules always take a context and a `CancellationToken`.

### Walker interfaces and explain trees

- Public walker interfaces: `IComposedBusinessRule` / `IComposedAsyncBusinessRule` (`Operator`, `Operands`: And/Or/Not nodes, groups, composites) and `IDecoratedBusinessRule` / `IDecoratedAsyncBusinessRule` (`InnerRule`: cached, conditional, severity-override, feature-gated). Adapters expose `InnerRule` typed as the other side.
- Every in-library composed/decorated node implements the internal `IExplainableBusinessRule.EvaluateCore(context, trace)` (async twin). `BusinessRuleWalker.EvaluateNode` / `AsyncBusinessRuleWalker.EvaluateNodeAsync` dispatch: in-library node → `EvaluateCore`; public `IExplainableDecoratedBusinessRule` (async twin) → `EvaluateDecorated(DecoratedRuleEvaluation)`; anything else → leaf (`IsBroken` + `Evaluate`). `IsBroken`/`Check` pass a null trace; `Explain`/`ExplainAsync` pass a `RuleExplanationTrace`, so the `RuleExplanation` tree is a by-product of the one real evaluation and timing is allocated only while explaining.
- `IExplainableDecoratedBusinessRule` is the seam for decorators in sibling packages (Testing's coverage and mutation decorators use it). `DecoratedRuleEvaluation.EvaluateInner()` runs the inner rule through the walker at most once; the walker records a node labelled `ExplanationOperator` with the inner subtree, or the inner rule as `NotEvaluated` ("not evaluated by decorator") if the decorator never calls it. `EvaluateDecorated` is the verdict on every walker path, so such decorators should implement `IsBroken` through it.
- When adding an in-library composed or decorated rule: implement the internal interface on both sides and record a node in the trace.
- Semantics worth knowing: `And` is broken only when both operands are broken, `Or` when either is (pinned by `CompositionTruthTableTests`); both forward the context and short-circuit. `RuleGroup.All/Any/AtLeast/Exactly` are the unambiguous N-ary quantifiers (immutable; `WithShortCircuit`, `WithSeverity`, async `WithParallelEvaluation`; parallel + short-circuit throws). Member failures travel in `BusinessRuleResult.Details` (`Flatten()` yields leaves). `CompositeBusinessRule` has code `"CompositeRule"` and is the `RuleOperator.All` node.

### Checkers, options objects and the ledger

`BusinessRuleChecker` / `AsyncBusinessRuleChecker` are static and `partial` (the subject entry points `For(subject)` and `EvaluateEach[Async]` in `*.Typed.cs`). Every overload runs one internal core (`EvaluateAllCore` / `EvaluateAllAsyncCore`) that records a `RuleEvaluationEntry` per rule (order, outcome, duration via `RuleTiming`/`Stopwatch.GetTimestamp()`, fault); `CheckAll` is the core plus `ThrowIfAtLeast(options.ThrowOn)`. `RuleEvaluationOptions` / `AsyncRuleEvaluationOptions`: `StopOnFirstFailure`, `OnFault` (`FaultPolicy.Throw` default / `Record` / `TreatAsBroken`; cancellation is never caught), `ThrowOn` (honoured by `CheckAll` only), `Observer`, async `RunInParallel` (fail-fast wins: `EffectiveParallel`). Observers get callbacks from `CheckAll` and the options-taking `EvaluateAll`; batch hooks are DIMs. A cancelled rule gets `OnRuleCancelled` (never `OnRuleFaulted`), and every started batch ends with exactly one of `OnBatchCompleted` / `OnBatchAborted` (cancellation, a fault under `Throw`, a null rule); the async checker passes `CancellationToken.None` to those two hooks. The tracing observers rely on this to close their spans. Context read failures are wrapped by the internal `ContextReadFault.Wrap` into `BusinessRuleContextException`. 2.x call shapes must keep binding as in 2.0.0 (pinned by `BusinessRuleCheckerLegacyCallShapeTests`, `#nullable disable`): a positional `default`/`null` after the rules (`CheckAll(rules, default)`, `EvaluateAll(rules, null)`, `CheckAllAsync(ctx, rules, default)`, `CheckBySeverityAsync(ctx, rules, default, Error)`) must reach the bool/context/token overload. So no overload whose first parameter is a bare `TSubject` may sit on the static checkers (it captures the rule array by identity), every options overload is generic `TOptions : RuleEvaluationOptions` / `AsyncRuleEvaluationOptions` (a typeless literal cannot infer it; that is why both options classes are unsealed), and the async filters have no token-free params form (it is ambiguous with a `default` token). Check any new overload against these shapes.

### Subject-typed rules

`IBusinessRule<in T>` / `IAsyncBusinessRule<in T>` (bases `BaseBusinessRule<T>` / `BaseAsyncBusinessRule<T>`) receive the subject at evaluation time and do **not** derive from the non-generic interfaces; the only bridge is `.For(subject)` → `BoundBusinessRule<T>` / `BoundAsyncBusinessRule<T>` (a leaf to the walkers). Typed batches go through `BusinessRuleChecker.For(subject)` / `AsyncBusinessRuleChecker.For(subject)`, which return `SubjectRuleChecker<T>` / `AsyncSubjectRuleChecker<T>` (the untyped batch methods with typed rule sets; the async filters take the token before the severities/tags) and bind through `SubjectBinding.Bind` onto the same internal cores; `EvaluateEach` / `EvaluateEachAsync` stay static and return the positional `SubjectEvaluationCollection<T>`. Typed `And/Or/Not` nodes are plain typed rules, not `IComposedBusinessRule`. All four builders derive from `BusinessRuleMetadataBuilder<TBuilder>` (`private protected` ctor; internal `BuildMetadata`, `BuildParameterBinding`, `BuildDescriptor`, `BuildDefinition`).

### Builders and their seams

`When(...)` / `WhenAsync(...)` set the condition (the last call wins on the untyped builders; `BusinessRuleBuilder<T>` deliberately keeps its subject-only and context conditions side by side, pinned by `TypedBusinessRuleBuilderTests`, while a parameters condition replaces and is replaced by both); `WithDetail/WithActual/WithExpected/WithRemediation` attach broken-path details (`BuiltRuleDescriptor`); `WithParameters` / `WithParameter<T>(name, fallback)` declare scoped parameters (`RuleParameterBinding`, resolved per evaluation). Two seams for sibling packages: `When(IRuleConditionCompiler, text)` / `WhenAsync(IAsyncRuleConditionCompiler, text)` (core never parses text), and `WithRuleFactory(IBuiltRuleFactory)` / `WithRuleFactory(IAsyncBuiltRuleFactory)`, which makes `Build()` return the factory's rule built from a public `BuiltRuleDefinition` (metadata, `ResolveParameters[Async]`, `CreateResult` with details). A factory replaces the condition and a later `When` replaces the factory. Expressions' `WhenExpression` uses it so `Build()` returns `ExpressionBusinessRule`. The typed builders have no factory seam.

### Context, typed keys, parameters, caching, gates

- `BusinessRuleContext` is a string-keyed bag (not thread-safe); `BusinessRuleContext<T>` stores `Data` under `"__typedData"`. `ContextKey<T>` overloads delegate through `key.Name`; `Get<T>` throws `ContextKeyNotFoundException` / `ContextValueTypeMismatchException`.
- Scoped parameters: `RuleScope` + `IRuleParameterProvider` / `IAsyncRuleParameterProvider` (`InMemoryRuleParameterProvider`). Resolution: scope from `RuleParameters.ScopeKey`; provider from `AsyncContextKey` (async) → `ContextKey` → injected async → injected sync → fallbacks. A reserved key holding the wrong type throws instead of being ignored. The scoped `RuleRegistry` lives in the DependencyInjection package.
- `CachedBusinessRule` caches the boolean only (one entry per instance, or per key with `.Cached(ttl, ctx => key)`); uses `DateTime.UtcNow`. The lock/semaphore guards only lookups and stores (the inner rule runs outside it; a store is skipped when an invalidation happened meanwhile, via a generation counter), and a verdict whose trace branch `HasCapturedFault` (set by the walkers when `Explain(captureExceptions: true)` captures a fault, propagated to ancestor branches) is never stored. Cache belongs inside a gate, never outside.
- `FeatureGatedBusinessRule` (`.GatedBy(flag, provider, stableKey)`) reads a `RuleRollout` per evaluation via the internal `RuleGate`; shadow results go to the internal `ShadowViolationSink` the checkers attach to the context per batch (`ShadowResults`, `OnShadowViolation`). Shadow mode never blocks: a non-cancellation exception from the inner rule is caught, reported as a shadow violation with a `RuleFaultResult` stand-in plus `OnShadowFault`, and recorded as a `Faulted` child. The sync decorator evaluates un-gated without a context.
- Specifications (`SpecificationBusinessRule<T>.BrokenWhen`) splice operand bodies with `ParameterReplacer` into one lambda; `WhereBroken`/`WhereSatisfies` accept only `ISpecificationRule<T>`; both `IsBroken` overloads are sealed and context-blind.

## Sibling packages in brief

- **Expressions**: parser → immutable AST → one interpreter (`RuleExpressionInterpreter`) used for both verdict and `Explain` (binding recorder); `Compile<T>()` is the only expression-tree path. `ExpressionBusinessRule.IsBroken()` without a context throws `InvalidOperationException`.
- **Diagnostics**: observers only plug into core seams; tracing callbacks are deliberately not `async` so the rule span is `Activity.Current` inside the rule. `RuleActivities` lookups skip foreign and stopped activities. The `Traced()` decorators implement `IExplainableDecorated*` (operator `TRACED`) so walker paths keep `Details` and the explain subtree.
- **DependencyInjection**: scanning is deterministic (ordinal type order, `TryAddEnumerable`); untyped rules are scanned only with `IncludeUntypedRules = true` (default false). Registry layers merge least to most specific, ties in declaration order; the batch methods stamp `scope ?? Global` under `RuleParameters.ScopeKey` for the call and restore the previous entry (`RuleScopeOverride`), async batches run sync rules adapted then async rules, sync batches throw when the set has async rules.
- **FeatureManagement**: variant → rollout mapping (`RolloutVariantConvention`); the sync provider side blocks on the async one.
- **Testing**: decorators implement `IExplainableDecoratedBusinessRule`. CsCheck 4.9.1 (the `net8.0` target) cannot generate `Gen.Decimal[0m, x]` (a zero bound fails "Failing Where max count"; fixed in CsCheck 5); `Verify` reports it as `InvalidOperationException`, the README documents workarounds.

## Tests

One test class per feature under `tests/<Package>.UnitTests/`, mirroring the `src/` folders. Names are `Subject_Scenario_Expectation` (older tests use `Subject_Expectation`), Arrange-Act-Assert, happy path plus error paths, null guards, cancellation (use `TestContext.Current.CancellationToken`) and edge cases, mirrored sync/async. Prefer `BusinessRuleBuilder` with a counting closure and `RuleTestHelper` factories (`CreateBrokenRule`, `CreatePassingRule`, `CreateContext`, `CreateFlagProvider`, `CreateParameterProvider`) over hand-written stubs; a stub class is fine only to exercise an interface seam. Never weaken a failing assertion; fix the code.

## Documentation

`README.md` is packed into the core package (each companion package packs its own `src/<Package>/README.md`) and is the user-facing reference: when public surface changes, update the feature section, the Features list, the API Reference table of the right package, and add a `CHANGELOG.md` entry (grouped Breaking Changes / Added (core, then per package) / Changed / Fixed). Each version section opens with a one-line summary, because `publish.yml` uses the section verbatim as the GitHub Release body.
