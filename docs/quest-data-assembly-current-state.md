# Quest Data Assembly Current State

## Purpose

This document explains how Echoglossian currently assembles quest data, where
that data comes from, what gets persisted into `questplates`, and why the
current behavior can still feel inconsistent.

This is a current-state document. It exists to make the present implementation
explicit before the next quest-data refactor.

## Executive Summary

Today, a quest in Echoglossian is assembled from three different layers:

1. `QuestManager` for accepted quests and live sequence
2. Lumina sheets for canonical quest text rows
3. addon UI nodes for some display-time composition, especially `JournalDetail`

The persistence target is `questplates`, but the runtime still does not consume
that canonical shape uniformly across all quest-family addons.

That is why these can all be true at the same time:

- the accepted-quest prefetch notification says `26`
- the DB only contains `16` quest rows
- some rows look only partially populated
- `JournalDetail` can still show content that does not fully match the DB row

## Why The Prefetch Notification Said 26

The accepted-quest prefetch notification counts the number of accepted quests
discovered from `QuestManager`, not the number of complete DB rows.

Implementation:

- [AcceptedQuestPrefetchRuntime.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/AcceptedQuestPrefetchRuntime.cs)

The queue count comes from:

- `acceptedQuestPrefetchQueue.Count`

That queue is built by:

- `TryCollectAcceptedQuestIds(...)`

Which reads:

- `QuestManager.Instance()->NormalQuests`
- `QuestManager.Instance()->DailyQuests`

So `26` means:

- the runtime discovered `26` accepted quests

Important detail:

- the ids coming from `QuestManager` are the short runtime/work ids
- they are not the full `Quest.RowId` values seen in Journal/Lumina rows
- the prefetch resolver now promotes those short ids into full quest row ids by
  adding `0x10000` before looking up the `Quest` sheet when needed

Example:

- runtime/work id `1475` corresponds to full `Quest.RowId` `67011`
- runtime/work id `4393` corresponds to full `Quest.RowId` `69929`

It does not mean:

- `26` `questplates` rows were inserted
- `26` quests successfully resolved through Lumina
- `26` quests finished background translation
- `26` rows are complete

## Why The DB Can Show Fewer Rows

A quest only becomes a useful `QuestPlate` row if all of these happen:

1. it is found in `QuestManager`
2. it resolves through Lumina
3. its quest text sheet mounts correctly
4. a canonical `QuestPlate` is built
5. that row is inserted or merged
6. translation callbacks eventually enrich the translated fields

If one of those later steps does not happen, the prefetch count can still say
`26` while the DB contains fewer rows.

## Current Sources Of Quest Data

### 1. Live accepted quest list

Source:

- `QuestManager`

Code:

- [AcceptedQuestPrefetchRuntime.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/AcceptedQuestPrefetchRuntime.cs)

Purpose:

- discover accepted quests
- build a stable accepted-quest signature
- trigger background prewarm when that set changes

### 2. Stable quest identity and text rows

Sources:

- Lumina `Quest` sheet
- mounted raw quest text sheet

Code:

- [QuestLuminaResolver.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/QuestLuminaResolver.cs)
- [QuestProgressResolver.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/QuestProgressResolver.cs)

Current behavior:

- `QuestLuminaResolver` resolves `QuestId` from a visible quest name
- `QuestProgressResolver` loads the Lumina `Quest` row
- it derives `QuestTextSheetName` from `Quest.Id`
- it mounts the raw quest text sheet
- it reads row key/value pairs
- it classifies rows into:
  - `_TODO_`
  - `_SEQ_`
  - `_SYSTEM_`

Current snapshot shape:

- `QuestId`
- `QuestSequence`
- `QuestName`
- `QuestSheetName`
- `QuestSteps`
- `QuestSeqTexts`
- `QuestSystemTexts`
- `ContentHash`

### 3. Addon-local display state

Source:

- native UI nodes

Primary example:

- `JournalDetail`

Code:

- [JournalHandler.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/AddonHandlers/Quest/JournalHandler.cs)

Current behavior:

- the runtime still reads UI nodes for the detail display
- it then reconciles those values with:
  - queued translations
  - `questplates`
  - the current quest progress snapshot

This is one of the main remaining reasons the detail tooltip can still drift
from the persisted canonical row.

## How `QuestPlate` Is Built Today

The canonical prefetch plate is created in:

- [AcceptedQuestPrefetchRuntime.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/AcceptedQuestPrefetchRuntime.cs)
- [QuestCanonicalData.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/QuestCanonicalData.cs)

Method:

- `CreateAcceptedQuestPrefetchPlate(...)`
- `QuestCanonicalData.Create(...)`
- `QuestCanonicalData.ToQuestPlate(...)`
- `QuestPlate.ApplyCanonicalPayload(...)`

Today it fills these scalar fields immediately:

- `QuestId`
- `QuestName`
- `OriginalQuestMessage`
  - current `SEQ` row text only
