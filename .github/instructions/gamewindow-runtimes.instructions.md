---
description: "Use when editing the shared DB-first GameWindow runtime in Echoglossian."
applyTo:
  - "NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs"
  - "DBHelpers/GameWindowPersistenceHelper.cs"
  - "EFCoreSqlite/Models/GameWindow*.cs"
---

# Shared GameWindow runtime

- The shared DB-first GameWindow base owns the common capture, lookup, persistence, and apply flow.
- Keep `AtkValue` and text-node capture local to the runtime that actually owns the mutation.
- Preserve stable payload signatures and context-sensitive page or submenu ownership.
- Do not redirect GameWindow surfaces through StringArrayData.
- Keep hover-tooltip registration and native writes tied to the current display mode.
- If the base flow changes, preserve the current live-addon lifecycle and persistence semantics.
- For unclear native UI behavior, consult official Dalamud and
  `FFXIVClientStructs` references first, then use `Exter-N/Dynamis` and
  `MidoriKami/VanillaPlus` as trusted reverse-engineering and runtime
  inspection references.
- Capture one complete `TranslationReuseScope` per operation and carry it
  through lookup, provider work, persistence, and publication; fail closed if
  source resolution fails. It owns source, target, effective engine, and reuse
  policy, while content and version remain owner-query filters.
- Do not reread mutable target or engine configuration after asynchronous work
  begins. Any scope member changing must retire the prior operation before it
  persists or publishes; a same-scope `PreDraw` must not reduce the token to
  source-only or retire in-flight work.
- Do not widen dynamic-context reuse.
- Keep `nodeId:ordinal` allocation identical across capture, apply, stale
  recovery, and restore. Every visible node consumes an ordinal before filters.
- Invalidate old scope-owned state before publishing a new generation.
