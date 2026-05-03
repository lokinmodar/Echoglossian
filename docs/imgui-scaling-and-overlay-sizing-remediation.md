# ImGui Scaling And Overlay Sizing Remediation

Date: 2026-05-03

## Goal

Document how Echoglossian should handle ImGui sizing, font scale, overlay bounds, and layout calculations without falling into the common `GlobalScale` traps discussed by Dalamud plugin developers.

This is a remediation plan, not an immediate refactor.

## Why This Document Exists

Recent discussion in the Dalamud plugin-dev community highlighted a recurring source of UI bugs:

- `ImGuiHelpers.GlobalScale` affects some things but not all things
- `ImGui.GetStyle()` values such as `WindowPadding`, `ItemSpacing`, and `FramePadding` stay unscaled
- plugins that manually multiply or divide layout numbers by `GlobalScale` often become inconsistent under custom user themes and font settings
- `WindowSystem` adds another layer because some plugin windows expect unscaled size input

Echoglossian does not use Dalamud's `WindowSystem` for its main configuration UI or translation overlays. We draw directly with `ImGui` / `ImRaii`.

That means the exact `WindowSystem` issue from the Discord thread does not apply to us directly.

However, the discussion still exposed two important risks for this repository:

1. rigid pixel-based ImGui layout can still degrade badly under large font scales and custom theme spacing
2. overlay sizing can still be wrong if text is measured under a different font or scale than the one used for rendering

## Executive Summary

Current conclusion:

- Echoglossian is **not currently abusing `ImGuiHelpers.GlobalScale`**
- that is good and should stay that way
- the biggest current sizing risk is **overlay text measurement order**
- the second risk is **fixed-pixel config UI layout**

Recommended implementation rule:

- for direct `ImGui` / `ImRaii` UIs, do **not** manually divide or multiply window sizes by `GlobalScale`
- do **not** manually scale `WindowPadding`, `ItemSpacing`, or `FramePadding`
- prefer layout based on `GetContentRegionAvail()`, `GetContentRegionMax()`, viewport fractions, and text measured under the actual render font

## Scope

This document covers:

- `PluginUI/*`
- `UIOverlays/TranslationOverlay/*`
- helper code that computes ImGui window size, text width, or child widths

This document does not cover:

- native addon mutation in `AtkTextNode` / `AtkValue`
- game-space scaling driven by `AtkUnitBase->Scale`
- DPI behavior outside Dalamud/ImGui

## Current Repository Audit

### 1. GlobalScale usage

Repository search shows no active use of:

- `ImGuiHelpers.GlobalScale`
- manual `size / GlobalScale`
- manual `styleValue * GlobalScale`

This is the correct baseline.

We should preserve that.

### 2. Overlay bounds are based on game-space addon geometry, not ImGui scale

Overlay placement currently uses:

- live addon root node coordinates
- toast text-node screen coordinates
- `AtkUnitBase->Scale`
- viewport-relative anchors for quest toast

That separation is correct.

Examples:

- `UIOverlays/TranslationOverlay/TranslationOverlayDrawer.cs`
- `UpdateOverlayBounds(...)`
- `UpdateToastOverlayBounds(...)`
- `TrySyncQuestToastOverlayToViewport()`

These paths are about game UI geometry, not ImGui theme scale. They should stay independent from `GlobalScale`.

### 3. Overlay text measurement is the main current risk

In `UIOverlays/TranslationOverlay/TranslationOverlayDrawer.cs`, `DrawTranslationWindow(...)` currently:

1. calculates `textWidth` with `ImGui.CalcTextSize(...)`
2. calculates width constraints from that measurement
3. pushes the font handle
4. calls `ImGui.SetWindowFontScale(config.FontScale)`
5. renders the text

This means the width can be calculated using a different effective font/scale than the one actually used to render.

That is a correctness problem even when `GlobalScale == 1`.

It becomes more visible when:

- the selected plugin language uses a different font atlas handle
- the overlay uses a non-default `FontScale`
- the user changes Dalamud font scaling
- the line contains wide glyphs or CJK text

This is the highest-priority remediation target.

### 4. Config UI uses several fixed pixel values

