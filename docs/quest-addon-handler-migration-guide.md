# Quest Addon Handler Migration Guide

## Purpose

This guide documents the intended migration of quest-related addon handlers out of the legacy partial-class layout and into standalone handlers under `NativeUI/AddonHandlers/Quest/`.

It is based on the current design docs in `docs/` and on the repo rules in `AGENTS.md`:

- the UI is a capture surface, not the authoritative quest source
- Lumina and live quest state define stable quest identity and progress
- the shared translation queue, caches, and hover system must be reused
- dense quest windows should avoid frame-by-frame retranslations
- native mutation and overlay rendering must remain separate concerns

## Target Structure

Quest-related handlers should follow the same naming convention already used by the existing standalone handlers:

- `NativeUI/AddonHandlers/Quest/JournalHandler.cs`
- `NativeUI/AddonHandlers/Quest/JournalAcceptHandler.cs`
- `NativeUI/AddonHandlers/Quest/JournalResultHandler.cs`
- `NativeUI/AddonHandlers/Quest/ScenarioTreeHandler.cs`
- `NativeUI/AddonHandlers/Quest/ToDoListHandler.cs`
- `NativeUI/AddonHandlers/Quest/RecommendListHandler.cs`
- `NativeUI/AddonHandlers/Quest/AreaMapHandler.cs`
- `NativeUI/AddonHandlers/Quest/QuestAddonHandlerBase.cs` for shared quest-family helpers

The shared support layer should live under `NativeUI/AddonHandlers/Quest/` as well, but it should stay clearly separate from the addon-specific classes.

`Journal` may stay as one handler class that registers both `Journal` and `JournalDetail`, or it may be split into two thin quest-specific classes if that produces clearer wiring.

## Shared Behavior To Reuse

The quest handlers should continue to use the existing runtime infrastructure rather than inventing new pipelines:

- `Translators/TranslationService`
- the shared in-memory translation queue helpers
- `QuestUiTranslationCache`
- `QuestHoverTranslationCache`
- `QuestLuminaResolver`
- `QuestProgressResolver`
- `QuestTodoProgressResolver`
- `HoverTooltipManager`
- the existing DB `QuestPlate` model and persistence helpers

The migration should preserve the current quest behavior unless a doc explicitly says the behavior is changing.

## Quest-Specific Rules From The Docs

The docs establish these rules for quest handling:

- `Quest.Id` is the sheet identifier; `Quest.RowId` is the runtime quest identity
- the quest sheet path must be derived from `Quest.Id`
- live quest progress comes from native quest state, not from visible UI text alone
- structured quest text should preserve payload shape where possible
- `QuestPlate` should converge toward one canonical row per quest identity, language, engine, and game version
- repeated translation work in repaint-heavy windows should be short-circuited by stable cache keys

## Migration Phases

### Phase 1: Extract shared quest support

Create one shared quest support class that centralizes the logic currently repeated across the quest-family handlers:

- quest display-mode helpers
- translation queue access
- DB lookup and persistence delegation
- quest plate formatting
- hover tooltip registration helpers
- diacritics handling
- common quest-body and progress-body helpers

The goal is to make the addon-specific classes thin and readable.

### Step-by-step execution order

The migration should be executed in this order so each step stays reusable and easy to validate:

| Step | Target | Files | Reason |
|---|---|---|---|
| 1 | Shared quest support + smallest handler | `NativeUI/AddonHandlers/Quest/QuestAddonModeHelpers.cs`, `NativeUI/AddonHandlers/Quest/QuestAddonHandlerBase.cs`, `NativeUI/AddonHandlers/Quest/AreaMapHandler.cs`, `NativeUI/Helpers/QuestAddonWiring.cs`, `NativeUI/Helpers/AddonHandlerWiring.cs`, `Echoglossian.cs` | Proves the new quest handler pattern with the smallest possible surface |
| 2 | JournalAccept | `NativeUI/AddonHandlers/Quest/JournalAcceptHandler.cs`, wiring, teardown cleanup | Validates `PreSetup` handling and native `AtkValue` mutation in the new folder layout |
| 3 | JournalResult | `NativeUI/AddonHandlers/Quest/JournalResultHandler.cs`, wiring, teardown cleanup | Confirms the name-only quest path under the new structure |
| 4 | ScenarioTree | `NativeUI/AddonHandlers/Quest/ScenarioTreeHandler.cs`, wiring, teardown cleanup | Exercises live progress resolution and `AtkValue` refresh behavior |
| 5 | RecommendList | `NativeUI/AddonHandlers/Quest/RecommendListHandler.cs`, wiring, teardown cleanup | Validates the two-pass queue/apply pattern in the new layout |
| 6 | ToDoList | `NativeUI/AddonHandlers/Quest/ToDoListHandler.cs`, wiring, teardown cleanup | Moves the dense quest objective path once the shared helpers are proven |
| 7 | Journal | `NativeUI/AddonHandlers/Quest/JournalHandler.cs` or split quest detail handlers, wiring, teardown cleanup | Migrates the largest and most sensitive quest surface last |

Every step should keep the old handlers untouched only until the new handler is registered and verified, then remove the legacy registration for that addon.

Current status:

- Step 1 is complete.
- Step 2 (`JournalAccept`) is complete.
- Step 3 (`JournalResult`) is complete.
- Step 4 (`ScenarioTree`) is complete.
- Step 5 (`RecommendList`) is complete.
- Step 6 (`ToDoList`) is complete.
- Step 7 (`Journal`) is complete.
- All quest-addon steps in this guide are complete.

### Phase 2: Migrate the smaller quest handlers first

Move the easiest handlers first so the new structure is proven before the larger quest surfaces are touched:

- `AreaMap`
- `JournalAccept`
- `JournalResult`
- `ScenarioTree`

These handlers are good first migrations because they are easier to isolate and they validate the new constructor/injection pattern.

### Phase 3: Migrate the dense quest windows

After the base pattern is stable, move the heavier handlers:

- `Journal`
- `RecommendList`
- `ToDoList`

These windows need extra care because they repaint frequently and rely heavily on cache stability.

### Phase 4: Remove the legacy quest partials

Once the new handlers are wired and validated, remove the old quest-specific partial-class handlers from `NativeUI/Handlers/` and update teardown/unregistration so only the new standalone handlers remain in the runtime path.

## Wiring Changes

The runtime wiring should be updated so quest handlers are registered through the same `AddonHandlerRegistrar` path already used by the newer standalone handlers.

The old manual unregister blocks in `Echoglossian.cs` should be removed for the quest handlers once the new registrations are in place.

## Validation Checklist

After each migration step, verify:

1. the solution still builds cleanly
2. quest hover registration still works for the migrated addon
3. native-writing modes still mutate only when they should
4. overlay-only modes leave native nodes untouched
5. cached quest text is not retranslated every frame
6. the quest DB still reuses the canonical row instead of fragmenting by visible text

For the quest family, the important runtime checks are `Journal`, `JournalAccept`, `JournalResult`, `ScenarioTree`, `ToDoList`, `RecommendList`, and `AreaMap`.

## Notes

- Keep the migration narrow.
- Do not create a second quest translation queue.
- Do not introduce a new cache tier unless a specific handler truly needs it.
- Prefer one shared quest support layer over duplicated handler-local helpers.
- If a handler can stay on the existing capture surface without switching to a different data source, keep that behavior unless the docs require otherwise.