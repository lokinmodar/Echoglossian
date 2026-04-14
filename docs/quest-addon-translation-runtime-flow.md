# Quest Addon Translation — Runtime Flow

## Purpose

This document describes the **current actual** runtime translation flow for every quest-family addon in Echoglossian. It does not describe the intended future pipeline (see `quest-full-pipeline-design.md` for that). It describes what the code does today, how each addon is triggered, how text is resolved, how the DB is queried, how caches work, and how hover tooltips are registered.

---

## Addons in scope

| Addon internal name | Config flag                          | Wiring file                  |
|---------------------|--------------------------------------|------------------------------|
| `Journal`           | `TranslateJournal`                   | `AddonHandlerWiring.cs`      |
| `JournalDetail`     | `TranslateJournal`                   | `AddonHandlerWiring.cs`      |
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

The `ShouldDrawHoverTooltips` property in the same file aggregates all families to decide whether `hoverTooltipManager.Draw()` should run at all each frame.

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

### 2a. Journal-local runtime caches (instance-scoped)

- `JournalHandler` now keeps its own local runtime state for the surfaces that
  repaint the most and were shown to regress when coupled to the broader
  quest-family caches.
- Journal list:
  - `journalListTextCache`
  - `journalListHoverCache`
  - these are trimmed to the currently visible quest names and node anchors at
    the end of each Journal list scan
- Journal detail:
  - `journalDetailTextCache`
  - scoped by a quest-detail key derived from `QuestProgressSnapshot.CacheKey`
    when available, with a `questName|questMessage` fallback
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
4. **DB hit:** use `TranslatedQuestName`; write to node if `WritesNative`; `Remember` in both `QuestUiTranslationCache` and `QuestHoverTranslationCache`; register hover tooltip; `continue`.
5. **DB miss:** call `TryGetQueuedTranslation($"Journal|{questNameText}")`.
   - **Queue hit:** diacritics-strip if configured; write to node if `WritesNative`; populate caches; register hover tooltip.
   - **Queue miss:** register an original-text hover target for this cycle, call
     `QueueTranslation` (persist callback runs `InsertQuestPlate`), then
     `continue`.
6. At the end of the scan, trim the Journal-local visible-list caches to only
   the quest names and node anchors that were visible in that pass.

**DB table used:** `questplates` — by name only via `FindQuestPlateByName`.

---

### `JournalDetail` (quest body panel — active quest)

**Trigger events:**
- `AddonEvent.PostRequestedUpdate` on `"Journal"` → `JournalHandler.OnJournalDetailEvent` → `TranslateJournalDetail()`
- `AddonEvent.PreRequestedUpdate` on `"JournalDetail"` → same

**What fires:** `TranslateJournalDetail()` calls `TranslateJournalBox` first; if no active quest node found, falls back to `TranslateCompletedQuest`.

**`TranslateJournalBox` flow:**

1. Read `questName` (node 38), `questMessage` (node 43→comp→node 8), `objectiveText` (node 43→comp→node 12→comp→node 3), `summaryText` (optional, node 43→comp→node 52→comp→node 2).
2. Build `QuestPlate`; run `QuestProgressResolver.TryResolveQuestProgress` → attach `SourceContentHash` to plate.
3. `FindQuestPlate(plate)` — by name + message.
4. If found and `GameVersion` stale → `UpdateQuestPlateGameVersion` (non-blocking).
5. **Early exit (non-hover mode only):** if all four texts hit `QuestUiTranslationCache` → skip everything; `return`.
6. Resolve `translatedQuestName`, `translatedQuestMessage`, `translatedQuestObjective`, `translatedQuestSummary`:
   - DB present → read directly from `foundQuestPlate` + `Objectives` dict
   - DB absent → `TryGetQueuedTranslation($"JournalDetail|{batchKey}")` (batched)
   - Queue absent → `QueueTranslationBatch` then `return`
7. Apply `RemoveDiacritics` if configured.
8. If `WritesNative`: `SetText` on all four nodes.
9. Populate `QuestUiTranslationCache` for the four canonical texts only.
10. If `UsesHoverTooltips`:
    - Register `JournalDetail-QuestName-{nodePtr}` on the name node.
    - Build `originalQuestBody` and `translatedQuestBody` from the visible three-part shape:
      - current description source (the visible quest description / translated quest message)
      - current objective text
      - current summary block (current `SEQ` row plus the live summary node text, when present)
    - Register `JournalDetail-QuestBody-{nodePtr}` using bounds that start from `JournalCanvasComponentNode` and then expand to include the visible description, objective, and summary nodes only.
    - Do **not** fold the additional visible Journal summary-node list into the canonical body or persisted row; those nodes can retain stale text across quest switches and were observed contaminating one quest with summary text from another.

**`TranslateCompletedQuest` flow:** same as above but reads only name + message (no objective/summary), uses `JournalDetail-CompletedQuestName-*` and `JournalDetail-CompletedQuestMessage-*` + `JournalDetail-CompletedQuestBody-*` hover keys.

**DB table used:** `questplates` — by `QuestName + QuestMessage` via `FindQuestPlate`.

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
- `AddonEvent.PreRefresh` → `ScenarioTreeHandler.OnScenarioTreeEvent` → `TranslateQuestOnScenarioTree`
- `AddonEvent.PreRequestedUpdate` → same
- `AddonEvent.PreDraw` → `ScenarioTreeHandler.OnScenarioTreeHoverRefreshEvent` → combined hover refresh only

