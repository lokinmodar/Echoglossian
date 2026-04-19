## Purpose

This document records the current ownership model for `StringArrayData`-backed
surfaces in Echoglossian.

It answers three practical questions:

1. which surfaces are actively handled today
2. which persistence backend each surface uses
3. whether the current runtime reacts automatically to live value changes

This is intentionally a runtime map, not a schema design doc.

## Executive Summary

Not all `StringArrayData` surfaces are currently treated by one single runtime.

The repo is in a transitional state with three distinct situations:

1. active DB-first runtime using canonical `StringArrayDatas`
2. active DB-first runtime using `GameWindow` for non-`StringArrayType` payloads
3. active DB-first runtime using `QuestPlate`

So the short answer is:

- some `StringArrayData` surfaces do react automatically today
- they react through addon lifecycle refreshes, not through a global hook on
  every native array mutation
- the active `StringArrayType` surfaces now persist through canonical
  `stringarraydatas`
- `_MainCommand` remains on `gamewindows`, but now captures and applies
  translated payload through visible text nodes instead of `AtkValues`

## Current Validation Snapshot

The most recent manual validation cycle produced an important split:

- `_MainCommand` and `AddonContextMenuTitle` now repopulate `gamewindows` with
  clean English originals and PT-BR translations after a DB reset
- the `Character*` family still contaminates `stringarraydatas` by persisting
  already-visible PT-BR strings as `OriginalStrings`

That means the ownership model is directionally right, but the original payload
recovery step for the `Character*` string-array surfaces is still incomplete.

In the same validation window:

- `ScenarioTree` had no useful runtime signal
- `AreaMap` had no useful runtime signal
- `ActionTooltip` and `ItemTooltip` were already prefetching into their own
  tables, but still need broader in-game validation of apply behavior

## Runtime Families

## 1. Active DB-First Canonical `StringArrayDatas` Runtime

These surfaces are currently owned by
[DbFirstGameWindowAddonHandler.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs).

### Active surfaces

- `Character`
- `CharacterClass`
- `CharacterRepute`
- `CharacterProfile`
- `CharacterStatus`
- `Hud`
- `Hud2`
- `OperationGuide`
- `AddonContextMenuTitle`

### Registration point

These are registered in
[AddonHandlerWiring.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/AddonHandlerWiring.cs)
when the corresponding config toggles are enabled.

### Persistence backend

These surfaces now use the canonical `stringarraydatas` table as the translated
payload owner.

Important code path:

- `DbFirstGameWindowAddonHandler.RefreshOrQueue()`
- `DbFirstGameWindowAddonHandler.QueueTranslationIfNeeded()`
- `DbFirstStructuredStringArrayHelper.TranslateAndPersistAsync(...)`
- `StringArrayDataPersistenceHelper.CreateCanonicalRow(...)`
- `StringArrayDataPersistenceHelper.FindStringArrayData(...)`

The live apply and restore lifecycle still reuses the stable
`DbFirstGameWindowAddonHandler` machinery, but persistence and lookup for these
surfaces no longer go through `gamewindows`.

### Reactivity model

These surfaces do react automatically, but only through addon lifecycle.

The runtime listens to:

- `PreSetup`
- `PreRefresh`
- `PreRequestedUpdate`
- `PreDraw` as a lightweight retry gate
- `PreHide`
- `PreFinalize`

That means:

- if the visible payload changes, the addon-local runtime will usually notice on
  the next lifecycle pass
- it will look up the payload in the DB
- if the payload is missing, it queues background translation and save
- once the row exists, it applies the translated payload to the live surface

This is automatic enough for the migrated windows, but it is not a global
watcher on every native setter call.

### Current caveat for `Character*`

For the `Character*` surfaces specifically, "reacts automatically" currently
also means "can react to contaminated live payloads." Until original recovery is
made stricter, these windows may still:

- send mixed PT/EN `kNN|...` payloads to translation
- create `stringarraydatas` rows whose `OriginalStrings` are not truly
  canonical
- flicker when the game repaints and the addon-local runtime tries to chase the
  new state

## 2. Active DB-First `GameWindow` Runtime

These surfaces are still owned by
[DbFirstGameWindowAddonHandler.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs),
but they do not use the canonical `StringArrayDatas` runtime path.

### Active surfaces

- `_MainCommand`
- `AddonContextMenuTitle`

### Persistence backend

These surfaces currently use the `gamewindows` table.

### Why it is separate

`_MainCommand` and `AddonContextMenuTitle` do not flow through the same
`StringArrayType` ownership model used by the migrated `Character*` / `Hud*` /
`OperationGuide` surfaces.

They now use a hybrid `GameWindow` path where:

- capture is based on visible `AtkTextNode`s
- persistence is still `gamewindows`
- native apply/restoration is also done through those text nodes

This keeps them out of the fragile `AtkValues` path that was causing:

- `_MainCommand` to fail to apply visible translations
- `AddonContextMenuTitle` to place translated values in the wrong nodes for
  reused submenu contexts

For `AddonContextMenuTitle`, compatible payload reuse is intentionally disabled:

- exact payload matches are allowed
- "compatible superset" reuse is not

because the addon reuses the same visible slots for different submenu contexts.

## 3. Active DB-First Quest Runtime

These surfaces do not belong to the generic `StringArrayData` migration wave.
They are listed here only because they are active text runtimes with their own
DB-first ownership.

### Active surfaces

- `Journal`
- `JournalDetail`
- `_ToDoList`
- `ScenarioTree`

### Persistence backend

These use `questplates`, backed by canonical quest data and prefetch driven by
`QuestManager`.

### Reactivity model

These surfaces react automatically to:

- accepted quest list changes
- current quest sequence changes
- DB availability for the required quest payload

They do not depend on the `stringarraydatas` table.

## 4. Active Dedicated Text Runtimes

These surfaces use specialized handlers and specialized tables or entity shapes.

Examples:

- `Talk`
- `_BattleTalk`
- `TalkSubtitle`
- `_MiniTalk`
- `CutSceneSelectString`
- toast-family handlers

These are not part of the current `StringArrayData` DB-first migration map.

## 5. Canonical `stringarraydatas` Infrastructure

The repo now contains the canonical infrastructure for the next wave of
`StringArrayData` work, and that infrastructure is now the production owner for
the active `StringArrayType` surfaces listed above.

### Current pieces

- [StringArrayDataPersistenceHelper.cs](/C:/Dante/_dalamud/Echoglossian/DBHelpers/StringArrayDataPersistenceHelper.cs)
- [StringArrayStructuredPayload.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/StringArrayStructuredPayload.cs)
- [StringArrayStructuredPayloadResolver.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/StringArrayStructuredPayloadResolver.cs)
- [IStringArrayStructuredSchema.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/IStringArrayStructuredSchema.cs)
- [StringArrayStructuredPayloadBuilder.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/StringArrayStructuredPayloadBuilder.cs)

### What it can do today

- persist canonical structured `StringArrayDatas` rows
- resolve structured payloads back from DB rows
- fall back from legacy flat slot maps when needed
- define typed schemas for future surfaces
- own the translated payload for active `Character*`, `Hud`, `Hud2`,
  `OperationGuide`, and `AddonContextMenuTitle` windows

### What it does not do yet

- it does not currently run a generic runtime that watches all
  `StringArrayData` mutations and automatically saves/applies every change
- it does not currently own `_MainCommand`
- it does not yet expose the full plugin configuration UI needed to control all
  migrated `StringArrayData` surfaces cleanly
- it does not yet guarantee canonical-original capture for all `Character*`
  runtime mutations

### Presentation Rule for `StringArrayData` Surfaces

For migrated `StringArrayData` surfaces, non-native presentation should prefer
Echoglossian tooltips per translated text:

- native-only mode: translated text may be applied directly into the addon
- ImGui mode: keep the native addon untouched and use Echoglossian tooltips for
  each translated text block
- swap mode: keep the translated text in the addon and use Echoglossian
  tooltips to show the original text for each translated block

This rule should guide future migrations so we do not reintroduce direct
array-write contention just to support overlay-like presentation.

## 6. Dormant or Intentionally Quiet Surfaces

### `RecommendList`

`RecommendList` still exists in the repo as
[RecommendListHandler.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/AddonHandlers/Quest/RecommendListHandler.cs),
but it is intentionally not registered in
[AddonHandlerWiring.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/AddonHandlerWiring.cs)
right now.

So:

- it is not active
- it is not reacting automatically
- it is not currently part of the production path

### Other quiet quest handlers

The repo still contains some quest-family handlers that are not currently
registered as part of the stabilization pass.

They should not be treated as production-active ownership.

## Current Answer to “Does it react automatically?”

## For migrated `StringArrayType` surfaces

Yes, but via addon lifecycle.

If the visible captured payload changes:

1. the addon-local handler captures it
2. serializes a payload key
3. checks canonical `StringArrayDatas` cache / DB
4. if missing, queues translation and save
5. on a later lifecycle pass, reads the translated row and applies it

So this is automatic, but scoped to the addon’s lifecycle.

## For all possible native `StringArrayData` mutations globally

No.

The plugin does not currently hook every native `StringArrayData` setter and
react to all mutations in one place.

That global-hook idea was researched, but it is not the current production
approach.

## Why this distinction matters

Right now, saying “`StringArrayData` is handled” can mean two different things:

- “this addon has an automatic DB-first runtime and will react on the next
  lifecycle pass”
- or “the repo has typed schema/canonical infrastructure ready for additional
  surfaces that are not yet active”

Those are not the same.

The current repo has both, but it still does not use a single global mutation
hook as the owner of every `StringArrayData` surface.

## Recommended Next Step

If the goal is to keep expanding `stringarraydatas` as the owner of future
`StringArrayData` surfaces, the next meaningful step is:

1. choose the next real consumer surface
2. define its typed schema
3. persist it through canonical `StringArrayDatas`
4. add an addon-local DB-first runtime that reads from that table

The current repo state suggests the next suitable targets are surfaces that
still rely on legacy/global `StringArrayData` behavior rather than the already
migrated windows.
