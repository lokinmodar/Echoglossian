---
description: "Use when editing StringArrayData runtime helpers, canonical payload schemas, or structured apply logic in Echoglossian."
applyTo:
  - "NativeUI/Helpers/DbFirstStructuredStringArrayHelper.cs"
  - "NativeUI/Helpers/IStringArrayStructuredSchema.cs"
  - "NativeUI/Helpers/StringArrayStructuredPayload*.cs"
---

# StringArrayData structured runtime

- Treat StringArrayData as a structured runtime with explicit schema, not raw text.
- Keep `AtkValues`, `StringArrayData`, and sheet-backed payloads separate.
- Prefer typed canonical slots and stable cache keys over global hooks or broad scans.
- Preserve original payload shape and avoid flattening when adding or updating structured rows.
- Use failure and cooldown behavior so repaint-heavy windows do not retry every frame.
- Do not introduce a second translation queue or a generic global mutation observer unless the design docs clearly require it.
- Keep addon lifecycle and native update semantics intact when writing back translated values.
- For uncertain native array, addon, or node ownership, consult official
  Dalamud and `FFXIVClientStructs` references first, then use
  `Exter-N/Dynamis` and `MidoriKami/VanillaPlus` as trusted reverse-engineering
  and runtime inspection references.
- Carry `SourceClientLanguage` through structured helpers; persist its
  `PersistenceCode` and pass the contract to `TranslationService`.
- Reject blank, unknown, generic-provider, and ambiguous Chinese source
  provenance for reuse. Overlay-only flow must not mutate native values.
