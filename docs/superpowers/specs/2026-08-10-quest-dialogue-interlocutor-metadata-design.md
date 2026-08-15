# Issue 214 Quest Dialogue Interlocutor Metadata Design

**Date:** 2026-08-10

**Repository:** `lokinmodar/Echoglossian`

## Objective

Complete issue `#214` by adding a quest-scoped dialogue metadata subsystem that
can derive, persist, and reuse speaker and addressee hints for quest-related
`Talk` and `BattleTalk` lines without blocking Dalamud callbacks or creating a
second translation pipeline.

## Issue Context

Issue `#214` is not only about attaching the visible speaker name to the first
request. Its user-facing symptom is incorrect pronoun and form-of-address
selection on the first line of dialogue, especially in languages where
speaker identity and relationship strongly affect output.

The current branch work already improved two narrower parts of that problem:

- first-line dialogue requests can now carry current-speaker context; and
- `Talk` and `BattleTalk` work was moved away from callback-blocking execution.

That still does not fully resolve `#214` when the first-line failure is caused
by missing interlocutor or addressee context rather than only a missing current
speaker.

This design therefore treats quest-derived interlocutor metadata as part of the
remaining `#214` scope, not as a separate unrelated follow-up.

## Problem

The current LLM dialogue path can carry current-speaker identity and bounded
prior turns, but it still lacks a reliable way to infer the addressee or
interlocutor for many quest-driven dialogue lines.

This shows up most clearly in gendered target-language output where:

- the speaker is known;
- the visible text is quest-related;
- the actor being addressed is not the local player; and
- the runtime does not have broad, easy-to-query game metadata that directly
  exposes the interlocutor.

The result is that the model may guess grammatical gender or addressee role
incorrectly even when the quest text itself contains enough surrounding
structure to derive a better fallback hint.

## Goals

- Persist quest-derived dialogue metadata in a dedicated entity keyed to the
  quest.
- Precompute that metadata asynchronously when a quest is accepted.
- Reuse the metadata later, even outside the currently active quest session.
- Query metadata cheaply from `Talk` and `BattleTalk` without blocking runtime
  callbacks.
- Prefer stronger live actor or NPC evidence when it exists.
- Version derived metadata by source language, game version, and derivation
  logic version.

## Non-Goals

- No second translation service or parallel LLM pipeline.
- No replacement of canonical translation tables or DB-first translation
  semantics.
- No requirement that every `Talk` or `BattleTalk` line resolve an
  interlocutor.
- No forced dependency on quest metadata when stronger live actor metadata
  exists.
- No broad scan of all game dialogue at runtime.
- No synchronous quest-sheet parsing, DB I/O, or model lookup from framework or
  ImGui callbacks.

## Existing Foundations

The repository already has the main building blocks needed for this design:

- `QuestLuminaResolver` resolves `Quest.RowId` and `Quest.Id`.
- `QuestProgressResolver` resolves live `QuestSequence` and mounts the
  quest text sheet through Lumina.
- structured dialogue requests already accept optional metadata fields.
- `Talk` and `BattleTalk` already use a runtime-only dialogue context path.

This design extends those foundations with a new metadata subsystem rather than
replacing the current translation architecture. It assumes the already-landed
first-line speaker-context work remains useful but insufficient by itself for
fully resolving `#214`.

## Metadata Sources

The first implementation should use explicit concrete metadata sources instead
of vague "gender detection" or generic prompt heuristics.

### Local Player

When the dialogue target is the local player, the primary source should be
`IPlayerState.Sex`.

Reasons:

- it is already exposed as managed Dalamud state;
- it avoids relying on raw `Customize` buffers as the main path;
- it is easier to test through DalaMock because `MockPlayerState.Sex` already
  exists.

`Customize[(int)CustomizeIndex.Gender]` may remain a reference or fallback for
native understanding, but it should not be the preferred first-cut source when
`IPlayerState.Sex` is available.

### Live NPC Or Actor

When the current dialogue line can be linked strongly to a loaded actor or NPC,
the runtime may capture a `LiveDialogueActorSnapshot` using a path similar to
the metadata extraction seen in `TextToTalk`.

