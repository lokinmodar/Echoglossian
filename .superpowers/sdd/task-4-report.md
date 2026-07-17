# Task 4 Report: DalaMock Startup Smoke-Test Rail and Canonical Local Validation Script

Date: 2026-07-17

## Summary

Finished Task 4 by adding the standalone `Echoglossian.Mock.Tests` project and the canonical `scripts/validate-local-tests.ps1` rail without changing `Echoglossian.sln`.

## Files Added

- `Echoglossian.Mock.Tests/Echoglossian.Mock.Tests.csproj`
- `Echoglossian.Mock.Tests/StartedPlugin.cs`
- `Echoglossian.Mock.Tests/TestBoot.cs`
- `Echoglossian.Mock.Tests/PluginStartupSmokeTests.cs`
- `Echoglossian.Mock.Tests/packages.lock.json`
- `scripts/validate-local-tests.ps1`

## Root Cause and Fix

### Root cause reproduced

Command:

```powershell
dotnet test .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -v normal
```

Observed failure before the fix:

- `DalaMock.Core` headless mode (`CreateWindow = false`) resolves `NullTextureProvider`
- the production plugin constructor eagerly calls `TextureProvider.CreateFromImageAsync(...)`
- headless startup therefore threw `System.NotImplementedException` from `NullTextureProvider.CreateFromImageAsync(...)`

### Minimal fix applied

Kept the production entrypoint unchanged and fixed the headless rail locally inside `Echoglossian.Mock.Tests`:

- added a local `HeadlessTextureProviderProxy` in `TestBoot.cs`
- wrapped DalaMock's resolved `ITextureProvider`
- intercepted only `CreateFromImageAsync(...)`
- returned a synthetic 1x1 `HeadlessTextureWrap` so constructor-time embedded image loads can succeed under headless DalaMock

## What Changed

### Mock test project

- Added the requested `Echoglossian.Mock.Tests` project using `Dalamud.NET.Sdk/15.0.0`
- Targeted `net10.0-windows` / `win-x64`
- Added `DalaMock.Core 6.1.7`, `FluentAssertions`, `Microsoft.NET.Test.Sdk`, and `xUnit`
- Added the requested explicit Dalamud/Lumina/FFXIV output references
- Added a direct `ProjectReference` to `..\Echoglossian.csproj`

### Boot helper and startup seam

- Added the requested `StartedPlugin` record wrapper
- Added `TestBoot` to build a local headless `MockContainer`
- Reused the existing async adapter approach already present in the interrupted Task 4 working tree
- Kept the production `Echoglossian : IDalamudPlugin` entrypoint unchanged
- Added a local texture-provider shim only inside the test project

### Smoke tests

- Kept the requested startup milestone assertions
- Kept the requested shutdown milestone assertions
- Added a test-only headless dispose preparation step that replaces the private `registeredAddonHandlers` list with an empty instance before `Dispose()`

This was needed because headless DalaMock has no live `AtkStage`, and the first shutdown attempt failed in native addon restoration (`JournalHandler.OnPluginUnload()` -> `AtkStage.Instance()`).

That preparation keeps the shutdown smoke test focused on plugin-level disposal milestones in a headless rail instead of native UI restoration behavior that requires a live game UI.

### Canonical local validation script

- Added `scripts/validate-local-tests.ps1`
- The script now validates:
  - `Echoglossian.Tests`
  - `Echoglossian.Mock`
  - `Echoglossian.Mock.Tests`

## Validation

### Focused mock rail

Commands:

```powershell
dotnet build .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore
dotnet test .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -v normal
```

Result: PASS

Passing tests:

- `PluginStartupSmokeTests.StartPluginAsync_marks_expected_startup_stages`
- `PluginStartupSmokeTests.Dispose_marks_expected_shutdown_stages`

### Canonical local validation rail

Command:

```powershell
.\scripts\validate-local-tests.ps1
```

Result: PASS

Observed results:

- `dotnet build .\Echoglossian.sln -c Debug --no-restore` — PASS
- `dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build` — PASS (`624` tests)
- `dotnet build .\Echoglossian.Mock\Echoglossian.Mock.csproj -c Debug --no-restore` — PASS
- `dotnet test .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build` — PASS (`2` tests)

Warnings observed during validation:

- transitive `NU1903` warning for `SQLitePCLRaw.lib.e_sqlite3` `2.1.11`
- existing Multilingual App Toolkit warning from `Echoglossian.csproj`
- existing duplicate-using warning in `Echoglossian.Tests/TranslatorContractTests.cs`

None of those warnings blocked Task 4 validation.

## Self-Review

