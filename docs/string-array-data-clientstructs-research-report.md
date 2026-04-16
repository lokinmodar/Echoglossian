# StringArrayData and FFXIVClientStructs Research Report

## Purpose

This document records the current understanding of `StringArrayData` in
Echoglossian and the relevant write/read/update functions exposed by
`FFXIVClientStructs`.

The main goal is to answer three questions:

1. What does the game already expose for reading and setting string-array data?
2. Which of those APIs are good candidates for Echoglossian?
3. How should we use them to improve `RecommendList`, future
   `StringArrayData` processing, `ItemTooltips`, and `ActionTooltips` without
   creating another unstable translation path?

---

## Executive Summary

`FFXIVClientStructs` already exposes the important low-level primitives for
`StringArrayData`.

At a minimum, we already have direct access to:

- the global `AtkStage` array holder
- typed `StringArrayType` indices
- the backing `StringArrayData*`
- the write functions used by the game to set array entries
- addon subscription metadata
- addon request/update functions
- several typed wrappers or direct addon/agent pointers for specific string
  arrays

This means we do **not** need to reverse-engineer the entire write path from
scratch.

It also means the "hook the global write path" idea is technically plausible,
but it should be treated as a **separate architectural choice**, not as the
default next step. The primitives are already available without going global.

The safest direction for Echoglossian remains:

- **DB-first translation ownership**
- **typed/canonical slot schemas per string-array surface**
- **explicit write-back through `StringArrayData` only when data is already in
  the DB**

That keeps behavior predictable and avoids building a second translation system
that tries to infer context in a global hook.

---

## Current Repo State

The current repository historically touched `StringArrayData` in two older
paths. Those paths have since been removed from the live runtime, but they are
still worth documenting because they explain the architecture we are
replacing:

### `StringArrayDataHandler`

Legacy path:
- plugin-level global `StringArrayDataHandler` startup/runtime

What it does today:

- walks `AtkStage.Instance()->GetStringArrayData(type)` for many
  `StringArrayType` values
- extracts entries into:
  - original raw bytes
  - filtered plain-text dictionary
- attempts DB lookup by `StringArrayType`
- if a translated row exists, writes values back with `stringArrayData->SetValue`
- otherwise prepares and translates the serialized payload

What is good about it:

- captures both raw bytes and extracted text
- already understands that `StringArrayData` is not just loose addon text
- already persists a dedicated `stringarraydatas` table

What is weak about it:

- it is still a translation-first path instead of a DB-first path
- it serializes by index without a typed semantic schema
- it scans broadly and works globally rather than per-surface
- it can become noisy or unstable because the game may keep rewriting the same
  arrays
- it does not give us a canonical semantic model like the current quest work
  does

### `GenericAddonHandler.OnArrayDataUpdate`

Legacy path:
- `GenericAddonHandler.OnArrayDataUpdate(...)`

What it does today:

- identifies the subscribed string-array slot for an addon
- loads DB rows by `StringArrayType`
- parses serialized translated strings
- writes them with `addonArrayData->SetValue(index, value, suppressUpdates: true)`

What is good about it:

- it already uses the game's own `StringArrayData` write path
- it already keeps DB as a dependency in the apply stage

What is weak about it:

- it still depends on broad serialized string payloads
- it is not canonical or schema-driven
- it does not separate "capture schema" from "write-back schema"
- it is not yet aligned with the newer DB-first addon approach used in quests

What now exists to replace it:

- canonical `StringArrayDatas` persistence fields
- `StringArrayDataPersistenceHelper` for strict DB-first lookup/update
- dead global runtime types removed from the active build

---

## Core FFXIVClientStructs Primitives

These are the most relevant APIs currently exposed by `FFXIVClientStructs`.

### `AtkStage`

Source:
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Component/GUI/AtkStage.cs>

Relevant behavior:

- `AtkStage.Instance()`
- `GetStringArrayData()`
- `GetStringArrayData(StringArrayType type)`

Why it matters:

- this is the direct global read access point for `StringArrayData`
- it lets us pull a specific array by stable `StringArrayType`
- this is already enough to read many string-array surfaces without reflection

Practical use for Echoglossian:

- `RecommendList`
- `_ToDoList`
- `Hud`
- `ActionDetail`
- `ItemDetail`
- future typed `StringArrayData` surfaces

### `AtkArrayDataHolder`

Source:
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Component/GUI/AtkArrayDataHolder.cs>

Relevant behavior:

- `GetStringArrayData(int index)`
- `StringArrays`
- `StringArrayKeys`
- `StringArrayCount`

Why it matters:

- this is the backing holder for the stage arrays
- it gives us index-based access when we know the slot index
- it is useful when an addon is subscribed to an array but we are still
  learning the exact array index

Practical use for Echoglossian:

- introspection
- diagnostics
- schema discovery
- addon subscription cross-checking

### `AtkArrayData`

Source:
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Component/GUI/AtkArrayData.cs>

Relevant fields:

- `Size`
- `SubscribedAddonsCount`
- `UpdateState`
- `RefCount`

Why it matters:

- `UpdateState` tells us how the game requests subscriber refreshes
- subscriber information helps us understand which addon is consuming which
  array
- these fields explain why writes can appear to "stick" or be overwritten

Important semantics:

- `UpdateState = 0` means no update pending
- `UpdateState = 1` means update subscribed addons
- `UpdateState = 2` means force update subscribed addons

This is crucial when deciding whether to rely on natural repaint versus an
explicit request/update path.

### `StringArrayData`

Source:
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Component/GUI/StringArrayData.cs>

Relevant fields:

- `StringArray`
- `ManagedStringArray`

Relevant write functions:

- `SetValue(...)`
- `SetValueUtf8(...)`
- `SetValueIfDifferent(...)`
- `SetValueIfDifferentUtf8(...)`
- `SetValueForced(...)`
- `SetValueForcedUtf8(...)`
- `SetValueAndUpdate(...)`
- `SetValueAndUpdateUtf8(...)`

Why it matters:

- these are the actual write primitives exposed by the game layer
- they already support managed allocation and update signaling
- they are the most likely hook targets if we ever decide to instrument global
  writes

Important semantics:

- `managed = true` is the safe default when passing temporary C# strings
- `suppressUpdates = false` triggers subscribed addon refresh behavior when the
  value changed
- `SetValueIfDifferent` and `SetValueForced` are not equivalent
- `SetValueAndUpdate` is the explicit "write and request update" path

Recommendation for Echoglossian:

- prefer `SetValue(..., managed: true, suppressUpdates: <intentional>)` or
  `SetValueAndUpdate(...)`
- do not rely on unmanaged writes unless lifetime is completely under our
  control

### `AtkModuleInterface`

Source:
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Component/GUI/AtkModuleInterface.cs>

Relevant behavior:

- `GetStringArrayData(int index)`
- `ClearStringArrayData(int index)`
- `ResetStringArrayDataSubscribers(int index)`

Why it matters:

- this gives additional module-level control over string arrays
- it is useful for reset/cleanup scenarios
- it is also a reminder that array ownership is broader than a single addon

Recommendation for Echoglossian:

- treat `ClearStringArrayData` and `ResetStringArrayDataSubscribers` as
  dangerous, cleanup-oriented tools
- do not use them in normal translation flow

### `RaptureAtkModule`

Source:
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/RaptureAtkModule.cs>

Relevant behavior:

- `IncRefStringArrayData(int index)`
- `DecRefStringArrayData(int index)`

Why it matters:

- these functions manage ref-count semantics for string arrays
- they matter if we ever retain array pointers or build a longer-lived wrapper
  around them

Recommendation for Echoglossian:

- if we only read/write within valid lifecycle windows, we likely do not need
  to own references long-term
- if we build reusable runtime wrappers that survive beyond a short callback,
  these functions need to be part of the ownership model

### `AtkUnitBase` and `AtkUnitManager`

Sources:
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Component/GUI/AtkUnitBase.cs>
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Component/GUI/AtkUnitManager.cs>

Relevant behavior:

- `SubscribeAtkArrayData(byte arrayType, byte arrayIndex)`
- `UnsubscribeAtkArrayData(byte arrayType, byte arrayIndex, bool clean = false)`
- `OnRequestedUpdate(NumberArrayData** numberArrayData, StringArrayData** stringArrayData)`
- `AddonRequestUpdateById(...)`

Why it matters:

- this is the subscription/request-update layer that explains how arrays drive
  addon repaints
- it provides a more official route than "poke nodes and hope"

Recommendation for Echoglossian:

- if we apply DB-backed array translations, we should prefer natural addon
  update behavior or an intentional request-update path instead of brute-force
  frame reapplication

---

## Surface-Specific Hints Already Exposed by FFXIVClientStructs

### `_ToDoList`

Sources:

- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/AddonToDoList.cs>
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Arrays/ToDoListStringArray.cs>

