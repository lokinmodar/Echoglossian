# NamePlate Depth-Aware Overlay Design

Date: 2026-08-02
Branch: `feature/issues-230-233-234`
Worktree: `C:\Dante\_dalamud\Echoglossian\.worktrees\issues-230-233-234`

## Goal

Replace the current screen-space ImGui NamePlate overlay presentation with a reusable world-space depth-aware presentation path that:

- supports progressive fade and scale by distance
- avoids showing full-size overlays for far-away objects
- is structured so future world-space surfaces can reuse it
- preserves the existing NamePlate translation, cache, and prefetch behavior

This design is intentionally limited to presentation. It does not change NamePlate DB semantics, translation queueing, lookup rules, or native-write policy.

## Current State

`NativeUI/AddonHandlers/NamePlates/NamePlateTranslationRuntime.cs` already does the correct high-level translation work:

- captures the source name from the one-frame Dalamud callback
- reuses `NamePlateCacheManager`
- records missing rows for background prefetch
- applies native text when the selected mode allows it
- uses `TranslationDisplayModeHelper` to decide between native, overlay, and swap behavior

The current weakness is presentation:

- NamePlate overlay rendering is screen-space through `TranslationOverlayRenderer`
- the overlay remains a generic ImGui overlay instead of a world-space depth-aware element
- there is no progressive visibility policy by distance
- there is no reusable depth-aware overlay runtime in the repository today

## Design Summary

Keep `NamePlateTranslationRuntime` as the content/orchestration layer and introduce a separate reusable world-space presentation layer.

The first consumer will be NamePlate. The new layer will be designed so later world-space surfaces can reuse it without inheriting NamePlate-specific translation rules.

## Architecture

### 1. `NamePlateTranslationRuntime` remains the orchestrator

This runtime keeps responsibility for:

- deciding whether a given nameplate kind is eligible
- resolving the original text
- reading exact cached rows from `NamePlateCacheManager`
- recording prefetch candidates when no row exists
- deciding whether native text should be written
- deciding whether overlay content should show translated text or original text in swap mode

It will stop being responsible for the low-level screen-space overlay lifecycle.

### 2. Add `DepthAwareWorldOverlayPresenter`

This new runtime will own active world-space overlays keyed by a stable identity such as `gameObjectId`.

Responsibilities:

- upsert active overlay state from the surface runtime
- clear hidden or stale overlays
- resolve object visibility and projection each frame
- apply distance-driven visibility, alpha, and scale
- apply depth-aware presentation configuration on the root visual node

Non-responsibilities:

- translation lookup
- DB access
- background prefetch
- mode selection semantics

### 3. Add `WorldOverlayDistancePolicy`

This will be a pure helper with no game or UI dependencies.

Inputs:

- effective distance
- internal near/mid/far thresholds

Outputs:

- `Visible`
- `Alpha`
- `Scale`

Behavior:

- near range: full alpha and full scale
- intermediate range: progressively reduce alpha and scale
- far range: hide completely

This isolates the user-requested progressive fade/scale behavior from the rendering implementation.

### 4. Add `WorldOverlayDepthProjection`

This helper isolates the sensitive world-to-depth calculation and any reverse-Z handling.

Responsibilities:

- accept the world anchor and the active camera/projection state needed by the implementation
- return either a valid depth result or an invalid result
- keep the math isolated from NamePlate logic

Reason for isolation:

- local references confirm `AtkResNode.SetUseDepthBasedPriority(bool)` exists
- the exact depth math is the unstable part and should not be spread across presentation or translation code

## NamePlate Data Flow

### Cached translation exists

1. `NamePlateTranslationRuntime.HandleNamePlateUpdate(...)` receives the one-frame handlers.
2. The runtime resolves the original text and object position.
3. The runtime reuses `NamePlateCacheManager` to find a matching translated row.
4. Based on the selected display mode:
   - native-only: write translated native text and clear any presenter-managed overlay
   - overlay-only: leave native text untouched and upsert translated overlay content
   - swap: write translated native text and upsert original-text overlay content

### Cached translation does not exist

1. The runtime records a prefetch candidate exactly as it does today.
2. The runtime does not queue translation work inside the one-frame callback.
3. The runtime does not publish placeholder overlay content.
4. Any active overlay for that object is cleared if there is no valid content to show.

## Frame Presentation Flow

For each active overlay entry:

1. Resolve the live game object by stable key.
2. Resolve the world anchor for that object.
3. Project the anchor to screen space.
4. Resolve depth through `WorldOverlayDepthProjection`.
5. Resolve distance-based visibility through `WorldOverlayDistancePolicy`.
6. If projection, depth, or visibility is invalid, hide the overlay.
7. If valid, update the root visual with:
   - screen position
   - depth
   - alpha
   - scale
   - final text payload

