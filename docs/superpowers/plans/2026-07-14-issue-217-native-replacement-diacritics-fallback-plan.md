# Issue 217 Native Replacement Diacritics Fallback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace hardcoded target-language id checks for optional native
replacement diacritics fallback with explicit language metadata and one shared
runtime policy, without changing current behavior.

**Architecture:** Keep the feature opt-in and native-replacement-only. Move
eligibility into `LanguageInfo`, then route UI visibility and runtime
normalizer application through a single shared helper instead of scattered
numeric checks and toggle-only branches.

**Tech Stack:** C# / .NET 10, xUnit, FluentAssertions where already used,
Dalamud plugin runtime, existing `Config`, `LanguageInfo`, quest handlers,
talk/toast handlers, and DB-first game-window normalization seams.

## Global Constraints

- No behavior change for overlay or texture-backed plugin presentation.
- The fallback remains opt-in through the existing config toggle.
- The fallback remains restricted to native replacement display paths only.
- Do not introduce heuristic auto-detection for language eligibility.
- Preserve the currently exposed eligible language set on the first pass.
- Avoid DB or save-format changes.
- Follow repo StyleCop rules, file headers, XML docs, braces, and `this.` call
  style.
- Validate with:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`

---

### Task 1: Add Explicit Language Metadata

**Files:**

- Modify: `LanguagesHandling/LanguageInfo.cs`
- Modify: `LanguagesHandling/LanguagesDictionary.cs`
- Test: `Echoglossian.Tests/NativeReplacementDiacriticsLanguageMetadataTests.cs`

**Interfaces:**

- Produces:

```csharp
public bool SupportsOptionalNativeReplacementDiacriticsFallback { get; set; }
```

- Produces object-initializer usage in `LanguagesDictionary.cs` for the current
  curated languages.

- [ ] **Step 1: Write the failing metadata test**

```csharp
[Fact]
public void LanguageDictionary_PreservesCurrentNativeDiacriticsEligibleLanguages()
{
    var languages = LanguagesDictionary.CreateLanguagesDictionary();

    languages[104].SupportsOptionalNativeReplacementDiacriticsFallback.Should().BeTrue();
    languages[110].SupportsOptionalNativeReplacementDiacriticsFallback.Should().BeTrue();
    languages[0].SupportsOptionalNativeReplacementDiacriticsFallback.Should().BeFalse();
}
```

- [ ] **Step 2: Run the targeted test to confirm it fails**

Run:

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~LanguageDictionary_PreservesCurrentNativeDiacriticsEligibleLanguages"
```

Expected: FAIL because the property does not exist yet.

- [ ] **Step 3: Add the property and mark the existing curated entries**

```csharp
public bool SupportsOptionalNativeReplacementDiacriticsFallback { get; set; }
```

```csharp
[104] = new LanguageInfo(...)
{
    SupportsOptionalNativeReplacementDiacriticsFallback = true,
},
```

- [ ] **Step 4: Re-run the targeted test**

Run the same command from Step 2.

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add LanguagesHandling/LanguageInfo.cs LanguagesHandling/LanguagesDictionary.cs Echoglossian.Tests
git commit -m "#217 Add language metadata for native diacritics fallback"
```

### Task 2: Centralize the Eligibility Policy

**Files:**

- Create: `LanguagesHandling/NativeReplacementDiacriticsPolicy.cs`
- Test: `Echoglossian.Tests/NativeReplacementDiacriticsPolicyTests.cs`

**Interfaces:**

- Produces:

```csharp
internal static class NativeReplacementDiacriticsPolicy
{
    internal static bool IsEligible(
        int languageId,
        IReadOnlyDictionary<int, LanguageInfo> languages);
}
```

- [ ] **Step 1: Write failing policy tests**

```csharp
[Fact]
public void IsEligible_ReturnsTrue_ForCuratedLanguage()
{
    var languages = LanguagesDictionary.CreateLanguagesDictionary();
    NativeReplacementDiacriticsPolicy.IsEligible(104, languages).Should().BeTrue();
}

[Fact]
public void IsEligible_ReturnsFalse_ForUnknownLanguage()
{
    var languages = LanguagesDictionary.CreateLanguagesDictionary();
    NativeReplacementDiacriticsPolicy.IsEligible(999, languages).Should().BeFalse();
}
```

- [ ] **Step 2: Run the targeted policy tests**

Run:

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~NativeReplacementDiacriticsPolicyTests"
```

Expected: FAIL because the policy type does not exist yet.

- [ ] **Step 3: Implement the minimal helper**

```csharp
internal static bool IsEligible(
    int languageId,
    IReadOnlyDictionary<int, LanguageInfo> languages)
{
    return languages.TryGetValue(languageId, out LanguageInfo? language) &&
           language.SupportsOptionalNativeReplacementDiacriticsFallback;
}
```

