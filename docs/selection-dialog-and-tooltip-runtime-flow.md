# Selection dialog and Tooltip runtime flow

This document records the current runtime shape for the generic
selection-dialog family and the dedicated `Tooltip` addon runtime.

It focuses on:

- where source text is captured
- which persistence table owns the translated history
- how native apply, anchored overlay, hover presentation, and restore are driven
- where the dedicated `Tooltip` addon now diverges from the older hover-only path

## Scope

Selection-dialog surfaces covered here:

- `SelectYesno`
- `SelectOk`
- `SelectString`
- `SelectIconString`

Dedicated tooltip surface covered here:

- `Tooltip`

## Display contract

The `Select*` family no longer uses overlay windows.

Current display modes:

- `NativeUiTranslation`
- `TooltipTranslation`
- `NativeUiTranslationWithOriginalTooltips`

Mode rules for selection dialogs:

- native-only mode writes translated text into the native addon
- tooltip mode leaves the native addon untouched and uses structured hover
  tooltips
- swap mode keeps translated native text and exposes the original text through
  plugin hover tooltips

The dedicated `Tooltip` addon uses the same mode enum, but its presentation
backend now differs:

- `NativeUiTranslation` stays native-only
- `TooltipTranslation` publishes an anchored overlay on top of the visible game
  `Tooltip` addon
- `NativeUiTranslationWithOriginalTooltips` keeps translated native text and
  publishes an anchored overlay showing the original text

`TooltipTranslation` and swap for addon `Tooltip` no longer rely on
`HoverTooltipManager`.

## Registration map

All of these surfaces are wired from
`NativeUI/Helpers/AddonHandlerWiring.cs`.

| Surface | Runtime owner | Persistence owner | Primary capture surface |
| --- | --- | --- | --- |
| `SelectYesno` | `SelectYesNoHandler` | `SelectionDialogText` | best-of `AtkValues`, `StringArrayData`, or readable text nodes |
| `SelectOk` | `SelectOkHandler` | `SelectionDialogText` | best-of `AtkValues`, `StringArrayData`, or readable text nodes |
| `SelectString` | `SelectStringHandler` | preferred `SelectString`, fallback `SelectionDialogText` | best-of `AtkValues`, `StringArrayData`, or readable text nodes |
| `SelectIconString` | `SelectIconStringHandler` | `SelectionDialogText` | best-of `AtkValues`, `StringArrayData`, or readable text nodes |
| `Tooltip` | `TooltipHandler` | `TooltipText` | readable text nodes only |

Dedicated config ownership:

- `SelectYesno`
  - `TranslateYesNoScreen`
  - `SelectYesNoTranslationDisplayMode`
- `SelectOk`
  - `TranslateSelectOk`
  - `SelectOkTranslationDisplayMode`
- `SelectString`
  - `TranslateSelectString`
  - `SelectStringTranslationDisplayMode`
- `SelectIconString`
  - `TranslateSelectIconString`
  - `SelectIconStringTranslationDisplayMode`
- `Tooltip`
  - `TranslateTooltipAddon`
  - `TooltipAddonTranslationDisplayMode`
  - `TooltipAddonHideNativeTooltipWhenOverlayActive`
  - `TooltipAddonOverlay*`

## Selection-dialog shared runtime

Shared owner:

- `NativeUI/AddonHandlers/SelectionDialogs/SelectionDialogHandlerBase.cs`

Concrete handlers:

- `SelectYesNoHandler.cs`
- `SelectOkHandler.cs`
- `SelectStringHandler.cs`
- `SelectIconStringHandler.cs`

### Lifecycle

The shared runtime listens to:

- `PreSetup`
- `PreRefresh`
- `PreRequestedUpdate`
- `PostUpdate`
- `PreDraw`
- `PreHide`
- `PreFinalize`

### Capture model

On each capture pass, the handler probes three possible source shapes:

1. `AtkValues`
2. `StringArrayData`
3. readable text nodes

