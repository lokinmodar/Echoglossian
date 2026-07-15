---
description: "Use when editing shared Character StringArrayData helpers in Echoglossian."
applyTo:
  - "NativeUI/AddonHandlers/Character/CharacterTextNodeWindowHandlerBase.cs"
  - "NativeUI/AddonHandlers/Character/CharacterCanonicalPayloadHelper.cs"
---

# Character shared helpers

- The shared Character helpers back the root window and the Character subwindows.
- Keep the canonical Character payload and `StringArrayType.Character` as the source of truth.
- Preserve value-based capture and canonical lookup rather than unstable node ordinals.
- Do not introduce a second translation queue or a generic Character-specific global hook.
- Keep `AtkValues` and text-node writes local to the code path that owns the mutation.
- Use the Character and StringArrayData history docs if the capture or apply boundary is unclear.
- Canonical rows include the captured source persistence identity. Carry
  `SourceClientLanguage` through structured helper and translation boundaries.
- Unknown, blank, generic-provider, and ambiguous Chinese legacy source values
  are not reusable. Overlay-only mode must not mutate native state.
