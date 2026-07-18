# Hybrid Previewer DalaMock Font Backend Design

## Status

Approved in discussion on 2026-07-17. Written spec pending final user review
before implementation planning.

## Summary

This spec defines the next previewer slice after the Phase 1 unified ImGui
previewer merged into `v4-series`.

The current previewer already works well for overlay surfaces and for real
plugin-window hosting through the standalone preview shell. The remaining
fidelity gap is that hosted plugin windows do not currently use the same
Dalamud-managed font-atlas build path that the live plugin uses. That matters
most for:

- `Config`
- `DB Manager`
- `Translator Metrics / Debugger`

The chosen direction is a hybrid architecture:

- keep one previewer shell
- keep the existing overlay preview pipeline unchanged
- add a selectable backend only for real plugin windows
- use DalaMock as an optional hosted runtime for those windows so they can use
  the real plugin's Dalamud font-atlas path more faithfully
- preserve automatic fallback to the current standalone backend when the hosted
  runtime is unavailable

This is not a DalaMock-first rewrite of the previewer. It is a targeted use of
DalaMock to improve font and window fidelity where the current standalone host
is weakest.

## Approved Decisions

The following decisions were made during brainstorming and are authoritative
for this spec:

- continue on a fresh dedicated worktree and branch based on the latest
  `v4-series`
- keep one previewer shell, not multiple windows or separate tools
- apply the new hosted path to real plugin windows only
- include all three real plugin windows in scope:
  - `Config`
  - `DB Manager`
  - `Translator Metrics / Debugger`
- allow interaction with cloned session state, not live user state
- expose backend choice both in CLI and in the previewer shell
- default behavior should try the hosted backend and fall back automatically
- fallback must remain visible and diagnosable, not silent

## Problem

The current previewer uses two very different font/runtime paths:

1. the live plugin path:
   - `UINewFontHandler`
   - `PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(...)`
   - `PluginInterface.UiBuilder.FontAtlas.BuildFontsAsync()`
   - `DalamudUiFontRuntime`

2. the standalone preview path:
   - `PreviewFontRuntime`
   - direct `ImGuiIO.Fonts.AddFontFromFileTTF(...)`
   - direct atlas build inside the preview host

That difference is acceptable for overlay scenarios because the previewer
already owns that fidelity boundary explicitly and the overlay pipeline is
designed around it.

It is weaker for hosted plugin windows because those windows were written under
the assumption that Dalamud owns font handles, atlas rebuilds, and runtime font
push/pop behavior. The previewer currently adapts those windows into a
standalone shell, but it does not reproduce the real Dalamud font-atlas
ownership model for them.

The result is:

- hosted windows may not match the live plugin's font build behavior exactly
- language/font changes in preview are only partially representative
- window layout differences caused by atlas composition are harder to trust
- screenshots of plugin windows can still drift from what the user would see in
  a real Dalamud-hosted plugin session

## Goal

Improve fidelity for real plugin windows by letting them use a hosted runtime
path that is closer to the live plugin's Dalamud font-atlas behavior, while
preserving the existing previewer shell and overlay workflow.

## Non-Goals

- no preview of native game UI or native addon mutation
- no conversion of the entire previewer to a DalaMock-first architecture
- no removal of the current standalone plugin-window backend
- no change to plugin packaging or release flows
- no broad plugin dependency-injection rewrite
- no automatic write-back from preview session clones into the user's live
  config or DB
- no promise that overlay rendering will move to DalaMock in this slice

## Constraints

### Repository And Packaging

- keep the previewer outside `Echoglossian.sln`
- keep previewer dependencies out of normal plugin packaging
- keep `Echoglossian.Mock` and `Echoglossian.Mock.Tests` as local-only
  development/runtime complements
- preserve the current plugin entrypoint and release path

### Runtime Safety

- all preview edits continue to target cloned session config and cloned session
  DB state only
- network or provider actions remain blocked in preview mode
- any runtime service that cannot be provided safely under the hosted backend
  must be stubbed, redirected, or cause a backend fallback

### UX

- one shell remains the only user-facing preview control surface
- backend choice must be visible in both CLI and shell
- fallback cannot silently degrade fidelity

## Options Considered

### Option 1: Improve The Standalone Font Runtime Only

This would keep the current preview architecture and just push
`PreviewFontRuntime` closer to the plugin's font stack.

Pros:

- smallest patch
- no hosted runtime coordination

Cons:

- still does not exercise the real `UiBuilder.FontAtlas` path
- still leaves plugin windows on a preview-owned runtime instead of a more
  faithful Dalamud-like host
- likely solves only part of the fidelity gap