- [ ] **Step 4: Re-run the targeted policy tests**

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add LanguagesHandling/NativeReplacementDiacriticsPolicy.cs Echoglossian.Tests/NativeReplacementDiacriticsPolicyTests.cs
git commit -m "#217 Add canonical native diacritics policy"
```

### Task 3: Move UI Visibility Off Numeric Ids

**Files:**

- Modify: `PluginUI/PluginUI.cs`
- Modify: `PluginUI/Tabs/GeneralTab.cs`
- Test: `Echoglossian.Tests/NativeReplacementDiacriticsUiPolicyTests.cs`

**Interfaces:**

- Consumes:

```csharp
NativeReplacementDiacriticsPolicy.IsEligible(int languageId, IReadOnlyDictionary<int, LanguageInfo> languages)
```

- Produces metadata-driven assignment for `LangToRemoveDiacritics`.

- [ ] **Step 1: Write a failing UI policy test**

```csharp
[Fact]
public void SelectedLanguage_UsesMetadataForDiacriticsToggleEligibility()
{
    var languages = LanguagesDictionary.CreateLanguagesDictionary();
    NativeReplacementDiacriticsPolicy.IsEligible(104, languages).Should().BeTrue();
    NativeReplacementDiacriticsPolicy.IsEligible(0, languages).Should().BeFalse();
}
```

- [ ] **Step 2: Replace the numeric id expression in `PluginUI.cs`**

```csharp
LangToRemoveDiacritics = NativeReplacementDiacriticsPolicy.IsEligible(
    this.configuration.Lang,
    LangDict);
```

- [ ] **Step 3: Verify the General tab still only shows the checkbox when `LangToRemoveDiacritics` is true**

Run:

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~NativeReplacementDiacriticsUiPolicyTests"
```

Expected: PASS.

- [ ] **Step 4: Commit**

```powershell
git add PluginUI/PluginUI.cs PluginUI/Tabs/GeneralTab.cs Echoglossian.Tests
git commit -m "#217 Drive diacritics toggle eligibility from language metadata"
```

### Task 4: Harden Shared Runtime Normalizer Creation

**Files:**

- Modify: `GeneralHelpers/Utils.cs`
- Test: `Echoglossian.Tests/NativeReplacementTextNormalizerPolicyTests.cs`

**Interfaces:**

- Produces a runtime guard that refuses to create a native replacement
  normalizer unless the selected language is explicitly eligible.

- [ ] **Step 1: Write failing normalizer-creation tests**

```csharp
[Fact]
public void TryCreateNativeReplacementTextNormalizer_ReturnsNull_ForIneligibleLanguage()
{
    var config = new Config
    {
        Lang = 0,
        RemoveDiacriticsWhenUsingReplacementTalkBTalk = true,
    };

    Echoglossian.TryCreateNativeReplacementTextNormalizer(config).Should().BeNull();
}
```

- [ ] **Step 2: Implement metadata-aware gating in `TryCreateNativeReplacementTextNormalizer`**

```csharp
if (!NativeReplacementDiacriticsPolicy.IsEligible(config.Lang, LangDict))
{
    return null;
}
```

- [ ] **Step 3: Re-run the targeted test**

Expected: PASS.

- [ ] **Step 4: Commit**

```powershell
git add GeneralHelpers/Utils.cs Echoglossian.Tests
git commit -m "#217 Harden shared native normalizer gating"
```

### Task 5: Harden Quest and Game-Window Native Replacement Paths

**Files:**

- Modify: `NativeUI/AddonHandlers/Quest/QuestAddonModeHelpers.cs`
- Modify: `NativeUI/Helpers/QuestAddonWiring.cs`
- Modify: `NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs`
- Modify: `NativeUI/AddonHandlers/MainMenu/MainCommandHandler.cs`
- Test: `Echoglossian.Tests/QuestAddonModeHelpersTests.cs`
- Test: `Echoglossian.Tests/NativeGameWindowDiacriticsPolicyTests.cs`

**Interfaces:**

- Consumes `NativeReplacementDiacriticsPolicy`.
- Produces one canonical decision for native quest and DB-first game-window
  normalization.

- [ ] **Step 1: Write failing tests for quest-family gating**

```csharp
[Fact]
public void ShouldRemoveDiacritics_ReturnsFalse_WhenLanguageIsIneligible()
{
    QuestAddonModeHelpers.ShouldRemoveDiacritics(
        JournalTranslationDisplayMode.NativeUiTranslation,
        true,
        0,
        LanguagesDictionary.CreateLanguagesDictionary()).Should().BeFalse();
}
```

- [ ] **Step 2: Expand the helper signature to include language context**

