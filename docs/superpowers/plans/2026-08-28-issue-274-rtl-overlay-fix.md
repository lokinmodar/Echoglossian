# Issue #274 RTL Overlay Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ensure Arabic, Persian, Urdu, and the other texture-backed languages always use one configured target-language identity from translation request through overlay presentation, while adding a reproducible Arabic Talk-overlay visual gate.

**Architecture:** Treat `Config.Lang` plus `Echoglossian.LangDict` as the authoritative runtime target-language source. Synchronize the legacy `LanguageInt` and `SelectedLanguage` mirrors through one helper, make GTranslate honor the `targetLanguage` argument on every call, and make the overlay derive both language ID and language code from the configured source rather than mixing `Config.Lang` with `SelectedLanguage.Code`. Keep capture, translation, and presentation separate; do not add queues, caches, DB changes, or native UI mutation.

**Tech Stack:** C# 14, .NET 10, Dalamud, GTranslate, System.Drawing.Common 10.0.10, xUnit, Echoglossian.Previewer/Veldrid.

**Spec:** `docs/issue-274-arabic-rtl-overlay-rca.md`

## Global Constraints

- Work on branch `issue-274-rtl-overlay-rendering` created from the freshly fetched `origin/v4-series` commit `017ddef7ab74e656c4f89839d00139b7092663ee` or a newer `origin/v4-series` if the remote advances before execution.
- Preserve overlay-only behavior: never mutate native addon nodes, text nodes, or `AtkValue`s for Arabic/RTL output.
- Keep `Config.Lang` as the persisted source of truth; `LanguageInt` and `SelectedLanguage` are compatibility mirrors only.
- Do not introduce a parallel translation path, cache, queue, or persistence migration.
- Route plugin-owned logs through `PluginRuntimeLog`; never log translated dialogue, prompts, keys, or secrets.
- Keep hot-path logging silent. Any mismatch diagnostic must be edge-triggered or deduplicated and contain language metadata only.
- Follow the repository header, XML documentation, braces, and `this.` call rules.
- Use PowerShell and Windows-safe commands.
- Commit each stable task with `#274` in the commit subject.

---

### Task 1: Centralize configured target-language synchronization

**Files:**
- Create: `GeneralHelpers/TargetLanguageRuntimeState.cs`
- Create: `Echoglossian.Tests/TargetLanguageRuntimeStateTests.cs`
- Modify: `Echoglossian.cs:250-375`
- Modify: `PluginUI/PluginConfigWindowRenderer.cs:25-40,250-275`
- Modify: `GeneralHelpers/RuntimeConfigurationRefresh.cs:30-85`
- Modify: `Echoglossian.Tests/RuntimeConfigurationRefreshContractTests.cs`

**Interfaces:**
- Consumes: `Config.Lang`, `Dictionary<int, LanguageInfo>`, and `LanguagePresentationPolicy.ApplyLanguageFlags(Config)`.
- Produces: `TargetLanguageRuntimeState.Synchronize(Config, Dictionary<int, LanguageInfo>) -> LanguageInfo`, which atomically refreshes `LangDict`, `LanguageInt`, `SelectedLanguage`, and presentation flags from the configured language.

- [x] **Step 1: Write failing synchronization tests**

Create `TargetLanguageRuntimeStateTests.cs` with tests that save and restore all touched static fields. The key regression must start with deliberately stale English mirrors and an Arabic configuration:

```csharp
[Fact]
public void Synchronize_ArabicConfiguration_RepairsStaleLegacyMirrors()
{
    var languages = Echoglossian.CreateLanguagesDictionary();
    var configuration = new Config { Lang = 2 };
    Echoglossian.LanguageInt = 28;
    Echoglossian.SelectedLanguage = languages[28];

    var selected = TargetLanguageRuntimeState.Synchronize(
        configuration,
        languages);

    Assert.Same(languages[2], selected);
    Assert.Equal(2, Echoglossian.LanguageInt);
    Assert.Same(languages[2], Echoglossian.SelectedLanguage);
    Assert.Same(languages, Echoglossian.LangDict);
    Assert.True(configuration.OverlayOnlyLanguage);
    Assert.False(configuration.UnsupportedLanguage);
}
```

Add a second test proving an unknown configured ID throws `KeyNotFoundException` without partially changing `LanguageInt` or `SelectedLanguage`. Restore the prior static values in `finally` blocks so parallel test order cannot leak state.

