# TextNode vs ClientStructs Addon Analysis

Date: 2026-04-30

## Goal

Check whether current `TextNode`-based reads and writes in Echoglossian can be replaced, wholly or partially, with addon-specific access provided by `FFXIVClientStructs`.

The intent is not "use ClientStructs everywhere". The correct question is narrower:

1. Does the current addon have a concrete struct in `FFXIVClientStructs`?
2. If it does, does that struct expose semantic fields or only inherited generic helpers?
3. Would switching reduce fragility compared to the current path?

## Scope

This audit focuses on the active paths that currently read or mutate visible text:

- `NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs`
- `NativeUI/Helpers/ActionItemDetailUiRuntime.cs`
- `NativeUI/AddonHandlers/Talk/TalkHandler.cs`
- `NativeUI/AddonHandlers/Talk/TalkSubtitleHandler.cs`
- `NativeUI/AddonHandlers/Talk/BattleTalkHandler.cs`
- `NativeUI/AddonHandlers/Quest/JournalHandler.cs`
- `NativeUI/AddonHandlers/Quest/RecommendListHandler.cs`
- `NativeUI/AddonHandlers/Quest/ToDoListHandler.cs`
- `NativeUI/AddonHandlers/Quest/ScenarioTreeHandler.cs`
- `NativeUI/AddonHandlers/Character/CharacterWindowHandler.cs`
- `NativeUI/AddonHandlers/ActionMenu/ActionMenuWindowHandler.cs`

Reference sources used:

- `C:\Users\lokin\AppData\Roaming\XIVLauncher\addon\Hooks\dev\FFXIVClientStructs.xml`
- `C:\Users\lokin\AppData\Roaming\XIVLauncher\addon\Hooks\dev\Dalamud.xml`

## Current Text Handling Categories

### 1. Shared generic DB-first text-node pipeline

`DbFirstGameWindowAddonHandler` is the main generic path for "capture visible text nodes, look up canonical payload, then write translated text back".

Current behavior:

- captures nodes via `CaptureVisibleTextNodes(...)`
- relies on `AddonTextNodeResolvers.ResolveMiniTalkBubbleTextNodes(...)`
- keys text by `nodeId:ordinal`
- writes back with `AtkTextNode->SetText(...)`
- restores stale text with `AtkTextNode->SetText(previousOriginalText)`

This path is intentionally generic. It should not be replaced wholesale with addon-specific structs.

### 2. Structured tooltip runtime

`ActionItemDetailUiRuntime` is the highest-risk `TextNode` path in the repo.

Current behavior:

- resolves `ActionDetail` / `ItemDetail` addons at runtime
- gets stable content IDs from `AgentActionDetail` and `AgentItemDetail`
- heuristically scans visible text nodes through `CollectStructuredTooltipTextNodeCandidates(...)`
- matches candidate nodes to semantic roles such as name and description
- mutates those nodes directly

This is the best candidate for selective ClientStructs-backed improvement.

### 3. Targeted node-ID handlers

`TalkHandler`, `TalkSubtitleHandler`, and `BattleTalkHandler` already use stable node IDs:

- `GetTextNodeById(...)`
- direct `SetText(...)`
- `AtkValue.SetManagedString(...)` when the refresh payload is available

These handlers are already materially better than blind `NodeList` traversal.

### 4. Manual component-tree traversal

`JournalHandler`, `RecommendListHandler`, and `ToDoListHandler` still walk `UldManager.NodeList` / nested component lists and find text nodes by child layout.

This is the most layout-sensitive family outside tooltips.

### 5. ATK-value-driven paths

Some surfaces are not really `TextNode` problems:

- `ScenarioTreeHandler` works from `AtkValues`
- `AreaMapHandler` works from `AtkValues`
- `MainCommandHandler` is already `AtkValue`-driven and hover-hitbox-driven

These should not be treated as `TextNode -> ClientStructs` migration targets.

## ClientStructs Findings

### AddonItemDetail

Evidence:

- `FFXIVClientStructs.xml` exposes `AddonItemDetail` fields around line `86336+`
- visible semantic members include:
  - `DisplayingItemName`
  - `HeaderStatsGroup`
  - `SpiritbondConditionCrestGroup`
  - `EquipRestrictionGroup`
  - `MaterializeText`
  - `_materiaLines`
  - `_bonuses`

Assessment:

- This is a real semantic addon struct, not just inherited helpers.
- It is the strongest candidate for replacing heuristic `TextNode` candidate collection in `ItemDetail`.

