# `/eglotranslatordebugger`

`/eglotranslatordebugger` opens Echoglossian's Translator Debugger and Metrics
window.

## What It Shows

- aggregated live translator request counts
- provider and configured model labels per engine
- successes, failures, and short-circuits per engine
- average, max, and last observed live request latency
- last observed failure reason per engine

The window is session-scoped and uses in-memory aggregates only. It does not
create hot-path per-request logs.
