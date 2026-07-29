# Issues 206, 68, 172, and Quest Popup Follow-Ups Design

## Status

Proposed on 2026-07-29.

This spec defines one coordinated follow-up tranche for the current
`feature/issues-230-233-234` branch, but the work is intentionally split into
four narrow implementation fronts with short commits and immediate pushes after
each validated commit.

## Summary

This tranche covers four independent but related fronts:

- `#206`: fix the prompt-editor preview so `{targetLanguage}` reflects the
  current configured target language instead of the hard-coded `Japanese`
  preview default.
- Quest popup follow-up: finish `JournalAccept` and `JournalResult` without
  polluting canonical quest persistence when the addon does not expose a
  trustworthy quest identity.
- `#68`: deliver real runtime coverage and visible configuration for
  `SelectYesNo`, `SelectOk`, and `SelectString`, while keeping
  `CutSceneSelectString` as its own separate path.
- `#172`: add dedicated handling for the `ToDo` addon used in instanced/FATE
  style content without treating it as `_ToDoList`.

The implementation order is fixed:

1. `#206` and the `ActionDetail` / `ItemDetail` tooltip copy cleanup
2. `JournalAccept` and `JournalResult`
3. `#68`
4. `#172`

## Global Constraints

- Work only in `C:\Dante\_dalamud\Echoglossian\.worktrees\issues-230-233-234`.
- Do not use the root checkout as source of truth.
- Keep patches narrow.
- Keep commits short.
- Push after each validated commit.
- Do not infer addon structure, quest identity, or payload ownership from weak
  signals.
- If an addon field, lifecycle, or identity source is not clear enough from
  code or direct validation evidence, stop and ask the user before proceeding.
- Do not touch `Journal`, `JournalDetail`, `ScenarioTree`, `RecommendList`, or
  `_ToDoList` unless a front in this spec explicitly requires it.
- Do not write speculative `QuestPlate` rows for quest surfaces that cannot be
  reconciled safely later.

## Goals

- Make prompt preview output trustworthy for users editing translator prompts.
- Finish quest popup behavior without weakening canonical quest persistence.
- Close the remaining selection-dialog gap with both runtime coverage and
  visible configuration.
- Add a dedicated `ToDo` implementation path for the `#172` surface.
- Keep each front independently testable and independently shippable.

## Non-Goals

- Do not refactor the quest-family architecture broadly.
- Do not revisit `Journal` / `JournalDetail` matching logic in this tranche.
- Do not merge `ToDo` into `_ToDoList` behavior or configuration.
- Do not overload `QuestPlate` with rows that are not canonically identifiable
  as quests.
- Do not claim addon identity or field availability without direct evidence.

## Current Architecture

### Prompt preview

`PluginUI/Components/PromptEditorUI.cs` renders the live preview by calling
`PromptTemplateManager.ApplyPromptVariables(...)`.

`PluginUI/Helpers/PromptEditorStateManager.cs` currently stores:

- `PreviewSampleText = "My blade is for the Fury."`
- `PreviewSourceLang = "English"`
- `PreviewTargetLang = "Japanese"`

That hard-coded `PreviewTargetLang` is the concrete source of `#206`.

### Tooltip configuration copy

`Config.NormalizeStructuredTooltipPresentationSettings()` and the tooltip UI
currently expose a user-visible message saying native `ActionDetail` /
`ItemDetail` translation remains disabled until verified mappings are available
through `FFXIVClientStructs`.

That dependency detail is useful for internal engineering context, but it does
not belong in the end-user configuration copy for the current shipped behavior.

### Quest popup handlers

The branch already contains dedicated handlers:

- `NativeUI/AddonHandlers/Quest/JournalAcceptHandler.cs`
- `NativeUI/AddonHandlers/Quest/JournalResultHandler.cs`

Both are wired conditionally in
`NativeUI/Helpers/AddonHandlerWiring.cs` behind:

- `TranslateJournalAccept`
- `TranslateJournalResult`

Both currently lean on quest-family helpers and `QuestPlate` lookup paths.

### Selection dialogs

The branch already contains:

- `PluginUI/Tabs/SelectionDialogsTab.cs`
- full runtime for `CutSceneSelectString`

The UI tab exists but is currently not reachable from the main overlay tab,
because the `SelectionDialogsTab.Draw(config)` branch is commented out in
`PluginUI/Tabs/OverlayTab.cs`.

The code inspection performed for this spec found real runtime implementation
for `CutSceneSelectString`, but did not find equivalent addon handlers for:

