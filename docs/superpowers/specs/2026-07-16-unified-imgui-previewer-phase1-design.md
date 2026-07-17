# Unified ImGui Previewer Phase 1 Design

## Summary

This spec defines Phase 1 of a unified ImGui previewer for Echoglossian.
The goal is to evolve `Echoglossian.Previewer` from an overlay-only preview
tool into a single desktop preview workbench that can host:

- overlay scenarios using the real overlay renderer
- the real plugin configuration window
- the real DB manager window
- the real translator metrics / debugger window

All of those surfaces must run outside FFXIV, inside one preview app, from
cloned snapshots of the user's config and DB state, with screenshot and export
support for every supported surface.

Approved decisions from brainstorming:

- one preview app, not separate tools
- one shell-driven UI, not multiple previewer windows
- controlled startup scenarios by default
- real plugin window code, not preview-only reimplementations
- real config/DB snapshots cloned into preview-owned state
- screenshot/export support in Phase 1 for overlay and plugin windows
- preserve the current overlay RTL path and screenshot workflow
- do not use DalaMock in Phase 1

## Problem

The current previewer solves only part of the iteration loop. It already makes
overlay work much faster by previewing and capturing ImGui overlay surfaces
outside the game, but the rest of the plugin's ImGui surfaces still require the
normal runtime cycle:

1. build the plugin
2. launch the game
3. wait for Dalamud to load the plugin
4. open the correct plugin window or command path
5. inspect the result manually

That is still too slow for work on:

- config window layout and behavior
- DB manager iteration
- translator debugger iteration
- cross-window visual consistency
- screenshot capture for documentation or comparison

The repository already contains real ImGui surfaces that should be previewable
outside the game:

- `PluginUI/PluginUI.cs`
- `PluginUI/TranslatorMetricsWindow.cs`
- `DBManagerUI/DBEditorWindow.cs`
- `UIOverlays/TranslationOverlay/...`

The missing capability is not "draw more fake UI". The missing capability is a
single preview host that can run those real surfaces together against safe,
cloned preview state.

## Goals

1. Keep one development-only preview executable: `Echoglossian.Previewer`.
2. Preview overlay and plugin ImGui surfaces in the same app session.
3. Reuse real rendering and window code wherever practical.
4. Preserve the existing overlay fidelity boundary, including `PlainImGui` and
   `RtlTexture`.
5. Open plugin windows from cloned snapshots of user config and DB state.
6. Support screenshot/export for overlay and plugin windows.
7. Keep plugin build, packaging, deploy, and release behavior unchanged.
8. Prefer narrow extractions and adapters over broad plugin-runtime refactors.

## Non-Goals

- no preview of native FFXIV UI surfaces
- no `AtkTextNode`, `AtkValue`, or addon mutation preview
- no attempt to simulate the full FFXIV addon lifecycle
- no attempt to host the entire plugin runtime in the previewer
- no DalaMock adoption in Phase 1
- no change to release packaging or plugin artifact contents
- no requirement that preview edits write back to the user's live config or DB

## Constraints

### Build And Deploy Isolation

The previewer remains a development tool only.

Required isolation rules:

- the main plugin package continues to be produced only from
  `Echoglossian.csproj`
- `Echoglossian.Previewer` remains `IsPackable=false`
- previewer code and dependencies remain excluded from normal plugin packaging
- previewer dependencies must not be copied into the plugin artifact
- the previewer remains outside `Echoglossian.sln`
- the main plugin project continues to exclude previewer paths from recursive
  SDK item inclusion

### Fidelity Boundary

Phase 1 must preserve the existing overlay fidelity contract.

Shared with the live plugin:

- overlay renderer code
- overlay config inputs
- font selection behavior
- `PlainImGui` rendering path
- `RtlTexture` rendering path
- wrapping, spacing, opacity, and placement rules

Not reproduced:

- native FFXIV UI composition
- native addon geometry from the game
- game compositor, post-processing, HDR, or color management
- full plugin lifecycle and all Dalamud services

### Session Safety

The previewer must not operate directly on the user's live writable state.

Required rules:

- config is read from the selected source and cloned
- DB files are cloned before preview use
- preview mutations affect only the cloned session state
- preview saves never write back automatically to the user's live files
- any future export-back flow is outside Phase 1

## Chosen Approach

Phase 1 extends the existing `Echoglossian.Previewer` into a unified preview
workbench with one host and one shell.

The app keeps the current overlay host, renderer, and screenshot foundation.
It adds:

- a preview session layer that clones config and DB state
- a unified shell that controls overlay and plugin window visibility
- a reusable config window renderer extracted from the current plugin partial
  UI implementation
- plugin-window hosting for `DbEditorWindow` and `TranslatorMetricsWindow`
- deterministic screenshot targeting for full frame, overlay surface, and
  individual plugin windows

This approach intentionally does not adopt DalaMock in Phase 1. The current
plugin architecture is still centered around plugin-owned partial classes,
static service access, and `UiBuilder`-driven callbacks. Replatforming around a
generic mock runtime now would increase integration cost while providing little
benefit for the approved Phase 1 scope.

## Architecture

### 1. Unified Preview Host

The previewer remains a single Windows desktop app with one ImGui context and
one main frame loop. The existing host and rendering foundations remain the
owner of:

- process entry
- ImGui binding initialization
- Veldrid host lifecycle
- per-frame rendering
- screenshot capture

The previewer shell becomes a control surface, not a replacement renderer.

### 2. Preview Session

A preview session is the runtime container for all user-selected preview state.
It owns:

- the source config path
- the cloned editable config instance
- the source DB location
- the cloned DB files
- the active viewport
- selected overlay scenario
- per-session output directory
- preview-only temporary overrides

The session is the single source of truth for all previewed surfaces in a run.

### 3. Overlay Runtime

Overlay preview remains on the current path. It continues to use the real
shared overlay renderer and current font/runtime handling. This keeps the most
important fidelity-sensitive path stable.

### 4. Plugin Window Runtime

The plugin-window runtime hosts the real window implementations inside the same
ImGui context as the overlay preview.

Phase 1 hosts:

- the real `DbEditorWindow`
- the real `TranslatorMetricsWindow`
- a newly extracted reusable config window renderer using the current config UI
  code paths

The shell controls whether those windows are opened, focused, and included in
targeted capture.

### 5. Screenshot Coordinator

Phase 1 capture is expanded into three capture types:

- `full-frame`: capture the whole preview frame
- `overlay-surface`: capture the relevant overlay region
- `window-target`: capture a named plugin window after deterministic layout and
  frame stabilization

Every exported image is accompanied by manifest metadata describing the inputs
used to produce it.

## Components

### Preview Shell

The shell remains the main control panel and grows into the top-level
orchestrator for:

- overlay scenario selection
- viewport selection
- plugin window toggles
- session source inspection
- preview-only font and styling overrides
- export and screenshot commands
- layout reset / deterministic capture preparation

The shell should stay visually separate from the previewed windows so it does
not become confused with plugin UI.

### Config Window Renderer Extraction

The current config UI is still embedded in the plugin partial class flow,
especially `PluginUI/PluginUI.cs` and `PluginUI/PluginRuntimeUi.cs`.

Phase 1 extracts the actual window drawing logic into a reusable renderer that:

- consumes a `Config` instance and explicit runtime helpers
- can be called from the live plugin and from the previewer
- preserves existing tabs, footer behavior, and save semantics
- keeps save effects directed at the preview session when used in preview mode

This is the main structural extraction in Phase 1.

### DB Manager Hosting

`DBManagerUI/DBEditorWindow.cs` is already close to preview-hostable shape
because it is a real window class that accepts `EchoglossianDbContext`.

Phase 1 hosts it against the cloned preview DB and keeps all edits scoped to
the preview session copy.

### Translator Metrics / Debugger Hosting

`PluginUI/TranslatorMetricsWindow.cs` is also already close to preview-hostable
shape because it accepts a `Config` instance and delegates for runtime actions.

Phase 1 provides preview-safe delegates and predictable diagnostics state so
the window can render meaningfully without a live game session.

### Snapshot / Clone Layer

The snapshot layer is responsible for:

- locating the requested user config source
- locating the requested DB source
- cloning both into preview-owned working files
- exposing preview-safe paths and live objects to the rest of the app
- cleaning up temporary session data when appropriate

This layer must be safe to rerun and must tolerate missing optional sources.

## Data Flow

### Startup Flow

1. The previewer parses CLI arguments and resolves the initial preview mode.
2. The previewer locates config and DB sources.
3. The previewer clones those sources into a preview session workspace.
4. The previewer creates the editable runtime objects from the cloned state.
5. The previewer boots the overlay runtime and plugin-window runtime.
6. The shell opens with controlled initial scenarios and window visibility.

### Interactive Flow

1. The shell updates preview state such as scenario, viewport, or window
   selection.
