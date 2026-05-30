# Native Toast And MiniTalk Iteration Log

Date: 2026-05-19

## Scope

This log tracks the follow-up work on native text reflow and state restoration
 for:

- `_MiniTalk`
- `_BattleTalk`
- native addon toasts handled through addon-node mutation
- `_TextGimmickHint`

This document exists because the work is highly iterative and probe-driven. The
 goal is to keep the history of what was attempted, what was learned from the
 probes, and what changed in code.

## Iteration 1

Goal:
- introduce a shared native text reflow helper so translated text can grow
  wrappers and the nearest background without repeating handler-specific sizing
  code

Changes:
- added shared native sizing and wrapper-growth logic in
  `NativeTextNodeLayoutHelper`
- wired the helper into:
  - `_BattleTalk`
  - `_MiniTalk`
  - addon toast handlers
  - `_TextGimmickHint`

Validation:
- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`

## Iteration 2

Goal:
- stop `_MiniTalk` from repeatedly translating already-translated visible text

Findings:
- `_MiniTalk` in native replacement mode could recapture its own translated
  visible text as a new source line

Changes:
- `_MiniTalk` now tries to recover the original source through:
  - tracked replacement-to-original state
  - `OriginalTextPointer`
  - known replacement-to-original mapping across tracked bubbles

Validation:
- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`

## Iteration 3

Goal:
- stop repeated width collapse in `_MiniTalk`

Findings:
- repeated reflow reused the already-shrunk text-node width as the next wrap
  width
- bubble reuse caused each pass to get narrower and taller

Changes:
- the shared helper now prefers a stable container/background width over a
  previously shrunken text-node width
- `_BattleTalk` was aligned to the same width-resolution rule

Validation:
- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`

## Iteration 4

Goal:
- make addon probing usable for long-lived `_BattleTalk` and toast captures

Changes:
- `/egloaddonprobe` now accepts explicit watch durations such as:
  - `/egloaddonprobe _BattleTalk 15m`
  - `/egloaddonprobe _BattleTalk 900s`
- localized the new probe command wording in `Resources`

Validation:
- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`

## Iteration 5

Goal:
- understand why `_MiniTalk` still degraded after the shared text-node helper
  looked mostly correct

Probe findings:
- `_MiniTalk` is a pooled addon with multiple reusable bubble slots
- hidden component roots can still leave descendant text nodes readable
- the problem is not just text-node width; it is slot-state leakage across the
  whole bubble
- the addon has a dedicated `StringArrayData` subscription:
  - `_MiniTalk`
  - `stringArrayIndex=32`

Conclusion:
- `_MiniTalk` must be handled as a component-backed bubble pool, not as a
  simple list of free-standing text nodes

## Iteration 6

Goal:
- restore whole-slot layout state and stop stale translation/layout reuse in
  `_MiniTalk` and toast-family native replacement

Changes:
- `AddonTextNodeResolvers.ResolveMiniTalkBubbleTextNodes(...)` now resolves
  bubble text nodes from visible top-level component slots instead of collecting
  any readable text node from the entire addon tree
- `_MiniTalk` now reconciles the currently visible source line before trusting
  already-resolved translated state
- restoring a prior native layout snapshot can now skip writing the previous
  original text back when the slot has already been reused by a different
  source line
- `NativeTextNodeLayoutHelper` now restores:
  - wrapper X/Y
  - wrapper width/height
  - secondary-container X/Y
  - secondary-container width/height
- addon toast handlers and `_TextGimmickHint` now reconcile visible source text
  before using stale resolved state, following the same model

Validation:
- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- `dotnet build Echoglossian.csproj -c Release --no-restore`

## Current State

What improved:
- `_BattleTalk` native mode is visually better than the earlier fixed-width and
  forced-font-shrink implementation
- the code now has shared snapshot-and-restore infrastructure instead of ad hoc
  one-off sizing per handler
- `_MiniTalk` and toast-family state handling is now much closer to a correct
  pooled-slot model

## Iteration 7

Goal:
- explore `ToastGui` as an earlier source-capture path for supported toasts
  without regressing the existing overlay/native presentation behavior

Findings:
- `IToastGui.QuestToast` is already a full callback-native runtime in the repo
- `IToastGui.Toast` and `IToastGui.ErrorToast` expose earlier source payloads
  for normal/error toasts
- generic `IToastGui.Toast` does not expose enough subtype metadata to replace
  `_WideText`, `_AreaText`, and `_TextClassChange` as presentation owners
- because overlay bounds and swap mode still depend on the live addon shape,
  the safest first step is callback-assisted prefetch, not a full replacement
  of the addon handlers

