---
name: dalamud-gamewindow-workflow
description: Work on ActionMenu, MainCommand, AddonContextMenuTitle, and the shared DB-first GameWindow runtime using the repo's game-window flow docs and instructions.
---

# GameWindow workflow for Echoglossian

Use this skill when working on ActionMenu, \_MainCommand, AddonContextMenuTitle, or the shared DB-first GameWindow base.

## Goal

Keep each live addon on its intended ownership path and avoid redirecting GameWindow surfaces through StringArrayData or sheet-backed text flows.

## Primary sources

Use these repo sources first:

- .github/instructions/gamewindow-runtimes.instructions.md
- .github/instructions/actionmenu-gamewindow.instructions.md
- .github/instructions/mainmenu-gamewindow.instructions.md
- docs/maincommand-addon-gamewindow-flow.md
- docs/actionmenu-runtime-flow.md
- docs/textnode-clientstructs-analysis.md
- docs/maincommand-sheet-flow.md
- docs/action-reference-text-sheet-flow.md

## Workflow

1. Decide whether the change touches the shared GameWindow base or one live addon runtime.
2. Preserve stable payload signatures and context-sensitive ownership for the current page or submenu.
3. Keep ATK-value capture and text-node capture local to the runtime that owns the mutation.
4. For ActionMenu, keep the live menu on the GameWindow path and reuse persisted payloads or canonical text caches when possible.
5. For \_MainCommand and AddonContextMenuTitle, keep refresh handling and hover registration tied to the live ATK payload and current visible addon state.
6. Do not treat MainCommandText or other sheet-backed rows as the live runtime owner.
7. Avoid reusable superset payloads when the same visible slots mean different contexts.
8. Capture one complete `TranslationReuseScope` per operation and use it for
   lookup, queue, persistence, and publication. It owns source, target,
   effective engine, and engine-reuse policy; content and version remain
   owner-query filters.
9. Never reread mutable target or engine configuration after asynchronous work
   begins. A changed scope retires old completion work, while a same-scope
   `PreDraw` must preserve the in-flight operation.
10. Capture the effective translator instance before the first await when an
   operation makes more than one provider call. Every chunk must reuse that
   captured resolution rather than reroute through mutable configuration.
11. For ActionMenu stable signatures, include the lifecycle generation in queue
   ownership. A stale generation must not release a newer same-scope request.
   Evaluate a failed-payload cooldown before claiming the signature so an
   ineligible refresh cannot suppress the later retry.
12. Keep duplicate visible-node `nodeId:ordinal` allocation consistent across
   capture, apply, stale recovery, and restore; filtered visible nodes consume
   an ordinal.
13. Invalidate old scope-owned state before publishing a new generation.

## Completion checks

- The runtime remains on the correct ownership model.
- Hover, native write, and restore behavior match the selected display mode.
- The change stays within the smallest addon or shared base that controls the behavior.
- Build and test validation is run when code changes are made.
- Source transitions cannot publish stale async work into the new generation.
