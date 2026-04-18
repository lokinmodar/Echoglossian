# StringArrayData DB-First Remediation Plan

## Purpose

This document defines the replacement architecture for `StringArrayData`
processing in Echoglossian.

It is intentionally scoped to:

- `StringArrayData` surfaces that are still handled through the legacy global
  array path
- addon families that are **not** already covered by the newer quest DB-first
  handlers
- addon families that are **not** `ItemTooltips` or `ActionTooltips`, which are
  scheduled to move to a sheet-driven approach instead of the generic
  `StringArrayData` flow

Out of scope for this plan:

- `Journal`, `JournalDetail`, `_ToDoList`, and `ScenarioTree`
- `ItemTooltips`
- `ActionTooltips`

---

## Current Problem

The current `StringArrayData` implementation is doing too many things in one
place:

1. global extraction
2. DB lookup
3. translation
4. write-back into live game arrays
5. addon refresh side effects

That design is causing exactly the class of issues we want to avoid:

- flickering
- fights against the game resetting values
- unstable timing
- duplicated work
- broad writes without addon-local readiness checks
- DB records that are too blob-oriented and not semantic enough

The current code paths are centered around:

- the removed legacy global `StringArrayDataHandler` /
  `GenericAddonHandler` design
- [StringArrayDatas.cs](/C:/Dante/_dalamud/Echoglossian/EFCoreSqlite/Models/StringArrayDatas.cs)
- [StringArrayDatas.partial.cs](/C:/Dante/_dalamud/Echoglossian/EFCoreSqlite/Models/StringArrayDatas.partial.cs)
- [StringArrayDataPersistenceHelper.cs](/C:/Dante/_dalamud/Echoglossian/DBHelpers/StringArrayDataPersistenceHelper.cs)
- [DbFirstGameWindowAddonHandler.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs)

The extraction/translation part is not the main problem.
The application timing and ownership model is.

## Current Validation Snapshot

After the recent DB-first migration wave, the repo now has a much clearer split
between healthy and unhealthy paths:

- `gamewindows` is repopulating cleanly for `_MainCommand` and
  `AddonContextMenuTitle`
- `stringarraydatas` is still vulnerable to promoting already-visible localized
  text to `Original*` for the `Character*` family

This matters because the contaminated `OriginalStrings` then become part of the
lookup hash and retranslation input, which causes:

- repeated translation attempts for mixed PT/EN payloads
- `GoogleTranslator` 404s for large `kNN|...` blobs
- UI flicker when the game resets the arrays and the plugin tries to react
  again

The current failure mode is therefore not "the DB-first model is wrong." The
failure mode is that canonical capture for some `StringArrayData` surfaces is
still being derived from a live UI state that may already be partially
translated or otherwise localized.

---

## Root Causes

### 1. Global translation ownership

`StringArrayDataHandler.LoadAndTranslateStringArrayDatas()` walks many arrays at
once, translates them, and writes them back globally.

That means:

- it does not know whether the owning addon is ready
- it does not know whether the game is about to rewrite the same array
- it does not know whether the visible surface needs the data right now

### 2. Apply path is too generic

`GenericAddonHandler.OnArrayDataUpdate(...)` applies translated array payloads
based mainly on `StringArrayType` and the subscribed array index.

That is not enough to guarantee correctness for all surfaces because:

- some surfaces have multiple semantic regions in one array
- some surfaces vary by runtime context
- some surfaces should not be touched until all visible slots are ready

### 3. DB contract is too blob-oriented

The current `stringarraydatas` table stores:

- type
- raw data
- formatted raw data
- original serialized strings
- translated serialized strings
- translated strings with payloads

This is useful for storage, but weak as an application contract because it does
not explicitly capture:

- semantic slot meaning
- schema version
- stable context key
- source-content hash for the semantic payload

### 4. Multiple writers compete with the game

Today the game and the plugin can both write to the same `StringArrayData`
surface with no strict ownership model.

That creates the classic bad loop:

- game writes
- plugin overwrites
- game refreshes
- plugin rewrites

Even if each individual write is "correct", the timing model is unstable.

### 5. Canonical recovery is still incomplete for `Character*`

The repo now contains recovery helpers intended to map a translated or mixed
live payload back to a known canonical original row before capture proceeds.

That mitigation already improved the `GameWindow` path, but the `Character*`
`StringArrayData` path still shows incomplete recovery in practice. The current
evidence is:

- `gamewindows` rows are clean after a DB reset
- `stringarraydatas` rows for `Character`, `CharacterClass`, and
  `CharacterRepute` still contain PT-BR text in `OriginalStrings`