Recommendation:

- Do this selectively.
- Keep the current DB lookup, overlay logic, and atomic apply logic.
- Replace the generic name/description node discovery in `ActionItemDetailUiRuntime` with `AddonItemDetail`-aware resolution where possible.

Risk:

- `AddonItemDetail` still does not automatically solve every inline `SeString` or timing issue.
- Native mutation safety rules must stay intact.

### AddonActionDetail

Evidence:

- The current `FFXIVClientStructs.xml` does not expose a documented `AddonActionDetail` member surface comparable to `AddonItemDetail`.
- The only relevant hit in this audit was a tooltip-args remark around line `202558`.
- The runtime already gets stable action identity from:
  - `AgentActionDetail.Instance()->ActionId`
  - `AgentActionDetail.Instance()->OriginalId`
  - `GameGui.HoveredAction.ActionId`
  - `GameGui.HoveredAction.DetailKind`

Assessment:

- There is no useful addon-specific struct surface in the current reference set for replacing the tooltip layout scan.
- The repo is already using the correct agent-backed identity path.

Recommendation:

- Do not plan a ClientStructs addon migration for `ActionDetail` layout itself right now.
- Keep `AgentActionDetail` as the semantic source of identity.
- Keep tooltip-node resolution isolated and conservative.

Risk:

- Any attempt to "fake" an addon-specific migration here would still depend on layout heuristics, just with a cast in front of it.

### AddonActionMenu

Evidence:

- `FFXIVClientStructs.xml` exposes `AddonActionMenu` extensively around lines `34386+`
- exposed members include:
  - `Actions`
  - `RootNode`
  - `WindowNode`
  - `AtkValues`
  - `GetNodeById(...)`
  - `GetTextNodeById(...)`
  - `GetComponentNodeById(...)`
  - `EnableTextNodePopulation`

Assessment:

- This is a real typed addon with meaningful surface area.
- If `ActionMenu` needs further hardening, typed access is available.

Recommendation:

- Use this only for targeted improvements.
- Do not rewrite the whole `ActionMenuWindowHandler` away from the current DB-first flow.
- Good targets would be stable chrome, action rows, or specific component access where current payload capture proves noisy.

### AddonCharacter

Evidence:

- `FFXIVClientStructs.xml` exposes `AddonCharacter` around lines `44928+`
- it includes `_tabs` and the usual addon base accessors

Assessment:

- There is at least some semantic structure here, especially around tabs/chrome.
- The current runtime already uses `StringArrayType.Character`, `AtkValues`, and value-based visible-text apply for the stable window chrome.

Recommendation:

- Use ClientStructs selectively for stable root chrome or tab ownership if future issues require it.
- Do not replace the current `Character` family architecture wholesale.

### AddonToDoList

Evidence:

- `FFXIVClientStructs.xml` exposes `AddonToDoList` around lines `158462+`
- visible members in this audit were effectively inherited addon helpers:
  - `GetNodeById(...)`
  - `GetTextNodeById(...)`
  - `GetComponentNodeById(...)`
  - `AtkValues`
  - visibility and base addon methods
- no semantic row model or quest-entry fields were visible in the current reference set

Assessment:

- The type exists, but it does not appear to give a semantic representation of the visible quest rows.
- A cast alone would not remove the current need to walk component trees.

Recommendation:

- Keep the current traversal-based implementation unless a stronger typed row surface appears later.
- At most, cast to `AddonToDoList` for convenience if the code becomes clearer, but do not expect it to change fragility materially.

### AddonTalk / TalkSubtitle / BattleTalk

Evidence:

- `TalkHandler` already uses stable node IDs via `GetTextNodeById(...)`
- `TalkSubtitleHandler` already uses fixed node IDs plus `AtkValue.SetManagedString(...)`
- `BattleTalkHandler` already uses `GetTextNodeById(...)`
- `FFXIVClientStructs.xml` shows `AddonTalk` in the current reference set, but the visible surface in this audit is primarily inherited base functionality

Assessment:

- These are already on the correct side of the tradeoff.
- There is no strong reason to migrate them just to add a more specific struct type name.

Recommendation:

- Keep current access patterns.
- The main optimization surface here is translation flow and native mutation safety, not ClientStructs addon typing.

### Journal / RecommendList / ScenarioTree

Evidence:

- In the current audit, no useful addon-specific `FFXIVClientStructs` surface was found for:
  - `AddonJournal`
  - `AddonRecommendList`
  - `AddonScenarioTree`
