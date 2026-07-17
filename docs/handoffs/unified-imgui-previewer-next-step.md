# Unified ImGui Previewer Next Step

Snapshot date: 2026-07-17

This handoff captures the current state of the standalone ImGui previewer work
on `feature/dalamock-unified-previewer` after Phase 1 implementation and the
final review-fix passes that followed it.

Read `AGENTS.md` first. Then verify the current branch and worktree state
before editing.

## Branch and workspace

- branch: `feature/dalamock-unified-previewer`
- recommended base: current `v4-series`
- worktree used during implementation:
  `C:\Dante\_dalamud\worktrees\Echoglossian\feature-dalamock-unified-previewer`
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

The highest-value next slice is **preview shell control expansion**, not more
runtime plumbing.

Keep the next slice scoped to shell-driven preview controls that do not require
starting a fake Dalamud runtime and do not write back to live user files.

Recommended scope:

1. add richer preview-only control knobs in the shell:
   - explicit re-render / reapply button
   - preview font family picker from the resolved preview font set
   - preview font size override
   - preview text color / opacity controls if they can be wired through the
     existing overlay config model without forking renderer logic
2. keep using real plugin-window code for plugin windows:
   - do not reimplement the config window in preview-only code
3. preserve current isolation rules:
   - previewer remains outside `Echoglossian.sln`
   - preview artifacts stay out of plugin packaging
   - preview session writes only to cloned session files
   - runtime-owned actions remain explicitly unavailable in preview

## Constraints for the next chat

- continue from a fresh dedicated worktree based on the latest `v4-series`
- do not assume this branch stays the base forever; re-check current repo state
  first
- keep DalaMock out unless a future step has a concrete reason to absorb its
  maintenance cost
- prefer shell-level controls over deeper renderer rewrites
- preserve 1:1 behavior for real plugin windows whenever a control can be
  expressed through existing config/runtime inputs

## Quick resume prompt

> Continue from `docs/handoffs/unified-imgui-previewer-next-step.md` on a fresh
> dedicated worktree based on the latest `v4-series`. First verify the current
> repo state, then scope the next previewer slice around shell-side control
> expansion only.