Changes:
- added `Config.UseToastGuiCaptureForSupportedToasts`
- added `ToastGuiCaptureRuntime`
- registered `IToastGui.Toast` and `IToastGui.ErrorToast` callbacks
- the callback runtime now prefetches translations into the existing
  `ToastMessage` persistence path for:
  - supported normal toasts
  - error toasts
- overlay publication and native replacement remain owned by the addon
  handlers, preserving existing overlay bounds behavior
- documented the split explicitly in
  `docs/dialogue-and-toast-runtime-flows.md`

Validation:
- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`

What still needs in-game confirmation:
- `_MiniTalk` bubble reuse stability after the slot-level reconcile changes
- toast-family source reconciliation and centering consistency
- whether the `ToastGui`-assisted toast path improves cache-hit timing without
  changing overlay/swap presentation
- whether additional toast surfaces can be moved further toward source-level
  handling after this first prefetch-only cut

## Next Investigation

1. test the latest `_MiniTalk` and toast-family reconcile changes in game
2. test the new `ToastGui`-assisted capture toggle with:
   - `_WideText`
   - `_TextError`
   - `_AreaText`
   - `_TextClassChange`
3. preserve the current addon-node flow as the default stable path until the
   callback-assisted runtime proves safe enough to own more of the pipeline

## Iteration 8

Goal:
- stop `_MiniTalk` and `_BattleTalk` from re-applying native layout on top of
  already-mutated geometry
- keep wrap width tied to the stable original bubble/addon width instead of the
  potentially bloated width left behind by a prior native replacement pass

Findings:
- `_MiniTalk` could still capture a layout snapshot from an already-mutated
  bubble when the same visible line was re-applied
- `_BattleTalk` native replacement still used exact string equality for the
  visible translated text, so layout-driven whitespace changes could trigger
  another apply pass against the same source line
- the shared helper still trusted the current text-node width over the stable
  container width, and it allowed `ResizeNodeForCurrentText()` to report a
  larger width than the intended wrap width

Changes:
- `NativeTextNodeLayoutHelper.ResolvePreferredWrapWidth(...)` now prefers the
  bounded container width whenever it exists
- `NativeTextNodeLayoutHelper.ApplyWrappedTextAndMeasure(...)` now clamps the
  effective width back to the chosen wrap width after the resize pass and uses
  that width for downstream container sizing
- `_MiniTalk` now restores the tracked native layout for the same bubble/source
  before reapplying translation, so each apply pass starts from the original
  bubble geometry
- `_MiniTalk` native reflow now keeps width growth disabled again, relying on a
  better stable wrap width instead of growing the whole bubble horizontally
- `_BattleTalk` now restores the tracked native layout before a same-source
  reapply
- `_BattleTalk` now uses normalized text comparison when checking whether the
  visible translated text is already applied
- `_BattleTalk` native reflow now keeps width growth disabled so the original
  addon width remains the wrap target

Validation:
- pending after implementation

## Iteration 9

Goal:
- start probing `_BattleTalk` and `_MiniTalk` automatically at login in
  `DEBUG` builds so the earliest hidden addon state can be captured before the
  game chat becomes available
- allow `/egloaddonprobe stop` to stop both manual and auto-started probe
  watches for the current login session

Findings:
- the existing addon-probe command only tracked one manual watch
- the shared `AddonStructureProbeWatch` already dumps an addon even when it is
  resolved while invisible, so it can capture `_BattleTalk`'s startup state as
  soon as the login session becomes ready
- `_MiniTalk` is not guaranteed to exist at login, but starting a managed watch
  there is still useful because the watch will dump as soon as the addon
  appears later in the same session

Changes:
- added `AddonProbeAutoWatchPolicy` as the small testable gate for:
  - one automatic watch set per login session
  - stop-command suppression until the next logout
- added `AddonProbeAutoWatchHelpers` to:
  - tick the manual watch
  - tick managed auto-started watches
  - start `_BattleTalk` and `_MiniTalk` managed watches for `15m` once per
    login session
  - reset that gate on logout
- `/egloaddonprobe stop` now stops:
  - the manual watch
  - any auto-started managed watches
- documented the new `DEBUG` login auto-probe behavior in
  `docs/commands/egloaddonprobe.md`
- added hidden config flag `EnableDebugLoginAddonProbe`, default `false`, so
  the login auto-probe path is opt-in instead of always-on

Validation:
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter AddonProbeAutoWatchPolicyTests`
- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`

## Iteration 10

Goal:
- stop cross-surface leakage where stale `OriginalTextPointer` content could be
  mistaken for the logical source line in `_MiniTalk`, addon toasts, or
  `_TextGimmickHint`
- reduce `_MiniTalk` and `BattleTalk` flicker by restoring only geometry before
  same-source native reapply passes instead of briefly writing the original text
  back into the node mid-frame

Findings:
- `_MiniTalk`, addon toasts, and `_TextGimmickHint` all trusted
  `AtkTextNode.OriginalTextPointer` whenever it differed from the visible node
  text
- pooled/recycled native text nodes can carry stale original-pointer content
  from an unrelated prior use, which is enough to make one surface believe the
  source text belongs to another surface
- `_MiniTalk` and `BattleTalk` same-source reapply paths restored the original
  text as part of the geometry reset, which can create visible flicker when the
  next apply happens in a later lifecycle callback of the same frame

Changes:
- `_MiniTalk` now accepts `OriginalTextPointer` only when MiniTalk itself can
  corroborate that original through:
  - the current bubble state
  - another tracked MiniTalk bubble state
  - or a stored `MiniTalkMessage` row whose replacement matches the visible text
- addon toasts now accept `OriginalTextPointer` only when the toast surface can
  corroborate it through:
  - the current toast handler state
  - or a stored `ToastMessage` row whose replacement matches the visible text
- `_TextGimmickHint` now applies the same corroboration rule through:
  - the current gimmick-hint handler state
  - or a stored `TextGimmickHintMessage` row whose replacement matches the
    visible text
- `_MiniTalk` same-source reapply now restores only geometry and flags before
  writing the replacement back, avoiding an intermediate original-text flash
- `BattleTalk` same-source reapply now restores only geometry and flags before
  reapplying the translated text and translated speaker name

Validation:
- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`

