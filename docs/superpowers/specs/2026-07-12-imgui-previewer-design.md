# ImGui Previewer Design

## Summary

This spec defines a development-only ImGui preview workflow for Echoglossian.
The goal is to preview and capture screenshots of the plugin's ImGui surfaces
without building, loading, and opening the plugin inside FFXIV.

Approved scope:

- plugin configuration UI
- translator metrics / debugger UI
- DB editor UI
- ImGui overlay surfaces such as `Talk`, `BattleTalk`, `TalkSubtitle`,
  `MiniTalk`, `CutSceneSelectString`, and toast-family overlays

Out of scope:

- native FFXIV UI mutation previews
- `AtkTextNode` / `AtkValue` previews
- non-ImGui game UI rendering

The chosen solution is a desktop C# preview host that reuses the same ImGui
rendering code paths as the plugin wherever practical, while supplying a
preview runtime for config, fonts, textures, assets state, viewport geometry,
and overlay bounds.

## Problem

Today, validating an ImGui surface generally requires the full runtime loop:

1. build the plugin
2. start the game
3. wait for Dalamud to load the plugin
4. open the right game state or addon state
5. inspect the resulting UI manually

That is slow for iteration, especially when the work is only about:

- layout
- wrapping
- font behavior
- spacing
- overlay sizing
- overlay positioning
- screenshots for comparison or documentation

The repository already contains meaningful ImGui-only logic in:

- `PluginUI/PluginUI.cs`
- `PluginUI/TranslatorMetricsWindow.cs`
- `DBManagerUI/DBEditorWindow.cs`
- `UIOverlays/TranslationOverlay/TranslationOverlayDrawer.cs`

Those surfaces should be previewable outside the game as long as the previewer
can provide equivalent ImGui runtime inputs.

## Goals

1. Preview Echoglossian ImGui surfaces outside FFXIV.
2. Reuse real rendering code instead of maintaining a second UI
   implementation.
3. Preserve high fidelity for fonts, wrapping, spacing, scaling, and overlay
   sizing.
4. Support screenshots from the preview environment.
5. Keep plugin build, packaging, and release behavior unchanged.
6. Keep implementation risk low by limiting refactors to UI/runtime boundaries.

## Non-Goals

- no preview of native game UI surfaces
- no attempt to simulate full FFXIV addon lifecycle
- no change to translation business logic as part of the preview feature
- no release-time dependency on a new preview assembly
- no web-first or browser-first implementation in the initial slice
- no attempt to guarantee pixel-identical native game geometry outside the game

## Constraints

### Branch Execution Gate

The deferred execution gate was completed on 2026-07-15.

- the prior UI work and this design are merged into `v4-series`
- the implementation branch `issue-215-imgui-previewer` starts from
  `origin/v4-series` commit `af6e1f70fae0a0c747e1956860237f327b940cfa`
- the current overlay, font, RTL texture, configuration, metrics, and DB editor
  paths were re-audited before implementation planning
- this spec and the implementation plan were revised from that post-merge
  state

Implementation can proceed on the dedicated issue branch. Any later phase
must still re-audit the surface it extracts because configuration, metrics,
and DB editor work will land after the overlay foundation.

### Build / Deploy Isolation

The previewer must behave like a development tool, not like a plugin runtime
dependency.

Required isolation rules:

- the main plugin package continues to be produced only from
  `Echoglossian.csproj`
- the previewer must be `IsPackable=false`
- the previewer must not be included in release packaging flows
- the previewer must not require a new runtime dependency to ship with the
  plugin artifact
- the previewer should be kept out of the main solution file, mirroring the
  repo's current loose coupling for `Echoglossian.Tests`
- the main plugin project must explicitly remove previewer source and resources
  from its recursive SDK item discovery

### Binding And Backend Compatibility

Dalamud's source tree contains `imgui/StandaloneImGuiTestbed`, which proves
that `Dalamud.Bindings.ImGui` can run in a normal Windows process. The binding
loads `cimgui.dll` from the calling assembly's output directory, so the
previewer must copy the current Dalamud development binding, its runtime
dependency, and `cimgui.dll` into its own output without changing the plugin
artifact.

The testbed is an architectural reference, not code to copy. Dalamud is AGPL,
while this repository has a different license. The host renderer should instead
adapt the MIT-licensed Veldrid ImGui backend and retain its notice. Preview-only
Veldrid package dependencies must remain isolated to the preview project.

### Current Rendering Modes

The post-merge overlay renderer has two text presentation paths:

- `PlainImGui`, which uses the active ImGui font atlas
- `RtlTexture`, which rasterizes text through `TextImageRenderer` and presents
  the result as an ImGui texture

The previewer must exercise both paths. A preview that silently replaces RTL
texture rendering with plain text is not considered faithful.

## Options Considered

### Option A: Desktop C# preview host reusing shared renderers

