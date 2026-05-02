# Quest Addon Translation — Runtime Flow

## Purpose

This document describes the **current actual** runtime translation flow for every quest-family addon in Echoglossian. It does not describe the intended future pipeline (see `quest-full-pipeline-design.md` for that). It describes what the code does today, how each addon is triggered, how text is resolved, how the DB is queried, how caches work, and how hover tooltips are registered.

## Current Stabilization Scope

At the current checkpoint, the quest-family runtime is intentionally narrowed:

- `Journal` and `JournalDetail` remain active as separate handler/runtime
  surfaces
- the other quest-family handlers remain in the repo but are not currently
  registered
- accepted-quest prefetch remains active so the DB can stay warm

This document still records the wider handler model for reference, but in live
runtime validation the active stabilization target is now the Journal family.

---

## Addons in scope

| Addon internal name | Config flag                          | Wiring file                  |
|---------------------|--------------------------------------|------------------------------|
| `Journal`           | `TranslateJournal`                   | `AddonHandlerWiring.cs`      |
| `JournalDetail`     | `TranslateJournalDetail`             | `AddonHandlerWiring.cs`      |
| `JournalAccept`     | `TranslateJournalAccept`             | `AddonHandlerWiring.cs`      |
| `JournalResult`     | `TranslateJournalResult`             | `AddonHandlerWiring.cs`      |
| `RecommendList`     | `TranslateRecommendList`             | `AddonHandlerWiring.cs`      |
| `ScenarioTree`      | `TranslateScenarioTree`              | `AddonHandlerWiring.cs`      |
| `AreaMap`           | `TranslateAreaMap`                   | `AddonHandlerWiring.cs`      |
| `_ToDoList`         | `TranslateToDoList`                  | `AddonHandlerWiring.cs`      |

Registrations are assembled in `NativeUI/Helpers/AddonHandlerWiring.cs`, registered through `NativeUI/Helpers/AddonHandlerRegistrar.cs`, and unregistered from `Echoglossian.cs` during plugin teardown.

The quest-family handlers described in this document now live as standalone handlers under `NativeUI/AddonHandlers/Quest/`. The migration plan for that structure is documented in [Quest Addon Handler Migration Guide](./quest-addon-handler-migration-guide.md).

---

## Translation display modes

Every addon family has its own `JournalTranslationDisplayMode`-typed config property. The shared helper properties are computed in `NativeUI/AddonHandlers/Quest/QuestAddonModeHelpers.cs`:

| Mode enum value                          | `WritesNative` | `UsesHoverTooltips` | `HoverShowsOriginal` |
|------------------------------------------|:--------------:|:-------------------:|:--------------------:|
| `NativeUiTranslation` (0)                | ✓              | ✗                   | ✗                    |
| `TooltipTranslation` (1)                 | ✗              | ✓                   | ✗ (shows translated) |
| `NativeUiTranslationWithOriginalTooltips` (2) | ✓         | ✓                   | ✓ (shows original)   |

The `ShouldDrawHoverTooltips` property in the same file aggregates all families
to decide whether `hoverTooltipManager.Draw()` should run at all each frame.
`JournalDetail` now participates in that aggregation independently from the
Journal list.

---

## Cache tiers

### 1. `QuestUiTranslationCache` (static, session-scoped)

- **Key:** the applied/visible text string (translated text or original if no translation yet)
- **Value:** `QuestUiTranslationSnapshot { OriginalText, AppliedText, LastUpdatedUtc }`
- **Purpose:** skip addon nodes whose text is already on screen — avoids re-querying DB and re-translating for each refresh cycle
- **Keyed by text**, not by node or quest identity — the same translated string from different quests produces only one entry
- `Remember(original, applied)` writes to it; `TryGetAppliedSnapshot(appliedText, out snapshot)` reads from it
- **Does not expire automatically** during a session; cleared on plugin dispose or explicit clear

### 2. `QuestHoverTranslationCache` (static, session-scoped)

- **Key:** node pointer (`nint`) of the `AtkTextNode*` used to anchor the tooltip
- **Value:** `QuestHoverTranslationSnapshot { OriginalText, TranslatedText }`
- **Purpose:** stable per-node hover translation memory so the tooltip can be re-registered on refresh without looking up the DB again
- Used by `Journal` quest list (`JournalList-*` keys) and `RecommendList`
- `Remember(nodePtr, original, translated)` writes; `TryGet(nodePtr, out snap)` reads
- When several tooltip targets overlap, the hover manager now prefers the
  smallest hovered rectangle. This matters for dense quest rows where a broad
  title or row trigger can otherwise swallow a narrower objective trigger.

### 2a. Journal-family local runtime caches (instance-scoped)

- `JournalHandler` now keeps its own local runtime state for the surfaces that
  repaint the most and were shown to regress when coupled to the broader
  quest-family caches.