## Iteration 11

Goal:
- reduce the remaining `_MiniTalk` flicker by letting the game keep the live
  bubble position while still restoring the original geometry between native
  apply passes
- give `_MiniTalk` a little more horizontal room derived from the live bubble
  padding instead of fixed widths
- stop re-centering toast text inside widened backgrounds when that centering
  pushes the text to the right of the visual addon center

Findings:
- `_MiniTalk` bubbles track NPC movement, so restoring the snapshot X/Y
  positions on every reset can fight the game and create frame-to-frame
  position jitter
- the prior width fix had not actually landed in a validated build because the
  new `_MiniTalk` helper had a compile-time type ambiguity
- the shared horizontal-centering helper was still running for addon toasts and
  could visibly shift text rightward relative to the toast background

Changes:
- `RestoreLayoutSnapshot(...)` now supports restoring size/flags without
  restoring node positions
- `_MiniTalk` now restores tracked native layouts with `restorePositions: false`
  so the bubble keeps the game-controlled live position while geometry resets to
  the original client state
- `_MiniTalk` native apply now adds a small extra wrap-width allowance derived
  from the current left/right padding inside the live bubble
- the shared native text reflow helper now accepts:
  - `restoreHorizontalCentering`
  - `additionalWrapWidth`
- addon toasts and `_TextGimmickHint` now disable the horizontal-centering
  adjustment so their text can stay anchored to the original addon layout

Validation:
- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- `dotnet build Echoglossian.csproj -c Release --no-restore`

## Iteration 12

Goal:
- document the current toast runtime ownership model in English
- document the proposed full alternate `ToastGui` path for supported toasts in
  English
- make the distinction explicit between:
  - today's addon-handler presentation path
  - today's optional `ToastGui` prefetch
  - the proposed callback-owned full runtime

Changes:
- added `docs/toast-runtime-current-state.md`
- added `docs/toastgui-runtime-alternative-path.md`
- recorded the current and proposed ownership of:
  - capture
  - translation lookup
  - overlay publication
  - native mutation
  - persistence
- documented that `_TextGimmickHint` remains outside the proposed migration

Validation:
- not run, docs-only change

## Iteration 13

Goal:
- verify exactly what `ToastGui` supports in Dalamud before implementing a full
  alternate callback-owned toast route
- update the toast design docs with the real callback boundaries and
  architectural constraints

Findings from Dalamud source and docs:
- `IToastGui` exposes exactly three callback surfaces:
  - `Toast`
  - `QuestToast`
  - `ErrorToast`
- the implementation hooks:
  - `UIModule.ShowWideText`
  - `UIModule.ShowText`
  - `UIModule.ShowErrorText`
- `isHandled = true` suppresses the original native call
- the generic normal-toast callback only exposes:
  - `SeString message`
  - `ToastOptions.Position`
  - `ToastOptions.Speed`