- `SelectYesNo`
- `SelectOk`
- `SelectString`

For this tranche, those three surfaces must be treated as missing runtime
coverage, not merely hidden UI.

### `ToDo`

The branch contains mature `_ToDoList` support:

- `Config.TranslateToDoList`
- `NativeUI/AddonHandlers/Quest/ToDoListHandler.cs`

It does not yet contain an equivalent handler or toggle for a separate
`ToDo` addon. That means `#172` is not a `_ToDoList` tuning task; it is a new
surface-discovery and dedicated-handler task.

## Proposed Architecture

### Front A: `#206` and tooltip copy cleanup

#### Prompt preview design

The prompt editor state should keep only truly editor-local state:

- edited prompt text
- sample source text
- preview result
- validation warning state

The target language used for preview rendering should not be a hard-coded field
owned by the prompt editor state.

Instead, preview rendering should derive the target language from the current
plugin configuration each time the prompt editor draws.

The source language preview sample may remain fixed unless a stronger product
need appears, because `#206` is specifically about the wrong target-language
preview token.

#### Tooltip copy design

The visible configuration/help text for `ActionDetail` and `ItemDetail` should
describe only the current product behavior:

- Plugin Tooltip presentation is the only supported mode today.
- Native translation for those surfaces is currently unavailable.

Do not mention `FFXIVClientStructs` in the visible UI/help text for this case.
Internal comments and code documentation may still keep that engineering
context when helpful.

### Front B: `JournalAccept` and `JournalResult`

#### Shared rules

- Do not touch `Journal` or `JournalDetail`.
- Do not assume a quest identity from title/body text alone unless the design
  for the specific surface explicitly allows a fallback.
- Use short-lived runtime state, async queueing, and explicit reapply.
- If an addon-provided quest identity is unclear, stop and ask the user rather
  than guessing.

#### `JournalAccept`

`JournalAccept` should behave as a surface-specific runtime with:

- setup-time capture of title and body
- async translation queueing
- local runtime cache
- reapply on later lifecycle events once the translation is ready

Persistence policy:

- If the addon exposes a trustworthy `questId`, `JournalAccept` may use the
  standard quest translation path and canonical quest persistence.
- If the addon does not expose a trustworthy `questId`, `JournalAccept` must
  persist through a new dedicated popup-oriented table instead of `QuestPlate`.

`JournalAccept` must not write speculative rows into `QuestPlate` when the
payload cannot be reconciled to a canonical quest later.

#### `JournalResult`

`JournalResult` should prefer canonical quest reuse when possible:

1. lookup in `QuestPlate` by `questId` when the addon exposes a trustworthy
   quest identity
2. otherwise lookup by quest title
3. otherwise fall back to real-time translation when no usable row exists yet

Persistence policy:

- `QuestPlate` is allowed only when reconciliation to the quest is safe.
- If the addon does not expose enough information for safe canonical storage,
  use the same dedicated popup-oriented persistence rule as `JournalAccept`
  rather than polluting quest tables.

This preserves the user's preferred behavior:

- use canonical quest data when the addon identity is trustworthy
- otherwise isolate popup-specific rows so they can be managed later without
  contaminating quest persistence

### Front C: `#68`

#### Scope

Deliver both runtime and visible configuration for:

- `SelectYesNo`
- `SelectOk`
- `SelectString`

Keep `CutSceneSelectString` separate.

#### UI/configuration

`SelectionDialogsTab` must be reachable from the main config flow again.

The toggles already present in configuration remain the source of truth:

- `TranslateYesNoScreen`
- `TranslateSelectOk`
- `TranslateSelectString`

This front should not expose settings that do not have real runtime backing.
UI and runtime land together in the same front.

#### Runtime model

Treat each of the three surfaces as a distinct addon/runtime path.

Do not pretend they are already implemented just because the config toggles
exist. The implementation should explicitly choose one of these two outcomes
per surface:

- reuse a real existing shared helper with clear evidence
- or add a dedicated handler for that surface

Persistence policy:

- `SelectString` may reuse or extend the existing `SelectString` persistence
  model when the payload shape genuinely matches.
- `SelectOk` and `SelectYesNo` must not be forced into unrelated tables.
- If they need persistence and no suitable existing table matches cleanly,
  add explicit dedicated persistence.

Presentation policy:

- `CutSceneSelectString` keeps its current overlay/native behavior.
- This front does not require new generic overlay work for the three missing
  dialogs unless direct runtime evidence shows that the surface already needs
  it to function correctly.