- [x] **Step 2: Run the focused tests and verify the red state**

Run:

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~TargetLanguageRuntimeStateTests -p:VSTestMaxCpuCount=1 --nologo
```

Expected: compilation fails because `TargetLanguageRuntimeState` does not yet exist.

- [x] **Step 3: Implement the smallest synchronization helper**

Create the helper as an internal static class. Validate dictionary membership before performing any mutations, then update all mirrors and language flags:

```csharp
internal static class TargetLanguageRuntimeState
{
    internal static LanguageInfo Synchronize(
        Config configuration,
        Dictionary<int, LanguageInfo> languages)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(languages);

        if (!languages.TryGetValue(configuration.Lang, out var selectedLanguage))
        {
            throw new KeyNotFoundException(
                $"Configured target language id {configuration.Lang} is not registered.");
        }

        Echoglossian.LangDict = languages;
        Echoglossian.LanguageInt = configuration.Lang;
        Echoglossian.SelectedLanguage = selectedLanguage;
        LanguagePresentationPolicy.ApplyLanguageFlags(configuration);
        return selectedLanguage;
    }
}
```

Use this helper at plugin startup after the dictionary is created, at the beginning of configuration-window drawing, immediately after a language dropdown change, and in `ApplyPendingRuntimeConfigurationChanges()` before translation signatures are evaluated and translator services are rebuilt. Preserve the existing asset/font refresh callbacks after synchronization.

- [x] **Step 4: Add an ordering contract for runtime refresh**

Extend `RuntimeConfigurationRefreshContractTests` to assert that the synchronization call appears before `ComputeTranslationRuntimeSignature()` and `RebuildTranslationServiceSafely()` inside `ApplyPendingRuntimeConfigurationChanges()`. This prevents future save-coordinator changes from rebuilding a translator against stale legacy mirrors.

- [x] **Step 5: Run the focused state tests**

Run:

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~TargetLanguageRuntimeStateTests|FullyQualifiedName~RuntimeConfigurationRefreshContractTests" -p:VSTestMaxCpuCount=1 --nologo
```

Expected: all selected tests pass with zero failures.

- [x] **Step 6: Commit the synchronized runtime state**

```powershell
git add -- GeneralHelpers\TargetLanguageRuntimeState.cs Echoglossian.Tests\TargetLanguageRuntimeStateTests.cs Echoglossian.cs PluginUI\PluginConfigWindowRenderer.cs GeneralHelpers\RuntimeConfigurationRefresh.cs Echoglossian.Tests\RuntimeConfigurationRefreshContractTests.cs
git commit -m "fix(#274): synchronize target language runtime state"
```

---

### Task 2: Make translation and presentation consume the configured target

**Files:**
- Modify: `Translators/GTranslateTranslator.cs:13-95`
- Create: `Echoglossian.Tests/GTranslateTranslatorTargetLanguageTests.cs`
- Modify: `UIOverlays/TranslationOverlay/TranslationOverlayRenderer.cs:90-145`
- Modify: `Echoglossian.Tests/TranslationOverlayRendererContractTests.cs`
- Modify: `Echoglossian.Tests/TextPresentationResolverTests.cs`

**Interfaces:**
- Consumes: the `targetLanguage` argument already defined by `ITranslator.TranslateAsync`, `Config.Lang`, and `RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(int)`.
- Produces: `GTranslateTranslator.ResolveRequestedTargetLanguage(string) -> GTranslate.Language` as a testable provider-boundary helper; the overlay builds `TextLayoutRequest.LanguageId` and `.LanguageCode` from the same configured language.

- [x] **Step 1: Write failing provider and renderer contract tests**

Create a GTranslate test that deliberately leaves `SelectedLanguage` set to Arabic while asking the provider helper for English:

```csharp
[Fact]
public void ResolveRequestedTargetLanguage_UsesMethodArgumentNotSelectedGlobal()
{
    var previous = Echoglossian.SelectedLanguage;
    try
    {
        Echoglossian.SelectedLanguage = new LanguageInfo(
            "ar",
            "Arabic",
            "NotoSansArabic-Medium.ttf",
            string.Empty,
            []);

        var resolved = GTranslateTranslator.ResolveRequestedTargetLanguage("en");

        Assert.Equal(
            GTranslate.Language.GetLanguage("en").Name,
            resolved.Name);
    }
    finally
    {
        Echoglossian.SelectedLanguage = previous;
    }
}
```

