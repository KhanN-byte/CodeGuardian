# CodeEnhancement

CodeEnhancement is a Roslyn-powered command-line tool that helps .NET teams protect
architecture boundaries, find risky C# patterns, and detect breaking public API
changes before release.

It is designed for teams that want lightweight, reviewable engineering rules
without introducing a full analysis platform.

## What it demonstrates

- Roslyn workspace and syntax analysis
- Configurable architecture enforcement
- Public API snapshotting and compatibility checks
- Useful CLI output and CI-friendly exit codes
- Separation between analysis logic and command-line delivery
- Automated tests against in-memory Roslyn projects

## Current rules

| Rule | Severity | Description |
| --- | --- | --- |
| `CG001` | Error | A project reference violates a configured architecture boundary. |
| `CG101` | Warning | An `async void` method cannot be awaited and can hide failures. |
| `CG102` | Warning | `.Result` or `.Wait()` synchronously blocks a task. |
| `CG103` | Warning | An empty `catch` block silently swallows an exception. |

## Quick start

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```bash
dotnet build CodeEnhancement.slnx
dotnet run --project tests/CodeEnhancement.Tests
dotnet run --project src/CodeEnhancement.Cli -- analyze samples/ShopSample/ShopSample.slnx
```

The included sample intentionally contains a forbidden project reference and
risky code patterns, so analysis returns a non-zero exit code.

## Configure architecture rules

Place `codeenhancement.json` next to a solution or pass `--config <path>`:

```json
{
  "architectureRules": [
    {
      "source": "*.Domain",
      "cannotReference": "*.Infrastructure",
      "message": "Domain must not depend on infrastructure."
    }
  ]
}
```

`source` and `cannotReference` support `*` and `?` wildcards.

## Track public API changes

Create a baseline:

```bash
dotnet run --project src/CodeEnhancement.Cli -- \
  baseline samples/ShopSample/ShopSample.slnx --output api-baseline.json
```

Compare a later build:

```bash
dotnet run --project src/CodeEnhancement.Cli -- \
  api-diff samples/ShopSample/ShopSample.slnx --baseline api-baseline.json
```

Removed public or protected members are errors; additions are informational.
Both `analyze` and `api-diff` support `--format json` for CI integration.

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | Analysis completed without errors |
| `1` | An architecture violation or breaking API removal was found |
| `2` | Invalid command, configuration, or workspace failure |

## Architecture

```text
CodeEnhancement.Cli
      |
      v
CodeEnhancement.Core
  |- Workspace loading
  |- Architecture analysis
  |- Risky-code analysis
  `- API surface comparison
```

The core library is independent from console formatting, which keeps the
analysis behavior testable and reusable.

## Roadmap

- Namespace-level architecture rules
- SARIF output for code-scanning platforms
- Suppressions with justification and expiry dates
- Git-based API comparison
- IDE diagnostics

## License

MIT
