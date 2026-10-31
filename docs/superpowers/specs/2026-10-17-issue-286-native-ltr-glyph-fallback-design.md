# Issue 286 native LTR glyph fallback design

## Status

The official Dalamud and FFXIVClientStructs baseline now contains the native
font bindings required to start the feasibility spike.

GitHub issue: [#286](https://github.com/lokinmodar/Echoglossian/issues/286)

This document records the verified dependency baseline and the architectural
decision. It does not claim that native glyph fallback is implemented.

## Objective

Render missing LTR glyphs in native FFXIV UI text with fonts already bundled
by Echoglossian. Preserve the game's glyphs when they exist, and supply only
missing glyphs from the selected Echoglossian font.

The feature must remain opt-in and must not affect RTL, overlay-only, or
texture-backed presentation.

## Decision

Echoglossian will own the implementation.

- Do not require FFXIV-FontChanger or another plugin at runtime.
- Do not require IPC for the first implementation.
- Use only the FFXIVClientStructs assembly supplied by the supported Dalamud
  build.
- Do not package another FFXIVClientStructs assembly.
- Do not duplicate native signatures locally.
- Keep FFXIV-FontChanger as a technical reference, subject to its Apache-2.0
  license when code is reused.

IPC remains a possible future coordination mechanism. It is not part of the
minimum feature or its acceptance criteria.

## Verified official baseline

The dependency gate was rechecked on October 17, 2026.

- `Dalamud.dll`: `15.0.3.8`, commit
  `f7d06f363955251991222363a7c008b8998b6565`
- `FFXIVClientStructs.dll`: `7.57.0.9386`, commit
  `d6397a76b3a63c2b247025dc296653182b718ca6`
- FFXIVClientStructs pull request:
  [#2009](https://github.com/aers/FFXIVClientStructs/pull/2009)
- Pull request merge commit:
  `44b4a4d7a6fa74805976b55ab6cfd5e14c015b28`

The installed FFXIVClientStructs commit is a descendant of the merge commit
and is 36 commits ahead of it. Reflection against the official installed DLL
confirmed these members:

- `AtkFontManager.BuildFont`
- `AtkFontManager.GameFontTable`
- `AtkFontManager.LobbyFontTableStandardResolution`
- `AtkFontManager.LobbyFontTableHighResolution`
- `AtkFontManager.FontSet.PickFont`
- `AtkFontManager.FontSet.GetGlyph`
- `AtkTextNode.BuildFontCache`
- `AtkTextNode.FreeFontCache`
- `AtkFontAnalyzerBase.LayOutCharacter`
- `StdUnorderedMap<TKey, TValue>`

The generated `Addresses`, `Delegates`, and `MemberFunctionPointers` types are
also present for the relevant member functions.

## What the official DLL provides

FFXIVClientStructs now owns the mapped native structures, signatures, resolved
addresses, delegates, and generated wrappers. This removes the need for
Echoglossian to maintain local byte signatures for the spike.

The official DLL does not implement the font replacement behavior. The spike
must still provide:

- Dalamud hooks and their lifecycle;
- a private DirectWrite collection for bundled TTF or OTF files;
- missing-glyph detection and fallback-only selection;
- glyph shaping, rasterization, and atlas allocation;
- texture uploads outside repeated per-frame rebuild paths;
- native font-cache invalidation;
- deterministic hook, texture, COM, and unmanaged-memory disposal.

## Runtime boundaries

The service may run only when all of these conditions are true:

- the user explicitly enables the experimental feature;
- the selected target language is LTR;
- the presentation mode writes translated text into native UI;
- the selected font asset resolves successfully;
- every required FFXIVClientStructs address resolves.

Overlay-only and texture-backed modes must not initialize or call this
service. RTL behavior remains unchanged.

Any initialization failure must disable only native glyph fallback, log one
actionable error through `PluginRuntimeLog`, and preserve the existing
translation behavior.

## Minimum implementation scope

The first spike should touch only these areas:

- one isolated native-font service under `NativeUI/`;
- existing language font metadata in `LanguageInfo` and
  `LanguagesDictionary`;
- existing asset resolution through `AssetsManager`;
- existing presentation eligibility through `LanguagePresentationPolicy`;
- one experimental config flag, localized UI text, and localized failure
  notification;
- pure eligibility and font-path tests;
- hosted startup, failure, and disposal tests where DalaMock can model them.

Do not add an editor, presets, FreeType, custom line breaking, subpixel
rendering, nameplate support, or per-addon font configuration to the spike.

## Relationship to issue 217

Issue [#217](https://github.com/lokinmodar/Echoglossian/issues/217) removes
diacritics from eligible native replacement text because the game font lacks
some glyphs. Issue #286 aims to preserve the original Unicode text by adding
the missing glyphs instead.

Keep the #217 fallback unchanged until #286 passes runtime validation. After
that validation, reevaluate whether diacritic removal is still needed for LTR
native and swap modes. Do not remove the existing fallback as part of the
feasibility spike.

## Validation boundary

Unit tests can prove eligibility, configuration defaults, asset resolution,
and failure behavior. Hosted tests can prove startup and disposal wiring only
when the mock drives those paths.

DalaMock cannot prove native detours, FFXIV font-cache behavior, game texture
uploads, or rendered glyph output. Validate those behaviors in-game with the
Polish and Turkish samples listed in issue #286, including plugin reload and
unload.

## References

- [Dalamud IPC API](https://dalamud.dev/api/Dalamud.Plugin.Ipc/)
- [FFXIVClientStructs PR #2009](https://github.com/aers/FFXIVClientStructs/pull/2009)
- [FFXIV-FontChanger reference implementation](https://github.com/Soreepeong/FFXIV-FontChanger/tree/cbfe4ae71af1b42c66b7cd325031ff21316c56e9/FontChanger.DalamudPlugin)
