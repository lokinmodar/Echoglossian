# Echoglossian Refactor Conversation History

## Purpose

This document is the living memory of the current Echoglossian refactor.

It is intentionally not a raw transcript. It should answer:

- what changed
- when it changed
- why it changed
- what evidence led to the change
- where the refactor currently stands

If this document ever diverges from the code, the code and the focused technical docs take precedence.

## Maintenance Rule

Keep this file append-oriented and chronological.

When adding a new milestone:

1. Prefer the real git commit timestamp when the work was committed.
2. If the work is still uncommitted, mark it clearly as an uncommitted working-tree checkpoint.
3. Do not insert new milestones out of order.
4. Do not replace prior milestones unless they were factually wrong.
5. If older sections become inaccurate, add a correction milestone instead of silently rewriting history.

## Long-Term Conclusions

The refactor converged on these stable decisions:

- quest data must not be treated as UI text alone
- UI is a locator and presentation surface, not the canonical quest source
- stable quest identity should come from Lumina
- live quest progress should come from runtime state such as `QuestManager` and director todos
- structured text must preserve payloads, placeholders, and formatting
- hover and native mutation modes must remain separate
- quest persistence should converge on canonical, versioned snapshots rather than fragmented UI captures

## Current State

As of the latest working-tree checkpoint:

- quest-family addons have been migrated to standalone handlers under `NativeUI/AddonHandlers/Quest/`
- `Journal` and `JournalDetail` tooltips are partially working, but `JournalDetail` remains the least stable surface
- `ToDoList` tooltip behavior improved, but the dense quest-family hover surfaces still need more runtime validation
- the quest pipeline is moving toward sheet-first composition using Lumina plus live progress
- the current `questplates` table is no longer fragmented by duplicate quest rows, but older rows were sparse and legacy-shaped
- the current uncommitted change narrows `JournalDetail` body hover content to the current SEQ row and enlarges the hover bounds
- the current uncommitted change also starts isolating `Journal` runtime state away from the broader quest-family caches by keeping a local cache for the visible quest list and a local scope cache for `JournalDetail`
- the current uncommitted change also adds an accepted-quest background prefetch driven by `QuestManager`, so accepted quests can populate `questplates` before the quest UI needs them
- the current uncommitted change also moves `ScenarioTree`, `RecommendList`, and `AreaMap` off the shared quest UI caches and into handler-local runtime caches
- the current uncommitted change also adds user-facing notifications for accepted-quest prefetch start and completion
- the current uncommitted change also introduces `QuestCanonicalData` as the explicit current-state quest shape used by prefetch and shared SEQ resolution
- the current uncommitted change also emits accepted-quest canonical dumps into a purpose-named file next to `Echoglossian.db`
- the current uncommitted change also emits accepted-quest prefetch activity events into a separate purpose-named file so background translation can be traced step by step
- the current uncommitted change also fixes accepted-quest prefetch quest-id resolution by promoting `QuestManager` work ids into full `Quest.RowId` values before resolving Lumina quest rows
- the current uncommitted change also splits `JournalDetail` into its own
  handler, config toggle, display mode, and runtime caches so it no longer
  shares Journal list presentation state

## Reference Docs

- [Refactor Timeline and Flow Analysis](./refactor-timeline-and-flow-analysis.md)
- [Quest Data Assembly Current State](./quest-data-assembly-current-state.md)
- [Quest Sheet Acquisition Pipeline](./quest-sheet-acquisition-pipeline.md)
- [Quest Full Pipeline Design](./quest-full-pipeline-design.md)
- [Journal Quest Data Model and Flow](./journal-quest-data-model-and-flow.md)
- [Quest Addon Translation Runtime Flow](./quest-addon-translation-runtime-flow.md)
- [Quest Tooltip Validation Notes](./quest-tooltip-validation-notes.md)
- [Structured Text Payload Pipeline](./structured-text-payload-pipeline.md)
- [Quest Probe Command](./quest-probe-command.md)

## Chronological Milestones

### 2026-04-03 15:23:57 -0300 - `2591513` - talk flow migrated to addon-handler runtime

**What changed**

- `Talk` moved to the addon-handler runtime and stopped depending on the older mixed path.

**Why it changed**

- This established the modern separation of capture, translation, overlay, and native mutation that the rest of the refactor now follows.

**Next step**

- Apply the same runtime separation to the next addon families.

### 2026-04-03 19:51:40 -0300 - `2b4d548` - talk and battletalk handlers stabilized

**What changed**

- The new talk-family runtime was hardened.

**Why it changed**

- This reduced the risk of restoring native state incorrectly and helped define the later quest-mode rules.

**Next step**

- Keep extending the handler-based runtime to the remaining UI families.

### 2026-04-04 23:07:47 -0300 - `aa65f16` - MiniTalk finalized

**What changed**

- `_MiniTalk` was reworked to support multiple overlay instances and viewport-aware rendering.

**Why it changed**

- This reinforced the rule that visual attachment should follow visible addon instances, not a single global text slot.

**Next step**

- Reuse the same instance-aware mindset on other dense UI surfaces.

### 2026-04-05 10:35:10 -0300 - `96352ab` - checkpoint before removing legacy overlay handlers

**What changed**

- Overlay work for the earlier addon families reached a stable checkpoint.

**Why it changed**

- This became the safe boundary before legacy overlay handler cleanup.

**Next step**

- Remove the legacy overlay handlers that already have modern replacements.

### 2026-04-05 10:38:07 -0300 - `e972ea3` - legacy overlay handlers removed

**What changed**

- Old overlay handlers that already had modern replacements were removed.

**Why it changed**

- This left quest and Journal work as the main remaining legacy-heavy surface.

**Next step**

- Start modernizing the Journal-family flow.

### 2026-04-05 11:41:33 -0300 - `109b838` - journal tooltip foundation added

**What changed**

- The first shared quest hover tooltip foundation was introduced.

**Why it changed**

- The DB save semantics were explicitly preserved while the new presentation path was added.

**Next step**

- Expand hover presentation across the rest of the Journal family.

### 2026-04-05 11:58:45 -0300 - `00abaac` - hover tooltips for journal windows

**What changed**

- Hover-driven translation display was expanded across Journal-family windows.

**Why it changed**

- This created the first stable tooltip-only path for dense quest surfaces.

**Next step**

- Remove synchronous hot-path translation from those windows.

### 2026-04-05 12:52:52 -0300 - `fdb28e7` - quest window translations moved to queueing

**What changed**

- Quest-family windows began using queued background translation rather than synchronous hot-path translation.

**Why it changed**

- This was the first big step toward reducing UI stalls in Journal-family addons.

**Next step**

- Migrate the remaining heavy Journal-adjacent handlers to the async path.

### 2026-04-05 14:14:09 -0300 - `2736097` - journal and recommend handlers migrated to async flow

**What changed**

- The heaviest remaining quest-adjacent handlers were moved off the synchronous runtime path.

