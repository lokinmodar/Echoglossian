# Issue 139 RTL ImGui Presentation Architecture Design

## Summary

This spec defines the architecture for resolving GitHub issue `#139` by
unlocking right-to-left languages across Echoglossian's ImGui-owned
presentation surfaces without waiting for full upstream RTL support in Dear
ImGui or Dalamud.

The approved roadmap has three tracks:

1. `Phase A` in the plugin:
   introduce a presentation seam and ship a texture-backed RTL renderer for
   overlays, tooltips, and other read-only ImGui surfaces.
2. `Phase B` in the plugin:
   research a plugin-local shaped-text backend for static text, still avoiding
   ownership of a full ImGui RTL fork.
3. `Phase B` upstream in Dalamud:
   propose a reusable complex-text service so plugins can eventually consume a
   framework-level solution instead of carrying their own static-text RTL
   stack.

This branch is `feature/issue-139-rtl-imgui-support`.

## Problem

Echoglossian already ships fonts and glyph ranges for Arabic- and
Hebrew-script languages, but it still blocks them from activation because the
current ImGui rendering path does not support complex text layout reliably.

Current repo facts:

- `PluginUI/PluginUI.cs` marks several RTL languages as
  `UnsupportedLanguage`.
- `GeneralHelpers/TranslationActivationGuard.cs` blocks activation when that
  flag is set.
- `UIOverlays/TranslationOverlay/TranslationOverlayDrawer.cs` renders with
  `ImGui.CalcTextSize`, `ImGui.TextWrapped`, and `ImGui.TextUnformatted`.
- `NativeUI/Helpers/HoverTooltipManager.cs` renders tooltips with plain
  `ImGui.TextUnformatted`.
- `NativeUI/Helpers/ActionItemDetailUiRuntime.cs` routes structured tooltip
  overlays through the same overlay drawer.
- `ImageGeneration/TextImageRenderer.cs`,
  `ImageGeneration/TextTextureGenerator.cs`, and
  `ImageGeneration/TextTextureCache.cs` already form a partial text-to-texture
  pipeline, but they are not integrated into live presentation.

External constraints also matter:

