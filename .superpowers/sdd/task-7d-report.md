# Task 7D Report: Correct and Bound Texture-backed Text Presentation

## Scope

Implemented Task 7D production changes only in:

- `ImageGeneration/TextImageRenderer.cs`
- `UIOverlays/TextPresentation/RtlTexturePresentationService.cs`

Direct regression coverage was added in:

- `Echoglossian.Tests/TextImageRendererTests.cs`
- `Echoglossian.Tests/RtlTexturePresentationServiceTests.cs`

No translation, cache implementation, persistence, database, handler, overlay
drawer, hover manager, or language-policy file was edited. Existing overlay and
hover callers already treat a null render block as a pending/fallback state, so
their APIs did not require changes.

## Root-Cause Trace

1. `LanguagePresentationPolicy` intentionally sends Azerbaijani (6), Hausa
   (40), and Kurdish (57) through texture presentation while leaving their RTL
   and right-alignment flags false.
2. `RtlTexturePresentationService` retained the correct outer alignment in
   `RenderedTextBlock`, but `TextTextureGenerator` constructed
   `TextImageRenderer` without any policy direction input.
3. `TextImageRenderer.CreateStringFormat` then unconditionally applied
   `DirectionRightToLeft` and `StringAlignment.Far`, so the bitmap itself was
   already laid out as RTL even when the outer block was left aligned.
4. On every texture cache miss, `TryRender` ran GDI measurement/rasterization,
   PNG encoding, and `CreateFromImageAsync(...).GetAwaiter().GetResult()` inside
   the synchronous cache factory reached from ImGui drawing.
5. Adaptive hover width measurements used a standalone
   `ConcurrentDictionary` keyed by full text and viewport/layout data. It had no
   capacity or eviction path and therefore outlived evicted textures.

## TDD Evidence

### RED

The first focused run failed at compile time for the expected missing contracts:

- no direction-aware `TextImageRenderer.CreateStringFormat` API
- no injectable asynchronous texture-creation seam
- no pending texture state
- no bounded adaptive-width cache capacity or statistics

The failures were `CS0117`, `CS0246`, `CS1660`, `CS1739`, and `CS1061` in the
new direct tests. Production code was changed only after this red run.

### GREEN

The focused presentation run passes 8/8 tests. Coverage verifies:

- LTR raster format uses normal direction and near alignment
- RTL raster format retains right-to-left direction and far alignment
- Azerbaijani, Hausa, and Kurdish creation requests remain LTR
- an unresolved fake upload returns `null` without synchronous waiting
- repeated calls for the same pending key schedule only one creation
- a completed texture is returned with the existing LTR outer alignment
- adaptive width entries evict at a configured two-entry test capacity
- existing compact RTL line-height and adaptive measurement behavior remains
  covered

The non-blocking assertion uses an unresolved `TaskCompletionSource` injection;
it does not use a timing sleep as evidence. The later completion check uses a
condition-based wait only after the non-blocking assertion has passed.

## Implementation

- `TextImageRenderer` now accepts a direction flag that defaults to RTL for
  compatibility with existing callers. The original public constructor
  signature remains intact, while the owned service uses an internal
  direction-aware overload. Its shared measurement/draw `StringFormat` uses
  `DirectionRightToLeft` plus far alignment only for RTL; LTR uses near
  alignment without the RTL flag.
- `RtlTexturePresentationService` derives direction from
  `LanguagePresentationPolicy.ShouldRightAlign`, passes it into rasterization
  and adaptive measurement, and includes `rtl`/`ltr` in the texture key.
- Cache hits remain synchronous and cheap. A cache miss schedules the complete
  raster, PNG, and upload operation on a background task and returns `null`.
- Pending work uses `ConcurrentDictionary<string, Lazy<Task>>`, so repeated
  ImGui frames reuse one scheduled operation per complete layout key.
- Completed textures are inserted into the existing bounded
  `TextTextureCache`; failure cooldown behavior remains in place.
- Clear/dispose generation epochs prevent an in-flight result from repopulating
  cleared or disposed texture state. Such late results are disposed.
- Adaptive hover widths now use a lock-protected 128-entry LRU. The existing
  layout key inputs and adaptive sizing algorithm are preserved.

## Compatibility and Risk

- RTL direction, far alignment, line-height scaling, wrapping, adaptive width,
  texture byte budgets, and outer right alignment remain unchanged for
  completed RTL textures.
