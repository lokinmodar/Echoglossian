---
description: "Use when editing MainCommand and AddonContextMenuTitle runtime code in Echoglossian."
applyTo: "NativeUI/AddonHandlers/MainMenu/**/*.cs"
---

# MainMenu GameWindow runtime

- `_MainCommand` and `AddonContextMenuTitle` are live GameWindow runtimes.
- `MainCommandText` is a separate sheet-backed canonical source and does not replace the live addon runtime.
- Keep `AtkValue` capture and text-node capture local to the runtime that owns the mutation.
- `AddonContextMenuTitle` reuses visible slots across submenu contexts, so compatible superset payload reuse is not stable there.
- Tie hover registration and native writes to the current display mode and current visible addon state.
- Keep `MainCommand` refresh handling focused on the ATK-value payload and avoid redirecting through StringArrayData.
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
