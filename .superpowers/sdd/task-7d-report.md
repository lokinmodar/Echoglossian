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
