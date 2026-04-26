# Action-domain runtime flow index

This index separates the currently active action-adjacent data flows so storage,
cache ownership, and runtime behavior stay unambiguous.

## Active flows

1. Action/Item/Trait detail sheet prefetch
   - doc: [action-detail-sheet-flow.md](/C:/Dante/_dalamud/Echoglossian/docs/action-detail-sheet-flow.md)
   - owner: dedicated entity families (`ActionTooltip`, `ItemTooltip`, `Trait`)
   - entrypoint: `TickActionDetailPrefetch()`, `TickItemDetailPrefetch()`, `TickTraitDetailPrefetch()`

2. Action-adjacent reference sheet prefetch
   - doc: [action-reference-text-sheet-flow.md](/C:/Dante/_dalamud/Echoglossian/docs/action-reference-text-sheet-flow.md)
   - owner: `*ActionText` families plus `MainCommandText`
   - entrypoint: `TickReferenceTextPrefetch()`

3. `_MainCommand` live addon runtime
   - doc: [maincommand-addon-gamewindow-flow.md](/C:/Dante/_dalamud/Echoglossian/docs/maincommand-addon-gamewindow-flow.md)
   - owner: `GameWindow`
   - entrypoint: `MainCommandHandler`

4. `ActionMenu` live runtime
   - doc: [actionmenu-runtime-flow.md](/C:/Dante/_dalamud/Echoglossian/docs/actionmenu-runtime-flow.md)
   - owner: `GameWindow` plus cache-first lookups into action-adjacent reference stores
   - entrypoint: `ActionMenuWindowHandler`

## Boundary rule

- Sheet-backed canonical text storage is for reusable lookup, translation reuse,
  and future DB-first features.
- Live addon capture is a separate concern and stays on its proven runtime path
  unless there is an explicit migration request.