- A narrow runtime-first implementation is preferred over speculative display
  abstractions.

### Front D: `#172` `ToDo`

#### Discovery gate

This front begins with addon identification, not implementation assumptions.

Before writing the `ToDo` handler, implementation must verify:

- the real addon name
- the lifecycle events that expose readable payload
- whether the surface is quest-canonical, transient text-only, or mixed
- whether the existing quest prefetch/canonical pipeline applies safely

If those facts cannot be established from code, mocks, probes, or other direct
evidence, stop and ask the user before proceeding further.

#### Runtime model

`ToDo` is a dedicated surface.

- Do not merge it into `_ToDoList`.
- Do not extend `_ToDoList` behavior and claim that `#172` is solved.
- Add a dedicated handler and dedicated config only if the addon is confirmed
  to be a separately controllable translation surface.

If the verified payload turns out to be quest-canonical, reuse shared quest
prefetch and canonical translation lookup where safe.

If the verified payload is transient and not canonically reconcilable, use a
surface-specific translation path rather than bending quest persistence around
it.

## Data and Persistence Rules

### Canonical quest persistence

`QuestPlate` remains the source of truth only for quest surfaces that can be
reconciled safely to a real quest identity.

Allowed examples in this tranche:

- `JournalResult` with trustworthy `questId`
- `JournalResult` title fallback only when the matching rule is already
  accepted for that surface and does not create unsafe ambiguity

Disallowed examples:

- `JournalAccept` rows written by text approximation alone
- popup text that may represent quest offers but cannot be reconciled later
- `ToDo` payloads whose canonical identity is unknown

### Dedicated popup persistence

If `JournalAccept` or `JournalResult` need non-canonical storage, use a new
dedicated persistence model with explicit ownership semantics rather than
reusing `QuestPlate`.

That dedicated model must make it obvious that the row belongs to popup/runtime
capture and is not canonical quest content.

### Selection-dialog persistence

Use persistence only where it matches the real payload model cleanly.

If a surface needs storage and there is no clean existing row type, add one.
Do not overload `SelectString` rows with `YesNo` or `SelectOk` payloads that
do not actually share the same semantic shape.

## Validation Strategy

Each front must validate independently.

### Required repository validation

- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`

### Mock/runtime validation

When a front touches addon lifecycle or hosted plugin wiring, extend or reuse
`Echoglossian.Mock.Tests` where practical.

If the current harness cannot model the target addon payload or lifecycle
reliably, document that gap and keep the required in-game validation explicit.

### In-game validation expectations

- Front A: prompt preview and tooltip copy visible in config UI
- Front B: `JournalAccept` and `JournalResult` capture, delayed reapply, and
  presentation modes
- Front C: `SelectYesNo`, `SelectOk`, and `SelectString` live capture and
  application
- Front D: `ToDo` behavior in instanced/FATE style content once the addon is
  positively identified

## Commit and Push Strategy

This tranche must not land as one large changeset.

Expected integration style:

1. one short validated commit for Front A
2. push
3. one or more short validated commits for Front B
4. push
5. one or more short validated commits for Front C
6. push
7. one or more short validated commits for Front D
8. push

The spec document itself is also a short standalone commit.

## Stop-and-Ask Gates

Stop and ask the user before proceeding if any of these remain unclear:

- whether `JournalAccept` exposes a trustworthy `questId`
- whether `JournalResult` exposes a trustworthy `questId`
- whether `JournalResult` title fallback is ambiguous for the concrete addon
  payload being handled
- the real addon name and payload shape for `ToDo`
- whether a missing selection-dialog surface can safely reuse an existing row
  model

These are not implementation details to infer from guesswork.

## Success Criteria

### Front A

- Prompt preview uses the actual configured target language.
- Tooltip UI copy no longer mentions `FFXIVClientStructs`.

### Front B

- `JournalAccept` works end-to-end without speculative `QuestPlate` pollution.
- `JournalResult` prefers canonical quest reuse when identity is safe.
- Both surfaces reapply asynchronously when the translation arrives later.

### Front C

- `SelectYesNo`, `SelectOk`, and `SelectString` have real runtime coverage.
- Their toggles are visible in the config UI.
- `CutSceneSelectString` remains intact.

### Front D

- `ToDo` is implemented as a distinct verified surface.
- `_ToDoList` behavior remains unchanged.
- The implementation path matches verified addon evidence instead of guesses.
