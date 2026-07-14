# Issue 139 RTL Performance, Usability, and Validation Spec

## Summary

This spec defines the non-functional requirements for the RTL presentation work
on issue `#139`.

The first shipped solution is texture-backed and in-memory. That can work well,
but only if the plugin treats text rendering as a cached presentation artifact
instead of an every-frame operation.

This document therefore defines:

- memory budgets
- latency budgets
- cache and eviction rules
- degradation behavior
- usability expectations
- automated and in-game validation requirements

## Performance Model

The approved `Phase A` model is:

1. translation produces logical text
2. layout and rasterization happen only when the logical text or presentation
   inputs change
3. a cache stores the resulting texture and metrics
4. draw frames only place and draw the cached result

Unapproved model:

- render shaped text into a bitmap every frame
- upload a new texture every frame
- depend on `ImGui.CalcTextSize` to measure RTL text after rasterization

## Latency Budgets

### Warm-path budgets

Once a renderable block already exists in cache:

- per-surface lookup and draw decision should stay below `0.25 ms` on the UI
  thread in normal play
- window placement and draw must not allocate a new bitmap
- no async texture work should be queued on cache hit

### Miss-path budgets

On a true cache miss:

- one miss is acceptable when translated content changes
- repeated misses for unchanged visible text are not acceptable
- one surface must never enqueue more than one active generation job for the
  same request key
- a failed generation must enter a cooldown state instead of retrying every
  frame

### Frame-safety rule

No cache miss may be caused solely by:

- blinking cursor changes
- hover state changes
- window position changes
- repeated calls to the same draw method during the same frame

## Memory Budgets

The cache must be bounded by estimated byte size, not only by entry count.

### Default budgets

- soft budget: `32 MiB`
- hard budget: `48 MiB`
- default inactive-entry TTL: `60 seconds`
- default max entries: `96`

Estimated entry size should use:

`width * height * 4`

for the RGBA texture payload, plus small managed-object overhead.

### Entry constraints

- max texture width: `2048 px`
- max texture height: `2048 px`
- max single-entry area: `2,097,152 px`
- oversized requests must be wrapped, clipped, paged, or rejected explicitly;
  they must not allocate an unbounded texture

### Eviction policy

The cache must implement:

- LRU ordering
- stale-entry pruning by inactivity
- hard-cap enforcement by estimated bytes

When soft budget is exceeded:

- evict from least-recently-used entries until usage falls below soft budget

When hard budget would be exceeded by the new entry:

- evict aggressively first
- if the new entry still cannot fit, reject the request and surface a debug
  warning instead of overcommitting memory

## Cache Key Requirements

The cache key must include every property that changes the rendered result:

- logical text
- language code
- requested direction
- font identity
- font size
- effective scale
- max width
- foreground color
- background color
- alignment
- any title/body style variant that affects layout

The cache key must not include:

- transient window position
- transient hover state
- frame count

## Generation and Threading Rules

### Approved

- CPU-side layout and bitmap generation on content change
- async or background preparation where the underlying API permits it
- one in-flight job per unique cache key

### Not approved

- blocking the draw loop for all visible surfaces on every content change
- unbounded parallel generation for multiple visible texts
- generation retries every frame on failure

### Concurrency guardrails

- max concurrent texture-generation jobs: `4`
- deduplicate concurrent requests for the same key
- keep the last successful renderable block visible while a refresh is pending
  whenever possible

## Degradation Behavior

Failure handling must be explicit and calm.

If a renderable block cannot be generated:

- keep the previous valid texture if one exists
- otherwise show a stable fallback state such as no translated block or a
  short diagnostic placeholder
- enter a cooldown before retrying the same key
- log at debug or warning level, not hot-path spam

Do not fall back to visually broken raw RTL text as if the render were
successful.

## Usability Requirements

### Visual behavior

- RTL body text must align to the right by default
- mixed RTL and LTR text must preserve readable numerals and punctuation
- tooltip titles and separators must preserve the current semantic structure
- multiline wrapping must operate on the chosen backend, not on plain ImGui
  line wrapping alone

### Interaction behavior

- hover hit boxes must not shrink or drift because presentation became
  texture-backed
- overlay click-through behavior must stay unchanged
- no new input capture, focus, or navigation behavior may be introduced by the
  RTL path

### Mode behavior

- RTL languages remain overlay-only in `Phase A`
- native-write display modes must collapse to overlay or tooltip presentation
  for RTL languages
- user-facing settings should communicate this as a supported limitation, not
  an error state

## Automated Validation

### Unit tests

Add coverage for:

- RTL language policy and activation rules
- display-mode collapse to overlay-only behavior
- cache key stability
- cache eviction by byte budget
- stale-entry pruning
- in-flight request deduplication
- fallback and cooldown behavior after generation failure
- width and height calculation from rendered result metadata

Candidate test files:

- `Echoglossian.Tests/RtlLanguagePresentationPolicyTests.cs`
- `Echoglossian.Tests/TextPresentationResolverTests.cs`
- `Echoglossian.Tests/TextTextureCacheBudgetTests.cs`
- `Echoglossian.Tests/TextTextureCacheConcurrencyTests.cs`
- `Echoglossian.Tests/RtlTextureRenderMeasurementTests.cs`
- `Echoglossian.Tests/RtlOverlayPresentationDecisionTests.cs`

### Optional image or golden tests

If the preview workflow becomes available, add golden-image tests or
operator-reviewed sample captures for:

- Arabic dialogue
- Hebrew tooltip
- Persian mixed with Latin numerals
- Urdu multiline wrap

These are useful but not required for the first code slice.

## In-Game Validation Matrix

Minimum manual validation matrix for `Phase A`:

- `Talk`
- `BattleTalk`
- `TalkSubtitle`
- `MiniTalk`
- one toast-family overlay
- hover tooltip path
- structured tooltip overlay path

Languages:

- Arabic
- Hebrew
- Persian
- Urdu

At least one mixed-content case per language:

- numeral-heavy line
- punctuation-heavy line
- RTL text with embedded Latin names or acronyms

Required observations:

- correct visual direction
- acceptable wrapping
- no flicker on stable visible text
- no repeated regeneration while idle
- no obvious frame hitch when translation result first appears
- no runaway memory growth after repeated surface changes

## Instrumentation Requirements

The runtime should expose lightweight diagnostics for development builds:

- cache entry count
- estimated cache bytes
- cache hit and miss counts
- generation failure count
- eviction count
- last generation duration

These do not need to ship as noisy logs. A debugger window or one-shot trace is
preferred.

## Exit Criteria

`Phase A` is acceptable only if:

- warm-path rendering is stable and allocation-light
- cache growth stays inside the defined budgets
- generation is tied to content changes, not frame frequency
- the usability requirements above are met on the in-game validation matrix

## As-Built Status (2026-07-13)

Automated coverage establishes bounded texture/adaptive-width state, one
scheduled creation per pending key, direction-aware layout keys, and
non-blocking cache misses. The implementation enforces the `2048 px` dimension,
`2,097,152 px` area, and `48 MiB` single-entry limits before target bitmap
allocation, PNG encoding, upload, or cache insertion. A normal texture miss
creates one validated layout and reuses it for rasterization; an oversized
layout enters the existing per-key cooldown without retrying every frame.

This does not establish the in-game observations in this document. RTL
alignment, line-height density, long-hover sizing, and dense GameWindow
performance remain manual checks in
`docs/issue-139-canonical-language-validation.md`.
