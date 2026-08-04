# `/eglotooltipregisterlogging`

## Purpose

`/eglotooltipregisterlogging` temporarily logs hover-tooltip registrations,
hover-enter transitions, hover-exit transitions, and popup-body geometry
decisions for hover-tooltip surfaces.

It is intended for short focused investigations when a hover-triggered tooltip
is not registering, is anchored to the wrong node, or behaves erratically while
the mouse moves across the addon.

## Availability

- This command exists only in `DEBUG` builds.
- It is a diagnostic command, not a normal user-facing feature.

## Usage

```text
/eglotooltipregisterlogging <surface|all> [duration]
/eglotooltipregisterlogging stop
/eglotooltipregisterlogging cancel
```

Examples:

```text
/eglotooltipregisterlogging JournalAccept
/eglotooltipregisterlogging JournalResult 90s
/eglotooltipregisterlogging all 10m
/eglotooltipregisterlogging stop
```

## Behavior

When started, the command opens one temporary logging session:

- default duration: `60s`
- maximum duration: `30m`
- optional duration suffixes:
  - `<n>s`
  - `<n>m`

The `surface` token is matched against the surface prefix embedded in the
registration key. `all` disables surface filtering for the active session.

During an active session, the runtime logs:

- tooltip target register/update/remove events
- hover-enter and hover-exit transitions
- anchor kind (`text node`, `res node`, `addon root`, `explicit bounds`)
- top-left / bottom-right corners and derived size
- whether the payload is translated or original-swap text
- popup-body geometry decisions before a final anchor is chosen

The command writes through `PluginRuntimeLog`, so the lines appear in both
`Echoglossian.log` and the Dalamud log mirror.

## Typical Use

Use this command when you want to:

- confirm which surface actually triggered a plugin hover tooltip
- confirm whether the runtime anchored to a text node, a larger res node, or
  explicit bounds
- compare title and body hover hit areas for quest popups
- capture hover transitions while switching translation modes

## Notes

- This command is for hover-tooltip surfaces only.
- It does not enable the separate anchored-overlay publication diagnostics used
  by dedicated `Tooltip` addon overlays or overlay-only `NamePlate`
  presentation.
- Prefer short sessions and narrow surface filters so `Echoglossian.log`
  remains readable.
