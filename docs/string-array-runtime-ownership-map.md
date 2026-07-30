## Purpose

This document records the current ownership model for `StringArrayData`-backed
surfaces and adjacent live-text runtimes in Echoglossian.

It answers three practical questions:

1. which surfaces are actively handled today
2. which persistence backend each surface uses
3. whether the current runtime reacts automatically to live value changes

This is intentionally a runtime map, not a schema design doc.

## Executive summary

Not all `StringArrayData` surfaces share one runtime.

The current production split is:

1. canonical `StringArrayDatas` runtimes for structured string-array payloads
2. `GameWindow`-family runtimes for live main-menu windows
3. `QuestPlate` or popup-backed runtimes for quest surfaces
4. dedicated tables for surfaces that do not reconcile cleanly with the generic
   owners

So the short answer is:

- some `StringArrayData` surfaces do react automatically today
- they react through addon lifecycle events, not through a global hook on every
  native array mutation
- canonical `stringarraydatas` owns migrated structured string-array payloads
- `_MainCommand`, `AddonContextMenuTitle`, and `SystemMenu` do not belong to
  that canonical string-array path

## Runtime families

## 1. Active canonical `StringArrayDatas` runtimes

These surfaces persist translated structured payloads through the canonical
`stringarraydatas` table.

### Active surfaces

- `Character`
- `CharacterClass`
- `CharacterRepute`
- `CharacterProfile`
- `CharacterStatus`
- `Hud`
- `Hud2`
- `OperationGuide`
- `AreaMap`
- `_NaviMap`

### Runtime owners

- `NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs`
  for `Character*`, `Hud*`, and `OperationGuide`
- `NativeUI/AddonHandlers/Quest/MapSurfaceStringArrayHandler.cs`
  for `AreaMap` and `_NaviMap`

### Persistence backend

- canonical `stringarraydatas`

Important code paths:

- `DbFirstStructuredStringArrayHelper.TranslateAndPersistAsync(...)`
- `StringArrayDataPersistenceHelper.CreateCanonicalRow(...)`
- `StringArrayDataPersistenceHelper.FindStringArrayData(...)`
- `StringArrayDataCacheManager`

### Reactivity model

These surfaces react automatically, but only through addon lifecycle:

- `PreSetup`
- `PreRefresh`
- `PreRequestedUpdate`
- `PreDraw` as a lightweight retry gate
- `PreHide`
- `PreFinalize`

This is automatic enough for the migrated windows, but it is not a global
watcher on every native setter call.

### Current caveat for `Character*`

For the `Character*` surfaces specifically, original recovery can still be
contaminated by already-visible translated payloads. Until that is made
stricter, these windows may still persist non-canonical originals.

## 2. Active `GameWindow`-family runtimes

These surfaces use the shared DB-first GameWindow base but do not belong to the
canonical `StringArrayDatas` ownership path.

### Active surfaces

- `_MainCommand`
- `AddonContextMenuTitle`
- `SystemMenu`

### Runtime owner

- `NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs`

### Persistence backend

- `gamewindows`

### Why it is separate

These surfaces are live menu windows whose capture, apply, and restore behavior
is tied to visible text nodes and menu state, not to the typed structured
string-array schemas.

`MainCommandText` remains a sheet-backed canonical lookup source. It is not the
authoritative live runtime owner for these windows.

## 3. Active quest-backed runtimes

These surfaces do not belong to the generic `StringArrayData` migration wave.
They are listed here because they are active text runtimes with their own
DB-first ownership.

### Active surfaces

- `Journal`
- `JournalDetail`
- `JournalAccept`
- `JournalResult`
- `RecommendList`
- `_ToDoList`
- `ScenarioTree`

### Persistence backends

- `questplates` for canonical quest-backed rows
- dedicated `QuestPopupText` fallback rows for popup surfaces that do not have
  a reliable quest id at capture time

### Notes

- `JournalAccept` and `JournalResult` prefer canonical quest lookup when a
  proven quest id is available, and fall back to dedicated popup rows
  otherwise.
- `_ToDoList` remains separate from the dedicated `ToDo` addon runtime.

## 4. Active dedicated-table runtimes

These surfaces use specialized handlers and specialized tables or entity
shapes.

Examples:

- `ContextMenu` -> `ContextMenuText`
- `Tooltip` -> `TooltipText`
- `ToDo` -> `ToDoText`
- `SelectYesno`, `SelectOk`, `SelectString`, `SelectIconString`
  -> `SelectionDialogText` and, for `SelectString`, preferred reuse of
  `SelectString`
- `Talk`
- `_BattleTalk`
- `TalkSubtitle`
- `_MiniTalk`
- `CutSceneSelectString`
- toast-family handlers

These are not part of the canonical `StringArrayData` migration map.

## 5. Current answer to "Does it react automatically?"

### For migrated structured string-array surfaces

Yes, but via addon lifecycle.

If the visible captured payload changes:

1. the addon-local handler captures it
2. serializes a payload key
3. checks canonical `StringArrayDatas` cache / DB
4. if missing, queues translation and save
5. on a later lifecycle pass, reads the translated row and applies it

### For all possible native `StringArrayData` mutations globally

No.

The plugin does not currently hook every native `StringArrayData` setter and
react to all mutations in one place.

That global-hook idea was researched, but it is not the current production
approach.

## 6. Presentation rule for migrated string-array surfaces

For migrated `StringArrayData` surfaces, non-native presentation should prefer
plugin hover tooltips per translated text:

- native-only mode: translated text may be applied directly into the addon
- tooltip mode: keep the native addon untouched and use plugin hover tooltips
  for each translated text block
- swap mode: keep the translated text in the addon and use plugin hover
  tooltips to show the original text for each translated block

This rule should continue to guide future migrations so we do not reintroduce
direct array-write contention just to support overlay-like presentation.

## 7. #139 source contract

Canonical structured rows carry the captured source persistence identity.
Structured helpers receive `SourceClientLanguage`; translation selects the
provider code internally. A blank, unknown, generic-provider, or ambiguous
Chinese legacy source remains stored history but is not reusable. Overlay-only
flows publish presentation only and do not mutate native `StringArrayData`.
