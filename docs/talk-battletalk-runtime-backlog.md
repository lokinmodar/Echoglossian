# Talk And BattleTalk Runtime Backlog

Date: 2026-05-03

## Status

Future backlog candidate.

For the current live runtime shape before any future experiment work, see
[dialogue-and-toast-runtime-flows.md](dialogue-and-toast-runtime-flows.md).

## Goal

Capture a future investigation track for `Talk` and `BattleTalk` that reduces
direct dependence on live addon text-node reads and presentation mutation,
without removing the current runtime that already works well enough for release
use.

This is not an immediate refactor plan. It is a backlog note for a safer future
experiment path.

## Current State

### Talk

`Talk` already has a stronger source surface than most native UI handlers:

- source capture is primarily driven by `AtkValue*` during `PreRefresh`
- the visible addon is still consulted for native apply, visible-state checks,
  and restoration of text-node presentation

This means `Talk` is already partially less dependent on direct UI reads than
many other handlers, but it still mutates visible text-node presentation when
native translation is enabled.

### BattleTalk

`BattleTalk` is still more UI-first:

- source capture currently comes from the visible addon nodes
- native apply still rewrites the visible text node directly
- native apply now uses the shared
  [NativeTextNodeLayoutHelper.cs](../NativeUI/Helpers/NativeTextNodeLayoutHelper.cs)
  so wrapper heights and the nearest background can follow the resized text
- native apply still changes presentation and geometry such as:
  - `TextFlags`
  - timer-node X offset
  - wrapper and background height

That makes `BattleTalk` more coupled to live node shape, repaint timing, and
layout stability than `Talk`.

## Desired Direction

Investigate whether `Talk` and `BattleTalk` can support alternative runtime
implementations that are less dependent on direct UI reads and less aggressive
about direct native node mutation.

The important constraint is:

- do not remove the current implementation just to explore this
- keep the current runtime available as the default stable path
- make any new path opt-in and easy to disable

## Proposed Experimental Shape

Create parallel implementation tracks that can be toggled internally for
testing.

### Talk

Potential experimental track:

- keep `AtkValue` capture as the source of truth
- minimize or eliminate visible-node reads except where they are strictly
  required for lifecycle gating
- investigate whether native write can rely more on refresh-payload mutation and
  less on direct post-refresh text-node mutation
- reduce or remove presentation mutation if translated text can still fit and
  render acceptably

### BattleTalk

Potential experimental track:

- investigate whether the real canonical source can be recovered from
  `StringArrayData` reliably enough to stop depending on visible-node reads as
  the primary capture surface
- if `StringArrayData` proves too unstable alone, investigate a hybrid path:
  - canonical source from structured runtime data when available
  - visible addon only as a fallback or validation layer
- investigate whether native write can be narrowed so it changes only text, or
  at least uses a symmetrical snapshot-and-restore model for every changed
  property

Near-term non-experimental work that still fits the current runtime:

- keep using the shared late reflow helper for native text replacement
- audit whether the timer node can anchor from existing wrapper geometry instead
  of any handler-specific offset logic
- verify whether any remaining wrapper clipping comes from parent nodes outside
  the currently captured ancestor chain

## Why Parallel Implementations Matter

This work is risky because `Talk` and `BattleTalk` are timing-sensitive and
user-visible. A broad rewrite would make it too easy to regress:

- capture correctness
- overlay timing
- native swap behavior
- restoration correctness
- line wrapping and layout stability

A parallel path allows:

- testing alternative capture/apply strategies in isolation
- comparing source fidelity against the current runtime
- disabling the experiment immediately if it regresses live behavior

## Recommended Gating Strategy

If this is pursued later, prefer internal opt-in switches rather than removing
the current runtime:

- a per-handler experimental flag for `Talk`
- a per-handler experimental flag for `BattleTalk`
- default both to off
- keep the current stable implementation as the default release path until the
  experimental path proves safer

These switches should be treated as implementation toggles for testing, not as
user-facing feature clutter, unless there is a later reason to expose them.

## Minimum Future Investigation Checklist

1. Confirm whether `BattleTalk` really has a usable `StringArrayData`-driven
   source path in the current game build.
2. Document the exact slot ownership and subscriber behavior for the active
   `BattleTalk` runtime data.
3. Verify whether `Talk` can complete native translation purely through refresh
   payload mutation for more cases than today.
4. Audit which native presentation mutations are truly necessary in `Talk` and
   `BattleTalk`, and which are historical convenience hacks.
5. If any presentation mutation remains necessary, make the mutation symmetric
   with explicit snapshot and restore for every changed property.
6. Add temporary instrumentation only behind debug-only paths and remove hot-path
   logging after the experiment is understood.

## Non-Goals

This backlog item does not currently imply:

- re-enabling `ActionDetail` or `ItemDetail`
- broad migration of every talk-like surface to `StringArrayData`
- DB schema changes
- replacing the current stable `Talk` or `BattleTalk` runtime immediately

## Summary

Future work should test a less UI-dependent runtime for:

- `Talk`, building further on its existing `AtkValue` source path
- `BattleTalk`, ideally by proving or disproving a viable `StringArrayData` or
  hybrid canonical source path

That work should happen behind opt-in parallel implementations so the current
runtime remains available while experiments are validated.
