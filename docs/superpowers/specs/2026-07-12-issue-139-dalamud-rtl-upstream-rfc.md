# Issue 139 Dalamud Upstream Complex-Text RFC Draft

## Summary

This document defines the recommended upstream direction for complex text after
Echoglossian ships its plugin-local `Phase A` solution for issue `#139`.

The recommendation is not "add full RTL everywhere in Dalamud immediately".
The recommended first upstream target is narrower:

- framework support for static complex text
- reusable shaping or layout service boundaries
- optional texture or drawlist output for plugin-owned read-only UI

This lets plugins stop reinventing static-text complex layout without forcing
Dalamud to own editable-widget RTL semantics in the first pass.

## Motivation

Echoglossian can solve its immediate product problem locally with a
texture-backed backend, but the broader ecosystem problem remains:

- multiple plugins may eventually need static complex-text layout
- texture generation and layout policy are easy to duplicate poorly
- font loading and texture lifetime are already concerns that Dalamud owns in
  adjacent systems

The current Dear ImGui reality matters here:

- [#4227](https://github.com/ocornut/imgui/issues/4227) shows that full RTL
  support required deep ownership of shaping, bi-di, wrapping, input widgets,
  selections, caret logic, and atlas behavior
- [#2365](https://github.com/ocornut/imgui/issues/2365) shows interest in
  external text layout and rasterization

The current Dalamud API surface also matters:

- `IFontAtlas.NewDelegateFontHandle(...)` allows custom font construction
- `IFontAtlasBuildToolkitPreBuild` already supports font addition and scaling
- `ITextureProvider` already supports `CreateFromImageAsync(...)`
- `ITextureProvider` also supports `CreateDrawListTexture(...)`

That combination suggests a good upstream seam for static complex text.

## Problem Statement

Dalamud today provides strong font-atlas and texture primitives, but it does
not yet provide a first-class complex-text presentation service for plugin
authors.

That means every plugin needing complex text must choose between:

- broken plain text
- one-off string preprocessing
- local text-to-image pipelines
- local layout or rasterization stacks

## Goals

1. Provide a reusable framework-level answer for static complex text.
2. Keep the first upstream slice narrow enough to be maintainable.
3. Avoid forcing full editable-widget RTL semantics into the first proposal.
4. Allow plugin-local `Phase A` solutions to migrate cleanly later.

## Non-Goals

- no promise of full RTL support for every Dear ImGui widget in v1
- no immediate bidi-aware `InputText` or selection semantics
- no forced global replacement of Dear ImGui text rendering in the first slice
- no plugin-breaking API churn for font or texture primitives

## Proposed Phases

### Upstream Phase B1: Static complex-text service

Add a small service boundary, conceptually similar to:

- `IComplexTextLayoutService`
- `ComplexTextLayoutRequest`
- `ComplexTextLayoutResult`

Possible request fields:

- logical text
- culture or language code
- direction hint
- max width
- font request or font handle
- size and scale
- alignment

Possible result fields:

- measured size
- line metrics
- logical-to-visual mapping metadata where available
- either:
  - a framework-owned texture handle, or
  - glyph or draw commands suitable for read-only drawlist use

This phase should explicitly target static read-only text.

### Upstream Phase B2: ImGui-facing helpers

Once the service exists, add convenience helpers such as:

- `ImGuiComponents.DrawComplexText(...)`
- `ITextureProvider.CreateTextureFromTextLayout(...)`
- optional helpers for measuring and clipping static complex text blocks

This keeps most plugins away from raw shaping and cache management.

### Upstream Phase B3: Editable-widget investigation

Only after the static-text surface is stable should Dalamud consider:

- editable text input
- caret movement in bidi content
- selection rectangles in bidi content
- integration with Dear ImGui editing internals

This phase is intentionally deferred because it is closer to the problem space
described in Dear ImGui issue `#4227` than to Echoglossian's shipping needs.

## Suggested API Direction

The exact names can change, but the upstream concept should preserve these
properties:

- plugin authors submit logical text, not pre-reordered visual strings
- framework owns layout or rasterization policy
- result exposes measured size and a renderable artifact
- font and texture lifetime are framework-managed
- caching behavior is explicit and observable

## Why This Fits Dalamud

Dalamud already owns related primitives:

- font atlas build hooks
- scaling modes
- texture creation from image bytes
- drawlist-backed texture creation

It therefore already has most of the substrate needed for a framework-level
static complex-text service without jumping immediately to "fork Dear ImGui and
rewrite text editing".

## Why Echoglossian Still Needs Local `Phase A`

Even a good upstream direction will take time:

- design review
- API acceptance
- implementation
- testing across plugin consumers
- release cadence

Echoglossian therefore should not block the product fix on upstream work. The
plugin should ship with a local backend first, but it should do so behind a
presentation interface that can later consume the upstream service.

## Adoption Plan For Echoglossian

1. Ship `Phase A` with `RtlTextureBackend`.
2. Gather real-world latency and memory behavior from plugin use.
3. Write the upstream proposal against actual observed needs, not only theory.
4. If Dalamud lands a static complex-text service, add a
   `DalamudComplexTextBackend` adapter in Echoglossian.
5. Retire or reduce plugin-local text rendering once upstream behavior is
   sufficient.

## Open Questions For Upstream Discussion

1. Should the first framework result be texture-backed, drawlist-backed, or
   both?
2. Should caching live inside the service, inside `ITextureProvider`, or in a
   separate abstraction?
3. What font-resolution story is acceptable for culture-specific fallback
   fonts?
4. What diagnostics should plugin authors receive for cache misses, layout
   failure, or unsupported shaping cases?
5. How much of the service can remain independent from Dear ImGui internals in
   the first pass?