- `OriginalLang`
- `TranslationLang`
- `TranslationEngine`
- `GameVersion`
- `QuestTextSheetName`
- `SourceContentHash`

And it now seeds a canonical row payload first:

- `CanonicalRows`
  - all `_SEQ_`, `_TODO_`, and `_SYSTEM_` rows
  - with:
    - section
    - row key
    - original text
    - translated text
    - stable order
    - current-sequence marker

Legacy compatibility projections are then rebuilt from that canonical payload:

- `Objectives`
- `Summaries`
- `SystemRows`
- translated counterparts

Important detail:

- `CanonicalRows` is now the intended persisted source of truth
- legacy text-keyed dictionaries are compatibility projections only
- translated dictionaries are still populated incrementally by background callbacks

## Current Lossy Projection Problem

The current DB-compatible compatibility surface is still text-keyed for
`Objectives`, `Summaries`, and `SystemRows`.

That means:

- if two different quest rows have the same visible text
- but different row keys

they collapse into a single dictionary entry in the current persistence shape.

The new `QuestCanonicalData` helper and canonical row payload make this visible
by separating:

- row-keyed canonical maps
- text-keyed compatibility maps

This now solves the main persistence gap for new rows because the full canonical
payload is stored separately in `CanonicalRowsAsText`, while the lossy
projections remain only as compatibility surfaces.

## What The Background Prefetch Queues

The accepted-quest prefetch runtime queues translation for:

- quest name
- current quest message
- every `SEQ` row
- every `TODO` row
- every `SYSTEM` row

Code:

- [AcceptedQuestPrefetchRuntime.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/AcceptedQuestPrefetchRuntime.cs)

Methods:

- `PrefetchAcceptedQuestName(...)`
- `PrefetchAcceptedQuestCurrentMessage(...)`
- `PrefetchAcceptedQuestSummaries(...)`
- `PrefetchAcceptedQuestObjectives(...)`
- `PrefetchAcceptedQuestSystemRows(...)`

So the intent is already correct:

- background translation should cover the canonical quest payload

The remaining problem is not the intent. It is that the display-time addon logic
still does not consume that canonical payload consistently enough.

## What Gets Persisted

Persistence happens through:

- [DbOperations.cs](/C:/Dante/_dalamud/Echoglossian/DBHelpers/DbOperations.cs)

Relevant methods:

- `InsertQuestPlate(...)`
- `UpdateQuestPlate(...)`
- `TryFindQuestPlateForSave(...)`
- `MergeQuestPlateValues(...)`

For working-tree diagnostics, the accepted-quest prefetch path now also emits a
structured file dump in the DB directory via:

- [DiagnosticFileEmitter.cs](/C:/Dante/_dalamud/Echoglossian/GeneralHelpers/DiagnosticFileEmitter.cs)

Current purpose file:

- `accepted-quest-prefetch-canonical.log`

That file appends one structured block per accepted quest payload containing:

- `QuestCanonicalData.ToString()`
- the projected `QuestPlate.ToString()`

This is meant for validating the assembled quest shape outside the normal
Dalamud log stream.

There is now a second purpose file for the prefetch activity itself:

- `accepted-quest-prefetch-activity.log`

That file appends one structured block per important prefetch event, including:

- accepted quest selected for processing
- quest-progress resolve failure
- quest-progress resolve success
- existing-row translation state
- per-field or per-row translation:
  - `skip-existing`
  - `skip-empty`
  - `cache-hit`
  - `queued`
  - `already-in-flight`
  - `resolved`

This activity file is the first place to inspect when the user sees accepted
quest notifications but no canonical payload file, because it captures whether
the pipeline failed before quest resolution, before queueing, or after the
translation broker returned.

Current save priority:

1. match by `QuestId + TranslationLang + TranslationEngine + GameVersion`
2. else match by `QuestName + OriginalQuestMessage + lang/engine/version`
3. else match by `QuestName + lang/engine/version`

Current merge behavior:

- scalar fields are copied when the source has a non-empty value
- canonical rows are now merged as the primary persisted payload
- existing translated row text is preserved by row key when the same canonical
  row still exists in a newer payload
- legacy dictionaries are rebuilt from canonical rows after merge
- later translations still enrich the same row

This supports incremental prewarm, but it does not mean a row is complete the
moment it first appears.

## Current Meaning Of Main `QuestPlate` Fields

### Scalar fields

- `QuestId`
  - stable numeric quest identity
- `QuestName`
  - visible quest title
- `OriginalQuestMessage`
  - currently the current `SEQ` row text, not the full detail body
- `TranslatedQuestName`
  - translated title
- `TranslatedQuestMessage`
  - translated current `SEQ` row text, not the whole detail body
- `QuestTextSheetName`
  - mounted sheet path like `quest/043/AktKmb114_04393`
- `SourceContentHash`
  - fingerprint of all current translatable quest rows

### Dictionary-backed fields

