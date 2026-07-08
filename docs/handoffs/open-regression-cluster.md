# Open Regression Cluster Handoff

Snapshot date: 2026-07-07

## Scope

This handoff is for release-facing open issues and the issue-triage front that
should remain anchored on `v4-series`.

## Current tracker snapshot

- repo open-issue count at last check: `27`
- no open issue or comment delta was found after `2026-06-27`
- the most recently updated open issue is still:
  - [#171](https://github.com/lokinmodar/Echoglossian/issues/171)
  - last updated `2026-06-09T09:11:24Z`
- the next most recently updated still includes:
  - [#206](https://github.com/lokinmodar/Echoglossian/issues/206)
  - last updated `2026-06-01T20:40:31Z`

The repo-level backlog snapshot remains the canonical local summary:

- [docs/github-issue-backlog.md](../github-issue-backlog.md)

## Highest-signal still-open regression cluster

These are still the main release-facing items:

- `#206` `{targetLanguage}` prompt-variable regression
- `#207` quest tracker / `ToDoList` / `ScenarioTree` regression on official
  build
- `#203` mixed “not translating” fallout across engines
- `#204` `OpenRouter` translation failure
- `#208` Turkish native-UI character rendering corruption
- `#212` DeepL free-tier `TooManyRequests` behavior
- `#171` mixed provider/runtime and tracker-progression symptoms
- `#172` dynamic objective staleness and cross-quest text mix

## Important already-shipped context

These are not the active front anymore, but future chats should know they were
already pushed forward in the release line:

- `#187` MiniTalk native balloon overflow
- `#188` small dialogue-box overflow
- the toast / `_MiniTalk` / `_BattleTalk` reflow work already has dedicated
  runtime docs and should not be rediscovered from scratch

Relevant references if those surfaces need follow-up later:

- [docs/dialogue-and-toast-runtime-flows.md](../dialogue-and-toast-runtime-flows.md)
- [docs/native-toast-and-minitalk-iteration-log.md](../native-toast-and-minitalk-iteration-log.md)
- [docs/toast-runtime-current-state.md](../toast-runtime-current-state.md)
- [docs/toastgui-runtime-alternative-path.md](../toastgui-runtime-alternative-path.md)

## Related branches worth knowing about

### `issue-207-todolist-regression`

- latest local branch commit:
  - `c5b1d89` `fix(#207): allow partial quest tracker activation`
- divergence versus `v4-series` at snapshot time:
  - `v4-series` unique commits: `26`
  - `issue-207-todolist-regression` unique commits: `1`
- branch diff scope:
  - `NativeUI/AddonHandlers/Quest/ScenarioTreeHandler.cs`
  - `NativeUI/AddonHandlers/Quest/ToDoListHandler.cs`
  - `Echoglossian.xml`

Interpretation:

- this is a narrow old branch around tracker activation
- it is stale relative to current `v4-series`
- treat it as a reference branch, not as safe merge material without review

### `fix-openrouter-translations`

- latest local branch commit:
  - `756d6ca` `fix(open-router): harden prompt expansion`
- branch is fully behind `v4-series` and has no unique diff left

Interpretation:

- this branch exists mostly as historical context
- the prompt-expansion hardening is already in the release branch history

## Canonical docs to read before issue work

1. [docs/github-issue-backlog.md](../github-issue-backlog.md)
2. [docs/translation-surface-support-matrix.md](../translation-surface-support-matrix.md)
3. [docs/quest-addon-translation-runtime-flow.md](../quest-addon-translation-runtime-flow.md)
4. [docs/translation-engines-architecture-and-flows.md](../translation-engines-architecture-and-flows.md)

Then, if the issue is quest-tracker related:

- [docs/journal-quest-data-model-and-flow.md](../journal-quest-data-model-and-flow.md)
- [docs/quest-addon-detailed-flow-and-remediation-plan.md](../quest-addon-detailed-flow-and-remediation-plan.md)

If the issue is dialogue/toast reflow related:

- [docs/dialogue-and-toast-runtime-flows.md](../dialogue-and-toast-runtime-flows.md)

## Recommended workflow for a new chat

1. Start from `v4-series` unless the issue already has a dedicated branch.
2. Re-read the exact GitHub issue and comments before coding.
3. If the work is unstable or issue-sized, create or reuse an issue branch.
4. Keep `v4-series` release-safe.
5. Update `docs/github-issue-backlog.md` when issue status or prioritization
   materially changes.

## Validation

Required after code changes:

- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`

If native UI behavior changes, always include an in-game verification list in
the commit or follow-up notes.
