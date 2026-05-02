# Quest Addon Detailed Flow and Remediation Plan

## Purpose

This document captures the **current detailed flow** of each quest-family addon
handler after the recent quest DB normalization work, and it records what still
needs to change in each addon so the quest pipeline behaves correctly and
consistently.

This is meant to be a practical engineering reference:

- what each addon consumes today
- where it gets its data
- what it mutates
- what cache scope it uses
- what is already good enough
- what still needs to be corrected

This document is intentionally more operational than
`quest-addon-translation-runtime-flow.md`. That document explains the runtime
flow. This document explains the current flow **plus the remediation plan**.

## Status Update - 2026-05-01

`JournalDetail` is currently in a good operational state for the active
stabilization branch and is no longer the main refactor focus.

Keep the current `JournalDetail` runtime as-is unless a concrete regression is
observed. The active tooltip/runtime investigation has moved to
`ActionDetail`, `ItemDetail`, and the related tooltip identity and overlay
coordination issues.

---

## Current Shared Quest Substrate

All quest-family addons now sit on top of the same shared data substrate:

1. `QuestManager`
   - accepted quest discovery
   - live sequence and runtime quest identity
2. Lumina quest resolution
   - `Quest` row
   - mounted quest text sheet
3. `QuestProgressResolver`
   - canonical quest row acquisition
   - current progress snapshot
   - `QuestTextSheetName`
   - `SourceContentHash`
4. `QuestCanonicalData`
   - canonical row model for:
     - `SEQ`
     - `TODO`
     - `SYSTEM`
5. `QuestPlate`
   - persisted DB row in `questplates`
   - canonical payload now stored in `CanonicalRowsAsText`
6. brokered translation queue
   - async translation resolution
   - paced background work
7. addon-local runtime caches
   - hover state
   - visible text state
   - short-lived per-addon presentation state

The intended architecture is now:

- **shared data source**
- **isolated addon runtime state**

That is the right direction. It is important that we keep that boundary clean.

## Current Stabilization Scope

At the current checkpoint, the quest-family runtime is being stabilized one
surface at a time.

That means:

- `Journal` and `JournalDetail` remain active as separate handlers with
  separate toggles and display modes
- the other quest addon handlers are intentionally not registered right now
- accepted-quest prefetch remains active so the DB can stay warm

This is temporary and deliberate. The goal is to stabilize one addon family
against the normalized quest DB before re-enabling the others.

---

## Current Global Observations

### What is now working

- accepted quest prefetch resolves accepted quests and translates them in
  background
- canonical quest payloads are now saved into `questplates`
- translated quest payloads are now saved as well
- duplicate canonical rows with the same visible text now respect `RowKey`
- the DB now contains richer quest rows with:
  - `QuestTextSheetName`
  - `SourceContentHash`
  - canonical row payload
  - translated objective/summary/system maps

### What is still uneven

- not every quest addon consumes the canonical quest payload directly
- several addons still use `FindQuestPlateByName`, which is looser than ideal
- `JournalDetail` is still the most behavior-sensitive quest surface
- `ScenarioTree`, `RecommendList`, and `AreaMap` still need stronger runtime
  validation and possibly simpler, more deterministic tooltip composition
- `JournalAccept` and `JournalResult` are intentionally simpler, but they still
  persist thinner quest rows than the canonical prefetch path

### The main design rule from now on

Quest addons should not invent their own quest content.

They should:

1. identify the quest and live progress
2. read canonical rows from the DB or canonical resolver
3. decide what subset is appropriate for that addon
4. render or mutate according to the selected display mode

They should **not** rebuild quest meaning from UI text unless no canonical data
exists yet and the UI text is being used as an explicit fallback.

---

## Shared Risks Across Addons

Before going addon by addon, these are the cross-cutting issues we still need
to keep in mind.

### Risk 1: name-only lookup is still too loose

Handlers that still use `FindQuestPlateByName(...)` can behave incorrectly when:

- quest names collide
- the visible UI is lagging behind the active quest context
- the addon shows only a title but not enough body text

Long term, these handlers should move toward:

- `QuestId`-aware lookup
- progress-aware lookup where relevant

### Risk 2: UI nodes still leak into canonical decisions

The UI should help us locate:

- which addon is open
- which quest row is selected
- which objective row is hovered

The UI should not define the quest payload when canonical sheet data is already
available.

### Risk 3: hover registration and translation should stay separate

The correct split remains:

- sparse events for capture or translation enqueue
- `PreDraw` or equivalent for hover-target refresh only

If we merge those concerns again, we will reintroduce either:

- repeated translation work
- dead hover targets
- or both

### Risk 4: addon-local caches must remain local

The shared quest DB is correct.
The shared broker is correct.

But UI-facing caches should remain local per addon, because each addon has:

- different lifecycle timing
- different hitboxes
- different visible text subsets
- different tooltip needs

---

## Addon-by-Addon Current State and Plan

---

## `Journal` (quest list)

### Current role

The `Journal` list handler translates and/or tooltips the quest titles in the
left-hand list.

### Current flow

- events:
  - `PreUpdate`
  - `PreRequestedUpdate`
- runtime file:
  - `NativeUI/AddonHandlers/Quest/JournalHandler.cs`
- local state:
  - `journalListTextCache`
  - `journalListHoverCache`
- DB lookup:
  - `FindQuestPlateByName(...)`
- hover anchor:
  - quest-name text nodes

### Current data source

Today this surface is intentionally being simplified into a **DB-first title
surface**.

It reads:

- the visible quest title from the node
- persisted translated title from `QuestPlate`
- original UI title as fallback when the DB is not warm yet

This is acceptable for this surface, because the Journal list is only trying to
show titles.

### Current mode contract

`Journal` title hover should now follow this strict rule:

- `NativeUiTranslation`: no hover tooltip
- `TooltipTranslation`: show the tooltip only when translated title payload is
  ready
- `NativeUiTranslationWithOriginalTooltips`: show the original-title tooltip
  only when the translated/native title payload is ready

The important point is that the title hover should not silently fall back to
original text in a mode that is supposed to present translation.

### What is already good enough

- it has local runtime cache
- it is narrow in scope
- it does not need full quest-body composition
- it can tolerate name-only lookup better than the other addons

### What still needs improvement

- it should prefer a `QuestId`-aware path when a selected or visible quest id
  can be resolved cheaply
- it should stay read-only with respect to translation generation
- if the accepted-quest prefetch already warmed the title, this handler should
  behave as a pure DB consumer

### Recommendation

Keep `Journal` list relatively simple.

This addon does **not** need a large refactor first. It should mostly become a
clean consumer of the prewarmed DB row.

---

## `JournalDetail`

### Current role

This is the most important and most fragile quest addon surface.

It is responsible for the full quest body presentation:

- description
- current objective
- summary-like context
- title tooltip/body tooltip behavior

### Current flow

- events:
  - `PreUpdate` on `JournalDetail`
  - `PreRequestedUpdate` on `JournalDetail`
  - `PostRequestedUpdate` on `JournalDetail`
  - Journal lifecycle updates used as a selection-driven refresh path
  - `PreHide` / `PreFinalize` on `JournalDetail`
- runtime file:
  - `NativeUI/AddonHandlers/Quest/JournalDetailHandler.cs`
- config:
  - `TranslateJournalDetail`
  - `JournalDetailTranslationDisplayMode`
- local state:
  - `journalDetailTextCache`
  - `journalDetailOriginalCache`
  - `currentJournalDetailScopeKey`
- DB lookup:
  - `FindQuestPlate(...)` using `QuestName + QuestMessage`
- hover anchor:
  - name node
  - expanded body bounds derived from `JournalCanvasComponentNode`

### Current data source

This handler is now the first surface being pushed toward a **DB-first**
composition model.

At the moment, the split is:

1. UI nodes
   - selected quest context
   - visible objective text
   - visible summary nodes only as anchors / original-state snapshots
2. `QuestProgressResolver`
   - current sequence
   - canonical quest rows
3. persisted `QuestPlate`
4. original UI fallback text when the DB does not have the translated payload yet

### Current mode contract

`JournalDetail` hover should now follow the same strict readiness rule:

- `NativeUiTranslation`: no hover tooltip
- `TooltipTranslation`: no tooltip until the translated payload required by the
  visible section is ready
- `NativeUiTranslationWithOriginalTooltips`: no original-text tooltip until the
  translated/native payload for that same visible section is ready

