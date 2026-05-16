# Changelog

This changelog is curated from two sources:

- the local `Echoglossian` git history
- merged release PRs in `goatcorp/DalamudPluginsD17`

It is intentionally high-signal rather than a verbatim dump of every commit.

## Submitted Release `v4.2601.0516.x`

This is the current post-`v4.2601.0512.x` hotfix package prepared for the
official plugin repo.

Highlights:

- fixed the `OpenRouter` prompt-expansion bug that could leave placeholder
  tokens in the request instead of sending the fully materialized translation
  prompt to the provider
- hardened the `OpenRouter` prompt builder so literal `{sourceLanguage}` and
  `{targetLanguage}` text inside dialogue content is no longer corrupted during
  prompt substitution

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