**Why it changed**

- This reduced frame-time risk but left tooltip consistency work still open.

**Next step**

- Tighten hover renewal and cache-hit behavior.

### 2026-04-05 19:38:06 -0300 - `feecc77` - journal hover tooltip refresh fixed

**What changed**

- Hover renewal for quest windows was improved so tooltips would not disappear simply because a translated node was no longer re-registered in that frame.

**Why it changed**

- Dense quest windows repaint often and needed a more persistent hover registration path.

**Next step**

- Keep trimming `Echoglossian.cs` while preserving the new quest flow.

### 2026-04-06 08:03:42 -0300 - `ec62d99` - core wiring and quest flow split

**What changed**

- `Echoglossian.cs` was trimmed by extracting core wiring and quest-related helper logic.

**Why it changed**

- This made the quest refactor easier to reason about and modify safely.

**Next step**

- Continue splitting quest-specific helper types out of monolithic files.

### 2026-04-06 13:08:46 -0300 - `505c8b7` - quest item helper types extracted

**What changed**

- `SummaryQuest` and `ToDoItem` were split into their own files.

**Why it changed**

- This was a structural cleanup step that made the quest handlers more readable.

**Next step**

- Keep pushing Journal toward a better data source than raw UI text.

### 2026-04-07 03:45:01 -0300 - `eba106e` - Journal improvements checkpoint

**What changed**

- Journal behavior and presentation were improved enough to support the next Lumina-oriented push.

**Why it changed**

- This commit was an early sign that the Journal family needed a better source of truth than raw UI text.

**Next step**

- Start enriching quest identity with Lumina data.

### 2026-04-09 00:10:03 -0300 - `1905ab9` - journal quest body hover stabilized

**What changed**

- `JournalDetail` started using the `JournalCanvasComponentNode` as the preferred body trigger.

**Why it changed**

- This improved hitbox stability, but the body content still came from a mixed UI-plus-cache composition.

**Next step**

- Improve quest identity so the Journal family stops keying off raw UI text alone.

### 2026-04-13 20:34:00 -0300 - uncommitted working-tree checkpoint - Journal runtime isolation started

**What changed**

- `JournalHandler` now keeps a local runtime cache for the currently visible
  Journal quest list instead of relying on the broader quest-family UI and
  hover caches for that surface.
- `JournalDetail` now has an explicit quest-scope runtime cache key and clears
  its local body cache when the visible quest changes.
- Journal cleanup now clears the Journal-local list cache, Journal-local hover
  cache, and JournalDetail-local scope cache when the views close.

**Why it changed**

- Recent log slices showed the quest-family runtime collapsing into `Journal`
  hover traffic while the other addons stayed silent, which strongly suggests
  the shared short-lived caches are too broad.
- The Journal list is dense, highly repainted, and a good first candidate for
  addon-local runtime state.
- This also matches the design direction discussed during the refactor: shared
  DB, sheet resolution, live progress, and translation broker; local runtime
  state per addon.

**Next step**

- Validate in game whether the Journal list remains stable while the shared
  quest-family caches are no longer part of its hot path.
- If that reduces regressions, apply the same isolation pattern incrementally
  to `JournalDetail`, `ScenarioTree`, `RecommendList`, and `AreaMap`.

### 2026-04-09 00:49:15 -0300 - `05b5289` - quest plates enriched from Lumina

**What changed**

- Quest identity resolution began using Lumina to populate `QuestId`.

**Why it changed**

- This reduced ambiguity and prepared the database for a more canonical quest model.

**Next step**

- Split quest flows more cleanly and add stronger probe/documentation support.

### 2026-04-10 08:14:53 -0300 - `0f466ff` - quest flows split and command docs added

**What changed**

- The quest-family flows were split more cleanly and the probe/documentation surface was expanded.

**Why it changed**

- `/egloquestprobe` and the related docs became part of the repeatable refactor workflow.

**Next step**

- Make the quest translation pipeline more stable and identity-aware.

### 2026-04-12 16:14:31 -0300 - `a065dc3` - stable quest translation pipeline fixes

**What changed**

- `FindQuestPlateByName` became `QuestId`-first when possible.
- `MergeQuestPlateValues` began preserving translated objectives and summaries better.
- Quest progress resolution became more stable across AreaMap and ToDoList flows.

**Why it changed**

- The quest-family windows needed a more stable identity path and less fragile persistence behavior.

**Next step**

- Move the quest-family addons into the standalone handler architecture.

### 2026-04-12 17:03:47 -0300 - `5569e96` - AreaMap migrated to standalone quest handler

**What changed**

- `AreaMap` became the first quest-family addon migrated to the standalone `NativeUI/AddonHandlers/Quest/` architecture.
- Shared quest dependencies and mode helpers were introduced to support further migrations.

**Why it changed**

- This proved the quest handler bundle on the smallest quest-family addon before moving to denser windows.

**Next step**

- Migrate the remaining small quest addons.

### 2026-04-12 17:09:35 -0300 - `d7d15b4` - JournalAccept migrated

**What changed**

- `JournalAccept` moved to the standalone quest handler architecture.

**Why it changed**

- This proved the new shared quest dependency bundle on a small `PreSetup` surface.

**Next step**

- Continue through the rest of the small quest surfaces.

### 2026-04-12 17:11:11 -0300 - `3e0d496` - JournalResult migrated

**What changed**

- `JournalResult` moved to the standalone quest handler architecture.

**Why it changed**

- The quest-family migration path stayed consistent across small setup-based addons.

**Next step**

- Move on to the next quest surface in the migration guide.

### 2026-04-12 17:15:21 -0300 - `8ce521c` - ScenarioTree migrated

**What changed**

- `ScenarioTree` moved into the standalone quest handler architecture.

**Why it changed**

- This extended the new model into a denser quest surface.

**Next step**

- Bring `RecommendList` into the same architecture.

### 2026-04-12 17:19:33 -0300 - `c031b40` - RecommendList migrated

**What changed**

- `RecommendList` moved into the standalone quest handler architecture.

**Why it changed**

- Its more complex delayed and two-pass behavior was preserved under the new quest bundle.

**Next step**

- Consolidate the migration checkpoint and finish the remaining quest-family handlers.

### 2026-04-12 17:55:43 -0300 - `feae983` - standalone quest architecture migration checkpoint

**What changed**

- The quest-family addon migration was consolidated under the standalone architecture.

**Why it changed**

- From this point on, the main work shifted from migration itself to correctness, hover stability, and data-source quality.

**Next step**

- Stabilize tooltip behavior and hover registration across the quest-family surfaces.

### 2026-04-12 22:32:54 -0300 - `dfc7a17` - quest tooltip flow stabilized

**What changed**

- Quest-family tooltip behavior was tightened and documented.

**Why it changed**

- `Journal` and `JournalDetail` became the main active tooltip debugging target after this point.

**Next step**

- Fix tooltip lifetime on cache hits.