- Texture misses now leave the existing overlay/tooltip absent for the pending
  frame(s), matching the callers' existing null fallback behavior rather than
  stalling the ImGui draw path.
- Texture upload is now initiated from scheduled background work. Dalamud's
  asynchronous `ITextureProvider.CreateFromImageAsync` remains responsible for
  upload dispatch.
- In-game verification remains appropriate for Arabic/Persian RTL presentation,
  Azerbaijani/Hausa/Kurdish punctuation and left alignment, and first-frame
  pending behavior in both overlays and hover tooltips.
- The shared worktree contained a modified `Echoglossian.xml` before Task 7D.
  It is excluded from Task 7D staging and commit.

## Validation

Commands and results:

```text
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~TextImageRendererTests|FullyQualifiedName~RtlTexturePresentationServiceTests"
PASS: 8/8

dotnet build Echoglossian.sln -c Debug --no-restore
PASS: 0 errors, 79 existing/concurrent warnings

dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
PASS: 525/525

git diff --check
PASS
```

The build warnings include the existing unavailable Multilingual App Toolkit
warning, `NU1903` for `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, and existing nullable
or obsolete-API warnings. No warning points to a Task 7D-owned file.

After the final documentation-only style pass, another focused retry was
blocked at test-project compilation by concurrent, unstaged changes in
`PrefetchBrokerSourceScopeTests.cs` that reference prefetch dispatch contracts
not yet present in the shared worktree. Task 7D did not edit or stage that
work. The complete 8/8 focused, 0-error build, and 525/525 full-suite results
above were recorded after all Task 7D behavior and compatibility changes.

## Review Hardening

Task 7D review findings were addressed only in the asynchronous texture
presentation boundary and its direct draw callers/tests. No translation,
database, persistence, or `Echoglossian.xml` changes belong to this patch.

### Root Causes

1. Background completion inserted directly into `TextTextureCache`, whose LRU
   immediately disposed evicted wrappers even when a returned render block
   still referenced one for the current ImGui draw.
2. Scheduling captured an epoch under `lifecycleLock` but inserted and started
   `Lazy<Task>` work afterward. `Clear` could therefore retire the epoch before
   stale work entered the pending dictionary, and stale completion removed by
   key without proving ownership of the current entry.
3. The pending dictionary and failed-key cooldown dictionary were unbounded,
   and each admitted unique key owned an independent `Task.Run`.
4. Cache keys independently rounded floats and source `Vector4` colors instead
   of keying the exact resolved font, width, line-height, direction, and final
   GDI ARGB values supplied to rasterization.

### Implementation

- Completed uploads now wait in a generation-owned completion queue and are
  published into the texture LRU only by `TryRender` on the draw thread.
- Cached textures use reference-counted draw leases. LRU eviction, `Clear`, and
  `Dispose` retire ownership immediately but defer the underlying wrapper's
  disposal until overlay or hover drawing releases its lease.
- Each lifecycle generation owns its cancellation source, pending map, bounded
  FIFO, completion queue, and worker count. The default limits are 16 admitted
  requests and two workers. Generation replacement is atomic, old workers
  cannot access the newer generation's map, and the task factory is invoked
  under the generation gate before background GDI work begins.
- Retry cooldown state is a 128-entry LRU and expired entries are pruned on
  unrelated requests, so one-off failed text cannot accumulate indefinitely.
- Texture keys are built from one `TextureCreationRequest` using exact float
  bits, final ARGB integers, exact integer wrap width, direction, and
  length-prefixed strings. Adaptive-width keys likewise use one captured input
  set with exact viewport and configured-width bits.
- Existing pending/null fallback, LTR/RTL direction, outer alignment,
  line-height, wrapping, and adaptive-width behavior are unchanged.

### TDD Evidence

The focused red run failed at compilation for the expected missing
cancellation-aware seam, capacity controls, and retry statistics. Production
code was changed only after that red run.

The deterministic tests use controlled `TaskCompletionSource` gates and no
sleep calls. They verify:

- forced one-entry LRU eviction cannot dispose a held current-draw lease
- `Clear` cancels the old generation, starts the same key again immediately,
  disposes stale output, and prevents stale completion from removing new work
- a two-entry pending queue and one-worker limit apply backpressure while a
  rejected request remains eligible when capacity opens
- three one-off failures retain only two configured cooldown entries
- close font/color inputs and close viewport widths do not alias keys
- existing texture LTR/RTL, cache budget, and tooltip layout coverage remains
  green

Focused review-hardening result:

```text
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~RtlTexturePresentationServiceTests|FullyQualifiedName~TextImageRendererTests|FullyQualifiedName~TextTextureCacheBudgetTests|FullyQualifiedName~HoverTooltipLayoutPolicyTests"
PASS: 26/26 before concurrent unrelated prefetch compilation became incomplete
```

Final repository validation was retried after the owned changes. The shared
worktree was temporarily blocked before tests by concurrent, unstaged issue work in
`NativeUI/Helpers/ActionDetailPrefetchRuntime.cs:304`, which references the
not-yet-defined `RunActionDetailNamePrefetchEntry`. That file and its related
`PrefetchBrokerSourceScopeTests.cs` changes are not owned, edited, or staged by
Task 7D.

To verify Task 7D without stashing, reverting, or incorporating those files, a
disposable local clone of committed `HEAD` was created and only the four owned
C# file diffs were applied. Fresh validation there produced:

```text
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~RtlTexturePresentationServiceTests|FullyQualifiedName~TextImageRendererTests|FullyQualifiedName~TextTextureCacheBudgetTests|FullyQualifiedName~HoverTooltipLayoutPolicyTests"
PASS: 26/26

