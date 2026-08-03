# Plugin Runtime Logging

## Rule

All Echoglossian-owned runtime logging must go through `PluginRuntimeLog`.

Do not add direct `IPluginLog`, `PluginLog`, `Serilog.Log`, `Console`, or ad hoc file logging in plugin code.

## Why

- keeps Dalamud logging behavior consistent
- gives us one choke point for debug gating and formatting
- mirrors the same lines into an Echoglossian-exclusive runtime log file
- makes log analysis possible without repeatedly filtering `dalamud.log`

## Exclusive runtime log file

`PluginRuntimeLog` writes to Dalamud as before and also mirrors every line to:

`<PluginConfigDirectory>\Echoglossian.log`

When that file reaches the line cap, the current active file is archived with a
timestamp added to its name and logging continues in a fresh active file. For
example:

`<PluginConfigDirectory>\Echoglossian-20260725-221530-123.log`

The accepted-quest diagnostic dump files use the same rotation policy, such as:

- `accepted-quest-prefetch-activity.log`
- `accepted-quest-prefetch-canonical.log`

Those accepted-quest files intentionally remain the verbose source of truth for
prefetch activity. The main `Echoglossian.log` should stay higher-signal and
only mirror a narrow subset of prefetch phases such as queued requests and
failures.

`AcceptedQuestPrefetchRuntime` now does only lightweight capture and queueing on
the hot `Framework.Update` path. The expensive DB and translation work runs in
serialized background work items, so the main runtime log should continue to
favor queue, cache, and failure boundaries instead of per-frame chatter.

## Practical guidance

- prefer scoped messages through `PluginRuntimeLog.Debug("Scope", "...")`
- keep hot-path logging temporary and remove or silence it after investigation
- when adding probes or runtime tracing, reuse `PluginRuntimeLog` instead of creating another sink
- use `DiagnosticFileEmitter` only for purpose-built structured dumps, not for ordinary runtime log lines

## Temporary diagnostic commands

### `/eglotooltipregisterlogging`

`/eglotooltipregisterlogging` is a `DEBUG`-only command that temporarily logs:

- hover-tooltip register/update/remove events
- hover-enter and hover-exit transitions
- anchor kind and bounds
- popup-body geometry decisions for hover-tooltip surfaces

The command writes through `PluginRuntimeLog`, so the output lands in both
`Echoglossian.log` and the mirrored Dalamud log. Its intended use is short
surface-scoped sessions such as:

```text
/eglotooltipregisterlogging JournalAccept 90s
/eglotooltipregisterlogging JournalResult
/eglotooltipregisterlogging all 10m
/eglotooltipregisterlogging stop
```

Prefer this command for hover-tooltip investigation before adding fresh ad hoc
logs.

## Overlay publication diagnostics

Anchored overlay publication and synchronization diagnostics are routed through
`OverlayPublicationDiagnostics`.

Current behavior:

- emissions are deduplicated
- `NamePlateOverlayDiag` and `TooltipAddonOverlayDiag` are scope-disabled by
  default in code
- those scopes should stay off outside focused `DEBUG` investigations

This keeps dedicated overlay runtimes quiet by default while preserving one
shared diagnostic path when an overlay visibility regression needs focused
instrumentation.

## Debug workflow

- rebuild the plugin
- reproduce the issue in game
- inspect `Echoglossian.log` first
- fall back to `dalamud.log` only when cross-plugin context is needed