### Option 2: Hybrid Backend For Real Plugin Windows

This keeps the current overlay preview pipeline and current shell, but adds a
new backend only for real plugin windows.

Pros:

- targets the specific gap without destabilizing overlay preview
- keeps one shell and one user workflow
- allows automatic fallback
- uses DalaMock where it adds value instead of making it the whole system

Cons:

- adds backend-selection complexity
- requires a host/runtime abstraction for plugin windows
- needs explicit handling for hosted-runtime startup, failure, and cleanup

### Option 3: Convert The Entire Previewer To DalaMock-First

This would make DalaMock the primary host for overlays and plugin windows.

Pros:

- single hosted runtime model

Cons:

- highest rewrite cost
- highest risk to the already-good overlay workflow
- would likely couple the previewer too tightly to DalaMock lifecycle details

## Selected Approach

Select **Option 2: Hybrid backend for real plugin windows**.

This gives the best tradeoff between fidelity and stability:

- overlays stay on the current proven preview path
- real plugin windows gain a more faithful hosted runtime option
- the shell stays unified
- the current standalone backend remains available as fallback and comparison

## Architecture

### 1. One Shell, Two Domains

The previewer remains one desktop application with one shell. That shell
continues to own:

- scenario controls
- viewport controls
- window toggles
- screenshot actions
- fidelity diagnostics
- backend selection UI

The shell must treat preview work as two separate rendering domains:

1. overlay preview domain
2. plugin-window preview domain

The overlay domain stays on the current path. The plugin-window domain becomes
backend-selectable.

### 2. Plugin-Window Backend Abstraction

Introduce a narrow backend contract for hosted plugin windows. The concrete
name can change, but the responsibility split should be:

- initialize a backend over preview-owned session state
- draw `Config`
- draw `DB Manager`
- draw `Translator Metrics / Debugger`
- report stable bounds for screenshot cropping
- report effective mode and diagnostics
- clean up hosted resources

Expected concrete backends:

- `StandalonePluginWindowPreviewBackend`
- `DalaMockHostedPluginWindowPreviewBackend`

The shell talks only to this abstraction.

### 3. Backend Modes

Add an explicit backend mode enum for plugin windows:

- `Auto`
- `Standalone`
- `DalaMockHosted`

Rules:

- `Auto` tries hosted first, then falls back to standalone
- `Standalone` always uses the current preview-owned window host
- `DalaMockHosted` requires hosted startup to succeed and must not degrade
  silently

The backend mode must be available:

- through CLI arguments
- through shell controls

The shell also tracks an effective mode after startup:

- requested mode
- effective mode
- fallback reason, if any

## Hosted DalaMock Runtime Design

### Scope Of Hosted Runtime

The DalaMock-backed host exists only to improve fidelity for real plugin
windows. It is not the new owner of the entire previewer.

It should provide:

- plugin construction under DalaMock
- access to real plugin-window objects or equivalent real plugin rendering
  paths
- Dalamud-like font-atlas ownership for those plugin windows
- lifecycle and service seams already proven by `Echoglossian.Mock` and
  `Echoglossian.Mock.Tests`

### Session Ownership

The hosted backend must run only against preview-session clones:

- cloned config path
- cloned DB path
- preview-owned local state root

Any write performed by the hosted backend is acceptable only if it stays inside
those clones.

### Safe Interaction Model

The hosted backend should allow:

- editing preview-owned config
- editing preview-owned DB data through DB Manager
- viewing translator metrics/debugger state that can be hosted safely

The hosted backend must still block or neutralize:

- provider/network actions
- mutations of live shared state outside the preview session
- runtime behaviors that depend on the real game lifecycle or unavailable
  services

If a required service cannot be provided safely, the backend should:

- fail cleanly during hosted initialization, or
- surface the action as unavailable inside the hosted session

Which behavior is chosen depends on whether the missing service is essential to
window startup or only to a narrow runtime-owned action.

## Font Fidelity Contract

The core value of the hosted backend is improved font fidelity for real plugin
windows.

The intended fidelity gain is:

- the hosted plugin-window path uses the real plugin's Dalamud-style font
  atlas ownership model
- real plugin-window font handles are used where practical
- rebuilds triggered by language/font changes follow the hosted runtime path
  rather than only the previewer's direct standalone atlas path

This does **not** mean the previewer now promises total equivalence with the
live game or all Dalamud rendering environments. It means the hosted backend is
a more trustworthy approximation specifically for plugin-window font behavior.

## Fallback Contract

Fallback behavior must be explicit.

### `Auto`

When `Auto` is selected:

1. try `DalaMockHosted`
2. if hosted initialization succeeds, use it
3. if hosted initialization fails, record the reason and use `Standalone`