- it does not expose:
  - addon name
  - normal-toast subtype
  - a stable identity that distinguishes `_WideText`, `_AreaText`, and
    `_TextClassChange`

Implication:
- a full `ToastGui` route is straightforward for quest and error toasts
- a full subtype-preserving normal-toast migration needs either:
  - a correlation layer
  - or a deliberate collapse into one generic normal-toast family under the
    alternate route

Changes:
- updated `docs/toast-runtime-current-state.md`
- updated `docs/toastgui-runtime-alternative-path.md`

Validation:
- not run, docs-only change

## Iteration 14

Goal:
- record the architectural decision for the future `ToastGui` alternate route
  after confirming that Echoglossian already persists supported normal toasts
  as one logical DB family

Decision:
- under the alternate `ToastGui` route, `_WideText`, `_AreaText`, and
  `_TextClassChange` should be treated as one unified `Toast / NonError`
  runtime family
- the alternate route should not try to preserve addon-specific ownership for
  activation or behavior inside that normal-toast family

Changes:
- updated `docs/toast-runtime-current-state.md`
- updated `docs/toastgui-runtime-alternative-path.md`

Validation:
- not run, docs-only change

## Iteration 15

Goal:
- implement the hidden full `ToastGui` route for supported normal and error
  toasts
- keep the legacy addon-handler route as the default fallback
- stop treating `_WideText`, `_AreaText`, and `_TextClassChange` as separate
  activation owners while the hidden route is enabled

Changes:
- added hidden config toggle:
  - `UseToastGuiRuntimeForSupportedToasts`
- added `ToastGuiSupportedToastPolicy`
- added `ToastGuiSupportedToastRuntime`
- added `ToastGuiSupportedToastRuntimeRegistration`
- the supported normal-toast family is now callback-owned through
  `IToastGui.Toast` when the hidden route is enabled
- the error-toast family is now callback-owned through
  `IToastGui.ErrorToast` when the hidden route is enabled
- legacy addon handlers for:
  - `_WideText`
  - `_AreaText`
  - `_TextClassChange`
  - `_TextError`
  are not registered while the hidden route is active
- the old capture-only `ToastGui` runtime now self-suppresses when the hidden
  full route owns the same family
- the hidden route treats supported normal toasts as one logical
  `Toast / NonError` family and currently reuses
  `WideTextToastTranslationDisplayMode` as its family display-mode source
- added focused tests for:
  - hidden config default
  - legacy prefetch suppression
  - family-level normal-toast routing policy
- updated:
  - `docs/toast-runtime-current-state.md`
  - `docs/toastgui-runtime-alternative-path.md`

Focused validation:
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "ConfigDefaultsTests|ToastGuiCaptureRuntimeTests|ToastGuiSupportedToastPolicyTests"`

## Iteration 16

Goal:
- keep the BattleTalk timer spinner inside the visible background after native
  text reflow
- preserve client-derived sizing instead of introducing fixed widths or
  resolution-specific heuristics

Root cause:
- BattleTalk already re-anchored the timer/spinner node after text reflow, but
  the nine-grid background width was not guaranteed to grow with it
- that let the spinner end up past the right edge of the background even when
  the parent addon and text node had resized correctly

Changes:
- extended `NativeTextNodeLayoutSnapshot` to capture:
  - anchored sibling width
  - original right padding between the anchored sibling and the secondary
    background
- added
  `NativeTextNodeLayoutHelper.ResolveMinimumSecondaryWidthForAnchoredNode(...)`
  as a reusable geometry helper
- `ResizeFromSnapshot(...)` now grows the secondary container just enough to
  keep an anchored sibling node covered by the same background, while clamping
  negative historical padding to zero
- added focused tests in:
  - `Echoglossian.Tests/NativeTextNodeLayoutHelperTests.cs`

Validation:
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter NativeTextNodeLayoutHelperTests`

## Iteration 17

Goal:
- apply the global non-quest native replacement diacritics-removal toggle
  consistently across shared DB-first native UI surfaces
- keep overlay-only payloads and persistence semantics unchanged

Root cause:
- Talk, BattleTalk, MiniTalk, subtitles, and toast-family handlers already
  normalized replacement text in their addon-local native apply paths
- shared DB-first handlers and the `_MainCommand` refresh writer still wrote
  translated native text directly without passing through that same
  normalization step

Changes:
- added `NativeReplacementTextNormalizationHelper` as a pure shared payload
  normalizer for native replacement-only writes
