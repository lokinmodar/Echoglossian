# `/egloaddonprobe`

## Purpose

`/egloaddonprobe` starts a recursive probe of a live addon and writes the result to the Dalamud log.

It is the main diagnostic command for understanding addon tree structure, node layout, component roots, and likely overlay anchors.

## Usage

```text
/egloaddonprobe <addon name> [index]
/egloaddonprobe stop
```

Examples:

```text
/egloaddonprobe JournalDetail
/egloaddonprobe _ToDoList 0
/egloaddonprobe stop
```

## Behavior

When started, the probe watches the requested addon for a short period and logs:

- addon lifecycle events
- live node structure
- component roots
- likely text nodes and anchor candidates

When `stop` or `cancel` is passed, the active watch is stopped if one exists.

## Typical Use

Use this command when you want to:

- understand why a tooltip or overlay trigger is missing
- inspect the real addon structure instead of guessing from behavior
- confirm which node should be used as an anchor
- watch a live addon while testing in-game

## Notes

- This command is diagnostic only.
- It is intended to be paired with `dalamud.log` inspection.
- The command is useful when working on dense addons such as Journal, ToDoList, RecommendList, ScenarioTree, and similar UI trees.