Important details:

- `AddonToDoList.ObjectiveTimerTextNodeData` contains `StringArrayIndex`
- `ToDoListStringArray` already provides a typed wrapper over
  `StringArrayType.ToDoList`
- the wrapper defines structured regions such as:
  - queued duty text
  - quest texts
  - quest status messages
  - duty objectives
  - fate objectives

Why this is valuable:

- this is exactly the pattern we want for DB-first string-array work
- it gives semantic slot groupings instead of a flat "index -> string" map
- it proves that some arrays already have a stable typed schema in the structs

### `ActionDetail`

Source:
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentActionDetail.cs>

Important details:

- `AgentActionDetail` exposes:
  - `NumberArray`
  - `StringArray`
  - `HandleActionHover(...)`
- `StringArray` is explicitly documented as `StringArrayType.ActionDetail`

Why this is valuable:

- this is a strong signal for future `ActionTooltips`
- instead of guessing from rendered nodes alone, we can reason about the
  tooltip's own backing array

### `ActionBar`

Source:
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/AddonActionBarBase.cs>

Important details:

- `UpdateHotbarSlot(...)` accepts `StringArrayData*`
- `ShowTooltip(...)` accepts `StringArrayData*`

Why this is valuable:

- it tells us action-bar tooltip generation is already wired into a known array
  path
- it is relevant for future `ActionTooltips`

### `ItemDetail`

Source:
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/AddonItemDetail.cs>

Important detail:

- `UpdateGroupPositions(NumberArrayData* numberArray, StringArrayData* stringArray)`

Why this is valuable:

- even though `AddonItemDetailBase` is sparse, the full item-detail addon does
  show an explicit `StringArrayData*` dependency
- it supports the idea that `ItemTooltips` should be studied through their
  backing arrays, not only through visible text nodes

---

## Community Notes Worth Keeping

The community guidance shared in chat matches the structs surprisingly well.

Two especially useful takeaways:

1. Hooking the string-array write path is technically possible because
   `FFXIVClientStructs` exposes multiple setter variants on `StringArrayData`.
2. Some arrays already have stable, structured slot meanings, such as the
   `RecommendList` hints shared informally:
   - `StringArray[59]`
   - names, additional text, hovered text, and empty/default text regions

This community data should be treated as **high-value but still provisional**
until it is verified against a typed schema or direct capture.

For `RecommendList`, the user-provided notes were:

- `StringArray[59]`
  - `[0-9] = Name`
  - `[10-19] = Additional Text`
  - `[20-29] = Hovered Text`
  - `[30] = Default display text when empty`
- `NumberArray[63]`
  - `[0] = count`
  - `[1-10] = quest icon`
  - `[11-20] = additional icon`

This is exactly the kind of schema we should encode explicitly once validated.

---

## Should We Hook the Global StringArrayData Write Functions?

Short answer: **possible, but not the default next step**.

### Why It Is Attractive

- it would let us observe dynamic string writes in one place
- it could reduce flicker caused by surface-specific reapplication
- it would catch arrays written by game code before the addon redraw completes
- it could become a global capture pipeline for dynamic strings

### Why It Is Risky

- it becomes a second translation system unless carefully restricted
- many arrays are unrelated to the surface we want
- chat-like arrays and other noisy arrays would need exclusions
- write-time translation risks latency, reentrancy, and repeated work
- some arrays are rewritten frequently and context-dependent
- it makes "DB is the source of truth" harder to preserve
- hooking all setter variants adds maintenance burden

### Best Use of That Idea

If we explore this path, the safest first use is:

- **instrumentation or capture**
- not immediate translation mutation

That means:

- hook setters only to discover slot schemas, update cadence, and addon
  ownership
- log or map `StringArrayType + index + value`
- use that to define stable canonical schemas
- keep actual translation/write-back DB-first and surface-specific

This would give us the value of the hook without moving translation policy into
an unstable global interception layer.

---

## Recommended Architecture for Echoglossian

### 1. Treat each `StringArrayType` surface like a canonical data model

For example:

- `ToDoList`
- `RecommendList`
- `ActionDetail`
- `ItemDetail`
- selected `Hud` segments

Each one should get:

- a typed schema
- stable semantic keys
- explicit slot ownership

Not just:

- raw serialized `index -> string`

### 2. Keep DB-first ownership

The array write path should not become the place where translation is invented.

Instead:

- capture or resolve source data
- persist canonical original content
- translate in controlled background flow
- write back only when the DB already contains what the surface needs