```csharp
internal static bool ShouldRemoveDiacritics(
    JournalTranslationDisplayMode displayMode,
    bool removeDiacriticsWhenUsingReplacementQuest,
    int languageId,
    IReadOnlyDictionary<int, LanguageInfo> languages)
```

- [ ] **Step 3: Update quest-family and game-window callers to use the new canonical guard**

```csharp
return WritesNativeTranslation(displayMode) &&
       removeDiacriticsWhenUsingReplacementQuest &&
       NativeReplacementDiacriticsPolicy.IsEligible(languageId, languages);
```

- [ ] **Step 4: Re-run targeted helper tests**

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add NativeUI/AddonHandlers/Quest/QuestAddonModeHelpers.cs NativeUI/Helpers/QuestAddonWiring.cs NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs NativeUI/AddonHandlers/MainMenu/MainCommandHandler.cs Echoglossian.Tests
git commit -m "#217 Canonicalize quest and game-window diacritics gating"
```

### Task 6: Harden Talk and Toast Native Replacement Paths

**Files:**

- Modify: `NativeUI/AddonHandlers/Talk/TalkHandler.cs`
- Modify: `NativeUI/AddonHandlers/Talk/BattleTalkHandler.cs`
- Modify: `NativeUI/AddonHandlers/Talk/TalkSubtitleHandler.cs`
- Modify: `NativeUI/AddonHandlers/SingleText/MiniTalkHandler.cs`
- Modify: `NativeUI/AddonHandlers/CutSceneSelectString/CutSceneSelectStringHandler.cs`
- Modify: `NativeUI/AddonHandlers/Toasts/AddonTextToastHandler.cs`
- Modify: `NativeUI/AddonHandlers/Toasts/ToastGuiSupportedToastRuntime.cs`
- Modify: `NativeUI/AddonHandlers/Toasts/TextGimmickHintHandler.cs`
- Modify: `NativeUI/AddonHandlers/Toasts/QuestToastRuntime.cs`
- Test: `Echoglossian.Tests/NativeDialogueDiacriticsPolicyTests.cs`

**Interfaces:**

- Produces one consistent rule: toggle + native replacement path + eligible
  language.

- [ ] **Step 1: Write failing tests for one talk-family and one toast-family path**

```csharp
[Fact]
public void NativeDialogueFallback_DoesNotApply_ForIneligibleLanguage()
{
    var config = new Config
    {
        Lang = 0,
        RemoveDiacriticsWhenUsingReplacementTalkBTalk = true,
    };

    NativeReplacementDiacriticsPolicy.IsEligible(
        config.Lang,
        Echoglossian.LangDict).Should().BeFalse();
}
```

- [ ] **Step 2: Replace raw toggle-only checks with canonical policy usage**

```csharp
return NativeReplacementDiacriticsPolicy.IsEligible(
           this.config.Lang,
           Echoglossian.LangDict) &&
       this.config.RemoveDiacriticsWhenUsingReplacementTalkBTalk
    ? this.normalizeReplacementText(text)
    : text;
```

- [ ] **Step 3: Re-run targeted dialogue/toast tests**

Expected: PASS.

- [ ] **Step 4: Commit**

```powershell
git add NativeUI/AddonHandlers/Talk NativeUI/AddonHandlers/SingleText NativeUI/AddonHandlers/CutSceneSelectString NativeUI/AddonHandlers/Toasts Echoglossian.Tests
git commit -m "#217 Align dialogue and toast diacritics fallback policy"
```

### Task 7: Full Validation and Coverage Audit

**Files:**

- Modify: `docs/translation-surface-support-matrix.md` only if the final
  implementation changes operator-visible behavior
- Modify: `Echoglossian.xml` when XML documentation changes during
  implementation

- [ ] **Step 1: Run build**

```powershell
dotnet build .\Echoglossian.sln -c Debug --no-restore
```

Expected: build succeeds with zero new errors.

- [ ] **Step 2: Run tests**

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
```

Expected: all tests pass.

- [ ] **Step 3: Verify in-game native replacement behavior**

Check:

- eligible language + toggle off => no stripping
- eligible language + toggle on + native replacement => stripping applies
- eligible language + overlay/tooltip presentation => no stripping
- ineligible language + toggle on => no stripping

- [ ] **Step 4: Audit expansion candidates but do not change coverage without explicit validation**

Candidates:

- `bs`
- `az`
- `eo`
- `mt`
- `cy`
- `ig`
- `yo`

- [ ] **Step 5: Commit**

```powershell
git add docs/translation-surface-support-matrix.md Echoglossian.xml Echoglossian.Tests
git commit -m "#217 Validate native diacritics fallback refactor"
```

---

Plan complete and saved to `docs/superpowers/plans/2026-07-14-issue-217-native-replacement-diacritics-fallback-plan.md`.