dotnet build Echoglossian.sln -c Debug --no-restore
PASS: 0 errors, 2 known warnings

dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
PASS: 529/529

git diff --check -- <owned files and report>
PASS
```

After the concurrent prefetch work left the shared working tree, the mandated
commands were rerun there against the final owned files:

```text
dotnet build Echoglossian.sln -c Debug --no-restore
PASS: 0 errors, 78 existing/concurrent warnings

dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
PASS: 534/534
```

## Final Lifecycle Review

The two remaining lifecycle findings were addressed without changing
translation, persistence, native handlers, overlay layout, hover layout, or
`Echoglossian.xml`.

### Root Causes

1. Overlay and hover callers disposed their draw leases immediately after
   `ImGui.Image` submitted commands. Lease counting therefore protected only
   command construction, not the remainder of the host frame.
2. Worker and pending limits belonged to each generation. Replacing a
   generation canceled its token but retained its queue/map, and a provider
   operation that ignored cancellation allowed every later generation to start
   another bounded set of workers.

### Implementation

- Disposed draw leases now enter a service-owned deferred release queue. The
  queue is drained at the beginning of the next `UiBuilder.Draw` callback via
  `BeginDrawFrame`, which is a provably later host frame than the one that
  submitted the image command.
- `Clear` still retires cache ownership immediately, but an evicted wrapper is
  not disposed until its prior-frame leases are released. Final plugin disposal
  drains any remaining releases after retiring cache ownership.
- Worker concurrency is now counted across the entire service rather than per
  generation. A canceled provider call that ignores its token continues to own
  one globally bounded slot; current-generation work remains admitted and
  starts as soon as a slot exits.
- `Clear` and `Dispose` immediately clear the retired generation's queued work
  and keyed pending map. Thus retained stale keys are bounded by running global
  workers, while queued keys belong only to the bounded current generation.

### TDD Evidence

The focused RED run failed for the expected missing contracts:

- `CS1061`: no `BeginDrawFrame`
- `CS1061`: no global active-worker statistic
- `CS1061`: no queued-work statistic

The deterministic regressions use controlled completion signals and verify:

- a lease disposed after simulated image submission survives same-frame LRU
  eviction and is released only when the next draw frame begins
- five repeated clears around a blocked upload retain one global worker, drop
  stale queued keys, and run only the newest admitted request when capacity
  returns
- repeated dispose cancels the running token, removes queued state, and safely
  disposes a late stale upload
- same-key post-clear work remains schedulable without stale completion
  removing or publishing it

Final commands and results:

```text
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~RtlTexturePresentationServiceTests"
PASS: 12/12

dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~RtlTexturePresentationServiceTests|FullyQualifiedName~TextImageRendererTests|FullyQualifiedName~TextTextureCacheBudgetTests|FullyQualifiedName~HoverTooltipLayoutPolicyTests"
PASS: 28/28

dotnet build Echoglossian.sln -c Debug --no-restore
PASS: 0 errors, 2 known warnings

dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
PASS: 524/524
```

In-game verification remains appropriate for repeated configuration clears
while RTL overlays/tooltips are visible and for plugin unload immediately after
a texture-backed frame. No automated concern remains in the owned lifecycle
boundary.