- added `Echoglossian.TryCreateNativeReplacementTextNormalizer(...)` so shared
  handlers can reuse the same game-font diacritics-removal callback without
  changing DB semantics
- `DbFirstGameWindowAddonHandler.ApplyPayload(...)` now normalizes translated
  payloads only for display modes that actually write native translation
- `_MainCommand` refresh apply now normalizes translated payloads before
  writing translated labels into refresh `AtkValue`s
- added focused tests in:
  - `Echoglossian.Tests/NativeReplacementTextNormalizationHelperTests.cs`

Validation:
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter NativeReplacementTextNormalizationHelperTests`

## Iteration 18

Goal:
- make it explicit, in runtime UI and policy tests, whether supported toasts
  are currently using the legacy addon path, the old ToastGui capture/prefetch
  path, or the full ToastGui runtime path
- enable the full supported-toast ToastGui route in the user's real local
  `Echoglossian.json` without guessing

Root cause:
- the hidden toast routing toggles lived only in config and policy code
- there was no explicit runtime-facing status showing which route was actually
  active for normal/error toasts
- the real local config file did not contain either hidden ToastGui toast key,
  so runtime behavior silently fell back to the default legacy addon handlers

Changes:
- added `ToastGuiRouteState` as the canonical route-state enum for supported
  toast-family routing
- added:
  - `ToastGuiSupportedToastPolicy.GetSupportedNormalToastRouteState(...)`
  - `ToastGuiSupportedToastPolicy.GetSupportedErrorToastRouteState(...)`
- expanded `ToastGuiSupportedToastPolicyTests` to cover:
  - default legacy state
  - capture/prefetch state
  - full runtime state
  for both normal and error toast families
- updated `OverlayTab.DrawToastGeneralPage(...)` to show live route status for:
  - supported normal toasts
  - supported error toasts
  - quest toast
  - text gimmick hint
- added localized resource keys for those route-status labels and values
- manually aligned `Resources.Designer.cs` because this repo branch did not
  regenerate the designer automatically during the test-first pass
- updated the real local config at:
  - `%APPDATA%\\XIVLauncher\\pluginConfigs\\Echoglossian.json`
  to set:
  - `UseToastGuiRuntimeForSupportedToasts = true`
  - `UseToastGuiCaptureForSupportedToasts = false`
  with a timestamped backup created alongside the file

Focused validation:
- `dotnet test Echoglossian.Tests\\Echoglossian.Tests.csproj -c Debug --filter ToastGuiSupportedToastPolicyTests`

## Iteration 19

Goal:
- stop ImGui table assertions that were spamming `dalamud.log` and potentially
  impacting frame-time stability while the DB Editor window is open

Root cause:
- `DbTableView` called `ImGui.TableSetupColumn` with a non-zero initial width
  (`150f`) while using `ImGuiTableColumnFlags.None`
- ImGui requires explicit table or column sizing policy whenever
  `initWidthOrWeight` is specified; otherwise it triggers:
  - `init_width_or_weight <= 0.0f && "Can only specify width/weight if sizing policy is set explicitly in either Table or Column."`

Changes:
- updated `DBManagerUI/Components/DbTableView.cs` so data columns use explicit
  `ImGuiTableColumnFlags.WidthFixed` when passing an initial width
- kept behavior narrow: no table routing, cache, or native-surface behavior
  changes

Validation:
- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\\Echoglossian.Tests.csproj -c Debug --no-build`

## Iteration 20

Goal:
- make the supported toast ToastGui runtime path always active when
  `TranslateToast` is enabled
- remove redundant user-facing controls for legacy capture/runtime toggles
- keep route-state diagnostics visible only in debug builds

Root cause:
- the first toast general-page checkbox (`UseToastGuiCaptureForSupportedToasts`)
  had become operationally redundant for user flows after we stabilized the
  callback-owned path
- exposing hidden route toggles in normal UI increased confusion and made it
  harder to reason about effective runtime ownership

Changes:
- updated `ToastGuiSupportedToastPolicy` so supported runtime activation is now
  driven only by `TranslateToast`
- disabled legacy capture-prefetch policy gates in that policy (`false`)
- removed `UseToastGuiRuntimeForSupportedToasts` from addon-handler
  registration signature refresh inputs (it no longer affects routing)
- removed the toast general-page hidden capture checkbox from public UI
- kept route-status text blocks behind `#if DEBUG` in
  `PluginUI/Tabs/OverlayTab.cs`
- updated toast policy tests and capture-runtime gating test semantics to
  reflect the always-on callback-owned supported-toast route

Validation:
- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\\Echoglossian.Tests.csproj -c Debug --no-build`
