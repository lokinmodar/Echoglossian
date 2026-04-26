# ActionMenu runtime flow

This document covers the live `ActionMenu` addon runtime.

## Runtime owner

- handler:
  [ActionMenuWindowHandler.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/AddonHandlers/ActionMenu/ActionMenuWindowHandler.cs)
- live persistence fallback:
  `GameWindow`
- reusable lookup inputs:
  `ActionTooltip`, `Trait`, `MainCommandText`, and the `*ActionText` families

## Data flow

```text
ActionMenu addon lifecycle
  -> capture current page payload
  -> try cache-first canonical text resolution
  -> try dedicated detail/reference caches
  -> try persisted GameWindow payloads
  -> if unresolved, queue translation
  -> persist current live payload
  -> apply translated/native or hover output
```

## Current lookup layers

1. action/item/trait dedicated caches
2. reference-text cache registry, including `MainCommandText`
3. persisted `GameWindow` payload matching

## Why `MainCommandText` helps here

`ActionMenu` contains pages whose visible labels are command-like rather than
job-action-like. A sheet-backed `MainCommandText` source gives the runtime a
canonical text store for those labels without forcing the live addon runtime to
change owner.
