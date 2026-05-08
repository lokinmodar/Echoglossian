# GitHub Issue Backlog

Snapshot date: 2026-05-08

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

## Fixes Landed In Code, Awaiting Published Validation

- `#186` Randomly stops translating to PT-BR and displays English text instead
- `#174` Translate already saved translated texts does not work
- `#181` Prevent TextNode Flags corruption while reading them
- `#191` Talk text is translated multiple time

Notes:

- The current fix for this cluster stops persisting transient exact-failure
  rows such as `empty-result` / synthetic translation-error placeholders.
- Dialogue-family DB rows that merely echo the original source text across
  different languages are now ignored on lookup and skipped on save.
- This should reduce sticky English fallbacks and stale "no translation"
  behavior without requiring DB cleanup in normal cases.
- `#191` also has a branch fix now:
  - translator-local LLM caches were hardened to use a shared concurrent
    request cache instead of raw `Dictionary<string, string>`
  - `TalkHandler` now rechecks the DB before inserting, which should reduce
    duplicate `TalkMessage` rows for the same source line

## P0: Urgent and Likely Short Fix

These are the best immediate targets because they are blocking, widespread, or
highly visible and appear to have a narrow root cause.

### #190 Seleção de mecanismo de tradução não está funcionando corretamente

- Priority: P0
- Ease: medium
- Status: active regression with fresh user confirmations
- Why it is first:
  - Reports say the selected engine does not match the engine actually used.
  - Users are seeing "API key not configured" for providers they did not pick.
  - A new comment suggests the symptom is broader than Google-only and may
    still affect OpenAI/ChatGPT selection as well.

### #169 Overlay doesn't appear

- Priority: P0
- Ease: medium
- Status: active regression
- Notes:
  - The plugin can appear "dead" even when translation itself is working.
  - This is one of the most visible remaining post-release failures.
  - It likely affects both `#175` and part of the perceived "nothing works"
    cluster after clean installs.

### #175 Overlay problem

- Priority: P0
- Ease: medium
- Status: likely same cluster as `#169`
- Notes:
  - Keep linked to `#169` until proven distinct.
  - Reports point to translation working while overlay rendering or visibility
    fails.

## P1: Urgent but Medium Investigation

These are still release-quality problems, but they likely need a more careful
 runtime pass than the P0 items above.

### #186 Randomly stops translating to PT-BR and displays English text instead

- Priority: P1
- Ease: medium
- Status: fixed in code, awaiting published-build confirmation
- Notes:
  - This was treated as the visible edge of the same stale-cache / stale-DB /
    transient-failure persistence cluster affecting `#178` and `#174`.
  - Keep open until the published build confirms the English fallback no longer
    becomes sticky for exact dialogue lines.

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

### #171 Deepseek translation is not available... mission titles and descriptions are not being translated

- Priority: P1
- Ease: medium
- Status: partly addressed, partly still active
- Notes:
  - The persisted `[Translation Error: ...]` path was already fixed in code.
  - Remaining symptoms may collapse into `#170` and `#174`.
  - Reassess after the next published build and engine-migration fix.

### #189 Barra de Próxima MSQ e Janela de Missão sem tradução

- Priority: P1
- Ease: medium
- Status: fresh regression report
- Notes:
  - This looks like missing translation coverage on quest-facing HUD windows
    that users expected from prior releases.
  - Likely intersects `ScenarioTree`, quest tracker, or game-window coverage,
    but should be treated as a visible release regression until verified.

### #188 Translated texts that go beyond the small dialogue boxes

- Priority: P1
- Ease: medium/hard
- Status: active layout bug
- Notes:
  - This is the dense small-dialogue layout family rather than generic overlay
    failure.
  - Closely related to `#187` and likely shares the same native reflow gap.

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
- Status: partially addressed in code, still open
- Notes:
  - Dynamic quest objective updates and slot text reuse were already fixed in
    code and need release validation.
  - Remaining risk is the NPC dialogue/native layout path and possibly
    selection-dialog sizing.

### #187 MiniTalk text extrapolates balloon size when using Native UI replacement

- Priority: P2
- Ease: medium/hard
- Status: active native-layout bug
- Notes:
  - This is the explicit `MiniTalk` variant of the same "translated text no
    longer fits the native box" problem.
  - Keep paired with `#188` until we know whether a shared reflow strategy can
    handle both surfaces.

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
  - This likely sits beneath part of the `Talk` and `JournalDetail` layout
    corruption reports.
  - The issue is sensitive because the bug appears to happen on read, not only
    on write, which points to text-node flag handling or access patterns.
  - Important, but riskier and less likely to be a quick hotfix than `#182`
    or the remaining overlay/settings items.

## P3: Important, Not Immediate Release Blockers

### #176 [Performance] Overhead de ~1s entre captura do texto e exibição da tradução com LLM local

- Priority: P3
- Ease: medium/hard
- Status: valid performance backlog
- Notes:
  - This needs investigation across capture latency, request overhead,
    prompt size, and presentation timing.
  - Important, but not more urgent than bootstrap/load/overlay failures.

## Long-Term Product Backlog

These remain open on purpose and still represent real feature or architecture
work rather than release fallout.

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

1. `#170`
2. `#190`
3. verify published release outcome for `#168`
4. verify published release outcome for `#177`
5. `#169` + `#175` as one overlay cluster
6. `#174` + `#178` as one cache/settings cluster
7. reassess `#171` after `#170` and `#174`
8. `#189`
9. `#188` + `#187` as one native-dialogue sizing cluster
10. `#182`
11. `#183`
12. release-validate and then close `#170`
13. release-validate and then close `#180`
14. release-validate `#191`
15. `#167`
16. release-validate the remaining open parts of `#172`
17. `#181`
18. `#173` / `#179`
19. `#176`
20. long-term backlog `#148`, `#139`, `#104`, `#103`, `#68`, `#15`
