# Tooltip Addon Anchored Overlay Design

## Context

The game addon `Tooltip` currently has three presentation modes in Echoglossian:

- `NativeUiTranslation`
- `TooltipTranslation`
- `Swap`

The current `TooltipTranslation` and `Swap` behavior routes through `HoverTooltipManager`. That path was built for surfaces where Echoglossian registers a hover target over game UI, not for the game's own visible tooltip addon. For `Tooltip`, this causes the wrong abstraction to own anchoring, visibility, layout, and diagnostics. The result is erratic publication, weak restore behavior on mode switches, and poor sizing for texture-backed languages such as Arabic.

At the same time, the dedicated `TooltipTextCacheManager` must remain the single fast path for persisted `Tooltip` rows across all display modes. Its current preload query is broken and must be corrected as part of this work.

## Goals

- Keep `NativeUiTranslation` working through the existing native `Tooltip` mutation path.
- Replace `TooltipTranslation` and `Swap` presentation for addon `Tooltip` with an overlay anchored directly to the visible `Tooltip` addon.
- Keep one shared data pipeline for `Tooltip` capture, cache lookup, DB fallback, translation queueing, and persistence.
- Make overlay sizing follow the native tooltip geometry instead of the generic hover-tooltip sizing path.
- Support texture-backed and RTL languages through the same anchored overlay backend.
- Provide a dedicated option to hide the native tooltip while the anchored overlay is active.
- Keep the patch narrow and avoid broad refactors in unrelated tooltip systems.

## Non-Goals

- Replacing `ActionDetail`, `ItemDetail`, or any other tooltip surface.
- Generalizing a new cross-repo overlay framework before fixing addon `Tooltip`.
- Changing `TooltipText` persistence semantics or introducing a second cache for `Tooltip`.
- Reworking the existing `HoverTooltipManager` for unrelated surfaces.

## Approved Decisions

- `NativeUiTranslation` remains native-only.
- `TooltipTranslation` and `Swap` move off `HoverTooltipManager`.
- The target surface is only the game addon `Tooltip`.
- All modes continue using the dedicated `TooltipTextCacheManager` and the same `TooltipText` data source.
- The anchored overlay backend may reuse internal mechanics already proven elsewhere in the repo, but it must remain functionally specific to addon `Tooltip`.

## Architecture

### Shared data pipeline

The addon `Tooltip` keeps one source-of-truth pipeline:

1. Capture and normalize the visible/canonical tooltip payload.
2. Lookup in `TooltipTextCacheManager`.
3. Fallback to SQLite when the in-memory cache misses.
4. Queue translation asynchronously when no reusable translation exists.
5. Persist the resolved translation back into `TooltipText`.
6. Update `TooltipTextCacheManager` with the resolved row.

This pipeline is shared across all three display modes. Presentation mode selection must not fork capture, cache, DB, or translation queue behavior.

### Presentation backends

Two presentation backends exist for addon `Tooltip`:

- Native backend
  - Used only by `NativeUiTranslation`.
  - Continues to rely on `TooltipHandler` native text application and native restore behavior.

- Anchored overlay backend
  - Used by `TooltipTranslation` and `Swap`.
  - Anchors to the visible bounds of the `Tooltip` addon itself.
  - Publishes Echoglossian-owned overlay content at the same screen location as the native tooltip.
  - Optionally hides the native tooltip while the overlay is active.

The runtime selects the backend from the effective display mode. `TooltipTranslation` and `Swap` must no longer register hover targets through `HoverTooltipManager`.

## Mode Behavior

### NativeUiTranslation

- Uses the shared `Tooltip` data pipeline.
- Applies translated content directly to the native `Tooltip` addon.
- Publishes no anchored overlay.
- Ignores the overlay-only hide-native-tooltip setting.

### TooltipTranslation

- Uses the shared `Tooltip` data pipeline.
- Leaves native tooltip text untouched.
- Publishes an anchored overlay that shows translated content.
- Uses the visible `Tooltip` addon bounds as the overlay anchor.
- Hides the native tooltip only when the dedicated setting is enabled and the overlay content is fully ready to publish.
- Restores native tooltip visibility immediately when the overlay clears or the mode changes.

### Swap

- Uses the shared `Tooltip` data pipeline.
- Applies translated content to the native `Tooltip` addon.
- Publishes an anchored overlay showing the original content.
- Uses the same hide-native-tooltip behavior as `TooltipTranslation`.
- Restores native tooltip visibility immediately when the overlay clears or the mode changes.

## Anchored Overlay Behavior

### Anchor source

The overlay anchor is the live `Tooltip` addon rectangle, not a separately registered hover rectangle. The runtime must read the visible tooltip addon bounds each update and publish the overlay against that geometry.

### Publication rules

The overlay publishes only when all of the following are true:

- the `Tooltip` addon is visible and ready;
- the runtime has a usable original payload;
- the runtime has the correct display-mode target text for the current mode;
- the overlay bounds are valid.

