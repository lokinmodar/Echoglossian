# Issue 139 RTL ImGui Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> `superpowers:subagent-driven-development` (recommended) or
> `superpowers:executing-plans` to execute this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve GitHub issue `#139` by shipping a safe `Phase A` RTL
presentation path for plugin-owned ImGui surfaces, while documenting and
gating later `Phase B` work both inside Echoglossian and upstream in Dalamud.

**Architecture:** Introduce a stable text-presentation seam that lets overlays
and tooltips request a measured renderable block without caring whether the
backend is plain ImGui text, texture-backed RTL, a future plugin-local shaped
backend, or an eventual Dalamud-level complex-text service.

**Tech Stack:** C# / .NET 10 Windows, xUnit, FluentAssertions, Dalamud ImGui
UI, existing `ImageGeneration` pipeline, existing `ITextureProvider`
integration, optional later Dalamud upstream RFC work.

## References

- `docs/superpowers/specs/2026-07-12-issue-139-rtl-imgui-architecture-design.md`
- `docs/superpowers/specs/2026-07-12-issue-139-rtl-performance-usability-and-test-spec.md`
- `docs/superpowers/specs/2026-07-12-issue-139-dalamud-rtl-upstream-rfc.md`
- [Dear ImGui #4227](https://github.com/ocornut/imgui/issues/4227)
- [Dear ImGui #2365](https://github.com/ocornut/imgui/issues/2365)
- [RTLScript](https://github.com/oscar7070/RTLScript)
- [FarsiType](https://github.com/AmyrAhmady/FarsiType)

## Global Constraints

- Keep the first shipped slice to plugin-owned ImGui presentation only.
- Do not treat `Phase A` as full RTL support for generic ImGui widgets.
- RTL languages stop being `UnsupportedLanguage`, but remain forced to
  overlay-only presentation in `Phase A`.
- Do not redesign DB truth around bitmap persistence.
- No per-frame text-to-image work is acceptable in the steady state.
- Cache must be bounded by byte size, not only entry count.
- Follow the repo `.editorconfig` and StyleCop settings, including file
  headers, XML docs, braces, and `this.` call style.
- Validate with:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- In-game verification is required before calling `Phase A` complete.

---

## File Map

Expected implementation targets for `Phase A`:

- `UIOverlays/TextPresentation/TextLayoutRequest.cs`
  Shared request model for translated ImGui text.
- `UIOverlays/TextPresentation/RenderedTextBlock.cs`
  Shared measured render result for either text lines or texture output.
- `UIOverlays/TextPresentation/TextPresentationBackendKind.cs`
  Stable backend identifier for diagnostics and tests.
- `UIOverlays/TextPresentation/ITextPresentationBackend.cs`
  Shared contract for render backends.
- `UIOverlays/TextPresentation/TextPresentationResolver.cs`
  Chooses the correct backend for a given request.
- `UIOverlays/TextPresentation/LanguagePresentationPolicy.cs`
  Determines RTL requirements and overlay-only collapse behavior.
- `UIOverlays/TextPresentation/PlainImGuiBackend.cs`
  Current non-RTL behavior wrapped behind the new contract.
- `ImageGeneration/RtlTextTextureService.cs`
  Shared service that turns RTL text requests into measured texture-backed
  render blocks.
- `ImageGeneration/TextTextureCache.cs`
  Updated to enforce byte budgets, TTL, and better diagnostics.
- `ImageGeneration/CachedTextureEntry.cs`
  Updated to track estimated bytes and request metadata.
- `UIOverlays/TranslationOverlay/TranslationOverlayDrawer.cs`
  Integrates the presentation seam for overlays.
- `NativeUI/Helpers/HoverTooltipManager.cs`
  Integrates the presentation seam for hover tooltips.
- `NativeUI/Helpers/ActionItemDetailUiRuntime.cs`
  Integrates the presentation seam for structured tooltip overlays.
- `PluginUI/PluginUI.cs`
  Removes hard unsupported blocking for RTL languages and preserves forced
  overlay-only semantics.
- `GeneralHelpers/TranslationActivationGuard.cs`
  Continues to enforce activation rules after the unsupported-language change.
- `Echoglossian.Tests/RtlLanguagePresentationPolicyTests.cs`
  Tests RTL activation and overlay-only behavior.
- `Echoglossian.Tests/TextPresentationResolverTests.cs`
  Tests backend selection.
- `Echoglossian.Tests/TextTextureCacheBudgetTests.cs`
  Tests cache byte-budget enforcement.
- `Echoglossian.Tests/TextTextureCacheConcurrencyTests.cs`
  Tests in-flight deduplication and cooldown behavior.
- `Echoglossian.Tests/RtlTextureRenderMeasurementTests.cs`
  Tests measurement and width-limit semantics from the rendered result.
- `Echoglossian.Tests/RtlOverlayPresentationDecisionTests.cs`
  Tests integration decisions for overlay surfaces.

Expected later `Phase B` plugin targets:

- `UIOverlays/TextPresentation/PluginShapedTextBackend.cs`
- any shaping or bidi helper types introduced for static-text R&D

No code targets are approved yet for the Dalamud upstream phase in this
repository; that track stays design-first until `Phase A` is shipped and
measured.

## Phase Structure

### Phase A: Product delivery in the plugin

Ship correct RTL presentation for overlays and tooltips with bounded in-memory
texture caching.

### Phase B1: Plugin-local R&D

Investigate a better static-text backend only after `Phase A` ships and only if
real limitations remain.

### Phase B2: Dalamud upstream proposal

Use the measured `Phase A` experience to draft or refine an upstream static
complex-text proposal for Dalamud.

`Phase B1` and `Phase B2` are explicitly gated; do not start them while
`Phase A` remains incomplete.

---

## Task 1: Add the Presentation Seam

**Files:**

- Create: `UIOverlays/TextPresentation/TextLayoutRequest.cs`
- Create: `UIOverlays/TextPresentation/RenderedTextBlock.cs`
- Create: `UIOverlays/TextPresentation/TextPresentationBackendKind.cs`
- Create: `UIOverlays/TextPresentation/ITextPresentationBackend.cs`
- Create: `UIOverlays/TextPresentation/TextPresentationResolver.cs`
- Create: `UIOverlays/TextPresentation/LanguagePresentationPolicy.cs`
- Create: `UIOverlays/TextPresentation/PlainImGuiBackend.cs`
- Test: `Echoglossian.Tests/RtlLanguagePresentationPolicyTests.cs`
- Test: `Echoglossian.Tests/TextPresentationResolverTests.cs`

- [ ] Write failing tests for:
  - RTL languages are no longer classified as unsupported for activation
  - RTL languages still force overlay-only presentation
  - resolver chooses `PlainImGuiBackend` for normal LTR requests
  - resolver chooses `RtlTextureBackend` for RTL requests

- [ ] Implement the minimal presentation contract and policy layer.

- [ ] Keep `PlainImGuiBackend` functionally equivalent to the current LTR path.

- [ ] Re-run targeted tests before proceeding.

## Task 2: Upgrade the Texture Cache for Real Runtime Use

**Files:**

- Modify: `ImageGeneration/TextTextureCache.cs`
- Modify: `ImageGeneration/CachedTextureEntry.cs`
- Create: `ImageGeneration/RtlTextTextureService.cs`
- Test: `Echoglossian.Tests/TextTextureCacheBudgetTests.cs`
- Test: `Echoglossian.Tests/TextTextureCacheConcurrencyTests.cs`

- [ ] Write failing tests for:
  - byte-budget eviction
  - stale-entry pruning
  - in-flight generation deduplication
  - failure cooldown behavior

- [ ] Replace count-only cache assumptions with byte-budget accounting.

- [ ] Add diagnostics fields:
  - entry count
  - estimated bytes
  - hit count
  - miss count
  - eviction count
  - failure count

- [ ] Add request-key deduplication so identical visible text does not queue
  multiple generations.

- [ ] Enforce the budgets from the performance spec.

## Task 3: Build the `RtlTextureBackend`

**Files:**

- Create: `UIOverlays/TextPresentation/RtlTextureBackend.cs`
- Modify: `ImageGeneration/TextImageRenderer.cs` as needed
- Modify: `ImageGeneration/TextTextureGenerator.cs` as needed
- Test: `Echoglossian.Tests/RtlTextureRenderMeasurementTests.cs`

- [ ] Write failing tests for:
  - returned measurement respects width constraints
  - returned measurement is based on rendered output, not raw ImGui text size
  - alignment and direction metadata are preserved in the render block

- [ ] Implement the backend around the existing text-to-image path.

- [ ] Ensure the backend can return a render block that callers can draw
  without calling `ImGui.CalcTextSize` for the RTL text body.

- [ ] Add explicit handling for oversized requests so one huge paragraph cannot
  allocate an unbounded texture.

## Task 4: Integrate Overlay Surfaces

**Files:**

- Modify: `UIOverlays/TranslationOverlay/TranslationOverlayDrawer.cs`
- Test: `Echoglossian.Tests/RtlOverlayPresentationDecisionTests.cs`

- [ ] Refactor the drawer so it requests a render block from the resolver.

- [ ] Keep the existing plain-text flow for `PlainImGuiBackend`.

- [ ] Add texture draw handling for `RtlTextureBackend`.

- [ ] Size overlay windows from the resolved block metrics rather than always
  using `ImGui.CalcTextSize`.

- [ ] Verify that `Talk`, `BattleTalk`, `TalkSubtitle`, `MiniTalk`,
  `CutSceneSelectString`, and toast-family overlays keep their current LTR
  behavior unchanged.

## Task 5: Integrate Tooltip Paths

**Files:**

- Modify: `NativeUI/Helpers/HoverTooltipManager.cs`
- Modify: `NativeUI/Helpers/ActionItemDetailUiRuntime.cs`

- [ ] Replace plain `TextUnformatted` rendering for RTL tooltip bodies with the
  presentation seam.

- [ ] Preserve existing title/body semantics and separators.

- [ ] Preserve hover hit-box behavior and tooltip-open conditions.

- [ ] Route structured tooltip overlays through the same resolver rules so they
  do not diverge from the main overlay path.

## Task 6: Unlock Activation While Preserving Safe Mode Rules

**Files:**

- Modify: `PluginUI/PluginUI.cs`
- Modify: `GeneralHelpers/TranslationActivationGuard.cs`
- Modify tests around activation and display modes as needed

- [ ] Remove the hard unsupported-language gate for the approved RTL languages.

- [ ] Preserve overlay-only or tooltip-only collapse behavior for those
  languages.

- [ ] Make sure the operator experience is "supported with presentation
  constraints", not "unsupported language".

- [ ] Re-run the existing activation and display-mode tests plus the new RTL
  policy tests.

## Task 7: Add Lightweight Diagnostics and Developer Validation Hooks

**Files:**

- Modify the most appropriate debugger or diagnostic surface once the
  implementation shape is clear
- Optional: add one-shot trace helpers if a UI surface is too expensive

- [ ] Expose cache counts and estimated bytes in a non-spammy way.

- [ ] Expose the active backend kind per visible RTL surface when useful for
  debugging.

- [ ] Ensure failures are observable without leaving hot-path log spam in
  normal play.

## Task 8: Full Validation Before Completion

- [ ] Run:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`

- [ ] Validate in-game for:
  - `Talk`
  - `BattleTalk`
  - `TalkSubtitle`
  - `MiniTalk`
  - one toast-family overlay
  - hover tooltips
  - structured tooltip overlays

- [ ] Validate at least:
  - Arabic
  - Hebrew
  - Persian
  - Urdu

- [ ] Include one mixed-content case per language:
  - numerals
  - punctuation
  - embedded Latin text

- [ ] Confirm:
  - no stable-text flicker
  - no repeated generation while idle
  - no obvious frame hitch on cache hit
  - memory remains bounded during repeated surface changes

---

## Phase B1 Gate: Plugin-Local R&D

Start only after `Phase A` ships and passes the validation above.

- [ ] Re-audit the shipped `Phase A` code and measured diagnostics.
- [ ] Decide whether remaining issues are about correctness, fidelity, or only
  maintainability.
- [ ] If continued work is justified, run a small spike comparing:
  - current texture backend
  - a string-preprocessing prototype based on ideas from `RTLScript` or
    `FarsiType`
  - a plugin-local shaped static-text backend
- [ ] Reject any direction that drifts into owning full editable-widget RTL in
  the plugin.

## Phase B2 Gate: Dalamud Upstream Track

Start only after `Phase A` behavior is measured with real plugin usage.

- [ ] Convert `Phase A` findings into an upstream problem statement focused on
  static complex text.
- [ ] Keep the first upstream request narrow:
  - static read-only text
  - framework-managed layout and render artifacts
  - no full `InputText` RTL promise
- [ ] Map Echoglossian's presentation seam to a future
  `DalamudComplexTextBackend` adapter so the plugin can adopt upstream work
  later without another surface rewrite.

## As-Built Status (2026-07-13)

Phase A is implemented as bounded, texture-backed presentation selected by
`LanguagePresentationPolicy`. It supports plugin-owned overlays and hover
tooltip presentation, with direction-aware cache keys, right alignment for RTL
languages, adaptive hover sizing, and `TexturePresentationLineHeightScale` for
multiline density. It is not universal native-widget bidi support. Phase B
remains an R&D and upstream track: static shaped text first, then ImGui helpers;
editable widgets remain separate investigation. The shipped path clamps texture
dimensions to `2048 px`, rejects layouts above `2,097,152 px`, caps one cache
entry at `48 MiB`, and validates one reusable layout before the default upload
path allocates or encodes a bitmap.
