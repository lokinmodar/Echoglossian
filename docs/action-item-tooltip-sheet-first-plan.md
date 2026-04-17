<!--
Copyright (c) lokinmodar. All rights reserved.
Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
-->

# Action and Item Tooltip Sheet-First Plan

## Goal

Define a canonical DB-first source for `ActionTooltip` and `ItemTooltip` so
tooltip rendering can become a pure apply step instead of a hot-path
translation workflow.

## Canonical Sources

### Actions

- `Action.Name` comes from the `Action` sheet.
- `Action.Description` comes from `ActionTransient.Description`.
- `Action.Icon` comes from the `Action` sheet.
- `Action.ClassJob` and `Action.ClassJobCategory` are used to determine whether
  the action belongs to the current class/job.

This means the canonical action tooltip source is:

- `Action`
- `ActionTransient`

and not the text currently shown in the live tooltip.

### Items

- `Item.Name` comes from the `Item` sheet.
- `Item.Description` comes from the `Item` sheet.
- `Item.Icon` comes from the `Item` sheet.
- `ItemAction`, `ItemUICategory`, and `ClassJobCategory` also come from the
  `Item` sheet and are preserved in the canonical payload.

This means the canonical item tooltip source is:

- `Item`

and not the text currently shown in the live tooltip.

## Prefetch Scope

### Actions

The prefetch runtime resolves the current class/job from `PlayerState` and then
prefetches every player action that matches either:

- the action's explicit `ClassJob`
- or the action's `ClassJobCategory`

for the current class/job.

### Items

The prefetch runtime resolves item ids from:

- inventory bags
- equipped gear
- armory containers
- hotbars

and then materializes canonical item payloads from the `Item` sheet.

## Persistence Contract

The old tooltip entities were shallow and are now replaced by canonical rows.

### ActionTooltip

The canonical row now stores:

- action identity
- class/job identity
- icon and category metadata
- original name
- original description
- assembled original tooltip text
- translated name
- translated description
- assembled translated tooltip text
- stable source-content hash
- serialized canonical payload

### ItemTooltip

The canonical row now stores:

- item identity
- icon and item metadata
- original name
- original description
- assembled original tooltip text
- translated name
- translated description
- assembled translated tooltip text
- stable source-content hash
- serialized canonical payload

## Runtime Shape

The new prefetch flow is:

1. detect tracked action/item ids
2. resolve canonical payload from sheets
3. persist original canonical row
4. translate only missing fields through the shared broker
5. persist translated canonical payload back into the same DB row

The tooltip addon apply step now does this:

1. identify the current action or item id
2. read the canonical row from the DB
3. require a complete translated canonical payload before touching the live tooltip
4. apply the configured mode from stored translated/original payloads

The current live runtime is DB-first:

- if the translated canonical payload is missing or incomplete, the live
  tooltip stays untouched
- in native-only mode, the tooltip name/description nodes are rewritten from
  the translated payload
- in ImGui mode, native tooltip text is restored/or left untouched and the
  translated tooltip is rendered in Echoglossian overlay
- in swap mode, native tooltip text is rewritten to translated content while
  the overlay shows the canonical original payload

## Presentation Mode Rule

The future live tooltip apply path must follow this rule:

- in native-only mode, the native tooltip may be rewritten with translated text
- in ImGui mode, the native tooltip should remain untouched and Echoglossian
  should render the translated payload in overlay form
- in swap mode, the native tooltip should show the translated payload and
  Echoglossian should render the original payload in overlay form

This is intentionally different from the `StringArrayData` surfaces, which
should prefer Echoglossian hover tooltips per translated text when operating in
ImGui or swap presentation modes.

## Important Constraint

Tooltip UI code should not become the source of truth for tooltip semantics.

The source of truth is now:

- `Action + ActionTransient` for actions
- `Item` for items
- DB persistence for translated payloads

The live tooltip should be treated only as the final presentation surface.