If any prerequisite fails, the overlay clears and any hidden native tooltip is restored immediately.

### Restore rules

On addon hide, mode change, language change, content identity change, or runtime reset:

- clear the anchored overlay;
- restore native tooltip visibility if this runtime hid it;
- restore native text only if the active path had previously mutated native text.

This preserves the existing rule that native state should not be restored unless that path actually mutated it.

## Overlay Sizing and Rendering

The anchored overlay must stop depending on the generic hover-tooltip sizing path.

### Sizing policy

- Base overlay width and height on the live `Tooltip` addon bounds.
- Derive text scale from the native tooltip geometry first.
- Treat user font-scale configuration as a multiplier on top of the derived native size, not as the primary size source.
- Reuse the last valid native-derived metrics when one frame cannot resolve stable metrics.
- Fall back to configured defaults only when no valid native-derived metrics are available.

This keeps texture-backed overlay text visually aligned with the native tooltip instead of appearing oversized.

### Rendering policy

- Non-texture languages render through the normal overlay text path.
- Texture-backed and RTL languages render through the existing texture presentation path.
- Background, padding, opacity, and line-height controls remain available in Echoglossian configuration.
- The renderer choice must not affect anchoring, visibility, or cache behavior.

## Configuration

The addon `Tooltip` overlay needs dedicated configuration instead of reusing `HoverTooltip*` settings as its only control surface.

Required configuration additions:

- `TooltipAddonHideNativeTooltipWhenOverlayActive`
- `TooltipAddonOverlayTextColor`
- `TooltipAddonOverlayBackgroundColor`
- `TooltipAddonOverlayBackgroundOpacity`
- `TooltipAddonOverlayPadding`
- `TooltipAddonOverlayLineHeightScale`
- `TooltipAddonOverlayFontScaleAdjustment`
- `TooltipAddonOverlayMaxWidthMode`

Behavior rules:

- These settings apply only to the anchored overlay backend for addon `Tooltip`.
- `NativeUiTranslation` ignores them except where a shared renderer utility strictly requires defaults.
- Existing `HoverTooltip*` settings remain scoped to hover-tooltip surfaces that still use `HoverTooltipManager`.

`TooltipAddonOverlayMaxWidthMode` defaults to `MatchNative`. A manual fallback mode may exist, but native-matched sizing is the primary design target.

## Diagnostics

Runtime diagnostics for addon `Tooltip` must move away from hover-registration logging and toward explicit overlay-runtime state logging.

When diagnostics are enabled, logs should report:

- active display mode;
- selected backend (`native` or `anchored-overlay`);
- cache hit, DB hit, or queued translation;
- overlay publish, clear, suppress, and restore decisions;
- native tooltip visibility before and after overlay suppression;
- resolved addon bounds;
- renderer type (`text` or `texture`);
- content identity hashes rather than noisy full payload spam by default.

Logging stays quiet by default and remains opt-in for focused debugging.

## Cache Manager Requirement

`TooltipTextCacheManager` preload failure is part of this design scope because it directly undermines all three display modes.

Required outcome:

- preload succeeds without relying on an EF query that tries to translate `HasSavedTranslation` into SQL;
- cache preload continues to load only rows that have a usable translated payload;
- no second tooltip cache is introduced.

## Testing and Validation

### Automated coverage

Add or update tests for:

- `TooltipTextCacheManager` preload behavior;
- display-mode-to-backend selection;
- overlay publish and clear policy;
- hide-native-tooltip restore behavior;
- overlay sizing fallback when native metrics are temporarily unavailable;
- mode switching between native and overlay backends without stale state leakage.

### In-game validation

Verify:

- `NativeUiTranslation` still behaves exactly as before for addon `Tooltip`;
- `TooltipTranslation` shows a stable anchored overlay for normal languages;
- `TooltipTranslation` shows a stable anchored overlay for Arabic and other texture-backed languages;
- `Swap` shows translated native text and original anchored overlay;
- hiding the native tooltip while overlay is active restores correctly on hover exit, mode switch, and language switch;
- changing target language resets old `Tooltip` presentation state before applying the new language;
- no repeated cache miss or translation spam appears for unchanged tooltip content.

## Risks and Constraints

- The patch must stay inside the correct worktree and branch and must not mix with unrelated dirty files.
- Tooltip lifecycle is repaint-heavy, so the new runtime must avoid frame-by-frame retranslations or repeated expensive layout work.
- The design must not reopen unrelated `Journal`, `NamePlate`, or selection-dialog behavior.
- The overlay backend must preserve correct behavior for overlay-only languages already defined by global policy.

## Implementation Direction

Implementation should introduce a dedicated addon-`Tooltip` anchored overlay runtime alongside the existing native tooltip handler, then route display-mode selection between them while preserving one shared `TooltipText` data pipeline. The implementation must correct `TooltipTextCacheManager` preload first or as part of the same narrow change set so the new backend does not sit on top of a broken cache foundation.
