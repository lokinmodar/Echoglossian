# Action/Item/Trait detail sheet flow

This flow owns the dedicated detail entities used by tooltips and detail
surfaces.

## Code paths

- action detail runtime:
  [ActionItemDetailUiRuntime.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/ActionItemDetailUiRuntime.cs)
- action detail prefetch:
  [ActionDetailPrefetchRuntime.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/ActionDetailPrefetchRuntime.cs)
- trait detail prefetch:
  [TraitDetailPrefetchRuntime.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/TraitDetailPrefetchRuntime.cs)
- persistence helpers:
  [ActionTooltipPersistenceHelper.cs](/C:/Dante/_dalamud/Echoglossian/DBHelpers/ActionTooltipPersistenceHelper.cs)
  [ItemTooltipPersistenceHelper.cs](/C:/Dante/_dalamud/Echoglossian/DBHelpers/ItemTooltipPersistenceHelper.cs)
  [TraitPersistenceHelper.cs](/C:/Dante/_dalamud/Echoglossian/DBHelpers/TraitPersistenceHelper.cs)
- caches:
  [ActionTooltipCacheManager.cs](/C:/Dante/_dalamud/Echoglossian/Cache/ActionTooltipCacheManager.cs)
  [ItemTooltipCacheManager.cs](/C:/Dante/_dalamud/Echoglossian/Cache/ItemTooltipCacheManager.cs)
  [TraitCacheManager.cs](/C:/Dante/_dalamud/Echoglossian/Cache/TraitCacheManager.cs)

## Data flow

```text
Excel sheet rows
  -> canonical payload builder
  -> dedicated persistence helper
  -> dedicated DB table
  -> dedicated in-memory cache
  -> tooltip/detail runtime lookup
  -> native UI or hover presentation
```

## Active entities

- `ActionTooltip`
- `ItemTooltip`
- `Trait`
- `EventItemText`
- `DeepDungeonItemText`
- action-adjacent `*ActionText` families as identity fallback for
  non-`Action` addon content

## Notes

- Status update - 2026-05-01:
  `JournalDetail` is no longer the active front for this refactor pass.
  Current runtime work is focused on `ActionDetail`, `ItemDetail`, and their
  associated tooltip surfaces, especially fast-hover transitions, content
  identity, and overlay/native-mode synchronization.
- This flow is sheet-first and not tied to one live addon capture payload.
- It is already split by entity family and should remain split.
- `ActionDetail` and `ItemDetail` runtime presentation is overlay-backed for
  translated detail text; they should not be treated as hover-tooltip-only
  surfaces in config or UX wording.
- Live runtime resolution is lifecycle-gated:
  `ActionDetail` and `ItemDetail` only run while their addons are active in
  `AddonLifecycle`, and the active payload is resolved from the current hover
  first, with `AgentActionDetail` / `AgentItemDetail` used only as guarded
  fallback when the opposite tooltip family is not currently hovered.
- Native lookup is now identity-first:
  `ActionTooltip` / `ItemTooltip` stay primary for standard rows, while
  `EventItemText`, `DeepDungeonItemText`, and the relevant `*ActionText`
  tables can supply translated payloads by `id + targetLang + engine +
  gameVersion` without depending on live addon text equality.
- When one standard `Action` row is hovered before it exists in translated
  canonical storage, `ActionDetail` now triggers the existing
  `ActionDetailPrefetchRuntime` on demand with a cooldown, so missing common
  actions can backfill through the same canonical `ActionTooltip` pipeline
  instead of waiting for the periodic per-job sweep.