For the body tooltip, “ready” means:

- translated description ready
- translated current objective ready when an objective is visible
- translated summary block ready when a summary or current `SEQ` row is visible

### Why it is still the problem child

Even though the DB is now much healthier, `JournalDetail` still has the largest
chance of drifting because it is the place where:

- canonical data
- live progress
- and UI-specific presentation

still get mixed together.

That means it can still show:

- objective selection that is still anchored to the visible objective node
- stale fallback behavior when the DB is still incomplete
- or UI-shape mismatches if the game changes how many summary nodes it exposes

### What needs to change

`JournalDetail` should become the **first fully canonical quest consumer**.

The ideal shape is:

1. resolve the active quest id and sequence
2. fetch the canonical `QuestPlate`
3. read canonical rows by `RowKey`
4. build a deterministic detail payload from:
   - current description source
   - current active objective subset
   - current relevant summary subset
5. render or mutate from that payload

### Specific remediation

- keep the current description and summary flow canonical-first:
  - description from current `SEQ`
  - summary from prior `SEQ` rows through the current sequence
- create an explicit `JournalDetailBodyModel` or similar helper that is built
  from `QuestCanonicalData`/`QuestPlate`
- decide, in one place, which rows belong to:
  - description
  - active objective block
  - summary block
- keep the UI node text only as:
  - selection context
  - hitbox anchor
  - last-resort fallback
- keep this handler read-only with respect to translation generation; it should
  not enqueue new quest translation work locally

### Recommendation

This addon should be the next main refactor target.

If we get `JournalDetail` right, the rest of the quest-family addons become
much easier because we will finally have a canonical “what quest content should
look like” consumer.

---

## `JournalAccept`

### Current role

This handler translates the quest acceptance popup before its first draw.

### Current flow

- event:
  - `PreSetup`
- runtime file:
  - `NativeUI/AddonHandlers/Quest/JournalAcceptHandler.cs`
- data source:
  - `AddonSetupArgs`
  - `AtkValue` slots
- DB lookup:
  - `FindQuestPlate(...)`

### Current strengths

- `PreSetup` is the correct lifecycle event for this addon
- it can mutate `AtkValue`s before first paint
- it already resolves quest progress and stores metadata

### Current weakness

This addon still starts from setup-time strings rather than from a canonical
quest payload assembled earlier.

That is survivable, but it means:

- if prefetch is warm, it should read canonical values immediately
- if prefetch is cold, it still has to do its own work

### What needs to change

- prefer a canonical quest row by `QuestId` first when available
- treat the setup args as a display-time input, not the long-term source
- keep its current `PreSetup` mutation strategy, because that is still the
  right place to mutate accept dialog text

### Recommendation

This addon needs only a **small alignment pass**, not a redesign.

---

## `JournalResult`

### Current role

This handles the completion/result popup and mainly cares about the quest name.

### Current flow

- event:
  - `PreSetup`
- runtime file:
  - `NativeUI/AddonHandlers/Quest/JournalResultHandler.cs`
- data source:
  - setup-time `AtkValue`
- DB lookup:
  - `FindQuestPlateByName(...)`

### Current strengths

- small surface
- simple responsibilities
- `PreSetup` timing is appropriate

### Current weakness

- name-only lookup remains the main looseness
- it does not benefit enough yet from the normalized quest DB

### What needs to change

- if possible, resolve the quest id from the setup context or active progress
- if not possible, keep the title-only path but make it explicitly “best effort”
- do not let this addon drive canonical persistence strategy; it should be a
  consumer, not a source of truth

### Recommendation

Low priority. Keep it simple and let the canonical prefetch and `JournalDetail`
do the heavy lifting.

---

## `_ToDoList`

### Current role

This surface renders active quest titles and objectives in the in-world duty
list / todo list.

### Current flow

- events:
  - `PostRequestedUpdate`
  - `PreRequestedUpdate`
  - `PreDraw` for retry / hover refresh
- runtime file:
  - `NativeUI/AddonHandlers/Quest/ToDoListHandler.cs`
- local state:
  - `toDoRuntimeEntries`
  - readiness gate / retry timer / debounced waiting notification state
- DB lookup:
  - `FindQuestPlate(...)`
