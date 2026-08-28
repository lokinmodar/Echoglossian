# Issue #274 RCA: Arabic Overlay Could Bypass the RTL Backend

- **Issue:** [#274 - RTL Arabic Broken after new update](https://github.com/lokinmodar/Echoglossian/issues/274)
- **Reported:** August 27, 2026
- **Affected release:** `v4.2601.0816.1235`
- **Last reported working state:** Before the affected release
- **Affected surface:** Talk overlay in overlay-only mode
- **Target language / engine:** Arabic / Google Translate
- **Status:** Corrective action implemented and locally validated on August 28, 2026; live FFXIV verification still required

## Executive Summary

Issue #274 was not caused by malformed Google output, a changed Arabic font,
or a direct edit to the RTL renderer in the August 16 release. The screenshot
is most consistent with Arabic text being drawn by the ordinary ImGui text
backend instead of the texture-backed RTL backend. That produces the exact
failure class shown in the report: isolated Arabic glyphs, incorrect visual
ordering, and right-edge clipping.

The current texture renderer was exercised with the bundled
`NotoSansArabic-Medium.ttf` and a representative Arabic sentence. It produced
connected, right-to-left text without using the fallback font. The same
experiment with a missing bundled font also produced connected text through
the system fallback. In addition, current Google output for the English text
shown in the issue contained no bidi control characters and survived the
plugin's normalization unchanged.

The release comparison is equally important: every file in the RTL selection,
rasterization, overlay, language-policy, and bundled-font path has the same Git
object ID in `v4.2601.0815.1339` and `v4.2601.0816.1235`. The same path is also
unchanged from `v4.2601.0807.1730`. The August 16 release therefore contains no
direct RTL implementation or asset regression.

The architectural weakness is that translation and presentation do not use
one authoritative language state. The Google translator captures
`SelectedLanguage.Code`, while the overlay independently selects its backend
from `configuration.Lang`. The plugin also maintains `LanguageInt`. If these
values diverge, Google can return Arabic while the renderer classifies the
same output as a non-RTL language and sends it to ImGui. That is the only
current code path that matches both halves of the report: valid Arabic
translation and visibly unshaped overlay text.

Red/green tests now validate the corrective path directly: stale target-language
mirrors can be synchronized from `Config.Lang`, the Google translator no longer
captures `SelectedLanguage.Code`, the overlay no longer mixes `Config.Lang`
with `SelectedLanguage.Code`, and a deterministic Previewer artifact renders
the issue sentence through `RtlTexture` with the bundled Arabic font. Hosted
Mock validation also exposed one adjacent startup-order regression after the
state-centralization refactor: `MigrateTranslationEngineSelection()` still read
the static `LangDict` before synchronization. The final fix moved that lookup
to the plugin-owned `languagesDictionary`, restoring startup safety without
changing persistence or presentation behavior.

## User Impact

- Arabic, Persian, and Urdu can become unreadable in the Talk overlay when the
  ordinary ImGui backend receives their translated text.
- Overlay-only mode leaves the original English native text intact, so the
  failure is isolated to translated presentation rather than native mutation.
- The problem can appear engine-specific or translation-specific even though
  the returned string is valid and the defect occurs after translation.

## Expected and Observed Flow

```text
Expected
Arabic target -> RtlTexture backend -> GDI+ raster with RTL format -> connected glyphs

Observed / inferred from screenshot
Arabic translation -> PlainImGui backend -> ImGui text drawing -> isolated glyphs + clipping
```

`RtlTexturePresentationService` does not fall back to plain ImGui after a
texture-rendering failure. It reports the draw as unavailable instead. The
presence of visibly drawn, disconnected Arabic therefore points to backend
classification before texture rendering, not failure inside the texture
renderer.

## Evidence

| Evidence | Result | Interpretation |
| --- | --- | --- |
| Issue screenshot | Arabic glyphs are isolated and clipped while native English remains unchanged. | Matches ordinary ImGui rendering in overlay-only mode. |
| Current production rasterizer with bundled Noto Arabic font | Connected RTL output; `FallbackFontUsed == false`. | The bundled font and current texture renderer can render the reported script correctly. |
| Rasterizer with an intentionally missing bundled font | Connected RTL output through the system fallback. | A missing font alone does not reproduce the screenshot. |
| Current Google translation of the reported English sentence | Valid Arabic; no format characters; overlay normalization did not alter it. | No evidence that the translator or normalizer split the glyphs. |
| `v4.2601.0815.1339` vs. `v4.2601.0816.1235` | RTL renderer, resolver, overlay renderer, language policy, language dictionary, and font objects are identical. | Rules out a direct last-release change to the RTL path. |
| `v4.2601.0807.1730` vs. `v4.2601.0816.1235` | The same RTL path is unchanged. | The direct renderer path predates both recent releases. |
| Current backend contract | Arabic language ID `2` resolves to `RtlTexture`; a failed texture draw does not render plain text. | The screenshot implies that the request did not reach the renderer classified as Arabic. |
| Runtime language ownership | Translator reads `SelectedLanguage.Code`; renderer reads `configuration.Lang`; `LanguageInt` also exists. | A state mismatch can produce Arabic output with a non-RTL presentation backend. |

## Release Comparison

The affected release tag points to commit
`2901ef0d8b1d68956c6ac86afe97ff3bc098e7f3`; the previous release points to
`edc589fbe92a4aa52f4e6fa2a23536287d93b9f0`.

The following paths have identical Git object IDs in those two releases:

- `ImageGeneration/TextImageRenderer.cs`
- `UIOverlays/TextPresentation/RtlTexturePresentationService.cs`
- `UIOverlays/TextPresentation/LanguagePresentationPolicy.cs`
- `UIOverlays/TextPresentation/TextPresentationResolver.cs`
- `UIOverlays/TranslationOverlay/TranslationOverlayRenderer.cs`
- `Font/NotoSansArabic-Medium.ttf`
- `LanguagesHandling/LanguagesDictionary.cs`

The project dependency on `System.Drawing.Common` also remained `10.0.10`.
Changes shipped on August 16 covered prompt/configuration behavior and other
runtime surface settings, not RTL rendering or its font asset. Static analysis
of those surrounding changes found no direct write that intentionally changes
the target-language fields, but it cannot reconstruct the reporter's runtime
state.

## Root Cause and Contributing Factors

### Validated incident mechanism — high confidence

The overlay rendered an RTL translation through `PlainImGui` instead of
`RtlTexture`. This conclusion is based on the visual signature, successful
production-rasterizer reproduction in the correct backend, and the renderer's
no-fallback contract. The branch now enforces one configured target-language
identity across translator request handling and overlay presentation metadata.

### Architectural root cause — high confidence

Language identity is duplicated across mutable state:

- `configuration.Lang` controls presentation-backend selection;
- `SelectedLanguage.Code` controls the translator target captured at translator
  construction; and
- `LanguageInt` is maintained as an additional runtime value.

No invariant requires these values to agree at the point where translated text
is presented. The backend request also carries only the numeric configuration
language rather than the exact translator target that produced the text.

### Reporter-specific runtime trigger — still unobserved

The most likely trigger is stale or divergent target-language state during or
after configuration/runtime refresh. No normal source path in the release diff
has yet been shown to create that persistent mismatch. Confirmation requires a
log or instrumentation capture from an affected installation. The local fix
addresses the divergence mechanism directly even though the exact field values
on the reporter's August 27, 2026 installation were never captured.

### Release escape — high confidence

The RTL tests cover policy membership, request values, cache behavior,
`StringFormat` flags, bitmap dimensions, and texture limits. They do not prove
the end-to-end invariant:

> the language selected by the translator is the language used to select the
> presentation backend, and real Arabic output is visually shaped in the Talk
> overlay.

Several renderer tests intentionally use a missing font, and the overlay
preview scenarios do not provide an Arabic golden image. Existing hosted tests
validate startup and policy wiring but do not drive a real Arabic Talk payload
through capture, translation, backend selection, texture upload, and drawing.

Issue #139 was consequently closed after validating components rather than the
complete official-build user path. The August 16 release was also shipped
without an Arabic/Persian/Urdu visual smoke test. That is where the release
process failed even though the release did not directly modify the RTL code.

## Ruled-Out or Unsupported Hypotheses

- **A direct RTL code regression in the last release:** ruled out by identical
  Git objects across the release boundary.
- **A changed or missing bundled Arabic font:** not supported; the font object
  is identical, and both bundled-font and fallback-font diagnostic renders
  produced connected glyphs.
- **Google returning separated Arabic characters:** not reproduced; current
  Google output is normal Arabic and is unchanged by display normalization.
- **Texture rendering failing and then falling back to ImGui:** ruled out by
  the current renderer contract; failure produces no draw, not plain text.
- **Unicode-format stripping as the immediate cause:** not reproduced for the
  reported sentence. Broad removal of all Unicode `Format` characters remains
  a separate RTL robustness risk because some bidi controls and joiners belong
  to that category.

## Corrective Actions

Implemented locally on August 28, 2026:

1. `Config.Lang` plus the plugin language dictionary now act as the
   authoritative target-language source through
   `TargetLanguageRuntimeState.Synchronize(...)`, which repairs `LangDict`,
   `LanguageInt`, `SelectedLanguage`, and presentation flags together.
2. `GTranslateTranslator` now resolves the provider target from the
   `targetLanguage` method argument on every call and no longer captures
   `SelectedLanguage.Code`.
3. `TranslationOverlayRenderer` now derives both the language id and language
   code from the configured target language before it selects the presentation
   backend or builds `TextLayoutRequest`.
4. The branch now includes a real bundled-font Arabic raster test, a committed
   `talk-arabic-274` Previewer scenario, a secret-free sample config, and a
   deterministic screenshot command. The generated manifest for the validated
   artifact reported `RtlTexture`.
5. Hosted Mock validation exposed a startup-order regression after the language
   synchronization refactor. `MigrateTranslationEngineSelection()` now reads
   the plugin instance dictionary instead of the static `LangDict`, preventing
   constructor-time null access before synchronization completes.

## Remaining In-Game Verification Boundary

Previewer and DalaMock validate the configuration, translator, and overlay
coupling, but they do not reproduce the live FFXIV Talk capture path together
with the game compositor. The remaining required checks are:

- English client, Google or GTranslate engine, Arabic target, Talk in overlay-only mode.
- Arabic Talk text uses connected glyphs, correct RTL order, expected wrapping, and no right-edge clipping.
- Repeat the same check with Persian and Urdu.
- Switch Arabic -> English -> Arabic without restarting and confirm the backend changes `RtlTexture -> PlainImGui -> RtlTexture` while old-language text clears.
- Confirm overlay-only mode never mutates native Talk text.
- Inspect `<PluginConfigDirectory>\Echoglossian.log` for runtime errors.

## Bottom Line

The evidence does not support "we broke the Arabic renderer in the last
release." The more accurate conclusion is:

> We shipped a system in which translation language and rendering language
> could diverge, and we lacked the end-to-end Arabic visual test that would
> catch that divergence before release.

The August 28, 2026 branch fix removes that configuration/renderer split in the
covered paths and adds deterministic Arabic regression coverage, but live game
verification is still required before closing issue #274.
