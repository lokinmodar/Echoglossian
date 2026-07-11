# Issue 174 Dialogue Retranslation Semantics Design

## Summary

This spec defines the next smallest shippable follow-up for GitHub issue
`#174` on top of the stable `v4.2601.0710.1250` release published on
`2026-07-11`.

The release already shipped explicit visible dialogue retranslation and
persistence for `Talk` and `BattleTalk`. The remaining operator gap is twofold:

- users still cannot easily tell whether story-facing text currently shown to
  the player came from an old DB row or from a fresh live translation request
- the explicit visible retranslate-and-persist action is still limited to
  `Talk` and `BattleTalk`

The approved next slice is therefore a diagnostics-first follow-up:

- expose visible provenance for player-facing story text in
  `/eglotranslatordebugger`
- extend explicit `Retranslate Visible ... And Persist` support to every
  supported story-facing surface in this slice
- add a non-destructive `View In DB Manager` handoff from the debugger
- cover:
  - `Talk`
  - `BattleTalk`
  - `TalkSubtitle`
  - `CutSceneSelectString`
  - `TextGimmickHint`
- do not change non-dialogue DB semantics
- do not add DB deletion or purge actions in this slice
- treat the existing `/eglodbmanager` window as the correct operator surface for
  any future targeted DB cleanup work

## Problem

Issue `#174` comments on `2026-05-05` still describe user confusion after
translator experimentation:

- clearing the DB fixes the symptom
- users do not know when old persisted rows are still winning
- users want clearer feedback than a checkbox or a display-only refresh

The stable build already improved this meaningfully by shipping
`Retranslate Visible Dialogue And Persist`, but the issue remains open because
the operator cannot yet answer a basic question for the currently visible
story-facing text:

`Where did this translation come from?`

## Existing Behavior

Current shipped behavior:

- `Talk`, `BattleTalk`, `TalkSubtitle`, `CutSceneSelectString`, and
  `TextGimmickHint` all have DB-backed lookup paths
- when no suitable row exists, they can issue a live translation request
- when runtime-only dialogue context is used, the result stays non-persistent
- `/eglotranslatordebugger` already exposes aggregate metrics, dialogue
  sessions, glossary status, provider state, and the explicit retranslate
  action
- `/eglodbmanager` already exposes the existing database editor window for
  direct table inspection and deletion

Current limitation:

- the debugger shows aggregate translator activity, not per-visible-line
  provenance
- the explicit retranslate action reports whether persistence succeeded, but
  only for `Talk` and `BattleTalk`
- none of these story-facing surfaces tell the operator what source the
  currently shown text originally came from

## Goals

1. Make DB reuse for visible player-facing story text explicit.
2. Reduce operator confusion during engine, model, and prompt experiments.
3. Extend explicit visible retranslate-and-persist to all supported
   story-facing surfaces in this slice.
4. Hand the operator off to the existing DB manager without adding destructive
   controls to the debugger.
5. Keep the change narrow and safe for the published branch line.

## Non-Goals

- no broad `clear database` button
- no bulk rewrite or purge workflow
- no change to quest, tooltip, toast, or canonical game-window persistence
- no change to runtime-only dialogue-context persistence rules
- no latency optimization work for `#176`
- no metadata/glossary expansion work for `#148`

## Options Considered

### Option A: Visible story-surface provenance in the debugger

Add a small current-line diagnostics section that reports:

- addon family or surface kind
- source: `DB`, `Live translation`, or `Runtime-only context`
- effective engine id or label
- whether the most recent explicit retranslate persisted successfully
- a `View In DB Manager` action that opens the existing DB manager on the
  relevant table

Pros:

- smallest code change that directly addresses the remaining user confusion
- no DB semantics change
- reuses the existing DB-management surface instead of creating another one
- extends an already shipped operator workflow instead of inventing a second
  remediation concept
- easy to validate in-game

Cons:

- does not itself remove stale rows
- first cut may only preselect the table, not deep-link to an exact row
- non-dialogue story surfaces do not all support runtime-only dialogue context
- requires a broader runtime contract than the current Talk-only retranslation
  interface

### Option B: Targeted purge for the current visible dialogue row

Pros:

- stronger remediation tool for bad stored rows

Cons:

- riskier semantics around engine filtering and row selection
- easier to misuse
- broader than needed before provenance is visible
- should reuse `/eglodbmanager` or shared DB-manager infrastructure rather than
  add destructive controls to `/eglotranslatordebugger`

### Option C: Broader DB cleanup workflow

Pros:

- addresses the user request for clearing cached data

Cons:

- too broad for the next slice
- high risk of collateral damage to DB-first behavior

## Chosen Approach

Choose Option A first.

The right next slice is not more mutation. It is visibility. Once the debugger
can show whether visible story text was reused from DB or freshly translated,
the operator can reason about experiments without wiping storage blindly.

If the issue remains open after this slice, a later follow-up can add a narrow
dialogue-row purge action with better scope and clearer operator expectations,
but that should build on the existing `/eglodbmanager` surface instead of
turning `/eglotranslatordebugger` into a second DB-management window.

## Proposed Design