### 2026-04-12 22:39:53 -0300 - `fafc9d5` - hover targets kept alive on cache hits

**What changed**

- `AreaMap` and `ToDoList` stopped silently skipping hover re-registration on cache hits.

**Why it changed**

- This specifically targeted the problem where tooltips would work once and then disappear.

**Next step**

- Audit the live quest database and compare runtime composition against what gets persisted.

### 2026-04-12 late evening - questplate audit from the live SQLite file

**What changed**

- The live `questplates` table was inspected directly.
- The table was no longer fragmented into many rows per quest.
- The rows were still sparse and legacy-shaped:
- `GameVersion` present
- `QuestTextSheetName` missing
- `SourceContentHash` missing

**Why it changed**

- This changed the diagnosis: the remaining inconsistency looked more like runtime composition trouble than database row contamination.

**Next step**

- Narrow `JournalDetail` body composition so it follows the sheet-first design instead of mixing UI fragments and quest-step lists.

### 2026-04-13 working tree checkpoint - JournalDetail body narrowed to current SEQ row

**What changed**

- `JournalDetail` body hover was changed to prefer the current SEQ row from `QuestProgressSnapshot`.
- The body no longer concatenates the visible description, current objective, summary nodes, and all TODO rows into one tooltip blob.
- The `JournalCanvasComponentNode` hover bounds were padded so the body trigger is easier to hit.
- This checkpoint is not yet committed at the time of this entry.

**Why it changed**

- `JournalDetail` was still the biggest source of tooltip inconsistency, especially mixed content and an impractical body trigger.

**Next step**

- Validate this in-game and then revisit event choice for hover maintenance on the dense quest-family addons.

### 2026-04-13 09:34:05 -03:00 - working tree checkpoint - quest hover maintenance and Journal config combo ids

**What changed**

- Addon-wide hover registration now uses the root node screen coordinates instead of local node coordinates.
- `AreaMap` and `ScenarioTree` now keep lightweight hover payload snapshots and refresh their tooltip targets during `PreDraw` without queueing new translations.
- `RecommendList` now has a `PreDraw` hover-refresh pass that re-registers visible quest-name targets from cache or persisted data only.
- The `JournalTab` quest display-mode combos now use unique ImGui ids, fixing the broken dropdown behavior where options would not open or select correctly.
- This checkpoint is not yet committed at the time of this entry.

**Why it changed**

- `AreaMap` and `ScenarioTree` were anchoring whole-addon tooltips from the wrong coordinate space, which made those hover surfaces effectively unreachable.
- Several quest-family addons still depended on sparse lifecycle events to keep tooltip targets alive, so targets could disappear even when the addon was still visible.
- The config UI regression was a pure ImGui id collision: every quest-family display-mode combo reused the same label id.

**Next step**

- Validate in-game that `AreaMap`, `ScenarioTree`, and `RecommendList` now emit practical hover targets.
- Re-check whether `JournalDetail` body still needs an even larger hitbox after the hover-maintenance changes.
- Do a focused follow-up on `PluginVersion`, because the config on disk still reports `0.0.0.0`.

### 2026-04-13 10:18:00 -03:00 - working tree checkpoint - plugin version metadata fixed at the assembly level

**What changed**

- `Echoglossian.csproj` now generates assembly info again.
- `FileVersion` and `InformationalVersion` are now explicitly set from the existing calculated `$(Version)` property.
- The rebuilt DLL now reports:
  - `AssemblyVersion = 4.0.2604.797`
  - `FileVersion = 4.0.2604.797`
  - `ProductVersion = 4.0.2604.797+fafc9d5f293f0fbb0637efb17317a843679df352`
- This checkpoint is not yet committed at the time of this entry.

**Why it changed**

- The plugin UI and config were showing `0.0.0.0` because the project had `GenerateAssemblyInfo=false` without any manual version attributes, so the built assembly genuinely had zeroed version metadata.

**Next step**

- Reload the plugin and confirm that the config window and persisted config now pick up the real build version.
- Keep runtime validation focused on the quest-family tooltip surfaces.

### 2026-04-13 12:40:47 -03:00 - working tree checkpoint - ToDoList row hover refresh and JournalDetail body rebuilt

**What changed**

- `ToDoList` now stores per-row hover payloads and refreshes them in `PreDraw`, so tooltip visibility is no longer tied only to the translation/update event.
- `ToDoList` hover registration now uses the full row bounds combined with the inner text bounds instead of the narrow text-node rectangle alone.
- `JournalDetail` body hover now rebuilds its tooltip from three visible sections again:
  - description
  - current objective
  - current summary block
- `JournalDetail` body bounds now expand from the `JournalCanvasComponentNode` to include the visible description/objective/summary nodes as well, with larger padding.
- The noisy `AreaMap` hot-path lifecycle debug line was removed.
- This checkpoint is not yet committed at the time of this entry.

**Why it changed**

- `ToDoList` was registering tooltip targets without producing practical on-screen hover behavior, which strongly suggested that the hitbox and target lifetime were still too fragile.
- `JournalDetail` had swung too far toward a single description-like source and stopped reflecting the expected quest plate shape of description + current objective + summary.
- `AreaMap` logs were noisy enough to bury useful hover diagnostics for the other quest-family addons.

**Next step**

- Validate in-game that `ToDoList` now shows tooltips when hovering the row, not just the text.
- Re-check whether `JournalDetail` now shows the correct three-part body without cross-quest contamination.
- Confirm whether `ScenarioTree`, `RecommendList`, and `AreaMap` still need additional trigger/event adjustments after the log noise reduction.

### 2026-04-13 12:55:02 -03:00 - runtime validation checkpoint - ToDoList closed out, JournalDetail narrowed to content stability

**What changed**

- Runtime validation confirmed repeated real `hover` hits for `ToDoList`, not just registrations.
- `ToDoList` is now considered stable and removed from the active quest-tooltip problem list.
- `JournalDetail` continued to produce consistent body hover hits with the larger trigger.
- The remaining `JournalDetail` problem is now the stability of the composed body content and bounds across refreshes.
- This checkpoint is not yet committed at the time of this entry.

**Why it changed**

- The latest logs showed that the previous `ToDoList` work solved the actual UX bug, so continuing to change it would add risk without value.
- The same logs also showed that `JournalDetail` has moved past trigger failure and into a narrower content-composition problem, which is a healthier place to debug from.

**Next step**

- Make the `JournalDetail` body composition less dependent on changing visible node sets.
- Keep `ToDoList` untouched unless a fresh regression appears.
- Do focused runtime passes later for `ScenarioTree`, `RecommendList`, and `AreaMap`.

## Runtime Findings That Matter

These findings were learned from probes, logs, and in-game validation and are still important:

- `Journal` title hover is generally stable.
- `JournalDetail` body is the most fragile quest hover surface.
- `ToDoList` tooltip behavior has improved, but it needs repeated runtime validation because dense quest windows can silently stop re-registering hover targets.
- Addon-wide quest tooltips are sensitive to coordinate space; using local root-node coordinates was not sufficient.
- `ScenarioTree`, `RecommendList`, and `AreaMap` have all had moments where lifecycle events were firing but tooltip registration was missing or not surviving cache-hit paths.
- A tooltip registration event does not guarantee the bounds are good enough for a practical hover experience.
- In dense quest windows, capture/update events and hover-maintenance events may not be the same ideal event.
- The plugin version label in the UI depends on real assembly metadata, not just the config default string.

## Where We Are Now

The repo is in the post-migration stabilization phase for quests.

The main active problems are:

- verifying that the standalone quest handlers all keep tooltip registration alive under real gameplay repaint patterns
- making `JournalDetail` body content fully sheet-first and stable
- confirming that the new `questplates` rows repopulate with the intended fields after the table reset
- confirming that nested hover targets prefer the most specific node instead of whichever overlapping rect happens to win first
- continuing the shared structured-text pipeline so quest logic can later be reused for `ItemTooltip` and `ActionTooltip`

## Next Likely Steps

- Validate the new `JournalDetail` SEQ-only hover body in-game.
- Validate the new `PreDraw` hover maintenance path on `AreaMap`, `ScenarioTree`, and `RecommendList`.
- Confirm whether `QuestTextSheetName` and `SourceContentHash` start populating correctly after the quest table reset.
- Revisit event choice per quest addon:
  - use setup/requested-update style hooks for capture and queueing
  - use a lighter continuous hook only where hover maintenance needs it
- Keep this document append-only from here, with exact timestamps whenever a milestone corresponds to a commit.

### 2026-04-13 13:18:42 -03:00 - working tree checkpoint - JournalDetail persistence aligned with runtime composition

**What changed**

- `JournalDetail` now treats the quest description and current sequence summary as separate pieces again:
  - description comes from `TranslatedQuestMessage`
  - the SEQ row is folded into the summary block instead of replacing the description
- Existing `QuestPlate` rows now backfill missing `TranslatedQuestMessage` on demand instead of assuming that a quest-name-only row already contains the full detail translation.
- `JournalDetail` save/update paths now populate and reuse:
  - `QuestId`
  - `QuestTextSheetName`
  - `SourceContentHash`
  - translated objective rows
  - translated summary / SEQ rows
- Existing rows found from the DB now get their sheet-first quest metadata persisted the first time `JournalDetail` resolves a live quest snapshot.
- Build and tests both passed after this adjustment.
- This checkpoint is not yet committed at the time of this entry.

**Why it changed**

- The latest DB inspection showed real gaps between what `JournalDetail` used at runtime and what it materialized into `questplates`.
- Description text could stay in English because `JournalDetail` sometimes matched an existing row created from the Journal list, then trusted that row even when `TranslatedQuestMessage` was empty.
- Summary and SEQ translation state was being cached and queued, but not consistently materialized into the translated quest maps, which made DB reuse weaker than intended.
- The previous body composition had also drifted into using the SEQ text as the description, which made the tooltip feel inconsistent with the actual quest plate.

**Next step**

- Reload in-game and verify that `JournalDetail` now:
  - shows translated description text reliably after the first resolve
  - keeps the current SEQ row inside the summary block
  - repopulates `questplates` with `QuestTextSheetName` and `SourceContentHash`
- If the new build is loaded, a controlled wipe of `questplates` is now reasonable so the table can repopulate without legacy partial rows.

### 2026-04-13 15:59:46 -03:00 - working tree checkpoint - JournalDetail canonical data isolated from stale UI summaries

**What changed**

- `QuestPlate` merge now persists the sheet-first metadata fields that were
  previously being dropped during save:
  - `QuestTextSheetName`
  - `SourceContentHash`
  - `SystemRows`
  - `TranslatedSystemRows`
- `JournalDetail` no longer aggregates the extra summary-node collection into
  the canonical persisted quest body.
- The quest body now stays anchored to:
  - description
  - current objective
  - live summary text
  - current `SEQ` row
- The older UI-derived summary-node list remains out of the persisted row so it
  cannot contaminate one quest with leftover text from another quest.

**Why it changed**

- Fresh DB inspection showed that several recent `questplates` rows still had
  empty `QuestTextSheetName` and `SourceContentHash`, even after the metadata
  backfill work. The root cause was that the merge routine never copied those
  fields.
- The same DB inspection also confirmed real cross-quest contamination in
  `TranslatedSummariesAsText`: rows such as `Three Beaks to the Wind` and
  `Protecting the Pom` were carrying an unrelated summary fragment from a
  different quest.
- That contamination matches the current `JournalDetail` implementation, which
  was still reading additional visible summary nodes from the live UI and
  persisting them into the row.

**Next step**

- Reload in-game and confirm that new `questplates` rows now populate
  `QuestTextSheetName` and `SourceContentHash`.
- Re-check whether `JournalDetail` tooltips stop inheriting stale summary text
  from previously viewed quests.
- Validate whether translated description text now stabilizes after the first
  translation pass, with fewer repeated `GoogleTranslator` bursts in the log.

### 2026-04-13 19:05:00 -03:00 - working tree checkpoint - quest hover selection and requested-update fallback tightened

**What changed**

- `HoverTooltipManager` now picks the smallest hovered rectangle instead of the
  first matching entry in dictionary iteration order.
- `ScenarioTree` now resolves its `AtkValues` from the live addon when the
  lifecycle event comes through `PreRequestedUpdate` without `AddonRefreshArgs`.
- `AreaMap` now does the same requested-update fallback instead of silently
  returning.

**Why it changed**

- Dense quest windows can register overlapping hover targets. Picking the first
  match is effectively arbitrary, and it can cause a larger quest-title target
  to swallow a smaller objective target beneath the cursor.
- `ScenarioTree` and `AreaMap` were both registered on `PreRequestedUpdate`,
  but their handlers only accepted `AddonRefreshArgs`. That turned one of their
  intended trigger paths into a no-op.
- The latest user validation pointed directly at these two symptoms:
  `ToDoList` showing title tooltips but not objective-node tooltips, and
  `ScenarioTree` staying completely silent.

**Next step**

- Validate in-game whether objective-node hovers now win over broader title-row
  hovers in `_ToDoList`.
- Re-test `ScenarioTree` and `AreaMap` to confirm they now register tooltips in
  sessions where only `PreRequestedUpdate` fires.
- Keep `JournalDetail` work focused on content stability, not trigger liveness.

### 2026-04-13 20:20:09 -03:00 - working tree checkpoint - latest 10-minute log slice suggests quest-addon runtime is still too coupled

**What changed**

- No code changed in this checkpoint; this is a runtime diagnosis milestone.
- The last 10-minute `dalamud.log` slice was reduced almost entirely to
  `JournalList` hovers, with only a few `JournalDetail` body hovers and no
  visible activity from `_ToDoList`, `ScenarioTree`, `RecommendList`, or
  `AreaMap`.
