---
id: installation
title: "Installation"
---

Install the core package first. Then add only the companion packages you need. All six packages ship together with the same version number.

| Package | What it is for | Target framework | Extra dependencies |
|---------|----------------|------------------|--------------------|
| `Masterly.BusinessRules` | The core: rules, builders, composition, batch checking, context, caching, feature gates, parameters and specifications | `netstandard2.1` | None |
| `Masterly.BusinessRules.Expressions` | Rule conditions written as text, with explanations that show the bound values | `netstandard2.1` | None |
| `Masterly.BusinessRules.Diagnostics` | Structured `ILogger` logging, OpenTelemetry spans and metrics for rule evaluation | `netstandard2.1` | `Microsoft.Extensions.Logging.Abstractions` 10.0.14, `System.Diagnostics.DiagnosticSource` 10.0.14 |
| `Masterly.BusinessRules.DependencyInjection` | Assembly scanning, injectable rule runners and per-tenant rule sets | `netstandard2.1` | `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.14 |
| `Masterly.BusinessRules.FeatureManagement` | Drives feature-gated rules from `Microsoft.FeatureManagement` flags and variants | `netstandard2.1` | `Microsoft.FeatureManagement` 4.8.0 |
| `Masterly.BusinessRules.Testing` | Outcome coverage, boundary finder, property checks and mutants for your test projects | `net8.0`, `net10.0` | `CsCheck` 4.9.1 on .NET 8 and 9, 5.0.0 on .NET 10 |

Every companion package also depends on the core package. The versions shown are the minimum versions each package references.

```bash
dotnet add package Masterly.BusinessRules
dotnet add package Masterly.BusinessRules.Expressions
dotnet add package Masterly.BusinessRules.Diagnostics
dotnet add package Masterly.BusinessRules.DependencyInjection
dotnet add package Masterly.BusinessRules.FeatureManagement
dotnet add package Masterly.BusinessRules.Testing
```

Or, in the Visual Studio Package Manager Console, run `Install-Package Masterly.BusinessRules`, and the same for each companion package you need.

:::note

`netstandard2.1` runs on .NET Core 3.0 or later and on .NET 5 or later. It does not run on .NET Framework. The Testing package needs a test project that targets .NET 8 or later.

:::