- Journal list:
  - `journalListTextCache`
  - `journalListHoverCache`
  - these are trimmed to the currently visible quest names and node anchors at
    the end of each Journal list scan
- `JournalDetailHandler` now keeps its own local runtime state, separate from
  the Journal list handler:
  - `journalDetailTextCache`
  - `journalDetailOriginalCache`
  - `currentJournalDetailScopeKey`
- These caches are cleared when `Journal` or `JournalDetail` closes.
- Shared infrastructure still remains shared:
  - SQLite `questplates`
  - Lumina quest-sheet resolution
  - live quest progress resolution
  - queued translation broker

### 3. In-memory translation queue (instance-scoped)

- Managed by `TryGetQueuedTranslation(cacheKey, out text)` / `QueueTranslation(key, workFn, persistFn)`
- Acts as the pending translation buffer — when a translation is queued but not yet complete, the handler returns early; on the next refresh cycle the handler calls `TryGetQueuedTranslation` again and finds the result
- `QueueTranslationBatch(key, sources, persistFn)` is used by `JournalDetail` for multi-field batches (name + message + objective + summary + summaries)
- The DB `InsertQuestPlate` / `UpdateQuestPlate` call is **inside the persist callback**, fired after translation completes asynchronously
- In the current Journal-family stabilization pass, this queue is still used by
  accepted-quest prefetch and other quest-family code paths that remain in the
  repo, but `Journal` and `JournalDetail` themselves are being treated as
  DB-first consumers and no longer enqueue local quest translation work.

### 4. SQLite DB (`questplates` / `QuestPlate` model)

- Permanent store, survives sessions and game restarts
- Lookup methods:
  - `FindQuestPlate(plate)` — by `QuestName + QuestMessage` (full content match)
  - `FindQuestPlateByName(plate)` — by `QuestName` only (looser, used when message is not available at lookup time)
- `InsertQuestPlate`, `UpdateQuestPlate`, `UpdateQuestPlateGameVersion` mutate it
- `SourceContentHash` (from `QuestProgressSnapshot`) is stored per plate to detect quest text changes across game patches without a full retranslation

---

## Per-addon flow

---

### `Journal` (quest list panel)

**Trigger events:**
- `AddonEvent.PreUpdate` → `JournalHandler.OnJournalQuestEvent` → `TranslateJournalQuests()`
- `AddonEvent.PreRequestedUpdate` → same

**What fires on each event:** `TranslateJournalQuests()` scans all visible quest name nodes in the sidebar list (NodeId 25 → component list). For each visible quest name node:

1. Read text from `AtkTextNode`.
2. Add the quest name and node pointer to the current visible Journal-list
   snapshot.
3. Check the Journal-local visible-list cache:
   - **Cache hit:** if `WritesNative`, write the translated text into the node;
     refresh the Journal-local hover snapshot; register the hover tooltip; `continue`.
   - **Cache miss:** proceed.
4. Build a name-only `QuestPlate` and call `FindQuestPlateByName`.
5. **DB hit:** use `TranslatedQuestName`; write to node if `WritesNative`;
   remember the translated title in the Journal-local cache and local hover
   cache; register hover tooltip; `continue`.
6. **DB miss:** keep the original title in the native node when needed and
   register hover only according to the current hover-mode readiness rule. No
   Journal-local translation is queued anymore.
7. At the end of the scan, trim the Journal-local visible-list caches to only
   the quest names and node anchors that were visible in that pass.

**DB table used:** `questplates` — by name only via `FindQuestPlateByName`.

---

### `JournalDetail` (quest body panel — active quest)

**Trigger events:**
- `AddonEvent.PreUpdate` on `"JournalDetail"` → `JournalDetailHandler.OnJournalDetailEvent` → `TranslateJournalDetail()`
- `AddonEvent.PreRequestedUpdate` on `"JournalDetail"` → same
- `AddonEvent.PostRequestedUpdate` on `"JournalDetail"` → same
- `AddonEvent.PreUpdate` / `PreRequestedUpdate` / `PostRequestedUpdate` on `"Journal"` → same handler instance, used as a selection-driven refresh path
- `AddonEvent.PreHide` / `AddonEvent.PreFinalize` on `"JournalDetail"` and `"Journal"` → `JournalDetailHandler.OnJournalDetailCleanupEvent`

**Config surface:**
- `TranslateJournalDetail`
- `JournalDetailTranslationDisplayMode`

**Runtime file:**
- `NativeUI/AddonHandlers/Quest/JournalDetailHandler.cs`

**What fires:** `TranslateJournalDetail()` calls `TranslateJournalBox` first; if no active quest node found, falls back to `TranslateCompletedQuest`.

