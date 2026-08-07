<!--
  Copyright (c) lokinmodar. All rights reserved.
  Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
-->

# Translation Surface Catalog And Generated Docs Design

**Date:** 2026-08-07

**Repository:** `lokinmodar/Echoglossian`

**Target branch:** `v4-series`

## Objective

Create one reusable, repo-local documentation pipeline that keeps Echoglossian's
translation-surface inventory synchronized across:

- a human-readable runtime map
- a machine-readable JSON audit artifact
- the canonical English support matrix
- every localized support-matrix variant already tracked in `docs/`

The pipeline must be deterministic, reviewable in pull requests, and safe to
run locally without relying on external translation services or manual
copy-paste refreshes.

## Problem

The repository already contains valuable but fragmented runtime documentation:

- `docs/translation-surface-support-matrix.md` inventories user-facing surfaces
  and release status
- family-specific docs such as
  `docs/dialogue-and-toast-runtime-flows.md`,
  `docs/selection-dialog-and-tooltip-runtime-flow.md`,
  `docs/quest-addon-translation-runtime-flow.md`,
  `docs/maincommand-addon-gamewindow-flow.md`,
  `docs/actionmenu-runtime-flow.md`, and
  `docs/action-detail-sheet-flow.md`
  describe operational behavior for subsets of surfaces
- localized matrix files such as
  `docs/translation-surface-support-matrix.pt-BR.md`
  duplicate the high-level inventory in multiple languages

Today there is no single source of truth that states, for every surface:

- how translation is performed
- whether runtime caching exists
- which persistence owner or table is involved
- whether DB reads and writes are synchronous, asynchronous, or absent

That leaves the documentation vulnerable to drift:

- operational facts have to be reconstructed manually from multiple docs and
  code paths
- support-matrix refreshes require repeated hand edits across multiple files
- localized support matrices can fall behind the English source
- there is no mechanical check that a documented surface still matches the
  references it claims to use

## Goals

1. Establish one canonical structured catalog for all translation surfaces.
2. Generate both Markdown and JSON artifacts from that catalog.
3. Regenerate the English support matrix and all tracked localized support
   matrices from the same source.
4. Record operational facts per surface, including translation model, cache
   presence, DB owner, and DB read/write sync-or-async status.
5. Validate the generated output against basic repository reality, including
   referenced docs and required code anchors.
6. Keep the workflow fully local and deterministic.

## Non-goals

- no attempt to infer full runtime semantics automatically from source code
  without a curated catalog
- no LLM- or service-based translation of documentation during generation
- no replacement of the specialized runtime docs that already explain family
  internals in detail
- no automatic edits to unrelated documentation outside the translation-surface
  runtime map and support-matrix files
- no schema migration, plugin behavior change, or runtime refactor in this pass

## Options considered

### Option A: Pure scanner

Generate every artifact by inferring surfaces, caches, and DB behavior directly
from source code and existing docs.

Pros:

- minimal manual catalog content
- attractive on paper as a "self-discovering" solution

Cons:

- fragile for nuanced operational semantics such as hybrid prefetch behavior,
  DB-first consumers, or surfaces split across multiple fallback paths
- difficult to preserve high-quality localized prose
- encourages false confidence when heuristics appear to succeed but silently
  misclassify a surface

### Option B: Canonical catalog plus generator and validator

Store the translation-surface inventory in one structured catalog, then
generate every doc artifact from that catalog while validating referenced
source files and code anchors.

Pros:

- deterministic and reviewable
- keeps operational truth explicit
- easy to diff in PRs
- supports both machine-readable and human-readable outputs
- makes localized support-matrix regeneration safe and repeatable

Cons:

- requires deliberate maintenance of the catalog when a surface changes

### Option C: Hybrid inference with human overrides

Use a partial catalog but let the generator infer missing fields from code.

Pros:

- less manual data entry than a full catalog

Cons:

- harder to reason about when output is wrong
- mixes declarative truth with heuristic guesses
- increases maintenance burden instead of reducing it

## Chosen approach

Choose Option B.

The catalog is the explicit source of truth. The generator automates
publication. Validation catches structural drift, but the script does not
pretend to be authoritative about semantics that only a human maintainer can
curate correctly.

## Architecture

The solution consists of four layers:

1. a canonical structured catalog stored in `docs/`
2. locale-aware template resources for generated support matrices
3. a repo-local generator/validator utility in `scripts/`
4. generated Markdown and JSON artifacts committed back into `docs/`

### Canonical catalog

Add a new structured source file:

- `docs/translation-surface-catalog.json`

