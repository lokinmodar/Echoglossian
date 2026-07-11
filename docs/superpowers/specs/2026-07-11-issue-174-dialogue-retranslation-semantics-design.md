# Issue 174 Dialogue Retranslation Semantics Design

## Summary

This spec defines the next smallest shippable follow-up for GitHub issue
`#174` on top of the stable `v4.2601.0710.1250` release published on
`2026-07-11`.

The release already shipped explicit visible dialogue retranslation and
persistence for `Talk` and `BattleTalk`. The remaining operator gap is that
users still cannot easily tell whether a visible line came from an old DB row
or from a fresh live translation request.

The approved next slice is therefore a diagnostics-first follow-up:

- expose visible dialogue provenance in `/eglotranslatordebugger`
- keep scope limited to `Talk` and `BattleTalk`
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
the operator cannot yet answer a basic question for the currently visible line:

`Where did this translation come from?`

## Existing Behavior

Current shipped behavior:

- `Talk` and `BattleTalk` first try DB lookup
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
- the retranslate action reports whether persistence succeeded, but not what
  source the currently shown line originally came from

## Goals

1. Make DB reuse for visible `Talk` and `BattleTalk` lines explicit.
2. Reduce operator confusion during engine, model, and prompt experiments.
3. Reuse the existing debugger and dialogue handler architecture.
4. Keep the change narrow and safe for the published branch line.

## Non-Goals

- no broad `clear database` button
- no bulk rewrite or purge workflow
- no change to quest, tooltip, toast, or canonical game-window persistence
- no change to runtime-only dialogue-context persistence rules
- no latency optimization work for `#176`
- no metadata/glossary expansion work for `#148`

## Options Considered

### Option A: Visible dialogue provenance in the debugger

Add a small current-line diagnostics section that reports:

- addon family: `Talk` or `BattleTalk`
- source: `DB`, `Live translation`, or `Runtime-only context`
- effective engine id or label
- whether the most recent explicit retranslate persisted successfully

Pros:

- smallest code change that directly addresses the remaining user confusion
- no DB semantics change
- easy to validate in-game

Cons:

- does not itself remove stale rows

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
can show whether a visible line was reused from DB or freshly translated, the
operator can reason about experiments without wiping storage blindly.

If the issue remains open after this slice, a later follow-up can add a narrow
dialogue-row purge action with better scope and clearer operator expectations,
but that should build on the existing `/eglodbmanager` surface instead of
turning `/eglotranslatordebugger` into a second DB-management window.

## Proposed Design

### 1. Add a visible dialogue diagnostics snapshot

Introduce a small runtime-only snapshot model for the most recently resolved
visible dialogue line. It should capture only what the debugger needs:

- addon family
- original speaker text when available
- original line text
- translated speaker text when available
- translated line text
- provenance kind
- effective translation engine id
- whether runtime-only dialogue context was used
- timestamp of the observation
- latest explicit retranslate outcome for that addon family

This state stays in memory only.

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

### 4. Show the snapshot in `/eglotranslatordebugger`

Add a compact section near the existing retranslate controls that shows the
current visible dialogue state when available.

Suggested fields:

- current visible dialogue source
- current visible dialogue addon family
- effective engine
- context-aware runtime-only flag
- last explicit retranslate result
- observation timestamp

The debugger should clearly distinguish:

- `DB reuse`
- `Fresh live translation`
- `Fresh live translation (runtime-only dialogue context)`

### 5. Keep failure behavior simple

If no visible dialogue snapshot is available, the debugger should say so
plainly instead of inferring anything from aggregate metrics.

Do not backfill provenance from DB queries or logs after the fact.
Only show what the handlers actually observed while resolving the visible line.

## Data Flow

1. `TalkHandler` or `BattleTalkHandler` resolves one visible line.
2. The handler determines whether the result came from:
   - DB lookup
   - live translation without runtime-only context
   - live translation with runtime-only context
3. The handler writes one in-memory visible dialogue diagnostics snapshot.
4. `/eglotranslatordebugger` reads and renders the latest snapshot.
5. The explicit retranslate action updates the last retranslate result in the
   same in-memory state.

## Files Expected To Change

Smallest expected touch set:

- `NativeUI/AddonHandlers/Talk/TalkHandler.cs`
- `NativeUI/AddonHandlers/Talk/BattleTalkHandler.cs`
- `PluginUI/TranslatorMetricsWindow.cs`

Possible new helper file if needed:

- one small runtime-only visible dialogue diagnostics store under an existing
  runtime or translator-adjacent namespace

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

### Risk 3: Unintended behavior changes in dialogue handlers

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
- repeat the same checks for `BattleTalk`

## Success Criteria

This slice is successful when:

- operators can tell how the current visible `Talk` or `BattleTalk` line was
  resolved
- users no longer need to guess whether a stale DB row or a live request is
  responsible for the currently visible output
- dialogue retranslation semantics are clearer without changing DB-first rules
  outside dialogue
