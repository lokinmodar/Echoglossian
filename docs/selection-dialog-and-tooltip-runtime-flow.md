# Selection dialog and Tooltip runtime flow

This document records the current runtime shape for the generic
selection-dialog family and the dedicated `Tooltip` addon runtime.

It focuses on:

- where source text is captured
- which persistence table owns the translated history
- how native apply, tooltip presentation, and restore are driven
- where the current mode contract differs from the older overlay path

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

The same display contract is also used by the dedicated `Tooltip` addon
runtime.

Mode rules:

- native-only mode writes translated text into the native addon
- tooltip mode leaves the native addon untouched and uses structured hover
  tooltips
- swap mode keeps translated native text and exposes the original text through
  plugin hover tooltips

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

### Flow

```text
Tooltip addon lifecycle
  -> capture readable visible text nodes
  -> normalize ordered node payload
  -> dedicated TooltipText lookup
  -> async translation when missing
  -> dedicated persistence
  -> native apply / structured hover / guarded restore
```

Detailed flow:

1. The handler captures readable visible text nodes only.
2. The ordered source payload is normalized by text-node key
   (`nodeId:ordinal`) so duplicate visible nodes keep stable ordering.
3. Lookup uses the dedicated `TooltipText` table keyed by:
   - addon name
   - ordered original text JSON
   - source language
   - target language
   - effective engine
   - game version
   - source-content hash
4. If the payload is missing, the shared DB-first GameWindow base queues async
   translation.
5. Persisted translated rows are projected back onto the current visible
   text-node keys before apply.
6. Presentation then follows the current display mode:
   - native mode may replace safe readable text nodes
   - tooltip mode leaves native text untouched and uses hover
   - swap mode writes translated native text and shows original hover text

### Persistence notes

- `Tooltip` uses the dedicated `TooltipText` entity family.
- This keeps the addon separate from `ActionDetail` / `ItemDetail` and other
  prefetch-backed tooltip surfaces.

## Related source files

| Concern | File |
| --- | --- |
| Event wiring | `NativeUI/Helpers/AddonHandlerWiring.cs` |
| Shared selection-dialog runtime | `NativeUI/AddonHandlers/SelectionDialogs/SelectionDialogHandlerBase.cs` |
| Selection capture policy | `NativeUI/AddonHandlers/SelectionDialogs/SelectionDialogCapturePolicy.cs` |
| Text-node helpers | `NativeUI/AddonHandlers/SelectionDialogs/SelectionDialogNodeResolvers.cs` |
| Dedicated Tooltip runtime | `NativeUI/AddonHandlers/Common/TooltipHandler.cs` |
| Hover registration | `NativeUI/Helpers/HoverTooltipRegistration.cs` |
| Hover manager | `NativeUI/Helpers/HoverTooltipManager.cs` |