2. Overlay rendering reads from session-owned config and scenario state.
3. Plugin windows read from session-owned config or DB state.
4. Any save or edit action writes only to preview-owned session files.
5. Export actions snapshot the requested target plus metadata.

### Screenshot Flow

1. The user chooses a target: frame, overlay surface, or plugin window.
2. The previewer applies deterministic layout rules for that target.
3. The previewer renders enough frames to stabilize layout-dependent surfaces.
4. The previewer captures the image region.
5. The previewer writes a manifest containing target type, scenario, viewport,
   relevant config source metadata, font metadata, and output paths.

## Error Handling

Phase 1 should fail soft wherever possible.

### Missing Config

If the selected config file is missing or malformed:

- the previewer uses a new default `Config`
- the shell reports the fallback clearly
- the session remains usable

### Missing DB

If the DB source is missing:

- overlay and config preview remain available
- DB manager and DB-dependent debugger affordances are disabled or shown with a
  clear unavailable-state explanation

### Snapshot Failure

If cloning the config or DB fails:

- the previewer reports which source failed
- unaffected surfaces remain usable where practical
- the previewer never falls back to editing the live source directly

### Window-Specific Dependency Gaps

If a specific real window still depends on a live plugin-only service:

- the previewer supplies an explicit unavailable-state message for that action
- the rest of the window should still render when possible
- the fix should prefer small adapters over broad runtime emulation

### Screenshot Failure

If export fails:

- the previewer reports the failed target and output path
- the app remains open
- partial output must not be presented as successful

## Testing Strategy

### Automated Validation

Phase 1 should add or extend tests for:

- session config cloning behavior
- DB snapshot cloning behavior
- preview-only save isolation
- config renderer extraction contracts
- plugin window host visibility and targeting behavior
- deterministic screenshot naming and manifest content
- capture targeting for plugin windows
- isolation checks proving previewer work stays out of plugin packaging

### Manual Validation

Manual validation remains required for fidelity-sensitive UI.

Required checks:

- compare at least one `PlainImGui` overlay scenario in preview vs live plugin
- compare at least one `RtlTexture` overlay scenario in preview vs live plugin
- open `Config`, `DB Manager`, and `Translator Metrics/Debugger` in preview and
  confirm they match the live window structure for the same config or DB inputs
- verify edits in preview do not alter the live config or DB files
- verify screenshot/export works for overlay and for each plugin window target

### Baseline Commands

At minimum, Phase 1 implementation must continue validating the repository with:

- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`

Previewer-specific restore, build, test, and smoke commands should also be kept
documented and runnable independently from the main solution.

## Risks

### Config Window Extraction Risk

This is the biggest structural risk in Phase 1 because the config window is not
yet a standalone class. The extraction must preserve behavior while moving only
the minimum amount of code needed for reuse.

### Hidden Runtime Coupling

Some config or debugger actions may still implicitly depend on plugin-owned
state, static stores, or runtime services. Those should be surfaced and adapted
incrementally rather than solved with a full preview-runtime rewrite.

### Screenshot Determinism

Window-target capture is more sensitive to layout drift than the current
overlay-only crop path. Phase 1 must add deterministic placement rules before
claiming stable window screenshots.

## DalaMock Status

DalaMock is explicitly out of Phase 1 scope.

Reasoning:

- the approved Phase 1 scope does not require a generic mock plugin host
- the current Echoglossian architecture is not yet a clean fit for a
  DalaMock-first runtime
- using DalaMock now would likely increase the amount of extraction work before
  user-visible value appears

Future adoption remains possible if a later phase benefits from broader mocked
Dalamud-service coverage. Phase 1 should preserve small seams where that could
be introduced later, but it should not pay that integration cost in advance.

## Phase 1 Deliverable

Implemented Phase 1 behavior keeps the previewer outside the plugin solution
and package, hosts all approved ImGui surfaces in the unified shell, and uses
preview-owned config and DB snapshots. Window-target capture is available from
the interactive shell; batch CLI export continues to cover overlay scenarios.

Phase 1 is complete when all of the following are true:

- `Echoglossian.Previewer` remains isolated from plugin packaging
- one preview app hosts overlay plus `Config`, `DB Manager`, and
  `Translator Metrics/Debugger`
- overlay preview still supports both `PlainImGui` and `RtlTexture`
- plugin windows use real code paths, not preview-only facsimiles
- preview state comes from cloned config and DB snapshots
- preview edits remain isolated from live user files
- screenshot/export works for overlay and plugin windows
- the repo validation path still passes
