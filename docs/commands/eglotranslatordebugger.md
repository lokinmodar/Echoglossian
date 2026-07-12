# `/eglotranslatordebugger`

`/eglotranslatordebugger` opens Echoglossian's Translator Debugger and Metrics
window.

## What It Shows

- aggregated live translator request counts
- aggregated context-aware live request counts
- current dialogue-family LLM override routing status
- current OpenAI-family provider variant, endpoint, model, and readiness state
- last OpenAI-family live model refresh outcome and failure detail
- provider and configured model labels per engine
- successes, failures, and short-circuits per engine
- average, max, and last observed live request latency
- last observed failure reason per engine
- runtime-only retained dialogue sessions for context-aware translation

The window is session-scoped and uses in-memory aggregates only. It does not
create hot-path per-request logs.

It also exposes a `Clear Dialogue Sessions` action that clears the retained
runtime-only `Talk` / `BattleTalk` history used for context-aware translation.

It now also shows the latest visible story-surface snapshot for supported
story-facing surfaces and can hand the operator off directly to
`/eglodbmanager`.

When the dialogue-family LLM override is enabled, the window also shows:

- the current primary engine
- the effective engine currently serving dialogue-family surfaces
- whether the override is active or falling back to the primary engine

## Operator Action

The window also exposes an explicit
`Retranslate Visible Story Text And Persist` button.

Current scope:

- `Talk`
- `BattleTalk`
- `TalkSubtitle`
- `CutSceneSelectString`
- `TextGimmickHint`

Behavior:

- forces a fresh live translation for the currently visible story-facing text
- persists the refreshed result through the owning DB path when allowed
- shows the latest visible provenance snapshot in the debugger
- can open the owning table in `/eglodbmanager`
- keeps runtime-only dialogue-context output out of canonical persisted rows

## Related Guide

For the shared debugger-to-DB-manager flow and extension rules, see
[`docs/story-surface-debugger-db-manager-guide.md`](../story-surface-debugger-db-manager-guide.md).
