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

- Capture one complete `TranslationReuseScope` for each live operation: source
  client identity, target, effective engine, and engine-reuse policy. Retain it
  through lookup, translation, persistence, and publication; do not reread
  mutable target or engine configuration after the request begins.
- A source, target, engine, or policy change retires prior asynchronous work;
  a same-scope `PreDraw` must preserve the in-flight operation. Dynamic
  MainCommand contexts must not use broad compatible-superset payload reuse.
- Preserve visible text-node `nodeId:ordinal` traversal across capture, apply,
  stale recovery, and restore. Every visible node consumes its ordinal before
  capture filters run.
- On any source, target, engine, or policy transition, invalidate old
  native/hover/publication state before publishing the new scope generation.
  Overlay-only mode must not mutate native state.
