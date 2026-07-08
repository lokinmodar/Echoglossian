# JournalDetail And Quest Native Reflow Handoff

Snapshot date: 2026-07-07

## Scope

This handoff is for the unstable quest-surface work around:

- `#181` text-node flag corruption and native reflow side effects
- `JournalDetail` native body reflow
- `Journal` / `JournalDetail` display-mode correctness
- quest tooltip versus native mutation behavior

This work is intentionally isolated from `v4-series`.

## Branch and PR state

- branch: `issue-181-journaldetail-reflow`
- latest local branch commit:
  - `e9722ff` `fix(journaldetail): restore mode-safe native apply flow`
- open PR:
  - [#193](https://github.com/lokinmodar/Echoglossian/pull/193)
  - title: `fix(#181): continue journal detail native reflow investigation`
  - state: open draft
- divergence versus `v4-series` at snapshot time:
  - `v4-series` unique commits: `2`
  - `issue-181-journaldetail-reflow` unique commits: `9`

Interpretation:

- this branch is ahead with its own experimental work
- it is also behind `v4-series`
- sync it before resuming meaningful implementation

## Why this branch exists

`JournalDetail` native reflow is behavior-sensitive enough that it should not
be worked directly on `v4-series`.

The branch exists to preserve:

- native body-flow experiments
- richer original-layout snapshots
- probe-driven container and wrapper analysis
- mode-restoration work that is not yet safe enough for release

## Branch-only files currently defining this front

- `NativeUI/AddonHandlers/Quest/JournalDetailHandler.cs`
- `NativeUI/AddonHandlers/Quest/JournalHandler.cs`
- `NativeUI/Helpers/NativeTextFlowReflowHelper.cs`
- `NativeUI/Helpers/AddonStructureProbe.cs`
- `docs/journal-journaldetail-surface-fix-iteration-log.md`
- `docs/commands/egloaddonprobe.md`

## What is already known and should not be re-learned

### Mode contract is strict

The three Journal-family modes must stay separated:

- `NativeUiTranslation`
  - translated native UI
  - no hover tooltip
- `TooltipTranslation`
  - original native UI
  - translated tooltip only when the translated payload is ready
- `NativeUiTranslationWithOriginalTooltips`
  - translated native UI
  - original tooltip only when the translated/native payload is ready

If the selected mode does not mutate native UI, do not touch native nodes.

### JournalDetail is the most sensitive quest surface

The repo already treats `JournalDetail` as the highest-risk quest surface.
Dense translated bodies need container-aware flow reflow, not just text-node
resizing.

### Reflow target selection matters

For `JournalDetail`, a healthy native layout generally keeps outer viewport
containers fixed and grows only the internal scroll/body flow. If the outer
body viewport or root clipped nodes start growing, the wrong container is being
reflowed.

### Original-state capture must be scope-safe

Mode switches and repeated repaints need a stable per-scope original snapshot
that includes:

- original texts
- text flags and font sizes
- ordered body-flow blocks
- container heights
- summary and supplemental summary nodes

### The quest pipeline should remain canonical-first

Quest handlers should identify quest and live progress, then consume canonical
or DB-backed quest payloads. UI text is fallback material, not canonical truth,
when structured quest data is already available.

## Canonical docs to read before editing

Start with these, in this order:

1. [docs/quest-addon-translation-runtime-flow.md](../quest-addon-translation-runtime-flow.md)
2. [docs/journal-quest-data-model-and-flow.md](../journal-quest-data-model-and-flow.md)
3. [docs/quest-addon-detailed-flow-and-remediation-plan.md](../quest-addon-detailed-flow-and-remediation-plan.md)
4. [docs/github-issue-backlog.md](../github-issue-backlog.md)
5. [docs/commands/egloaddonprobe.md](../commands/egloaddonprobe.md)

Then read branch-only material directly from the branch:

- `docs/journal-journaldetail-surface-fix-iteration-log.md`

## What the branch currently changes

At a high level, this branch adds:

- a shared `NativeTextFlowReflowHelper`
- richer `JournalDetail` original snapshot capture
- per-scope mode-safe native apply and restore behavior
- stronger addon-probe output for layout inspection

The point is not “make text bigger”. The point is:

- restore correctly
- grow only the correct flow containers
- avoid leaking mutated geometry across mode switches and repaints

## Known unresolved concerns

- the branch is stale relative to `v4-series`
- `JournalDetail` native mode still needs in-game validation for long body
  translations and summary-heavy quests
- tooltip/original pairing must remain correct in swap mode
- any fix must avoid reintroducing the older bug where read-only or tooltip
  modes touched native text/flags they never mutated

## Recommended resume steps

1. Switch to `issue-181-journaldetail-reflow`.
2. Sync it with `v4-series` before new edits.
3. Read the docs listed above plus PR `#193`.
4. Run targeted probing on `Journal` and `JournalDetail`.
5. Validate all three display modes in-game before broadening scope.
6. Keep the iteration log updated on every meaningful pass.

## Probe and validation guidance

Useful commands:

- `/egloaddonprobe JournalDetail`
- `/egloaddonprobe Journal`
- `/egloaddonprobe _ToDoList 0`

Required validation after code changes:

- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`

In-game checks:

- `JournalDetail` native mode with long quest body
- `TooltipTranslation` restores the original native UI and shows translated
  tooltip only when ready
- `NativeUiTranslationWithOriginalTooltips` shows translated native UI and the
  correct original tooltip
- opening, closing, and mode switching do not leave mutated layout behind

## Merge rule

Do not merge `#193` into `v4-series` until:

- layout is stable in-game
- tooltip behavior is stable in-game
- no native text is touched outside the modes that truly mutate it