## Display Mode Semantics

This design preserves the existing NamePlate mode contract.

- native UI mode:
  - native text shows translation
  - depth-aware overlay is cleared

- overlay-only mode:
  - native text remains original
  - depth-aware overlay shows translated text

- swap mode:
  - native text shows translation
  - depth-aware overlay shows original text

Important boundary:

- the runtime decides content semantics
- the presenter decides presence, placement, scale, alpha, and depth-aware visibility

## Reuse Boundary

The new world-space presentation layer is intentionally generic enough for future surfaces, but the first implementation will only serve NamePlate.

Current planned consumer:

- NamePlate

Not in scope for this change:

- retrofitting other overlays
- introducing a general migration of all overlay surfaces
- adding depth-aware behavior to dense addon UI surfaces such as Journal, Tooltip, ScenarioTree, ToDoList, or RecommendList

## Config Strategy

Phase 1 will not add new user-facing configuration knobs.

Rationale:

- the requested behavior is known and narrow
- the risky part is correctness of the depth-aware presentation path, not end-user customization
- adding tuning controls before stabilizing the base behavior increases surface area without improving the first delivery

Distance thresholds and interpolation values will start as conservative internal constants. If in-game validation shows a real need, configuration exposure can be evaluated later.

Existing NamePlate settings remain in force where they still make sense, such as:

- translation enablement
- display mode
- font scale, if the new presenter still maps it to final text sizing
- background styling, if the new visual path keeps a background concept

## Error Handling And Fallback Rules

Conservative failure rules:

- if the game object is missing or invalid, hide the overlay
- if world-to-screen projection fails, hide the overlay
- if depth calculation fails, hide the overlay
- if distance policy returns not visible, hide the overlay

No automatic fallback to the old ImGui NamePlate overlay in the same frame.

Reason:

- hybrid fallback would make runtime behavior harder to reason about
- it would reintroduce erratic visual switching
- it would blur whether a reported bug belongs to the old or new presentation path

## Testing Strategy

### Pure tests

Add focused tests for the new helpers:

- `WorldOverlayDistancePolicy`
  - near range returns visible with full alpha and scale
  - intermediate range reduces alpha and scale monotonically
  - far range returns not visible

- `WorldOverlayDepthProjection`
  - invalid camera/projection inputs return invalid
  - valid inputs return bounded depth output according to the implementation contract

### Contract tests

Keep and extend contract tests to preserve current runtime guarantees:

- NamePlate live callbacks still do not queue translation work
- NamePlate runtime still guards reentrant callback entry
- NamePlate depth-aware presentation path is no longer coupled to the old generic ImGui renderer for this surface
- mode switches still clear obsolete overlay state correctly

### Existing geometry coverage

Retain and extend the existing NamePlate geometry tests around:

- world anchor selection
- centered bounds/positioning assumptions where they remain applicable

### In-game verification

Required runtime checks after implementation:

- overlays shrink and fade progressively with distance
- far-away objects no longer keep a full-size overlay
- occluded objects do not present the overlay as if fully visible in front
- native-only, overlay-only, and swap remain semantically correct
- no new callback-time translation work is introduced

## Risks

Primary risks:

- incorrect depth math because of reverse-Z or mismatched projection handling
- coupling the new presenter too tightly to NamePlate-specific translation semantics
- silently reintroducing the old ImGui renderer as a hidden fallback path

Mitigations:

- isolate depth math in one helper
- keep translation/content semantics in `NamePlateTranslationRuntime`
- keep presenter state keyed and explicit
- prefer hiding on invalid state instead of guessing

## Out Of Scope

This design does not include:

- a cap on the number of simultaneous NamePlate overlays
- new DB tables or schema changes
- new translation queues or callback-time live translation
- broad overlay refactors for unrelated surfaces
- Journal, Tooltip, or quest-popup runtime changes

## Files Likely To Change During Implementation

- `NativeUI/AddonHandlers/NamePlates/NamePlateTranslationRuntime.cs`
- new world-space overlay presenter/helper files under a focused runtime namespace
- `Echoglossian.Tests/NamePlateOverlayGeometryTests.cs`
- new tests for distance policy and depth projection
- `Echoglossian.cs` or `NativeUI/Helpers/NamePlateTranslationRuntimeRegistration.cs` only if constructor wiring changes are required

## Acceptance Criteria

- NamePlate overlay presentation no longer relies on the current generic screen-space ImGui path for the depth-aware route
- NamePlate overlays become distance-sensitive with progressive fade and scale
- the design remains reusable for future world-space surfaces
- current NamePlate cache, prefetch, and translation-mode semantics remain intact
- failure modes default to hiding instead of unstable presentation