Create a standalone preview app that hosts ImGui in a normal desktop window
and calls the same renderer code used by the plugin.

Pros:

- best fidelity for ImGui surfaces
- straightforward screenshot support
- strongest code reuse
- no need to recreate layouts in another UI technology

Cons:

- requires extracting some runtime dependencies away from current static and
  plugin-owned state
- overlay positioning still needs simulated geometry outside the game

### Option B: Native renderer with optional web control surface

Keep rendering native, but provide a browser or local web panel to change
presets, sample text, viewport, and screenshot scenarios.

Pros:

- can remain high fidelity
- more convenient control surface

Cons:

- more moving parts
- unnecessary complexity for the first slice

### Option C: Browser-only ImGui explorer

Recreate the surfaces in a browser workflow similar to generic ImGui explorers.

Pros:

- easy to share
- attractive as a general UX tool

Cons:

- does not reuse the exact Echoglossian runtime stack
- risks drift in fonts, spacing, wrapping, and viewport behavior
- weakest path for the user's `1:1 if possible` goal

## Chosen Approach

Choose Option A.

Echoglossian already draws directly with `Dalamud.Bindings.ImGui`,
`ImRaii`, custom font handles, and custom overlay layout logic. A desktop host
that reuses those renderers is the only approach that has a realistic path to
high-fidelity previews without building a parallel UI stack.

The previewer should therefore be a developer-only desktop application that:

- loads real or sample `Config` data
- creates a preview runtime for fonts, textures, links, notifications, and
  assets state
- simulates overlay viewport and bounds inputs
- calls shared ImGui renderer code
- exports screenshots

The desktop host uses `Dalamud.Bindings.ImGui` with Veldrid, SDL2, and a
Veldrid texture registry. This keeps the ImGui API identical to the plugin
while changing only the platform and graphics backend.

## Proposed Design

### 1. Add a dev-only preview project

Add a new project such as `Echoglossian.Previewer`.

Rules:

- `IsPackable=false`
- not added to `Echoglossian.sln`
- optional separate solution such as `Echoglossian.Previewer.sln`
- references the main plugin project
- acts only as a developer utility

This keeps the plugin package and release path unchanged.

### 2. Extract shared ImGui renderers inside the main plugin assembly

Do not create a new shared runtime DLL in the first slice.

Instead, move the minimum necessary ImGui drawing code into renderer classes
that live inside the existing `Echoglossian` assembly and can be called by both
the plugin runtime and the previewer.

Initial renderer candidates:

- `PluginConfigWindowRenderer`
- `TranslatorMetricsWindowRenderer`
- `DbEditorWindowRenderer`
- `TranslationOverlayRenderer`

The plugin remains the owner of real runtime orchestration. The previewer
becomes another caller of the same draw-oriented code.

### 3. Introduce small runtime interfaces

The current UI code mixes draw logic with access to plugin-owned or static
runtime state. The previewer needs narrow abstractions so it can substitute its
own runtime safely.

Expected runtime boundaries across all phases:

- `IUiFontRuntime`
- `IUiTextureRuntime`
- `IUiActionRuntime`
- `IAssetsStateProvider`
- `IOverlayPreviewContext`

These interfaces should stay small and UI-oriented. They are not meant to
generalize the whole plugin runtime.

The first phase should introduce only the boundaries required by overlays,
starting with `IUiFontRuntime` and the existing RTL texture creation seam.
Other interfaces are added only when configuration, metrics, or DB editor
extraction demonstrates a concrete need.

Examples of what they cover:

- font handles and rebuild hooks
- image / texture handles
- external actions such as opening a URL
- assets downloaded / missing state
- viewport size, overlay anchor, and simulated addon bounds

### 4. Reuse real models and config where possible

Prefer using existing models directly:

- `Config`
- `TranslationOverlay`
- `TranslationWindowConfig`

That keeps preview behavior aligned with the plugin's actual configuration and
surface contracts.

The previewer should support:

- load current user config
- load sample config presets
- edit preview-only scenario inputs without mutating the user's live config

### 5. Simulate overlay geometry explicitly

For overlays, fidelity depends on more than text and font scale. Current
overlay draw code uses viewport size and overlay dimensions / positions derived
from game UI geometry.

Outside the game, the previewer should provide scenario presets that define:

- preview viewport resolution
- addon bounds
- anchor mode
- width / height multipliers
- sample text and optional speaker name

This will not recreate native game UI. It will recreate the geometry inputs
that the ImGui overlay renderer expects, which is enough for high-quality
ImGui-surface preview.

### 6. Support screenshots as a first-class feature

The preview host should be able to:

- capture the full visible viewport to `PNG`
- capture only the currently selected surface
- optionally batch-export multiple presets

This is one of the main workflow wins for UI iteration and documentation.