- Scope stayed local to the additive validation rail
- `Echoglossian.sln` was left unchanged
- the production plugin host architecture was not changed
- the production `Echoglossian : IDalamudPlugin` entrypoint remains intact
- `Echoglossian.Tests` remains the primary unit/integration suite; DalaMock was added as an extra rail rather than a replacement
- the texture shim is local to `Echoglossian.Mock.Tests` and only intercepts the constructor-time image load seam that failed under headless DalaMock
- the shutdown smoke test now explicitly documents the headless/native-UI boundary instead of silently depending on a live `AtkStage`

## Commit

- `8cb3a0e` — `test: add local DalaMock startup smoke tests`

## Concerns

- The shutdown smoke test intentionally skips native addon restoration by clearing `registeredAddonHandlers` before headless dispose. That keeps this rail aligned with headless DalaMock, but it means native UI restoration on unload is still something to verify in a live game/client environment rather than through this smoke test.
- Validation still surfaces unrelated existing/transitive warnings noted above.

## Review Fixes (2026-07-17)

### Findings addressed

- `PluginStartupSmokeTests` now disposes the started rail in both smoke paths instead of leaving the first started plugin/container alive for the rest of the run.
- `TestBoot` no longer reuses `Environment.CurrentDirectory\.dalamock` or `Environment.CurrentDirectory\test.json`; each startup now gets its own isolated state root.

### Root cause

- The original rail returned a passive `StartedPlugin` wrapper, so the startup smoke test had no deterministic teardown path and left a live plugin instance behind.
- `TestBoot` anchored DalaMock save/config state to fixed working-directory paths, so repeated local validations could reuse stale persisted files from earlier runs.
- When I added immediate state-root deletion, the SQLite DB file could still retain a handle briefly after unload, so hard hermeticity needed to come from unique per-run roots rather than assuming synchronous directory deletion.

### Narrow fix applied

- Promoted `StartedPlugin` into an `IDisposable` helper that:
  - prepares headless unload once,
  - disposes the owning mock container,
  - falls back to direct plugin disposal only if the container did not unload it,
  - stays idempotent so explicit dispose and test-scope cleanup cannot double-dispose the plugin.
- Updated `PluginStartupSmokeTests` to dispose through `StartedPlugin` and added regression assertions for:
  - deterministic cleanup capability,
  - host-state isolation away from the test runner working directory.
- Changed `TestBoot` to create a unique temp-root per startup run and place both `.dalamock` and `test.json` under that isolated root.
- Kept temp-root directory deletion best-effort so a short-lived SQLite handle does not fail the smoke rail after successful plugin teardown.

### Review-fix validation

Red-phase reproduction:

```powershell
dotnet test .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug
```

Observed before the fix:

- `StartedPlugin_exposes_deterministic_cleanup` failed because `StartedPlugin` did not implement `IDisposable`.
- `StartPluginAsync_keeps_host_state_out_of_the_test_working_directory` failed because `.dalamock` was created under `Environment.CurrentDirectory`.

Required fresh verification:

```powershell
dotnet test .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build
.\scripts\validate-local-tests.ps1
```

Observed after the fix:

- `dotnet test .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build` — PASS (`4` tests)
- `.\scripts\validate-local-tests.ps1` — PASS
  - `dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build` — PASS (`624` tests)
  - `dotnet test .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build` — PASS (`4` tests)

### Review-fix concern

- The rail now guarantees hermetic per-run local state, but immediate removal of the temp state root is best-effort because the SQLite DB file can still hold a handle briefly after teardown. This no longer affects repeatability because each run gets a fresh state root.

## Rereview Micro-Fix (2026-07-17)

### Finding addressed

- Replaced the ambient `Environment.CurrentDirectory` cleanup pattern in `PluginStartupSmokeTests.StartPluginAsync_keeps_host_state_out_of_the_test_working_directory()` with assertions against the started rail's owned state paths.

### Narrow fix applied

- Added `StartedPlugin.PluginSavePath` and `StartedPlugin.ConfigPath` so the smoke tests can assert the DalaMock-owned save/config locations directly.
- Updated the isolation test to verify:
  - the per-run `StateRoot` is outside the test working directory,
  - the rail-owned `.dalamock` save path lives under `StateRoot`,
  - the rail-owned `test.json` config path resolves under `StateRoot`.
- Removed the helper that deleted generic `.dalamock` / `test.json` names under `Environment.CurrentDirectory`.

### Command and result

```powershell
dotnet test .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build
```

Result:

- PASS (`5` tests)

### Concern

- This fix keeps the test hermetic without touching ambient working-directory files, but it still validates path ownership rather than forcing immediate physical deletion of the temporary state root, which remains best-effort by design after headless teardown.
