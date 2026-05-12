# GitHub Issue Backlog

Snapshot date: 2026-05-11

This document is a lightweight backlog snapshot derived from the current open
GitHub issues. It is meant to keep release fallout separate from medium-term
feature work.

When the user asks to "read the issues" or "update the issue list", the
default source of truth for that request is the open issue tracker in
`lokinmodar/Echoglossian`.

## Triage Model

Priority here is sorted by two factors:

1. User impact and release risk
2. Ease of delivering a narrow, low-risk fix

The first bucket is not "most important only"; it is "most important and
likely to be the next best use of engineering time right now."

## Recently Closed In `4.2600.0605`

- `#168` The plugin isn't opening
- `#170` Plugin failed to load
- `#177` "Waiting to stored quest data" notification
- `#180` Remove need for downloaded font assets for languages that donot use them
- `#182` Translation inconsistent - quest tracker sometimes doesn't translate
- `#183` Align Version numbering to goatcorp standards

## Recently Closed In `4.2600.0605`

- `#178` It isn't translating anything

Notes:

- `4.2600.0605` is the first published release that includes:
  - hot refresh/runtime reconfig without unload/reload
  - fix for the config UI save loop
  - translation activation guard
  - persistent config-fix notification flow
  - engine selection / migration fix
  - Amazon translator hardening
- This makes `#178` the clearest issue now resolved by a currently published
  build, rather than only by local code.

## Resolved In Submitted `4.2600.1105.x`

- `#186` Randomly stops translating to PT-BR and displays English text instead
- `#190` Seleção de mecanismo de tradução não está funcionando corretamente
- `#191` Talk text is translated multiple time
- `#195` Translation stuck on Gemini engine regardless of configuration settings

Notes:

