# Task 4 Report

## Status

DONE_WITH_CONCERNS

## Implementation

- Added `DalaMockHostedPluginWindowPreviewBackend`, which owns the started hosted session and delegates preview-host drawing, capture stabilization, and cleanup to the existing standalone renderer.
- Added `PluginWindowPreviewBackendFactory`: `Standalone` creates standalone directly; `Auto` catches hosted boot failures and returns standalone with an `Auto` fallback status; explicit `DalaMockHosted` rethrows hosted boot failures.
- Referenced `Echoglossian.Mock` from the previewer and threaded backend selection through interactive and screenshot startup.
- Hosted startup uses only the current preview session's working directory, cloned config, and cloned database path.

## TDD Evidence

- RED: added `PluginWindowPreviewBackendFactoryTests` and ran the focused test command. It failed because `PluginWindowPreviewBackendFactory` did not exist (`CS0103`).
- GREEN: implemented the factory and hosted backend. The focused factory suite passes 2/2.

## Validation

- `dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~PluginWindowPreviewBackendFactoryTests`
  - Passed: 2/2 tests.
- `dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --filter FullyQualifiedName~HostedPreviewPluginSessionTests`
  - Blocked by the known pre-existing DalaMock/Dalamud incompatibility: `ReflectionTypeLoadException` from `DalaMock.Core.Mocks.DalamudServices.MockFramework.CreateDebouncer` while `MockContainer` registers services.
- `git diff --check`
  - Passed.

## Self-Review

- `Auto` exposes the original hosted error through `PluginWindowBackendStatus.FallbackReason`; it does not silently reduce fidelity.
- Explicit `DalaMockHosted` does not catch hosted boot failures.
- The change is confined to previewer-owned code and the local mock reference; no plugin packaging, release, or production-plugin runtime files changed.
- The outstanding hosted-session test failure predates this task and is intentionally preserved so `Auto` can exercise its safe fallback at runtime.

## Review Fix Wave

### Root Cause

The original hosted backend owned a `HostedPreviewPluginSession` but delegated every window operation to `StandalonePluginWindowPreviewBackend`. It therefore reported `DalaMockHosted` while drawing standalone-created window objects.

### Changes

- Replaced the standalone delegation with a preview-owned, cached reflection bridge over the hosted production plugin's existing `EchoglossianConfigUi`, `DrawDbEditorWindow`, and `DrawTranslatorMetricsWindow` methods.
- The bridge resolves the private production members once, then uses the hosted plugin's own config, DB manager, and metrics window open state and captured bounds for draw, stable capture, DB availability, and state synchronization.
- Preserved the existing deterministic capture layout and stability tracker for the hosted windows.
- Wrapped `Auto` fallback backends with the factory-selected status, so screenshot callers retain requested/effective mode and fallback diagnostics through `IPluginWindowPreviewBackend.Status`.
- Expanded `ReflectionTypeLoadException` fallback detail with loader exception messages.
- Updated the hosted-session focused test to assert preview-owned input paths and recognize only the known `CreateDebouncer` DalaMock blocker.

### Review Fix Validation

- `PluginWindowPreviewBackendFactoryTests`: 3/3 passed, including cached hosted-window member resolution and fallback-status retention.
- `HostedPreviewPluginSessionTests`: 1/1 passed. The test reaches the known DalaMock `CreateDebouncer` `ReflectionTypeLoadException` and records it as the expected pre-existing blocker after validating explicit preview-owned inputs.
- `git diff --check`: passed.

### Remaining Concern

The local DalaMock/Dalamud incompatibility still prevents an end-to-end hosted UI frame. When that upstream incompatibility is resolved, hosted mode will invoke the cached production plugin window members; until then `Auto` falls back visibly and explicit `DalaMockHosted` surfaces the startup failure.

## Runtime Auto-Fallback Fix Wave

### Root Cause

`Auto` returned the hosted backend directly after successful startup. Any later hosted draw, capture, crop, or availability failure escaped without switching to standalone, and the interactive shell retained its initial factory status snapshot. Screenshot CLI output did not report a fallback.

### Changes

- Added an Auto-only wrapper backend that begins as `requested=Auto`, `effective=DalaMockHosted` and catches hosted runtime failures from draw, capture, crop, and availability operations.
- The wrapper creates standalone once, retries the interrupted operation, updates its status to `effective=Standalone`, and preserves the hosted failure as the fallback reason. Explicit `DalaMockHosted` continues to surface both startup and runtime failures.
- The interactive fidelity summary now reads the backend's current status each frame.
- Screenshot export retains the backend instance and prints requested/effective/fallback diagnostics after a fallback.

### Validation

- `dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~PluginWindowPreviewBackendFactoryTests`
  - Passed: 8/8 tests, including hosted startup status, runtime draw fallback, capture fallback, availability fallback, and explicit-hosted runtime failure propagation.
- `dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~HostedPreviewPluginSessionTests`
  - Passed: 1/1 test.
- `git diff --check`
  - Passed.

### Remaining Concern

The known upstream DalaMock/Dalamud `CreateDebouncer` incompatibility still prevents a local end-to-end hosted frame. Auto fallback now also protects against hosted failures after a future successful startup; explicit hosted mode remains intentionally fail-fast.

## Hosted Database Redirect Fix Wave

### Root Cause

`HostedPreviewPluginOptions.DatabasePath` was passed from the previewer but never consumed by hosted startup. The production plugin creates its DB Manager with `Echoglossian.ConfigDirectory/Echoglossian.db`, so it could not see the cloned preview snapshot.

### Changes

- Hosted startup now copies a supplied preview database into DalaMock's production plugin config directory (`PluginSavePath/Echoglossian/Echoglossian.db`) before the plugin starts.
- After startup, the factory verifies that the production plugin resolved that exact database path. A DalaMock layout mismatch is an explicit hosted startup failure, allowing Auto mode to report and use its safe fallback.
- Added a focused test that supplies a real SQLite snapshot with a marker table and verifies the marker through the effective production DB Manager path. It still recognizes the known `CreateDebouncer` blocker.

### Validation

- `dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~HostedPreviewPluginSessionTests`
  - Passed: 2/2 tests.
- `git diff --check`
  - Passed.

### Remaining Concern

The known upstream DalaMock/Dalamud `CreateDebouncer` incompatibility still prevents a local hosted frame from completing. The effective database-path validation will run once that blocker is resolved.
