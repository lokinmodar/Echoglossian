# GitHub Issue Backlog

Snapshot date: 2026-07-18

This document is the operational snapshot for open issues in
[`lokinmodar/Echoglossian`](https://github.com/lokinmodar/Echoglossian/issues).
GitHub remains the source of truth for issue state and comments. Published
release history belongs in [`CHANGELOG.md`](../CHANGELOG.md), not in this file.

## Published Release Baseline

- Release: [`v4.2601.0715.1114`](https://github.com/lokinmodar/Echoglossian/releases/tag/v4.2601.0715.1114)
- Product commit: `b584115640c5b9b9bda53001652007a710d892b3`
- Official submission: [`goatcorp/DalamudPluginsD17#9026`](https://github.com/goatcorp/DalamudPluginsD17/pull/9026)
- D17 status: approved and merged on 2026-07-16
- D17 merge commit: `c18449a14f89ff94184516bd3f9e4229bd66c8fb`
- Validation: manifest lint and official build checks passed
- Open issues in the previous snapshot: 27
- Independently closed before this release pass: #215 through PR #223
- Release-pass closures: 6
- Open issues after the post-publication pass: 20
- Open issues at the current audit head: 25

## Current `v4-series` Delta

- Audit head: `origin/v4-series` at `0b30d30cd27ac97ad4d65087f7b0a0d88f775466`
- Latest tagged runtime build on `v4-series`: `v4.2601.0717.0008` at `578bce65cc09deeb23571e0e58ed6306f871d859`
- No new runtime code merged after PR [#228](https://github.com/lokinmodar/Echoglossian/pull/228); the repository baseline for this audit is still the same 2026-07-17 `v4-series` head.
- Issue [#12](https://github.com/lokinmodar/Echoglossian/issues/12) was reopened on 2026-07-18 and should remain the living known-issues tracker, updated whenever focused issue state changes materially.
- GitHub issue triage on 2026-07-18 updated [#15](https://github.com/lokinmodar/Echoglossian/issues/15), [#68](https://github.com/lokinmodar/Echoglossian/issues/68), [#171](https://github.com/lokinmodar/Echoglossian/issues/171), and [#172](https://github.com/lokinmodar/Echoglossian/issues/172).
- GitHub issue triage on 2026-07-18 also created focused follow-up issues [#230](https://github.com/lokinmodar/Echoglossian/issues/230), [#231](https://github.com/lokinmodar/Echoglossian/issues/231), [#232](https://github.com/lokinmodar/Echoglossian/issues/232), [#233](https://github.com/lokinmodar/Echoglossian/issues/233), and [#234](https://github.com/lokinmodar/Echoglossian/issues/234).
- Issue [#181](https://github.com/lokinmodar/Echoglossian/issues/181) was closed as resolved on 2026-07-18. Draft PR [#193](https://github.com/lokinmodar/Echoglossian/pull/193) remains open only as a separate follow-up if the narrower native `JournalDetail` reflow work is still wanted.
- The open-issue inventory is now grouped by problem type instead of by workflow state so the remaining work is easier to prioritize by subsystem.

## Closed After Publication

The release-bound closure comments were posted and these issues were closed on
2026-07-16.

| Issue | Published resolution |
| --- | --- |
| [#139 Arabic Translation Support](https://github.com/lokinmodar/Echoglossian/issues/139) | Plugin-owned overlays and hover tooltips support Arabic and other RTL or complex-script languages through texture-backed shaping, bidi ordering, right alignment, adaptive sizing, and bounded caches. Native FFXIV UI RTL remains separate Phase B research. |
| [#174 Retranslate saved texts](https://github.com/lokinmodar/Echoglossian/issues/174) | Translation reuse and persistence are scoped by client source language, target language, and engine policy. Language changes invalidate incompatible runtime state and permit explicit retranslation. |
| [#189 MSQ bar and mission windows untranslated](https://github.com/lokinmodar/Echoglossian/issues/189) | Quest-family handlers use source-scoped state and incremental application across `ScenarioTree`, `RecommendList`, `JournalAccept`, `JournalDetail`, and `ToDoList`. |
| [#204 OpenRouter not translating](https://github.com/lokinmodar/Echoglossian/issues/204) | Shared prompt expansion performs one safe placeholder pass and no longer reinterprets placeholder-like content introduced by an earlier replacement. |
| [#207 ToDoList not translating](https://github.com/lokinmodar/Echoglossian/issues/207) | `ToDoList` applies available translations without waiting for every visible quest and scopes runtime state to the current source and target languages. |

## Other Closure Since The Previous Snapshot

| Issue | Resolution |
| --- | --- |
| [#215 Dev-only ImGui preview host](https://github.com/lokinmodar/Echoglossian/issues/215) | Closed automatically by merged PR [#223](https://github.com/lokinmodar/Echoglossian/pull/223), which added the isolated standalone previewer, shared overlay rendering, font and RTL preview support, and screenshot export. Current `v4-series` later extended that closed front in PR [#227](https://github.com/lokinmodar/Echoglossian/pull/227) with unified ImGui previewer Phase 1 for the real Config, DB Manager, and Translator Metrics windows. |
| [#181 Prevent TextNode Flags corruption while reading them](https://github.com/lokinmodar/Echoglossian/issues/181) | Closed on 2026-07-18 after the read-only flag corruption fix was accepted. Draft PR [#193](https://github.com/lokinmodar/Echoglossian/pull/193) remains open only as a separate follow-up for narrower native `JournalDetail` translated-text reflow work. |

## Open Issues Grouped By Problem Type

### Meta Tracking And Living Documentation

| Issue | Current reason |
| --- | --- |
| [#12 Current known issues](https://github.com/lokinmodar/Echoglossian/issues/12) | Living meta-tracker that should stay open and mirror the real current plugin state, recent closures, and the focused open fronts that matter to users. |

### Quest-Family Runtime And Surfaces

| Issue | Current reason |
| --- | --- |
| [#104 Unending Journey](https://github.com/lokinmodar/Echoglossian/issues/104) | Planned quest-surface coverage remains undelivered. |
| [#171 DeepSeek, mission text, layout, and tracker progression](https://github.com/lokinmodar/Echoglossian/issues/171) | After the 2026-07-18 split, keep this mixed issue focused on the DeepSeek authentication/runtime side and any remaining mission-text symptom that still lacks its own isolated repro. |
| [#172 Google layout and untranslated quest/FATE text](https://github.com/lokinmodar/Echoglossian/issues/172) | After the 2026-07-18 split, keep this mixed issue only for still-unsplit quest/FATE or selection-dialog repros. The tracker-progression and Journal/recommendation follow-up work moved out to focused issues. |
| [#231 Quest retriggering, cache invalidation, and UI reapplication](https://github.com/lokinmodar/Echoglossian/issues/231) | New focused bug for quest acceptance, progression, cache refresh, and consumer reapply behavior that currently stalls after the first partial translation pass. |
| [#232 Remaining `Journal*` and recommendation-family quest surfaces](https://github.com/lokinmodar/Echoglossian/issues/232) | New focused bug for the remaining `Journal*` and `RecommendList`-style surfaces that still do not reliably apply translated quest text. |

### Surface Coverage And Interaction UIs

| Issue | Current reason |
| --- | --- |
| [#15 ActionDetail / ItemDetail tooltip translation](https://github.com/lokinmodar/Echoglossian/issues/15) | Structured tooltip runtime exists, but `ActionDetail` and `ItemDetail` still need real validation, stabilization, and activation for release use. |
| [#68 Selection dialogs and other specific surfaces](https://github.com/lokinmodar/Echoglossian/issues/68) | Generic coverage tracker is now intentionally narrowed to `SelectYesNo`, `SelectOk`, `SelectString`, `CutSceneSelectString`, and `ChatBubble`. |
| [#103 Interactible WorldObjects](https://github.com/lokinmodar/Echoglossian/issues/103) | No delivered implementation. |

### Overlay, Toast, And Presentation Behavior

| Issue | Current reason |
| --- | --- |
| [#167 Overlay-only dialogue glitches](https://github.com/lokinmodar/Echoglossian/issues/167) | Still awaiting a fresh retest with display mode, screenshot, and log if native text continues to change or glitch. |
| [#175 Overlay problem](https://github.com/lokinmodar/Echoglossian/issues/175) | Still awaiting a fresh retest with saved display mode, affected addon, and startup/runtime log if the overlay remains absent. |
| [#217 Diacritics fallback metadata](https://github.com/lokinmodar/Echoglossian/issues/217) | Canonical future task for opt-in native replacement fallback metadata. Overlay and tooltip paths must remain unaffected. |
| [#230 Position-aware toast controls](https://github.com/lokinmodar/Echoglossian/issues/230) | New focused enhancement for independent toast treatment by toast type and runtime placement, including separate overlay/native/swap behavior and per-placement styling/offset controls. |

### Translation Engines, Prompts, And Provider Support

| Issue | Current reason |
| --- | --- |
| [#148 Structured LLM input/output](https://github.com/lokinmodar/Echoglossian/issues/148) | Foundation work exists, but the requested cross-provider glossary and metadata contract is incomplete. |
| [#176 Local LLM latency](https://github.com/lokinmodar/Echoglossian/issues/176) | The reported approximately one-second overhead still needs focused profiling and acceptance evidence. |
| [#203 Echoglossian not translating](https://github.com/lokinmodar/Echoglossian/issues/203) | Still awaiting a fresh engine-by-engine retest with client language, target language, surface, and log evidence. |
| [#206 `{targetLanguage}` preview](https://github.com/lokinmodar/Echoglossian/issues/206) | Confirmed current defect: the prompt editor preview state initializes the target language as `Japanese`. This was not fixed by #204. |
| [#209 Dialogue context controls](https://github.com/lokinmodar/Echoglossian/issues/209) | User-facing disable or limit controls for local LLM context are not implemented. |
| [#212 DeepL `TooManyRequests`](https://github.com/lokinmodar/Echoglossian/issues/212) | The supplied log indicates provider rate limiting. Backoff, classification, and user-facing behavior need focused triage. |
| [#214 First dialogue speaker context](https://github.com/lokinmodar/Echoglossian/issues/214) | First-line speaker context correctness remains unresolved. |
| [#234 Google AI Studio compatibility](https://github.com/lokinmodar/Echoglossian/issues/234) | New enhancement to verify whether the current Gemini path truly covers Google AI Studio and to add a separate engine if it does not. |

### Compatibility, Diagnostics, And UX Polish

| Issue | Current reason |
| --- | --- |
| [#173 CharacterPanelRefined incompatibility](https://github.com/lokinmodar/Echoglossian/issues/173) | Originating user report remains unverified against the compatibility work requested by #179. |
| [#179 CharacterPanelRefined analysis](https://github.com/lokinmodar/Echoglossian/issues/179) | Explicit compatibility engineering task remains incomplete. |
| [#192 Configuration screenshots](https://github.com/lokinmodar/Echoglossian/issues/192) | Documentation and UI enhancement has not been implemented. |
| [#233 Surface-aware translation logging](https://github.com/lokinmodar/Echoglossian/issues/233) | New diagnostics enhancement so logs can identify which surface/runtime triggered translation requests, cache reuse, skips, failures, and applies. |

## Complete Open-Issue Inventory

This table is the countable audit of all 25 open issues at the snapshot date, including the living meta-tracker [#12](https://github.com/lokinmodar/Echoglossian/issues/12).

| Problem type | Issues | Count |
| --- | --- | ---: |
| Meta tracking and living documentation | #12 | 1 |
| Quest-family runtime and surfaces | #104, #171, #172, #231, #232 | 5 |
| Surface coverage and interaction UIs | #15, #68, #103 | 3 |
| Overlay, toast, and presentation behavior | #167, #175, #217, #230 | 4 |
| Translation engines, prompts, and provider support | #148, #176, #203, #206, #209, #212, #214, #234 | 8 |
| Compatibility, diagnostics, and UX polish | #173, #179, #192, #233 | 4 |
| **Total open at snapshot** |  | **25** |

## Next Actions

1. Prioritize the new focused quest issues [#231](https://github.com/lokinmodar/Echoglossian/issues/231) and [#232](https://github.com/lokinmodar/Echoglossian/issues/232) so the remaining mixed work in [#171](https://github.com/lokinmodar/Echoglossian/issues/171) and [#172](https://github.com/lokinmodar/Echoglossian/issues/172) can continue shrinking.
2. Keep [#68](https://github.com/lokinmodar/Echoglossian/issues/68) narrowed to selection dialogs and similar surfaces, while tooltip activation remains isolated in [#15](https://github.com/lokinmodar/Echoglossian/issues/15).
3. Decide whether draft PR [#193](https://github.com/lokinmodar/Echoglossian/pull/193) should be reconciled, retargeted, or closed now that [#181](https://github.com/lokinmodar/Echoglossian/issues/181) itself is resolved.
4. Monitor reporter retests on [#167](https://github.com/lokinmodar/Echoglossian/issues/167), [#175](https://github.com/lokinmodar/Echoglossian/issues/175), and [#203](https://github.com/lokinmodar/Echoglossian/issues/203) and close only after the exact symptom is validated or replaced by a more focused issue.
5. Re-run the open-issue audit and refresh [#12](https://github.com/lokinmodar/Echoglossian/issues/12) whenever issue state changes again or when the next quest-surface or provider-support tranche lands.