### 1. Add a visible story-surface diagnostics snapshot

Introduce a small runtime-only snapshot model for the most recently resolved
visible story-facing surface. It should capture only what the debugger needs:

- addon family or surface kind
- original speaker text when available
- original line text
- original options text when applicable
- translated speaker text when available
- translated line text
- translated options text when applicable
- provenance kind
- effective translation engine id
- whether runtime-only dialogue context was used
- timestamp of the observation
- latest explicit retranslate outcome

This state stays in memory only.

The snapshot should also expose enough coarse identity for UI handoff:

- addon family or surface kind
- effective table name

### 2. Record provenance in `TalkHandler`

When `TalkHandler` resolves a line:

- record `DB` when a stored row is accepted
- record `Runtime-only context` when a live translation used dialogue context
  and was therefore not persisted
- record `Live translation` when a live translation completed without
  runtime-only context

When the explicit visible retranslate path runs:

- update the latest explicit retranslate outcome after persistence succeeds or
  fails

### 3. Record provenance in `BattleTalkHandler`

Apply the same rules and same snapshot contract used by `TalkHandler`.

Keep the namespace separate so the debugger can identify which addon family the
current snapshot belongs to.

### 4. Record provenance in `TalkSubtitleHandler`

When `TalkSubtitleHandler` resolves a line:

- record `DB` when a stored subtitle row is accepted
- record `Live translation` when a fresh subtitle translation is produced and
  stored

Add explicit visible retranslate-and-persist support:

- capture the currently visible subtitle source text
- request a fresh translation through the same existing subtitle translation
  path
- persist the refreshed `TalkSubtitleMessage`
- update the current in-memory resolved state and latest retranslate outcome

Use `TalkSubtitleMessage` as the DB-manager handoff target.

### 5. Record provenance in `CutSceneSelectStringHandler`

When `CutSceneSelectStringHandler` resolves one question-and-options payload:

- record `DB` when a stored `SelectString` row is accepted
- record `Live translation` when the handler translates the question or options
  live and stores the result
- model provenance at the whole prompt-plus-options payload level, not at one
  option row at a time

Add explicit visible retranslate-and-persist support:

- capture the currently visible question and option list
- request a fresh translation through the same existing batch-or-fallback
  CutSceneSelectString translation path
- persist the refreshed `SelectString` payload
- update the current in-memory resolved state and latest retranslate outcome

Use `SelectString` as the DB-manager handoff target.

### 6. Record provenance in `TextGimmickHintHandler`

When `TextGimmickHintHandler` resolves a hint line:

- record `DB` when a stored hint row is accepted
- record `Live translation` when a fresh hint translation is produced and
  stored
- do not claim runtime-only dialogue context for this surface unless the actual
  runtime path proves it

Add explicit visible retranslate-and-persist support:

- capture the currently visible hint source text
- request a fresh translation through the same existing gimmick-hint
  translation path
- persist the refreshed `TextGimmickHintMessage`
- update the current in-memory resolved state and latest retranslate outcome

Use `TextGimmickHintMessage` as the DB-manager handoff target.

### 7. Show the snapshot in `/eglotranslatordebugger`

Add a compact section near the existing retranslate controls that shows the
current visible story-surface state when available.

Rename or broaden the current retranslate button label so it no longer implies
only `Talk` and `BattleTalk`.

Suggested fields:

- current visible source
- current visible surface
- effective engine
- context-aware runtime-only flag
- last explicit retranslate result
- observation timestamp
- `Retranslate Visible Text And Persist`
- `View In DB Manager`

The debugger should clearly distinguish:

- `DB reuse`
- `Fresh live translation`
- `Fresh live translation (runtime-only dialogue context)`

The `View In DB Manager` action should:

- open the existing DB manager window
- preselect `TalkMessage` when the current snapshot is `Talk`
- preselect `BattleTalkMessage` when the current snapshot is `BattleTalk`
- preselect `TalkSubtitleMessage` when the current snapshot is `TalkSubtitle`
- preselect `SelectString` when the current snapshot is `CutSceneSelectString`
- preselect `TextGimmickHintMessage` when the current snapshot is
  `TextGimmickHint`

First-pass limit:

- no exact-row filter
- no auto-delete
- no mutation from the debugger itself

### 8. Broaden the runtime retranslate contract

The current runtime contract is centered on
`IVisibleDialogueRetranslationHandler` and
`VisibleDialogueRetranslationResult`, which currently read as `Talk` /
`BattleTalk` specific.

For this slice, the runtime should support the same explicit operation across
all covered story-facing surfaces.

Smallest acceptable implementation direction:

- broaden the existing interface contract to include the added handlers, even
  if the type names remain unchanged for the first pass
- keep the same result shape:
  - applicability
  - success
  - surface name
  - user-facing message
- update the plugin-runtime dispatcher so the debugger action can probe the
  expanded handler set, not only `Talk` and `BattleTalk`

### 9. Keep failure behavior simple

If no visible story-surface snapshot is available, the debugger should say so
plainly instead of inferring anything from aggregate metrics.

Do not backfill provenance from DB queries or logs after the fact.
Only show what the handlers actually observed while resolving the visible line.