### 7. Start with the highest-value surfaces first

Recommended implementation order:

1. `TranslationOverlayRenderer`
2. overlay preview host and scenario presets
3. plugin config window renderer
4. translator metrics window renderer
5. DB editor renderer
6. batch screenshot / compare workflows

This order favors the surfaces that benefit most from fast visual iteration and
that already have concentrated layout logic.

### 8. Deliver the approved scope in phases

The approved objective remains previewing every Echoglossian-owned ImGui
surface, but it should not be delivered as one release-blocking refactor.

Phase A establishes the reusable foundation:

- standalone Windows host using the real Dalamud ImGui binding
- shared translation overlay renderer
- all currently registered translation overlay surface scenarios
- real user configuration loading without modifying the source file
- matching font files and font sizes
- `PlainImGui` and `RtlTexture` presentation
- full-frame, selected-surface, and batch PNG screenshots

Phase B adds the plugin configuration window after extracting its static font,
asset, save, notification, and external-action dependencies.

Phase C adds the translator metrics/debugger and DB editor with deterministic
sample data and a temporary or read-only database. It must never modify the
user's live plugin database.

Each phase builds on the same host and screenshot infrastructure. Issue #215
remains the umbrella tracker until all approved ImGui surfaces are covered.

## Risks

### Runtime Coupling Risk

Some draw paths currently depend on static state or Dalamud-owned services more
deeply than is ideal. The real effort may be larger than it first appears.

Mitigation:

- extract only narrow UI-facing interfaces
- avoid touching translation business logic
- stage the work surface by surface

### Font Fidelity Risk

The preview is only valuable if font behavior matches plugin behavior closely.
Current overlay sizing relies on font selection and `ImGui.CalcTextSize(...)`
under the effective render font and scale.

Mitigation:

- treat shared font runtime as a first-class requirement
- verify wrapping and width calculations against the in-plugin render

### Spec Drift Risk

The initial post-merge audit is complete, but later UI phases may start after
additional changes to their target surfaces.

Mitigation:

- re-audit each target surface before its phase starts
- revise phase plans when the real runtime boundary has changed

## Validation Expectations

When implementation eventually starts, validation should include:

- plugin build still succeeds:
  `dotnet build Echoglossian.sln -c Debug --no-restore`
- plugin tests still succeed:
  `dotnet test Echoglossian.Tests\\Echoglossian.Tests.csproj -c Debug --no-build`
- previewer builds independently
- screenshots verify the expected surface layout for representative presets
- at least one in-plugin visual comparison is performed for the same config and
  overlay scenario

## Phase A Delivered State

Phase A delivered the development-only overlay preview foundation described by
this spec:

- `Echoglossian.Previewer` and `Echoglossian.Previewer.Tests` remain outside
  `Echoglossian.sln`.
- Veldrid, SDL2, preview native binaries, and screenshot tooling are isolated to
  the preview projects.
- The main plugin project excludes previewer sources and resources from SDK item
  discovery and has no Veldrid package references.
- The standalone Windows host uses the dev Dalamud ImGui binding from
  `%APPDATA%\XIVLauncher\addon\Hooks\dev`, with optional `DalamudLibPath`
  override.
- The shared translation overlay renderer is callable from both the plugin and
  previewer.
- The previewer loads `Echoglossian.json` read-only, clones it for preview use,
  and falls back to defaults with redacted diagnostics.
- Font selection, logical viewport inputs, overlay layout, wrapping, placement,
  `PlainImGui`, and `RtlTexture` rendering paths are exercised in the previewer.
- The interactive shell covers the 12 currently registered runtime translation
  overlay surfaces.
- Full-frame, selected-surface, and batch PNG export are implemented with a
  sidecar manifest.

The fidelity boundary is explicit: plugin ImGui renderer code, config, fonts,
viewport inputs, layout, and RTL rasterization are shared. Native FFXIV UI,
native addon lifecycle, game compositor color management, real addon geometry,
animation, z-order, and occlusion are not reproduced.

Automated validation can prove build isolation, process launch, rendering
mechanics, and screenshot capture. It cannot prove 1:1 game fidelity. Before
merge or use as a fidelity baseline, a developer must compare at least one
`PlainImGui` scenario and one `RtlTexture` scenario in-game against preview
screenshots using the same `Echoglossian.json`, logical viewport, text, font
size, and simulated addon bounds. Differences caused by real addon geometry or
game compositor behavior must be recorded separately from renderer regressions.

## Deliverables

- revised, post-merge preview architecture
- dev-only `Echoglossian.Previewer` project
- shared renderer extraction for each targeted ImGui surface
- preview runtime adapters introduced only at demonstrated seams
- overlay scenario presets and deterministic later-phase sample data
- full-frame, selected-surface, and batch screenshot export
- updated docs that describe how to run, validate, and re-review the previewer
  workflow