Recommended captured managed fields:

- `NpcId` via `INpc.DataId` when available;
- `Gender` via the actor's native `Character` sex field;
- `Race` via `DrawData.CustomizeData.Race`;
- `BodyType` via `DrawData.CustomizeData.BodyType`.

These fields are useful because they allow the runtime to preserve stronger live
speaker metadata when the world actor is actually present and resolvable.

### Capture Safety Rule

All live actor and player metadata must be copied into managed immutable values
before the first asynchronous boundary.

That means:

- no `Span<byte>` or borrowed `Customize` buffer crosses an `await`;
- no native `Character*`, `Human*`, or other pointer survives into background
  work;
- lookup and translation operate only on the copied managed snapshot.

## Relationship To PR 262

The current `issue-214-first-dialogue-speaker-context` branch and draft PR
`#262` already cover:

- first-line current-speaker context on the initial request; and
- non-blocking async execution for `Talk` and `BattleTalk`.

They do not yet guarantee that the first line has enough addressee or
interlocutor metadata to materially fix the original pronoun-selection symptom
reported in `#214`.

This design is therefore intended as remaining branch work for `#214`, not as a
separate branch or issue by default.

## Recommended Architecture

Use a hybrid metadata strategy with fixed precedence tiers:

1. `Tier 1`: strong live actor or NPC match.
2. `Tier 2`: persisted quest dialogue metadata with exact quest and text match.
3. `Tier 3`: no reliable hint, so the runtime falls back to the current
   dialogue flow.

This avoids two common failure modes:

- blindly trusting derived quest metadata when a live actor is already known;
- relying only on live actor state when the actor is absent, unloaded, or not
  easily linked to the visible line.

The new subsystem remains auxiliary. `TranslationService` stays the only
translation orchestrator. The metadata subsystem only enriches dialogue request
inputs.

## Data Model

Add one dedicated persisted entity:

```csharp
public sealed class QuestDialogueMetadata
{
    public long Id { get; set; }

    public uint QuestId { get; set; }
    public ushort QuestSequence { get; set; }

    public string SourceLanguageCode { get; set; } = string.Empty;
    public string GameVersion { get; set; } = string.Empty;

    public string QuestSheetId { get; set; } = string.Empty;
    public string QuestTextSheetName { get; set; } = string.Empty;

    public string SourceRowKey { get; set; } = string.Empty;
    public string SourceTextHash { get; set; } = string.Empty;
    public string SourceTextPreview { get; set; } = string.Empty;

    public string SpeakerHint { get; set; } = string.Empty;
    public string AddresseeHint { get; set; } = string.Empty;

    public string SpeakerRoleHint { get; set; } = string.Empty;
    public string AddresseeRoleHint { get; set; } = string.Empty;

    public string Provenance { get; set; } = string.Empty;
    public int ConfidenceTier { get; set; }

    public string DerivationVersion { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
```

The logical identity for reuse is:

- `QuestId`
- `QuestSequence`
- `SourceLanguageCode`
- `GameVersion`
- `SourceRowKey`
- `SourceTextHash`

`SourceTextPreview` is diagnostic-only and must not be used as the primary
lookup key.

## Background Generation

### Trigger

When a quest is accepted, the runtime captures only managed immutable values and
starts a background derivation job.

The capture must be limited to:

- `QuestId`
- source language
- game version
- derivation version
- timestamp or generation id as needed for task ownership

No quest-sheet parsing or database work may run inline with the acceptance
callback.

### Derivation

The background worker:

1. resolves the `Quest` row from Lumina;
2. reads `Quest.Id`;
3. derives the quest text sheet path;
4. mounts the raw quest text sheet;
5. traverses quest text grouped by `QuestSequence`;
6. builds candidate dialogue metadata entries;
7. upserts the resulting `QuestDialogueMetadata` rows.

The first cut should derive metadata for the full quest text, not only the
currently visible sequence, so the persisted entity remains useful outside the
active quest session later.

### Failure Behavior

Derivation failure must not block quest acceptance, quest UI behavior, or later
translation. It only means the fallback metadata is unavailable until a later
successful derivation.

