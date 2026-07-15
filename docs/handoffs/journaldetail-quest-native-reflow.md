# JournalDetail Native Translation Reflow Handoff

Snapshot date: 2026-07-15

## Objective

Complete vertical text reflow only when translated text is written into the
native `JournalDetail` UI.

The target behavior is:

- translated native text wraps without overlap or clipping
- later body blocks move by the cumulative growth of earlier blocks
- the internal body or scroll extent grows enough to contain the final block
- original geometry is restored when the handler leaves a native-writing mode
- no work is repeated every frame once the current layout is already applied

This is not a general quest-handler refactor and not an overlay-sizing task.

## Branch And PR State

- working branch: `issue-181-journaldetail-reflow`
- local branch head: `e9722ff5230cc9a2e42ab7e9d322e5a0874ac4d4`
- remote branch and draft PR head:
  `0476fa2e784dcbef84dd18d0cf5f81d861c919c9`
- local-only commits that must be preserved:
  - `14c0d63` `fix(journal): reset list-node cache on node reuse`
  - `e9722ff` `fix(journaldetail): restore mode-safe native apply flow`
- draft PR:
  [#193](https://github.com/lokinmodar/Echoglossian/pull/193)
- PR base: `v4-series`
- state at this snapshot: open, draft, and conflicting
- divergence before this handoff update:
  - `v4-series`: 163 unique commits
  - local reflow branch: 9 unique commits

The new worktree must start from the local branch, not only
`origin/issue-181-journaldetail-reflow`, or the two local-only commits will be
lost.

## Scope Boundary

In scope:

- `JournalDetail` body layout in modes that write translated text to native
  nodes
- original geometry snapshot and owned restoration
- translated text measurement
- ordered body-block flow
- cumulative vertical delta calculation
- internal body, canvas, or scroll-container growth
- narrow tests for reflow calculations and native mutation ownership
- probe-driven in-game validation of `JournalDetail`

Out of scope:

- `Journal` list behavior, except preserving already-committed behavior during
  the merge
- `Talk`, `BattleTalk`, `MiniTalk`, `ToDoList`, or other quest surfaces
- hover tooltip content, hit areas, or texture-backed rendering
- overlay sizing, RTL texture layout, or ImGui work
- quest lookup, canonical data, translation cache, persistence, or engine work
- broad addon traversal or handler architecture refactors

Tooltip-only behavior is a regression constraint, not an implementation target.

## Why The Front Is Still Open

The original read-only corruption reported by issue #181 is already addressed
in current `v4-series`: handlers track native mutation ownership and do not
restore or rewrite native nodes in read-only or tooltip-only paths.

The residual work is different and narrower: when a verbose translation is
intentionally applied to native `JournalDetail` nodes, the body still needs a
real flow layout instead of independent `SetText(...)` and node resizing calls.

Do not reintroduce the solved read-only bug while completing the native reflow.

## Current `v4-series` Baseline To Preserve

Current `v4-series` includes later `JournalDetail` work that is not present in
the old draft branch:

- `1ec9780` introduced the newer RTL and native-runtime baseline
- `563dd87` scoped quest source identity per operation
- `bda68c0` restored Character hovers and preserved `JournalDetail` formatting
- current handler lifecycle uses explicit mode helpers and
  `ownsJournalDetailNativeMutation`
- restoration occurs only when the handler owns a prior native mutation
- tooltip-only paths keep native text and flags untouched
- current quest source-scope and lifecycle tests must continue to pass

Treat the current `v4-series` `JournalDetailHandler` as the lifecycle baseline.
Do not resolve the merge by accepting the stale handler wholesale.

## Branch Work Worth Recovering

The branch contains experimental material that should be reviewed and ported
selectively:

- `NativeUI/Helpers/NativeTextFlowReflowHelper.cs`
- richer original text and geometry snapshots
- body-block ordering and section resolution experiments
- internal container growth experiments
- enhanced addon probe output
- the local mode-safe native apply correction in `e9722ff`

The helper is experimental, not an accepted abstraction. Keep it only if it can
be reduced to the narrow `JournalDetail` need and covered by deterministic
tests. Do not generalize it to other addons during this front.

## Required Branch Update

Use a merge, not a rebase or force-push, because the branch already has an open
shared PR and prior merge history.

1. Start from local `issue-181-journaldetail-reflow` at `e9722ff`.
2. Fetch `origin`.
3. Merge `origin/v4-series` into the issue branch.
4. Resolve `JournalDetailHandler.cs` using current `v4-series` lifecycle and
   source-scoping behavior as the baseline, then port only the native reflow
   behavior that is still required.
5. Resolve `Echoglossian.xml` from the current `v4-series` side first; regenerate
   it through the validated build after resolving source files.
6. Preserve the already-committed `JournalHandler` node-reuse guard without
   expanding its scope.
7. Build, test, commit the merge resolution, and push the issue branch to update
   PR #193.

A merge-tree simulation predicts content conflicts in:

- `NativeUI/AddonHandlers/Quest/JournalDetailHandler.cs`
- `Echoglossian.xml`

`JournalHandler.cs` auto-merges in the simulation but still requires review.

## Reflow Model

Capture one layout scope for the visible `JournalDetail` body. For every mutable
block, preserve:

- node identity and expected parent
- original text
- original X and Y
- original width and height
- original text flags and font size
- original container heights required for restoration

Apply native translated layout in this order:

1. Restore the block to its original geometry before measuring a new payload.
2. Apply the translated text with the required multiline and wrapping flags.
3. Measure the resulting text-node height.
4. Compute `deltaHeight = translatedHeight - originalHeight`.
5. Move every later block in the same body flow by the cumulative prior delta.
6. Grow only the internal body, canvas, or scroll extent needed to contain the
   final block plus original bottom padding.
7. Mark the scope as applied so identical repaint events short-circuit.

Do not grow the outer viewport, root addon geometry, or unrelated controls.

## Lifecycle Contract

`NativeUiTranslation`:

- apply translated native text and reflow
- do not add hover behavior as part of this work

`NativeUiTranslationWithOriginalTooltips`:

- apply the same translated native reflow
- preserve existing original-tooltip behavior unchanged

`TooltipTranslation`:

- do not mutate text, flags, node geometry, or containers
- do not run a restore unless this handler owns a previous native mutation

On addon reuse, scope change, mode change, hide, or finalize:

- restore text and geometry only when the handler owns a native mutation
- clear the applied-layout key and per-scope geometry snapshot at the correct
  lifecycle boundary
- never restore stale geometry into a newly reused addon scope

## Performance Constraints

- no full node-tree traversal every frame after the layout is resolved
- no database access or translation request from the reflow helper
- no repeated text measurement for an unchanged translated payload and scope
- no unbounded snapshot growth across quest changes
- no hot-path debug logging after investigation
- use the existing addon probe rather than permanent diagnostic output

The reflow cache key must distinguish at least addon scope and applied text or
payload identity. A changed quest or translated body must invalidate the prior
layout even if node addresses are reused.

## Tests To Preserve And Extend

Preserve the current coverage in:

- `Echoglossian.Tests/QuestAddonHandlerLifecycleTests.cs`
- `Echoglossian.Tests/QuestOperationSourceScopeTests.cs`
- `Echoglossian.Tests/NativeRuntimeSourceScopeTests.cs`
- `Echoglossian.Tests/QuestAddonOriginalTextHelperTests.cs`

Add deterministic tests for logic that does not require a live addon:

- cumulative block offsets for zero, positive, and mixed deltas
- container growth from the final block and original bottom padding
- idempotence for the same scope and payload
- invalidation when the scope or translated payload changes
- restoration action only when native mutation ownership is true
- tooltip-only mode never selecting native apply or geometry mutation

Required repository validation:

```powershell
dotnet build Echoglossian.sln -c Debug --no-restore
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
```

Commit `Echoglossian.xml` when regenerated by the validated source change.

## In-Game Acceptance

Use:

```text
/egloaddonprobe JournalDetail
/egloaddonprobe stop
```

Validate with a long, verbose translated quest body:

- no text overlap between objective, summary, description, and footer blocks
- no original text leaking under translated text
- no clipping at the internal viewport or scroll extent
- later sections move by the correct cumulative amount
- changing quests does not reuse stale text or geometry
- closing and reopening restores a clean baseline
- switching from native to tooltip-only restores original geometry once
- tooltip-only mode does not modify native text, flags, or layout
- native plus original tooltips keeps its existing tooltip pairing
- stable frame rate while the window remains open and while scrolling

## Completion Rule

The front is complete only when:

- the issue branch is merged with current `v4-series` and pushed
- the PR contains only the narrow native `JournalDetail` reflow plus required
  regression guards
- repository build and tests pass
- all in-game acceptance checks above pass
- the draft PR description is updated with current architecture and evidence
- the PR can be marked ready without carrying unrelated handler or generated
  file regressions

If a different native addon later needs reflow, open a focused issue and branch
from current `v4-series`; do not broaden PR #193.
