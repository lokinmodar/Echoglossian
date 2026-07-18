# `/egloaddonprobe`

## Purpose

`/egloaddonprobe` starts a recursive probe of a live addon and writes the result to the Dalamud log.

It is the main diagnostic command for understanding addon tree structure, node layout, component roots, and likely overlay anchors.

## Usage

```text
/egloaddonprobe <addon name> [index]
/egloaddonprobe <addon name> [index] [duration]
/egloaddonprobe stop
```

Examples:

```text
/egloaddonprobe JournalDetail
/egloaddonprobe _ToDoList 0
/egloaddonprobe _BattleTalk 15m
/egloaddonprobe _BattleTalk 0 900s
/egloaddonprobe stop
```

## Behavior

When started, the probe watches the requested addon for a short period and logs:

- addon lifecycle events
- live node structure
- local and screen positions for each node
- node sizes, effective sizes, and screen-space bounds
- component roots
- likely text nodes and anchor candidates
- text-node `TextId` values when the live node is still sheet-backed
- matching `StringArrayData` subscriptions for the probed addon's runtime id
- raw subscriber ids plus best-effort addon-name resolution for those arrays

When `stop` or `cancel` is passed, the active watch is stopped if one exists.
In `DEBUG` builds, the plugin can also auto-start managed probe watches for
`_BattleTalk` and `_MiniTalk` once per login session so their earliest hidden
state can be captured before the in-game chat is available. This path is gated
by the hidden config flag `EnableDebugLoginAddonProbe`, which defaults to
`false`. `/egloaddonprobe stop` stops both the manual watch and any auto-started
managed watches for the current login session.

The optional `duration` token accepts:

- `<n>s` for seconds
- `<n>m` for minutes

Examples:

- `90s`
- `10m`
- `15m`

The current maximum watch duration is `30m`.

## Typical Use

Use this command when you want to:

- understand why a tooltip or overlay trigger is missing
- inspect the real addon structure instead of guessing from behavior
- confirm which node should be used as an anchor
- confirm whether a shared `StringArrayData` surface is currently subscribed by
  the addon
- watch a live addon while testing in-game

## Notes

- This command is diagnostic only.
- It is intended to be paired with `dalamud.log` inspection.
- The command is useful when working on dense addons such as Journal, ToDoList, RecommendList, ScenarioTree, and similar UI trees.
- For `JournalDetail`, a healthy native layout usually keeps the outer
  viewport containers fixed and grows only the internal scroll-content flow.
  If `NodeList[0]` or the clipped body container starts growing with the
  translated text, the reflow is likely targeting the wrong container.
