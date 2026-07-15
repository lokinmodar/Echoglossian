# GitHub Issue Backlog

Snapshot date: 2026-07-15

This document is the operational snapshot for open issues in
[`lokinmodar/Echoglossian`](https://github.com/lokinmodar/Echoglossian/issues).
GitHub remains the source of truth for issue state and comments. Published
release history belongs in [`CHANGELOG.md`](../CHANGELOG.md), not in this file.

## Current Release Gate

- Candidate release: [`v4.2601.0715.1114`](https://github.com/lokinmodar/Echoglossian/releases/tag/v4.2601.0715.1114)
- Product commit: `b584115640c5b9b9bda53001652007a710d892b3`
- Official submission: [`goatcorp/DalamudPluginsD17#9026`](https://github.com/goatcorp/DalamudPluginsD17/pull/9026)
- Current status: open, mergeable, validation checks passing, review required
- Closure rule: do not post the prepared closure comments or close release-bound
  issues until the D17 PR is merged and the version is available in the
  official feed
- Open issue count at this snapshot: 27
- Expected open count after the approved closure batch: 20

## Close After Official Publication

These issues have a direct implementation and validation trail in
`v4.2601.0715.1114`. Close them only after the release gate above is satisfied.

| Issue | Resolution in this release | Closure evidence |
| --- | --- | --- |
| [#139 Arabic Translation Support](https://github.com/lokinmodar/Echoglossian/issues/139) | Phase A texture-backed presentation supports Arabic and other RTL or complex-script languages in plugin-owned overlays and hover tooltips. It includes shaping, bidi ordering, right alignment, adaptive sizing, line-height control, and bounded memory caches. | Release implementation and RTL layout/cache tests. Native FFXIV UI RTL remains separate Phase B research and is not required for the plugin-owned presentation requested by this issue. |
| [#174 Retranslate saved texts](https://github.com/lokinmodar/Echoglossian/issues/174) | Translation reuse is canonically scoped by client source language, target language, and engine policy. Language changes invalidate runtime caches, restore defaults, and permit explicit retranslation instead of reusing incompatible rows. | Persistence, reuse-scope, cache invalidation, and retranslation tests. |
| [#181 TextNode flags corruption](https://github.com/lokinmodar/Echoglossian/issues/181) | Overlay-only and tooltip-only paths preserve native node ownership. `JournalDetail` and dialogue handlers no longer restore or mutate native flags and text unless that path actually changed them. | Native lifecycle and read-only ownership tests, plus the final `JournalDetail` formatting correction. |
| [#189 MSQ bar and mission windows untranslated](https://github.com/lokinmodar/Echoglossian/issues/189) | Quest-family handlers now use source-scoped state and incremental application for `ScenarioTree`, `RecommendList`, `JournalAccept`, `JournalDetail`, and `ToDoList`. | Quest runtime, lifecycle, and partial-application tests. |
| [#204 OpenRouter not translating](https://github.com/lokinmodar/Echoglossian/issues/204) | Shared prompt expansion no longer expands placeholder-like text introduced by a previous replacement. | Commit `36c3565` and translator contract tests cover the reported prompt corruption path. |
| [#207 ToDoList not translating](https://github.com/lokinmodar/Echoglossian/issues/207) | `ToDoList` no longer waits for every quest translation before applying already-resolved entries. | `ToDoListRuntimeAvailabilityTests` and source-scoped runtime tests. |

### Prepared Closure Comments

#### #139

> Implemented in official release `v4.2601.0715.1114`, published through
> `goatcorp/DalamudPluginsD17#9026`. Plugin-owned overlays and hover tooltips now
> support Arabic and other RTL/complex scripts through texture-backed shaping,
> bidi ordering, right alignment, adaptive sizing, and bounded caches. Native
> FFXIV UI RTL remains separate Phase B research. If Arabic still fails on this
> version, please open a focused report with the affected surface, display mode,
> translation engine, font configuration, screenshot, and log.

#### #174

> Fixed in official release `v4.2601.0715.1114`, published through
> `goatcorp/DalamudPluginsD17#9026`. Translation reuse and persistence are now
> scoped by client source language, target language, and engine policy. Language
> changes invalidate runtime caches and restore default text, and the explicit
> retranslation path no longer reuses incompatible saved results. Please open a
> focused report with reproduction steps and DB diagnostics if a stale row still
> survives these scope changes.

#### #181

> Fixed in official release `v4.2601.0715.1114`, published through
> `goatcorp/DalamudPluginsD17#9026`. Overlay-only and tooltip-only rendering now
> leaves native text nodes and flags untouched, while native mutation paths only
> restore state they actually changed. This also includes the `JournalDetail`
> formatting correction and lifecycle coverage. Please report a fresh repro with
> the exact addon and display mode if line breaks or node flags are still altered.

#### #189

> Fixed in official release `v4.2601.0715.1114`, published through
> `goatcorp/DalamudPluginsD17#9026`. The quest window and tracker family now uses
> source-scoped runtime state and applies completed translations incrementally
> across `ScenarioTree`, `RecommendList`, `JournalAccept`, `JournalDetail`, and
> `ToDoList`. Please open a focused report naming the exact surface if one remains
> untranslated on this version.

#### #204

> Fixed in official release `v4.2601.0715.1114`, published through
> `goatcorp/DalamudPluginsD17#9026`. Shared prompt expansion now performs a single
> safe placeholder pass and does not reinterpret placeholder-like content added
> by an earlier replacement. This path is covered by translator contract tests,
> including OpenRouter-compatible prompts. Please attach a current log and prompt
> template in a new issue if the provider still returns the reported fixed text.

#### #207

> Fixed in official release `v4.2601.0715.1114`, published through
> `goatcorp/DalamudPluginsD17#9026`. `ToDoList` now applies each available
> translation without waiting for every visible quest to finish, and its runtime
> state is scoped to the current client and target languages. Remaining dynamic
> progression or slot-reuse symptoms should be reported as a focused current
> repro rather than keeping this original all-or-nothing report open.

## Administrative Closure

### [#12 Current known issues](https://github.com/lokinmodar/Echoglossian/issues/12)

This is an obsolete version-specific tracker. Its maintainer comment already
states that the old tracker should be closed, but the issue remains open. Close
it with the release batch so current status is not duplicated across an issue,
the changelog, and this backlog.

Prepared comment:

> Closing this obsolete version-specific tracker. Published fixes and release
> history are maintained in `CHANGELOG.md`; active triage is maintained in
> `docs/github-issue-backlog.md` and in focused GitHub issues. The current release
> line is `v4.2601.0715.1114`, published through
> `goatcorp/DalamudPluginsD17#9026`.

## Retest Before Closure

The release contains adjacent hardening, but the available reports do not prove
that their exact symptom is resolved. Request a retest after publication and do
not auto-close them.

| Issue | Why it remains open | Prepared retest request |
| --- | --- | --- |
| [#167 Overlay-only dialogue glitches](https://github.com/lokinmodar/Echoglossian/issues/167) | The release enforces read-only native ownership in overlay-only mode, but the report has no detailed addon or current-version reproduction. | `v4.2601.0715.1114` hardens overlay-only lifecycle behavior. Please retest and provide the exact surface (`Talk`, `BattleTalk`, or other), display mode, screenshot, and log if it still reproduces. |
| [#175 Overlay problem](https://github.com/lokinmodar/Echoglossian/issues/175) | The original report has no diagnostics and only a configuration/reload workaround. | Please retest on `v4.2601.0715.1114` with a saved display mode. If the overlay is still absent, attach the relevant configuration fields, affected addon, and startup/runtime log. |
| [#203 Echoglossian not translating](https://github.com/lokinmodar/Echoglossian/issues/203) | Comments report different behavior for Google, Yandex, Gemini, and DeepL. This is not one validated root cause. | Please retest each affected engine separately on `v4.2601.0715.1114` and report source language, target language, engine, surface, and the matching log excerpt. |

## Mixed Issues To Narrow

These issues aggregate independent symptoms. Keep them open after publication,
record which portions were addressed, and ask for separate current reproductions
for what remains.

### [#171 DeepSeek, mission text, layout, and tracker progression](https://github.com/lokinmodar/Echoglossian/issues/171)

The new release addresses source-scoped tracker state, partial translation
application, and several layout/ownership paths. It does not prove resolution
of DeepSeek authentication/runtime errors, selection-dialog clipping, or the
latest dynamic progression report. After publication, comment with the shipped
coverage and request one focused issue per remaining symptom.

### [#172 Google layout and untranslated quest/FATE text](https://github.com/lokinmodar/Echoglossian/issues/172)

The new release addresses quest slot reuse, stale source scoping, incremental
tracker application, and read-only `JournalDetail` formatting. It does not prove
resolution of untranslated FATE text or selection-dialog clipping. Keep the
umbrella open only while collecting fresh reports, then split or close it once
the remaining symptoms have focused issues.

## Must Remain Open

| Issue | Current reason |
| --- | --- |
| [#15 Move Description translation](https://github.com/lokinmodar/Echoglossian/issues/15) | Structured `ActionDetail` and `ItemDetail` native tooltip translation remains disabled for release safety. ActionMenu hover work does not complete this request. |
| [#68 Specific in-game addons](https://github.com/lokinmodar/Echoglossian/issues/68) | Rolling coverage tracker still includes unsupported or incomplete addons such as selection dialogs, chat bubbles, and production native tooltips. |
| [#103 Interactible WorldObjects](https://github.com/lokinmodar/Echoglossian/issues/103) | No delivered implementation in this release. |
| [#104 Unending Journey](https://github.com/lokinmodar/Echoglossian/issues/104) | No delivered implementation in this release. |
| [#148 Structured LLM input/output](https://github.com/lokinmodar/Echoglossian/issues/148) | Foundation work exists, but the requested cross-provider glossary and metadata contract is not complete. |
| [#173 CharacterPanelRefined incompatibility](https://github.com/lokinmodar/Echoglossian/issues/173) | Originating user report remains unverified against the compatibility work requested by #179. |
| [#176 Local LLM latency](https://github.com/lokinmodar/Echoglossian/issues/176) | The reported approximately one-second overhead has no release acceptance evidence and still needs profiling. |
| [#179 CharacterPanelRefined analysis](https://github.com/lokinmodar/Echoglossian/issues/179) | Explicit compatibility engineering task remains incomplete. |
| [#192 Configuration screenshots](https://github.com/lokinmodar/Echoglossian/issues/192) | Documentation and UI enhancement has not been implemented. |
| [#206 `{targetLanguage}` preview](https://github.com/lokinmodar/Echoglossian/issues/206) | Confirmed current defect: the prompt editor preview state still initializes the target language as `Japanese`. This must not be closed with #204. |
| [#209 Dialogue context controls](https://github.com/lokinmodar/Echoglossian/issues/209) | User-facing disable/limit controls for local LLM context are not implemented. |
| [#212 DeepL `TooManyRequests`](https://github.com/lokinmodar/Echoglossian/issues/212) | The supplied log indicates provider rate limiting. Backoff, classification, and user-facing behavior still need focused triage. |
| [#214 First dialogue speaker context](https://github.com/lokinmodar/Echoglossian/issues/214) | New correctness report created after the previous release; no specific implementation in this release. |
| [#215 Dev-only ImGui preview host](https://github.com/lokinmodar/Echoglossian/issues/215) | Explicitly deferred developer tooling. |
| [#217 Diacritics fallback metadata](https://github.com/lokinmodar/Echoglossian/issues/217) | Canonical future task for opt-in native replacement fallback. Issue #208 is already closed in favor of this issue; the comment in #217 that calls #208 open is stale. |

## Complete Open-Issue Inventory

This table is the countable audit of all 27 open issues at the snapshot date.

| Decision | Issues | Count |
| --- | --- | ---: |
| Close after official publication | #139, #174, #181, #189, #204, #207 | 6 |
| Administrative close with release batch | #12 | 1 |
| Request retest before closure | #167, #175, #203 | 3 |
| Keep open and narrow | #171, #172 | 2 |
| Keep open as active or planned work | #15, #68, #103, #104, #148, #173, #176, #179, #192, #206, #209, #212, #214, #215, #217 | 15 |
| **Total open at snapshot** |  | **27** |

## Post-Publication Checklist

1. Confirm `goatcorp/DalamudPluginsD17#9026` is merged.
2. Confirm `v4.2601.0715.1114` is available in the official Dalamud feed.
3. Post the prepared comments and close #12, #139, #174, #181, #189, #204,
   and #207.
4. Post retest requests on #167, #175, and #203 without closing them.
5. Comment on #171 and #172 with the delivered scope and request focused
   reproductions for residual symptoms.
6. Re-run the open-issue audit and update this snapshot and its counts.