Add cases for `ar`, `fa`, and `ur`, plus an empty-code case that must throw `ArgumentException` before a network request is attempted.

Extend `TranslationOverlayRendererContractTests` to read the renderer source and assert:

```csharp
Assert.Contains(
    "RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(this.configuration.Lang)",
    source,
    StringComparison.Ordinal);
Assert.DoesNotContain("SelectedLanguage.Code", source, StringComparison.Ordinal);
```

Add a `TextPresentationResolverTests` case confirming a consistent Arabic request (`2`, `ar`) selects `RtlTexture` and an English request (`28`, `en`) selects `PlainImGui`. Keep the existing policy behavior unchanged.

- [x] **Step 2: Run the focused tests and verify the red state**

Run:

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~GTranslateTranslatorTargetLanguageTests|FullyQualifiedName~TranslationOverlayRendererContractTests|FullyQualifiedName~TextPresentationResolverTests" -p:VSTestMaxCpuCount=1 --nologo
```

Expected: the new provider helper is missing and the renderer still contains `SelectedLanguage.Code`.

- [x] **Step 3: Make GTranslate honor the request contract**

Remove the constructor-captured `gTransTargetLanguage` field and the unused stored `Config` field. Add:

```csharp
internal static Language ResolveRequestedTargetLanguage(
    string requestedTargetLanguage)
{
    var normalizedTargetLanguage =
        RuntimeLanguageHelper.NormalizeLanguage(requestedTargetLanguage);
    if (string.IsNullOrWhiteSpace(normalizedTargetLanguage))
    {
        throw new ArgumentException(
            "A target language is required.",
            nameof(requestedTargetLanguage));
    }

    return Language.GetLanguage(normalizedTargetLanguage);
}
```

Inside the existing `try` in `TranslateAsync`, resolve from the method's `targetLanguage` argument and pass that language's provider name to `AggregateTranslator.TranslateAsync`. Do not read `SelectedLanguage` in this class. Preserve existing failure behavior: log through `PluginRuntimeLog` and return an empty string on provider errors.

- [x] **Step 4: Make the overlay language metadata internally consistent**

In `TranslationOverlayRenderer.Draw`, resolve once:

```csharp
var presentationLanguageId = this.configuration.Lang;
var presentationLanguageCode =
    RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
        presentationLanguageId);
```

Use `presentationLanguageId` for RTL alignment and `TextLayoutRequest.LanguageId`, and `presentationLanguageCode` for `TextLayoutRequest.LanguageCode`. Remove the `SelectedLanguage.Code` read. Do not add fallback from a failed texture render to plain ImGui; the existing no-draw contract is safer than rendering broken complex text.

- [x] **Step 5: Run focused provider and renderer tests**

Run:

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~GTranslateTranslatorTargetLanguageTests|FullyQualifiedName~TranslationOverlayRendererContractTests|FullyQualifiedName~TextPresentationResolverTests" -p:VSTestMaxCpuCount=1 --nologo
```

Expected: all selected tests pass; no network call is made.

- [x] **Step 6: Commit the provider/presentation fix**

```powershell
git add -- Translators\GTranslateTranslator.cs Echoglossian.Tests\GTranslateTranslatorTargetLanguageTests.cs UIOverlays\TranslationOverlay\TranslationOverlayRenderer.cs Echoglossian.Tests\TranslationOverlayRendererContractTests.cs Echoglossian.Tests\TextPresentationResolverTests.cs
git commit -m "fix(#274): use configured language for RTL presentation"
```

---

### Task 3: Add the real Arabic raster and preview regression gates

**Files:**
- Modify: `Echoglossian.Tests/TextImageRendererTests.cs`
- Modify: `Echoglossian.Previewer/Scenarios/PreviewScenarioCatalog.cs`
- Create: `Echoglossian.Previewer/Samples/issue-274-arabic.json`
- Modify: `Echoglossian.Previewer.Tests/Scenarios/PreviewScenarioCatalogTests.cs`
- Modify: `Echoglossian.Previewer.Tests/Session/PreviewSessionLoaderTests.cs`
- Modify: `Echoglossian.Previewer/README.md`

**Interfaces:**
- Consumes: bundled `Font/NotoSansArabic-Medium.ttf`, the shared `TextImageRenderer`, the existing Talk preview surface, and preview configuration loading.
- Produces: built-in scenario key `talk-arabic-274` and a secret-free preview config that deterministically selects language ID `2` and overlay-only Talk presentation.

