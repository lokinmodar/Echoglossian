# `/eglotranslatordebugger`

`/eglotranslatordebugger` opens Echoglossian's Translator Debugger and Metrics
window.

## What It Shows

- aggregated live translator request counts
- aggregated context-aware live request counts
- provider and configured model labels per engine
- successes, failures, and short-circuits per engine
- average, max, and last observed live request latency
- last observed failure reason per engine
- runtime-only retained dialogue sessions for context-aware translation

The window is session-scoped and uses in-memory aggregates only. It does not
create hot-path per-request logs.

It also exposes a `Clear Dialogue Sessions` action that clears the retained
runtime-only `Talk` / `BattleTalk` history used for context-aware translation.

## Operator Action

The window also exposes an explicit `Retranslate Visible Dialogue And Persist`
button.

Current first-pass scope:

- `Talk`
- `BattleTalk`

Behavior:

- forces a fresh live translation for the currently visible dialogue line
- persists the refreshed result through the dialogue DB path
- prefers the refreshed row on later lookups by making it the newest matching
  dialogue row
- keeps session-aware runtime dialogue context out of the persisted row
