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

## Operation Scope And Queue Ownership

The shared DB-first runtime captures one `TranslationReuseScope` before an
`ActionMenu` payload enters asynchronous translation. It includes source-client
identity, target language, effective engine, and engine-reuse policy. The
captured scope, rather than mutable configuration, governs the translated
payload normalization, canonical fallback lookup, stable-signature diagnostics,
and `GameWindow` persistence after the request begins.

Stable page signatures are owned by that full scope **and** its lifecycle
generation. A failed, rejected, or stale-persistence completion releases only
the signature claimed by its own generation. A target, engine, policy, or
source change starts a new signature set. This prevents a transient failure
from blocking retries, prevents one page's completion from populating another
translation scope, and prevents an old `A -> B -> A` callback from releasing a
new `A` request with the same visible signature.

The failed-payload cooldown is evaluated before a stable signature is claimed.
The tracker also rejects a cooldown-bearing claim defensively. Therefore a
transient provider or persistence failure cannot leave a signature reserved
during the cooldown and permanently suppress the first eligible retry.

## Why `MainCommandText` helps here

`ActionMenu` contains pages whose visible labels are command-like rather than
job-action-like. A sheet-backed `MainCommandText` source gives the runtime a
canonical text store for those labels without forcing the live addon runtime to
change owner.
