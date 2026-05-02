---
name: dalamud-addon-probing
description: Probe live FFXIV addons in Echoglossian using `/egloaddonprobe`, inspect reused addon instances such as `ActionDetail` and `ItemDetail`, and interpret probe output from `dalamud.log`.
---

# Dalamud addon probing for Echoglossian

Use this skill when the task is to inspect a live addon tree, capture node layout, confirm likely text nodes or anchors, or compare multiple runtime states of the same addon instance.

## Goal

Use Echoglossian's existing probe workflow instead of adding ad hoc logging to hot UI paths.

## Core workflow

1. Identify the live addon name and index.
2. Start a probe watch with `/egloaddonprobe <addon> [index]`.
3. Reproduce the in-game states that matter.
4. Stop the watch with `/egloaddonprobe stop`.
5. Inspect `dalamud.log` for `[AddonProbe]` entries and correlate them with the runtime bug.

## Key commands

```text
/egloaddonprobe ActionDetail 0
/egloaddonprobe ItemDetail 0
/egloaddonprobe _ToDoList 0
/egloaddonprobe stop
```

## What to look for

- addon lifecycle transitions
- root and component node structure
- candidate text nodes for title, type label, description, and footer text
- `TextId` values when a text node is still sheet-backed
- `StringArrayData` subscriptions tied to the probed addon runtime id
- whether the game is reusing the same addon instance for different visible content

## Reused-addon guidance

`ActionDetail` and `ItemDetail` commonly reuse the same addon pointer across multiple hovers or states. Prefer the probe watch over one-shot dumps so the log captures content changes on the reused instance. When the watch is working correctly, reused-state dumps should appear with `trigger='watch-content-change'`.

## Files to inspect

- [docs/commands/egloaddonprobe.md](docs/commands/egloaddonprobe.md)
- [NativeUI/Helpers/AddonStructureProbe.cs](NativeUI/Helpers/AddonStructureProbe.cs)
- [NativeUI/Helpers/AddonProbeCommandHelpers.cs](NativeUI/Helpers/AddonProbeCommandHelpers.cs)
- [PluginUI/PluginRuntimeUi.cs](PluginUI/PluginRuntimeUi.cs)

## Investigation notes

- Prefer probe output over guessing node offsets from screenshots.
- For dense or unstable addons, compare multiple probe snapshots before changing node-selection logic.
- If a mode or restore bug is suspected, separate probe findings from translation or persistence findings.
- Remove temporary logging after the probe and log review are enough to explain the issue.

## Output style

Summarize:

1. which addon state was probed
2. whether the same instance was reused
3. which nodes are the best candidates for title, type, description, and anchors
4. what the probe implies for the smallest safe code change
