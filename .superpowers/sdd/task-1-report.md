# Task 1 Report
# Task 1 Report: Plugin-Window Backend Mode Contract And CLI/Shell State

## Status

Completed.

## Implementation

- Added the internal `PluginWindowPreviewBackendMode` contract with `Auto`, `Standalone`, and `DalaMockHosted` values.
- Added `PluginWindowBackendStatus` for requested/effective backend state and fallback diagnostics.
- Added `--plugin-window-backend` parsing for `auto`, `standalone`, and `dalamock`; unknown values throw an `ArgumentException` naming the plugin window backend.
- Kept the existing standalone plugin-window host unchanged. Interactive shell status now reflects the requested mode, effective standalone mode, and an explicit uninitialized-hosted-backend fallback reason when applicable.
- Added focused tests for mode parsing and backend status retention.

## TDD Evidence

1. Added the focused tests before production implementation.
2. Ran the required focused command. It failed because the new `PluginWindows` namespace and backend types did not exist; the provided FluentAssertions sample also could not compile because this project has no FluentAssertions package.
3. Adapted assertions to the repository's existing xUnit-only test dependencies and kept the production contract internal. The public xUnit theory accepts the expected enum as `object` to avoid exposing an internal enum through a public test method.
4. Added the minimum production implementation and re-ran the focused command successfully.

## Validation

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter "FullyQualifiedName~PreviewCommandLineBackendModeTests|FullyQualifiedName~PreviewShellBackendStatusTests"
```

Result: passed, 6/6 tests. The command emitted pre-existing package vulnerability and project build warnings, but no task-specific warnings or errors.

## Self-Review

- Reviewed the diff and ran `git diff --check`; no whitespace errors.
- Confirmed modified production files are limited to the task-owned CLI, program, shell, and new plugin-window contract files.
- Confirmed no plugin runtime, packaging, release-flow, host selection, or DalaMock initialization behavior changed.
