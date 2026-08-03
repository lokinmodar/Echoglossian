# ContextMenu and ToDo Dedicated Runtime Design

## Status

Proposed on 2026-07-30.

This spec defines two narrow surfaces for the current
`feature/issues-230-233-234` branch:

- `ContextMenu` as a new standalone native-window translation surface
- `ToDo` as a separate instanced/FATE-oriented surface with dedicated
  persistence

The design is intentionally conservative. `AddonContextMenuTitle`,
`MainCommand`, `SystemMenu`, `_ToDoList`, `Journal`, `JournalDetail`,
`ScenarioTree`, and `RecommendList` keep their current ownership and behavior.

## Summary

Echoglossian should add two new standalone translation surfaces without
polluting existing persistence models or broadening unstable runtime seams.

`ContextMenu` should:

- be independent from the `MainCommand` / `AddonContextMenuTitle` family
- have its own toggle and display mode
- support the same three display modes already used by stable DB-first native
  window surfaces
- store translations in its own table
- capture variable-length menu rows from the real visible text nodes

`ToDo` should:

- be treated as distinct from `_ToDoList`
- translate all visible text nodes except the live timer node
- persist to a table dedicated exclusively to the `ToDo` addon
- keep translation and application asynchronous so the live instance/FATE UI
  does not block or churn every frame

## Global Constraints

- Work only in `C:\Dante\_dalamud\Echoglossian\.worktrees\issues-230-233-234`.
- Do not use the root checkout as source of truth.
- Keep patches narrow.
- Keep commits short.
- Push after each validated implementation commit.
- Do not infer addon structure or identity from weak signals.
- Do not break the existing `MainCommand` / `AddonContextMenuTitle` shared
  path to make `ContextMenu` work.
- Do not merge `ToDo` into `_ToDoList` configuration, persistence, or handler
  ownership.
- Do not leave hot-path retry spam or frame-by-frame retranslations behind.

## Goals

- Add standalone `ContextMenu` coverage with native UI, tooltip, and swap
  support.
- Preserve the current `AddonContextMenuTitle` integration exactly as-is.
- Add dedicated persistence for `ContextMenu` rather than overloading
  `GameWindow` or `SelectionDialogText`.
- Add dedicated `ToDo` persistence that stays exclusive to the `ToDo` addon.
- Translate instance/FATE `ToDo` text content while never translating the live
  countdown timer node.
- Reuse stable shared runtime behavior where that reuse does not expand risk to
  existing surfaces.

## Non-Goals

- Do not refactor the `MainCommand` / `AddonContextMenuTitle` architecture.
- Do not rework `_ToDoList` beyond the already-landed timer-stability fix.
- Do not merge `ToDo` and `_ToDoList` into one persistence contract.
- Do not move `ContextMenu` into `SelectionDialogText`.
- Do not move `ContextMenu` into the generic `gamewindows` table.
- Do not introduce a second parallel async translation pipeline when shared
  runtime behavior is already stable enough.

## Current Architecture

### `ContextMenu`

The branch currently supports:

- `_MainCommand`
- `AddonContextMenuTitle`
- `SystemMenu`

through the DB-first game-window runtime family.

That family is configured today through the shared game-main-menu section:

- `Config.TranslateGameMainMenu`
- `Config.GameMainMenuWindowTranslationDisplayMode`

This ownership is correct for `AddonContextMenuTitle`, but it is not the right
model for `ContextMenu`.

The addon probe confirms that `ContextMenu` is a separate addon with repeated
menu rows under `NodeList[2]/ComponentNodes[1]` and visible label text at each
row's `ComponentNodes[3]/Next` text node. The number of rows is variable.
Observed rows include ordinary labels such as `Dismiss`, `Emote`, `Mark`, and
`Focus Target`, plus decorated entries such as `More Information`.

### `ToDo`

The branch already has `_ToDoList` support with its own quest-family handler
and recent timer-stability fixes. That surface remains separate.

The addon requested in this spec is `ToDo`, which appears in instanced/FATE
style content and exposes text that should be translated even when it is not
canonical quest data. The user explicitly wants:

- all visible text nodes translated except the timer node
- persistence in a table dedicated exclusively to the `ToDo` addon

That means `ToDo` is not a `_ToDoList` cleanup task and must not share the
same persistence identity.

## Proposed Architecture

### `ContextMenu`

#### Surface ownership

`ContextMenu` becomes a standalone native-window surface:

- `Config.TranslateContextMenu`
- `Config.ContextMenuTranslationDisplayMode`

In the plugin UI, `ContextMenu` should render as its own section rather than
being folded into the unified game-main-menu section.

The existing `TranslateGameMainMenu` and
`GameMainMenuWindowTranslationDisplayMode` logic remains unchanged and
continues to govern only:

- `_MainCommand`
- `AddonContextMenuTitle`
- `SystemMenu`

#### Runtime model

`ContextMenu` should use a dedicated `ContextMenuHandler`.

That handler should reuse the stable DB-first native-window behavior that
already solves:

- async translation queueing
- ownership-aware native restore
- hover tooltip registration
- display-mode resolution
- failure cooldowns

The reuse must come through a narrow, opt-in seam in
`NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs`.

The shared base must not change lookup or persistence behavior for existing
`GameWindow` consumers unless the derived handler explicitly opts into the new
seam. This is the core guardrail against breaking the current game-main-menu
family.

#### Persistence model

`ContextMenu` uses a new dedicated entity and table:

- entity: `ContextMenuText`
- table: `contextmenutexts`

The row should store:

- ordered original row texts
- ordered translated row texts
- `OriginalLang`
- `TranslationLang`
- `TranslationEngine`
- `GameVersion`
- `SourceContentHash`
- `CreatedDate`
- `UpdatedDate`
- `RowVersion`

