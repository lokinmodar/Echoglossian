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
- Capture one `SourceClientLanguage` per ActionMenu operation and use
  `TranslationReuseScope` for lookup and queued work.
- Preserve duplicate-node `nodeId:ordinal` traversal in every capture/apply/
  restore path; do not broaden reuse across dynamic menu contexts.
- Invalidate old source publication before exposing a new source generation.
