# Masterly.BusinessRules.Diagnostics

Structured `ILogger` logging, OpenTelemetry tracing spans and `Meter` metrics for business-rule evaluation. Use it when you want rule outcomes in your logs and telemetry without writing an observer.

This is a companion package of [Masterly.BusinessRules](https://www.nuget.org/packages/Masterly.BusinessRules), a composable business-rule engine for .NET.

[![NuGet](https://img.shields.io/nuget/v/Masterly.BusinessRules.Diagnostics?style=flat-square)](https://www.nuget.org/packages/Masterly.BusinessRules.Diagnostics)

## Install

```bash
dotnet add package Masterly.BusinessRules.Diagnostics
```

- **Target framework:** `netstandard2.1` (.NET Core 3.0+, .NET 5+)
- **Dependencies:** `Masterly.BusinessRules`, `Microsoft.Extensions.Logging.Abstractions` 10.0.14, `System.Diagnostics.DiagnosticSource` 10.0.14

## Example

```csharp
using System.Collections.Generic;
using Masterly.BusinessRules;
using Masterly.BusinessRules.Diagnostics;
using Microsoft.Extensions.Logging;

public sealed class CheckoutValidator
{
    private readonly ILogger<CheckoutValidator> _logger;

    public CheckoutValidator(ILogger<CheckoutValidator> logger)
    {
        _logger = logger;
    }

    public RuleEvaluationResult Validate(IEnumerable<IBusinessRule> rules, BusinessRuleContext context)
    {
        // One structured log line per broken or faulted rule: RuleCode, RuleName, Category, Severity, Outcome, ElapsedMs.
        RuleEvaluationOptions options = new RuleEvaluationOptions
        {
            Observer = new LoggingRuleExecutionObserver(_logger)
        };

        return BusinessRuleChecker.EvaluateAll(rules, context, options);
    }
}
```

For async batches use `LoggingAsyncRuleExecutionObserver`. For tracing and metrics, attach `TracingRuleExecutionObserver` (or `TracingAsyncRuleExecutionObserver`) and subscribe to the `ActivitySource` named `BusinessRulesActivitySource.Name` and the `Meter` named `BusinessRulesMetrics.MeterName`; no OpenTelemetry extension package is needed.

## Documentation

Log levels and options, span and metric names, and the `.Traced()` decorator are covered in the full documentation: <https://github.com/a7mdfre7at/Masterly.BusinessRules#readme>