- [x] **Step 1: Write the real-font regression test**

Add a test using the exact issue sentence rather than `missing-font.ttf`:

```csharp
[Fact]
public void RenderShapedText_Issue274Arabic_UsesBundledFontAndDrawsPixels()
{
    var fontPath = Path.Combine(
        FindRepositoryRoot().FullName,
        "Font",
        "NotoSansArabic-Medium.ttf");
    const string text =
        "أعتذر، ولكن أخشى أنني يجب أن أبعدك في الوقت الحالي. " +
        "يرجى العودة في وقت لاحق.";

    using var renderer = new TextImageRenderer(
        fontPath,
        24f,
        FontStyle.Regular,
        1f);
    using var bitmap = renderer.RenderShapedText(
        text,
        Color.White,
        Color.Transparent,
        480);

    Assert.False(renderer.FallbackFontUsed);
    Assert.InRange(bitmap.Width, 1, 2048);
    Assert.InRange(bitmap.Height, 1, 2048);
    Assert.True(ContainsVisiblePixel(bitmap));
}
```

Implement `FindRepositoryRoot()` and `ContainsVisiblePixel(Bitmap)` as private test helpers. Do not use a platform-sensitive PNG hash as the assertion; anti-aliasing can differ across supported Windows builds.

- [x] **Step 2: Register a deterministic issue scenario and sample config**

Add `talk-arabic-274` to `PreviewScenarioCatalog.Defaults`, using `TranslationOverlaySurfaceId.Talk`, the issue's Arabic sentence, and a stable speaker title. Create the sample JSON with no credentials:

```json
{
  "Lang": 2,
  "Translate": true,
  "TranslateTalk": true,
  "TalkTranslationDisplayMode": 1,
  "FontSize": 24
}
```

The scenario supplies content and geometry; the sample config supplies the language/font/backend selection. Do not embed API keys or a live plugin configuration.

- [x] **Step 3: Add preview catalog and config-loader tests**

Assert that `ResolveScenario("talk-arabic-274")` returns Talk, contains Arabic characters, and has non-empty bounds. Load the tracked sample through `PreviewSessionLoader` and assert `Lang == 2`, `TalkTranslationDisplayMode == TooltipTranslation`, and the source sample remains unchanged after preview session disposal.

- [x] **Step 4: Run the raster and previewer test projects**