- `4.2600.1105.x` is the current submitted release package and is tracked in
  the official repo submission
  [PR #8626](https://github.com/goatcorp/DalamudPluginsD17/pull/8626).
- This package includes:
  - engine-selection stabilization that keeps `ChosenTransEngine` and
    `ChosenTransEngineKey` synchronized
  - translator-local concurrency hardening for the LLM cache path
  - `TalkHandler` DB recheck-before-insert behavior to reduce duplicate talk
    rows
  - transient dialogue-failure persistence guards so exact-failure placeholders
    and cross-language original-text echoes stop becoming sticky fallbacks

## Fixed In Code, Awaiting Published Validation

These issues already have fixes merged into `v4-series`, but should stay open
until a published Dalamud release confirms the behavior in the field.

### #198 Texts are translated multiple times when OpenAI model is changed

- Priority: tracked
- Ease: done in code
- Status: fix landed in `v4-series`, not yet validated in a published release
- Notes:
  - The newer reproduction steps narrow this down to runtime-refresh listener
    accumulation, not translator-local dictionary concurrency.
  - The first follow-up fix stabilized register/unregister delegate identity.
  - A second review follow-up fixed the shared-handler case where the same
    handler instance is registered for multiple addon names and only the first
    unregister used to succeed.
  - Keep this open until the next published build confirms that repeated model
    changes no longer multiply live talk translations or token usage.

## P0: Urgent and Likely Next Targets

These are the best immediate targets because they are blocking, widespread, or
highly visible and appear to have a narrow root cause.

### #189 Barra de Próxima MSQ e Janela de Missão sem tradução

- Priority: P0
- Ease: medium
- Status: active regression
- Why it is first:
  - This is a visible quest-facing regression in one of the most commonly seen
    gameplay surfaces.
  - If coverage regressed for the next-MSQ bar / mission window cluster, users
    read that as "quest translation is broken" even when deeper systems still
    work.
  - A new follow-up comment narrows this down to the quest-family surfaces
    around `ScenarioTree`, `Recommendations`, `JournalAccept`, and related
    mission-window coverage.
  - This is now better scoped as a concrete quest-surface coverage regression,
    not a generic "plugin stopped translating" report.

## P1: Urgent but Medium Investigation

These are still release-quality problems, but they likely need a more careful
 runtime pass than the P0 items above.

### #188 Translated texts that go beyond the small dialogue boxes

- Priority: P1
- Ease: medium/hard
- Status: active layout bug
- Notes:
  - This is now clearly part of the native reflow/layout family rather than a
    generic overlay failure.
  - The current `JournalDetail` probe work shows that these surfaces need
    explicit wrapper/container/scroll reflow, not only text-node resizing.
  - Keep paired with `#187` until a shared reflow helper lands on both.
  - This is the clearest user-facing umbrella for the small native boxes that
    cannot accommodate more verbose translated text.

### #187 MiniTalk text extrapolates balloon size when using Native UI replacement

- Priority: P1
- Ease: medium/hard
- Status: active native-layout bug
- Notes:
  - This is the explicit `MiniTalk` variant of the same "translated text no
    longer fits the native box" problem.
  - The new native text-flow reflow helper was introduced with `JournalDetail`
    specifically so `MiniTalk` can reuse that strategy next.
  - A fresh follow-up confirms this also reproduces in Spanish, which makes it
    clearly language-agnostic rather than a PT-BR-specific edge case.
  - Treat this as the first downstream consumer of whatever stable reflow model
    we settle on for `JournalDetail`.

### #175 Overlay problem

- Priority: P1
- Ease: medium
- Status: remaining open overlay-startup symptom
- Notes:
  - `#169` is no longer open, so this is now the only active overlay-visibility
    report left in the release-fallout cluster.
  - The body still points to "translation works but the overlay does not show"
    after reinstall.
  - The only follow-up comment is a user workaround that involves saving config
    and toggling the plugin, which suggests this may overlap with activation /
    refresh timing rather than a pure overlay renderer failure.

### #174 Translate already saved translated texts does not work

- Priority: P1
- Ease: medium
- Status: partially improved in published builds, still open
- Notes:
  - Users expect settings or engine changes to affect already stored rows.
  - Part of this overlapped with dialogue rows persisting unchanged source text
    and being reused as if they were valid translations.
  - `4.2600.0605` addressed the hot-refresh / activation / engine-selection
    parts of this complaint, but the cached-row semantics still need reassessment
    before closure.
  - Recent comments keep pointing at DB reuse semantics and lack of a clear
    operator workflow for forcing retranslation after translator experiments.

### #171 Deepseek translation is not available... mission titles and descriptions are not being translated

- Priority: P1
- Ease: medium
- Status: mixed umbrella issue, partly addressed
- Notes:
  - The original report mixes at least two clusters:
    - engine / API-error behavior on DeepSeek
    - mission-title / description coverage and layout failures on other engines
  - A follow-up comment adds Google/Spanish screenshots of text overflowing
    dialogue boxes, which is better categorized with the `#188` / `#187`
    native-layout cluster than with translator-engine availability.
  - Keep this open for now, but treat it as an umbrella report that likely
    decomposes into `#189`, `#174`, and the small-native-box reflow work.

## P2: Release Stabilization, More Involved

These are serious, but they are either more specialized or more likely to
require careful UI/runtime investigation rather than a narrow config fix.

### #167 Dialogue text glitches when using overlay translation only

- Priority: P2
- Ease: medium/hard
- Status: active regression
- Notes:
  - Most likely in the `Talk` / `BattleTalk` overlay-only path.
  - Suspect native state mutation or incomplete restore.

### #172 Google Translation breaks quest and NPC dialogue layout, some quest/FATE text remains untranslated

- Priority: P2
- Ease: medium/hard
- Status: mixed umbrella issue, partially decomposed
- Notes:
  - The dynamic quest objective `0/3` update bug and wrong-quest slot reuse
    comments match the quest-tracker bug family that was already addressed in
    code and previously tracked through `#182`.
  - A later follow-up comment adds a distinct "selection dialogs line is cut
    off" symptom, which points at a separate small-native-box layout problem.
  - The original body also includes the "original English text gets too many
    line breaks" symptom, which aligns with the `#181` read-only/native-state
    corruption investigation.
  - Treat this as a decomposed umbrella issue rather than a single root cause:
    remaining live parts appear to split across selection-dialog sizing and the
    broader `#181` native-layout/runtime-state work.

### #173 Plugin function incompatibility: Character panel refined

- Priority: P2
- Ease: hard
- Status: open user report
- Notes:
  - Game closes when Character translation is enabled with
    CharacterPanelRefined.
  - This likely needs addon-shape compatibility work.

### #179 Analyze compatibility with CharacterPanelRefined

- Priority: P2
- Ease: hard
- Status: tracking issue
- Notes:
  - This is the explicit analysis/backlog issue created for the CPR
    compatibility work.
  - Keep this as the structured engineering item; `#173` is the originating
    user bug report and may later be closed as superseded by `#179`.

### #181 Prevent TextNode Flags corruption while reading them

- Priority: P2
- Ease: hard
- Status: active deep-runtime investigation
- Notes:
  - Part of the read-only mutation issue has already been narrowed and fixed:
    overlay-only / tooltip-only paths no longer rewrite native text just to
    "restore" state they never changed.
  - The unstable native `JournalDetail` reflow work is intentionally isolated
    on `issue-181-journaldetail-reflow` / draft PR `#193`, outside the current
    release branch.
  - The remaining active problem has shifted toward native-mode layout/reflow,
    especially in `JournalDetail`, where verbose translations require wrapper
    and container growth rather than isolated text-node resizing.
  - No new issue comments changed scope here, but the current `JournalDetail`
    investigation now clearly serves as the foundation for later `MiniTalk`
    and small-dialog native reflow fixes.
  - Keep this open until the native reflow family is stable enough that the
    original-text corruption and overlapping layout reports stop reproducing.

## P3: Important, Not Immediate Release Blockers

### #176 [Performance] Overhead de ~1s entre captura do texto e exibição da tradução com LLM local

- Priority: P3
- Ease: medium/hard
- Status: valid performance backlog
- Notes:
  - This needs investigation across capture latency, request overhead,
    prompt size, and presentation timing.
  - The follow-up comment and reply also suggest a second angle beyond raw
    latency: reducing unnecessary context and potentially reusing a single
    ongoing local-LLM conversation/session instead of sending the same large
    fixed prompt every time.
  - Important, but not more urgent than bootstrap/load/overlay failures.

### #192 Add example images for the Game UI elements possible to be translated to each configuration window panel option

- Priority: P3
- Ease: easy/medium
- Status: valid UX/documentation enhancement
- Notes:
  - This is useful for onboarding and configuration clarity, but it is not a
    release-stability blocker.
  - The new translation-surface support matrix can serve as the canonical
    textual inventory alongside any future example-image work.

## Long-Term Product Backlog

These remain open on purpose and still represent real feature or architecture
work rather than release fallout.

### #196 Add Custom OpenAI-Compatible API Support

- Status: keep open
- Scope:
  - custom OpenAI-compatible endpoint support
  - engine/provider configuration expansion
  - likely intersects the broader custom-provider direction already mentioned
    in `#148`

### #148 Structured input and output for glossary and metadata

- Status: keep open
- Scope:
  - LLM prompt and output shaping
  - richer glossary and metadata flow
  - future translation-engine enhancement work

### #139 Arabic Translation Support

- Status: keep open
- Notes:
  - Engine-side support alone is not enough.
  - Proper overlay and UI support still depends on right-to-left rendering
    remediation.

### #104 Add quest translations to the Unending Journey

- Status: keep open

### #103 Translate Interactible WorldObjects

- Status: keep open

### #68 Handling of specific in-game addons

- Status: keep open
- Notes:
  - Treat as rolling addon coverage tracker.
  - Remaining notable items include:
    - `SelectYesNo`
    - `SelectOk`
    - `CutSceneSelectString`
    - `SelectString`
    - `Tooltips`
    - `ChatBubble`

### #15 Move Description translation

- Status: keep open
- Notes:
  - Intersects the currently disabled structured tooltip path.
  - `ActionDetail` and `ItemDetail` remain off for release safety.

## Tracking and Meta

### #12 Current known issues

- Status: keep open
- Purpose:
  - top-level known-issues tracker
  - preserves the RTL limitation
  - points users to the issue tracker plus changelog

## Recommended Execution Order

1. `#189`
2. `#188` + `#187` as one native-dialogue sizing cluster
3. `#175`
4. reassess / release-validate `#174`
5. reassess `#171` only after `#189` and the small-native-box cluster are clearer
6. `#167`
7. release-validate / decompose the remaining live parts of `#172`
8. `#181`
9. `#173` / `#179`
10. `#176`
11. `#192`
12. long-term backlog `#196`, `#148`, `#139`, `#104`, `#103`, `#68`, `#15`
