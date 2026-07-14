# Issue #139 Canonical Language Validation

## Purpose

Use this matrix to verify source-client, target-language, and translation-engine
transitions against a real Dalamud runtime. Automated tests cover canonical
identity and persistence boundaries; native mutation, overlay publication, and
live provider request codes remain in-game checks.

Record the plugin build, game version, client raw language value, target,
engine, display mode, surfaces checked, and observed persisted/provider codes
for each run.

## Manual Matrix

1. Start with an English client and target `he`. Open Character, ActionMenu,
   ActionDetail, ItemDetail, a quest surface, and dialogue. Verify each surface
   in native, overlay-only, and swap modes. Overlay-only must leave native text
   unchanged; swap must show translated text natively and original text in the
   overlay.
2. Change the target language. Verify rows created for the prior target are not
   displayed or reused on any surface.
3. Enable `TranslateAlreadyTranslatedTexts`, change the translation engine, and
   verify rows from the prior engine are not reused. Disable
   `TranslateAlreadyTranslatedTexts` and verify the established policy permits
   otherwise compatible same-source/same-target rows regardless of engine.
4. Change the source client between English, German, Japanese, and French.
   Verify that no row created under another source client is reused.
5. Exercise extended raw client values 4, 5, 6, and 7. Verify persisted source
   identities `chs`, `cht`, `ko`, and `tc`, respectively, and provider source
   codes `zh-CN`, `zh-CN`, `ko`, and `zh-TW`, respectively.
6. Create a row under raw client value 4 and verify raw value 5 does not reuse
   it. Create a row under raw value 5 and verify raw value 7 does not reuse it.
7. Return to the original client source and verify only rows compatible with
   the current source, target, and engine policy are reused.
8. After every source or target change, verify visible surfaces restore their
   default unmodified text until a matching translation becomes available.
9. Start a deliberately slow translation, change source client, target language,
   engine, or policy before it completes, and verify the old completion neither
   persists nor publishes into the new scope. For dialogue, verify the pending
   persistence is canceled before its database commit. Repeat with
   `TranslateAlreadyTranslatedTexts` enabled and disabled.
10. Exercise a long RTL hover body at an extreme tooltip width or text scale.
    Verify it is wrapped or rejected during bounded layout construction (before
    bitmap/upload), retains the prior valid texture when one exists, enters the
    bounded retry cooldown, and does not cause repeated work or frame loss.
11. Enable a dialogue-engine override that differs from the global engine with
    `TranslateAlreadyTranslatedTexts` enabled. Translate a `Talk`,
    `_BattleTalk`, `TalkSubtitle`, or `_MiniTalk` line twice and verify the
    second request reuses the row stored by the effective dialogue engine.

## Result Record

| Check | Result | Evidence / Notes |
| --- | --- | --- |
| English to `he`, all surfaces and modes | Not run | |
| Target transition | Not run | |
| Engine policy transition | Not run | |
| English/German/Japanese/French source transitions | Not run | |
| Raw clients 4/5/6/7 persistence and provider codes | Not run | |
| Raw 4 to 5 and raw 5 to 7 isolation | Not run | |
| Return to original source | Not run | |
| Default text restoration after source/target changes | Not run | |
| In-flight target/engine transition rejection | Not run | |
| RTL raster limit and cooldown behavior | Not run | |
| Dialogue effective-engine reuse | Not run | |

## As-Built Status (2026-07-13)

Automated task reports cover canonical source resolution, source-scoped reuse,
captured async operations, source-generation invalidation, and texture
presentation lifecycle behavior. They do not constitute in-game acceptance.
Keep every matrix row above `Not run` until direct evidence is recorded.

Additional required manual checks, also not run:

| Check | Result | Evidence / Notes |
| --- | --- | --- |
| RTL overlay right alignment | Not run | |
| Texture line-height density setting | Not run | |
| Long hover tooltip adaptive sizing | Not run | |
| RTL oversized texture rejection before upload | Not run | |
| Dense GameWindow performance | Not run | |
| Dialog persistence canceled on scope retirement | Not run | |
| Engine switch during chunked/dialogue operation remains pinned | Not run | |
