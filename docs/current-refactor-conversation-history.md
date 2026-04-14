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

## Reference Docs

- [Refactor Timeline and Flow Analysis](./refactor-timeline-and-flow-analysis.md)
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