**Args type:** primarily `AddonRefreshArgs`, with a live-addon fallback for
`PreRequestedUpdate` — if the requested-update event does not carry refresh
args, the handler now resolves `AtkUnitBase->AtkValues` directly from the
visible addon instead of turning that trigger path into a no-op.

**`TranslateQuestOnScenarioTree(setupAtkValues, valueIndex)` flow:**

Called twice per event: once for index 7 (MSQ entry) and once for index 2 (sub-quest entry).

1. Guard: `setupAtkValues[valueIndex].Type != ValueType.String`.
2. Read `questNameText`.
3. `QuestTodoProgressResolver.TryResolveQuestTodoProgress` → `questTodoProgressKey` (composite cache key including quest progress step identity).
4. **Cache check:** `QuestUiTranslationCache.TryGetAppliedSnapshot(questTodoProgressKey + "|" + questNameText)`:
   - **Hit:** if `UsesHoverTooltips` register `ScenarioTree-{addonPtr}-{valueIndex}-{progressKey}` with `cachedSnapshot.AppliedText`; `return`.
   - **Miss:** continue.
5. `FindQuestPlateByName`.
6. **DB hit:** diacritics strip; `SetManagedString(setupAtkValues[valueIndex])` if `WritesNative`; `Remember`; if `UsesHoverTooltips` register; `return`.
7. `TryGetQueuedTranslation($"ScenarioTree|{valueIndex}|{progressKey}|{questNameText}")`:
   - Hit: same output path as DB hit.
   - Miss: `QueueTranslation` (persist InsertQuestPlate); return.

**Hover maintenance:**
- Every resolved slot also updates an in-memory hover snapshot for the current `valueIndex`.
- `PreDraw` combines the visible MSQ/subquest entries into one tooltip payload and re-registers it on the addon root.
- This path is read-only and avoids requeueing translation work.

**Key difference from other addons:** text is written via `SetManagedString` directly into the `AtkValue*` array in `PreRefresh` / `PreRequestedUpdate`, so the game's own node layout picks up the translated string without the handler touching node pointers directly.

**DB table used:** `questplates` — `FindQuestPlateByName` (name only).

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
- `AddonEvent.PostRequestedUpdate` → `ToDoListHandler.OnToDoListEvent` → `TranslateToDoList()`
- `AddonEvent.PreRequestedUpdate` → same

**Why both Pre and Post:** `PreRequestedUpdate` fires before game updates node text; `PostRequestedUpdate` fires after. Using both catches different game-driven refresh cycles.

**`TranslateToDoList()` scan phase:**

1. Walk `todoList->UldManager.NodeList`. Skip invisible nodes, `Collision`/`Res` type nodes, fate nodes (NodeId 8, 9).
2. For each visible component node, walk its child `Text` nodes.
3. Skip empty nodes, time-format strings.
4. Classify each visible text node by NodeId:
   - `NodeId > 60000` or `(NodeId == 4 && childNodeId == 3)` or `(NodeId == 6 && childNodeId == 2)` → quest name candidate (`questNamesToTranslate`)
   - `NodeId == 4 || NodeId == 5` → level-quest objective (`levelQuestObjectivesToTranslate`)
   - Otherwise → objective (`objectivesToTranslate`)
5. Skip if `questNamesToTranslate` is empty after full scan.

**`TranslateTodoItems()` translate phase:**

For each quest name entry:

1. Associate objectives using `GetQuestObjectives` (adjacent NodeId heuristic).
2. `QuestTodoProgressResolver.TryResolveQuestTodoProgress` → `questTodoProgressKey`.
3. Build compound cache key `$"{progressKey}|{questName}|{objectives joined}"`.
4. **Cache check** `QuestUiTranslationCache.TryGetAppliedSnapshot(sanitized key)`:
   - Hit: if no quest plate can be resolved, `continue`; otherwise reuse the
     resolved quest-plate path so hover registration and any native refresh
     still happen without queuing new translations.
   - Miss: proceed.
5. `FindQuestPlateByName`.
6. **DB hit:** write quest name node if `WritesNative`; call `RegisterToDoTooltip` with translated name; iterate objectives:
   - Objective in `foundQuestPlate.Objectives` → write node if `WritesNative`; `RegisterToDoTooltip`; `continue`.
   - Objective in queued translations → write + register.
   - Neither → `QueueTranslation` for objective (persist `UpdateQuestPlate`).
   - After all objectives resolved → `QuestUiTranslationCache.Remember(questKey, aggregatedResult)`.
7. **DB miss:** `TryGetQueuedTranslation($"ToDoListQuest|{progressKey}|{questName}")`:
   - Hit: build `QuestPlate`; write name node if `WritesNative`; `RegisterToDoTooltip`; iterate objectives (same queue/write pattern); `InsertQuestPlate`.
   - Miss: `QueueTranslation`; `continue`.

**`RegisterToDoTooltip`:** inner helper that stores a stable row hover payload and registers it from explicit screen bounds computed as the union of:
- the full visible row node
- the inner text node

The stable key is `ToDoList-{progressKey}-{indexI}-{indexJ}-{nodeId}`. A lightweight `PreDraw` pass refreshes those row targets without queueing new translations.

**DB tables used:** `questplates` — `FindQuestPlateByName` (name only); objectives looked up from `foundQuestPlate.Objectives` dict (stored inline in the plate record).

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