### `Standalone`

Always use the standalone backend.

### `DalaMockHosted`

When hosted mode is selected explicitly:

- do not fall back silently
- mark the hosted plugin windows unavailable if startup fails
- surface the failure reason in the shell and diagnostics

## Restart And Reapply Behavior

The current previewer already warns when language or font-size changes require
restart for the preview-owned runtime. That concept should remain, but the
plugin-window backend now owns an additional runtime boundary.

Rules:

- changing plugin-window backend mode restarts only the plugin-window backend,
  not the entire preview session
- hosted backend may attempt a real runtime font rebuild when safe
- if the hosted path cannot reapply a language/font change safely, the shell
  must show that a backend restart is required
- overlays remain independent of hosted plugin-window backend restart rules

## Screenshot And Manifest Behavior

The screenshot pipeline remains centralized in the previewer. Plugin-window
backends only provide stable bounds and hosted-window availability.

Manifest metadata must expand for plugin-window captures to include:

- requested backend mode
- effective backend mode
- fallback reason when applicable
- capture target

This keeps fidelity claims honest in exported artifacts.

## Component-Level Changes

The exact file names can change, but the implementation should be organized
around these responsibilities.

### Previewer Shell

Likely touches:

- `Echoglossian.Previewer/Program.cs`
- `Echoglossian.Previewer/UI/PreviewShell.cs`
- `Echoglossian.Previewer/UI/PreviewPluginWindowHost.cs`
- CLI parsing and shell-state types

Add:

- plugin-window backend selection
- effective backend reporting
- fallback diagnostics

### Standalone Backend Extraction

Refactor the current plugin-window host into an explicit standalone backend
instead of leaving it embedded as the only path.

This should preserve current behavior as closely as possible so that:

- fallback is safe
- current screenshots keep working
- existing previewer tests can continue to validate the baseline path

### Hosted DalaMock Backend

Build a hosted backend on top of the existing local DalaMock rail:

- `Echoglossian.Mock`
- `Echoglossian.Mock.Tests`

The goal is to reuse proven startup/runtime seams instead of inventing a second
independent hosted bootstrap inside the previewer.

### Diagnostics

Add visible diagnostics in the shell so a user can tell:

- whether the hosted backend was requested
- whether it actually initialized
- which backend produced the current window view
- why fallback happened, when it did

## Testing Strategy

The next implementation slice should add targeted tests, not a broad rewrite of
existing suites.

Minimum required coverage:

1. backend-mode selection and fallback rules
2. manifest metadata for requested/effective backend
3. standalone backend behavior remains intact
4. hosted backend startup path over preview-owned cloned state
5. at least one hosted-backend smoke assertion that real plugin windows become
   available without touching live user state

Validation command set should continue to include:

```powershell
dotnet build Echoglossian.sln -c Debug --no-restore
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --no-restore
dotnet restore Echoglossian.Mock\Echoglossian.Mock.csproj
dotnet restore Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj
dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build
```

## Risks

### 1. DalaMock UI Loop Mismatch

The DalaMock host already has its own UI/runtime concepts. Trying to embed that
entire UI loop directly into the previewer's current window would likely create
fragile ownership and timing issues.

Mitigation:

- do not make `MockUi.Run()` the previewer's main shell
- use DalaMock to host runtime/services, not to replace the preview shell

### 2. Hosted Runtime Drift

The hosted backend may still need preview-safe stubs or adapters for some
runtime actions.

Mitigation:

- keep hosted scope narrow
- keep fallback explicit
- keep overlay preview independent

### 3. Over-Promising Fidelity

Users may interpret "hosted backend" as "exactly what Dalamud in-game would
render."

Mitigation:

- show effective backend in the shell
- carry backend metadata into screenshot manifests
- document that this improves plugin-window font fidelity, not native game UI
  fidelity

## Success Criteria

This design is successful when the next implementation can deliver all of the
following without breaking the current preview workflow:

- one previewer shell still hosts overlays and real plugin windows together
- `Config`, `DB Manager`, and `Translator Metrics / Debugger` can be rendered
  through either standalone or hosted backends
- both CLI and shell can choose backend mode
- `Auto` falls back to standalone safely and visibly
- hosted backend uses preview-owned cloned state only
- screenshots and manifests report which backend actually produced the capture
- overlays remain on the current preview pipeline unless explicitly changed in a
  future phase

## Out Of Scope For This Slice

- replacing the overlay runtime with DalaMock
- a full migration to hosted-plugin architecture
- live synchronization from preview session back into user files
- native game UI preview
- any release/deploy flow changes
