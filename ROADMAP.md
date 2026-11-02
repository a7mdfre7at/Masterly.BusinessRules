# Masterly.BusinessRules Feature Roadmap

A catalogue of planned features. Each entry is written so it can be picked up and implemented on its own:
what it is, why it matters, how it fits the existing engine, which files and packages it touches, and when it is done.

House rules that apply to every entry:

- The sync/async split is total. Every core feature is added to both hierarchies with mirrored tests.
- Companion packages stay `netstandard2.1` until 4.0 (C# 8: no records, no init-only setters, no file-scoped namespaces). The core keeps zero dependencies.
- New checker overloads must not break the 2.x call shapes pinned by `BusinessRuleCheckerLegacyCallShapeTests`.
- Every in-library composed or decorated node implements the internal `IExplainableBusinessRule` seam and records a node in the explain trace.
- Every public surface change updates the feature page under `website/docs/`, `overview.md`, the package table in `api-reference.md`, and `CHANGELOG.md`.

Sizing: S = days, M = one to two weeks, L = three to four weeks, XL = more than a month (solo maintainer).

---

## 1. Hardening and release safety

### 1.1 Package validation against the last shipped version (S)

**Purpose.** Guarantee that no patch or minor release ships an accidental binary break. 3.0.0 already had to document two of them.

**Design.** Enable the SDK's package validation in `Directory.Build.props` for every packable project:
`<EnablePackageValidation>true</EnablePackageValidation>` and `<PackageValidationBaselineVersion>3.0.0</PackageValidationBaselineVersion>`.
The baseline is bumped in the same commit that bumps `<Version>`. Suppressions live in a `CompatibilitySuppressions.xml` per project and each one needs a comment naming the changelog entry that justifies it.

**Touchpoints.** `Directory.Build.props`, `build.yml` (the pack step must run on pull requests so validation runs before merge, not only in `publish.yml`).

**Done when.** `dotnet pack` fails on a removed or re-typed public member and the pipeline runs pack on every PR.

### 1.2 Public API tracking files (S)

**Purpose.** Make every public surface change a reviewable text diff.

**Design.** Add `Microsoft.CodeAnalysis.PublicApiAnalyzers` to every `src/` project through `Directory.Build.props` (private asset, not a package dependency). Generate `PublicAPI.Shipped.txt` for 3.0.0 and an empty `PublicAPI.Unshipped.txt` per project. The release act moves Unshipped into Shipped.

**Done when.** A new public member without an Unshipped entry is a build error, and the release checklist in `CLAUDE.md` mentions the move.

### 1.3 Samples solution (M)

**Purpose.** Compiled, always-current examples. The docs site today has prose samples only, and nothing in CI proves they build.

**Design.** `samples/` with its own `Masterly.BusinessRules.Samples.sln`, project-referencing `src/` (not the NuGet packages):

- `Samples.MinimalApi`: ASP.NET Core minimal API, `AddBusinessRules`, `IRuleRunner<Order>`, an exception handler mapping `BusinessRuleValidationException` to Problem Details (the prototype for the AspNetCore package in 3.1), tenant scope from a header, feature flags from `Microsoft.FeatureManagement`.
- `Samples.Console`: a guided walk through building, composing, explaining, caching and gating rules, printing `RuleExplanation.ToText()` at each step.
- `Samples.EfCore`: specifications translated with `WhereBroken` against SQLite, plus `EvaluateEach` over a page of rows.
- `Samples.Diagnostics`: OpenTelemetry console exporter showing the batch and rule spans and the three meters.

`build.yml` builds the samples solution in a separate job. The docs pages link to the sample files by URL instead of carrying long listings.

**Done when.** All four samples build in CI and every long code block in `website/docs/` points at a sample file.

### 1.4 Benchmarks (M)

**Purpose.** Measure before 4.0 changes the platform, and publish numbers so users can size the engine.

**Design.** `benchmarks/Masterly.BusinessRules.Benchmarks` on BenchmarkDotNet, `net10.0`, with `[MemoryDiagnoser]`:

- single leaf `IsBroken` versus `Check` versus `Explain`
- `CheckAll` over 10, 100 and 1,000 rules, sync and async, sequential and parallel
- `And`/`Or` trees of depth 1, 4 and 8
- cached hit versus miss, single-key and per-key
- `RuleExpression` interpreter versus `Compile<T>()` versus a hand-written lambda
- `RuleRegistry.Resolve` with 1, 5 and 20 layers

Results are committed as markdown under `benchmarks/results/` per version and surfaced on a new `performance.md` docs page.

**Done when.** The suite runs with `dotnet run -c Release` and the docs page shows 3.0.x numbers.

### 1.5 Coverage gate and dependency updates (S)

**Design.** `codecov.yml` with a project target of 80 percent and a patch target of 90 percent, both failing the status check. `.github/dependabot.yml` grouping the Microsoft.Extensions packages, FeatureManagement, CsCheck, xunit and the GitHub Actions. Dependabot PRs must respect the `nuget.config` pin to nuget.org.

### 1.6 Known soft spots from the 3.0.0 changelog (M)

Three documented behaviours that should become proper features:

- **Async cache hits report metadata only.** `CachedAsyncBusinessRule` caches the boolean, so a served hit returns a result without the inner rule's `Describe` details or member `Details`. Add an opt-in `.Cached(duration, cacheResult: true)` that stores the `BusinessRuleResult` next to the verdict. Both sides, same option name; `RuleResultDetails` is already immutable so sharing a cached instance is safe.
- **Gated sync rules evaluate un-gated without a context.** `FeatureGatedBusinessRule.IsBroken()` has no context to read the rollout from. Give `GatedBy` an optional `RuleRollout defaultRollout` (default `Enforced`) applied when there is no context, and document it on `feature-flags.md`.
- **`AsyncToSyncRuleAdapter` cannot be cancelled.** Add a constructor overload taking a `Func<CancellationToken>` so a host can thread its shutdown token through. Entry 8.3 decides whether the adapter survives at all.

---

## 2. Core engine evolution (3.x, non-breaking)

### 2.1 Clock abstraction for cached rules (S)

**Purpose.** `CachedBusinessRule` reads `DateTime.UtcNow`, so TTL tests sleep and production cannot be made deterministic.

**Design.** Public `IRuleClock { DateTime UtcNow { get; } }` in core with an internal `SystemRuleClock` default, plumbed through `Cached(...)` overloads that take an `IRuleClock`. The DependencyInjection package registers an adapter over `TimeProvider` when one is in the container (`TimeProvider` reaches `netstandard2.1` through `Microsoft.Bcl.TimeProvider`, which the DI package may reference; the core may not). `RuleTestHelper.CreateClock(start)` returns a manual clock with `Advance(TimeSpan)`.

**Done when.** Every cache test runs without `Task.Delay` or `Thread.Sleep` and the DI package honours a registered `TimeProvider`.

### 2.2 Explain tree renderers and serialization (M)

**Purpose.** `RuleExplanation.ToText()` exists; support tools and UIs need structured output.

**Design.**
- Core (no dependencies): `RuleExplanationRenderer` with `ToText()` (existing), `ToMermaid()` (flowchart, nodes coloured by outcome) and `ToMarkdown()` (nested list). A `RuleExplanationVisitor` base class makes custom renderers a few lines.
- New package `Masterly.BusinessRules.Json` (`netstandard2.1`, depends on core and `System.Text.Json`): `RuleExplanationJsonConverter`, `BusinessRuleResultJsonConverter`, `RuleEvaluationResultJsonConverter`, and a `RuleJsonSerializerOptions.Default`. The shape is versioned (`"schema": "masterly.businessrules/explain/1"`) because the provenance store in 6.3 persists it. Rules themselves are not serialized, only their code, name, operator and outcome. Durations serialize as milliseconds with fractional precision, faults as `{ type, message }` with no stack trace by default.

**Done when.** `Explain().ToMermaid()` renders in the docs site, and a round-trip test proves the JSON schema is stable across the sync and async trees.

### 2.3 Message localization hook (M)

**Purpose.** `Message` is a plain string today. Multi-language products need per-culture messages without subclassing every rule.

**Design.** `IRuleMessageResolver { string? Resolve(IBusinessRuleMetadata rule, BusinessRuleContext? context, IFormatProvider provider); }` in core. `BusinessRuleContext.MessageResolver` (beside the existing `FormatProvider`). The walkers and builders call the resolver when creating a `BusinessRuleResult` and fall back to `rule.Message`. The DI package registers an `IStringLocalizer`-backed resolver that looks up the rule `Code` as the resource key. Builders gain `WithMessageKey(string)` for an explicit key.

**Constraints.** Resolution happens only on the broken path so passing rules pay nothing. The resolver is never invoked inside a cache lookup.

### 2.4 Bounded parallel `EvaluateEachAsync` (S)

**Purpose.** `EvaluateEachAsync` runs subjects sequentially; large batches need parallelism without unbounded task fan-out.

**Design.** `AsyncRuleEvaluationOptions.MaxDegreeOfParallelism` (default 1, so behaviour is unchanged). `EvaluateEachAsync` partitions subjects with a `SemaphoreSlim`, preserves positional order in `AsyncSubjectEvaluationCollection<T>`, and isolates faults per subject under the existing `FaultPolicy`. Each subject gets its own `ShadowViolationSink`, because the sink is attached to the shared context per batch and a shared one would interleave.

### 2.5 Streaming evaluation (M)

**Purpose.** Evaluate subjects as they arrive (Channels, Kafka consumers, paged readers).

**Design.** `AsyncBusinessRuleChecker.EvaluateStreamAsync<T>(IAsyncEnumerable<T> subjects, IEnumerable<IAsyncBusinessRule<T>> rules, [context], [options], ct)` returning `IAsyncEnumerable<AsyncSubjectEvaluation<T>>`. Honours `MaxDegreeOfParallelism` from 2.4 with back-pressure (never more than N subjects in flight; output in completion order with the original `Index` preserved). `IAsyncEnumerable<T>` ships in `netstandard2.1`, so no new dependency. The observer sees one `OnBatchStarted` with an unknown count (`-1`, documented) and one `OnBatchCompleted` when the stream ends.

### 2.6 Weighted groups and scoring (M)

**Purpose.** Risk and fraud engines need "broken when the weighted sum of failing members exceeds a threshold", not just All/Any/AtLeast/Exactly.

**Design.** `RuleGroup.Score(code, message, threshold, params (IBusinessRule rule, double weight)[] members)` and the async twin, producing `ScoredGroupBusinessRule` with `RuleGroupQuantifier.Score`. The result carries the score in `RuleResultDetails` (`Actual` = score, `Expected` = `< threshold`) and member results in `Details`. Explain node reason: `score 0.75 >= 0.60`. Short-circuit is allowed when the remaining weight cannot reach the threshold.

### 2.7 Temporal operators (L)

**Purpose.** Express "more than three failed logins in ten minutes" declaratively.

**Design.** Core gains `IRuleEventHistory` (`IReadOnlyList<RuleEvent> Within(string eventName, TimeSpan window, DateTime asOf)`) supplied through the context under `RuleEvents.ContextKey`, with an `InMemoryRuleEventHistory` for tests. Extension rules: `Rule.CountOf(eventName).Within(window).AtLeast(n)`, `Rule.Since(eventName).LongerThan(span)`, `Rule.RateOf(eventName).Exceeds(n, per)`. They are ordinary leaf rules built on `BusinessRuleBuilder`, so they compose and explain like any other. The clock from 2.1 provides `asOf`.

### 2.8 Executable remediations (M)

**Purpose.** `WithRemediation` returns text. UIs want an action they can wire to a button.

**Design.** `IRuleRemediation { string Title { get; } string Description { get; } }` with `ICommandRemediation : IRuleRemediation { string CommandName; IReadOnlyDictionary<string, object?> Arguments; }`. `RuleResultDetails.Remediations` (list; the existing string `Remediation` stays and becomes the first text entry). Builders: `WithRemediation(ctx => new CommandRemediation("request-tax-id", ("customerId", id)))`. The core never executes anything; the Json package serializes remediations and the AspNetCore package emits them in the Problem Details `extensions`.

### 2.9 Tenant refinement layers (M)

**Purpose.** A registry layer can disable or replace a rule; it should also be able to tighten one without copying it.

**Design.** On `RuleSetBuilder`: `Refine(code, r => r.And(extra))`, `Refine(code, r => r.WithSeverity(Warning))`, `OverrideParameter(code, name, value)`. Refinements are stored as decorators applied at resolve time on top of the less specific layer's rule, so the explain tree shows the base rule inside a `REFINED (tenant=acme)` node. `Replace` keeps its current semantics.

### 2.10 Rule effective dates (M)

**Purpose.** Answer "which rules applied on March 3rd" exactly.

**Design.** `BusinessRuleMetadataBuilder.EffectiveFrom(DateTime)` / `EffectiveTo(DateTime)`, stored as metadata (`IBusinessRuleMetadata` gains defaulted `EffectiveFrom`/`EffectiveTo` as DIMs returning null; decorators forward them). The registry filters by `RuleParameters.AsOfKey` from the context, defaulting to the clock. Out-of-window rules are recorded as `NotEvaluated` with reason `not effective` so audits see them.

---

## 3. Host integrations

### 3.1 `Masterly.BusinessRules.AspNetCore` (M)

**Purpose.** One package to drop into a web host.

**Design.** `netstandard2.1` cannot host `IExceptionHandler`; this package targets `net8.0` and `net10.0`. Contents:

- `BusinessRuleExceptionHandler : IExceptionHandler` mapping `BusinessRuleValidationException` to RFC 7807 with status 422 (configurable; 409 for a conflict category), `errors` grouped by code from `Flatten()`, plus `severity`, `category`, `tags`, `details` (actual/expected/remediation) and remediations from 2.8. Never includes faults or stack traces; `BusinessRuleContextException` maps to 500 with a generic detail.
- `services.AddBusinessRulesProblemDetails(options => ...)` with `StatusFor(severity)`, `IncludeRemediations`, `IncludePassedRules` (off).
- Request-scoped `BusinessRuleContext` factory: `services.AddBusinessRulesContext((http, ctx) => ctx.Set(RuleParameters.ScopeKey, RuleScope.Tenant(http.User.FindFirst("tenant")?.Value)))`.
- Health check `AddBusinessRulesHealthCheck()` that resolves the registry, probes the flag and parameter providers with a known key, and reports `Degraded` on provider faults.
- Endpoint `MapBusinessRulesCatalog("/rules")` serving the catalog from 4.3 as JSON.

### 3.2 `Masterly.BusinessRules.Configuration` (M)

**Purpose.** Parameters and rollouts from `appsettings.json`, environment variables, Azure App Configuration, anything behind `IConfiguration`.

**Design.** `ConfigurationRuleParameterProvider` reads `BusinessRules:Parameters:{name}` with scoped children (`BusinessRules:Parameters:maxOrderAmount:tenant=acme`), parses the value with the invariant culture into the requested `T` through `TypeConverter`, and reloads on `IChangeToken`. `ConfigurationRuleFeatureFlagProvider` reads `BusinessRules:Rollouts:{flag}` as `{ mode, percent }`. Both implement the sync and async provider interfaces. Registration: `services.AddBusinessRulesConfiguration(configuration.GetSection("BusinessRules"))`. Misconfigured values throw `RuleConfigurationException` naming the key.

### 3.3 OpenTelemetry conventions and dashboards (S)

**Design.** Align `BusinessRuleSpanAttributes` with OTel semantic naming (`business_rules.rule.code`, `business_rules.batch.size`, `error.type`). Publish under `deploy/observability/` a Grafana dashboard JSON (evaluation rate, broken ratio by code, p95 duration, shadow violations) and an OTel Collector config that routes the `Masterly.BusinessRules` meter and activity source. The Diagnostics docs page walks through it with the `Samples.Diagnostics` project.

### 3.4 Rule drift detector (M)

**Purpose.** Spot a rule whose broken ratio jumps after a deploy or a data change.

**Design.** In Diagnostics: `RuleDriftMonitor` implementing both observer interfaces, keeping a per-code exponentially weighted broken ratio with a baseline window and a detection window (`RuleDriftOptions`: `BaselineWindow`, `DetectionWindow`, `MinimumSamples`, `RatioDelta`). When the detection ratio moves by more than `RatioDelta` it raises `OnDriftDetected(RuleDriftEvent)` (code, baseline ratio, current ratio, sample counts, window) through a callback and an `ILogger` warning, and increments `business_rules.drift.events`. State is in-memory per process; an `IRuleDriftStateStore` seam allows a distributed store later.

### 3.5 Rules HTTP reference service (L)

**Purpose.** Let non-.NET services evaluate rules hosted by a .NET process.

**Design.** `samples/Samples.RulesApi` (not a package): a minimal API hosting a `RuleRegistry` loaded from rule packs (6.1). Endpoints: `POST /evaluate/{set}` (subject JSON bound to a registered CLR type, scope from a header, returns the ledger and explain tree from 2.2), `GET /rules` (catalog), `GET /rules/{code}/explain` (dry run with query parameters), `POST /packs/reload`. Authentication is left to the host. This doubles as the integration test bed for the AspNetCore, Json, Configuration and Packs packages.

---

## 4. Expressions and authoring

### 4.1 Expression functions registry (L)

**Purpose.** The grammar has operators and string predicates but no functions.

**Design.** `RuleExpressionFunctions` registry on `RuleExpressionOptions` (`WithFunction(name, Delegate)`), with a built-in set: `len(x)`, `lower(x)`, `upper(x)`, `trim(x)`, `abs(x)`, `round(x, n)`, `min(a, b)`, `max(a, b)`, `now()`, `today()`, `addDays(d, n)`, `daysBetween(a, b)`, `matches(x, pattern)` (compiled `Regex` with a 1-second match timeout, cached per pattern), `coalesce(a, b)`, `any(list, item => ...)` and `all(...)` with a one-parameter lambda. Grammar: `identifier '(' args ')'`. Functions are pure; `now()` and `today()` read the clock from 2.1 through the context. Both the interpreter and `Compile<T>()` support every built-in (the compiled path maps to `Expression.Call`), and a user function registered without an expression-tree form is rejected by `Compile<T>()` with a clear `RuleExpressionParseException`. Explain steps render calls as `len(name)(5) > 3 => true`.

### 4.2 SQL push-down for expressions (XL, 4.x)

**Purpose.** Run an expression as a `WHERE` clause over millions of rows.

**Design.** `RuleExpression.ToPredicate<T>()` returning `Expression<Func<T, bool>>` built from the same AST as `Compile<T>()` but without the context wrapper, so it composes with `IQueryable<T>.Where`. Supported subset: member paths, comparisons, arithmetic, `and`/`or`/`not`, `in [...]`, string predicates (mapped to `string.StartsWith` and friends, which EF Core translates), and the function subset EF Core can translate. Anything else throws `RuleExpressionTranslationException` naming the node. `ExpressionBusinessRule<T>` then implements `ISpecificationRule<T>` when its expression is translatable, so `WhereBroken` works for text rules too. Parameters bind as constants captured at translation time.

### 4.3 Rule catalog export (S)

**Purpose.** Compliance documentation from the registered rules.

**Design.** `RuleCatalog.From(IRuleRegistry)` and `RuleCatalog.From(IEnumerable<IBusinessRuleMetadata>)` returning `RuleCatalogEntry` rows (code, name, description, severity, category, tags, scope layer, effective dates, expression text when the rule is an `ExpressionBusinessRule`, parameter names, rollout flag when gated). Renderers: markdown table, CSV, JSON (via the Json package). The AspNetCore package serves it (3.1), and the CLI from 7.3 wraps it for CI.

### 4.4 Decision tables (L)

**Purpose.** Business users think in tables; DMN exports exist in most enterprises.

**Design.** New package `Masterly.BusinessRules.DecisionTables`. `DecisionTable<T>.Create(code, message).Input("amount", o => o.Amount).Input("country", o => o.Country).Row(r => r.When("amount", "> 10000").When("country", "in ['US','CA']").Then(outcome)).HitPolicy(HitPolicy.First)`. Rows compile to typed `And` nodes of `ExpressionBusinessRule`s; hit policies `First`, `Any`, `Collect`, `Priority` map onto `RuleGroup.Any`, scored groups and ordered evaluation. The table is itself an `IBusinessRule<T>` and an `IComposedBusinessRule`, so it explains row by row. `DmnImporter.Load(stream)` reads DMN 1.3 decision tables with FEEL comparison literals (the FEEL subset the expression grammar already covers; everything else is rejected with the cell coordinates).

### 4.5 Natural-language rule authoring tool (M)

**Purpose.** Turn a sentence into a validated rule plus its tests.

**Design.** `tools/Masterly.BusinessRules.Author`, a console tool using the Claude API (`claude-fable-5-1`) with the expression grammar, the subject type's public members (from reflection on a given assembly) and the parameter names as structured context. Output is a JSON rule pack entry (6.1) plus a generated xUnit test file with a boundary test from 5.3. The tool never trusts the model output blindly: it runs `RuleExpression.TryParse`, checks `Identifiers` against the subject's members, compiles with `Compile<T>()`, and runs the generated tests before printing. Lives outside `src/`, never packed.

---

## 5. Testing and quality tooling

### 5.1 Snapshot testing for rule sets (S)

**Design.** In Testing: `RuleSetSnapshot.Verify(IRuleRegistry registry, RuleScope scope, string snapshotPath)` writes the resolved codes, severities, parameter values and rollout modes as sorted text and compares with the committed snapshot, failing with a unified diff. Scopes are enumerated from a list the test passes. Uses Verify-style `*.verified.txt` naming so existing tooling picks it up.

### 5.2 Mutation testing for rules (L)

**Design.** Extend the smoke mutants with real operators: `RuleMutator.For(rule)` produces mutants by rewriting `ExpressionBusinessRule` ASTs (`>` to `>=`, `<` to `<=`, `==` to `!=`, boundary constants plus or minus one unit, `and` to `or`, dropping a conjunct) and by wrapping opaque rules with the existing `Invert` and `AlwaysPass`. `MutationRun.Execute(mutants, Action<IBusinessRule> testBody)` runs the caller's assertions against each mutant and reports `MutationScore` (killed, survived, with the survived mutant text). An xUnit `[RuleMutationData]` attribute feeds mutants into a theory. Specifications are mutated through an `ExpressionVisitor` on `BrokenExpression`.

### 5.3 Property synthesis from the boundary finder (M)

**Design.** `RuleProperties.Infer<T>(factory, from, to, step)` runs `RuleBoundaryFinder.Find`, infers `Monotonicity`, and returns a `RuleProperties<T>` pre-loaded with `MonotoneIn(monotonicity)`, a boundary law (`passes at LastPassing, breaks at FirstBroken`) and `ContextPure()`. `RuleTestGenerator.WriteXunitClass(rule, boundary, path)` emits a C# test class so the inferred laws become committed tests, not just a runtime check.

### 5.4 Roslyn analyzers (L)

**Design.** New package `Masterly.BusinessRules.Analyzers` (`netstandard2.0`, Roslyn 4.x, shipped as a development dependency with code fixes):

| Id | Rule | Severity |
|----|------|----------|
| MBR001 | `.Cached()` applied outside `.GatedBy()` (cache must sit inside the gate) | Warning |
| MBR002 | `new AsyncToSyncRuleAdapter(...)` inside an `async` method | Warning |
| MBR003 | Two `When(...)` calls on one `BusinessRuleBuilder` chain | Warning |
| MBR004 | A method call inside `BrokenWhen` that EF Core cannot translate | Info |
| MBR005 | A class deriving from `BaseBusinessRule*` with no test referencing its type (opt-in; test project named in `.editorconfig`) | Info |
| MBR006 | A rule type whose `Code` is interpolated or computed rather than constant | Warning |
| MBR007 | `RuleGroup` built with both `WithParallelEvaluation()` and `WithShortCircuit()` (throws at run time) | Error |

Each analyzer has tests on `Microsoft.CodeAnalysis.Testing`.

### 5.5 Dead rule finder (S)

**Design.** Production-safe coverage: `RuleCoverage.Begin(RuleCoverageOptions.Sampling(rate))` records one in N evaluations with no allocation on the skipped path. `RuleCoverageReport.NeverBroken(TimeSpan window)` lists codes never broken in the window with their evaluation counts. The Diagnostics logging observer can export the report periodically. A docs page covers retiring rules safely (shadow the removal with `AlwaysPass().GatedBy(...)` first).

---

## 6. Rules as data and governance

### 6.1 Rule packs (L)

**Purpose.** Declarative rule sets loaded at run time with an audit trail.

**Design.** New package `Masterly.BusinessRules.Packs` (depends on core, Expressions, Json). Format (JSON, with a published JSON Schema; YAML through an optional reader):

```json
{
  "schema": "masterly.businessrules/pack/1",
  "name": "orders",
  "scope": { "tenant": "acme" },
  "rules": [
    {
      "code": "ORDER.LIMIT",
      "message": "Order exceeds the limit",
      "severity": "Error",
      "category": "Orders",
      "tags": ["finance"],
      "subject": "Shop.Order, Shop",
      "when": "Data.Amount > @maxOrderAmount",
      "parameters": { "maxOrderAmount": 10000 },
      "rollout": { "mode": "Shadow", "percent": 10 },
      "effectiveFrom": "2027-01-01"
    }
  ]
}
```

`RulePackLoader.Load(stream)` validates against the schema, resolves subject types through an `IRulePackTypeResolver` (allow-list; never `Type.GetType` on arbitrary input), builds `ExpressionBusinessRule`s through `WhenExpression`, applies `GatedBy` and effective dates, and returns a `RuleSetBuilder` layer for the registry. `IRulePackSource` (file system with watcher, `IConfiguration`, HTTP) with `RulePackReloader` swapping the registry atomically and raising `OnPackReloaded(name, version, changedCodes)`. Every load records who, when and a content hash to an `IRulePackAuditSink`.

### 6.2 Rule pack signing (M)

**Design.** Detached signature over the canonical JSON (RFC 8785 canonicalization) with ECDSA P-256. `RulePackLoader` takes an `IRulePackVerifier` and refuses unsigned or tampered packs unless `AllowUnsigned` is set explicitly (logged as a warning). Key material comes from the host (`X509Certificate2` or a raw public key); signing is a function in the same package plus a `sign` command in the CLI from 7.3.

### 6.3 Rule time travel (L)

**Purpose.** Replay past batches against the current rule set and diff verdicts.

**Design.** New package `Masterly.BusinessRules.Provenance`. `IDecisionStore` with `AppendAsync(DecisionRecord)` and `QueryAsync(DecisionQuery)`. `DecisionRecord` = batch id, timestamp, scope, subject type and a subject snapshot (JSON via a host-supplied serializer, with an `IDecisionRedactor` hook for PII), the ledger entries, the explain tree (2.2 JSON), rule pack name and version (6.1), resolved parameter values, rollout modes seen. A `RecordingRuleExecutionObserver` writes records. `DecisionReplay.ReplayAsync(storeQuery, currentRegistry)` re-evaluates each recorded subject and yields `DecisionDiff` (same, newly broken, newly passing, changed severity) with both explain trees. Providers: in-memory and SQLite (sample); EF Core left to the host.

### 6.4 Rule provenance graph (XL)

**Purpose.** Make the engine the system of record for decisions.

**Design.** Builds on 6.3. Each `DecisionRecord` links to the rule version (pack version or assembly informational version), parameter source and value, flag state, scope and subject hash. `ProvenanceQueries`: `WhyAsync(subjectId, asOf)` (the explain tree for the decision closest to `asOf`), `AffectedByParameterChangeAsync(name, scope, since)` (decisions whose resolved value differs from the current one), `RuleAppliedToAsync(code, scopeFilter, window)` (proof of application or non-application). A `ProvenanceReport` renderer produces a markdown dossier per subject. The reference service (3.5) exposes these as endpoints.

### 6.5 Shadow-to-enforced autopilot (L)

**Purpose.** Promote a shadow rule automatically when the evidence supports it.

**Design.** Core gains `IRuleRolloutWriter { void SetRollout(string flag, RuleRollout rollout); }` (and async), implemented by `InMemoryRuleFeatureFlagProvider` and, in FeatureManagement, by a provider that writes variants back when the underlying store allows it. In Diagnostics, `RolloutAutopilot` observes shadow violations, batch counts and drift events (3.4) per flag against a `RolloutPolicy`: `PromoteAfter(batches, maxViolationRatio)`, `StepPercent(10)`, `DemoteOn(violationRatio | driftEvent)`, `MinimumDwell(TimeSpan)`. Every transition is logged, metered and published through `OnRolloutChanged`. The policy is data; humans approve the policy once. A `DryRun` mode reports what it would have done.

### 6.6 Counterfactual explain (L)

**Purpose.** Tell the caller what would have to change for a broken rule to pass.

**Design.** `rule.ExplainCounterfactual(context)` returning `RuleCounterfactual` entries (`Path`, `CurrentValue`, `SuggestedValue`, `Confidence`). Three strategies, tried in order: for `ExpressionBusinessRule`, symbolic inversion of comparison nodes on a single varying member (`amount > @limit` becomes `amount <= 10000`); for specifications, the same over `BrokenExpression`; for opaque leaf rules with a numeric member nominated by `WithCounterfactualProbe(o => o.Amount)`, a boundary search (an internal copy of the boundary finder, since core cannot depend on Testing). Composed rules combine children: `And` needs both, `Or` needs any, `Not` inverts. Results feed `RuleResultDetails.Expected` when the rule did not set it.

---

## 7. Developer experience

### 7.1 Rule playground in the docs site (M)

**Design.** A React page under `website/src/pages/playground.js`. The expression grammar is small enough to port the parser and interpreter to TypeScript (about 1,000 lines) and keep them pinned to the C# grammar by sharing the parameterised test vectors as JSON under `tests/shared/expression-vectors.json`, read by both test suites. The page shows the AST, the bound steps and the verdict live as the user edits the expression and a JSON context.

### 7.2 VS Code test adapter rendering (M, separate repository)

**Design.** A VS Code extension that recognises `RuleAssertionException` and `RuleExplanation.ToText()` output in test results and renders a collapsible tree with outcome icons in the Test Results view, plus a CodeLens "Explain" on rule classes that runs the rule against the fixture context from the nearest test. Tracked here, built in a separate `masterly-businessrules-vscode` repository.

### 7.3 CLI global tool (M)

**Design.** `dotnet tool install masterly-rules`: `catalog` (4.3), `sign` and `verify` (6.2), `pack validate`, `pack diff a.json b.json`, `explain --pack orders.json --code ORDER.LIMIT --context ctx.json`, `benchmark` (runs 1.4 against a pack). Built on `System.CommandLine`, lives under `tools/`, packed as a `DotnetTool` at the shared version.

---

## 8. Platform step (4.0, breaking)

### 8.1 Target `net8.0` and `net10.0` only (L)

**Purpose.** Retire `netstandard2.1` and the C# 8 constraints across `src/`.

**Design.** Change every `src/*.csproj` to multi-target, raise `LangVersion` to the default, and run a one-time sweep: file-scoped namespaces, records for immutable value types (`RuleScope`, `RuleRollout`, `RuleResultDetails`), `init` setters on options classes, `required` members where a null-forgiving comment exists today, `INumber<T>` in core so the boundary finder can move out of Testing. The code rules in `CLAUDE.md` are rewritten in the same PR. Package validation (1.1) switches to a new baseline with an explicit, documented list of breaks.

### 8.2 Trimming and Native AOT (L)

**Design.** `IsTrimmable` and `IsAotCompatible` on every package, with trim analyzer warnings fixed rather than suppressed. The hot spots: `RuleExpressionInterpreter` member binding by reflection, `AddBusinessRules` assembly scanning, `TypeConverter` use in the Configuration package. Fixes: a source generator `Masterly.BusinessRules.Generators` emitting a `[RuleAccessible]`-driven member accessor table per subject type (the interpreter uses it when present and falls back to reflection under a `RequiresUnreferencedCode` annotation), a generated registration method `AddBusinessRulesFromGenerated()` as an alternative to scanning, and `IParsable<T>` in place of `TypeConverter`. A `samples/Samples.Aot` project publishes with `PublishAot=true` in CI.

### 8.3 `AsyncToSyncRuleAdapter` decision (S)

**Design.** Either remove the adapter (recommended: every known use is covered by the DI runner's async batch and `ToAsync()`), or keep it with the cancellable constructor from 1.6 and mark it `[Obsolete]` for one minor. Decide at 4.0 planning and record the migration in the changelog.

### 8.4 Interface and overload clean-up (M)

**Design.** With the 2.x call-shape constraint retired: make the options classes `sealed`, collapse the generic `TOptions` overloads to plain parameters, replace the observer DIMs with an abstract `RuleExecutionObserver` base class beside the interface, and reconsider `IBusinessRule.Evaluate(context)` as a required member. Each change is listed in the 4.0 Breaking Changes section with its 3.x equivalent.

---

## Suggested order

Not a schedule, only the dependency order that keeps each step small:

1. 1.1, 1.2, 1.5 (a few days; protects everything after)
2. 2.1 clock, then 1.6 soft spots, 1.3 samples, 1.4 benchmarks
3. 2.2 renderers and Json, 2.4 and 2.5 parallel and streaming, 2.3 localization
4. 3.1 AspNetCore, 3.2 Configuration, 4.3 catalog, 3.3 OTel
5. 4.1 functions, 2.6 scoring, 2.7 temporal, 2.8 remediations, 2.9 refinement, 2.10 effective dates
6. 5.1 snapshots, 5.3 property synthesis, 5.4 analyzers, 5.2 mutation, 5.5 dead rules
7. 6.1 packs, 6.2 signing, 4.4 decision tables, 7.3 CLI, 7.1 playground
8. 3.4 drift, 6.3 time travel, 6.4 provenance, 6.5 autopilot, 6.6 counterfactual, 3.5 reference service, 4.5 authoring tool
9. 8.x platform step, then 4.2 SQL push-down and 8.2 AOT on the new platform