- progress helper:
  - `QuestTodoProgressResolver`
- canonical helper:
  - `QuestCanonicalData`

### Current status

This surface is now intentionally stricter than before.

It now:

- never queues translation from the addon
- never inserts or updates `QuestPlate` rows from the addon
- uses `QuestManager + canonical quest data + persisted QuestPlate` only
- activates only when **all visible quest rows** are ready in the DB
- restores the original addon state and emits a notification when visible data
  is still missing
- keeps row-level hover state and practical hitboxes once activated

### Current weakness

It still has one remaining UI-driven inference step:

- scan node tree
- infer quest/objective grouping
- join objectives back to a quest row

That means the content source is now correct, but the visible-row association
is still heuristic.

### What needs to change

- keep the current DB-only rule
- keep the current all-or-nothing readiness gate
- eventually make objective row association explicit by canonical row identity
  instead of visible order inference
- keep notifications debounced so login-time ToDoList visibility does not spam
  the user while accepted-quest prefetch is still warming the DB

### Recommendation

The large behavioral correction for `_ToDoList` is now done.

Future changes should focus on **removing visible-row inference** rather than
reintroducing any local translation path.

---

## `ScenarioTree`

### Current role

This surface handles the main scenario tree / quest tracker style entry points.

### Current flow

- events:
  - `PreRefresh`
  - `PreRequestedUpdate`
  - `PreDraw` for retry / hover refresh
- runtime file:
  - `NativeUI/AddonHandlers/Quest/ScenarioTreeHandler.cs`
- local state:
  - `scenarioTreeRuntimeEntries`
  - readiness gate / retry timer / debounced waiting notification state
- native identity source:
  - `AgentScenarioTree`
  - `CurrentScenarioQuest` / `MainScenarioQuestIds`
  - `JobQuestIds`
- DB lookup:
  - `FindQuestPlate(...)`
- progress helper:
  - `QuestTodoProgressResolver`
- canonical helper:
  - `QuestCanonicalData`

### Current status

This surface now follows the same DB-only rule as `_ToDoList`.

It now:

- never queues translation from the addon
- never inserts or updates `QuestPlate` rows from the addon
- resolves visible quest ids through `AgentScenarioTree`, then resolves the
  translated payload through `QuestManager + QuestCanonicalData + QuestPlate`
- activates only when all visible ScenarioTree quest slots already have the
  required translated payload in the DB
- restores original addon text and emits a notification when DB data is still
  missing
- keeps the runtime state local to ScenarioTree only

### What likely still needs to change

- validate in game that the all-or-nothing gate feels right on login and during
  accepted-quest prefetch warm-up
- confirm that the addon-level tooltip surface is the right UX for this window
- if needed later, enrich the payload beyond quest names without reintroducing
  any local translation path

### Recommendation

Keep the DB-only contract intact.

Future changes should focus on better visible-slot semantics and tooltip UX, not
on restoring any translation queue inside the handler.

---

## `RecommendList`

### Current role

This surface handles the recommended quest list.

### Current flow

- events:
  - `PostReceiveEvent`
  - `PreRequestedUpdate`
  - delayed `PostRequestedUpdate`
  - `PreDraw` for hover refresh
- runtime file:
  - `NativeUI/AddonHandlers/Quest/RecommendListHandler.cs`
- local state:
  - `recommendListHoverEntries`
  - `recommendListTextCache`
- DB lookup:
  - `FindQuestPlateByName(...)`

### Current status

This addon has the right structural ingredients:

- local runtime cache
- hover refresh path
- delayed async pass for unstable layouts

But it still has a history of going quiet or feeling inconsistent in runtime
validation.

### Why this addon is tricky

- it can repaint during transitions
- node stability may lag behind event timing
- it still behaves as a title-centric surface with looser identity resolution

### What needs to change

- make the visible entry key more explicit and stable
- reduce reliance on “name only” when canonical prefetch already knows accepted
  quests
- keep translation decisions sparse and let `PreDraw` only re-register hover
  state
- validate that delayed `PostRequestedUpdate` is still needed once canonical DB
  warmup is consistently available

### Recommendation

This addon likely does not need a full rewrite, but it does need a dedicated
validation pass once `JournalDetail` is stabilized.

---

## `AreaMap`

### Current role