**`TranslateJournalBox` flow:**

1. Read `questName` (node 38), `questMessage` (node 43→comp→node 8), `objectiveText` (node 43→comp→node 12→comp→node 3), and the visible summary nodes only as UI anchors / original-text snapshots.
2. Build `QuestPlate`; run `QuestProgressResolver.TryResolveQuestProgress` → attach `SourceContentHash` to plate.
3. Build a JournalDetail-local scope key from the progress snapshot when
   available, with a `questName|questMessage` fallback.
4. Preserve a JournalDetail-local original snapshot for that scope so mode
   switches can restore native text correctly.
5. `FindQuestPlate(plate)` — by name + message.
6. If found and `GameVersion` stale → `UpdateQuestPlateGameVersion`
   (non-blocking).
7. Ensure metadata such as `QuestId`, `QuestTextSheetName`, and
   `SourceContentHash` is persisted when the DB row exists but is still thin.
8. Resolve translated title, description, objective, and summary-like body from:
   - JournalDetail-local runtime cache
   - persisted `QuestPlate`
   - canonical current `SEQ` row from `QuestProgressSnapshot` for the description
   - canonical prior `SEQ` rows up to the current sequence for the summary block
   - original UI fallback only when canonical/DB data is still unavailable
9. Apply `RemoveDiacritics` if configured.
10. If `WritesNative`: `SetText` on the visible detail nodes.
11. Remember the translated values in the JournalDetail-local cache.
12. If `UsesHoverTooltips`:
    - Register `JournalDetail-QuestName-{nodePtr}` on the name node.
    - Build `originalQuestBody` and `translatedQuestBody` from the current three-part shape:
      - description: current canonical `SEQ` row
      - objective: current visible objective text (until TODO selection becomes fully canonical)
      - summary: canonical prior `SEQ` rows up to the live quest sequence
    - Register `JournalDetail-QuestBody-{nodePtr}` using bounds that start from `JournalCanvasComponentNode` and then expand to include the visible description, objective, and summary nodes only.
    - The additional visible Journal summary nodes are now treated as presentation anchors only:
      - they are snapshotted so original mode can restore them
      - their translated/native content is assigned from canonical prior `SEQ` rows

**`TranslateCompletedQuest` flow:** same as above but reads only name + message (no objective/summary), uses `JournalDetail-CompletedQuestName-*` and `JournalDetail-CompletedQuestMessage-*` + `JournalDetail-CompletedQuestBody-*` hover keys.

**DB table used:** `questplates` — by `QuestName + QuestMessage` via
`FindQuestPlate`.

**Important current rule:** `JournalDetail` no longer generates or queues local
quest translation work. It is now a DB-first consumer with original-text
fallback, and that is intentional while this surface is being stabilized.

---

### `JournalAccept` (quest accept dialog)

**Trigger event:**
- `AddonEvent.PreSetup` → `JournalAcceptHandler.OnJournalAcceptEvent`

**Why `PreSetup`:** this fires once when the addon is being built, before any node text is visible. The `AtkValues` array passed in `AddonSetupArgs` contains the raw quest data before it is written to nodes.

**Flow:**

1. Guard: `args is not AddonSetupArgs` → return.
2. Read `questName` from `setupAtkValues[5]`, `questMessage` from `setupAtkValues[12]`.
3. Build `QuestPlate`; run `QuestProgressResolver.TryResolveQuestProgress` → `SourceContentHash`.
4. `FindQuestPlate` → `GameVersion` check.
5. **Cache check:** `QuestUiTranslationCache.TryGetAppliedSnapshot(questName) && TryGetAppliedSnapshot(questMessage)`:
   - **Hit:** capture both `AppliedText` snapshots; if `UsesHoverTooltips` register `JournalAccept-{addonPtr}`; `return`.
   - **Miss:** continue.
6. If `foundQuestPlate` not null → use `TranslatedQuestName`, `TranslatedQuestMessage` directly.
7. If null → `TryGetQueuedTranslation($"JournalAccept|{questName}|{questMessage}")`:
   - Hit: decode pair via `TryDeserializeTranslationPair`.
   - Miss: `QueueTranslation` (pair, persist InsertQuestPlate); `return`.
8. Diacritics strip if configured.
9. If `WritesNative`: mutate `setupAtkValues[5]` and `[12]` with `SetManagedString` (modifies the `AtkValue` array directly, so native nodes pick up the translation on setup).
10. `Remember` both texts in `QuestUiTranslationCache`.
11. If `UsesHoverTooltips`: register `JournalAccept-{addonPtr}` anchored to the whole addon window.

**Important:** `AtkValueArray` mutation at `PreSetup` means translation happens before the addon draws its first frame — there is no screen flash of original text.

**DB table used:** `questplates` — `FindQuestPlate` (name + message).