So the next remediation step is to keep tightening original recovery for the
string-array-backed `Character*` windows until capture is blocked whenever the
runtime cannot confidently re-anchor to a canonical original payload.

---

## Replacement Principle

`StringArrayData` must follow the same high-level rule that has worked for the
quest-family addons:

- DB is the source of truth
- translation is done outside hot UI paths
- application is addon-local
- the addon is left untouched unless the required translated payload is ready

In other words:

### Capture

Capture can still happen globally or semi-globally.

### Translation

Translation should happen from canonical serialized input into the DB.

### Apply

Apply must be local to the consuming addon and occur only when the addon is in
the right lifecycle state and the required payload is complete.

---

## Target Architecture

## Layer 1: Schema

Each supported `StringArrayType` must have an explicit schema contract.

A schema must answer:

- which array indices matter
- what each slot means
- which slots are user-visible
- which slots are translatable
- how to derive a stable context key
- how to tell whether the payload is complete enough to apply

Examples:

- `Character`
- `Hud`
- `Hud2`
- later `RecommendList`

This is the most important missing piece.
Without a schema, we are still treating semantic UI state as a blob.

## Layer 2: Canonical DB payload

The DB row should be treated as the canonical payload for one semantic surface,
not just as a string dump.

The repo now has a dedicated persistence helper for this direction:

- [StringArrayDataPersistenceHelper.cs](/C:/Dante/_dalamud/Echoglossian/DBHelpers/StringArrayDataPersistenceHelper.cs)
- [StringArrayStructuredPayload.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/StringArrayStructuredPayload.cs)
- [StringArrayStructuredPayloadResolver.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/StringArrayStructuredPayloadResolver.cs)
- [IStringArrayStructuredSchema.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/IStringArrayStructuredSchema.cs)
- [StringArrayStructuredPayloadBuilder.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/StringArrayStructuredPayloadBuilder.cs)

The long-term `StringArrayDatas` contract should evolve toward something like:

- `Type`
- `ContextKey`
- `SchemaVersion`
- `SourceContentHash`
- `OriginalStructuredPayload`
- `TranslatedStructuredPayload`
- `OriginalLang`
- `TranslationLang`
- `TranslationEngine`
- `GameVersion`

The current columns can continue to exist during transition, but they should
stop being the sole contract for apply.

## Layer 3: Prefetch / background translation

Once a schema can serialize a canonical payload, translation should happen in a
background path, not in the addon apply path.

This path may be triggered by:

- login
- addon availability
- stable context change
- schema-specific refresh signals

But it must not write into the live addon as part of translation ownership.

## Layer 4: Addon-local apply runtime

Every consuming addon must own its own runtime state:

- local cache of original values
- local cache of last-applied translated values
- local readiness gate

## Investigation Tooling

To keep DB inspection repeatable, the repo now includes a reusable read-only
SQLite inspection flow:

- [inspect-eglo-db.ps1](/C:/Dante/_dalamud/Echoglossian/scripts/inspect-eglo-db.ps1)
- [DbInspector.csproj](/C:/Dante/_dalamud/Echoglossian/scripts/db-inspector/DbInspector.csproj)
- [Program.cs](/C:/Dante/_dalamud/Echoglossian/scripts/db-inspector/Program.cs)

Use it instead of rebuilding one-off ad hoc queries every time we need to
compare logs against persisted state.
- local restore logic
- local tooltip logic if relevant

This is the same isolation strategy that worked for the quest-family addons.

No global array runtime should decide presentation for every addon.

---

## Application Rules

These rules should govern every DB-first `StringArrayData` addon runtime.

### Rule 1: Never translate in the apply path

When the addon is visible:

- read DB
- check completeness
- apply or do nothing

Do not:

- queue translation
- call translator
- synthesize fallback translations

### Rule 2: Apply only when the visible payload is complete

If the addon needs five visible translated slots and only four are available:

- do not partially mutate the addon
- keep the addon untouched
- optionally notify the user once, with debounce

This avoids half-translated surfaces and visual instability.

### Rule 3: Write only from stable lifecycle points

Application should happen from the owning addon's lifecycle only, for example:

- `PreRequestedUpdate`
- `PostRequestedUpdate`
- `PreRefresh`
- `PreDraw` retry only as a lightweight readiness recheck

Do not rely on a global startup scan to decide when to write to a live addon.

### Rule 4: Restore only what was mutated

If a mode or addon path never wrote native translation:

- do not restore anything

If the path did mutate:

- restore from local original snapshot only

### Rule 5: Prefer `SetValueIfDifferent*` semantics

When writing into `StringArrayData`, the runtime should prefer the game-facing
functions that suppress unnecessary writes:

- `SetValueIfDifferent(...)`
- `SetValueIfDifferentUtf8(...)`

Escalate to update-forcing functions only when the surface truly requires it.

---

## Recommended Migration Strategy

## Phase 1: Freeze the legacy global write path

The old global `StringArrayDataHandler` should stop being the place that applies
live translations for supported DB-first surfaces.

It can temporarily remain useful for:

- diagnostics
- schema discovery
- one-time capture

But it should not remain the authoritative write path.

## Phase 2: Define one schema at a time

Do not refactor every `StringArrayType` at once.

Recommended order:

1. `Character*`
2. `MainCommand`
3. `Hud`
4. `Hud2`
5. `RecommendList`

`RecommendList` stays later because:

- it is more dynamic
- it intersects with other systems
- the user already wants separate thought around it

## Phase 3: Add canonical DB payload support

Before rewriting apply runtimes broadly, add a stronger serialized contract for
schema-driven rows in `stringarraydatas`.

This may require a migration.

That is acceptable and preferable to keeping an emaranhado of weak contracts.

## Phase 4: Replace addon apply logic one surface at a time

Each addon using a `StringArrayType` should move to:

- DB lookup only
- completeness gate
- local apply cache
- local restore cache
- local lifecycle timing

## Phase 5: Remove legacy translation ownership

Once a surface is fully migrated:

- it should no longer depend on `StringArrayDataHandler` for translation
- it should no longer queue translations locally
- it should no longer use the generic blob apply path

Current repo status:

- the plugin-level startup invocation of the legacy global
  `StringArrayDataHandler` has been disabled
- migrated `GameWindow` surfaces now rely solely on their addon-local DB-first
  runtime
- `stringarraydatas` now also contains additive canonical fields for the next
  schema-driven migration wave:
  - `ContextKey`
  - `SchemaVersion`
  - `SourceContentHash`
  - `OriginalStructuredPayload`
  - `TranslatedStructuredPayload`
- remaining non-migrated `StringArrayData` surfaces are intentionally left
  untouched until they receive a typed runtime of their own

---

## Practical Design Recommendation

The cleanest implementation path is:

1. Introduce a typed schema abstraction for `StringArrayData` surfaces.
2. Introduce a DB-first runtime helper for schema-driven apply.
3. Migrate one addon family at a time to that helper.
4. Leave the legacy global capture path only as a temporary discovery tool.

A possible abstraction shape:

- `IStringArraySchema`
- `StringArraySchemaPayload`
- `StringArrayDbFirstRuntime`
- `StringArrayApplySnapshot`

The repo now has the first concrete pieces of that shape:

- `IStringArrayStructuredSchema`
- `StringArrayStructuredPayloadBuilder`
- `StringArrayStructuredPayloadResolver`

Responsibilities:

- schema:
  - identify slots
  - build canonical payload
  - compute completeness
- DB payload:
  - store canonical original/translated slot map
  - store context key and source hash
- runtime:
  - read DB row
  - decide whether all visible slots are ready
  - apply or restore

---

## What Should Not Be Repeated

The following patterns should not survive into the new design:

- global translation of every visible string-array surface at startup
- live translation calls from addon array apply paths
- writing partial translated payloads to a visible addon
- using only `StringArrayType` as the semantic identity of a DB row
- broad repeated writes without addon-local readiness checks

---

## Immediate Next Step

The next concrete step should be:

1. choose the first non-quest `StringArrayData` surface to migrate
2. define its explicit slot schema
3. decide the canonical DB payload shape needed for that schema
4. only then replace the legacy apply path for that surface

For the current repo state, `Character*` plus `MainCommand` are the best first
candidates:

- already uses `StringArrayType.Character`
- highly visible
- likely to benefit from a clean DB-first apply contract
- `MainCommand` already fits naturally in `GameWindow`
- neither surface requires a `stringarraydatas` schema migration just to begin
  the DB-first runtime work
- less volatile than `RecommendList`

---

## Relationship to Other Docs

This plan builds on:

- [string-array-data-clientstructs-research-report.md](/C:/Dante/_dalamud/Echoglossian/docs/string-array-data-clientstructs-research-report.md)
- [structured-text-payload-pipeline.md](/C:/Dante/_dalamud/Echoglossian/docs/structured-text-payload-pipeline.md)
- [quest-addon-detailed-flow-and-remediation-plan.md](/C:/Dante/_dalamud/Echoglossian/docs/quest-addon-detailed-flow-and-remediation-plan.md)

The core lesson carried over from quest work is simple:

capture and translation can be centralized,
but presentation must be local, explicit, and gated by data readiness.