- Screenshot review in the same session showed `JournalDetail` still producing
  inconsistent body content for the same quest and switching between different
  content shapes.

**Why it changed**

- The current quest-family runtime still shares too much transient state across
  addons.
- Even when the shared DB and translation broker are correct abstractions, the
  shared UI caches and hover runtime make it too easy for one addon's
  repaint/hover behavior to dominate the debugging picture while others go
  effectively silent.
- The latest evidence points away from “one shared quest UI cache for all
  addons” and toward “shared data source, isolated addon runtime state.”

**Next step**

- Split quest-addon runtime state by surface:
  - `JournalList`
  - `JournalDetail`
  - `_ToDoList`
  - `ScenarioTree`
  - `RecommendList`
  - `AreaMap`
- Keep the shared pieces limited to:
  - `QuestPlate` persistence
  - Lumina/sheet resolvers
  - live quest-progress resolvers
  - translation broker
- Treat reflection cleanup as a separate follow-up pass once the quest-addon
  runtime stops regressing across unrelated surfaces.

### 2026-04-13 21:23:11 -03:00 - working tree checkpoint - accepted quest prefetch runtime added

**What changed**

- Added `AcceptedQuestPrefetchRuntime` as a partial `Echoglossian` runtime helper.
- The framework tick now performs a lightweight accepted-quest prefetch pass
  when translation is enabled and any quest-family addon is enabled.
- The prefetch runtime:
  - reads accepted quests from `QuestManager`
  - resolves each quest through `QuestProgressResolver`
  - seeds or updates canonical `questplates` rows with:
    - `QuestId`
    - `QuestTextSheetName`
    - `SourceContentHash`
    - current SEQ body text
    - objectives
    - summaries
    - system rows
  - queues missing translations through the existing paced broker
- The runtime is paced intentionally:
  - at most `2` quests per tick cycle
  - one prefetch cycle every `2` seconds
  - only when the accepted-quest signature changes or the queue is still
    draining
- Dispose now clears the accepted-quest prefetch state explicitly.

**Why it changed**

- Quest-family surfaces were still doing too much discovery at the moment the
  UI opened, which makes regressions feel random and contributes to cold-open
  stutter.
- The new accepted-quest prefetch path lets us warm `questplates` and the
  queued translation cache from stable runtime data before `Journal`,
  `JournalDetail`, `ScenarioTree`, or other quest surfaces need to render.
- This follows the emerging architecture more closely:
  - shared sources stay shared (`QuestManager`, Lumina, `QuestPlate`, broker)
  - addon UI/runtime state stays local to each surface

**Next step**

- Validate in-game that newly accepted quests start appearing in `questplates`
  with sheet metadata before their addon surfaces are opened.
- Keep isolating addon-local runtime state, starting with `JournalDetail`, so
  hover/content regressions cannot leak across quest-family addons.
- After runtime isolation is stable, audit the remaining reflection-heavy quest
  helpers for lower-overhead alternatives.

### 2026-04-13 21:35:00 -03:00 - working tree checkpoint - ScenarioTree, RecommendList, and AreaMap moved off shared UI caches

**What changed**

- `ScenarioTree`, `RecommendList`, and `AreaMap` now maintain their translated
  text state in handler-local runtime caches rather than reusing the broader
  quest-family UI caches.
- `ScenarioTree` now keeps:
  - local translated-text cache entries keyed by quest progress
  - local hover payload entries per visible slot
  - a `PreDraw` path that promotes queued results into the local cache before
    building the combined tooltip
- `RecommendList` now keeps:
  - local translated-text cache entries keyed by visible quest name
  - local hover payload entries keyed by visible node pointer
  - `PreDraw` refresh based on its own local state instead of the shared hover
    cache
- `AreaMap` now keeps:
  - local translated-text cache entries keyed by the visible area-map quest row
  - local hover text state that can promote queued translations during draw

**Why it changed**

- The latest logs and screenshot review suggested that the shared quest-family
  UI caches were still making the runtime picture too coupled: one addon's
  state could dominate the session while other addons went quiet.
- These three surfaces are all small enough that local runtime caches are a
  better fit than a shared UI-layer cache, while still leaving the real shared
  sources untouched:
  - `questplates`
  - Lumina/sheet resolvers
  - live progress resolvers
  - translation broker
- This is the next step in the strategy of “shared data sources, isolated addon
  runtime state.”

**Next step**

- Validate in-game whether `ScenarioTree`, `RecommendList`, and `AreaMap` now
  start leaving clearer, more isolated hover behavior in the logs.
- Continue focusing `JournalDetail` work on content stability rather than
  trigger liveness, now that more of the quest-family runtime is decoupled.

### 2026-04-13 21:45:00 -03:00 - working tree checkpoint - accepted quest prefetch now signals start and completion

**What changed**

- Added two localized notification messages for the accepted-quest prefetch
  runtime:
  - start
  - completion
- The prefetch runtime now raises one notification when a new accepted-quest
  prefetch queue begins and one when that queue fully drains.
- The notifications are tied to the prefetch queue lifecycle itself, not to any
  individual quest addon.

**Why it changed**

- The user wanted direct feedback about when the background quest-list
  translation work starts and ends.
- This makes it easier to correlate in-game behavior with the new
  `QuestManager`-driven prewarm path without having to inspect logs for every
  test pass.
- The notifications were added with queue-level state so they do not spam every
  quest translation callback.

**Next step**

- Rebuild the plugin and verify that:
  - one notification appears when accepted-quest prefetch begins
  - one notification appears when it completes
  - no repeated spam occurs while the queue is still draining

### 2026-04-13 22:05:00 -03:00 - working tree checkpoint - accepted quest prefetch now logs translation activity explicitly

**What changed**

- `AcceptedQuestPrefetchRuntime` now emits a structured activity trace for the
  background accepted-quest pipeline into:
  - `accepted-quest-prefetch-activity.log`
- The prefetch runtime now logs, per accepted quest:
  - quest selected for processing
  - resolve failure
  - resolve success
  - existing persisted row state
  - per-field/per-row translation state:
    - `skip-existing`
    - `skip-empty`
    - `cache-hit`
    - `queued`
    - `already-in-flight`
    - `resolved`
- These same activity transitions are also written into the normal Dalamud log
  with the `[AcceptedQuestPrefetch]` prefix.

**Why it changed**

- The user saw start/end notifications but no diagnostic file and no clear
  proof that quest background translation was actually being queued or
  resolved.
- The earlier canonical dump only emitted after quest progress resolution, so
  it could stay silent if the pipeline failed before that point.
- We needed exact evidence for whether the accepted-quest background path is:
  - discovering quests only
  - failing before canonical assembly
  - hitting existing rows
  - relying on broker cache
  - or actually queueing and resolving translations

**Next step**

- Reload the plugin and inspect:
  - `accepted-quest-prefetch-activity.log`
  - `accepted-quest-prefetch-canonical.log`