---

### `JournalResult` (quest completion result screen)

**Trigger event:**
- `AddonEvent.PreSetup` → `JournalResultHandler.OnJournalResultEvent`

**Flow:**

1. Guard: `args is not AddonSetupArgs`; guard: `setupAtkValues[1].Type != ValueType.String`.
2. Read `questNameText` from `setupAtkValues[1]`.
3. **Cache check:** `QuestUiTranslationCache.TryGetAppliedSnapshot(questNameText)`:
   - **Hit:** if `UsesHoverTooltips` register `JournalResult-{addonPtr}`; `return`.
   - **Miss:** continue.
4. Build name-only `QuestPlate`; `FindQuestPlateByName`.
5. **DB hit:** diacritics strip if needed; `SetManagedString(setupAtkValues[1])` if `WritesNative`; `Remember`; if `UsesHoverTooltips` register; `return`.
6. **DB miss:** `TryGetQueuedTranslation($"JournalResult|{questNameText}")`:
   - Hit: same steps as DB hit path.
   - Miss: `QueueTranslation` (persist InsertQuestPlate); return (no tooltip yet).

**DB table used:** `questplates` — `FindQuestPlateByName` (name only).

---

### `RecommendList` (recommended quests panel)

**Trigger events (all three register the same handler or its async variant):**
- `AddonEvent.PostReceiveEvent` → `RecommendListHandler.OnRecommendListEvent` → `TranslateRecommendListHandler()`
- `AddonEvent.PreRequestedUpdate` → same
- `AddonEvent.PreDraw` → `RecommendListHandler.OnRecommendListHoverRefreshEvent` → `RefreshRecommendListHoverTooltips()` (hover maintenance only)
- `AddonEvent.PostRequestedUpdate` → `RecommendListHandler.OnRecommendListEventAsync` → `Task.Delay(200).ContinueWith(TranslateRecommendListHandler)` (zone-change delay guard)

**Why three events:** `PostReceiveEvent` catches user interactions; `PreRequestedUpdate` catches server-push refreshes; the async variant catches zone transitions where node layout may not be settled yet.

**`TranslateRecommendListHandler` flow (two-pass):**

**Pass 1 — queue/translate:**
1. Iterate visible quest name nodes (NodeId 5 → list component → child items → NodeId 5 text node).
2. Read `questNameText` and `questNameNodeKey = (nint)questNameNode`.
3. If `UsesHoverTooltips`: pre-register `(original, original)` — ensures a tooltip entry exists even during first-time translation.
4. `QuestUiTranslationCache.TryGetAppliedSnapshot`:
   - **Hit + `QuestHoverTranslationCache.TryGet(nodePtr)`:** register with cached pair (full params including `swapEnabled`/`forceEnabled`/`denseHitbox`); `continue`.
   - **Hit, no hover cache:** register from `QuestUiTranslationCache` snapshot; `continue`.
   - **Miss:** proceed.
5. `FindQuestPlateByName`:
   - Found: write node if `WritesNative`; `Remember` in both caches; register hover; `continue`.
   - Not found: `TryGetQueuedTranslation($"RecommendList|{questNameText}")`:
     - Hit: write node if `WritesNative`; populate caches; register hover; `continue`.
     - Miss: `QueueTranslation`; `continue`.

**Pass 2 — `UpdateRecommendList()`:**
Identical traversal after pass 1. Re-reads all nodes and rewrites from `QuestUiTranslationCache` + `QuestHoverTranslationCache`. This ensures that translations that arrived from the async queue during pass 1 are visible immediately without waiting for the next event cycle.

**Hover maintenance — `RefreshRecommendListHoverTooltips()`:**
- Runs on `PreDraw`.
- Re-scans only the visible quest name nodes.
- Re-registers tooltip targets from `QuestHoverTranslationCache`, `QuestUiTranslationCache`, or the persisted `QuestPlate` row.
- Does **not** queue new translations and does **not** mutate native text.

**DB table used:** `questplates` — `FindQuestPlateByName` (name only).

---

### `ScenarioTree` (main scenario quest tracker)

**Trigger events:**
- `AddonEvent.PreRefresh` → `ScenarioTreeHandler.OnScenarioTreeEvent` → `RefreshScenarioTree()`
- `AddonEvent.PreRequestedUpdate` → same
- `AddonEvent.PreDraw` → `ScenarioTreeHandler.OnScenarioTreePreDrawEvent`
  → retry / hover refresh only

**Args type:** the handler now resolves the live visible addon directly from
`AtkStage` on each pass, so activation and retry behavior are no longer tied to
whether the lifecycle event carries `AddonRefreshArgs`.

**`RefreshScenarioTree()` flow:**

The handler inspects the visible quest slots at value indices:

- `7` (MSQ entry)
- `2` (sub-quest entry)

For each visible slot:

1. Read the visible quest name string from the live `AtkValue`.
2. Recover the original source text from the local runtime state if the addon
   was previously written in native mode.
3. `QuestTodoProgressResolver.TryResolveQuestTodoProgress` → canonical
   progress snapshot.
4. Build `QuestCanonicalData` from live progress.
5. Project a canonical `QuestPlate` lookup and resolve it through
   `FindQuestPlate(...)`.
6. Require `TranslatedQuestName` to already exist in the DB.

If any visible quest slot fails resolution or is still missing translated DB
data:

- restore original ScenarioTree text
- remove ScenarioTree hover targets
- leave the addon untouched
- emit one notification per waiting episode explaining that ScenarioTree is
  waiting for stored quest data

If all visible slots are ready:

- native mode writes only DB-backed translated quest names
- tooltip modes use only DB-backed translated quest names
- no translation is queued or performed from the addon

**Hover maintenance:**
- Every resolved slot updates a local runtime entry keyed by quest progress and
  addon value index.
- `PreDraw` combines the visible MSQ/subquest entries into one tooltip payload
  and re-registers it on the addon root.
- This path is read-only and never queues translation work.

**Key difference from other addons:** text is still written via
`SetManagedString` directly into the `AtkValue*` array, so the game's own node
layout picks up the translated string without the handler touching node
pointers directly. The content source, however, is now DB-only.

**DB table used:** `questplates` — canonical lookup via `FindQuestPlate(...)`.

---

### `AreaMap` (quest tracker inside the map window)

**Trigger events:**
- `AddonEvent.PreRefresh` → `AreaMapHandler.OnAreaMapEvent`
- `AddonEvent.PreRequestedUpdate` → same
- `AddonEvent.PreDraw` → `AreaMapHandler.OnAreaMapHoverRefreshEvent` (hover maintenance only)

**Args type:** primarily `AddonRefreshArgs`, with a live-addon fallback for
`PreRequestedUpdate` — if the requested-update event does not carry refresh
args, the handler resolves `AtkUnitBase->AtkValues` from the visible addon.

**Flow:**

1. Read the quest name from `setupAtkValues[142]`.
2. Resolve translation from `QuestUiTranslationCache`, `QuestPlate`, or the queued-translation cache.
3. If `WritesNative`, apply the translated quest name back to `setupAtkValues[142]`.
4. Always remember the latest `(original, translated)` pair for hover maintenance.
5. `PreDraw` re-registers a whole-addon tooltip from that remembered pair without queueing new translation work.

**DB table used:** `questplates` — `FindQuestPlateByName` (name only).

---

### `_ToDoList` (active quest objective list)

**Trigger events:**
- `AddonEvent.PostRequestedUpdate` → `ToDoListHandler.OnToDoListEvent` → `RefreshToDoList()`
- `AddonEvent.PreRequestedUpdate` → same
- `AddonEvent.PreDraw` → retry activation / refresh hover targets without any
  translation work

**Why both Pre and Post:** `PreRequestedUpdate` fires before game updates node
text; `PostRequestedUpdate` fires after. Using both catches different
game-driven refresh cycles while staying read-only with respect to quest
translation.

**`RefreshToDoList()` scan phase:**

1. Walk `todoList->UldManager.NodeList`. Skip invisible nodes, `Collision`/`Res` type nodes, fate nodes (NodeId 8, 9).
2. For each visible component node, walk its child `Text` nodes.
3. Skip empty nodes, time-format strings.
4. Classify each visible text node by NodeId:
   - `NodeId > 60000` or `(NodeId == 4 && childNodeId == 3)` or `(NodeId == 6 && childNodeId == 2)` → quest name candidate (`questNamesToTranslate`)
   - `NodeId == 4 || NodeId == 5` → level-quest objective (`levelQuestObjectivesToTranslate`)
   - Otherwise → objective (`objectivesToTranslate`)
5. Skip if `questNamesToTranslate` is empty after full scan.

**Resolve phase (DB-first, no local translation):**

For each quest name entry:

1. Associate objectives using `GetQuestObjectives` (adjacent NodeId heuristic).
2. `QuestTodoProgressResolver.TryResolveQuestTodoProgress` → `questTodoProgressKey`.
3. Build `QuestCanonicalData` from `QuestManager + live progress`.
4. Project a canonical `QuestPlate` lookup and resolve it through
   `FindQuestPlate(...)`.
5. Require all visible payloads to already exist in the DB:
   - translated quest name
   - translated objective rows for every tracked visible objective
6. If any required payload is missing:
   - restore original ToDoList text
   - remove ToDoList hover targets
   - keep the addon untouched
   - emit one notification per waiting episode that the ToDoList is waiting
     for stored quest data