- `ScenarioTreeHandler` already uses `AtkValues`, not generic `TextNode` mutation.
- `JournalHandler` and `RecommendListHandler` are still layout-driven component traversals.

Assessment:

- There is no current evidence that `ClientStructs` gives a semantic replacement for those row layouts.
- `ScenarioTree` is already using the better data path for its current design.

Recommendation:

- Do not schedule a ClientStructs addon migration for these surfaces right now.
- If these windows need more stability, the likely fix remains a better canonical data path, not a simple cast to an addon struct.

### AreaMap and MainCommand

Assessment:

- `AreaMapHandler` is `AtkValue`-driven.
- `MainCommandHandler` is `AtkValue`-driven and hover-hitbox-driven.

Recommendation:

- Out of scope for `TextNode` migration.

## Addon-by-Addon Recommendation Matrix

| Surface | Current Access Pattern | ClientStructs Value | Recommendation |
| --- | --- | --- | --- |
| `ItemDetail` | heuristic text-node candidate scan + direct mutation | high | Do now, selectively |
| `ActionDetail` | agent-backed ID + heuristic tooltip-node scan | low for addon layout, high for agent identity only | Keep current agent path; do not plan addon-layout migration |
| `ActionMenu` | DB-first generic payload + text nodes + atk values | medium to high | Selective override only |
| `Character` | string array + atk values + visible-text matching | medium | Selective override only |
| `Talk` | fixed `GetTextNodeById` + refresh `AtkValues` | low | Keep current |
| `TalkSubtitle` | fixed `GetTextNodeById` + refresh `AtkValues` | low | Keep current |
| `BattleTalk` | fixed `GetTextNodeById` | low | Keep current |
| `Journal` | manual `NodeList` traversal | low in current refs | Keep current traversal |
| `RecommendList` | manual `NodeList` traversal | low in current refs | Keep current traversal |
| `ToDoList` | manual `NodeList` traversal | low in current refs | Keep current traversal |
| `ScenarioTree` | `AtkValues` | not a TextNode problem | Keep current |
| `AreaMap` | `AtkValues` | not a TextNode problem | Keep current |
| `MainCommand` | `AtkValues` + hover hitboxes | not a TextNode problem | Keep current |

## What Should Actually Change

### Highest-value change

The best migration target is `ItemDetail` inside `ActionItemDetailUiRuntime`.

Why:

- the current code still uses heuristic candidate collection
- `FFXIVClientStructs` gives meaningful `AddonItemDetail` structure
- this is the path most exposed to partial node matches, wrong-node writes, and restore hazards

Suggested implementation direction:

1. Keep `AgentItemDetail` as the identity source.
2. Resolve the live tooltip addon as `AddonItemDetail` when available.
3. Prefer semantic groups and stable item-name ownership from that struct over full-text candidate scoring.
4. Fall back to the current heuristic path only when the typed path cannot resolve the needed nodes safely.

### Medium-value change

Use `AddonCharacter` and `AddonActionMenu` only for stable sub-surfaces that keep recurring as brittle in production.

Good examples:

- stable tab chrome
- stable row anchors
- specific component ownership that is currently inferred from tree order

### What should not change

Do not replace the shared generic `DbFirstGameWindowAddonHandler` with addon-specific structs across the board.

Why:

- that handler is meant to stay generic
- many target addons do not have useful semantic struct coverage
- the repo already separates capture, lookup, native mutation, and overlay logic correctly
- a broad migration would increase patch-risk without guaranteeing better stability

## Bottom Line

Yes, some current `TextNode` operations can and should be tightened with addon-specific `FFXIVClientStructs`.

But the answer is selective, not global:

- `ItemDetail`: yes, high-value target
- `ActionMenu`: maybe, targeted only
- `Character`: maybe, targeted only
- `Talk*`: mostly already fine
- `Journal` / `RecommendList` / `ToDoList`: current refs do not provide enough semantic addon structure to justify a migration
- `ScenarioTree`, `AreaMap`, `MainCommand`: not `TextNode` migration targets

## Suggested Next Step

If this analysis is accepted, the next implementation step should be:

1. rework only the `ItemDetail` node-resolution path in `ActionItemDetailUiRuntime`
2. keep the current translation/cache/overlay architecture unchanged
3. treat the typed `AddonItemDetail` path as a preferred resolver with heuristic fallback

That is the smallest change with the best expected return.
