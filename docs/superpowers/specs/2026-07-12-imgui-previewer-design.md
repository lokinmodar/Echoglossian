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

This design is being documented on branch
`issue-174-dialogue-retranslation-semantics`, which already contains active UI
and debugger work.

Implementation must **not** start directly from the assumptions in this spec
without first re-reviewing the post-merge state.

Required execution gate:

1. merge the current issue branch into `v4-series` first
2. re-audit the latest UI-related files and active branch state
3. update this spec and the eventual implementation plan if the underlying UI
   architecture changed
4. only then start implementation work

This is intentionally a design-first, deferred-execution document.

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

Expected runtime boundaries:

- `IUiFontRuntime`
- `IUiTextureRuntime`
- `IUiActionRuntime`
- `IAssetsStateProvider`
- `IOverlayPreviewContext`

These interfaces should stay small and UI-oriented. They are not meant to
generalize the whole plugin runtime.

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

Because this work is intentionally deferred until after the current branch
merges, the underlying UI code may change before implementation starts.

Mitigation:

- require the execution gate described above
- revise this spec and the later plan before coding starts

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

## Deliverables For The Deferred Implementation

- design-approved preview architecture
- dev-only `Echoglossian.Previewer` project
- shared renderer extraction for the targeted ImGui surfaces
- preview runtime adapters
- overlay scenario presets
- screenshot export support
- updated docs that describe how to run and re-review the previewer workflow
