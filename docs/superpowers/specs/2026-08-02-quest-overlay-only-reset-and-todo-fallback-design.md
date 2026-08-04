<!--
  Copyright (c) lokinmodar. All rights reserved.
  Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
-->

# Quest overlay-only enforcement, language-reset restore, and ToDoList fallback design

Date: 2026-08-02

## Summary

This spec defines a narrow runtime hardening pass for three linked regressions:

1. overlay-only languages such as Arabic are still mutating native quest-family
   UI in some surfaces
2. changing target language does not reliably restore visible addons to their
   original text before the next language begins applying
3. `_ToDoList` can remain stuck in retry/prefetch loops even when the relevant
   `QuestPlate` translation already exists in the database

The scope is intentionally narrow. It does not redesign the quest runtime,
translation broker, or persistence model.

## Problem

Current repo facts:

- `UIOverlays/TextPresentation/LanguagePresentationPolicy.cs` correctly marks
  Arabic and other complex-script targets as `OverlayOnlyLanguage`
- `NativeUI/Helpers/TranslationDisplayModeHelper.cs` correctly collapses
  overlay-only languages away from native-writing modes
- DB-first and several non-quest surfaces already consume that shared helper
- quest-family handlers still rely on
  `NativeUI/AddonHandlers/Quest/QuestAddonModeHelpers.cs`, which currently
  ignores `overlayOnlyLanguage`
- `GeneralHelpers/RuntimeConfigurationRefresh.cs` clears caches and rebuilds
  runtime services when the translation signature changes, but it does not
  guarantee a restore pass across currently visible addons before those caches
  are discarded
- `NativeUI/AddonHandlers/Quest/ToDoListHandler.cs` currently depends on
  `QuestTodoProgressResolver` for activation; when live progress cannot be
  resolved, it requests prefetch again instead of trying to reuse an existing
  persisted `QuestPlate`

That leaves three concrete behavior failures:

1. overlay-only languages can still write translated text into native
   `Journal*`, `ScenarioTree`, `ToDoList`, `RecommendList`, and related quest
   surfaces
2. switching target language can leave stale translated native text in place
   because the runtime clears state before every visible handler has had a
   chance to restore its owned mutations
3. `_ToDoList` can keep the original text visible and emit repeating
   `todo-progress-unavailable` or `quest-translation-missing` diagnostics even
   when canonical translated quest data is already persisted

## Goals

1. Enforce overlay-only presentation consistently across every quest-family
   addon surface.
2. Ensure a target-language change restores currently visible translated native
   UI back to original text before the next language begins applying.
3. Let `_ToDoList` reuse persisted quest translations when live progress
   resolution is temporarily unavailable.
4. Preserve the existing DB schema, broker architecture, and quest prefetch
   semantics.
5. Keep the patch small and reviewable.

## Non-Goals

- no refactor of the full quest runtime architecture
- no redesign of `QuestTodoProgressResolver`
- no change to `JournalAccept` or `JournalResult` tooltip behavior in this pass
- no persistence migration
- no new generic addon reset framework beyond what this fix strictly needs
- no changes to `ScenarioTree`, `RecommendList`, or other quest-family surfaces
  beyond the shared overlay-only policy enforcement

## Options considered

### Option A: Patch only the visibly broken handlers

Touch `JournalAccept`, `JournalResult`, and `_ToDoList` directly.

Pros:

- smallest immediate edit count
- low short-term review overhead

Cons:

- leaves the quest-family policy inconsistent
- likely misses another quest surface still using native writes for
  overlay-only languages
- does not address the restore ordering problem globally

### Option B: Fix the shared quest policy, add a targeted restore phase, and add one `_ToDoList` fallback

Pros:

- aligns quest-family behavior with the existing global language policy
- fixes target-language switching at the lifecycle seam where it actually breaks
- keeps `_ToDoList` logic local while reusing the existing `QuestPlate`
  persistence model
- narrow enough to land without a broad runtime rewrite

Cons:

- touches multiple files
- requires careful lifecycle tests so restoration does not become over-eager

### Option C: Unify all quest-family handlers under one new runtime layer

Pros:

- could remove duplicated policy and lifecycle seams long-term

Cons:

- far beyond the needed scope
- materially increases regression risk in a sensitive UI area

## Chosen approach

