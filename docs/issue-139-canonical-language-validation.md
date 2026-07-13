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
