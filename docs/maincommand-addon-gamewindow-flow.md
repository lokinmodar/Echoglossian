# _MainCommand addon runtime flow

This document covers the live `_MainCommand` addon behavior that is already
working today.

## Runtime owner

- table: `GameWindow`
- handler:
  `NativeUI/AddonHandlers/MainMenu/MainCommandHandler.cs`
- base runtime:
  `NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs`

## Data flow

```text
_MainCommand addon lifecycle
  -> capture visible payload from live addon
  -> GameWindow lookup
  -> background translation when missing
  -> GameWindow persist
  -> native apply / hover tooltip registration
```

## Important boundary

- This runtime remains on `GameWindow`.
- It is not being migrated to `StringArrayDatas`.
- It is also not being replaced by `MainCommandText`.

`MainCommandText` is a sheet-backed canonical lookup source. `_MainCommand` is a
live addon runtime. They are complementary, not competing owners.

## #139 Runtime Rules

- Capture one `SourceClientLanguage` for each live operation and retain it
  through lookup, translation, persistence, and publication.
- Use `TranslationReuseScope` for reuse; dynamic MainCommand contexts must not
  use broad compatible-superset payload reuse.
- Preserve visible text-node `nodeId:ordinal` traversal across capture, apply,
  stale recovery, and restore. Every visible node consumes its ordinal before
  capture filters run.
- On source transition, invalidate old native/hover/publication state before
  publishing the new source generation. Overlay-only mode must not mutate
  native state.