The catalog defines:

- translation mode families
- output sections used by the support matrix
- supported documentation locales
- every translation surface and its operational metadata

Each surface entry must include:

- stable display name
- config toggle
- mode family
- support-matrix section
- release status
- short release notes
- translation operation model
- cache summary
- DB owner or persistence family
- DB read behavior: `none`, `sync`, or `async`
- DB write behavior: `none`, `sync`, or `async`
- source docs that justify the entry
- required code anchors that the validator checks with `rg`

Each surface entry may also include:

- localized notes per locale when a locale-specific phrasing is needed
- presentation nuances such as overlay-backed detail text or swap-specific
  behavior
- grouping metadata if multiple surfaces intentionally share the same runtime
  family but need separate user-facing rows

### Generated artifacts

The generator emits:

- `docs/translation-surface-runtime-map.md`
- `docs/translation-surface-runtime-map.json`
- `docs/translation-surface-support-matrix.md`
- every existing localized support matrix:
  - `docs/translation-surface-support-matrix.da.md`
  - `docs/translation-surface-support-matrix.de.md`
  - `docs/translation-surface-support-matrix.el.md`
  - `docs/translation-surface-support-matrix.es.md`
  - `docs/translation-surface-support-matrix.eu.md`
  - `docs/translation-surface-support-matrix.fr.md`
  - `docs/translation-surface-support-matrix.it.md`
  - `docs/translation-surface-support-matrix.pt.md`
  - `docs/translation-surface-support-matrix.pt-BR.md`
  - `docs/translation-surface-support-matrix.ru.md`
  - `docs/translation-surface-support-matrix.vi.md`
  - `docs/translation-surface-support-matrix.zh-CN.md`
  - `docs/translation-surface-support-matrix.zh-TW.md`

The runtime-map Markdown is operational and cross-referenced. It explains the
runtime families once, then maps each surface to:

- family
- translation model
- cache status
- DB owner
- DB read/write sync-or-async status
- supporting docs

The runtime-map JSON mirrors the same facts for scripted audits or future
automation.

The support-matrix Markdown remains the concise user-facing inventory and
continues to document:

- sections
- toggles
- mode families
- notes
- release status

Its prose is generated from the catalog rather than edited manually.

### Locale strategy

Localization of generated support matrices must remain deterministic.

The generator therefore uses:

- locale-specific static labels and headings supplied by a small template map
- surface-level localized note overrides stored explicitly in the catalog when
  needed
- fallback to the English note only when a locale-specific override is missing
  and the catalog explicitly allows that fallback

The generator must not call an external translation service or use an LLM to
create localized prose during generation.

### Generator utility

Add a new utility under `scripts/translation-surface-docs/`.

Recommended implementation shape:

- `scripts/translation-surface-docs/TranslationSurfaceDocs.csproj`
- `scripts/translation-surface-docs/Program.cs`
- optional small internal model and renderer files if needed for clarity

The utility responsibilities are:

1. load and parse `translation-surface-catalog.json`
2. validate schema and required fields
3. validate referenced doc files exist
4. validate required code anchors exist through repo searches
5. render runtime-map JSON
6. render runtime-map Markdown
7. render English support-matrix Markdown
8. render all localized support-matrix Markdown variants
9. fail with actionable diagnostics if validation fails

### PowerShell wrapper

Add a wrapper script:

- `scripts/update-translation-surface-docs.ps1`

The wrapper provides a stable local entry point consistent with the existing
repo scripts. It should:

- run the generator project
- default to updating all outputs
- optionally support validation-only mode if that remains cheap to add

## Data model

The catalog schema should remain intentionally narrow and explicit.

Top-level sections should include:

- `generatedAtPolicy`
- `modeFamilies`
- `sections`
- `locales`
- `surfaces`

Each surface should carry enough information to generate both operational and
inventory outputs without hidden heuristics.

Representative fields:

```json
{
  "id": "MiniTalk",
  "section": "dialogAndOverlay",
  "displayName": "MiniTalk",
  "configToggle": "TranslateMiniTalk",
  "modeFamilyId": "overlay",
  "releaseStatus": "Enabled",
  "notes": {
    "en": "Small native surface; verbose text still requires careful native reflow.",
    "pt-BR": "Superfície nativa pequena; textos mais verbosos ainda exigem native reflow cuidadoso."
  },
  "runtime": {
    "translationModel": "Live capture -> cache or DB lookup -> async translation -> overlay/native publication",
    "cache": "Dedicated MiniTalk cache and shared source-publication lifecycle reuse",
    "dbOwner": "MiniTalkMessage",
    "dbRead": "sync",
    "dbWrite": "async"
  },
  "docs": [
    "docs/dialogue-and-toast-runtime-flows.md"
  ],
  "requiredCodeAnchors": [
    "NativeUI/AddonHandlers/SingleText/MiniTalkHandler.cs",
    "DBHelpers/DbOperations.cs:FindAndReturnMiniTalkMessage",
    "DBHelpers/DbOperations.cs:InsertMiniTalkData"
  ]
}
```

