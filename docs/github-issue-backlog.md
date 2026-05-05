# GitHub Issue Backlog

Snapshot date: 2026-05-04

This document is a lightweight backlog snapshot derived from the current open
GitHub issues. It is meant to keep release fallout separate from medium-term
feature work.

## Immediate 4.x Release Follow-up

These issues are active release-quality or rollout problems and should be
triaged before lower-priority feature work.

### #170 Plugin failed to load

- Status: active triage, `needs more info`
- Notes:
  - Reporter already confirmed reinstalling, disabling other plugins, and
    adding `%appdata%\XIVLauncher` to Microsoft Defender exceptions did not
    resolve the issue.
  - This may be distinct from the "config missing blocks UI" bug fixed in
    `7d2360d`.

### #169 Overlay doesn't appear

- Status: active regression
- Notes:
  - User reports overlay mode enabled but no visible overlay output.
  - Likely belongs to overlay bootstrap, configuration, visibility, or asset
    readiness investigation rather than translation-engine work.

### #168 The plugin isn't opening

- Status: active, likely addressed by `7d2360d`
- Notes:
  - This matches the first-launch/config bootstrap failure where a missing
    config plus asset gating prevented the UI from opening.
  - Verify against the next published build and close once the fix is
    confirmed in release.

### #167 Dialogue text glitches when using overlay translation only

- Status: active regression
- Notes:
  - Most likely belongs to the Talk/BattleTalk overlay-only path and should be
    investigated as a native-mutation or incomplete-restore leak.

## Active Product Backlog

These remain open on purpose and still represent real feature or architecture
work.

### #148 Structured input and output for glossary and metadata

- Status: keep open
- Scope:
  - LLM prompt and output shaping
  - richer glossary and metadata flow
  - likely future translation-engine enhancement work

### #139 Arabic Translation Support

- Status: keep open
- Notes:
  - Engine-side translation support is not enough on its own.
  - Proper overlay and UI support still depends on right-to-left rendering
    remediation.

### #104 Add quest translations to the Unending Journey

- Status: keep open
- Notes:
  - Still valid quest-family backlog item.

### #103 Translate Interactible WorldObjects

- Status: keep open
- Notes:
  - Still valid gameplay/UI capture backlog item.

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
  - This intersects the currently disabled structured tooltip path.
  - `ActionDetail` and `ItemDetail` remain off for release safety, so this is
    not done.

## Tracking and Meta

### #12 Current known issues

- Status: keep open
- Purpose:
  - top-level known-issues tracker
  - currently used to preserve the RTL limitation and point users to the issue
    tracker plus changelog

## Working Priority

Suggested priority order:

1. `#170` load failure triage
2. `#169` overlay visibility regression
3. `#168` verify and close after published fix confirmation
4. `#167` Talk/BattleTalk overlay-only glitch investigation
5. `#15` only after structured tooltip work is re-enabled safely
6. backlog items `#148`, `#139`, `#104`, `#103`, `#68`
