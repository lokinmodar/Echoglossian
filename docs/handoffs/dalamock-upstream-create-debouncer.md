# DalaMock `CreateDebouncer` Compatibility

Snapshot date: 2026-07-18

Status update 2026-07-19: this handoff is superseded for stable Dalamud.
Stable Dalamud 15.0.2.3 does not expose `IDebouncer`; keeping the local
`CreateDebouncer` patch breaks builds against stable binaries. Do not execute
this handoff unless targeting a staging-only API after rechecking the official
Dalamud contract.

This handoff originally isolated a staging-only DalaMock fix that appeared to
block hosted preview startup for Echoglossian.

## Goal

Historical staging-only goal: add compatibility for a non-stable Dalamud
`IFramework` contract by implementing:

```csharp
IDebouncer CreateDebouncer(TimeSpan throttleTime, Action action)
```

on DalaMock's `MockFramework`.

## Why this belongs upstream

This is not Echoglossian-specific behavior.

The DalaMock `6.1.7` package and source did not implement the staging
`IFramework.CreateDebouncer(TimeSpan, Action)` member. That causes hosted plugin
startup to fail during type loading before most plugin-specific behavior
matters.

## Symptom seen in Echoglossian

Before the local patch, hosted startup and mock tests could fail with
`ReflectionTypeLoadException` mentioning `CreateDebouncer`.

That failure showed up while starting the hosted preview rail and was also
captured in the historical version of
`Echoglossian.Mock.Tests\HostedPreviewPluginSessionTests.cs`.

## Upstream base and local patch source

- upstream repo: `https://github.com/Critical-Impact/DalaMock`
- upstream tag: `6.1.7`
- upstream commit: `2fd273393d55c9850bfce2314520a50254954b12`
- local patch basis:
  `C:\Dante\_dalamud\worktrees\Echoglossian\previewer-dalamock-font-builds\vendor\DalaMock`

Target file:

- `DalaMock/Mocks/DalamudServices/MockFramework.cs`

## Local patch shape

The vendored copy no longer contains this compatibility fix on the stable branch:

- the former `CreateDebouncer(TimeSpan, Action)` implementation was removed
- the former internal `IDebouncer` implementation was removed
- stable validation should fail if this staging-only member is reintroduced

Keep any future staging-only experiment separate from the stable upstream PR.

## Acceptance criteria

1. DalaMock compiles against stable Dalamud without `IDebouncer`.
2. Hosted plugin startup continues to be covered by the `PluginLoader`
   `AssemblyLocation` fix instead.
3. No staging-only API is reintroduced into stable-targeted code.

## Suggested validation

From the Echoglossian side, this fix should preserve:

```powershell
dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1
```

If working in a DalaMock clone, do not add `CreateDebouncer` tests unless the
target branch intentionally tracks staging Dalamud.

## Out of scope

- `PluginLoader` assembly-resolution fixes
- Echoglossian async adapter logic
- preview-owned database/config path handling
- headless plugin cleanup

## Sync-back note

After an upstream release includes the remaining `PluginLoader` fix, return to
Echoglossian and decide whether the vendored copy can be removed.

## Quick resume prompt

> Do not resume `docs/handoffs/dalamock-upstream-create-debouncer.md` for stable
> Dalamud. Recheck the official Dalamud API first; stable 15.0.2.3 does not
> expose `IDebouncer`.
