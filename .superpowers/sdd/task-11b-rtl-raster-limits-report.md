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
