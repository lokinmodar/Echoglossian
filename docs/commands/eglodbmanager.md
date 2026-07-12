# `/eglodbmanager`

## Purpose

`/eglodbmanager` opens Echoglossian's database editor window.

It is used to inspect and work with the local SQLite data store that Echoglossian keeps in the plugin config directory.

## Behavior

When the command is executed, the DB editor window is opened.

The command does not run a probe and does not automatically alter data.

## Typical Use

Use this command when you want to:

- inspect saved translations
- check quest plate rows
- validate what is currently stored in the local database
- compare live quest data with persisted data

## Notes

- This command is a UI entry point for database inspection.
- Any destructive or bulk data operation should be treated carefully and only done with a clear understanding of the current schema.
- `/eglotranslatordebugger` can now open this window on the table that owns the
  latest visible supported story surface.

## Related Guide

For the shared debugger handoff and reusable inspection-table flow, see
[`docs/story-surface-debugger-db-manager-guide.md`](../story-surface-debugger-db-manager-guide.md).