- `CanonicalRowsAsText`
  - full persisted canonical quest payload
- `ObjectivesAsText`
  - original `_TODO_` rows
- `SummariesAsText`
  - original `_SEQ_` rows
- `SystemRowsAsText`
  - original `_SYSTEM_` rows
- `TranslatedObjectivesAsText`
  - translated `_TODO_` rows
- `TranslatedSummariesAsText`
  - translated `_SEQ_` rows
- `TranslatedSystemRowsAsText`
  - translated `_SYSTEM_` rows

## Why The Current Shape Still Feels Confusing

There are still two main conceptual mismatches.

### 1. `OriginalQuestMessage` is not the whole quest body

Today, `OriginalQuestMessage` effectively means:

- current `SEQ` text

But many addon surfaces, especially `JournalDetail`, want a richer body:

- description
- current objective
- current or relevant summary

So a user can reasonably expect “quest message” to mean “the whole quest body,”
while the persistence model currently uses it for only one part.

### 2. `JournalDetail` still partly composes from UI

The canonical row may already contain:

- `TODO`
- `SEQ`
- `SYSTEM`

But `JournalDetail` still composes some output from live UI nodes and mixes that
with queued and persisted state.

That means the runtime can still show:

- repeated sections
- summary drift
- partially translated detail bodies

even when the DB row itself is more stable than the UI.

## What HaselDebug Already Does

HaselDebug already has a quest-specific path that is closer to the model we
want than a UI-driven assembly path.

Relevant implementation:

- `HaselDebug/Utils/UnlocksTabUtils.cs`

Key logic:

- mount quest text from:
  - `quest/{(quest.RowId - 0x10000) / 100:000}/{quest.Id}`
- get live sequence from:
  - `QuestManager.GetQuestSequence((ushort)(quest.RowId - 0x10000))`
- render every `TEXT_<Quest.Id>_SEQ_<nn>` up to the current sequence

What this tells us:

- HaselDebug does not trust `JournalDetail` UI nodes as the long-term quest source
- it uses `Quest` row metadata plus live sequence
- it reads quest text directly from the quest text sheet

The `Instances` tab itself is generic object inspection, but it is still useful
because it exposes the live `QuestManager` arrays directly.

## Current Gaps

### Gap 1: prefetch count is not completion quality

The notification is a queue-size signal, not a completeness signal.

It does not mean:

- all translations succeeded
- all rows were persisted
- all fields are now available for every quest

### Gap 2: addon runtimes still diverge

Even after isolating addon-local caches, the quest-family addons still do not
consume the canonical quest payload the same way.

That is why regressions can still appear addon by addon.

### Gap 3: `JournalDetail` is not fully sheet-first

`JournalDetail` still uses UI capture as part of its content assembly.

That keeps it as the least stable quest surface.

### Gap 4: the schema stores translated maps, but not field-level completion

The schema tells us which translated dictionaries exist, but not:

- whether every `TODO` row is translated
- whether every `SEQ` row is translated
- whether every `SYSTEM` row is translated
- whether the quest is fully prefetched

That makes operational debugging harder.

### Gap 5: translated quest rows can be lost before serialization

Until the latest working-tree fix, the prefetch path had one lifecycle bug:

- translated canonical rows were applied to a fresh `QuestPlate` in memory
- merge/save then refreshed that same plate from serialized `...AsText` fields
- because those serialized fields were still empty at that moment, the
  in-memory translated rows were discarded before `UpdateFieldsAsText()` ran

This explains the earlier state where:

- `CanonicalRowsAsText` persisted correctly
- but `TranslatedSummaryRowsByKeyAsText`,
  `TranslatedObjectiveRowsByKeyAsText`, and
  `TranslatedSystemRowsByKeyAsText`
  remained empty even though prefetch translation had already resolved

### Gap 6: duplicate source text must never outrank `RowKey`

Quest data can legitimately contain repeated visible text, especially in
`TODO` rows.

That means canonical row reads and writes must follow this rule:

- if a `RowKey` is available, use it as the primary identity
- only fall back to `sourceText` when no row key exists

Using `rowKey OR sourceText` in one lookup is unsafe because an earlier row
with the same visible text can absorb the translation intended for a different
row.

## Practical Conclusion

The references in `AGENTS.md` are enough to define a complete quest more
precisely than we do today.

A complete quest should be treated as:

- stable quest identity
- live quest sequence
- canonical `SEQ` rows
- canonical `TODO` rows
- canonical `SYSTEM` rows
- structured payload-preserving source
- translated counterparts
- explicit completion state

That is the shape the next quest-data refactor should target.

## Related Documents

- [Quest Sheet Acquisition Pipeline](./quest-sheet-acquisition-pipeline.md)
- [Structured Text Payload Pipeline](./structured-text-payload-pipeline.md)
- [Quest Addon Translation Runtime Flow](./quest-addon-translation-runtime-flow.md)
