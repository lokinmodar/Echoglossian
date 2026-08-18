# GitHub Issue Backlog

Snapshot date: 2026-08-17

This document is the operational snapshot for open issues in
[`lokinmodar/Echoglossian`](https://github.com/lokinmodar/Echoglossian/issues).
GitHub remains the source of truth for issue state and comments. Published
release history belongs in [`CHANGELOG.md`](../CHANGELOG.md), not in this file.

## Published Release Baseline

- Release: [`v4.2601.0816.1235`](https://github.com/lokinmodar/Echoglossian/releases/tag/v4.2601.0816.1235)
- Product commit: `2901ef0d8b1d68956c6ac86afe97ff3bc098e7f3`
- Official submission: [`goatcorp/DalamudPluginsD17#9213`](https://github.com/goatcorp/DalamudPluginsD17/pull/9213)
- D17 status: approved and merged on 2026-08-16
- D17 merge commit: `1eec8da93bcd024742f5d75f97b67389678d1266`
- Open issues after post-publication triage: 18
- Open issues at the current audit head: 18
- Focused open issues besides the living tracker [#12](https://github.com/lokinmodar/Echoglossian/issues/12): 17

## Current `v4-series` Delta

- Audit head: `origin/v4-series` at `2901ef0d8b1d68956c6ac86afe97ff3bc098e7f3`
- Latest tagged and officially published runtime build on `v4-series`: `v4.2601.0816.1235` at `2901ef0d8b1d68956c6ac86afe97ff3bc098e7f3`
- The audit head currently matches the latest published runtime baseline; there are no newer `v4-series` commits beyond this published release.
- Issue [#12](https://github.com/lokinmodar/Echoglossian/issues/12) remains the living known-issues tracker and should stay aligned with this document whenever focused issue state changes materially.
- Publication of `v4.2601.0816.1235` completed the remaining prompt-persistence and asynchronous glossary-refresh work for [#252](https://github.com/lokinmodar/Echoglossian/issues/252), resolved the per-surface toggle regression from [#270](https://github.com/lokinmodar/Echoglossian/issues/270), and added shutdown-safe live-model discovery cancellation.
- Publication of `v4.2601.0815.1339` completed first-line speaker context recovery from [#214](https://github.com/lokinmodar/Echoglossian/issues/214) and the exact dialogue-glossary preservation defect from [#252](https://github.com/lokinmodar/Echoglossian/issues/252), while broader structured LLM work remains tracked in [#148](https://github.com/lokinmodar/Echoglossian/issues/148).
- Publication of `v4.2601.0809.2000` completed the `_BattleTalk` and `_MiniTalk` stale native-layout stabilization from [#264](https://github.com/lokinmodar/Echoglossian/issues/264), including BattleTalk horizontal-geometry preservation plus the dialogue-family and `Translation Display Mode` label corrections that shipped with that pass.
- Publication of `v4.2601.0809.0046` completed the plugin UI culture-loading correction from [PR #260](https://github.com/lokinmodar/Echoglossian/pull/260), including explicit locale selection and locale-specific resource fallback safeguards.
- Publication of `v4.2601.0807.1730` completed the recycled `_MiniTalk` follow-up for [#246](https://github.com/lokinmodar/Echoglossian/issues/246).
- New performance and persistence architecture work is now tracked in [#258](https://github.com/lokinmodar/Echoglossian/issues/258).
- Draft PR [#193](https://github.com/lokinmodar/Echoglossian/pull/193) remains open only as a separate follow-up if the narrower native `JournalDetail` translated-text reflow work is still wanted.
- The open-issue inventory remains grouped by problem type instead of by workflow state so the remaining work is easier to prioritize by subsystem.

## Recently Resolved Fronts

These closures still define the current known-issues baseline and remain the
most relevant resolved fronts to reference during follow-up triage.

| Issue | Published resolution |
| --- | --- |
| [#270 Quest and map surfaces cannot be disabled with dialogue-only LLM override](https://github.com/lokinmodar/Echoglossian/issues/270) | The published runtime signature and addon-handler registration invalidation now include the dialogue-only override inputs plus Journal, JournalDetail, JournalAccept, JournalResult, RecommendList, and AreaMap toggles, so disabling those surfaces rebuilds the correct runtime state and avoids unwanted token usage. |
| [#214 First dialogue speaker context](https://github.com/lokinmodar/Echoglossian/issues/214) | The published dialogue flow carries the visible speaker through the first translation request and supplements it with persisted quest interlocutor metadata or a unique live-actor fallback when prior conversation history is unavailable. |
| [#252 Prompt and glossary behavior with LLM dialogue translation](https://github.com/lokinmodar/Echoglossian/issues/252) | The published flow protects configured exact glossary terms, persists LLM prompt edits through the shared save path, and refreshes structured dialogue glossaries asynchronously. Existing persisted translations still require explicit retranslation to pick up changed prompt or glossary settings. |
| [#264 `_BattleTalk` height accumulates, width drifts, and `_MiniTalk` background baseline leaks`](https://github.com/lokinmodar/Echoglossian/issues/264) | The published release now restores clean native `_BattleTalk` and `_MiniTalk` baselines after translated reuse, allows `_BattleTalk` vertical growth only when translated height exceeds the clean baseline, and preserves the game's horizontal geometry ownership in `_BattleTalk`. |
| [#246 `_MiniTalk` native bubble dimensions regress in `Native` and `Swap` modes`](https://github.com/lokinmodar/Echoglossian/issues/246) | The published follow-up release now isolates the compact detached-container baseline preference to `_MiniTalk_`, preventing recycled oversized bubble slots from stretching later translated balloons while keeping the shared tooltip and toast behavior unchanged. |
| [#15 ActionDetail / ItemDetail tooltip translation](https://github.com/lokinmodar/Echoglossian/issues/15) | The DB-first structured tooltip flow is active in production with cache-gap prefetch, source-id binding, atomic detail updates, and native/overlay ownership safeguards. |
| [#68 Selection dialogs and other specific surfaces](https://github.com/lokinmodar/Echoglossian/issues/68) | `SelectYesNo`, `SelectOk`, `SelectString`, `SelectIconString`, and `CutSceneSelectString` are shipped; the historical `ChatBubble` entry is covered by the production `_MiniTalk` / `MiniTalk` runtime. |
| [#103 Interactible WorldObjects](https://github.com/lokinmodar/Echoglossian/issues/103) | Eligible nameplate/world-object presentation now supports native translation plus distance-aware overlay fallback with configurable scaling, fading, and cutoff behavior. |
| [#206 `{targetLanguage}` preview](https://github.com/lokinmodar/Echoglossian/issues/206) | Prompt preview and runtime target-language state now use the configured target language instead of falling back to Japanese. |
| [#217 Diacritics fallback metadata](https://github.com/lokinmodar/Echoglossian/issues/217) | Native replacement eligibility is represented by explicit language metadata while remaining opt-in and excluded from overlay and texture-backed presentation. |
| [#230 Position-aware toast controls](https://github.com/lokinmodar/Echoglossian/issues/230) | Toast behavior can be configured by toast type and runtime placement without breaking native, overlay, or swap semantics. |
| [#231 Quest retriggering, cache invalidation, and UI reapplication](https://github.com/lokinmodar/Echoglossian/issues/231) | Quest-family state refreshes across acceptance, progression, language changes, recycled addons, and partial translation availability. |
| [#232 Remaining `Journal*` and recommendation-family surfaces](https://github.com/lokinmodar/Echoglossian/issues/232) | Translation application was restored across the remaining Journal, recommendation, scenario, and related quest consumers. |
| [#233 Surface-aware translation logging](https://github.com/lokinmodar/Echoglossian/issues/233) | Translation diagnostics identify the requesting surface/runtime for requests, reuse, skips, failures, and application decisions. |
| [#234 Google AI Studio compatibility](https://github.com/lokinmodar/Echoglossian/issues/234) | The Google provider audit, configuration-label clarification, and compatible model-selection follow-up are published. |
| [#139 Arabic Translation Support](https://github.com/lokinmodar/Echoglossian/issues/139) | Plugin-owned overlays and hover tooltips support Arabic and other RTL or complex-script languages through texture-backed shaping, bidi ordering, right alignment, adaptive sizing, and bounded caches. Native FFXIV UI RTL remains separate Phase B research. |
| [#174 Retranslate saved texts](https://github.com/lokinmodar/Echoglossian/issues/174) | Translation reuse and persistence are scoped by client source language, target language, and engine policy. Language changes invalidate incompatible runtime state and permit explicit retranslation. |
| [#189 MSQ bar and mission windows untranslated](https://github.com/lokinmodar/Echoglossian/issues/189) | Quest-family handlers use source-scoped state and incremental application across `ScenarioTree`, `RecommendList`, `JournalAccept`, `JournalDetail`, and `ToDoList`. |
| [#204 OpenRouter not translating](https://github.com/lokinmodar/Echoglossian/issues/204) | Shared prompt expansion performs one safe placeholder pass and no longer reinterprets placeholder-like content introduced by an earlier replacement. |
| [#207 ToDoList not translating](https://github.com/lokinmodar/Echoglossian/issues/207) | `ToDoList` applies available translations without waiting for every visible quest and scopes runtime state to the current source and target languages. |

## Additional Recent Closures

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
| [#237 Guildleves](https://github.com/lokinmodar/Echoglossian/issues/237) | Add translation coverage for the `GuildLeve` addon with current persistence, cache, and native/overlay/swap semantics. |

### Reference-Text And Sheet Coverage

| Issue | Current reason |
| --- | --- |
| [#238 Triple Triad card text](https://github.com/lokinmodar/Echoglossian/issues/238) | Add sheet-backed translation and version-aware reuse for names and descriptions from `TripleTriadCard`. |

### Overlay, Toast, And Presentation Behavior

| Issue | Current reason |
| --- | --- |
| [#167 Overlay-only dialogue glitches](https://github.com/lokinmodar/Echoglossian/issues/167) | Still awaiting a fresh retest with display mode, screenshot, and log if native text continues to change or glitch. |
| [#175 Overlay problem](https://github.com/lokinmodar/Echoglossian/issues/175) | Still awaiting a fresh retest with saved display mode, affected addon, and startup/runtime log if the overlay remains absent. |

### Translation Engines, Prompts, And Provider Support

| Issue | Current reason |
| --- | --- |
| [#148 Structured LLM input/output](https://github.com/lokinmodar/Echoglossian/issues/148) | The published release adds speaker metadata, deterministic glossary protection, and capability-aware structured requests, but the broader requested Auto/Structured/PlainText strategy, fallback, and optional session contract remains incomplete. |
| [#176 Local LLM latency](https://github.com/lokinmodar/Echoglossian/issues/176) | The reported approximately one-second overhead still needs focused profiling and acceptance evidence. |
| [#203 Echoglossian not translating](https://github.com/lokinmodar/Echoglossian/issues/203) | Still awaiting a fresh engine-by-engine retest with client language, target language, surface, and log evidence. |
| [#209 Dialogue context controls](https://github.com/lokinmodar/Echoglossian/issues/209) | User-facing disable or limit controls for local LLM context are not implemented. |
| [#212 DeepL `TooManyRequests`](https://github.com/lokinmodar/Echoglossian/issues/212) | The supplied log indicates provider rate limiting. Backoff, classification, and user-facing behavior need focused triage. |

### Compatibility, Diagnostics, And UX Polish

| Issue | Current reason |
| --- | --- |
| [#173 CharacterPanelRefined incompatibility](https://github.com/lokinmodar/Echoglossian/issues/173) | Originating user report remains unverified against the compatibility work requested by #179. |
| [#179 CharacterPanelRefined analysis](https://github.com/lokinmodar/Echoglossian/issues/179) | Explicit compatibility engineering task remains incomplete. |
| [#192 Configuration screenshots](https://github.com/lokinmodar/Echoglossian/issues/192) | Documentation and UI enhancement has not been implemented. |
| [#239 Generic batched translation fallback hardening](https://github.com/lokinmodar/Echoglossian/issues/239) | The published narrow fix rejects unchanged batch echoes, but structured transport, provider-echo classification, and negative-result cooldown remain open. |

### Performance, Scheduling, And Persistence Architecture

| Issue | Current reason |
| --- | --- |
| [#258 Runtime DB access and prefetch architecture](https://github.com/lokinmodar/Echoglossian/issues/258) | New RCA and remediation track for synchronous EF/SQLite work in runtime callbacks, unbounded prefetch, queue pressure, and plugin-correlated frametime pulses. |

## Complete Open-Issue Inventory

This table is the countable audit of all 18 open issues at the snapshot date, including the living meta-tracker [#12](https://github.com/lokinmodar/Echoglossian/issues/12).

| Problem type | Issues | Count |
| --- | --- | ---: |
| Meta tracking and living documentation | #12 | 1 |
| Quest-family runtime and surfaces | #104, #171, #172, #237 | 4 |
| Reference-text and sheet coverage | #238 | 1 |
| Overlay and presentation behavior | #167, #175 | 2 |
| Translation engines, prompts, and provider support | #148, #176, #203, #209, #212 | 5 |
| Compatibility, diagnostics, and UX polish | #173, #179, #192, #239 | 4 |
| Performance, scheduling, and persistence architecture | #258 | 1 |
| **Total open at snapshot** |  | **18** |

## Next Actions

1. Keep the remaining mixed symptoms in [#171](https://github.com/lokinmodar/Echoglossian/issues/171) and [#172](https://github.com/lokinmodar/Echoglossian/issues/172) narrowed now that the focused quest-runtime issues shipped.
2. Prioritize the new concrete coverage work in [#237](https://github.com/lokinmodar/Echoglossian/issues/237) and [#238](https://github.com/lokinmodar/Echoglossian/issues/238).
3. Continue the remaining structured LLM strategy and fallback work in [#148](https://github.com/lokinmodar/Echoglossian/issues/148) without reopening the published #214 speaker-context and #252 exact-glossary fixes.
4. Continue the structural remediation in [#239](https://github.com/lokinmodar/Echoglossian/issues/239) beyond the already published unchanged-batch fallback fix.
5. Decide whether [#258](https://github.com/lokinmodar/Echoglossian/issues/258) should stay as one bounded performance architecture front or be split further once the first remediation tranche lands.
6. Decide whether draft PR [#193](https://github.com/lokinmodar/Echoglossian/pull/193) should be reconciled, retargeted, or closed now that [#181](https://github.com/lokinmodar/Echoglossian/issues/181) itself is resolved.
7. Monitor reporter retests on [#167](https://github.com/lokinmodar/Echoglossian/issues/167), [#175](https://github.com/lokinmodar/Echoglossian/issues/175), and [#203](https://github.com/lokinmodar/Echoglossian/issues/203) and close only after the exact symptom is validated or replaced by a more focused issue.
8. Re-run the open-issue audit and refresh [#12](https://github.com/lokinmodar/Echoglossian/issues/12) whenever issue state changes again or when the next runtime or provider-support tranche lands.
