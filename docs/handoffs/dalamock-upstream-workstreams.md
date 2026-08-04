# DalaMock Upstream Workstreams

Snapshot date: 2026-07-18

Status update 2026-07-19: the `CreateDebouncer` workstream is superseded for
stable Dalamud 15.0.2.3 because stable does not expose `IDebouncer`. The
remaining upstream blocker for removing Echoglossian's DalaMock vendor is the
`PluginLoader` `AssemblyLocation` fix plus a released DalaMock package that
contains it.

This handoff is the entry point for a separate chat focused on upstreaming the
current Echoglossian-hosted preview compatibility fixes into `DalaMock`.

Read `AGENTS.md` first. Then verify the current branch, worktree, and local
`vendor/` state before editing anything.

## Current source of truth

- repo: `C:\Dante\_dalamud\Echoglossian`
- worktree used for this recovery:
  `C:\Dante\_dalamud\worktrees\Echoglossian\previewer-dalamock-font-builds`
- branch: `feature/previewer-dalamock-font-builds`
- local vendored upstream base:
  `vendor\DalaMock`
- vendored upstream repo: `https://github.com/Critical-Impact/DalaMock`
- vendored upstream tag: `6.1.7`
- vendored upstream commit: `2fd273393d55c9850bfce2314520a50254954b12`

## Important current-state rule

The current Echoglossian branch depends on the vendored DalaMock source tree.

That means `vendor/` is not optional in the current branch state:

- `Echoglossian.Mock\Echoglossian.Mock.csproj` references
  `..\vendor\DalaMock\DalaMock\DalaMock.Core.csproj`
- `Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj` does the same
- without versioning `vendor/`, the branch is not reproducible or buildable on
  another machine or in another chat

Until upstream DalaMock accepts and releases equivalent fixes, treat the
vendored copy as the patch basis.

## Goal

Split the current local DalaMock delta into upstream-worthy fixes and move those
fixes into DalaMock cleanly, without mixing in Echoglossian-specific hosting
glue.

## Workstreams

1. [DalaMock `CreateDebouncer` Compatibility](./dalamock-upstream-create-debouncer.md) (superseded for stable)
2. [DalaMock Hosted Plugin Loader Assembly Resolution](./dalamock-upstream-plugin-loader-assembly-resolution.md)

## What belongs upstream

Only this area is currently a strong stable upstream candidate:

- `DalaMock/Plugin/PluginLoader.cs`
  - resolve plugin assembly identity correctly when hosted startup uses an async
    adapter plus `PluginLoadSettings.AssemblyLocation`

## What does not belong in the upstream PRs

Do not push these Echoglossian-local pieces into DalaMock unless the upstream
project explicitly asks for abstractions that justify them:

- `EchoglossianAsyncPluginAdapter`
- `HeadlessPluginCleanup`
- preview-owned DB/config seeding logic
- SQLite pool cleanup and no-parallelization rules for Echoglossian tests
- `Echoglossian.csproj` vendor exclusion

Those are local hosting accommodations, not generic DalaMock fixes.

## Recommended execution order

1. create or use a dedicated DalaMock clone or fork
2. confirm the vendored `6.1.7` files match the intended upstream base
3. do not upstream `CreateDebouncer` for stable unless the official Dalamud API
   changes again
4. upstream the `PluginLoader` assembly-resolution fix
5. once that fix is accepted and available in a release, return to Echoglossian and:
   - remove or reduce the vendored delta
   - switch back to package consumption if that becomes practical

## Shared validation expectation

At minimum, another chat should preserve these rails while working:

```powershell
dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1
```

If the upstream chat works inside a DalaMock clone, add the equivalent local
DalaMock tests there as part of the PR.

## Related local docs

- [DalaMock Hosted Preview Boundary](../dalamock-hosted-preview-boundary.md)
- [Unified ImGui Previewer Next Step](./unified-imgui-previewer-next-step.md)
- [Vendored DalaMock upstream note](../../vendor/DalaMock/UPSTREAM.md)

## Quick resume prompt

> Continue from `docs/handoffs/dalamock-upstream-workstreams.md`. Use the
> vendored `vendor/DalaMock` tree in the Echoglossian previewer worktree as the
> known-good patch basis, but keep the PR scope DalaMock-only. Do not resume
> `CreateDebouncer` for stable; focus on the hosted `PluginLoader`
> assembly-resolution fix.
