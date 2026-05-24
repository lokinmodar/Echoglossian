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