7. If all visible quest rows are ready:
   - native mode writes only DB-backed translated text
   - tooltip modes register only DB-backed hover payloads
   - no translation is queued or performed from the addon

**`RegisterToDoTooltip`:** inner helper that stores a stable row hover payload and registers it from explicit screen bounds computed as the union of:
- the full visible row node
- the inner text node

The stable key is `ToDoList-{progressKey}-{indexI}-{indexJ}-{nodeId}`. A lightweight `PreDraw` pass refreshes those row targets without queueing new translations.

**DB tables used:** `questplates` — canonical lookup via `FindQuestPlate(...)`;
objectives are resolved from canonical TODO rows and persisted translated
objective rows keyed by quest row identity.

---

## Hover tooltip registration summary

All hover registrations go through `RegisterTranslatedHoverTooltip` (in `NativeUI/Helpers/HoverTooltipRegistration.cs`) → `RegisterHoverTooltip` → `hoverTooltipManager.Register(key, topLeft, bottomRight, title, body)`.

`HoverTooltipManager` (`NativeUI/Helpers/HoverTooltipManager.cs`):
- Internal store: `ConcurrentDictionary<string, HoverTooltipEntry>` keyed by string key.
- Registering with an existing key **overwrites** in place (no accumulation).
- `Remove(key)` removes by exact key.
- Stale entries (not hovered for >30 s) are pruned in `Draw()`.
- `Draw()` is called each frame via ImGui if `ShouldDrawHoverTooltips`.

Key patterns per addon:

| Addon       | Key pattern                                             | Anchor                  |
|-------------|--------------------------------------------------------|-------------------------|
| Journal list | `JournalList-{questNameNodePtr:X}`                   | `AtkTextNode*`          |
| JournalDetail name | `JournalDetail-QuestName-{nameNodePtr:X}`      | `AtkTextNode*`          |
| JournalDetail body | `JournalDetail-QuestBody-{canvasOrDescNodePtr:X}` | explicit bounds rect |
| JournalDetail completed name | `JournalDetail-CompletedQuestName-{ptr:X}` | `AtkTextNode*` |
| JournalDetail completed body | `JournalDetail-CompletedQuestBody-{ptr:X}` | explicit bounds rect |
| JournalAccept | `JournalAccept-{addonPtr:X}`                         | `AtkUnitBase*`          |
| JournalResult | `JournalResult-{addonPtr:X}`                         | `AtkUnitBase*`          |
| ScenarioTree | `ScenarioTree-{addonPtr:X}`                            | `AtkUnitBase*`        |
| AreaMap      | `AreaMap-{addonPtr:X}-142`                             | `AtkUnitBase*`        |
| ToDoList     | `ToDoList-{progressKey}-{i}-{j}-{nodeId}` | explicit row bounds |
| RecommendList | `RecommendList-{questNameNodePtr:X}`                 | `AtkTextNode*`          |

---

## Accepted quest background prefetch

Quest-family addons now have a background prefetch path that is intentionally
separate from addon-local hover/runtime caches.

**Entry point:** `Echoglossian.Tick(IFramework)` in
`PluginUI/PluginRuntimeUi.cs`

**Implementation:** `NativeUI/Helpers/AcceptedQuestPrefetchRuntime.cs`

### Purpose

Warm canonical quest data in `questplates` before quest-family addon surfaces
need to render it.

This reduces the amount of “discover, resolve, queue, and save” work that needs
to happen during the first open of:

- `Journal`
- `JournalDetail`
- `_ToDoList`
- `ScenarioTree`
- `RecommendList`
- `AreaMap`

### Scope

The prefetch runtime is **shared data prewarm**, not shared addon UI state.

It may populate:

- `questplates`
- brokered translation cache
- canonical quest metadata derived from Lumina/live progress

It does **not** populate or own:

- addon-local hover targets
- addon-local applied-text caches
- addon-local bounds/trigger heuristics

Those remain the responsibility of each quest-family addon handler.

### Gating

The prefetch runtime only runs when:

- global translation is enabled
- the player is logged in
- at least one quest-family addon feature is enabled in config

### Data source

Accepted quests are collected from `QuestManager`, then resolved through the
existing `QuestProgressResolver` pipeline. That means the prefetch path uses the
same stable inputs as the sheet-first quest work:

- live accepted-quest identity from `QuestManager`
- current quest progress from runtime state
- Lumina quest-sheet metadata and text rows

### Pacing

The runtime is intentionally slow and quiet:

- one prefetch cycle every `2` seconds
- up to `2` quests processed per cycle
- only when the accepted-quest signature changes or the queue still has quests
  left to process

This keeps prewarm work from turning into a hot-path burst when the player logs
in or opens a dense quest UI.

### Persistence behavior