- Compare the activity trace against the DB rows to see whether missing quest
  data comes from resolve failures, skipped rows, cache hits, or incomplete
  persistence after successful translation.

### 2026-04-13 22:20:00 -03:00 - working tree checkpoint - accepted quest prefetch now resolves QuestManager ids correctly

**What changed**

- `QuestProgressResolver` now accepts both:
  - full `Quest.RowId` values
  - short runtime/work ids coming from `QuestManager`
- When the incoming quest id is below `0x10000`, the resolver now promotes it
  to the full `Quest.RowId` space by adding `0x10000` before looking up the
  Lumina `Quest` row.
- The resolved `QuestProgressSnapshot.QuestId` now stays anchored to the full
  `Quest.RowId`, which aligns accepted-quest prefetch output with the ids seen
  elsewhere in Journal and in the DB.

**Why it changed**

- The new accepted-quest activity logs showed a clean and repeatable failure:
  every quest discovered from `QuestManager` reached `quest-start` and then
  immediately died in `resolve-failed`.
- The logged ids were values like `1475` and `4393`, which do not match the
  full quest ids used in Journal (`67011`, `69929`, etc.), but do match the
  low 16-bit/runtime form of those ids.
- The resolver was incorrectly feeding those short runtime ids directly into
  `questSheet.TryGetRow(...)` as if they were already full Lumina row ids.

**Next step**

- Reload the plugin and confirm that accepted-quest prefetch now starts
  producing:
  - `resolved`
  - `existing-row`
  - translation queue/cache events
  - canonical dump file output
- Compare the new canonical dumps against the DB rows to validate that accepted
  quests now prewarm real `questplates` entries before Journal surfaces are
  opened.

### 2026-04-14 00:20:00 -03:00 - working tree checkpoint - canonical quest rows now lead persistence and merge

**What changed**

- `QuestCanonicalData.ToQuestPlate(...)` now materializes the quest through
  `QuestPlate.ApplyCanonicalPayload(...)` instead of seeding only the old
  text-keyed dictionaries.
- `QuestPlate` persistence now includes `CanonicalRowsAsText` as the intended
  source-of-truth payload for quest text rows.
- `QuestPlate.Canonical.cs` now:
  - loads canonical rows from persisted JSON
  - falls back to rebuilding canonical rows from older projections when needed
  - preserves translated row text by row key during payload replacement
  - rebuilds legacy compatibility dictionaries from canonical rows
- `DbOperations.MergeQuestPlateValues(...)` now merges canonical rows first and
  treats the older dictionary fields as compatibility output rather than the
  primary merge surface.
- The pending migration and model snapshot were updated to include
  `CanonicalRowsAsText`.

**Why it changed**

- The accepted-quest diagnostics showed the canonical quest assembly was rich,
  but the projected `QuestPlate` shape still looked too poor and too dependent
  on the older text-keyed dictionaries.
- That gap meant background prefetch could resolve the quest correctly while
  persistence still only saved a thinner, more failure-prone projection.
- We needed one persisted quest payload that actually represents the full
  quest rows and can survive repeated merges without losing row identity.

**Next step**

- Reload the plugin and inspect:
  - `accepted-quest-prefetch-canonical.log`
  - `accepted-quest-prefetch-activity.log`
  - the `questplates` table
- Verify that new prefetched rows now persist:
  - `CanonicalRowsAsText`
  - `QuestTextSheetName`
  - `SourceContentHash`
  - fuller translated/original row coverage
- If the new canonical payload looks correct, continue by simplifying
  `JournalDetail` and the other quest handlers to consume the canonical quest
  row model more directly and rely less on UI-composed state.

### 2026-04-14 11:40:00 -03:00 - working tree checkpoint - fixed quest canonical column migration drift

**What changed**

- Reverted the already-applied `20260414103000_AddCanonicalQuestRowPayloads`
  migration so it again reflects the schema that existing databases actually
  received.
- Added a new additive migration:
  - `20260414120000_AddQuestCanonicalRowsColumn`
- That new migration adds only `CanonicalRowsAsText`, which is the missing
  column the runtime now expects when loading `QuestPlate`.

**Why it changed**

- The accepted-quest prefetch logs showed that background translation was
  absolutely running, but persistence still failed immediately with:
  - `SQLite Error 1: 'no such column: q.CanonicalRowsAsText'`
- The root cause was migration drift:
  - the earlier migration id was already recorded in the user's DB history
  - but the file had later been edited to add `CanonicalRowsAsText`
  - so the DB history said the migration existed while the actual table did not
    have the new column
- A new additive migration is the safe fix for both:
  - already-migrated user DBs
  - clean DBs created from scratch

**Next step**

- Rebuild and reload the plugin so the new migration runs.
- Confirm that:
  - `questplates` now has `CanonicalRowsAsText`
  - accepted-quest prefetch still resolves and translates
  - rows finally begin to persist instead of dying during `FindQuestPlate`

### 2026-04-14 12:10:00 -03:00 - working tree checkpoint - fixed in-memory quest translation loss before serialization

**What changed**

- `QuestPlate.UpdateFieldsFromText()` now preserves in-memory canonical rows and
  translated row dictionaries when the serialized `...AsText` fields are still
  empty.
- Added `QuestPlatePersistenceTests` to lock the regression:
  - an unsaved canonical translated row must survive a text refresh and still
    rebuild the translated legacy projections

**Why it changed**

- The accepted-quest prefetch logs proved translations were resolving, and the
  DB proved the original canonical quest payload was being saved.
- But the translated quest-row payload never reached persistence.
- The root cause was lifecycle-related:
  - prefetch created a fresh `QuestPlate`
  - applied translated canonical rows in memory
  - then merge/save called `UpdateFieldsFromText()`
  - because the serialized fields were still empty, that refresh wiped the
    in-memory translated rows before `UpdateFieldsAsText()` could serialize them

**Next step**

- Reload the plugin and verify that:
  - `TranslatedObjectiveRowsByKeyAsText`
  - `TranslatedSummaryRowsByKeyAsText`
  - `TranslatedSystemRowsByKeyAsText`
  - `CanonicalRowsAsText` with non-null `TranslatedText`
  now begin to fill during accepted-quest prefetch
- If persistence starts filling correctly, then clean up the Journal-side
  retraduction guard so PT text is not sent to translation again

### 2026-04-14 13:40:00 -03:00 - working tree checkpoint - canonical row matching now respects row keys for duplicate texts

**What changed**

- `QuestPlate.Canonical.cs` now resolves canonical translated rows by exact
  `RowKey` first and only falls back to source text when no row key was
  supplied.
- Added a regression test covering duplicated objective text with distinct row
  keys so both rows keep their own translated value.

**Why it changed**

- After persistence started working again, duplicate text rows inside the same
  quest still collided.
- Example: two `TODO` rows with the same source text such as
  `"Speak with Kupopo."`
  could both translate successfully in logs, but only the first row would keep
  the translated payload in the canonical model.
