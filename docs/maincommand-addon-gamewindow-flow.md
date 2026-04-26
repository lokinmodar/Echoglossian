# _MainCommand addon runtime flow

This document covers the live `_MainCommand` addon behavior that is already
working today.

## Runtime owner

- table: `GameWindow`
- handler:
  [MainCommandHandler.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/AddonHandlers/MainMenu/MainCommandHandler.cs)
- base runtime:
  [DbFirstGameWindowAddonHandler.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs)

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
