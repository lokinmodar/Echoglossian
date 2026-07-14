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
- Capture one `SourceClientLanguage` per operation and carry it through lookup,
  provider work, persistence, and publication; fail closed if it is unresolved.
- Build reuse with `TranslationReuseScope`, including source, target, content,
  version, and engine-policy checks; do not widen dynamic-context reuse.
- Keep `nodeId:ordinal` allocation identical across capture, apply, stale
  recovery, and restore. Every visible node consumes an ordinal before filters.
- Invalidate old source-owned state before publishing a new source generation.