The actual schema may differ in minor naming, but it must preserve these
semantics.

## Rendering rules

### Runtime map Markdown

The generated runtime map should have two layers:

1. runtime-family summaries that explain the operational model once
2. a complete surface table mapping each user-visible surface to its runtime
   facts

This avoids repeating long prose for every row while still covering all
surfaces.

### Runtime map JSON

The JSON output should be stable and diff-friendly:

- consistent ordering
- explicit enums serialized as strings
- no transient timestamps unless intentionally required by policy

The JSON exists for auditing and future automation, not as a cache file with
ephemeral run metadata.

### Support-matrix Markdown

The matrix generator should preserve the current documentation structure where
practical:

- title
- activation flow
- mode-family table
- surface sections
- hidden or temporarily restricted section
- operational notes
- maintenance rules

The content becomes generated, but the document should remain recognizable to
contributors already using it.

## Validation rules

The first implementation should validate only objective facts that the repo can
check mechanically.

Required validations:

1. every surface has a unique ID
2. every surface references a valid section and mode family
3. every surface defines release status, toggle, and notes
4. every runtime DB field is one of `none`, `sync`, or `async`
5. every referenced doc file exists
6. every required code anchor exists in the repository
7. every generated localized matrix target listed in the catalog is emitted
8. every support-matrix surface row can be derived from the catalog with no
   hand-maintained extras

Validation failures must stop generation and tell the maintainer exactly what
to fix.

## Workflow

The intended maintainer workflow becomes:

1. update `docs/translation-surface-catalog.json` when a surface or runtime
   fact changes
2. run `.\scripts\update-translation-surface-docs.ps1`
3. review the generated diffs
4. commit the catalog and regenerated artifacts together

This replaces repeated manual edits across the English matrix, localized
matrices, and any future runtime-map summary.

## File scope

New files:

- `docs/translation-surface-catalog.json`
- `docs/translation-surface-runtime-map.md`
- `docs/translation-surface-runtime-map.json`
- `scripts/update-translation-surface-docs.ps1`
- `scripts/translation-surface-docs/TranslationSurfaceDocs.csproj`
- `scripts/translation-surface-docs/Program.cs`

Modified files:

- `docs/translation-surface-support-matrix.md`
- every existing `docs/translation-surface-support-matrix.*.md`
- `docs/commands/README.md` only if a short reference to the updater belongs
  there after implementation
- `docs/localization-build-flow.md` only if the generated-locale workflow needs
  an explicit cross-reference after implementation

## Testing strategy

The implementation should follow test-first coverage for the generator logic.

The minimum automated checks are:

1. catalog parse succeeds for the canonical file
2. invalid enum values fail with clear diagnostics
3. missing doc references fail
4. missing code anchors fail
5. runtime-map Markdown generation emits expected sections and rows
6. runtime-map JSON generation emits stable expected values
7. support-matrix generation emits the expected English headings and at least
   one known surface row
8. localized generation emits the expected locale-specific title and headings

Validation commands after implementation:

```powershell
dotnet build Echoglossian.sln -c Debug --no-restore
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
```

If the generator ships as a standalone script project not yet covered by the
solution, the implementation must still include a direct local verification
path.

## Risks

- the catalog can still become stale if a code change lands without updating
  it, so the workflow must make regeneration cheap and expected
- localized note quality depends on explicit catalog content; fallback to
  English should be controlled rather than accidental
- overvalidating brittle code anchors could create noisy failures when files are
  renamed without semantic change
- undervalidating anchors could let the catalog drift, so the first cut should
  prefer a few meaningful anchors per family instead of dozens per surface

## Acceptance criteria

This design is complete when the implementation can:

1. regenerate one runtime-map Markdown doc and one runtime-map JSON artifact
   from a canonical catalog
2. regenerate the English support matrix from the same catalog
3. regenerate every currently tracked localized support matrix from the same
   catalog
4. fail deterministically when required docs or code anchors no longer match
   repo reality
5. run locally with a single repo script entry point and no external services
