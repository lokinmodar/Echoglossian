# DalaMock Hosted Plugin Loader Assembly Resolution

Snapshot date: 2026-07-18

This handoff isolates the second upstream DalaMock fix recovered during the
Echoglossian hosted preview work.

## Goal

Make DalaMock resolve the real plugin assembly correctly when hosted startup:

- uses an async adapter type for `AddPlugin(...)`
- supplies the production plugin assembly through
  `PluginLoadSettings.AssemblyLocation`

## Why this belongs upstream

This is host behavior, not Echoglossian behavior.

When a hosted flow uses an adapter such as `IAsyncDalamudPlugin` to bootstrap a
real production plugin, DalaMock should honor the explicit assembly location
supplied by the caller instead of deriving the wrong identity from the adapter's
base-type path.

## Failure mode seen locally

The problematic path can resolve plugin identity from the adapter base-type
chain and collapse to the wrong assembly source, including
`System.Private.CoreLib`-adjacent resolution instead of the intended production
plugin assembly.

In Echoglossian's hosted preview rail, that matters because the host starts an
async adapter but wants to load the real `Echoglossian` assembly explicitly.

## Upstream base and local patch source

- upstream repo: `https://github.com/Critical-Impact/DalaMock`
- upstream tag: `6.1.7`
- upstream commit: `2fd273393d55c9850bfce2314520a50254954b12`
- local patch basis:
  `C:\Dante\_dalamud\worktrees\Echoglossian\previewer-dalamock-font-builds\vendor\DalaMock`

Target file:

- `DalaMock/Plugin/PluginLoader.cs`

Related local caller:

- `Echoglossian.Mock/Hosting/HostedPreviewPluginSessionFactory.cs`

The local caller does this intentionally:

- `loader.AddPlugin(typeof(EchoglossianAsyncPluginAdapter))`
- `settings.AssemblyLocation = typeof(global::Echoglossian.Echoglossian).Assembly.Location`

That combination is valid and should not confuse DalaMock.

## Local patch shape

The vendored copy already carries the intended fix:

- prefer `PluginLoadSettings.AssemblyLocation` when it is available
- avoid deriving plugin assembly identity from adapter base-type resolution when
  the caller already supplied the concrete assembly to load

Keep this upstream patch narrow. Do not mix in `MockFramework.CreateDebouncer`
or Echoglossian-local hosting/disposal code.

## Acceptance criteria

1. Hosted DalaMock startup can load the intended production plugin assembly when
   an async adapter is used.
2. An explicit `AssemblyLocation` takes precedence over brittle inferred
   adapter-based resolution.
3. The fix remains generic and does not mention Echoglossian.

## Suggested validation

From the Echoglossian side, this fix should preserve:

```powershell
dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1
```

If possible in a DalaMock clone, add a focused hosted-plugin loader test that:

- registers an async adapter plugin type
- passes an explicit `AssemblyLocation`
- asserts that the real target assembly is loaded instead of an inferred base
  assembly

## Out of scope

- `MockFramework.CreateDebouncer`
- Echoglossian async adapter implementation details
- preview-owned DB/config staging
- headless plugin cleanup and SQLite pool handling

## Sync-back note

After an upstream PR exists or merges, return to Echoglossian and decide
whether the vendored `PluginLoader` delta can be removed or replaced through a
released package upgrade.

## Quick resume prompt

> Continue from
> `docs/handoffs/dalamock-upstream-plugin-loader-assembly-resolution.md`. Use
> the vendored `vendor/DalaMock` tree in the Echoglossian previewer worktree as
> the patch source, but keep the upstream PR scoped to generic hosted-plugin
> assembly resolution through `PluginLoadSettings.AssemblyLocation`.
