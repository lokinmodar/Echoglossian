# MainCommand sheet flow

This document covers the Excel-sheet-backed `MainCommand` canonical text flow.
It is separate from the live `_MainCommand` addon runtime.

## Source schema

- `MainCommand`
  - `Name`
  - `Description`
  - `Icon`
  - `Category`
  - `MainCommandCategory`
  - `Unknown0`
  - `SortID`

## Code paths

- sheet prefetch:
  [ReferenceTextPrefetchRuntime.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/ReferenceTextPrefetchRuntime.cs)
- entity:
  [MainCommandText.cs](/C:/Dante/_dalamud/Echoglossian/EFCoreSqlite/Models/MainCommandText.cs)
- context registration:
  [EchoglossianDBContext.cs](/C:/Dante/_dalamud/Echoglossian/EFCoreSqlite/EchoglossianDBContext.cs)
- cache:
  [ReferenceTextCacheRegistry.cs](/C:/Dante/_dalamud/Echoglossian/Cache/ReferenceTextCacheRegistry.cs)

## Data flow

```text
Lumina MainCommand sheet
  -> collect row ids
  -> build canonical payload
  -> translate Name/Description
  -> persist MainCommandText
  -> preload/update MainCommandTexts cache
  -> reuse by ActionMenu and future consumers
```

## Stored metadata

- `ReferenceId` / `MainCommandId`
- `IconId`
- `CategoryId`
- `MainCommandCategoryId`
- `Unknown0`
- `SortId`
- canonical original/translated payload text

## Purpose

- provide stable reusable source text outside the live addon payload
- support future routing and grouping logic that depends on `MainCommand`
  metadata
- improve lookup quality for `ActionMenu` and future command-like surfaces