Choose Option B.

The fix should be split into three small changes that share the same runtime
intent but stay architecturally narrow.

### 1. Shared quest-family overlay-only enforcement

`QuestAddonModeHelpers` must stop making raw display-mode decisions in
isolation. It should either delegate to `TranslationDisplayModeHelper` or
mirror its exact effective-mode behavior while taking
`overlayOnlyLanguage` explicitly.

Every quest-family caller that currently uses:

- `WritesNativeTranslation`
- `UsesHoverTooltips`
- `ShowsOriginalTooltips`
- `ShouldRemoveDiacritics`
- `CanRenderHoverTooltip`

must pass `this.Config.OverlayOnlyLanguage`.

Expected runtime result:

- overlay-only languages never write translated text into quest-family native
  nodes
- swap-like modes collapse to tooltip/overlay-only behavior for those languages
- native-font languages preserve current behavior exactly

### 2. Restore visible native state before rebuild on translation-signature changes

When the translation runtime signature changes because target language or other
translation-critical configuration changed, the runtime must restore visible
addon-owned native mutations before clearing session caches and rebuilding
translation services.

The intended order is:

1. restore visible handlers to original state
2. clear overlay/hover/runtime caches
3. rebuild translator and queued broker
4. force handler registration refresh

This should reuse existing `OnPluginUnload()` restoration behavior where
possible, because those handlers already know how to revert their own native
state safely.

Expected runtime result:

- changing target language first returns visible native UI to source/original
  text
- the next language then applies from a clean state
- stale session caches are still discarded, but only after restoration

### 3. `_ToDoList` persisted-translation fallback

`_ToDoList` should keep `QuestTodoProgressResolver` as the first path, because
that is still the authoritative source for live objective state. But when live
progress is unavailable, the handler should not immediately behave as if no
translation exists.

The fallback should:

1. resolve the original visible quest title using existing original-text
   recovery
2. attempt persisted `QuestPlate` lookup using the same target/source scope
   rules already used elsewhere
3. if a translated quest title is already persisted, use it for the visible
   quest row instead of leaving the row original and looping blindly
4. continue using prefetch/retry only when neither live progress nor persisted
   translated data is available

This fallback is intentionally limited to the quest row in this pass. Objective
translation still depends on progress-aware matching and should not be guessed
without a reliable active-objective mapping.

Expected runtime result:

- a quest row no longer remains stuck in the source language merely because the
  live progress snapshot is temporarily unavailable
- repeating retry logs reduce when the DB already has usable translated quest
  data

## Files in scope

Primary code files:

- `NativeUI/AddonHandlers/Quest/QuestAddonModeHelpers.cs`
- quest-family consumers that call `QuestAddonModeHelpers`
- `GeneralHelpers/RuntimeConfigurationRefresh.cs`
- `NativeUI/AddonHandlers/Quest/ToDoListHandler.cs`

Primary test files:

- `Echoglossian.Tests/TranslationDisplayModeHelperTests.cs`
- `Echoglossian.Tests/QuestAddonHandlerLifecycleTests.cs`
- `Echoglossian.Tests/ToDoHandlerContractTests.cs`
- additional focused tests if a new lifecycle seam needs direct coverage

## Testing strategy

Test-first coverage should prove:

1. quest-family mode helpers collapse overlay-only languages away from native
   writes
2. translation-signature refresh restores addon-owned native state before cache
   reset/rebuild sequencing proceeds
3. `_ToDoList` attempts persisted translated-quest fallback when live
   todo-progress resolution fails
4. native-font languages keep current swap/native semantics unchanged

Validation commands after implementation:

- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`

## In-game verification after implementation

Verify at minimum:

1. set target language to Arabic and confirm quest-family native UI remains
   original while plugin overlay/tooltip presentation still works
2. switch from one translated target language to another while affected addons
   are visible and confirm native text resets before the new language appears
3. open `_ToDoList` with a quest that already has a persisted translation and
   confirm the quest row no longer stalls in source text solely because
   live progress resolution is missing

## Risks

- restoring too aggressively during runtime refresh could clear UI state owned
  by a handler that did not mutate native text in the first place
- `_ToDoList` fallback must not guess objective translations without a stable
  live mapping
- quest-family policy changes must preserve current native behavior for
  non-overlay-only languages
