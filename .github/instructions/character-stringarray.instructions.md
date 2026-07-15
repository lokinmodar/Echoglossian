---
description: "Use when editing the main Character window, stable Character headers, or Character StringArrayData-backed runtime behavior in Echoglossian."
applyTo:
  - "NativeUI/AddonHandlers/Character/CharacterWindowHandler.cs"
---

# Character root runtime

- The root Character window is a DB-first StringArrayData-backed runtime, not a generic GameWindow surface.
- Prefer the shared canonical Character payload and `StringArrayType.Character` over ad hoc text capture.
- Stable root headers may use local fallback translations; dynamic Character subwindows own their own visible text and should not be mixed into the root window state.
- Capture, canonical lookup, and apply should stay value-based and avoid unstable node ordinals.
- Do not keep retranslating every frame or carry stale payloads across visible subwindow states.
- Use `AtkValues` and text nodes only when that path actually owns the mutation.
- If the runtime behavior is unclear, check the Character and StringArrayData history docs before broadening the change.
- Persist canonical Character rows with the operation-captured source identity;
  use the shared structured source contract rather than a derived provider code.
- Do not reuse blank or ambiguous legacy source rows, and do not write native
  `AtkValue` or text-node state in overlay-only mode.
