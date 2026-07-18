<!--
  Copyright (c) lokinmodar. All rights reserved.
  Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
-->

# Journal and JournalDetail Surface Fix Iteration Log

## Purpose

This log tracks regressions and fixes for the `Journal` and `JournalDetail` quest surfaces to avoid repeated regressions while native reflow work evolves.

## Mode Contract (must stay true)

`NativeUiTranslation`
- Apply translated text in native nodes.
- Do not register hover translation tooltips for this surface mode.

`TooltipTranslation`
- Keep native nodes untouched.
- Register hover tooltips with translated text.

`NativeUiTranslationWithOriginalTooltips`
- Apply translated text in native nodes.
- Register hover tooltips showing original source text.

## Iteration 2026-05-31 (issue-181-journaldetail-reflow)

### Observed regressions

- `Journal` list entries could receive mismatched quest-name translations while scrolling.
- `JournalDetail` could apply only the quest title and skip body text because native application depended on one all-or-nothing readiness gate.
- Mode behavior drift was reported for the native+original-tooltip scenario.

### Root causes identified

- `Journal` reused live text nodes across rows while runtime cache still associated original text by node address.
- `JournalDetail` native write path required all sections to be ready at the same time, even when some translated sections were already available and safe to apply.

### Implemented fixes

- `Journal`: added node-reuse guard that resets per-node original/hover/native-mutation cache when visible node text no longer matches cached original or its cached translated counterpart.
- `JournalDetail`: removed all-or-nothing native gating and switched to section-diff based native apply:
  - name
  - description
  - objectives
  - summary
  Native mutation now occurs whenever at least one section differs from original; otherwise state is restored if previously mutated.

### Validation commands

- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\\Echoglossian.Tests.csproj -c Debug --no-build`

### In-game validation checklist

- `Journal`:
  - Scroll long quest list up/down rapidly.
  - Confirm each row keeps correct translation mapping with no cross-row bleed.

- `JournalDetail` (`NativeUiTranslationWithOriginalTooltips`):
  - Confirm native title and body sections apply whenever their translated content is available.
  - Confirm hover displays original text for title/body hit regions.

- `JournalDetail` (`TooltipTranslation`):
  - Confirm native content stays original.
  - Confirm hover displays translated text.

- `JournalDetail` (`NativeUiTranslation`):
  - Confirm native content shows translated text.
  - Confirm no hover translation tooltip for this mode.

