# DalaMock Hosted Preview Boundary

Snapshot date: 2026-07-18

This note separates the fixes that should eventually move upstream into
`DalaMock` from the hosting code that remains specific to Echoglossian's local
preview and mock rails.

## Short Answer

The original hosted-startup failure was not primarily caused by a bad
Echoglossian implementation.

The first hard blocker lived in `DalaMock`:

- `MockFramework` did not implement
  `IFramework.CreateDebouncer(TimeSpan, Action)` for the current Dalamud
  contract.
- `PluginLoader` could resolve the wrong assembly identity when a hosted flow
  used an async adapter type plus `PluginLoadSettings.AssemblyLocation`.

Those two problems prevented the hosted runtime from starting correctly even
before most Echoglossian-specific behavior mattered.

## Upstream DalaMock Candidates

These changes are good candidates to upstream into `DalaMock` itself:

1. `DalaMock/Mocks/DalamudServices/MockFramework.cs`
   - add `CreateDebouncer(TimeSpan, Action)`
   - provide a small `IDebouncer` implementation backed by `RunOnTick`
2. `DalaMock/Plugin/PluginLoader.cs`
   - prefer `PluginLoadSettings.AssemblyLocation` when resolving the plugin
     assembly
   - avoid deriving plugin identity from an async adapter base type path that
     can collapse to `System.Private.CoreLib`

These are compatibility fixes for the host, not Echoglossian-specific behavior.

## Echoglossian-Owned Hosting Code

The following pieces should remain local to Echoglossian unless DalaMock grows
first-class equivalents:

- `Echoglossian.Mock/Hosting/EchoglossianAsyncPluginAdapter.cs`
  - bridges DalaMock's async plugin startup into Echoglossian's synchronous
    production entrypoint
  - assigns the static Dalamud services the real plugin expects
  - supplies headless-safe fallbacks for optional services that DalaMock may not
    provide in this flow
- `Echoglossian.Mock/Hosting/HeadlessPluginCleanup.cs`
  - prepares plugin shutdown for a headless rail that has no live native UI to
    restore
- `Echoglossian.Mock/Hosting/HostedPreviewPluginSessionFactory.cs`
  - seeds cloned preview-owned config and database state into the exact
    directory shape the hosted runtime resolves
- `Echoglossian.Mock/Hosting/HostedPreviewPluginSession.cs`
  - coordinates hosted disposal, plugin cleanup, and SQLite pool cleanup for
    repeatable local sessions
- `Echoglossian.Mock.Tests/AssemblyInfo.cs`
  - disables test parallelization because this rail touches static runtime state
    and SQLite/session resources
- `Echoglossian.csproj`
  - excludes `vendor/**` from normal plugin item discovery so local mock-host
    source never bleeds into plugin packaging

These are not proof that the plugin is "wrong". They are accommodations for
running a real Dalamud plugin inside a local headless host instead of inside the
game.

## Why Echoglossian Still Needs Local Seams

Even after the DalaMock fixes, Echoglossian still has traits that make hosted
preview more sensitive than a small test plugin:

- the real plugin entrypoint is synchronous
- many Dalamud services are stored in static fields
- config and database paths depend on `PluginInterface`-derived directories
- shutdown logic assumes native UI ownership unless the headless rail prepares
  for disposal

Those are hostability constraints, but they were not the root cause of the
original startup failure.

## Future Cleanup Candidates

None of these are required to keep the hosted preview working today, but they
would reduce local hosting glue over time:

- reduce static/global service ownership where a scoped runtime object would do
- expose a more explicit headless-safe cleanup seam instead of reflection-based
  state checks
- make path-sensitive services easier to redirect through explicit preview-owned
  inputs

## Practical Next Step

Treat the work in two tracks:

1. upstream the DalaMock compatibility fixes
2. keep Echoglossian's hosted preview adapter and disposal/state-isolation code
   local unless the upstream project grows equivalent abstractions

That keeps the previewer moving now without blocking on upstream release timing,
while still making it clear which fixes should not live forever in a vendored
copy.