Run:

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~RenderShapedText_Issue274Arabic -p:VSTestMaxCpuCount=1 --nologo
dotnet test .\Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~PreviewScenarioCatalogTests|FullyQualifiedName~PreviewSessionLoaderTests" -p:VSTestMaxCpuCount=1 --nologo
```

Expected: the real font is used, visible pixels are produced, and the preview inputs remain deterministic and isolated.

- [x] **Step 5: Document and capture the visual smoke command**

Add this Windows command to the Previewer README:

```powershell
dotnet run --project .\Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --config .\Echoglossian.Previewer\Samples\issue-274-arabic.json --scenario talk-arabic-274 --viewport 1920x1080 --screenshot surface --output .\artifacts\previewer\issue-274
```

The generated manifest must report `RtlTexture`. Open the PNG and verify connected Arabic glyphs, correct RTL order, word wrapping, and no right-edge clipping. Do not commit generated artifacts.

- [x] **Step 6: Commit the regression gates**

```powershell
git add -- Echoglossian.Tests\TextImageRendererTests.cs Echoglossian.Previewer\Scenarios\PreviewScenarioCatalog.cs Echoglossian.Previewer\Samples\issue-274-arabic.json Echoglossian.Previewer.Tests\Scenarios\PreviewScenarioCatalogTests.cs Echoglossian.Previewer.Tests\Session\PreviewSessionLoaderTests.cs Echoglossian.Previewer\README.md
git commit -m "test(#274): add Arabic overlay visual regression gate"
```

---

### Task 4: Validate the branch and record the remaining runtime boundary

**Files:**
- Modify: `docs/issue-274-arabic-rtl-overlay-rca.md`
- Modify: `docs/superpowers/plans/2026-08-28-issue-274-rtl-overlay-fix.md`
- Update if generated by the validated build: `Echoglossian.xml`

**Interfaces:**
- Consumes: all earlier commits, standard solution tests, Previewer screenshot output, and DalaMock startup validation.
- Produces: a validated issue branch with explicit automated coverage and an honest in-game verification checklist.

- [x] **Step 1: Run the standard solution validation**

Run:

```powershell
dotnet build .\Echoglossian.sln -c Debug --no-restore
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1 --nologo
```

Expected: build succeeds and all unit tests pass with zero failures. Investigate new failures; report unrelated baseline failures without hiding them.

- [x] **Step 2: Run hosted runtime validation**

Run:

```powershell
dotnet build .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore
dotnet test .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1 --nologo
```

Expected: startup, configuration, and shutdown tests pass. State explicitly that DalaMock startup does not prove live FFXIV Talk payload capture or GPU texture presentation.

- [x] **Step 3: Build the Previewer and capture the Arabic artifact**

Run:

```powershell
dotnet restore .\Echoglossian.Previewer\Echoglossian.Previewer.csproj
dotnet build .\Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-restore
dotnet run --project .\Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --config .\Echoglossian.Previewer\Samples\issue-274-arabic.json --scenario talk-arabic-274 --viewport 1920x1080 --screenshot surface --output .\artifacts\previewer\issue-274
```

Expected: exit code `0`, manifest backend `RtlTexture`, and a visually correct Arabic PNG. Record the artifact path in the execution report but keep `artifacts/` untracked.

- [x] **Step 4: Update the RCA status and plan checkboxes**

Change the RCA from preliminary mechanism to implemented corrective action only if the red/green tests demonstrate the stale-state path before and after the fix. Record:

- configuration is now authoritative for both request and presentation metadata;
- GTranslate no longer captures `SelectedLanguage.Code`;
- the renderer no longer reads `SelectedLanguage.Code`;
- automated preview coverage is present;
- live in-game Talk verification remains required because Previewer/DalaMock cannot reproduce the FFXIV compositor and addon lifecycle together.

- [x] **Step 5: Run final repository hygiene checks**

Run:

```powershell
git diff --check
git status --short
git log --oneline --decorate origin/v4-series..HEAD
```

Expected: no whitespace errors, no generated screenshot artifacts, no API keys, and only issue #274 files/commits in the branch delta.

- [x] **Step 6: Commit validated documentation and generated XML if changed**

```powershell
git add -- docs\issue-274-arabic-rtl-overlay-rca.md docs\superpowers\plans\2026-08-28-issue-274-rtl-overlay-fix.md
if (-not (git diff --quiet -- Echoglossian.xml)) { git add -- Echoglossian.xml }
git commit -m "docs(#274): record RTL overlay correction validation"
```

### Execution Notes

- Focused red/green validation covered synchronization, translator target resolution, overlay language metadata, the real bundled-font Arabic raster path, and deterministic Previewer inputs.
- Hosted Mock validation exposed one extra startup-order regression outside the original plan surface: `MigrateTranslationEngineSelection()` still read the static `LangDict` before `TargetLanguageRuntimeState.Synchronize(...)` ran. The smallest safe revision was to use `this.languagesDictionary` instead, with `TranslationEngineSelectionContractTests` guarding that constructor-time contract.
- Final validation on August 28, 2026 passed with:
  - `dotnet build .\Echoglossian.sln -c Debug --no-restore`
  - `dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1 --nologo` -> `1291/1291`
  - `dotnet test .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1 --nologo` -> `25/25`
  - `dotnet test .\Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --no-restore -p:VSTestMaxCpuCount=1 --nologo` -> `145/145`
- The deterministic Previewer artifact was written to `artifacts\previewer\issue-274\surface-talk-arabic-274-1920x1080.png`, and `artifacts\previewer\issue-274\manifest.json` recorded `PresentationMode: "RtlTexture"`.

## Required In-Game Verification

After automated validation, use the official-like Debug plugin artifact and verify all of the following in FFXIV:

1. English client, Google/GTranslate engine, Arabic target, Talk set to overlay-only.
2. Arabic Talk text uses connected glyphs, RTL reading order, word wrapping, and no right-edge clipping.
3. Repeat with Persian and Urdu.
4. Switch Arabic -> English -> Arabic without restarting; verify the backend changes `RtlTexture -> PlainImGui -> RtlTexture` and old-language text is cleared.
5. Confirm overlay-only mode never modifies native Talk text.
6. Inspect `<PluginConfigDirectory>\Echoglossian.log` for errors; do not rely on `dalamud.log` unless cross-plugin context is required.

Do not close issue #274 until this in-game checklist passes or the remaining gap is explicitly assigned to the reporter with a build and capture instructions.
