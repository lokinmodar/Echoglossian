# List of usable Excel Sheets that contain actions information

The action-adjacent sheets are not routed into one generic entity. The current
target storage is split by sheet family so capture, translation, persistence,
and cache lookups stay specific while the prefetch mechanics remain reusable.

- `Action + ActionTransient` -> `ActionTooltip` table + `ActionTooltipCacheManager`
- `AozAction + AozActionTransient` -> `AozActionText` table + specific reference-text cache
- `PetAction` -> `PetActionText` table + specific reference-text cache
- `CraftAction` -> `CraftActionText` table + specific reference-text cache
- `BgcArmyAction + BgcArmyActionTransient` -> `BgcArmyActionText` table + specific reference-text cache
- `CompanyAction` -> `CompanyActionText` table + specific reference-text cache
- `GeneralAction` -> `GeneralActionText` table + specific reference-text cache
- `BuddyAction` -> `BuddyActionText` table + specific reference-text cache
- `EventAction` -> `EventActionText` table + specific reference-text cache
- `EventItem` -> `EventItemText` table + specific reference-text cache
- `MountAction` -> `MountActionText` table + specific reference-text cache
- `EurekaMagiaAction` -> `EurekaMagiaActionText` table + specific reference-text cache
- `PvPAction` -> `PvPActionText` table + specific reference-text cache
- `MainCommand` -> `MainCommandText` table + specific reference-text cache
- `DeepDungeonItem` -> `DeepDungeonItemText` table + specific reference-text cache

`MainCommand` is intentionally kept in both dimensions, but with different
runtime owners:
- as an explicit Excel sheet in the source inventory
- and as its own canonical sheet-backed persistence/cache path for reusable
  text lookup
- while the live `_MainCommand` addon runtime remains on `GameWindow`

That means the sheet is not being dropped from the model, and the existing
working `_MainCommand` addon flow is also not being moved off `GameWindow`.

