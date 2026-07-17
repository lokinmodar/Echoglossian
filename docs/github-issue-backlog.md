# GitHub Issue Backlog

Snapshot date: 2026-07-16

This document is the operational snapshot for open issues in
[`lokinmodar/Echoglossian`](https://github.com/lokinmodar/Echoglossian/issues).
GitHub remains the source of truth for issue state and comments. Published
release history belongs in [`CHANGELOG.md`](../CHANGELOG.md), not in this file.

## Published Release

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

## Closed After Publication

The release-bound closure comments were posted and these issues were closed on
2026-07-16.

| Issue | Published resolution |
| --- | --- |
| [#12 Current known issues](https://github.com/lokinmodar/Echoglossian/issues/12) | Obsolete version-specific tracker closed administratively. Current status now lives in focused issues, this backlog, and the changelog. |
| [#139 Arabic Translation Support](https://github.com/lokinmodar/Echoglossian/issues/139) | Plugin-owned overlays and hover tooltips support Arabic and other RTL or complex-script languages through texture-backed shaping, bidi ordering, right alignment, adaptive sizing, and bounded caches. Native FFXIV UI RTL remains separate Phase B research. |
| [#174 Retranslate saved texts](https://github.com/lokinmodar/Echoglossian/issues/174) | Translation reuse and persistence are scoped by client source language, target language, and engine policy. Language changes invalidate incompatible runtime state and permit explicit retranslation. |
| [#189 MSQ bar and mission windows untranslated](https://github.com/lokinmodar/Echoglossian/issues/189) | Quest-family handlers use source-scoped state and incremental application across `ScenarioTree`, `RecommendList`, `JournalAccept`, `JournalDetail`, and `ToDoList`. |
| [#204 OpenRouter not translating](https://github.com/lokinmodar/Echoglossian/issues/204) | Shared prompt expansion performs one safe placeholder pass and no longer reinterprets placeholder-like content introduced by an earlier replacement. |
| [#207 ToDoList not translating](https://github.com/lokinmodar/Echoglossian/issues/207) | `ToDoList` applies available translations without waiting for every visible quest and scopes runtime state to the current source and target languages. |

Issue #181 was intentionally excluded from this closure batch. Its original
read-only flag corruption is fixed, but the native translated-text reflow front
remains open in draft PR
[#193](https://github.com/lokinmodar/Echoglossian/pull/193).

## Other Closure Since The Previous Snapshot

| Issue | Resolution |
| --- | --- |
| [#215 Dev-only ImGui preview host](https://github.com/lokinmodar/Echoglossian/issues/215) | Closed automatically by merged PR [#223](https://github.com/lokinmodar/Echoglossian/pull/223), which added the isolated standalone previewer, shared overlay rendering, font and RTL preview support, and screenshot export. |

## Awaiting Retest

Retest requests for the official release were posted on 2026-07-16. These
issues remain open until the exact reported symptom is validated.

| Issue | Required evidence |
| --- | --- |
| [#167 Overlay-only dialogue glitches](https://github.com/lokinmodar/Echoglossian/issues/167) | Retest the exact `Talk`, `BattleTalk`, or other surface and provide display mode, screenshot, and log if native text still changes or glitches. |
| [#175 Overlay problem](https://github.com/lokinmodar/Echoglossian/issues/175) | Retest with the display mode saved and provide relevant configuration, affected addon, and startup/runtime log if the overlay remains absent. |
| [#203 Echoglossian not translating](https://github.com/lokinmodar/Echoglossian/issues/203) | Retest Google, Yandex, Gemini, and DeepL separately with client language, target language, surface, and matching log excerpt. |

## Mixed Issues To Narrow

Release-scope comments were posted on 2026-07-16. These issues combine multiple
root causes and should remain open only while fresh focused reports are being
collected.

### [#171 DeepSeek, mission text, layout, and tracker progression](https://github.com/lokinmodar/Echoglossian/issues/171)

The release addresses source-scoped tracker state, partial translation
application, and several layout and native-ownership paths. DeepSeek
authentication/runtime errors, selection-dialog clipping, and the latest
dynamic progression symptom still require separate current reproductions.

### [#172 Google layout and untranslated quest/FATE text](https://github.com/lokinmodar/Echoglossian/issues/172)

The release addresses quest slot reuse, stale source scoping, incremental
tracker application, and read-only `JournalDetail` formatting. Untranslated
FATE text and selection-dialog clipping still require focused current
reproductions.

## Active And Planned Work

| Issue | Current reason |
| --- | --- |
| [#15 Move Description translation](https://github.com/lokinmodar/Echoglossian/issues/15) | Structured `ActionDetail` and `ItemDetail` native tooltip translation remains disabled for release safety. ActionMenu hover work does not complete this request. |
| [#68 Specific in-game addons](https://github.com/lokinmodar/Echoglossian/issues/68) | Rolling coverage tracker still includes unsupported or incomplete addons such as selection dialogs, chat bubbles, and production native tooltips. |
| [#103 Interactible WorldObjects](https://github.com/lokinmodar/Echoglossian/issues/103) | No delivered implementation. |
| [#104 Unending Journey](https://github.com/lokinmodar/Echoglossian/issues/104) | No delivered implementation. |
| [#148 Structured LLM input/output](https://github.com/lokinmodar/Echoglossian/issues/148) | Foundation work exists, but the requested cross-provider glossary and metadata contract is incomplete. |
| [#173 CharacterPanelRefined incompatibility](https://github.com/lokinmodar/Echoglossian/issues/173) | Originating user report remains unverified against the compatibility work requested by #179. |
| [#176 Local LLM latency](https://github.com/lokinmodar/Echoglossian/issues/176) | The reported approximately one-second overhead still needs focused profiling and acceptance evidence. |
| [#179 CharacterPanelRefined analysis](https://github.com/lokinmodar/Echoglossian/issues/179) | Explicit compatibility engineering task remains incomplete. |
| [#181 TextNode flags and native `JournalDetail` reflow](https://github.com/lokinmodar/Echoglossian/issues/181) | The read-only corruption is fixed. Draft PR #193 remains open for the narrower native translated-text reflow and requires long-text in-game acceptance. |
| [#192 Configuration screenshots](https://github.com/lokinmodar/Echoglossian/issues/192) | Documentation and UI enhancement has not been implemented. |
| [#206 `{targetLanguage}` preview](https://github.com/lokinmodar/Echoglossian/issues/206) | Confirmed current defect: the prompt editor preview state initializes the target language as `Japanese`. This was not fixed by #204. |
| [#209 Dialogue context controls](https://github.com/lokinmodar/Echoglossian/issues/209) | User-facing disable or limit controls for local LLM context are not implemented. |
| [#212 DeepL `TooManyRequests`](https://github.com/lokinmodar/Echoglossian/issues/212) | The supplied log indicates provider rate limiting. Backoff, classification, and user-facing behavior need focused triage. |
| [#214 First dialogue speaker context](https://github.com/lokinmodar/Echoglossian/issues/214) | First-line speaker context correctness remains unresolved. |
| [#217 Diacritics fallback metadata](https://github.com/lokinmodar/Echoglossian/issues/217) | Canonical future task for opt-in native replacement fallback. Issue #208 is already closed in favor of this issue. |

## Complete Open-Issue Inventory

This table is the countable audit of all 20 open issues at the snapshot date.

| Decision | Issues | Count |
| --- | --- | ---: |
| Awaiting reporter retest | #167, #175, #203 | 3 |
| Keep open and narrow | #171, #172 | 2 |
| Active or planned work | #15, #68, #103, #104, #148, #173, #176, #179, #181, #192, #206, #209, #212, #214, #217 | 15 |
| **Total open at snapshot** |  | **20** |

## Next Actions

1. Monitor reporter responses on #167, #175, and #203 and close only after the
   exact symptom is validated or a focused replacement issue exists.
2. Split remaining current symptoms from #171 and #172 into focused issues.
3. Complete native `JournalDetail` reflow in PR #193 before closing #181.
4. Prioritize confirmed direct defects #206 and #214 ahead of broad enhancement
   work.
5. Re-run the open-issue audit whenever issue state or release acceptance
   changes.