The configuration UI currently contains rigid values such as:

- main config window minimum size `900 x 900`
- left sub-tab child widths `185` and `170`
- prompt editor multiline heights like `200`
- footer vertical offset `windowSize.Y - 100`
- popup image sizes `450 x 512` and `512 x 512`

Examples:

- `PluginUI/PluginUI.cs`
- `PluginUI/Tabs/OverlayTab.cs`
- `PluginUI/Components/PromptEditorUI.cs`
- `PluginUI/Components/PluginConfigWindowFooter.cs`

These are not automatically wrong, but they are less resilient under:

- larger fonts
- custom spacing/padding themes
- smaller game resolutions
- very large UI scales

This is the second remediation target.

### 5. DbManagerUI is mostly safe from GlobalScale drift, but still has a few rigid layouts

The DB manager does not currently use `ImGuiHelpers.GlobalScale` either.

That is good.

However, the audit found two distinct categories of layout:

- relatively safe controls that already size from content width, such as `DBManagerUI/Components/Ui/TextInputHelpers.cs`
- rigid sections that still rely on fixed widths or manual `SameLine(...)` offsets, such as:
  - `DBManagerUI/Components/DbToolbar.cs`
  - `DBManagerUI/Components/EditModal.cs`
  - `DBManagerUI/DBEditorWindow.cs`

For the first remediation pass, the `DbToolbar` export-button alignment should stop relying on hardcoded button widths and should instead use measured button widths and content-region alignment.

The modal label/value layout and the DB editor sidebar split can stay as future cleanup items unless they become a concrete usability bug.

## Failsafe Rules For Echoglossian

These are the rules the repository should follow going forward.

### Rule 1. Do not manually scale ImGui style metrics

Do not do this:

- `ImGui.GetStyle().ItemSpacing * ImGuiHelpers.GlobalScale`
- `ImGui.GetStyle().WindowPadding * ImGuiHelpers.GlobalScale`
- `ImGui.GetStyle().FramePadding * ImGuiHelpers.GlobalScale`

Why:

- user-customized style values may already be tuned for their preferred scaling
- multiplying them again produces inconsistent spacing and layout drift
- the result differs across plugins and themes in ways users cannot predict

### Rule 2. Do not divide direct ImGui window sizes by GlobalScale

This specific workaround belongs to `WindowSystem`-style unscaled window APIs.

Echoglossian does not use that model for its core UI surfaces.

For direct `ImGui.Begin(...)` windows and `ImRaii.Child(...)`, treat sizes as normal ImGui-space values and let ImGui/Dalamud handle the rest.

### Rule 3. Prefer content-region layout over magic offsets

For internal UI layout, prefer:

- `ImGui.GetContentRegionAvail()`
- `ImGui.GetContentRegionMax()`
- viewport fractions
- elastic child widths
- ratio-based split layout

This is more resilient than manually stacking:

- hardcoded widths
- hardcoded heights
- repeated `SameLine()` chains that assume fixed spacing

### Rule 4. Measure text under the actual render font and scale

If a window width depends on text measurement:

1. push the same font handle that will be used for rendering
2. apply the same effective font scale logic
3. then measure text
4. then calculate size constraints

The measured text and rendered text must use the same effective font context.

Otherwise:

- wrapping is wrong
- autosized overlays clip
- windows grow or shrink unpredictably across languages

### Rule 5. Keep game-space overlay anchoring separate from ImGui text sizing

These are separate problems:

- where the overlay should appear relative to the game addon
- how wide/tall the overlay window should be when ImGui renders text inside it

Game-space anchoring should continue to rely on:

- live addon geometry
- text-node screen position
- addon scale

ImGui-space sizing should rely on:

- actual font metrics
- content region
- viewport constraints

Do not collapse these into one scaling formula.

## Remediation Plan

### Phase 1. Fix overlay measurement correctness

Target files:

- `UIOverlays/TranslationOverlay/TranslationOverlayDrawer.cs`
- possibly a new helper near overlay sizing logic

Work:

- move overlay text measurement into the same font context used for drawing
- ensure `CalcTextSize(...)` runs after selecting the proper font handle
- ensure width constraint logic reflects `config.FontScale`
- normalize overlay font scale to a positive supported range instead of allowing invalid negative render scale
- keep the patch narrow and avoid changing visual style policy at the same time
- for `DbManagerUI`, replace hardcoded right-alignment assumptions in `DbToolbar.cs` with measured button widths

Expected benefit:

- more accurate overlay width
- fewer wrap/clipping issues
- behavior that tracks actual rendered text instead of guessed text metrics
- `DbManagerUI` toolbar remains aligned under different fonts without assuming fixed button widths

Risk:

- overlay size may visibly change for some surfaces
- talk/battle talk/mini talk/toast overlays may need quick in-game verification after the change

### Phase 2. Replace rigid config layout hotspots with content-region layout

Target files:

- `PluginUI/PluginUI.cs`
- `PluginUI/Tabs/OverlayTab.cs`
- `PluginUI/Components/PromptEditorUI.cs`
- `PluginUI/Components/PluginConfigWindowFooter.cs`

Work:

- replace fixed left/right child widths with content-region ratios where appropriate
- reduce reliance on fixed footer offsets
- avoid assuming a static minimum vertical space budget
- keep popup sizing bounded by viewport instead of fixed large image assumptions

Expected benefit:

- config window behaves better under large fonts and narrow resolutions
- fewer collisions between labels, help indicators, and buttons

Risk:

- this can create broader visual churn than Phase 1
- should be done as isolated UI cleanup, not mixed with runtime behavior work

### Phase 3. Audit SameLine-heavy layouts

Target files:

- overlay settings pages
- footer buttons
- prompt editor action rows

Work:

- identify `SameLine()` chains that assume default spacing fits all scales
- replace the fragile ones with grouped width allocation or child-region splits
- keep simple two-control rows if they still behave correctly

Expected benefit:

- fewer edge cases under custom themes
- less dependence on theme spacing defaults

Risk:

- easy to over-refactor
- should only touch rows that are actually fragile

### Phase 4. Optional repo-local sizing helper

Only do this if Phase 1 and 2 reveal repeated patterns.

Possible helper responsibilities:

- measure wrapped text using the active overlay font context
- compute overlay width bounds consistently
- clamp popup/window sizes to viewport fractions

Do not add a helper just to wrap `ImGui.GetStyle()` or `GlobalScale`.

That would not solve the real problem.

## What We Should Not Do

Do not introduce a repo-wide “fix” that:

- multiplies style values by `GlobalScale`
- divides all window sizes by `GlobalScale`
- forces one “scaled spacing” formula for every UI surface
- mixes native addon scale and ImGui font scale into one generic calculation

Those approaches are exactly the class of workaround that causes the plugin-dev chaos described in the Discord thread.

## Verification Matrix

After Phase 1 or later UI changes, verify in-game with:

### Font and scale combinations

- default Dalamud font scale
- larger Dalamud global font scale
- at least one language using the general font path
- at least one language using the language-specific font path

### Theme/layout combinations

- default Dalamud theme spacing
- a theme with larger padding/spacing
- smaller game resolution
- larger game resolution

### Overlay surfaces

- Talk
- BattleTalk
- TalkSubtitle
- MiniTalk
- CutSceneSelectString
- WideText toast
- Error toast
- Area toast
- Class change toast
- Quest toast

Check for:

- clipped text
- premature wrapping
- oversized empty windows
- title and body misalignment
- popup or settings controls overlapping

## Practical Answer To The Original Discord Question

For Echoglossian, the failsafe answer is:

- do not manually build a `GlobalScale` sizing formula for direct ImGui/ImRaii windows
- use content-region and viewport-relative layout for plugin UI
- keep game-space addon anchoring separate from ImGui text/window sizing
- when text drives the width, measure it under the same font and scale that will actually render it

That is the stable path for this repository.

## Recommended Next Step

If this remediation plan is executed, start with **Phase 1 only**:

- fix overlay measurement order in `TranslationOverlayDrawer`
- do not mix that patch with broad config UI cleanup

That gives the highest correctness gain for the smallest behavior surface.
