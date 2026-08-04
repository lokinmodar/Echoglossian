# Main menu addon runtime flow

This document covers the live main-menu surfaces that currently share the
DB-first GameWindow runtime family, plus the dedicated adjacent `ContextMenu`
runtime that now belongs to the same implementation area.

## Runtime owners

| Surface | Handler | Persistence owner | Capture shape |
|---------|---------|-------------------|---------------|
| `_MainCommand` | `NativeUI/AddonHandlers/MainMenu/MainCommandHandler.cs` | `GameWindow` | live text-node payload |
| `AddonContextMenuTitle` | `NativeUI/AddonHandlers/MainMenu/AddonContextMenuTitleHandler.cs` | `GameWindow` | live text-node payload |
| `SystemMenu` | `NativeUI/AddonHandlers/MainMenu/SystemMenuHandler.cs` | `GameWindow` with supplemental canonical lookup | filtered live text-node payload |
| `ContextMenu` | `NativeUI/AddonHandlers/MainMenu/ContextMenuHandler.cs` | dedicated `ContextMenuText` | live row-chain text-node payload |

Shared base runtime:

- `NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs`

## Data flow

```text
main-menu addon lifecycle
  -> capture visible payload from live addon
  -> owner-specific lookup
  -> background translation when missing
  -> owner-specific persistence
  -> native apply / hover tooltip registration / guarded restore
```

## Ownership boundaries

- `_MainCommand`, `AddonContextMenuTitle`, and `SystemMenu` remain live
  GameWindow-family runtimes.
- None of those surfaces are being migrated to `StringArrayDatas`.
- `MainCommandText` remains a sheet-backed canonical lookup source. It does not
  replace the live runtime owner.
- `ContextMenu` is intentionally separate from `GameWindow`. It has its own
  `ContextMenuText` table because row labels come from instanced or
  context-sensitive menu content that does not reconcile cleanly with the
  `GameWindow` row shape.
- Dalamud `IContextMenu` / `Dalamud.Game.Gui.ContextMenu.ContextMenu` is not
  the runtime owner for translation. At most, it is an auxiliary future signal
  for menu-open timing.

## Surface notes

### `_MainCommand`

- Keeps its live runtime on `GameWindow`.
- Captures the visible text-node payload and persists translated rows in
  `GameWindow`.
- `MainCommandText` stays complementary: it is reusable canonical data, not the
  mutation owner for the live addon.

### `AddonContextMenuTitle`

- Shares the GameWindow base but must stay strict about visible payload shape.
- Compatible superset payload reuse is not stable because submenu contexts
  recycle the same visible slots.

### `SystemMenu`

- Shares the GameWindow base and the same display-mode contract as
  `_MainCommand`.
- Filters capture to the title and menu-entry text nodes actually used by the
  live SystemMenu states.
- Reuses `MainCommandCanonicalTextResolver` as a supplemental source of
  original and translated text, but still keeps native capture/apply local to
  the live addon.
- Broad compatible-payload reuse must remain disabled because SystemMenu
  recycles the same visible slots across states.

### `ContextMenu`

- Resolves row labels from the live row chain instead of generic readable
  text-node scans.
- Persists only normalized printable row labels into the dedicated
  `ContextMenuText` table.
- Hover registration uses row collision bounds, not the whole addon rectangle.
- Native replacement is guarded: if the live row still contains decorations the
  handler cannot safely rebuild, native mutation is skipped and hover falls back
  to translated tooltip presentation for that row.

## #139 Runtime rules

- Capture one complete `TranslationReuseScope` for each live operation: source
  client identity, target, effective engine, and engine-reuse policy. Retain it
  through lookup, translation, persistence, and publication; do not reread
  mutable target or engine configuration after the request begins.
- A source, target, engine, or policy change retires prior asynchronous work;
  a same-scope `PreDraw` must preserve the in-flight operation. Dynamic
  main-menu contexts must not use broad compatible-superset payload reuse.
- Preserve visible text-node `nodeId:ordinal` traversal across capture, apply,
  stale recovery, and restore. Every visible node consumes its ordinal before
  capture filters run.
- On any source, target, engine, or policy transition, invalidate old
  native/hover/publication state before publishing the new scope generation.
  Overlay-only mode must not mutate native state.