This surface handles quest text shown inside the map window.

### Current flow

- events:
  - `PreRefresh`
  - `PreRequestedUpdate`
  - `PreDraw` for hover refresh
- runtime file:
  - `NativeUI/AddonHandlers/Quest/AreaMapHandler.cs`
- local state:
  - `areaMapTextCache`
- DB lookup:
  - `FindQuestPlateByName(...)`

### Current status

This addon previously showed a classic symptom:

- lifecycle events fired
- tooltip registration did not follow

The handler is better now because it has:

- requested-update fallback to live addon `AtkValues`
- `PreDraw` hover refresh
- local text cache

But it still needs more validation.

### What needs to change

- verify that the visible quest name source is stable enough to bind to the
  right canonical row
- verify whether a whole-addon tooltip is the correct UX, or whether a smaller
  explicit bounds model is better
- reduce the amount of work done in refresh events once canonical DB warmup is
  proven stable

### Recommendation

Treat this as a focused follow-up addon, not as part of the main `JournalDetail`
refactor.

The likely winning shape here is simple:

- read current map quest identity
- read canonical quest row
- render one stable addon-scoped tooltip

---

## Addon Priority Order From Here

If we continue the quest-addon cleanup from the current normalized DB state, the
recommended order is:

1. `JournalDetail`
2. `ScenarioTree`
3. `RecommendList`
4. `AreaMap`
5. `JournalAccept`
6. `JournalResult`
7. `Journal` list
8. `_ToDoList` only after the rest are healthy

This order is based on risk and payoff, not complexity alone.

---

## Concrete Work We Still Need

### 1. Create a canonical presentation layer

We need one shared helper that turns canonical quest rows into addon-appropriate
presentation blocks.

Examples:

- `BuildJournalDetailBody(...)`
- `BuildToDoObjectiveBundle(...)`
- `BuildScenarioTreeTooltip(...)`
- `BuildRecommendListTooltip(...)`

These should consume canonical quest data, not raw UI text.

### 2. Reduce name-only lookups

Where possible, each addon should resolve:

- `QuestId`
- current sequence
- or another stable progress key

before looking up `QuestPlate`.

### 3. Keep hover refresh read-only

Every addon should follow this rule:

- translation enqueue on sparse lifecycle events
- hover registration refresh on `PreDraw`

No addon should reintroduce translation work into the hover path.

### 4. Keep local caches local

Each quest addon should keep only:

- visible translated text for itself
- hover payloads for itself
- minimal selection/body composition state for itself

Everything else should remain shared.

### 5. Make `JournalDetail` canonical-first

This is the biggest missing piece.

Until `JournalDetail` is canonical-first, the quest-family runtime will still
feel less stable than it should.

---

## Suggested Target State Per Addon

| Addon | Target source of truth | UI role |
|-------|------------------------|---------|
| `Journal` | DB canonical title | title display and title hover anchor |
| `JournalDetail` | DB canonical rows + live progress | selection context and hitbox anchor |
| `JournalAccept` | DB canonical row when available | setup-time native mutation target |
| `JournalResult` | DB canonical title | setup-time native mutation target |
| `_ToDoList` | DB canonical `TODO` rows + live progress | visible row association and hover anchor |
| `ScenarioTree` | DB canonical quest summary | visible slot identity and hover anchor |
| `RecommendList` | DB canonical title/summary subset | visible row identity and hover anchor |
| `AreaMap` | DB canonical current quest summary | addon-scoped tooltip anchor |

---

## Relationship To Other Docs

Use this document together with:

- [quest-addon-translation-runtime-flow.md](./quest-addon-translation-runtime-flow.md)
- [quest-data-assembly-current-state.md](./quest-data-assembly-current-state.md)
- [quest-sheet-acquisition-pipeline.md](./quest-sheet-acquisition-pipeline.md)
- [structured-text-payload-pipeline.md](./structured-text-payload-pipeline.md)
- [quest-tooltip-validation-notes.md](./quest-tooltip-validation-notes.md)

The intended split is:

- `quest-addon-translation-runtime-flow.md`
  - what the code does right now
- `quest-data-assembly-current-state.md`
  - what quest data means right now
- `quest-addon-detailed-flow-and-remediation-plan.md`
  - what each addon should do next
