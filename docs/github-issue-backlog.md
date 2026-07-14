# GitHub Issue Backlog

Snapshot date: 2026-07-11

This document is a lightweight backlog snapshot derived from the current open
GitHub issues. It is meant to keep release fallout separate from medium-term
feature work.

When the user asks to "read the issues" or "update the issue list", the
default source of truth for that request is the open issue tracker in
`lokinmodar/Echoglossian`.

## Publication Update (2026-07-11)

This pass is a targeted post-publication update after
`v4.2601.0710.1250` went live through the official
`DalamudPluginsD17` feed.

It rechecked the current open issue list (27 open issues at review time) and
re-read the issue comments for `#148`, `#174`, `#176`, `#196`, and `#201` to
separate what is now officially published and closeable from what remains open
as broader follow-up work.

Delta since the 2026-06-20 pass:

- official `v4.2601.0710.1250` is now live after
  [PR #9006](https://github.com/goatcorp/DalamudPluginsD17/pull/9006) merged
  on 2026-07-11
- `#196` and `#201` move from active product-direction backlog to
  published-and-closed release outcomes
- `#148` remains open because the shipped structured-dialogue work is only the
  first foundation slice, not the full glossary and metadata scope described
  in the issue
- `#174` and `#176` remain open because the release improved operator tooling
  and prompt/context behavior, but the open issue comments still point at
  broader persistence and latency follow-up rather than a clean validated
  closure
- no new issues were opened and 27 issues remain open

### Release-validated in official `v4.2601.0531.0115` and closed

- `#187` MiniTalk text extrapolates balloon size in native replacement mode
- `#188` Text overflow in small dialogue boxes

Notes:

- `v4.2601.0531.0115` is now live in official `DalamudPluginsD17` after
  [PR #8789](https://github.com/goatcorp/DalamudPluginsD17/pull/8789) merged
  on 2026-06-01.
- Both issues were closed with release-validation comments and explicit reopen
  guidance for reports on `v4.2601.0531.0115` or newer.
- Overflow/layout reports embedded in umbrella issues (`#171`, `#172`) are
  still treated as mixed symptoms until fresh post-release repro confirms what
  remains.

### Active regression cluster still reported in comments

- `#206` `{targetLanguage}` variable regression (fresh 2026-06-01 comments say
  it still resolves to `Japanese` even after updating and refreshing resources)
- `#207` quest tracker / ToDoList Portuguese regression on official build
- `#203` mixed engine behavior (Google/Yandex partial recovery, Gemini/DeepL
  failures reported)
- `#204` OpenRouter translation failure (no new comment yet, still open)
- `#208` Turkish rendering issue (newer report, no follow-up comments yet)
- `#212` DeepL free-tier `TooManyRequests` report on official `4.2601.0516.1152`
- `#171` currently mixes DeepSeek auth/runtime errors with additional quest
  tracker progression reports
- `#172` dynamic objective text staleness and cross-quest text mix in tracker

### Product-direction backlog (not pure break/fix)

- `#209` local LLM context limiting/disable controls
- `#148` structured input/output for glossary and metadata

## Recently Closed In Published `4.2601.0710.1250`

This package is now live in the official Dalamud feed after
[PR #9006](https://github.com/goatcorp/DalamudPluginsD17/pull/9006) merged on
2026-07-11.

- `#196` Add Custom OpenAI-Compatible API Support
- `#201` Add more visible feedback about LLM API usage limits exceeded

Notes:

- `#196` is now represented by the shipped custom OpenAI-compatible provider
  path, provider-aware runtime behavior, and live model-refresh / debugger
  support in the official build.
- `#201` is now represented by the shipped actionable LLM failure
  notifications, provider-aware runtime failure classification, and clearer
  debugger/runtime feedback for quota and endpoint failures.
- `#148` stays open because the release shipped the first structured-dialogue
  foundation, not the full glossary/metadata scope requested in that issue.
- `#174` and `#176` stay open as improved-but-not-closed follow-up work:
  operator retranslation / DB semantics and local-LLM latency still need
  separate field validation and likely further iteration.

### Compatibility and long-tail backlog

- `#173` and `#179`: CharacterPanelRefined compatibility/crash analysis
- older enhancement backlog remains open (`#192`, `#139`, `#104`, `#103`,
  `#68`, `#15`)

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

## Previously Closed In Published `4.2600.1105.x`

- `#186` Randomly stops translating to PT-BR and displays English text instead
- `#190` Seleção de mecanismo de tradução não está funcionando corretamente
- `#191` Talk text is translated multiple time
- `#195` Translation stuck on Gemini engine regardless of configuration settings

Notes:

- `4.2600.1105.x` was the release package that addressed this engine-selection
  and dialogue-cache cluster before the current stable line.
- Those fixes remain part of the published release line and should no longer be
  treated as "submitted only" backlog items.
- That package included:
  - engine-selection stabilization that keeps `ChosenTransEngine` and
    `ChosenTransEngineKey` synchronized
  - translator-local concurrency hardening for the LLM cache path
  - `TalkHandler` DB recheck-before-insert behavior to reduce duplicate talk
    rows
  - transient dialogue-failure persistence guards so exact-failure placeholders
    and cross-language original-text echoes stop becoming sticky fallbacks

## Recently Closed In Published `4.2601.0516.x`

This package is now live in the official Dalamud feed after
[PR #8674](https://github.com/goatcorp/DalamudPluginsD17/pull/8674) merged on
2026-05-16.

### #198 Texts are translated multiple times when OpenAI model is changed

- Priority: tracked
- Ease: done in code
- Status: published fix, should now be treated as release-validated unless
  fresh field reports contradict it
- Notes:
  - The newer reproduction steps narrow this down to runtime-refresh listener
    accumulation, not translator-local dictionary concurrency.
  - The first follow-up fix stabilized register/unregister delegate identity.
  - A second review follow-up fixed the shared-handler case where the same
    handler instance is registered for multiple addon names and only the first
    unregister used to succeed.
  - Because `4.2601.0516.x` is now officially published, this item should no
    longer sit in an "awaiting published validation" bucket.

## Recently Closed In Published `4.2601.0531.0115`

This package is now live in the official Dalamud feed after
[PR #8789](https://github.com/goatcorp/DalamudPluginsD17/pull/8789) merged on
2026-06-01.

- `#187` MiniTalk text extrapolates balloon size in native replacement mode
- `#188` Text overflow in small dialogue boxes

Notes:

- Both items were closed with release-validation comments and clear reopen
  instructions for users still reproducing on `v4.2601.0531.0115` or newer.
- Overflow reports that remain inside umbrella issues (`#171`, `#172`) should
  now be treated as new post-release repro candidates, not as pending closure
  blockers for `#187`/`#188`.

## P0: Urgent and Likely Next Targets

These are the best immediate targets because they are blocking, widespread, or
highly visible and appear to have a narrow root cause.

### #207 Quest tracker todolist not translating to Portuguese on v4.2601.0516.1152

- Priority: P0
- Ease: narrow/medium
- Status: active post-release regression confirmed on the current official
  build
- Why it is first:
  - This is the freshest tracker report and it explicitly uses the new release
    channel field, so the report is clearly tied to the official
    `DalamudPluginsD17` package rather than a local build.
  - The body scopes the failure to `Quest tracker / ToDoList / ScenarioTree`
    while also stating that the dialogue windows still translate, which makes
    this much narrower than a generic "plugin not translating" symptom.
  - The report explicitly calls out that it still reproduces on
    `v4.2601.0516.1152` even though the older `#182` fix was expected to cover
    this family.
  - This is now the strongest current-official signal that the quest-tracker
    cluster is not actually closed.
  - A later 2026-06-09 comment on `#171` reports the same family again in
    Spanish/Google, but specifically as tracker progression failing to advance
    after quest-state changes.

### #206 {targetLanguage} variable do not take Language to translate to

- Priority: P0
- Ease: narrow/medium
- Status: active post-release regression confirmed on the current official
  build
- Why it is first:
  - Two 2026-06-01 follow-up comments explicitly confirm that it still
    reproduces after updating to the newest installed official build and after
    forcing plugin-resource refresh.
  - The issue narrows a vague "LLM not translating" symptom into a concrete
    prompt-variable failure around `{targetLanguage}`.
  - This is directly adjacent to the shared prompt-template path and may
    explain a chunk of the broader provider-specific failures still being
    reported.
  - The screenshot evidence still shows `targetLanguage = Japanese`, so this
    is no longer just "awaiting release validation"; it is a live confirmed
    regression after the last production release.

### #204 OpenRouter not translating

- Priority: P0
- Ease: medium
- Status: active provider-specific manifestation of the same LLM prompt/runtime
  cluster
- Notes:
  - The body still describes the default prompt path turning dialogue into the
    same obviously wrong fixed string on OpenRouter after leaving the prompt
    untouched.
  - With `#206` now confirming a live `{targetLanguage}` variable problem on
    the official build, this issue should be treated as potentially overlapping
    evidence rather than a fully separate provider-only outage.
  - It is still a P0 because users read this as "LLM support regressed" even
    when other engines still translate.

### #203 Echoglossian not translating

- Priority: P0
- Ease: medium
- Status: active mixed engine/runtime report
- Notes:
  - The follow-up comment is useful because it narrows the symptom:
    `Google` and `Yandex` can recover, but only on some quest surfaces, while
    `Gemini API` and `DeepL-non API` still do not work at all.
  - This now looks less like a total plugin outage and more like a combination
    of provider gating, engine configuration UX, and uneven surface coverage.
  - Keep this near the top until we know whether it decomposes into provider
    runtime bugs, invalid engine/target-language support combinations, or a
    quest-surface-only partial translation state.

### #189 Barra de Próxima MSQ e Janela de Missão sem tradução

- Priority: P0
- Ease: medium
- Status: active quest-family coverage regression
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
  - `#207` makes it more likely that this cluster still includes the quest
    tracker / `ToDoList` path in the current official build, not only the
    earlier mission-window surfaces.

## P1: Active LLM / IA Rework Cluster

These are tightly related enough that they should be treated as one product and
runtime direction rather than isolated one-off fixes.

The first official release of this line is now published in
`4.2601.0710.1250`, but the remaining items below still need field validation
or broader follow-up beyond what that release shipped.

### #174 Translate already saved translated texts does not work

- Priority: P1
- Ease: medium
- Status: active, partially addressed by the in-progress LLM rework
- Notes:
  - Recent user comments still point at DB reuse semantics and lack of a clear
    operator workflow for forcing retranslation after translator experiments.
  - A fresh comment also confirms that clearing the DB resolves the bad state,
    which makes this a real cache/persistence UX problem rather than only a
    misunderstanding of settings.
  - This now belongs squarely in the same operator-facing cluster as dialogue
    retranslation controls and translator diagnostics.

### #176 Overhead de ~1s entre captura do texto e exibição da tradução com LLM local

- Priority: P1
- Ease: medium/hard
- Status: active performance and prompt-shaping issue
- Notes:
  - The comment trail still supports two root-cause angles:
    raw local-LLM latency and unnecessary prompt/context overhead.
  - The user-facing screenshots and discussion make it clear that "single
    ongoing conversation" and filtering unnecessary text are part of the same
    desired direction, not a separate enhancement.

### #148 Structured input and output for glossary and metadata

- Priority: P1
- Ease: hard
- Status: active architecture enhancement
- Notes:
  - This is no longer just "future nice-to-have" architecture; it is part of
    the same quality and control direction as the current LLM rework.
  - The issue remains broader than the first structured-dialogue cuts, so it
    should stay open even as partial foundation work lands elsewhere.

## P1: Urgent but Medium Investigation

These are still release-quality problems, but they likely need a more careful
 runtime pass than the P0 items above.

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

### #171 Deepseek translation is not available... mission titles and descriptions are not being translated

- Priority: P1
- Ease: medium
- Status: mixed umbrella issue, partly addressed
- Notes:
  - The original report mixes at least two clusters:
    - engine / API-error behavior on DeepSeek
    - mission-title / description coverage and layout failures on other engines
  - A follow-up comment adds Google/Spanish screenshots of text overflowing
    dialogue boxes, which was previously tracked by `#188` / `#187` (now closed
    in `4.2601.0531.0115`) and should now be validated as a fresh repro only if
    it still occurs on current official builds.
  - A new 2026-06-09 comment shifts the freshest live symptom toward dynamic
    quest-tracker progression not updating after quest-state changes, which is
    much closer to `#207` / `#172` than to DeepSeek auth alone.
  - Keep this open for now, but treat it as an umbrella report that likely
    decomposes into `#207`, `#189`, `#174`, and residual provider/runtime
    failures.

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
  - With `#207` now reporting current-official `ToDoList` / `ScenarioTree`
    translation failure and `#189` already scoped to the mission-window family,
    this issue is better kept as the broader quest-runtime umbrella than as the
    next narrow target by itself.

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

### #139 Arabic Translation Support

- Status: Phase A implemented; keep open for validation and Phase B research
- Notes:
  - Texture-backed, right-aligned presentation is available for plugin-owned
    overlays and hover tooltips through `LanguagePresentationPolicy`.
  - This is not universal bidi support for game-native or arbitrary ImGui
    widgets. In-game acceptance remains unrecorded.
  - Phase B is limited to shaped static-text and upstream ImGui research;
    editable widgets remain separate work.

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
- Notes:
  - This should stay a documentation/meta tracker, not compete with the real
    engineering backlog ordering above.

## Recommended Execution Order

1. `#206`
2. `#207`
3. `#189`
4. `#204`
5. `#203`
6. the remaining active LLM rework cluster: `#174`, `#176`, `#148`, `#209`
7. `#212` as provider-limits field validation against the newer feedback path
8. `#175`
9. reassess `#171` only after `#207/#189/#172` and `#203/#204/#206` are clearer
10. `#167`
11. release-validate / decompose the remaining live parts of `#172`
12. `#181`
13. `#173` / `#179`
14. `#192`
15. long-term backlog `#139`, `#104`, `#103`, `#68`, `#15`