The best source kind is chosen by
`SelectionDialogCapturePolicy.ResolveBestSource(...)`.

Important refinement:

- even after a preferred source kind is selected, the runtime may still promote
  the text-node payload if `SelectionDialogCapturePolicy` decides the
  text-node shape is a better structural match for the visible dialog

This is what keeps native capture flexible without hardcoding one source for
every `Select*` addon.

### Shared flow

```text
selection-dialog lifecycle
  -> capture live payload
  -> resolve effective payload shape
  -> load stored translation or queue async translation
  -> PostUpdate / PreDraw refresh visible addon
  -> restore if native mode is off
  -> register structured hover tooltips
  -> apply native translation if current mode writes natively
```

Detailed flow:

1. `PreSetup` / `PreRefresh` / `PreRequestedUpdate` capture the live payload.
2. The handler resolves the effective source language and normalizes the
   visible source payload.
3. It checks whether the currently held resolved state already matches the
   visible payload.
4. If not, it tries the persisted owner for the surface.
5. If persistence misses, it queues async translation without blocking the
   lifecycle callback.
6. `PostUpdate` / `PreDraw` then:
   - resolve the visible addon again
   - restore plugin-owned native mutation when the current mode is not native
   - register hover tooltips when the resolved payload came from readable text
     nodes
   - apply native translation through the captured source kind
7. `PreHide` / `PreFinalize` restore only handler-owned native mutations and
   clear local state.

### Native apply and restore

The shared runtime applies and restores through the same source kind that owned
capture:

- `AtkValues` -> `SetManagedString(...)`
- `StringArrayData` -> `SetValue(..., suppressUpdates: true)`
- readable text nodes -> `SetText(...)`

Restore is guarded:

- the handler restores only when it previously owned the native mutation
- restore only happens when the live text still equals the exact replacement
  text written by the handler

That keeps mode switches from clobbering fresh game-owned updates.

### Hover tooltip rules

Hover tooltips are only registered when the resolved payload came from readable
text nodes.

Current rules:

- the same structured title/body payload is registered on each visible captured
  text node
- `SelectYesno`, `SelectOk`, and normal `SelectString` payloads promote the
  first text into the tooltip title when multiple texts exist
- `SelectIconString` intentionally does not promote the first text to the title
  slot, so its tooltip stays body-only

## Surface-specific notes

### `SelectYesno`

- Native addon name must remain exactly `SelectYesno`.
- Persists only through `SelectionDialogText`.

### `SelectOk`

- Persists only through `SelectionDialogText`.
- Uses the same shared runtime contract as `SelectYesno`, but keeps its own
  config toggle and display mode.

### `SelectString`

- Tries to reuse the existing `SelectString` table when the payload shape is a
  clean "question + ordered options" dialog.
- Falls back to `SelectionDialogText` when the visible payload shape does not
  safely map to the canonical `SelectString` row shape.

### `SelectIconString`

- Keeps its own config toggle and display mode.
- Persists only through `SelectionDialogText`.
- Uses body-only structured tooltip presentation because the observed visible
  payload behaves like ordered option rows rather than a stable title/body
  split.

## Dedicated `Tooltip` addon runtime

Owner:

- `NativeUI/AddonHandlers/Common/TooltipHandler.cs`

Base runtime:

- `NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs`

Anchored overlay runtime:

- `NativeUI/Helpers/TooltipAddonAnchoredOverlayRuntime.cs`

### Shared data pipeline

All three `Tooltip` display modes share the same data path:

```text
Tooltip addon lifecycle
  -> capture readable visible text nodes
  -> normalize ordered node payload
  -> TooltipTextCacheManager lookup
  -> DB lookup on cache miss
  -> async translation when missing
  -> dedicated TooltipText persistence
  -> mode-specific presentation backend
```

Detailed flow:

1. The handler captures readable visible text nodes only.
2. The ordered source payload is normalized by text-node key
   (`nodeId:ordinal`) so duplicate visible nodes keep stable ordering.