This matches what is now working for quests.

### 3. Build typed wrappers where FFXIVClientStructs already points the way

Good candidates:

- `ToDoList`
- `RecommendList`
- `ActionDetail`
- `ItemDetail`

If the structs repo already provides a typed wrapper, reuse it.

If it does not, create a repo-local schema wrapper based on:

- stable `StringArrayType`
- verified slot layout
- optional `NumberArrayType` companion layout

### 4. Use the game write APIs intentionally

Default recommendation:

- `SetValue(..., managed: true, suppressUpdates: true)` when a natural update is
  already happening
- `SetValueAndUpdate(...)` when we truly need to request addon refresh

Use `SetValueForced` only when we really mean to override pointer behavior and
understand the lifetime implications.

### 5. Avoid global translation hooks as a primary product path

For production translation behavior, prefer:

- canonical typed model
- DB-backed translated payload
- explicit addon/runtime apply step

The global hook idea is more promising as:

- discovery
- diagnostics
- schema acquisition

than as the first stable runtime translation path.

---

## What This Means for RecommendList

`RecommendList` should probably move in this order:

1. verify the user/community-provided `StringArray[59]` and `NumberArray[63]`
   schema in live capture
2. define a typed canonical schema in the repo
3. persist the structured slots in DB
4. translate in background
5. apply only when the DB contains the complete visible payload

That is the same philosophy now used for `_ToDoList` and `ScenarioTree`, but
with typed string-array slots instead of quest-canonical rows.

---

## What This Means for ItemTooltips and ActionTooltips

These surfaces are likely better served by backing-array or agent-driven
capture than by text-node scraping alone.

Especially promising:

- `AgentActionDetail.StringArray`
- `AddonActionBarBase.ShowTooltip(...)`
- `AddonItemDetail` functions that already accept `StringArrayData*`

The likely long-term win is:

- capture at the array/agent layer
- preserve structured slot meaning
- translate only the meaningful payload
- write back through the existing string-array setters

---

## Proposed Next Steps

1. Add a lightweight diagnostic script or helper to dump:
   - `StringArrayType`
   - array size
   - visible string contents
   - update cadence
   - subscribed addon IDs
2. Validate the `RecommendList` slot map and create a repo-local typed schema.
3. Design a canonical DB model for structured string-array surfaces:
   - type
   - semantic key
   - slot index
   - original text
   - translated text
   - payload-aware serialized form when needed
   - game version
   - translation language
   - translation engine
4. Treat future `StringArrayData` work the same way quests were normalized:
   - canonical source
   - background translation
   - DB-first apply
5. If discovery stalls, prototype a **capture-only** hook on
   `StringArrayData.SetValue*` to learn slot schemas without turning it into a
   global translation engine.

---

## Sources

### Repository Sources

- [StringArrayDatas.cs](/C:/Dante/_dalamud/Echoglossian/EFCoreSqlite/Models/StringArrayDatas.cs)
- [StringArrayDataPersistenceHelper.cs](/C:/Dante/_dalamud/Echoglossian/DBHelpers/StringArrayDataPersistenceHelper.cs)
- [StringArrayStructuredPayload.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/StringArrayStructuredPayload.cs)
- [StringArrayStructuredPayloadResolver.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/StringArrayStructuredPayloadResolver.cs)
- [IStringArrayStructuredSchema.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/IStringArrayStructuredSchema.cs)
- [StringArrayStructuredPayloadBuilder.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/StringArrayStructuredPayloadBuilder.cs)
- [DbFirstGameWindowAddonHandler.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs)
- [GenericAddonHandlerHelper.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/GenericAddonHandlerHelper.cs)
- [structured-text-payload-pipeline.md](/C:/Dante/_dalamud/Echoglossian/docs/structured-text-payload-pipeline.md)

### FFXIVClientStructs Sources

- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Component/GUI/StringArrayData.cs>
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Component/GUI/AtkArrayData.cs>
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Component/GUI/AtkArrayDataHolder.cs>
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Component/GUI/AtkStage.cs>
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Component/GUI/AtkModuleInterface.cs>
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Component/GUI/AtkUnitBase.cs>
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Component/GUI/AtkUnitManager.cs>
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/RaptureAtkModule.cs>
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/AddonToDoList.cs>
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Arrays/ToDoListStringArray.cs>
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/AddonActionBarBase.cs>
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentActionDetail.cs>
- <https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/AddonItemDetail.cs>
