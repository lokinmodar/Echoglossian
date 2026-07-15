# Task 11B Report

## RED/GREEN Evidence

- RED: focused renderer, cache, and presentation tests ran before production
  changes; 5 new assertions failed while 18 existing focused tests passed.
- GREEN: the same focused suite passed 23/23 after the bounded implementation.

## Limits And Failure Behavior

- `TextRasterLimits` centralizes the 2048-pixel dimension, 2,097,152-pixel
  area, and 48 MiB single-texture cache limits.
- Request and adaptive tooltip widths clamp to the shared dimension. Unbroken
  words split at text-element boundaries before target bitmap allocation.
- The texture worker measures before invoking the upload seam. Over-limit
  layouts fail before PNG encoding, upload, or cache insertion and use the
  existing bounded retry cooldown.

## Validation

- Focused tests: 23 passed, 0 failed.
- `dotnet build .\\Echoglossian.sln -c Debug --no-restore`: passed with the
  existing multilingual-toolkit and SQLite advisory warnings.
- `dotnet test .\\Echoglossian.Tests\\Echoglossian.Tests.csproj -c Debug --no-build`:
  553 passed, 0 failed.
- `git diff --check`: passed.

## Residual Manual Checks

- In-game: verify long Arabic/Persian unbroken tooltip text at large viewport
  sizes remains readable, does not schedule repeated failed uploads, and keeps
  normal RTL alignment and adaptive tooltip behavior.

## Duplicate Layout Follow-Up

- Root cause: normal cache-miss work measured a layout for the raster-limit
  preflight, then the default upload delegate created a second renderer and
  rebuilt that same layout before rendering.
- RED: the focused precomputed-layout regression failed because the required
  layout creation and direct rendering seam did not exist.
- GREEN: the renderer now creates one immutable layout, validates it before
  target bitmap allocation, and passes that layout with its renderer to the
  default PNG/upload path. The previously built focused binary passed 24/24.
- Fresh focused and solution builds were attempted twice but are blocked by
  concurrent `CS0452` errors in `DbFirstGameWindowAddonHandler.cs` lines 3866
  and 3905. Full no-build validation ran 557 tests: 554 passed; three unrelated
  concurrently modified ActionMenu/native-dialogue/source-scope tests failed.
- The final direct regression source compiled by building
  `Echoglossian.Tests` with `BuildProjectReferences=false`; its focused run
  passed 24/24. Repeating full no-build validation produced the same three
  unrelated failures and 554 passing tests.