3. `TooltipTextCacheManager` is the fast path for all display modes.
4. Cache misses fall back to the dedicated `TooltipText` table keyed by:
   - addon name
   - ordered original text JSON
   - source language
   - target language
   - effective engine
   - game version
   - source-content hash
5. If the payload is still missing, the shared DB-first GameWindow base queues
   async translation.
6. Persisted translated rows are projected back onto the current visible
   text-node keys before apply.
7. Presentation then follows the current display mode backend.

### Mode-specific presentation backends

#### `NativeUiTranslation`

- uses the shared `TooltipText` pipeline
- applies translated text directly to the native `Tooltip` addon
- publishes no anchored overlay
- restores only handler-owned native mutations

#### `TooltipTranslation`

- uses the shared `TooltipText` pipeline
- leaves native tooltip text untouched
- publishes an anchored overlay against the live `Tooltip` root bounds
- can optionally hide the native tooltip while the overlay is active
- restores native visibility immediately when overlay publication clears

#### `NativeUiTranslationWithOriginalTooltips`

- uses the shared `TooltipText` pipeline
- applies translated native text
- publishes an anchored overlay showing the original text
- prefers one combined rich original-text presentation rebuilt from captured
  `SeString` payload segments when every visible text-node payload is available
- falls back to the plain ordered joined text body when one rich payload segment
  cannot be recovered safely

### Anchored overlay behavior

The dedicated `Tooltip` overlay no longer registers a hover target over the
game tooltip.

Current anchoring rules:

- the anchor source is the live `Tooltip` addon root rectangle
- publication requires a visible addon, a valid frame, and resolved mode
  content
- overlay clear restores native visibility when this runtime hid it
- mode changes, payload changes, addon hide, and runtime cleanup clear the
  overlay before the next presentation path becomes active

### Sizing and renderer behavior

The anchored overlay sizes itself from the native tooltip geometry first.

Current policy:

- base position and size come from the live `Tooltip` addon root bounds
- native addon scale is carried into the overlay frame
- user `TooltipAddonOverlay*` settings act as adjustments on top of the
  native-derived geometry
- standard languages continue through the shared ImGui text renderer
- RTL and texture-backed languages continue through
  `RtlTexturePresentationService`

This is deliberately separate from the generic hover-tooltip sizing path.

### Persistence notes

- `Tooltip` uses the dedicated `TooltipText` entity family.
- This keeps the addon separate from `ActionDetail` / `ItemDetail` and other
  prefetch-backed tooltip surfaces.
- The same persisted row is reused regardless of whether the active backend is
  native or anchored overlay.

## Related source files

| Concern | File |
| --- | --- |
| Event wiring | `NativeUI/Helpers/AddonHandlerWiring.cs` |
| Shared selection-dialog runtime | `NativeUI/AddonHandlers/SelectionDialogs/SelectionDialogHandlerBase.cs` |
| Selection capture policy | `NativeUI/AddonHandlers/SelectionDialogs/SelectionDialogCapturePolicy.cs` |
| Text-node helpers | `NativeUI/AddonHandlers/SelectionDialogs/SelectionDialogNodeResolvers.cs` |
| Dedicated Tooltip runtime | `NativeUI/AddonHandlers/Common/TooltipHandler.cs` |
| Tooltip anchored overlay policy | `NativeUI/Helpers/TooltipAddonAnchoredOverlayPresentationPolicy.cs` |
| Tooltip anchored overlay state | `NativeUI/Helpers/TooltipAddonAnchoredOverlayRuntime.cs` |
| Tooltip rich original presentation | `NativeUI/Helpers/TooltipAddonRichOriginalTextPresentationFactory.cs` |
| Hover registration | `NativeUI/Helpers/HoverTooltipRegistration.cs` |
| Hover manager | `NativeUI/Helpers/HoverTooltipManager.cs` |