Lookup should be keyed by:

- addon identity fixed to `ContextMenu`
- source language
- target language
- translation engine
- game version
- `SourceContentHash`

`SourceContentHash` should be computed from the normalized ordered payload so
the same visible menu content can be reused safely without treating the addon
name alone as the lookup identity.

#### Capture model

Capture should walk the repeated visible menu-row structure rooted at
`NodeList[2]/ComponentNodes[1]` and then follow sibling `Next` pointers until
the visible row chain ends.

For each visible row:

- use the line text at `ComponentNodes[3]/Next`
- ignore empty rows
- preserve visible order
- never assume a fixed row count

The handler should tolerate menus with five items, twenty items, or any other
visible count that follows the same row pattern.

#### Text normalization and decorated rows

Some menu entries may contain control bytes or icon/decorative payloads in the
raw visible text. Persistence and lookup should normalize those labels so a
decorated row can still reuse a prior translation.

Native application remains narrow:

- mutate only the row's text node
- do not rewrite component structure
- do not touch collision nodes, nine-grid nodes, or icon nodes

If a decorated row cannot be recomposed safely for native text replacement, the
handler should still allow tooltip or swap presentation without inventing a
synthetic native payload.

#### Display modes

`ContextMenu` must support all three standard display modes:

- `NativeUiTranslation`
- `TooltipTranslation`
- `NativeUiTranslationWithOriginalTooltips`

Rules:

- Tooltip mode registers hover tooltips per visible row and leaves the native
  menu text untouched.
- Native mode writes translated text to the menu row text nodes.
- Swap mode writes translated text to the native row text nodes while the hover
  tooltip shows the original row text.

Hover hitboxes should come from each row's real collision area rather than from
generic overlay bounds.

### `ToDo`

#### Surface ownership

`ToDo` becomes its own surface and does not merge into `_ToDoList` for:

- config
- persistence
- runtime state
- translation ownership

The explicit config contract should be:

- `Config.TranslateToDo`
- `Config.ToDoTranslationDisplayMode`

The existing `_ToDoList` path stays intact.

#### Persistence model

`ToDo` uses a new dedicated entity and table that are exclusive to the addon:

- entity: `ToDoText`
- table: `todotexts`

This table must not be shared with `_ToDoList`, quest-family rows, or generic
popup storage.

The row should store:

- ordered original captured texts
- ordered translated texts
- `OriginalLang`
- `TranslationLang`
- `TranslationEngine`
- `GameVersion`
- `SourceContentHash`
- `CreatedDate`
- `UpdatedDate`
- `RowVersion`

Lookup should use `SourceContentHash` of the normalized ordered payload rather
than a weak addon-only identity.

#### Capture and exclusion rules

`ToDo` should translate every visible text node that represents user-facing
content, including:

- instance/FATE title
- objective text
- placeholder lines such as `???`

The live timer node must be excluded from capture, lookup, persistence, and
application.

This exclusion is mandatory because the timer changes every second and would
otherwise force constant retranslation, cache misses, and unstable reapply
behavior.

#### Runtime behavior

Translation capture, lookup, queueing, and application must stay asynchronous.
The live UI must not block while translation resolves.

The handler should reuse current visible-state snapshots when only the timer
changes and the translated content-bearing nodes are otherwise unchanged. That
prevents churn and avoids reapplying the same translation on every countdown
tick.

#### Display modes

`ToDo` should use the same translation-mode contract already established for
quest-family dense native surfaces:

- tooltip-only leaves the native nodes untouched
- native writes translated text to the eligible nodes
- swap writes translated text natively and surfaces the original through plugin
  presentation

The timer node remains untouched in every mode.

## Shared Error Handling

- If a translated payload is empty, unusable, or structurally mismatched to the
  captured nodes, do not apply native mutation.
- If lookup misses and translation fails, cache the failure through the shared
  cooldown path so the handler does not retry every frame.
- If the addon content changes while translation is in flight, stale completion
  work must not publish into the new visible generation.
- If a handler did not own the visible native mutation, it must not attempt to
  restore text opportunistically.
- If `ContextMenu` row decoration cannot be preserved safely, prefer tooltip or
  swap presentation over speculative native text writes.

## Testing Strategy

### Contract tests

Add or extend contract tests to prove:

- `ContextMenu` wiring is separate from `TranslateGameMainMenu`
- `ContextMenu` has its own config toggle and display mode
- `ToDo` persistence is exclusive to the `ToDo` addon and does not reuse
  `_ToDoList` rows

### Persistence tests

Add persistence coverage for:

- `ContextMenuText` lookup by normalized ordered payload hash
- `ToDoText` lookup by normalized ordered payload hash
- row reuse staying scoped to the correct dedicated table

### Runtime tests

Add handler coverage for:

- `ContextMenu` capture of variable-length visible row lists
- `ContextMenu` tooltip registration using row-local hitboxes
- `ContextMenu` native mutation restricted to row text nodes
- `ToDo` excluding the timer node from capture and apply
- `ToDo` reusing the current translated presentation when only the timer ticks

### Validation

Implementation work that follows this spec must validate with:

- `dotnet build .\Echoglossian.sln -c Debug --no-restore`
- `dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`

If the implementation extends runtime behavior in a way that can be exercised
meaningfully in `Echoglossian.Mock.Tests`, add that coverage. If the current
mock stack cannot represent the addon behavior, document the gap explicitly
instead of claiming coverage that does not exist.

## Implementation Order

1. land the spec
2. add dedicated config and persistence contracts for `ContextMenu`
3. implement `ContextMenuHandler` with the narrow DB-first seam
4. validate and commit
5. add dedicated `ToDo` persistence and runtime capture rules
6. validate and commit

This order keeps the more isolated menu surface first and the dense instance UI
surface second.