- The cause was canonical row matching using:
  - `rowKey OR sourceText`
  in a single lookup, which let an earlier duplicate text row win even when a
  more specific row key had been provided.

**Next step**

- Reload the plugin and confirm that duplicate-text rows now persist translated
  payload independently by `RowKey`.
- Re-read the DB for quests such as `70391` and confirm previously missing
  duplicate `TODO` rows no longer stay `null`.

### 2026-04-14 14:35:00 -03:00 - working tree checkpoint - documented per-addon quest flow and remediation plan

**What changed**

- Added a new operational doc:
  - `docs/quest-addon-detailed-flow-and-remediation-plan.md`
- The new document records, per quest-family addon:
  - current trigger model
  - current data source mix
  - local cache scope
  - current validation status
  - recommended remediation path
- The document also records a priority order for the next quest-addon cleanup
  passes now that canonical DB persistence is behaving correctly.

**Why it changed**

- The quest DB and canonical payload are now in a much better place, so the
  next source of confusion is no longer persistence alone.
- We needed one place that answers, addon by addon:
  - what each handler is doing today
  - what is already good enough
  - what still needs to be reworked
- Without this, it is too easy to keep mixing runtime observations,
  persistence work, and per-addon cleanup into one blurry stream.

**Next step**

- Use the new doc as the working map for quest-addon cleanup.
- Start with `JournalDetail` as the first canonical-first consumer, then move
  to `ScenarioTree`, `RecommendList`, and `AreaMap`.

### 2026-04-14 15:05:00 -03:00 - working tree checkpoint - quest handlers narrowed to Journal and Journal switched to DB-only runtime

**What changed**

- Stopped registering the quest addon handlers we are not actively stabilizing:
  - `JournalAccept`
  - `JournalResult`
  - `ScenarioTree`
  - `RecommendList`
  - `_ToDoList`
  - `AreaMap`
- Kept only `Journal` and `JournalDetail` active in the quest-family handler
  registration.
- Removed the local translation-generation paths from `JournalHandler`:
  - no more `QueueTranslation` for Journal list titles
  - no more `QueueTranslation` or `QueueTranslationBatch` for Journal detail
  - no more Journal-side queue fallback for summaries, objectives, or current
    sequence rows
- Journal now behaves as:
  - DB-first when a canonical `QuestPlate` row exists
  - original-text fallback when the DB is not warm yet
- Journal local caches now avoid storing untranslated fallback values, so a
  later DB warmup can still be picked up while the addon remains open.

**Why it changed**

- The user wanted to stop the quest addons from continuing to invent or
  generate translations locally while the DB and prefetch path are now healthy.
- The cleanest stabilization move is to reduce the active quest-family runtime
  to a single surface and make that surface consume the normalized DB instead
  of reenqueuing translation work.
- This also prevents regressions caused by multiple quest addons still running
  their own local queue paths while we are trying to validate one addon family
  at a time.

**Next step**

- Validate `Journal` and `JournalDetail` in-game in DB-first mode.
- Confirm that:
  - list titles read from the DB
  - detail body no longer enqueues translation locally
  - opening Journal while prefetch is warm gives stable native text and tooltip
    content
- After that, continue the canonical-first cleanup in `JournalDetail` itself.

### 2026-04-14 15:40:00 -03:00 - working tree checkpoint - Journal hover modes now require ready translated payloads

**What changed**

- Added an explicit mode helper gate for quest-family hover rendering:
  - `QuestAddonModeHelpers.CanRenderHoverTooltip(...)`
- Extended translated-hover registration so callers can say whether the
  translated payload is actually ready for the current mode.
- Removed silent fallback between original and translated text inside the hover
  registration helper.
- `JournalHandler` now passes readiness explicitly for:
  - Journal list titles
  - JournalDetail title tooltip
  - JournalDetail body tooltip
  - completed-quest title/message/body tooltips
- `Journal` and `JournalDetail` now also clear their hover prefix immediately
  when hover mode is disabled, instead of waiting for stale-entry cleanup.

**Why it changed**

- The old hover helper still allowed a tooltip to appear when the configured
  mode expected translated content, but only original fallback text was
  available.
- That made `TooltipTranslation` misleading, because the user could hover a
  “translation tooltip” and still get original text.
- It also made `NativeUiTranslationWithOriginalTooltips` misleading, because
  the swap tooltip could appear before the translated/native side was actually
  ready.
- The desired contract is stricter:
  - native-only mode: no quest-family hover tooltip
  - tooltip-translation mode: no tooltip until translated payload is ready
  - swap mode: no original-text tooltip until the translated/native payload is
    ready

**Next step**

- Validate the three Journal modes in-game:
  - native-only
  - tooltip translation
  - native translation with original tooltips
- Confirm specifically that:
  - no tooltip appears in translation mode while only original fallback exists
  - no swap/original tooltip appears before the translated Journal payload is
    ready
  - stale Journal hover targets disappear immediately after switching back to
    native-only mode

### 2026-04-14 16:05:00 -03:00 - working tree checkpoint - Journal mode switching now reapplies on live detail updates

**What changed**

- `JournalDetail` now participates in `PreUpdate`, not only sparse requested
  update events.
- The active-detail handler no longer treats the native-only cached path as a
  no-op; when cached translated payloads already exist, it now reapplies the
  translated native text immediately.
- Original-detail snapshots remain the source used to restore native text when
  switching back to tooltip-only mode.

**Why it changed**

- Switching modes from the config UI while `JournalDetail` stayed open could
  leave the detail pane in the last-applied visual state until the addon
  happened to emit another requested update.
- Native-only mode also had a fast-path bug where fully cached detail content
  returned early before writing translated text into the native nodes.
- Those two gaps together made mode switching feel inconsistent and made swap
  mode look broken even when the DB payload itself was healthy.

**Next step**

- Validate in-game that:
  - `TooltipTranslation -> NativeUiTranslation -> TooltipTranslation`
    restores the original native Journal text correctly
  - `NativeUiTranslationWithOriginalTooltips` writes translated text into the
    native Journal UI and shows original text only in the tooltip
  - mode changes apply while `JournalDetail` remains open, without requiring a
    quest reselection

### 2026-04-14 16:30:00 -03:00 - working tree checkpoint - JournalDetail split into its own handler and toggle

**What changed**

- `JournalDetail` now has its own config surface:
  - `TranslateJournalDetail`
  - `JournalDetailTranslationDisplayMode`
- `AddonHandlerWiring` now registers `Journal` and `JournalDetail` separately.
- `JournalHandler` was reduced to the Journal list only.
- A new `JournalDetailHandler` now owns:
  - detail-node capture
  - detail hover registration
  - detail original snapshot restoration
  - detail-local caches and scope keys
- The global hover-draw gate now knows that `JournalDetail` can be active even
  when the Journal list is disabled.

**Why it changed**

- The next stabilization target is `JournalDetail`, and keeping it inside the
  same handler/runtime surface as the Journal list kept making it harder to
  reason about regressions.
