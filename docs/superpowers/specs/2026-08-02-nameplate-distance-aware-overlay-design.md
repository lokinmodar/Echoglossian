<!--
  Copyright (c) lokinmodar. All rights reserved.
  Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
-->

# NamePlate distance-aware overlay design

Date: 2026-08-02

## Goal

Add a shared distance-aware overlay presentation helper that lets `NamePlate`
fall back to our overlay renderer for overlay-only languages while scaling,
fading, and culling by camera distance.

This design intentionally does **not** implement real occlusion yet. It only
adds:

- camera-distance-driven scale
- camera-distance-driven fade
- hard max-distance cutoff

The backend must preserve the existing shared text renderer behavior:

- normal overlay text continues through the shared ImGui path
- RTL and custom-glyph languages continue through
  `RtlTexturePresentationService`

## Problem statement

The current native `NamePlate` path fixed the visual depth/scale problems only
for languages that can use the game's native font. That does not solve the
languages already governed by the global `OverlayOnlyLanguage` rule, because
those languages cannot rely on native nameplate text replacement and still need
our custom overlay/font pipeline.

The previous `NamePlate` overlay approach was a plain screen overlay. It did not
scale or fade by distance and caused excessive visual stacking when the camera
moved away from interactive objects.

We need a shared helper that can be reused by future surfaces, but the first and
only consumer in this scope is `NamePlate`.

## Scope

In scope:

- add a shared helper for distance-aware overlay presentation
- apply that helper to `NamePlate`
- preserve the current native `NamePlate` backend
- preserve RTL/custom-glyph rendering through the existing shared renderer
- add a small global configuration surface for distance-aware overlays

Out of scope:

- real depth-buffer or wall/tree occlusion
- a new generic world-overlay renderer
- changing translation lookup, prefetch, cache, or DB semantics
- per-surface tuning beyond `NamePlate` in this pass
- changing current non-`NamePlate` overlay behavior

## Functional requirements

### Backend selection

`NamePlate` must support two presentation backends:

1. native nameplate presentation
2. distance-aware overlay presentation

Backend selection is driven by existing global language policy first.

- when `OverlayOnlyLanguage == false`, `NamePlate` keeps using the native
  backend
- when `OverlayOnlyLanguage == true`, `NamePlate` must use our
  distance-aware overlay backend

This means backend selection is primarily language-policy driven, not
string-by-string glyph detection driven, because the repository already has a
global overlay-only language rule for exactly this problem class.

### Mode behavior

When `OverlayOnlyLanguage == false`:

- `NativeUiTranslation`
  - translated text is written to the native name field
  - no plugin overlay is drawn
- `TooltipTranslation`
  - native primary text remains original
  - auxiliary native presentation surface continues to show the translation
- `NativeUiTranslationWithOriginalTooltips`
  - translated text is written to the native name field
  - auxiliary native presentation surface shows the original text

When `OverlayOnlyLanguage == true`:

- the native backend is not used for translated presentation
- the visible native nameplate remains original
- the translation is shown only through our distance-aware overlay
- this preserves custom glyph and RTL coverage
- swap semantics collapse to overlay translation only, because native
  translation is forbidden by policy

### Distance-aware overlay behavior

The distance-aware overlay backend must:

- compute distance from the active camera to the surface world anchor
- render at full scale and full opacity while close
- reduce scale progressively as distance increases
- reduce opacity progressively as distance increases
- stop rendering completely after a configured maximum distance

If projection fails or the object is outside the configured maximum distance,
the overlay is skipped for that frame.

## Recommended approach

Three approaches were considered:

1. restore a `NamePlate`-specific overlay path
2. add a shared distance-aware presentation helper on top of the existing
   overlay renderer
3. build a new world-overlay subsystem

Approach 2 is the recommended design.

It keeps the patch narrow:

- no new translation renderer
- no duplicate RTL path
- no duplicate caches or queues
- no speculative world-overlay subsystem

It also keeps `NamePlate` as the first consumer of a reusable helper instead of
reintroducing a one-off overlay implementation.

## Architecture

### Shared helper

Add a shared helper responsible only for distance-aware presentation policy.

Proposed file:

- `UIOverlays/TranslationOverlay/DistanceAwareOverlayPresentation.cs`

Responsibilities:

- accept camera distance and distance-aware config
- return presentation values:
  - `IsVisible`
  - `Scale`
  - `Alpha`

This helper must not know anything about:

- `NamePlate`
- translation
- caches
- RTL
- ImGui internals beyond the presentation outputs

It is a pure presentation-policy unit.

### Renderer integration

The existing shared overlay renderer remains the text backend.

The renderer request path must accept per-frame presentation overrides:

- scale override
- alpha override

The renderer continues to choose between:

- plain ImGui text rendering
- `RtlTexturePresentationService`

No new renderer backend is introduced in this design.

### NamePlate runtime integration

`NamePlateTranslationRuntime` becomes responsible for choosing one presentation
backend per frame:

- native backend when the current language allows native presentation
- distance-aware overlay backend when `OverlayOnlyLanguage == true`

It must not attempt to use both at once for the same frame.

The runtime should reuse existing helper logic already present in the file:

- `ResolveNamePlateWorldAnchor(...)`
- `ResolveCenteredNamePlateOverlayBounds(...)`

The overlay path should use the object's projected anchor and the shared
distance-aware helper, then hand the final text draw to the shared overlay
renderer.

### Draw loop

The plugin draw loop must draw `NamePlate` overlays only when the active backend
for the frame is the distance-aware overlay backend and the overlay state is
currently visible.

This is intentionally narrower than the old always-on nameplate overlay path.

## Data flow

### Native backend path

1. Nameplate callback receives handler.
2. Runtime resolves original text.
3. Runtime resolves cached translated text or records a prefetch candidate.
4. Runtime applies the existing native presentation plan.
5. No plugin overlay draw occurs.

### Distance-aware overlay path

1. Nameplate callback receives handler.
2. Runtime resolves original text.
3. Runtime resolves cached translated text or records a prefetch candidate.
4. Runtime resolves the world anchor for the nameplate source object.
5. Runtime projects the anchor to screen space.
6. Runtime computes camera distance.
7. Shared distance-aware helper returns visibility, scale, and alpha.
8. Runtime publishes one `NamePlate` overlay request only when visible.
9. Shared overlay renderer draws the text using:
   - ImGui text for standard languages
   - `RtlTexturePresentationService` for RTL/custom-glyph cases

## Configuration

Distance-aware overlays use one small global configuration group. `NamePlate`
is the only consumer initially.

Proposed configuration fields:

- `EnableDistanceAwareOverlays`
  - default: `true`
- `DistanceAwareOverlayFullScaleDistance`
  - default: `8`
- `DistanceAwareOverlayFadeStartDistance`
  - default: `16`
- `DistanceAwareOverlayMaxDistance`
  - default: `28`
- `DistanceAwareOverlayMinScale`
  - default: `0.60`

These settings are global, not per-surface, in this design.

## Presentation formula

Distance is measured from the active camera to the overlay world anchor.

Behavior:

- if `distance <= FullScaleDistance`
  - `Scale = 1.0`
  - `Alpha = 1.0`
- if `FullScaleDistance < distance < MaxDistance`
  - `Scale` interpolates linearly from `1.0` to `MinScale`
- if `distance >= FadeStartDistance`
  - `Alpha` interpolates linearly from `1.0` to `0.0` at `MaxDistance`
- if `distance >= MaxDistance`
  - `IsVisible = false`

This is intentionally a simple linear model for the first pass.

## Error handling and edge cases

- if no live object position is available, do not draw the overlay that frame
- if projection to screen space fails, do not draw the overlay that frame
- if translated text is missing, keep the current cache/prefetch behavior and do
  not invent partial fallback text
- if the overlay is not visible by distance rule, do not draw it and do not
  force any native text mutation
- if `OverlayOnlyLanguage == true`, never attempt native translated presentation

## Testing strategy

### Pure tests

Add tests for the shared distance helper:

- full-scale region
- fade region
- scale interpolation region
- max-distance cutoff
- invalid or boundary values

### Contract tests

Add repository-level contract tests that verify:

- the shared overlay renderer request path accepts distance-aware presentation
  overrides
- `NamePlate` runtime contains a distinct distance-aware overlay branch
- `NamePlate` still preserves the native backend branch for non-overlay-only
  languages

### Verification commands

Repository verification after implementation:

- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`

If the hosted `.Mock` harness is still blocked by DalaMock drift, the design
does not require falsely claiming hosted validation. That gap must be stated
explicitly in the implementation handoff.

## In-game verification

Required manual verification after implementation:

1. non-overlay-only language
   - native `NamePlate` behavior still works as it does today
2. overlay-only language
   - translation appears through the plugin overlay
   - scaling reduces as the camera moves away
   - opacity reduces as the camera moves away
   - overlay disappears beyond max distance
3. RTL language
   - text still renders through the texture-backed path
   - distance-aware scale/fade/cutoff still apply

## Risks

- reintroducing the old always-visible `NamePlate` overlay behavior
- accidentally drawing both native and overlay backends at once
- coupling distance math to the text renderer instead of keeping it in a shared
  policy helper
- broadening this into a generic world-overlay subsystem before there is a
  concrete need

## Acceptance criteria

The work is complete when all of the following are true:

- `NamePlate` still prefers native presentation when the language is not
  overlay-only
- `NamePlate` uses our overlay pipeline when the language is overlay-only
- the overlay path remains compatible with RTL/custom-glyph rendering
- distance-aware scale, fade, and cutoff apply to the overlay path
- no duplicate translation queue, cache, or DB path is introduced
- the patch stays scoped to `NamePlate` plus a reusable presentation helper
