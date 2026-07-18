# DalaMock `CreateDebouncer` Compatibility

Snapshot date: 2026-07-18

This handoff isolates the first upstream DalaMock fix that blocked hosted
preview startup for Echoglossian.

## Goal

Add compatibility for the current Dalamud `IFramework` contract by implementing:

```csharp
IDebouncer CreateDebouncer(TimeSpan throttleTime, Action action)
```

on DalaMock's `MockFramework`.

## Why this belongs upstream

This is not Echoglossian-specific behavior.

The DalaMock `6.1.7` package and source do not implement the current
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

The vendored copy already contains the intended compatibility fix:

- implement `CreateDebouncer(TimeSpan, Action)` on `MockFramework`
- provide a small internal `IDebouncer` implementation
- back that implementation with the existing `RunOnTick` path so behavior stays
  aligned with the mock framework's scheduling model

Keep the upstream patch narrow. Do not mix in plugin-loader or Echoglossian
adapter changes here.

## Acceptance criteria

1. DalaMock compiles against the current Dalamud service contract without
   missing `CreateDebouncer`.
2. Hosted plugin startup no longer fails at type-load time because of
   `MockFramework`.
3. The patch does not require Echoglossian-specific code.

## Suggested validation

From the Echoglossian side, this fix should preserve:

```powershell
dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1
```

If working in a DalaMock clone, add or run a DalaMock-local test that exercises
`MockFramework.CreateDebouncer` directly or indirectly through a hosted plugin
startup path.

## Out of scope

- `PluginLoader` assembly-resolution fixes
- Echoglossian async adapter logic
- preview-owned database/config path handling
- headless plugin cleanup

## Sync-back note

After an upstream PR exists or merges, return to Echoglossian and decide whether
the vendored copy can be reduced, rebased, or replaced by a released package.

## Quick resume prompt

> Continue from `docs/handoffs/dalamock-upstream-create-debouncer.md`. Use the
> vendored `vendor/DalaMock` tree from the Echoglossian previewer worktree as
> the concrete patch source, but keep the PR scoped to `MockFramework` and the
> current Dalamud `CreateDebouncer` contract only.
