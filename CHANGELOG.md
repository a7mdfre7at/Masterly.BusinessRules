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