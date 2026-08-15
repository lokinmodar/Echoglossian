# Changelog

This changelog is curated from two sources:

- the local `Echoglossian` git history
- merged release PRs in `goatcorp/DalamudPluginsD17`

It is intentionally high-signal rather than a verbatim dump of every commit.

## Published Release `v4.2601.0815.1339`

This package was published to the official Dalamud feed after
[PR #262](https://github.com/lokinmodar/Echoglossian/pull/262). It focuses on
first-line dialogue speaker context recovery, precomputed quest dialogue
interlocutor metadata, shared glossary enforcement for dialogue LLM requests,
and capability-aware parameter handling plus diagnostics across the structured
LLM providers.

Published in `DalamudPluginsD17` via
[PR #9207](https://github.com/goatcorp/DalamudPluginsD17/pull/9207) on
2026-08-15.

Highlights:

- preserves speaker-aware dialogue context from the very first line by
  carrying speaker identity through the shared translation path and reusing
  precomputed quest dialogue metadata plus live actor fallbacks when a visible
  NPC or target can disambiguate the interlocutor
- precomputes and persists quest dialogue interlocutor metadata asynchronously
  after quest acceptance, keeping quest-sheet traversal, EF lookups, and stale
  result suppression off Dalamud callbacks while preserving DB-first lookup
  semantics for later dialogue resolution
- enforces glossary term protection in the shared dialogue LLM flow so
  provider-specific structured requests reuse the same protected replacements
  instead of silently dropping configured glossary terms
- adds conservative model capability learning, capability-gated LLM settings
  UI, and structured request diagnostics so unsupported parameters such as
  reasoning or temperature controls can be disabled or sanitized without
  provider-specific guesswork

## Published Release `v4.2601.0809.2000`

This package was published to the official Dalamud feed after
[PR #265](https://github.com/lokinmodar/Echoglossian/pull/265). It focuses on
the `_BattleTalk` native-layout restore fix, the remaining `_MiniTalk`
pooled-background baseline cleanup, and the follow-up localization string
corrections that shipped with that stabilization pass.

Published in `DalamudPluginsD17` via
[PR #9183](https://github.com/goatcorp/DalamudPluginsD17/pull/9183) on
2026-08-10.

Highlights:

- restores clean native `_BattleTalk` and `_MiniTalk` baselines after plugin
  ownership ends, preventing cumulative stale translated dimensions while
  preserving game-owned horizontal geometry in `_BattleTalk`
- scopes compact wrapped-height measurement to the known stale-layout reuse
  paths in `_BattleTalk` and `_MiniTalk`, keeping the shared tooltip and
  toast-style helper behavior unchanged for legitimate game-owned padding
- aligns the plugin UI dialogue-family source strings and unifies the
  `Translation Display Mode` label across tabs and localized resources
- includes regression coverage for the helper default height policy plus the
  explicit compact wrapped-height opt-in used by `_BattleTalk` and `_MiniTalk`

## Published Release `v4.2601.0809.0046`

This package was published to the official Dalamud feed with the runtime
localization correction merged through
[PR #260](https://github.com/lokinmodar/Echoglossian/pull/260). It keeps the
locale-specific resource naming introduced for Crowdin while ensuring the
plugin actually loads the locale selected in its configuration.

Published in `DalamudPluginsD17` via
[PR #9177](https://github.com/goatcorp/DalamudPluginsD17/pull/9177) on
2026-08-09.

Highlights:

- applies the normalized plugin UI culture before the first strongly typed
  resource lookup, preventing Dalamud's thread culture from silently falling
  back to English
- preserves distinct locale-specific resources such as `pt-BR`, `pt-PT`, and
  `fr-FR`, and adds the missing `ca -> ca-ES` and `nl -> nl-NL` normalization
  mappings
- adds exact-resource-set regression coverage and permanent Crowdin review,
  synchronization, recovery, and rollback guardrails

## Published Release `v4.2601.0807.1730`

This package was published to the official Dalamud feed after the follow-up
MiniTalk native-layout regression fix and keeps the release focused on the
remaining recycled-bubble height edge case.

Published in `DalamudPluginsD17` via
[PR #9163](https://github.com/goatcorp/DalamudPluginsD17/pull/9163) on
2026-08-08.

Highlights:

- isolates the compact detached-container baseline preference to `_MiniTalk_`
  so recycled oversized bubble slots stop stretching later native dialogue
  balloons in `Native` and `Swap` mode
- keeps the shared detached-container layout helper default unchanged for the
  tooltip and toast-style callers that still need the historical larger
  baseline behavior
- adds regression coverage for both stale-primary and stale-secondary recycled
  `_MiniTalk_` detached-container height paths

## Published Release `v4.2601.0806.0051`

This package was published to the official Dalamud feed and focuses on
MiniTalk native-layout stability, ActionDetail and ItemDetail overlay control
activation, and safer localized plugin-label sync.

Published in `DalamudPluginsD17` via
[PR #9151](https://github.com/goatcorp/DalamudPluginsD17/pull/9151) on
2026-08-06.

Highlights:

- preserved the visible `_MiniTalk` native bubble baseline in `Native` and
  `Swap` mode when detached-container synchronization encounters recycled
  container heights larger than the visible balloon
- made `ActionDetail` and `ItemDetail` overlays honor dedicated persisted
  width, line-height, padding, and color controls, and separated those
  settings visually in the tooltip configuration tab
- restored friendly localized plugin labels during the latest Crowdin resource
  sync while keeping the new action/item detail overlay strings required by the
  expanded tooltip configuration surface

## Published Release `v4.2601.0802.2355`

This package ships the large native UI translation runtime expansion merged
through [PR #240](https://github.com/lokinmodar/Echoglossian/pull/240), with a
focus on broader surface coverage, persistence-backed reuse, and safer native
and overlay presentation behavior.

Published in `DalamudPluginsD17` via
[PR #9125](https://github.com/goatcorp/DalamudPluginsD17/pull/9125) on
2026-08-05.

Highlights:

- restored selection-dialog translation and added a dedicated DB-first
  `Tooltip` addon runtime with structured original-text presentation, anchored
  overlays, canonical payload recovery, and hardened native wrapping
- expanded and stabilized quest-family coverage across `Journal*`,
  `_ToDoList`, `ScenarioTree`, `RecommendList`, and `AreaMap`, including popup
  persistence, accepted-quest prefetch, objective matching, and strict
  overlay-only native-state protection
- added native nameplate presentation plus distance-aware overlay fallback with
  configurable scale, fade, cutoff, and per-frame synchronization
- added dedicated persistence-backed `ContextMenu` and `ToDo` runtimes,
  placement-aware toast controls, action/item detail translation controls, and
  stronger tooltip cache hydration
- normalized locale-specific plugin resources and expanded focused runtime
  diagnostics through the rotating `Echoglossian.log` workflow

## Published Release `v4.2601.0718.2006`

This package submits the latest `v4-series` head after the 2026-07-17
MiniTalk hotfix release and focuses on OpenRouter recovery plus hosted preview
infrastructure that will support future UI validation without relying on
external mock-package drift.

Published in `DalamudPluginsD17` via
[PR #9031](https://github.com/goatcorp/DalamudPluginsD17/pull/9031) on
2026-07-19.

Highlights:

- fixed OpenRouter live model refresh when the configured base URL already
  includes `/v1`, so `Fetch Live Models` stops calling an invalid doubled
  endpoint
- vendored the `DalaMock` source used by the hosted preview backend so local
  preview and mock sessions run against a known-good source snapshot instead of
  drifting package behavior
- expanded previewer and hosted-plugin validation rails while keeping vendored
  preview infrastructure out of the shipped plugin package

## Published Release `v4.2601.0717.0008`

This package was published to the official Dalamud feed and focused on the
urgent MiniTalk native-layout regression that resurfaced after the previous
release.

Published in `DalamudPluginsD17` via
[PR #9027](https://github.com/goatcorp/DalamudPluginsD17/pull/9027) on
2026-07-17.

Highlights:

- restored MiniTalk plugin-owned bubble layout safely even when the game had
  already repainted a new source line into the same live field, preventing
  stale restoration from deforming later bubbles
- normalized raw SeString line-break payload bytes and residual control-format
  characters in both overlay rendering and MiniTalk native-text comparisons so
  source overlays stop showing invalid glyph boxes and native reconciliation
  stops treating wrapped text as a new untranslated line
- aligned MiniTalk overlay bounds and native extra-wrap-width handling with the
  resolved bubble container/background so original-text overlays wrap against
  the visible balloon and recycled bubble instances keep stable native sizing

## Published Release `v4.2601.0715.1114`

This package delivers the first production-ready RTL presentation path and a
broader stabilization of translated game-window, reference-text, and tooltip
workflows.

Published in `DalamudPluginsD17` via
[PR #9026](https://github.com/goatcorp/DalamudPluginsD17/pull/9026) on
2026-07-16.

Highlights:

- added the Phase A texture-backed RTL presentation path with bidirectional
  shaping, right alignment, adaptive hover sizing, configurable line height,
  and bounded CPU/GPU memory caches
- made client source language, target language, and translation engine part of
  the canonical reuse identity, including normalized provider language codes
  and extended Chinese, Korean, and Traditional Chinese client identities
- stabilized `MainCommand`, `AddonContextMenuTitle`, `ActionMenu`, and the
  `Character` family with canonical sheet-backed data, incremental translation
  application, corrected tooltip ownership, and removal of per-frame database
  work that caused severe frame-rate drops
- improved plugin hover rendering for dense action, item, quest, and game-window
  text while keeping tooltip-only `JournalDetail` presentation read-only with
  respect to native addon nodes
- added regression and performance coverage plus implementation specifications
  for later native RTL work in both Echoglossian and Dalamud

## Published Release `v4.2601.0712.1140`

This package was published to the official Dalamud feed and
extends the story-surface debugger and persistence tooling shipped in the
current `v4` line.

Published in `DalamudPluginsD17` via
[PR #9009](https://github.com/goatcorp/DalamudPluginsD17/pull/9009) on
2026-07-12.

Highlights:

- added explicit retranslate-and-persist coverage for visible `TalkSubtitle`,
  `CutSceneSelectString`, and `TextGimmickHint` story-facing surfaces alongside
  the existing `Talk` and `BattleTalk` flows
- added latest visible story-surface provenance inspection in
  `/eglotranslatordebugger`, including runtime provenance, effective table,
  last update status, and a direct `View In DB Manager` handoff
- extracted the DB-manager read-only inspection table primitives into reusable
  shared UI components so the debugger and DB manager render the same inspection
  structures while keeping their data providers separate
- hardened the new story-surface diagnostics path with follow-up fixes for enum
  mapping safety, latest-snapshot consistency, retranslation outcome promotion,
  and operator-facing fallback semantics

## Published Release `v4.2601.0710.1250`

This package advances the LLM dialogue runtime and provider workflow line and
is now live in the official Dalamud feed.

Published in `DalamudPluginsD17` via
[PR #9006](https://github.com/goatcorp/DalamudPluginsD17/pull/9006) on
2026-07-11.

Highlights:

- added structured dialogue translation plus glossary/context routing across
  ChatGPT, Claude, Gemini, DeepSeek, LM Studio, Ollama, OpenRouter, and custom
  OpenAI-compatible providers
- added runtime model refresh, provider diagnostics, dialogue sessions and
  metrics, and retranslation controls in `/eglotranslatordebugger`
- hardened prompt expansion, dialogue history limiting, quota/error feedback,
  and live model refresh state isolation for OpenAI-compatible engines
- refreshed target-language coverage and downloadable script-font support for
  newer LLM workflows

## Published Release `v4.2601.0531.0115`

This package advances the native-dialogue and toast runtime stability line and
is now live in the official Dalamud feed.

Published in `DalamudPluginsD17` via
[PR #8789](https://github.com/goatcorp/DalamudPluginsD17/pull/8789) on
2026-06-01.

Highlights:

- moved supported toast families to the `ToastGui` runtime path with corrected
  per-type enable/disable gating
- stabilized native reflow behavior for `MiniTalk`, `BattleTalk`, and toast
  family windows, including sizing and background alignment follow-ups
- fixed cross-surface text leakage between dialogue and toast surfaces by
  tightening source ownership and runtime routing
- unified diacritics-removal behavior for native replacement flows and exposed
  a single toggle in General settings
- centered overlay text rendering for toast-family overlays, `TextGimmickHint`,
  and `TalkSubtitle`

## Official DalamudPluginsD17 Release Timeline

These entries mark when Echoglossian started shipping through the official
repository workflow.

| Date | Version / PR | Notes |
| --- | --- | --- |
| 2022-08-19 | [PR #83](https://github.com/goatcorp/DalamudPluginsD17/pull/83) | `.NET 6` onboarding for official repo use |
| 2022-08-25 | [PR #221](https://github.com/goatcorp/DalamudPluginsD17/pull/221) | API bump and game `6.2` compatibility follow-up |
| 2023-01-14 | [PR #1072](https://github.com/goatcorp/DalamudPluginsD17/pull/1072) `v2.101.2301.891` | first numbered official release in the D17 era |
| 2023-05-29 | [PR #1922](https://github.com/goatcorp/DalamudPluginsD17/pull/1922) `3.x era has begun` | start of the `v3` release line |
| 2023-08-20 | [PR #2250](https://github.com/goatcorp/DalamudPluginsD17/pull/2250) | new version during the clipboard and overlay iteration period |
| 2023-10-06 | [PR #2578](https://github.com/goatcorp/DalamudPluginsD17/pull/2578) `v3.0.2310.x` | API9-era release after handler and data-entity groundwork |
| 2024-05-04 | [PR #3482](https://github.com/goatcorp/DalamudPluginsD17/pull/3482) `[TESTING] v3.1.x` | quest translation rollout entered testing |
| 2024-05-07 | [PR #3493](https://github.com/goatcorp/DalamudPluginsD17/pull/3493) `v3.1.x to stable` | `v3.1.x` promoted from testing to stable |
| 2024-05-11 | [PR #3503](https://github.com/goatcorp/DalamudPluginsD17/pull/3503) `[FIX] v3.1.x` | immediate stabilization for the first quest-heavy release |
| 2024-05-23 | [PR #3533](https://github.com/goatcorp/DalamudPluginsD17/pull/3533) `v3.2.x` | continued quest-family expansion and cleanup |
| 2024-07-11 | [PR #3918](https://github.com/goatcorp/DalamudPluginsD17/pull/3918) `v3.3.x` | start of the APIX release cadence |
| 2024-07-14 | [PR #3968](https://github.com/goatcorp/DalamudPluginsD17/pull/3968) `v3.4.x` | talk and battle-talk stabilization pass |
| 2024-07-18 | [PR #4050](https://github.com/goatcorp/DalamudPluginsD17/pull/4050) `v3.5.x` | follow-up fixes around quest and talk surfaces |
| 2024-07-20 | [PR #4083](https://github.com/goatcorp/DalamudPluginsD17/pull/4083) `v3.7.x` | continued APIX stabilization; no public `v3.6` PR was found in D17 |
| 2024-07-25 | [PR #4164](https://github.com/goatcorp/DalamudPluginsD17/pull/4164) `v3.8.x` | subtitle and font handling fixes |
| 2024-07-25 | [PR #4168](https://github.com/goatcorp/DalamudPluginsD17/pull/4168) `v3.9.x` | overlay delay and startup fixes |
| 2024-07-29 | [PR #4208](https://github.com/goatcorp/DalamudPluginsD17/pull/4208) `v3.10.x` | ChatGPT engine entered the official release line |
| 2024-08-04 | [PR #4288](https://github.com/goatcorp/DalamudPluginsD17/pull/4288) `v3.11.x` | prompt and model configuration iteration |
| 2024-08-04 | [PR #4290](https://github.com/goatcorp/DalamudPluginsD17/pull/4290) `v3.12.x` | configuration and API-key handling fixes |
| 2024-08-06 | [PR #4307](https://github.com/goatcorp/DalamudPluginsD17/pull/4307) `v3.13.x` | packaging and assets fixes |
| 2024-08-10 | [PR #4340](https://github.com/goatcorp/DalamudPluginsD17/pull/4340) `v3.14.x` | latin-extended rendering and language support work |
| 2024-08-18 | [PR #4416](https://github.com/goatcorp/DalamudPluginsD17/pull/4416) `v3.15.x` | windows-translation groundwork plus load and PvP fixes |
| 2024-11-17 | [PR #4886](https://github.com/goatcorp/DalamudPluginsD17/pull/4886) `v3.16.x` | API11 / patch `7.1` compatibility |
| 2024-12-15 | [PR #5236](https://github.com/goatcorp/DalamudPluginsD17/pull/5236) `v3.17.x` | OpenAI and DeepL support-matrix updates |
| 2024-12-24 | [PR #5268](https://github.com/goatcorp/DalamudPluginsD17/pull/5268) `v3.18.x` | engine-selection and persistence fixes |
| 2024-12-24 | [PR #5269](https://github.com/goatcorp/DalamudPluginsD17/pull/5269) `v3.19.x` | follow-up stabilization release |
| 2025-01-13 | [PR #5346](https://github.com/goatcorp/DalamudPluginsD17/pull/5346) `v3.20.x` | plugin-load and runtime fixes |
| 2025-03-28 | [PR #5782](https://github.com/goatcorp/DalamudPluginsD17/pull/5782) `v3.21` | Google translation quality fix plus `API12` / `.NET 9` |
| 2025-08-08 | [PR #6521](https://github.com/goatcorp/DalamudPluginsD17/pull/6521) `v3.22.x` | API13-era groundwork around generic addon and DB manager systems |
| 2025-08-09 | [PR #6690](https://github.com/goatcorp/DalamudPluginsD17/pull/6690) `v3.23.x` | immediate stabilization follow-up |
| 2025-11-23 | [PR #7183](https://github.com/goatcorp/DalamudPluginsD17/pull/7183) `v3.24.x` | translator and UI refactor line |
| 2025-12-22 | [PR #7523](https://github.com/goatcorp/DalamudPluginsD17/pull/7523) `v3.25.x` | `API14` bump |
| 2026-05-04 | [PR #8510](https://github.com/goatcorp/DalamudPluginsD17/pull/8510) `v4.2600.x` | first official `4.x` / `API15` release |
| 2026-05-04 | [PR #8522](https://github.com/goatcorp/DalamudPluginsD17/pull/8522) `v4.2600.x` hotfix | first-launch config creation fix |
| 2026-05-12 | [PR #8626](https://github.com/goatcorp/DalamudPluginsD17/pull/8626) `v4.2600.1105.x` | setup, engine selection, cache-concurrency, and version-reuse stabilization |
| 2026-06-01 | [PR #8789](https://github.com/goatcorp/DalamudPluginsD17/pull/8789) `v4.2601.0531.0115` | native dialogue/toast reflow stabilization, ToastGui route, and cross-surface isolation fixes |
| 2026-07-11 | [PR #9006](https://github.com/goatcorp/DalamudPluginsD17/pull/9006) `v4.2601.0710.1250` | LLM structured dialogue/runtime release: custom OpenAI-compatible providers, live model refresh, debugger diagnostics, and prompt/history hardening |
| 2026-07-12 | [PR #9009](https://github.com/goatcorp/DalamudPluginsD17/pull/9009) `v4.2601.0712.1140` | story-surface retranslation, debugger provenance, and reusable DB inspection tooling |
| 2026-07-16 | [PR #9026](https://github.com/goatcorp/DalamudPluginsD17/pull/9026) `v4.2601.0715.1114` | RTL overlay presentation plus game-window, reference-text, and tooltip stabilization |
| 2026-07-17 | [PR #9027](https://github.com/goatcorp/DalamudPluginsD17/pull/9027) `v4.2601.0717.0008` | urgent MiniTalk native-layout stabilization |
| 2026-07-19 | [PR #9031](https://github.com/goatcorp/DalamudPluginsD17/pull/9031) `v4.2601.0718.2006` | OpenRouter live-model refresh and hosted preview infrastructure follow-up |
| 2026-08-05 | [PR #9125](https://github.com/goatcorp/DalamudPluginsD17/pull/9125) `v4.2601.0802.2355` | broad native UI runtime expansion across selection dialogs, tooltips, quest surfaces, nameplates, toasts, persistence, and diagnostics |
| 2026-08-06 | [PR #9151](https://github.com/goatcorp/DalamudPluginsD17/pull/9151) `v4.2601.0806.0051` | MiniTalk native bubble stabilization follow-up, ActionDetail and ItemDetail overlay control activation, and localized plugin-label sync |
| 2026-08-08 | [PR #9163](https://github.com/goatcorp/DalamudPluginsD17/pull/9163) `v4.2601.0807.1730` | MiniTalk recycled-bubble height stabilization follow-up |
| 2026-08-09 | [PR #9177](https://github.com/goatcorp/DalamudPluginsD17/pull/9177) `v4.2601.0809.0046` | plugin UI culture application, locale-specific resource loading, and localization guardrails |
| 2026-08-10 | [PR #9183](https://github.com/goatcorp/DalamudPluginsD17/pull/9183) `v4.2601.0809.2000` | BattleTalk and MiniTalk stale native-layout stabilization plus dialogue-family label corrections |

## Pre-Official History

### 2021 Foundation

- initial public prototypes for real-time translation
- first talk, cutscene, and toast overlays
- early multilingual font support and script-specific rendering work
- first persistent translation storage and query paths
- first swap-style presentation where native text and overlay text diverged

### 2022 Stabilization

- EF Core database flow became viable for production use
- repeated fixes for Google translation breakage and API churn
- `.NET 6` and later official-repo compatibility work
- configuration, assets download, and cross-platform load fixes

### 2023 Expansion

- transition through `.NET 7` / API8 and API9
- handler rework and configuration UI cleanup
- troubleshooting tab and clipboard-copy support
- ChatGPT exploration began late in the year

### 2024 Quest And Dialogue Expansion

- quest translation became a major feature area across `Journal`,
  `_ToDoList`, `ScenarioTree`, `RecommendList`, and related windows
- `Talk`, `BattleTalk`, `TalkSubtitle`, and overlay logic were repeatedly
  reworked for stability and lower stutter
- ChatGPT entered the plugin UI and release cadence
- native window translation groundwork and language/font handling broadened

### 2025 Translator And Architecture Reset

- many translation engines were added or explored, including
  DeepL, OpenRouter, Yandex Public, LibreTranslate, and local-model targets
- the plugin UI and overlay systems went through several unsuccessful and then
  partially stabilized rewrites
- DB manager and generic addon handling groundwork landed
- late in the year, work shifted toward the large `v4-series` refactor

### 2026 `v4-Series` Refactor

- addon handling, overlay drawing, caches, and persistence were split into the
  current `NativeUI`, `UIOverlays`, `Cache`, and `DBHelpers` surfaces
- quest-family handlers were migrated to the standalone architecture
- string-array and game-window flows moved toward canonical payloads and
  DB-first lookups
- structured tooltip, action/item reference text, and character flows were
  rebuilt around typed caches and payload stability
- release-safety work added tests, migration hardening, quieter logging, debug
  probe gating, and governance-oriented AI disclosure docs