- Dear ImGui issue [#1228](https://github.com/ocornut/imgui/issues/1228)
  reflects that RTL was never a built-in goal.
- Dear ImGui issue [#4227](https://github.com/ocornut/imgui/issues/4227)
  shows that full RTL support required broad changes across shaping, bi-di,
  wrapping, input widgets, selection, cursor logic, and atlas behavior.
- Dear ImGui issue [#2365](https://github.com/ocornut/imgui/issues/2365)
  highlights the alternative of external text layout and rasterization.

The practical conclusion is that "fix unsupported language" and "teach ImGui
true RTL semantics" are different problems.

## Goals

1. Unblock RTL languages for player-facing translated ImGui surfaces.
2. Preserve existing translation, persistence, and overlay-mode behavior.
3. Keep the first shipped solution narrow, safe, and compatible with the
   current Dalamud version.
4. Introduce a stable presentation boundary so the backend can change later
   without rewriting overlay and tooltip flows again.
5. Avoid a plugin-local fork of Dear ImGui as the product path.

## Non-Goals

- no full RTL support for generic ImGui widgets in `Phase A`
- no bidi-aware `InputText`, selection, or caret ownership in the plugin
- no rewrite of native FFXIV UI mutation paths for RTL in this slice
- no DB schema redesign to persist bitmap payloads for every surface
- no per-frame text-to-image work in hot paths

## Options Considered

### Option A: Texture-backed RTL presentation for read-only surfaces

Render RTL text into a bitmap or texture and draw the result inside ImGui.

Pros:

- compatible with today's Dalamud and Dear ImGui
- matches the repository's existing WIP code
- solves the actual user-visible problem for overlays and tooltips
- isolates complex layout from ImGui's plain text APIs

Cons:

- not a general-purpose ImGui RTL solution
- requires cache, memory budget, and texture lifecycle discipline
- metrics must come from the rendered result, not `ImGui.CalcTextSize`

### Option B: Preprocess strings and keep using ImGui text APIs

Use libraries such as [RTLScript](https://github.com/oscar7070/RTLScript) or
[FarsiType](https://github.com/AmyrAhmady/FarsiType) to convert logical text
into a more display-friendly visual sequence, then draw it as plain ImGui
text.

Pros:

- smaller conceptual delta
- useful as a research spike or shaping prototype
- may help some mixed-text cases without generating textures

Cons:

- does not solve the full layout problem
- weak answer for wrapping, mixed bidi, numerals, punctuation, and metrics
- still leaves Echoglossian depending on ImGui's plain text measurement and
  wrapping logic

### Option C: Full plugin-local RTL stack over ImGui

Implement shaping, bi-di layout, wrapping, metrics, and drawlist output in the
plugin, possibly with HarfBuzz-like semantics.

Pros:

- strongest local control
- possible path toward richer text than a texture backend

Cons:

- very high maintenance burden
- drifts toward a local Dear ImGui text engine fork
- still does not justify owning editable widget RTL semantics in the plugin

## Chosen Approach

Choose Option A for product delivery, with Option B allowed only as a research
aid inside `Phase B`.

The key design decision is to separate `surface orchestration` from `text
presentation backend`.

The plugin should own one stable UI-facing contract:

- surfaces publish translated content
- a resolver chooses how the content must be presented
- the chosen backend returns a measured renderable block

That contract lets `Phase A` ship a texture backend now, lets `Phase B` test a
plugin-local shaped backend later, and lets an eventual Dalamud-level complex
text service slot in through an adapter instead of a rewrite.

## Proposed Design

### 1. Introduce a presentation seam

Add a small shared set of types for ImGui-owned translated text surfaces:

- `TextLayoutRequest`
- `RenderedTextBlock`
- `TextPresentationBackendKind`
- `ITextPresentationBackend`
- `TextPresentationResolver`
- `LanguagePresentationPolicy`

Responsibilities:

- `TextLayoutRequest` carries logical text, direction, width constraints,
  font identity, colors, and surface semantics.
- `RenderedTextBlock` returns measured size plus either plain lines or a
  texture handle.
- `ITextPresentationBackend` renders a request into a block.
- `TextPresentationResolver` picks the backend for a given request.
- `LanguagePresentationPolicy` decides whether the language must stay
  overlay-only and whether RTL presentation is required.

### 2. Keep the default LTR path unchanged

`PlainImGuiBackend` remains the default backend.

It keeps the existing behavior for:

- non-RTL languages
- surfaces that already behave correctly with the current font path
- fallback use when RTL presentation is not required

This preserves current behavior and keeps the patch narrow.

### 3. Ship `Phase A` through `RtlTextureBackend`

`RtlTextureBackend` becomes the first approved product backend for RTL
languages.

Pipeline:

1. logical translated text enters `TextLayoutRequest`
2. backend resolves wrap width and alignment
3. backend shapes and rasterizes via the existing image-generation path
4. backend obtains or reuses an `IDalamudTextureWrap`
5. backend returns a measured `RenderedTextBlock`
6. caller draws the texture and sizes the window from returned metrics

Integration points:

- `UIOverlays/TranslationOverlay/TranslationOverlayDrawer.cs`
- `NativeUI/Helpers/HoverTooltipManager.cs`
- `NativeUI/Helpers/ActionItemDetailUiRuntime.cs`

The backend must be used only for plugin-owned ImGui surfaces in `Phase A`.

### 4. Keep RTL languages overlay-only in `Phase A`

Shipping support does not require native FFXIV UI mutation for RTL.

`Phase A` rules:

- RTL languages stop being `UnsupportedLanguage`
- they remain `overlay-only`
- any display mode that currently implies native mutation must collapse to an
  overlay or tooltip presentation mode for RTL languages

This matches the current architecture and avoids pretending native addon text
mutation is solved.

### 5. Treat image persistence as non-authoritative

Do not redesign persistence around bitmap payloads in this slice.

Rules:

- translated text remains the source of truth
- text-to-texture output is a presentation artifact
- in-memory cache is preferred over DB-backed bitmap reuse
- existing `RTLLangTranslationImageData` fields stay non-authoritative unless a
  later phase proves a real need for persisted image reuse

### 6. Define the `Phase B` plugin backend as static-text only

`PluginShapedTextBackend` is the approved local R&D target after `Phase A`.

Scope:

- static read-only text only
- overlays
- tooltips
- debugger or config surfaces if needed later

Out of scope:

- `InputText`
- text editing
- cursor ownership
- selection semantics

This prevents the plugin from quietly becoming an ImGui fork.

### 7. Design for a future Dalamud adapter

The presentation seam must allow a fourth backend later:

- `DalamudComplexTextBackend`

That backend would consume a future Dalamud-level complex-text service for
static text. The surface callers should not need to know whether the result
comes from plugin-local texture rendering or framework-level layout services.

## Surface Coverage

`Phase A` coverage target:

- `Talk`
- `BattleTalk`
- `TalkSubtitle`
- `MiniTalk`
- `CutSceneSelectString`
- toast-family overlays
- hover tooltips
- structured tooltip overlays

Deferred:

- general plugin configuration windows unless a real RTL need emerges there
- native FFXIV UI mutation paths

## Risks

1. Texture generation can become expensive if it happens per frame.
2. Cache keys that omit width, scale, or colors will generate wrong reuse.
3. Tooltip sizing can regress if metrics still come from `ImGui.CalcTextSize`.
4. Large multiline paragraphs can create oversized textures without explicit
   limits.
5. Mixed bidi plus punctuation can still expose shaping gaps in the current
   bitmap renderer.

These risks are addressed by the performance and usability spec that
accompanies this document.

## Exit Criteria

`Phase A` is complete when:

- RTL languages no longer fail activation because of `UnsupportedLanguage`
- those languages remain forced to overlay or tooltip presentation
- the covered ImGui surfaces render RTL text in the correct visual direction
- measurements come from the chosen backend rather than raw ImGui text APIs
- no hot-path per-frame texture regeneration remains

`Phase B` plugin work only begins after:

- `Phase A` is shipped and verified in-game
- memory and latency behavior are observed to be acceptable
- remaining limitations are specifically about fidelity or maintainability, not
  about basic user-visible correctness

## As-Built Status (2026-07-13)

Phase A uses `RtlTexturePresentationService` behind the presentation resolver.
Textures are transient presentation artifacts, never persisted translations.
Generation is bounded and asynchronous; cache hits are cheap and misses return
pending presentation rather than generating a texture every frame. This design
does not add general RTL behavior to native FFXIV widgets or arbitrary ImGui
controls.
