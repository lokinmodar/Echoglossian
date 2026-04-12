# Quest Probe Command

## Purpose

`/egloquestprobe` is a diagnostic command for inspecting the full quest data shape that Echoglossian can currently see.

It is intended to help answer questions such as:

- what Lumina quest row is being resolved for a given quest
- what the live quest text sheet looks like for that quest
- what `QuestPlate` rows already exist in the local database
- what the current live quest progression and todo state look like

The command is meant for debugging and data-model validation. It is not part of the normal translation flow.

## Usage

```text
/egloquestprobe <quest id or quest name>
```

Examples:

```text
/egloquestprobe 662
/egloquestprobe Strange Bedfellows
```

If the command is executed without arguments, it prints a short help message to chat.

## What It Resolves

The command accepts either:

- a numeric quest id
- a quest name that can be resolved through the Lumina quest sheet

The resolver then tries to:

1. find the quest row in Lumina
2. determine the quest id and quest name
3. derive the live quest text sheet path
4. inspect matching `QuestPlate` rows from the local SQLite database
5. resolve live quest progress snapshots
6. resolve live todo snapshots for the same quest

## Output It Produces

The command logs the following categories to `dalamud.log`:

- quest metadata from Lumina
- the raw quest row properties
- every row in the quest text sheet that belongs to the quest
- matching `QuestPlate` database rows
- live quest progression snapshots
- live todo progress snapshots

The intent is to show the full quest shape, not just the currently visible UI text.

## How To Read The Output

The output is useful for checking whether Echoglossian is capturing too little or too much quest data.

Pay attention to:

- `QuestId`
- `QuestName`
- `TranslationLang`
- `TranslationEngine`
- `GameVersion`
- the number of rows in the quest text sheet
- the number of matching `QuestPlate` rows
- whether the live progress snapshot is changing as the quest advances

If the quest text sheet contains more entries than the current UI tooltip or Journal view exposes, that is a strong sign that the UI surface is not the best source of truth for quest content.

## Why This Exists

Echoglossian previously depended heavily on quest UI text and hover state to decide what to translate and what to save.

That worked for simple cases, but it caused problems when:

- the visible quest text changed as the quest advanced
- different quests shared similar UI text
- the same quest needed to be tracked across language, engine, and game version changes

This probe gives a direct way to inspect the quest data sources before deciding whether the quest table or the translation payload should be refactored again.

## Related Systems

- `NativeUI/Helpers/QuestLuminaResolver.cs`
- `NativeUI/Helpers/QuestProgressResolver.cs`
- `NativeUI/Helpers/QuestTodoProgressResolver.cs`
- `NativeUI/Helpers/QuestProbeCommandHelpers.cs`
- `EFCoreSqlite/Models/Journal/QuestPlate.cs`
- `EFCoreSqlite/Migrations/20260409193000_RecreateQuestPlateTable.cs`
- [Quest Sheet Acquisition Pipeline](quest-sheet-acquisition-pipeline.md)

## Notes

- The command is intentionally verbose because it is meant for one-off inspection.
- It should be used when validating quest data shape or database behavior, not during normal gameplay.
- The logs it produces are helpful when deciding whether to keep the current quest table shape or move to a different persistence strategy.
