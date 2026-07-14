---
description: "Use when editing ActionMenu runtime code in Echoglossian."
applyTo: "NativeUI/AddonHandlers/ActionMenu/**/*.cs"
---

# ActionMenu runtime

- `ActionMenu` stays on the live GameWindow ownership path.
- Keep the canonical text caches and persisted `GameWindow` payloads as the source of truth for repeated labels.
- Preserve stable page signatures and avoid treating reused pages as interchangeable when their visible payload differs.
- Reuse `MainCommandText` and the action-adjacent reference-text flows when they provide a better canonical source.
- Keep capture and hover/native apply tied to the current page, not to a broad global scan.
- Do not migrate this runtime onto StringArrayData.
- Prefer the smallest page-specific fix when one menu section is unstable.
- Capture one complete `TranslationReuseScope` per ActionMenu operation for
  lookup, queue ownership, post-translation normalization, diagnostics, and
  persistence. Do not derive target or engine from mutable configuration after
  queueing.
- Stable payload signatures belong to that full scope. Release a signature when
  its request fails or its translated payload is rejected; a changed scope owns
  a new signature set.
- Preserve duplicate-node `nodeId:ordinal` traversal in every capture/apply/
  restore path; do not broaden reuse across dynamic menu contexts.
- Invalidate old scope-owned publication before exposing a new generation.
