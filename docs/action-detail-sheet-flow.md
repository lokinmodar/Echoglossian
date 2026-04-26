# Action/Item/Trait detail sheet flow

This flow owns the dedicated detail entities used by tooltips and detail
surfaces.

## Code paths

- action detail runtime:
  [ActionItemDetailUiRuntime.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/ActionItemDetailUiRuntime.cs)
- action detail prefetch:
  [ActionItemDetailPrefetchRuntime.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/ActionItemDetailPrefetchRuntime.cs)
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

## Notes

- This flow is sheet-first and not tied to one live addon capture payload.
- It is already split by entity family and should remain split.
