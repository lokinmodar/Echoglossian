---
description: "Use when editing the main-menu GameWindow runtimes in Echoglossian."
applyTo: "NativeUI/AddonHandlers/MainMenu/**/*.cs"
---

# MainMenu GameWindow runtime

- `_MainCommand`, `AddonContextMenuTitle`, and `SystemMenu` are live
  GameWindow-family runtimes.
- `ContextMenu` is a dedicated adjacent runtime that still uses the shared
  DB-first GameWindow base, but persists to its own `ContextMenuText` table.
- `MainCommandText` is a separate sheet-backed canonical source and does not replace the live addon runtime.
- Keep `AtkValue` capture and text-node capture local to the runtime that owns the mutation.
- `AddonContextMenuTitle` reuses visible slots across submenu contexts, so compatible superset payload reuse is not stable there.
- `SystemMenu` also reuses visible text-node slots across menu states, so broad
  compatible-payload reuse must stay disabled there as well.
- `ContextMenu` captures row labels from the live row chain and uses row
  collision bounds for hover registration; do not flatten it into generic addon
  text-node scanning when the row-chain resolver already has the correct shape.
- Tie hover registration and native writes to the current display mode and current visible addon state.
- Keep `MainCommand` refresh handling focused on the ATK-value payload and avoid redirecting through StringArrayData.
- Do not treat Dalamud `IContextMenu` / `ContextMenu.cs` as the authoritative
  translation owner for `ContextMenu`. That service may become an auxiliary
  "menu opened" signal later, but it does not replace native payload capture,
  persistence, apply, or restore.
- Prefer the current runtime flow docs when extending or debugging this path.
- Capture one complete `TranslationReuseScope` per refresh operation rather
  than independently derived source strings. Its source, target, engine, and
  reuse-policy values must remain the authority through asynchronous lookup,
  translation, persistence, and publication.
- Do not reread mutable target or engine configuration after queueing. A changed
  scope retires prior work; an unchanged `PreDraw` scope keeps its in-flight
  operation valid.
- Preserve visible duplicate-node `nodeId:ordinal` allocation across capture,
  apply, stale recovery, and restore; filtered visible nodes consume an ordinal.
- Invalidate old scope-owned state before publishing a new generation;
  do not use broad superset reuse for submenu contexts.