## Data Flow

1. One supported story-facing handler resolves the currently visible text.
2. The handler determines whether the result came from:
   - DB lookup
   - live translation without runtime-only context
   - live translation with runtime-only context
3. The handler writes one in-memory visible story-surface diagnostics snapshot.
4. `/eglotranslatordebugger` reads and renders the latest snapshot.
5. When requested, the debugger invokes the explicit visible retranslate path
   for the currently applicable supported surface and records the outcome.
6. When requested, the debugger opens `/eglodbmanager` against the relevant
   table for the current snapshot.
7. The explicit retranslate action updates the last retranslate result in the
   same in-memory state for every supported surface.

## Files Expected To Change

Smallest expected touch set:

- `NativeUI/AddonHandlers/Talk/TalkHandler.cs`
- `NativeUI/AddonHandlers/Talk/BattleTalkHandler.cs`
- `NativeUI/AddonHandlers/Talk/TalkSubtitleHandler.cs`
- `NativeUI/AddonHandlers/CutSceneSelectString/CutSceneSelectStringHandler.cs`
- `NativeUI/AddonHandlers/Toasts/TextGimmickHintHandler.cs`
- `NativeUI/AddonHandlers/Talk/IVisibleDialogueRetranslationHandler.cs`
- `PluginUI/TranslatorMetricsWindow.cs`
- `PluginUI/PluginRuntimeUi.cs`
- `DBManagerUI/DBEditorWindow.cs`

Possible new helper file if needed:

- one small runtime-only visible dialogue diagnostics store under an existing
  runtime or translator-adjacent namespace

Possible small plugin-runtime touch:

- a narrow handoff method or delegate that opens the DB manager and selects the
  requested dialogue table

Avoid touching `DbOperations.cs` unless the implementation proves it is needed
for clearer engine labels only.

## Risks

### Risk 1: Misreporting provenance

If provenance is inferred too loosely, the debugger becomes misleading.

Mitigation:

- record provenance only at the exact decision point in each handler
- do not reconstruct it later from indirect clues

### Risk 2: Scope creep into DB-management UI

The existing `/eglodbmanager` window makes it tempting to add deletion controls
to the debugger for convenience.

Mitigation:

- keep this slice debugger-only
- do not add purge buttons or broader DB actions
- if later work adds targeted purge, route it through `/eglodbmanager` or its
  shared DB-manager components

### Risk 3: Unintended behavior changes in story-surface handlers

These handlers are behavior-sensitive.

Mitigation:

- add observation-only state
- do not alter lookup order, translation routing, or persistence rules

## Validation

Required:

- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`

Recommended in-game:

- open `/eglotranslatordebugger`
- trigger one `Talk` line already known to exist in DB and verify it shows
  `DB reuse`
- trigger one fresh `Talk` line and verify it shows `Fresh live translation`
- trigger one context-aware dialogue line that remains runtime-only and verify
  it shows the runtime-only provenance
- use `Retranslate Visible Dialogue And Persist` and verify the retranslate
  outcome updates clearly
- use `View In DB Manager` from a visible `Talk` snapshot and verify
  `/eglodbmanager` opens on `TalkMessage`
- use `View In DB Manager` from a visible `BattleTalk` snapshot and verify
  `/eglodbmanager` opens on `BattleTalkMessage`
- trigger one `TalkSubtitle` line already known to exist in DB and verify it
  shows `DB reuse`
- trigger one fresh `TalkSubtitle` line and verify it shows `Fresh live
  translation`
- use the debugger retranslate action on a visible `TalkSubtitle` line and
  verify the refreshed row is persisted and the visible subtitle updates
- use `View In DB Manager` from a visible `TalkSubtitle` snapshot and verify
  `/eglodbmanager` opens on `TalkSubtitleMessage`
- trigger one `CutSceneSelectString` prompt already known to exist in DB and
  verify it shows `DB reuse`
- trigger one fresh `CutSceneSelectString` prompt and verify it shows `Fresh
  live translation`
- use the debugger retranslate action on a visible `CutSceneSelectString`
  prompt and verify the refreshed question-and-options payload is persisted and
  the visible prompt updates
- use `View In DB Manager` from a visible `CutSceneSelectString` snapshot and
  verify `/eglodbmanager` opens on `SelectString`
- trigger one `TextGimmickHint` line already known to exist in DB and verify
  it shows `DB reuse`
- trigger one fresh `TextGimmickHint` line and verify it shows `Fresh live
  translation`
- use the debugger retranslate action on a visible `TextGimmickHint` line and
  verify the refreshed row is persisted and the visible hint updates
- use `View In DB Manager` from a visible `TextGimmickHint` snapshot and
  verify `/eglodbmanager` opens on `TextGimmickHintMessage`

## Success Criteria

This slice is successful when:

- operators can tell how the current visible supported story-facing surface was
  resolved
- users no longer need to guess whether a stale DB row or a live request is
  responsible for the currently visible output
- the explicit visible retranslate-and-persist workflow works consistently
  across every covered story-facing surface without changing DB-first rules for
  the broader plugin
