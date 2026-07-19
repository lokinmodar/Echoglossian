# Unified ImGui Previewer Next Step

Snapshot date: 2026-07-18

This handoff captures the current state of the standalone ImGui previewer work
on the previewer follow-up branches after Phase 1 implementation, the hybrid
hosted-backend slice, and the DalaMock compatibility recovery that followed.

Read `AGENTS.md` first. Then verify the current branch and worktree state
before editing.

## Branch and workspace

- branch at the original Phase 1 snapshot: `feature/dalamock-unified-previewer`
- latest follow-up branch at this snapshot:
  `feature/previewer-dalamock-font-builds`
- recommended base: current `v4-series`
- worktree used during implementation:
  `C:\Dante\_dalamud\worktrees\Echoglossian\feature-dalamock-unified-previewer`
- follow-up worktree used for hosted-backend recovery:
  `C:\Dante\_dalamud\worktrees\Echoglossian\previewer-dalamock-font-builds`
- merge base against `origin/v4-series` at this snapshot:
  `578bce65cc09deeb23571e0e58ed6306f871d859`

## What Phase 1 now provides

`Echoglossian.Previewer` is now a single development-only Windows host that can
run:

- overlay preview scenarios using the shared real overlay renderer
- the real `Config` window through preview-safe context and save routing
- the real `DB Manager` window against a cloned preview-owned SQLite snapshot
- the real `Translator Metrics / Debugger` window through preview-safe runtime
  delegates
- deterministic screenshot export for:
  - full frame
  - overlay surface crop
  - `Config`
  - `DB Manager`
  - `Translator Metrics / Debugger`

The previewer remains isolated from normal plugin build, deploy, and packaging
flows.

## Hosted plugin-window backend

Phase 2 added a hybrid plugin-window backend with:

- CLI and shell selection for `auto`, `standalone`, and `dalamock`
- a DalaMock-hosted runtime path for `Config`, `DB Manager`, and `Translator
  Metrics / Debugger`
- explicit fallback diagnostics in the shell and screenshot manifest

The previewer remains outside `Echoglossian.sln`; plugin packaging and release
flow are unchanged. `auto` requests DalaMock first and falls back visibly to
the existing standalone backend. Explicit `dalamock` mode does not hide a
hosted-startup failure.

## Phase 2 validation on 2026-07-17

Commands run from
`C:\Dante\_dalamud\worktrees\Echoglossian\previewer-dalamock-font-builds`:

```powershell
dotnet build Echoglossian.sln -c Debug --no-restore
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --no-restore -p:VSTestMaxCpuCount=1
dotnet restore Echoglossian.Mock\Echoglossian.Mock.csproj
dotnet restore Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj
dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --host-smoke
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --plugin-window-backend auto --screenshot full --capture-target config-window --scenario talk --viewport 1920x1080 --output artifacts\previewer\hosted-backend-validation
```

Observed results at that time:

- production build: passed with `0` errors
- production tests: `642 / 642` passed
- previewer tests: `143 / 143` passed
- DalaMock restores and build: passed with `0` errors
- DalaMock tests: failed, `5 / 11`; hosted startup throws
  `ReflectionTypeLoadException` because `DalaMock.Core` 6.1.7's
  `MockFramework.CreateDebouncer` has no implementation
- host smoke: exited `0`
- auto-backend Config capture: wrote `manifest.json` and one PNG; the manifest
  records requested `Auto`, effective `Standalone`, and the exact DalaMock
  fallback reason

That July 17 failure was a real DalaMock compatibility blocker, not evidence
that the previewer architecture or the Echoglossian plugin window path had to
be discarded.

## Hosted DalaMock recovery on 2026-07-18

Status update 2026-07-19: the `CreateDebouncer` part below was a staging-only
diagnosis. Stable Dalamud 15.0.2.3 does not expose `IDebouncer`, so that local
patch was removed. The remaining stable DalaMock blocker is hosted plugin
assembly resolution through `PluginLoadSettings.AssemblyLocation`.

Commands run from
`C:\Dante\_dalamud\worktrees\Echoglossian\previewer-dalamock-font-builds`:

```powershell
dotnet build Echoglossian.sln -c Debug --no-restore
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
dotnet build Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --no-restore
dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1
```

Observed results:

- production build: passed with `0` errors
- production tests: `642 / 642` passed
- previewer test build: passed with `0` errors
- DalaMock mock-test build: passed with `0` errors
- DalaMock mock tests: `11 / 11` passed

The hosted blocker was recovered locally by vendoring a minimal DalaMock source
copy and applying the hosted plugin assembly-resolution fix. A prior
`CreateDebouncer` experiment was removed after stable Dalamud validation:

- plugin assembly resolution in `PluginLoader` when using the async hosted
  adapter plus `AssemblyLocation`

See `docs/dalamock-hosted-preview-boundary.md` and `vendor/DalaMock/UPSTREAM.md`
for the upstream/local split.

## Important final fixes already in this branch

- screenshot output publication now stages atomically and preserves recovery
  directories when rollback is incomplete
- preview mode suppresses live runtime actions that would otherwise try to
  contact providers or mutate shared glossary/runtime state
- provider credential fields render masked instead of plain text
- preview engine configuration remains editable while passive live refresh
  requests stay suppressed
- duplicate `PreviewWorkbenchState.Scenario` storage was removed so the shell
  owns the only mutable scenario state

## Validation performed on 2026-07-17

Commands:

```powershell
dotnet build Echoglossian.sln -c Debug --no-restore -v minimal
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1 -v minimal
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --no-build --no-restore -p:VSTestMaxCpuCount=1 -v minimal
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --binding-smoke
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --host-smoke
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --screenshot full --capture-target config-window --scenario talk --viewport 1920x1080 --output .tmp\preview-export-check-20260717g
```

Observed results:

- main test suite: `640 / 640` passed
- previewer test suite: `122 / 122` passed
- binding smoke printed `Dalamud ImGui binding OK: 1.88`
- host smoke exited `0`
- config-window export wrote `manifest.json` plus one PNG at `1000x900`

The interactive previewer build from this branch was also opened and manually
tested on 2026-07-17.

## Review debt status

The two lingering minor findings from the subagent-driven-development ledger
were addressed as follows:

1. duplicate workbench scenario state:
   - resolved in code by removing the stale secondary `Scenario` storage from
     `PreviewWorkbenchState`
2. manifest/capture-target coverage concern:
   - accepted as already sufficiently covered by
     `BatchScreenshotRunnerTests.SerializeManifest_PluginWindowTarget_WritesCaptureTargetString`
     plus
     `BatchScreenshotRunnerTests.GetManifestTargetMetadata_PluginWindow_UsesNotApplicableValues`

## Recommended next step

The highest-value next slice is upstreaming the DalaMock compatibility fixes
before broadening hosted-runtime scope again.

Recommended scope:

1. upstream the DalaMock fix for hosted plugin assembly resolution through
   `AssemblyLocation`; do not upstream `CreateDebouncer` for stable unless the
   official Dalamud API changes again
2. keep using real plugin-window code for plugin windows:
   - do not reimplement the config window in preview-only code
3. preserve current isolation rules:
   - previewer remains outside `Echoglossian.sln`
   - preview artifacts stay out of plugin packaging
   - preview session writes only to cloned session files
   - runtime-owned actions remain explicitly unavailable in preview

## DalaMock versus Echoglossian boundary

The current evidence points to a split responsibility:

- DalaMock owned the original hosted-startup blocker
- Echoglossian owns the local adapter, state cloning, and headless cleanup
  needed to host the real plugin safely outside the game

That means the previewer should continue using real plugin code while the
upstream fixes are proposed separately.

## Constraints for the next chat

- continue from a fresh dedicated worktree based on the latest `v4-series`
- do not assume this branch stays the base forever; re-check current repo state
  first
- keep the hosted backend optional until upstream DalaMock ships the equivalent
  compatibility fixes or the vendored delta is consciously retained
- prefer shell-level controls over deeper renderer rewrites
- preserve 1:1 behavior for real plugin windows whenever a control can be
  expressed through existing config/runtime inputs

## Quick resume prompt

> Continue from `docs/handoffs/unified-imgui-previewer-next-step.md` on a fresh
> dedicated worktree based on the latest `v4-series`. First verify the current
> repo state, review `docs/dalamock-hosted-preview-boundary.md`, upstream the
> vendored DalaMock compatibility fixes if possible, and only then expand the
> hosted previewer shell controls or surface coverage.