For each accepted quest, the prefetch runtime seeds or updates a canonical
`QuestPlate` row with:

- `QuestId`
- `QuestName`
- `QuestTextSheetName`
- `SourceContentHash`
- current SEQ row in `OriginalQuestMessage`
- summary rows
- objective rows
- system rows

Missing translations are queued through the existing paced broker and applied
back into the same canonical row shape once they resolve.

### Relationship to addon handlers

Quest-family addon handlers should assume that the DB may already be warm, but
they must still tolerate cache misses and late-arriving translations.

The intended division of responsibility is:

- **Prefetch runtime:** accepted-quest discovery and background DB/broker warmup
- **Addon handler:** local capture, local hover registration, local native write
  decisions, and addon-specific runtime caches

---

## Addon-local quest runtime caches

The quest-family refactor now treats UI-facing runtime state as **per-addon
state**, even when the canonical data sources remain shared.

The shared layers are still:

- `questplates`
- Lumina/sheet resolvers
- live quest-progress resolvers
- the brokered translation queue

But the following handlers now keep their own local UI/runtime state instead of
depending on the broader quest-family UI caches:

- `Journal`
- `JournalDetail`
- `ScenarioTree`
- `RecommendList`
- `AreaMap`

### Why this matters

The quest-family surfaces repaint differently and have different hover
geometries. When they all depend on the same UI-layer text/hover caches, one
surface can make another harder to reason about during validation.

The current direction is:

- share **data sources**
- isolate **runtime presentation state**

### Local cache responsibilities

Handler-local runtime caches may hold:

- last visible translated text for that addon
- last visible hover payloads for that addon
- addon-specific progress/body composition state

Handler-local runtime caches should **not** replace:

- canonical DB persistence
- Lumina quest snapshots
- shared translation queueing

### Current Journal mode rule

The `Journal` / `JournalDetail` stabilization pass is currently in a good
state on this branch. The readiness rule below remains the intended contract,
but it is no longer the active refactor front. Current tooltip-runtime work is
focused on `ActionDetail` / `ItemDetail`.

For the stabilized `Journal` / `JournalDetail` path, hover tooltips must obey
a strict readiness contract:

- native-only mode: no hover tooltip
- tooltip-translation mode: no tooltip until translated payload is ready
- swap mode: no original-text tooltip until the translated/native payload is
  ready

This avoids misleading fallback where a “translation tooltip” still shows
original text just because the DB row is not warm enough yet.

### Current isolated handlers

**Journal / JournalDetail**

- Local visible-list cache for the current Journal list.
- Local scope cache for `JournalDetail`.

**ScenarioTree**

- Local translated-text cache keyed by progress-aware quest identity.
- Local hover payload entries per visible value slot.

**RecommendList**

- Local translated-text cache keyed by visible quest name.
- Local hover payload entries keyed by visible node pointer.

**AreaMap**

- Local translated-text cache keyed by the current AreaMap quest row.
- Local hover payload state refreshed from the handler-local cache.

---

## DB lookup method reference

| Method                    | Match criteria                   | Used by                                   |
|---------------------------|----------------------------------|-------------------------------------------|
| `FindQuestPlate(plate)`   | `QuestName + QuestMessage`       | JournalDetail, JournalAccept              |
| `FindQuestPlateByName(plate)` | `QuestName` only            | Journal list, JournalResult, ScenarioTree, ToDoList, RecommendList |

`FindQuestPlateByName` is inherently looser — it cannot distinguish two quests that share a name but differ in body text. This is a known gap tracked in `quest-full-pipeline-design.md` under the migration to `QuestId`-keyed lookups.

---

## Gap: text source for objectives and summaries

Current behavior for `JournalDetail` and `ToDoList` objectives: the text is captured **from the live UI nodes** (`AtkTextNode->NodeText`), not from Lumina quest sheets. This means:

- If the UI has already been translated by a previous handler cycle, the captured text may be the translated form, not the Japanese/English original.
- Objectives in `QuestPlate.Objectives` are stored as `{original UI text} → {translated text}` dict entries, which are fragile to UI text changes across game patches.

The intended fix (described in `quest-full-pipeline-design.md`) is to capture objectives from `_TODO_NN` rows in the quest text sheet via `QuestProgressSnapshot.QuestSteps`, keyed by stable row key rather than raw UI text. That migration is not yet implemented.

---

## JournalDetail persistence alignment update

`JournalDetail` now follows this runtime/persistence contract more closely:

- The tooltip description uses `QuestPlate.TranslatedQuestMessage` when available.
- The current SEQ row from `QuestProgressSnapshot` is translated and persisted as a summary-like translated row, then folded into the tooltip summary block instead of replacing the description.
- When an existing `QuestPlate` row is found but was originally created from a looser Journal list/title path, `JournalDetail` now fills in missing metadata and message translation on demand:
  - `QuestId`
  - `QuestTextSheetName`
  - `SourceContentHash`
  - `TranslatedQuestMessage`
  - translated objective and summary/SEQ rows

