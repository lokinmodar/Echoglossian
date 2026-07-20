---
name: dalamud-stringarraydata-workflow
description: Work on Character-family StringArrayData runtime behavior and shared structured StringArrayData helpers using the repo's structured payload and ownership docs.
---

# StringArrayData workflow for Echoglossian

Use this skill when working on Character-family StringArrayData runtime behavior, shared structured payload helpers, or structured apply logic.

## Goal

Keep StringArrayData structured, schema-driven, and separated from generic GameWindow or sheet-backed flows.

## Primary sources

Use these repo sources first:

- .github/instructions/character-stringarray.instructions.md
- .github/instructions/character-stringarray-shared.instructions.md
- .github/instructions/character-subwindows.instructions.md
- .github/instructions/stringarraydata-infrastructure.instructions.md
- .github/instructions/stringarraydata-persistence.instructions.md
- docs/string-array-data-clientstructs-research-report.md
- docs/string-array-runtime-ownership-map.md
- docs/textnode-clientstructs-analysis.md
- NativeUI/Helpers/DbFirstStructuredStringArrayHelper.cs
- DBHelpers/StringArrayDataPersistenceHelper.cs
- https://github.com/Exter-N/Dynamis
- https://github.com/MidoriKami/VanillaPlus

## Workflow

1. Determine whether the work is about the root Character window, a Character subwindow, or the shared structured StringArrayData helpers.
2. Keep `AtkValues`, `StringArrayData`, and sheet-backed payloads separate.
3. Use typed canonical slots and stable cache keys instead of broad scans or global mutation hooks.
4. Preserve original payload shape when building, persisting, or reapplying structured rows.
5. Keep persistence, cache suppression, and native update semantics separate.
6. Avoid per-frame retry loops and duplicate translation queues.
7. Treat Character root headers, Character subwindows, and shared structured helper logic as separate surfaces.
8. Carry `SourceClientLanguage` through structured helpers; persist canonical
   source identity and pass the contract to `TranslationService`.
9. Treat blank, generic-provider, unknown, and ambiguous Chinese legacy source
   provenance as non-reusable, and keep overlay-only paths non-mutating.

## Completion checks

- The right Character or structured helper surface was chosen.
- Capture and apply behavior stays value-based and stable.
- Persistence and cache behavior remain canonical.
- Build and test validation is run when code changes are made.
