# GitHub Issue Backlog

Snapshot date: 2026-05-05

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

## P0: Urgent and Likely Short Fix

These are the best immediate targets because they are blocking, widespread, or
highly visible and appear to have a narrow root cause.

### #170 Plugin failed to load

- Priority: P0
- Ease: short/medium
- Status: fixed in code, awaiting published-build validation
- Why it is first:
  - This is hard release breakage.
  - User workaround strongly suggests a concrete root cause: persisted
    `ChosenTransEngine` values from older builds now map to the wrong engine
    after engine-order changes.
  - This likely also explains part of `#171` and `#178`.
- Landed fix shape:
  - config migration/remap for old engine ids
  - bootstrap guard for invalid or newly incompatible engine selections
  - safe fallback translator so engine init failure no longer prevents plugin load
  - legacy ChatGPT base-url normalization moved into the same migration path

### #183 Align Version numbering to goatcorp standards

- Priority: P0
- Ease: short/medium
- Status: urgent enhancement
- Notes:
  - This is not a runtime bug, but it can become a release/process blocker.
  - The version number should stop being fully dynamic and align with the
    conventions expected by the goatcorp plugin release flow.
  - Good candidate for a narrow, isolated fix once the release-breakage
    cluster is stable.

### #168 The plugin isn't opening

- Priority: P0
- Ease: short
- Status: likely already fixed in code
- Notes:
  - This matches the "missing config prevents UI from opening" regression.
  - Code already creates and saves a default config on first launch.
  - Keep high only until the published build confirms it for users, then close.

### #177 "Waiting to stored quest data" notification

- Priority: P0
- Ease: short
- Status: likely already fixed in code
- Notes:
  - Code now has `ShowQuestProgressNotifications`, disabled by default.
  - This should be verified against the next release and then closed.

## P1: Urgent but Medium Investigation

These are still release-quality problems, but they likely need a more careful
 runtime pass than the P0 items above.

### #169 Overlay doesn't appear

- Priority: P1
- Ease: medium
- Status: active regression
- Notes:
  - Translation can still work while overlays remain invisible.
  - Likely tied to overlay visibility/apply-state/config propagation.

### #175 Overlay problem

- Priority: P1
- Ease: medium
- Status: likely same cluster as `#169`
- Notes:
  - Keep linked to `#169` until proven distinct.
  - User comments suggest settings may only take effect after plugin reload.

### #174 Translate already saved translated texts does not work

- Priority: P1
- Ease: medium
- Status: active regression/UX gap
- Notes:
  - Users expect settings or engine changes to affect already stored rows.
  - Reports also suggest some settings do not fully take effect until
    plugin toggle/restart.
  - Likely overlaps with cache invalidation, runtime refresh, and config
    propagation issues.

### #178 It isn't translating anything

- Priority: P1
- Ease: medium
- Status: active regression
- Notes:
  - Could be a downstream symptom of `#170` config/engine mismatch.
  - Could also overlap with `#174` stale cache/settings propagation.
  - Reassess after the `ChosenTransEngine` compatibility fix lands.

### #171 Deepseek translation is not available... mission titles and descriptions are not being translated

- Priority: P1
- Ease: medium
- Status: partly addressed, partly still active
- Notes:
  - The persisted `[Translation Error: ...]` path was already fixed in code.
  - Remaining symptoms may collapse into `#170` and `#174`.
  - Reassess after the next published build and engine-migration fix.

### #182 Translation inconsistent - quest tracker sometimes doesn't translate

- Priority: P1
- Ease: medium
- Status: active regression
- Notes:
  - This is a fresh tracker-specific bug report after the recent quest-family
    stabilization work.
  - Likely intersects `_ToDoList`, `ScenarioTree`, quest cache reuse, or
    background prefetch timing.
  - Treat as a high-value follow-up right after the overlay/settings cluster
    because it is very visible and likely narrower than the Talk-family work.

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

### #180 Remove need for downloaded font assets for languages that donot use them

- Priority: P2
- Ease: medium
- Status: fixed in code, awaiting published-build validation
- Notes:
  - Asset gating is now language-aware instead of global.
  - Languages that do not use downloaded CJK font assets are no longer blocked.
  - Languages that do require them now trigger automatic check/download,
    disable translation until assets are valid, and expose manual recovery UI.
  - Download retry behavior was added for transient fetch failures.

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
2. verify published release outcome for `#168`
3. verify published release outcome for `#177`
4. `#169` + `#175` as one overlay cluster
5. `#174` + `#178` as one cache/settings cluster
6. reassess `#171` after `#170` and `#174`
7. `#182`
8. `#183`
9. release-validate and then close `#170`
10. release-validate and then close `#180`
11. `#167`
12. release-validate the remaining open parts of `#172`
13. `#181`
14. `#173` / `#179`
15. `#176`
16. long-term backlog `#148`, `#139`, `#104`, `#103`, `#68`, `#15`