## Runtime Lookup

### Capture Boundary

`Talk` and `BattleTalk` continue to capture only managed immutable values on the
framework thread. In addition to the current text and speaker, the runtime may
capture an optional live actor snapshot when a strong actor or NPC match exists.

That snapshot must be copied into managed values before any `await`.

### Query Order

When translating a dialogue line:

1. try `Tier 1` live actor or NPC metadata;
2. if `Tier 1` is absent or weak, try `Tier 2` persisted quest dialogue
   metadata;
3. if neither source is reliable, do not invent an interlocutor hint.

The persisted lookup uses:

- `QuestId`
- `QuestSequence`
- `SourceLanguageCode`
- `GameVersion`
- `SourceRowKey` and/or `SourceTextHash`

The first cut should only consult the quest metadata path when the quest context
for the current dialogue line can be resolved with confidence. It should not do
global text-only fallback across all quests.

## Structured Dialogue Contract Changes

The current request contract already has optional metadata fields, but the
interlocutor problem needs more explicit optional hints.

Recommended additions:

- `SpeakerGenderHint`
- `AddresseeHint`
- `AddresseeRoleHint`
- `AddresseeGenderHint`

These remain optional. Engines that do not care can ignore them. Structured
engines receive them in the shared request contract. Plain-text providers may
receive an equivalent compact metadata block.

## Confidence and Provenance

Confidence must be discrete and auditable, not a free-form score.

Recommended tiers:

- `0`: unknown or unusable
- `1`: weak derived quest hint
- `2`: exact derived quest match
- `3`: strong live actor or NPC match

`Provenance` should capture where the metadata came from, for example:

- `LiveActor`
- `QuestSheetDerived`
- `QuestSheetDerivedExact`
- `QuestSheetPlusLiveFusion`

The first implementation can stay narrower and use only the values it actually
produces.

## Persistence and Invalidation

This entity is auxiliary, so invalidation rules must be strict and cheap:

- source language mismatch invalidates reuse;
- game version mismatch invalidates reuse;
- derivation version mismatch invalidates reuse;
- missing exact source key or hash match invalidates reuse.

The runtime must never rely on stale rows across those boundaries.

Upsert behavior should replace older rows for the same logical key instead of
creating duplicates.

## Validation Strategy

Automated validation should cover:

- derivation from a mounted quest text sheet into deterministic entity rows;
- versioning by source language, game version, and derivation version;
- exact lookup by `QuestId + QuestSequence + source row-key/hash`;
- tiered precedence where strong live actor metadata beats persisted quest
  metadata;
- clean fallback when metadata is absent or ambiguous;
- no blocking DB or sheet parsing inside framework and ImGui callback paths.

In-game validation should cover:

- accepting a quest triggers asynchronous metadata generation;
- a later `Talk` or `BattleTalk` line can consume persisted quest metadata;
- a line with a strong live NPC match still prefers live metadata;
- unrelated dialogue does not receive quest hints accidentally.

## Risks

- Quest text may not expose a unique addressee for every line.
- The same quest sequence can contain multiple dialogue targets, so
  `QuestId + QuestSequence` alone is not enough.
- Over-aggressive fallback could worsen output by forcing a wrong interlocutor.
- A background derivation job could become noisy or expensive if it recomputes
  the same quest too often.

## Mitigations

- require `SourceRowKey` or `SourceTextHash` in addition to quest identity;
- store confidence and provenance explicitly;
- use the fixed tier precedence instead of unconditional overrides;
- make derivation versioned and upsert-based;
- keep the subsystem auxiliary so absence of metadata does not break
  translation.

## First-Cut Scope

The first implementation should stay narrow:

- generate quest dialogue metadata asynchronously when a quest is accepted;
- persist it in a dedicated table;
- query it only for `Talk` and `BattleTalk`;
- prefer strong live actor metadata over persisted quest metadata;
- enrich the structured request with optional addressee-oriented hints;
- fall back cleanly when no confident match exists.

This is enough to validate the architecture without expanding into a general
dialogue knowledge system.