- The user explicitly wanted `JournalDetail` isolated so work on the detail
  body, tooltips, and caches cannot contaminate the already-stable Journal
  title list.
- Separating the config toggle and display mode also gives us a cleaner path
  for validating `JournalDetail` independently in game.

**Next step**

- Validate that `Journal` continues to behave as before with the list-only
  handler.
- Start treating `JournalDetail` as the next dedicated stabilization target,
  including any further mode, cache, and tooltip work without re-touching the
  Journal list unless necessary.

### 2026-04-14 16:50:00 -03:00 - working tree checkpoint - JournalDetail lifecycle reconnected to Journal selection flow

**What changed**

- `JournalDetailHandler` is still a separate handler with separate config and
  local runtime state.
- It is now also registered against the `Journal` addon lifecycle, in addition
  to `JournalDetail`.
- The handler now accepts both `Journal` and `JournalDetail` lifecycle events
  for refresh and cleanup.

**Why it changed**

- In practice, isolating the detail runtime was correct, but wiring it only to
  `JournalDetail` was too strict.
- The Journal list still drives selection changes that the detail pane needs to
  react to, and the separate toggle also needed to work without relying on a
  reload-time registration coincidence.
- This keeps the runtime separated while reconnecting it to the real selection
  lifecycle that feeds the detail pane.

**Next step**

- Revalidate all three `JournalDetail` modes in game:
  - native-only
  - tooltip translation
  - native translation with original tooltips
- Confirm that selection changes and mode switches now both reach the detail
  handler consistently.

### 2026-04-14 22:20:00 -03:00 - working tree checkpoint - JournalDetail selection refresh narrowed and summary collection restored

**What changed**

- `JournalDetailHandler` now treats `Journal` lifecycle events as a
  selection-driven refresh path only when they arrive through
  `PostRequestedUpdate`.
- Cleanup is once again scoped to the real `JournalDetail` addon closing,
  instead of reacting to any matching `Journal` cleanup event.
- The handler now recollects visible summary rows from the detail pane and
  filters them against the active canonical quest payload before using them.
- Extra visible summary rows now participate in:
  - native-mode writes
  - swap restoration
  - tooltip-body assembly
- Tooltip readiness for the body now also depends on those visible summary
  rows being translated, not just the single primary summary node.

**Why it changed**

- The broader `Journal` lifecycle hookup was making `JournalDetail` react too
  early during quest selection changes, which is the most likely reason the
  detail pane drifted back into tooltip-only behavior after a reselection.
- The split handler had also lost the old summary-row collection path, so
  quests with visible summary sections could end up missing part of the body in
  the tooltip.
- Filtering visible summary rows through the canonical quest payload keeps the
  summary coverage while avoiding the stale cross-quest contamination that the
  earlier implementation suffered from.

**Next step**

- Revalidate in game that:
  - changing the selected quest keeps honoring the active `JournalDetail` mode
  - native-only and swap continue to work after reselection
  - tooltip bodies now include visible summary content when the quest has it
  - no stale summary rows from other quests reappear in the detail tooltip

### 2026-04-14 23:05:00 -03:00 - working tree checkpoint - JournalDetail summary path switched to canonical quest rows

**What changed**

- `JournalDetail` now derives its description from the current canonical `SEQ`
  row resolved from `QuestManager` and `QuestCanonicalData`.
- The detail summary block is now projected from canonical prior `SEQ` rows up
  to the current quest sequence, instead of relying on the visible summary
  texts as semantic input.
- Supplemental summary nodes in the live addon are now treated as display
  anchors only:
  - their original text is snapshotted for restoration
  - their translated/native content is assigned from canonical summary rows
- Hover body composition for `JournalDetail` now uses canonical summary text
  sections, not the volatile UI summary text set.

**Why it changed**

- The native-translation path was still only translating part of the
  `JournalDetail` body because the summary block depended on whatever text the
  UI happened to expose in that frame.
- That meant summary translation could lag, partially apply, or drift when the
  detail pane changed quests.
- Moving the summary composition to `QuestManager + QuestCanonicalData +
  QuestPlate` removes the most fragile UI dependency from the detail pane while
  keeping the live nodes only for anchoring and restoration.

**Next step**

- Validate in game that native translation mode now updates both description
  and summary consistently when switching quests.
- Keep the current objective line under review until TODO row selection is also
  derived canonically from live progress instead of the visible objective node.

### 2026-04-14 23:30:00 -03:00 - working tree checkpoint - JournalDetail native summary consolidated into one node

**What changed**

- `JournalDetail` now collapses the canonical summary block into a single
  display string when writing native translation.
- The primary summary node receives the full canonical summary text for the
  active quest phase.
- Supplemental summary nodes are cleared in native mode instead of being fed a
  partial subset of translated rows.
- When the handler returns to a non-native mode, the original primary summary
  text and original supplemental summary node texts are restored from the
  snapshot.

**Why it changed**

- Some quests expose multiple summary paragraphs in the detail pane, and the
  previous projection still distributed those paragraphs across multiple live
  nodes.
- That left room for one node to stay in English or drift out of sync even
  though the canonical summary data was already correct in the DB.
- Collapsing the translated summary into a single node narrows the render path
  and better matches the canonicity work already done in `QuestManager +
  QuestCanonicalData + QuestPlate`.

**Next step**

- Validate in game that quests with multi-paragraph summaries no longer leave a
  trailing English block in `JournalDetail`.
- If the primary summary node clips the longer text, inspect whether the live
  node layout needs a small size adjustment in native mode.

### 2026-04-15 00:20:00 -03:00 - working tree checkpoint - JournalDetail summary node restoration and supplemental-node capture hardened

**What changed**

- `JournalDetail` now snapshots the primary summary node presentation for each
  scope:
  - width
  - text flags
  - font size
  - summary container height
- The handler now snapshots the discovered supplemental summary node addresses
  as part of the quest-detail scope.
- Supplemental summary discovery was broadened so it no longer depends only on
  the old `480700..480750` node-id window and no longer skips empty text nodes.
- In native mode, the primary summary node is expanded with multiline auto-size
  flags and the summary container height is raised to fit the translated block.
- On restoration, the original primary summary presentation and the original
  supplemental summary texts are reapplied from the scope snapshot.

**Why it changed**

- After collapsing the canonical summary into the primary summary node, some
  quests still left residual English text behind because not every supplemental
  node was being rediscovered and cleared.
- Once a supplemental node had been cleared, the previous collector could stop
  finding it entirely because it filtered out empty nodes, which made
  restoration fragile.
- The longer canonical summary blocks also needed the primary node's original
  presentation to be expanded predictably in native mode instead of relying on
  whatever size state happened to be active that frame.

**Next step**

- Validate in game that:
  - residual English paragraphs are gone from `JournalDetail`
  - long translated summaries no longer stack over stale nodes
  - switching back out of native mode fully restores the original summary
    presentation
