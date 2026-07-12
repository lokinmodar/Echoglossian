# Story-Surface Debugger and DB Manager Guide

This guide documents the shared operator and developer flow added for
story-facing surfaces that are shown directly to the player.

Current covered surfaces:

- `Talk`
- `BattleTalk`
- `TalkSubtitle`
- `CutSceneSelectString`
- `TextGimmickHint`

The goal of this flow is narrow:

- show the latest visible story-surface payload in `/eglotranslatordebugger`
- let the operator force a fresh visible retranslation and persistence
- show where the current visible result came from
- hand the operator off to `/eglodbmanager` on the matching table
- reuse one read-only display layer between debugger and DB manager
- keep DB CRUD and EF metadata handling inside the DB manager only

## Runtime Pieces

The flow is split across a few small runtime-specific components.

### Handler contract

Visible story-facing handlers that support explicit retranslation implement
[`IVisibleDialogueRetranslationHandler`](../NativeUI/AddonHandlers/Talk/IVisibleDialogueRetranslationHandler.cs).

That contract is responsible for:

- deciding whether the handler currently has a visible applicable payload
- forcing a fresh live translation for that visible payload
- persisting the refreshed result when it is valid and allowed to persist
- returning a user-facing result message for the debugger

### Diagnostics snapshot store

[`VisibleStorySurfaceDiagnosticsStore`](../NativeUI/Helpers/VisibleStorySurfaceDiagnosticsStore.cs)
holds the latest in-memory snapshot per supported surface.

Each snapshot records:

- the story surface kind
- provenance
- effective DB table name
- original payload
- translated payload
- runtime-only dialogue-context usage
- effective translation engine id
- last explicit retranslation result

This store is runtime-only. It is not persisted.

### Surface metadata helpers

These helpers keep surface-specific labels and routing out of the handlers:

- [`VisibleStorySurfaceKind`](../NativeUI/Helpers/VisibleStorySurfaceKind.cs)
- [`VisibleStorySurfaceProvenanceKind`](../NativeUI/Helpers/VisibleStorySurfaceProvenanceKind.cs)
- [`VisibleStorySurfaceTableMap`](../NativeUI/Helpers/VisibleStorySurfaceTableMap.cs)
- [`VisibleStorySurfaceText`](../NativeUI/Helpers/VisibleStorySurfaceText.cs)

If a new supported story surface is added later, update all four.

### Retranslation dispatcher

[`VisibleStorySurfaceRetranslationDispatcher`](../PluginUI/Helpers/VisibleStorySurfaceRetranslationDispatcher.cs)
walks the registered addon handlers and sends the explicit retranslation request
to the first applicable visible story surface.

This keeps `/eglotranslatordebugger` decoupled from individual handler types.

## Shared Read-Only UI Layer

The reusable read-only display layer lives under
[`DBManagerUI/Components`](../DBManagerUI/Components).

Shared components:

- [`InspectionCell`](../DBManagerUI/Components/InspectionCell.cs)
- [`InspectionRow`](../DBManagerUI/Components/InspectionRow.cs)
- [`InspectionColumnDefinition`](../DBManagerUI/Components/InspectionColumnDefinition.cs)
- [`InspectionCellFormatter`](../DBManagerUI/Components/InspectionCellFormatter.cs)
- [`InspectionTableView`](../DBManagerUI/Components/InspectionTableView.cs)

Usage split:

- `/eglodbmanager` still owns EF metadata, paging, selection, editing, export,
  and deletion
- `/eglotranslatordebugger` only builds rows and columns for display

This was intentional. The table renderer is generic enough for read-only
inspection, but the data providers remain context-specific.

## Debugger Flow

[`TranslatorMetricsWindow`](../PluginUI/TranslatorMetricsWindow.cs) now shows a
`Latest Visible Story Surface` section.

That section:

- reads the latest snapshot from `VisibleStorySurfaceDiagnosticsStore`
- renders the snapshot through `InspectionTableView`
- exposes the explicit `Retranslate Visible Story Text And Persist` action
- exposes `View In DB Manager`

The debugger-specific row builder lives in
[`VisibleStorySurfaceInspectionModelBuilder`](../PluginUI/Helpers/VisibleStorySurfaceInspectionModelBuilder.cs).

## DB Manager Handoff

[`DbEditorWindow`](../DBManagerUI/DBEditorWindow.cs) exposes
`OpenAndSelectTable(string tableName)`.

The debugger uses that handoff to:

- open `/eglodbmanager`
- resolve the matching table name
- select the table without reimplementing DB manager navigation logic

The table-name matching helper lives in
[`DbTableNameMatcher`](../DBManagerUI/Services/DbTableNameMatcher.cs).

## Provenance Rules

Current provenance values are:

- `DB reuse`
- `Fresh live translation`
- `Fresh live translation (runtime-only dialogue context)`

Meaning:

- `DB reuse`: the visible translated result came from an existing persisted row
- `Fresh live translation`: the visible translated result came from a fresh live
  translation and is safe to persist
- `Fresh live translation (runtime-only dialogue context)`: the live result used
  bounded runtime-only dialogue context and must not become canonical persisted
  history unless the handler explicitly reruns a persistence-safe path

Do not collapse the runtime-only dialogue-context case into normal persistence.

## Rules for Adding Another Story Surface

When extending this flow to another player-visible story surface:

1. Add the surface to `VisibleStorySurfaceKind`.
2. Add its localized display name in `VisibleStorySurfaceText`.
3. Map it to the correct DB table in `VisibleStorySurfaceTableMap`.
4. Implement `IVisibleDialogueRetranslationHandler` on the handler.
5. Record snapshots when the visible result comes from:
   - a stored DB row
   - a fresh live translation
   - a runtime-only dialogue-context live translation, if that surface really
     uses dialogue context
6. Clear the surface snapshot on hide/reset/finalize.
7. Reuse `Resources.*` for every new user-facing string.
8. Add focused tests for the new routing or snapshot behavior.

## Persistence Rules

Keep these semantics unchanged:

- DB remains the source of truth for persisted rows
- runtime-only dialogue context may improve live translation quality, but it
  should not silently redefine canonical persisted text
- the explicit visible retranslation path should persist only usable translated
  results
- the debugger should report persistence failure separately from live refresh

## Validation Checklist

When touching this area, validate at least:

1. `dotnet build Echoglossian.sln -c Debug --no-restore`
2. `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
3. In game, for each touched surface:
   - the latest snapshot appears in `/eglotranslatordebugger`
   - explicit retranslation reports the correct outcome
   - `View In DB Manager` opens the expected table
   - later lookups show `DB reuse` when the persisted row is reused