This means the DB row is expected to become more complete the first time the
quest is opened in `JournalDetail`, even if an earlier title-only row already
existed.

---

## Journal mode-switching runtime note

`Journal` and `JournalDetail` no longer rely only on sparse requested-update
events to react to display-mode changes while the addon remains open.

Current behavior:

- `Journal` list refreshes through `PreUpdate` and `PreRequestedUpdate`
- `JournalDetail` refreshes through:
  - `PreUpdate` on `JournalDetail`
  - `PreRequestedUpdate` on `JournalDetail`
  - `PostRequestedUpdate` on `JournalDetail`
  - `PostRequestedUpdate` on `Journal` as the selection-driven refresh path

This matters because the current stabilization pass is DB-first and
mode-sensitive:

- `TooltipTranslation` must restore original native text and show translated
  hover text only when the translated payload is ready
- `NativeUiTranslation` must actively reapply translated native text from cache
  or DB, even when no fresh requested update occurs
- `NativeUiTranslationWithOriginalTooltips` must write translated native text
  and only expose the original text in hover once the translated/native payload
  is ready

Without the live `PreUpdate` path on `JournalDetail`, switching modes from the
config window while the detail view stayed open could leave the pane in a stale
visual state until a later addon refresh happened by chance. Now that
`JournalDetail` is its own handler with its own toggle and display mode, that
live update path is also what keeps detail behavior isolated from the Journal
list. The `Journal`-driven path stays intentionally narrower: it is used only
to react to quest selection changes after the list has finished updating, so
the detail handler does not re-read half-updated UI state too early.

Visible summary subrows in the `JournalDetail` body are now recollected through
the detail handler itself, but they are filtered against the active canonical
quest payload before being used for native writes or tooltip assembly. This
keeps summary coverage while avoiding stale rows from a previously selected
quest.

For native-mode rendering, the detail handler now collapses the canonical
summary paragraphs into the primary summary node and clears the supplemental
summary nodes. The live supplemental nodes remain part of the original-state
snapshot so tooltip-only mode and swap restoration can still put the addon back
exactly as the game drew it.

That snapshot now includes the primary summary node presentation as well:

- original width
- original text flags
- original font size
- original summary-container height
- discovered supplemental summary node addresses

This allows native mode to expand the primary summary block for longer
translated text and still restore the original layout when `JournalDetail`
returns to a non-native mode.

---

## Related documents

- [quest-full-pipeline-design.md](./quest-full-pipeline-design.md) — intended target architecture
- [journal-quest-data-model-and-flow.md](./journal-quest-data-model-and-flow.md) — data model and flow analysis
- [quest-sheet-acquisition-pipeline.md](./quest-sheet-acquisition-pipeline.md) — Lumina sheet access details
- [quest-probe-command.md](./quest-probe-command.md) — in-game debugging tools
- [structured-text-payload-pipeline.md](./structured-text-payload-pipeline.md) — payload-safe translation rules
- [quest-tooltip-validation-notes.md](./quest-tooltip-validation-notes.md) — observed hover and tooltip coverage from the current validation pass

---

## Source files

| Concern           | File                                                   |
|-------------------|--------------------------------------------------------|
| Event wiring      | `NativeUI/Helpers/AddonHandlerWiring.cs`               |
| Event registrar   | `NativeUI/Helpers/AddonHandlerRegistrar.cs`            |
| Mode flag helpers | `NativeUI/AddonHandlers/Quest/QuestAddonModeHelpers.cs` |
| Hover registration| `NativeUI/Helpers/HoverTooltipRegistration.cs`         |
| Hover manager     | `NativeUI/Helpers/HoverTooltipManager.cs`              |
| Journal handlers  | `NativeUI/AddonHandlers/Quest/JournalHandler.cs`       |
| JournalAccept     | `NativeUI/AddonHandlers/Quest/JournalAcceptHandler.cs` |
| JournalResult     | `NativeUI/AddonHandlers/Quest/JournalResultHandler.cs` |
| ScenarioTree      | `NativeUI/AddonHandlers/Quest/ScenarioTreeHandler.cs`  |
| ToDoList          | `NativeUI/AddonHandlers/Quest/ToDoListHandler.cs`      |
| RecommendList     | `NativeUI/AddonHandlers/Quest/RecommendListHandler.cs` |
| UI text cache     | `Cache/QuestUiTranslationCache.cs`                     |
| Hover cache       | `Cache/QuestHoverTranslationCache.cs`                  |
| DB operations     | `DBHelpers/DbOperations.cs`                            |
