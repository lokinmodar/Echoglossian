# Canonical Translation Reuse Scope Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Make source client language, target language, engine policy, version, and source content a mandatory and uniform translation-reuse scope for every Echoglossian surface.

**Architecture:** Add one pure shared scope object that owns language and engine compatibility. Persist new source-language identities as normalized codes, preserve legacy named values through normalized comparison, and make every DB/cache retrieval path use the scope before returning a translation. Handlers retain only addon capture, native mutation, and layout responsibilities.

**Tech Stack:** C#/.NET 10, Dalamud IClientState, EF Core SQLite, xUnit, FFXIVClientStructs.

## Global Constraints

- Source identity is always resolved from the current ClientLanguage raw value; it never defaults to English.
- Raw values 0, 1, 2, 3, 4, 5, 6, and 7 persist respectively as ja, en, de, fr, chs, cht, ko, and tc.
- Provider source codes are ja, en, de, fr, zh-CN, zh-CN, ko, and zh-TW respectively; they are not cache identities.
- A row is reusable only when source and target language match the requested scope.
- TranslateAlreadyTranslatedTexts true additionally requires the active engine; false permits another compatible engine.
- Empty or unknown stored source language is not reusable.
- An unknown runtime client language fails closed: no provider call, cache reuse, persistence, or native mutation.
- Do not add a schema migration. Normalize legacy source names during comparison.
- Preserve existing game-version and source-content hash checks.
- Overlay-only mode must not mutate native addon state.
- Commit each completed task with #139.

---

## File Map

- Create: GeneralHelpers/SourceClientLanguage.cs - separate persistence identity and provider source code for the live client.
- Create: GeneralHelpers/TranslationReuseScope.cs - shared source/target/engine predicate.
- Create: Echoglossian.Tests/TranslationReuseScopeTests.cs - pure scope regression tests.
- Modify: GeneralHelpers/RuntimeLanguageHelper.cs - central raw ClientLanguage resolver and legacy normalization.
- Modify: DBHelpers/DbOperations.cs - all dialogue, toast, quest, select-string, hint, and legacy GameWindow retrieval paths.
- Modify: DBHelpers/EntitiesHelper.cs and NativeUI/Helpers/GenericAddonHandlerHelper.cs - normalized source writes.
- Modify: Cache/ActionTooltipCacheManager.cs, Cache/ItemTooltipCacheManager.cs, Cache/TraitCacheManager.cs, Cache/ReferenceTextCacheStore.cs, Cache/ReferenceTextCacheRegistry.cs, Cache/StringArrayDataCacheManager.cs, Cache/GameWindowCacheManager.cs - scoped lookup APIs.
- Modify: DBHelpers/ActionItemTooltipDbOperations.cs, DBHelpers/ReferenceTextDbOperations.cs, and the canonical persistence helpers - scope propagation.
- Modify: NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs - scoped GameWindow/StringArrayData recovery.
- Modify: NativeUI/AddonHandlers/Character/*.cs and NativeUI/AddonHandlers/ActionMenu/ActionMenuWindowHandler.cs - remove literal source/target fallbacks.
- Test: existing DbOperations, cache, persistence, Character, and ActionMenu test files.

## Task 1: Shared Scope Contract

**Files:**
- Create: GeneralHelpers/SourceClientLanguage.cs
- Create: GeneralHelpers/TranslationReuseScope.cs
- Modify: GeneralHelpers/RuntimeLanguageHelper.cs
- Create: Echoglossian.Tests/TranslationReuseScopeTests.cs

**Interfaces:**
- Produces bool RuntimeLanguageHelper.TryResolveSourceLanguage(ClientLanguage clientLanguage, out SourceClientLanguage sourceLanguage).
- Produces bool RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(out SourceClientLanguage sourceLanguage).
- Produces bool TranslationReuseScope.TryCreate(Config config, out TranslationReuseScope scope).
- Produces bool TranslationReuseScope.Matches(string? storedSourceLanguage, string? storedTargetLanguage, int? storedEngine).

- [ ] **Step 1: Write failing unit tests**

~~~csharp
[Theory]
[InlineData("English", "en")]
[InlineData("Deutsch", "de")]
[InlineData("Japanese", "ja")]
[InlineData("French", "fr")]
public void Matches_LegacyStoredSourceName_AcceptsEquivalentCode(
    string storedSource, string requestedSource)
{
    var scope = new TranslationReuseScope(requestedSource, "iw", 4, false);

    Assert.True(scope.Matches(storedSource, "iw", 9));
}

[Fact]
public void Matches_DifferentSourceOrTarget_ReturnsFalse()
{
    var scope = new TranslationReuseScope("ja", "iw", 4, false);

    Assert.False(scope.Matches("en", "iw", 4));
    Assert.False(scope.Matches("ja", "fa", 4));
}

[Fact]
public void Matches_RetranslationEnabled_RequiresActiveEngine()
{
    var scope = new TranslationReuseScope("en", "iw", 4, true);

    Assert.False(scope.Matches("en", "iw", 7));
}

[Theory]
[InlineData(4, "chs", "zh-CN")]
[InlineData(5, "cht", "zh-CN")]
[InlineData(6, "ko", "ko")]
[InlineData(7, "tc", "zh-TW")]
public void TryResolveSourceLanguage_ExtendedClientValue_ReturnsDistinctIdentity(
    int rawClientLanguage,
    string expectedPersistenceCode,
    string expectedProviderCode)
{
    var resolved = RuntimeLanguageHelper.TryResolveSourceLanguage(
        (ClientLanguage)rawClientLanguage,
        out var sourceLanguage);

    Assert.True(resolved);
    Assert.Equal(expectedPersistenceCode, sourceLanguage.PersistenceCode);
    Assert.Equal(expectedProviderCode, sourceLanguage.ProviderCode);
}

[Fact]
public void TryResolveSourceLanguage_UnknownClientValue_ReturnsFalse()
{
    var resolved = RuntimeLanguageHelper.TryResolveSourceLanguage(
        (ClientLanguage)99,
        out _);

    Assert.False(resolved);
}
~~~

- [ ] **Step 2: Run the test and observe RED**

Run: dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~TranslationReuseScopeTests

Expected: compile failure because SourceClientLanguage and the TryResolve APIs are missing.

- [ ] **Step 3: Implement the pure scope value object**

~~~csharp
internal readonly record struct SourceClientLanguage(
    string PersistenceCode,
    string ProviderCode);

public static bool TryResolveSourceLanguage(
    ClientLanguage clientLanguage,
    out SourceClientLanguage sourceLanguage)
{
    sourceLanguage = (int)clientLanguage switch
    {
        0 => new SourceClientLanguage("ja", "ja"),
        1 => new SourceClientLanguage("en", "en"),
        2 => new SourceClientLanguage("de", "de"),
        3 => new SourceClientLanguage("fr", "fr"),
        4 => new SourceClientLanguage("chs", "zh-CN"),
        5 => new SourceClientLanguage("cht", "zh-CN"),
        6 => new SourceClientLanguage("ko", "ko"),
        7 => new SourceClientLanguage("tc", "zh-TW"),
        _ => default,
    };

    if (!string.IsNullOrWhiteSpace(sourceLanguage.PersistenceCode))
    {
        return true;
    }

    try
    {
        var hostCode = NormalizeLanguage(clientLanguage.ToCode());
        if (!string.IsNullOrWhiteSpace(hostCode))
        {
            sourceLanguage = new SourceClientLanguage(hostCode, hostCode);
            return true;
        }
    }
    catch (ArgumentOutOfRangeException)
    {
        // The current host does not expose an identity for this raw value.
    }

    return false;
}

internal readonly record struct TranslationReuseScope(
    string SourceLanguageCode,
    string TargetLanguageCode,
    int? TranslationEngine,
    bool RequireMatchingEngine)
{
    public static bool TryCreate(Config config, out TranslationReuseScope scope)
    {
        if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
                out var sourceLanguage))
        {
            scope = default;
            return false;
        }

        var targetLanguage =
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(config.Lang);
        if (string.IsNullOrWhiteSpace(targetLanguage))
        {
            scope = default;
            return false;
        }

        scope = new TranslationReuseScope(
            sourceLanguage.PersistenceCode,
            targetLanguage,
            config.ChosenTransEngine,
            config.TranslateAlreadyTranslatedTexts);
        return true;
    }

    public bool Matches(
        string? storedSourceLanguage,
        string? storedTargetLanguage,
        int? storedEngine)
    {
        return !string.IsNullOrWhiteSpace(storedSourceLanguage) &&
               RuntimeLanguageHelper.LanguagesMatch(
                   storedSourceLanguage, this.SourceLanguageCode) &&
               RuntimeLanguageHelper.LanguagesMatch(
                   storedTargetLanguage, this.TargetLanguageCode) &&
               (!this.RequireMatchingEngine ||
                storedEngine == this.TranslationEngine);
    }
}
~~~

Do not put provider aliases in TranslationReuseScope. SourceClientLanguage is the only place where the raw client value maps to a persistence identity and provider code. Do not add a default English case.

- [ ] **Step 4: Run the test and observe GREEN**

Run: dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~TranslationReuseScopeTests

Expected: all scope tests pass.

- [ ] **Step 5: Commit**

~~~powershell
git add -- GeneralHelpers/SourceClientLanguage.cs GeneralHelpers/TranslationReuseScope.cs GeneralHelpers/RuntimeLanguageHelper.cs Echoglossian.Tests/TranslationReuseScopeTests.cs
git commit -m "#139 Add extended client language scope"
~~~

## Task 2: Persist Resolved Source Identities and Use Provider Codes

**Files:**
- Modify: DBHelpers/EntitiesHelper.cs
- Modify: NativeUI/Helpers/GenericAddonHandlerHelper.cs
- Modify: NativeUI/Helpers/ActionDetailPrefetchRuntime.cs
- Modify: NativeUI/Helpers/ItemDetailPrefetchRuntime.cs
- Modify: NativeUI/Helpers/TraitDetailPrefetchRuntime.cs
- Modify: NativeUI/Helpers/ReferenceTextPrefetchRuntime.cs
- Modify: NativeUI/Helpers/AcceptedQuestPrefetchRuntime.cs
- Modify: NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs
- Modify: NativeUI/AddonHandlers/CutSceneSelectString/CutSceneSelectStringHandler.cs
- Modify: NativeUI/AddonHandlers/Quest/QuestAddonHandlerBase.cs
- Modify: NativeUI/AddonHandlers/Toasts/TextGimmickHintHandler.cs
- Modify: NativeUI/AddonHandlers/Talk/TalkHandler.cs
- Modify: NativeUI/AddonHandlers/Talk/BattleTalkHandler.cs
- Modify: NativeUI/AddonHandlers/Talk/TalkSubtitleHandler.cs
- Modify: NativeUI/AddonHandlers/SingleText/MiniTalkHandler.cs
- Modify: NativeUI/AddonHandlers/Toasts/ToastGuiSupportedToastRuntime.cs
- Modify: NativeUI/AddonHandlers/Toasts/ToastGuiCaptureRuntime.cs
- Modify: NativeUI/AddonHandlers/Toasts/QuestToastRuntime.cs
- Modify: NativeUI/AddonHandlers/Toasts/AddonTextToastHandler.cs
- Modify: Translators/Helpers/Glossian.cs
- Test: Echoglossian.Tests/DbOperationsTests.cs and Echoglossian.Tests/RuntimeLanguageHelperTests.cs

**Interfaces:**
- All OriginalLang and equivalent fields receive SourceClientLanguage.PersistenceCode.
- Every live-client TranslationService call receives SourceClientLanguage.ProviderCode.
- A caller that cannot resolve the source client language returns before cache lookup, provider submission, persistence, or native mutation.

- [ ] **Step 1: Write a failing persisted identity test**

~~~csharp
[Fact]
public void FormatToastMessage_PersistsResolvedSourceIdentity()
{
    var row = plugin.FormatToastMessage("Area", "Test");

    Assert.True(RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
        out var sourceLanguage));
    Assert.Equal(
        sourceLanguage.PersistenceCode,
        row.OriginalToastMessageLang);
}
~~~

Use existing plugin-service doubles. Do not create another client-state abstraction.

- [ ] **Step 2: Run the test and observe RED**

Run: dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~DbOperationsTests

Expected: the stored field contains the old Humanize value, such as English.

- [ ] **Step 3: Split provider input from persisted identity**

~~~csharp
if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
        out var sourceLanguage))
{
    return;
}

var translatedText = TranslationService.Translate(
    originalText,
    sourceLanguage.ProviderCode,
    targetLanguage);

row.OriginalLang = sourceLanguage.PersistenceCode;
~~~

Apply this to all live-client translation submissions, row constructors, and
SetOriginalLang calls in the listed files. Do not pass Humanize(), ToString(),
or a raw enum number to TranslationService. A source-resolution failure must
return before any native state change.

- [ ] **Step 4: Run focused persistence tests**

Run: dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~DbOperationsTests|FullyQualifiedName~RuntimeLanguageHelperTests"

Expected: all selected tests pass.

- [ ] **Step 5: Commit**

~~~powershell
git add -- DBHelpers/EntitiesHelper.cs NativeUI/Helpers NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs NativeUI/AddonHandlers/CutSceneSelectString NativeUI/AddonHandlers/Quest/QuestAddonHandlerBase.cs NativeUI/AddonHandlers/Toasts/TextGimmickHintHandler.cs Echoglossian.Tests/DbOperationsTests.cs Echoglossian.Tests/RuntimeLanguageHelperTests.cs
git commit -m "#139 Persist resolved client source identities"
~~~

## Task 3: Apply the Scope to Legacy Database Retrieval

**Files:**
- Modify: DBHelpers/DbOperations.cs
- Modify: GeneralHelpers/TranslationPersistenceGuard.cs
- Test: Echoglossian.Tests/DbOperationsTests.cs

**Interfaces:**
- Each stored entity retrieval uses TranslationReuseScope.TryCreate(this.configuration, out scope).
- A failed scope resolution returns no stored row and queues no translation work.
- The existing semantic identity remains the SQL predicate; source, target, and engine policy are applied before selection.

- [ ] **Step 1: Add failing cross-source and engine tests**

~~~csharp
[Fact]
public void FindTalkData_DifferentStoredSourceLanguage_ReturnsNull()
{
    // Seed an English and Japanese row with the same source text and target.
    // Request the Japanese row scope; assert the English row is not reusable.
}

[Fact]
public void FindTalkData_RetranslationEnabled_RejectsOtherEngine()
{
    // Seed matching source/target rows for engines 4 and 7.
    // Enable TranslateAlreadyTranslatedTexts and request engine 4.
}
~~~

Use the same helper pattern for TalkMessage, BattleTalkMessage, ToastMessage, QuestPlate, TalkSubtitleMessage, MiniTalkMessage, TextGimmickHintMessage, SelectString, and legacy GameWindow.

- [ ] **Step 2: Run the test and observe RED**

Run: dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~DbOperationsTests

Expected: at least one mismatched-source row is returned.

- [ ] **Step 3: Apply the scope after the existing semantic query**

~~~csharp
if (!TranslationReuseScope.TryCreate(this.configuration, out var scope))
{
    return null;
}
var candidates = existingTalkMessage
    .AsEnumerable()
    .Where(row => scope.Matches(
        row.OriginalTalkMessageLang,
        row.TranslationLang,
        row.TranslationEngine));

var localFoundTalkMessage = OrderTalkMessageLookupQuery(
    candidates.AsQueryable()).FirstOrDefault();
~~~

Use the actual source-language field for every model. Replace local engine conditionals with scope.Matches, preserve ordering, source hash, and version behavior.

- [ ] **Step 4: Run focused tests and observe GREEN**

Run: dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~DbOperationsTests

Expected: all selected tests pass.

- [ ] **Step 5: Commit**

~~~powershell
git add -- DBHelpers/DbOperations.cs GeneralHelpers/TranslationPersistenceGuard.cs Echoglossian.Tests/DbOperationsTests.cs
git commit -m "#139 Scope legacy translation reuse by client language"
~~~

## Task 4: Scope DB-First, Tooltip, and Reference Caches

**Files:**
- Modify: Cache/ActionTooltipCacheManager.cs
- Modify: Cache/ItemTooltipCacheManager.cs
- Modify: Cache/TraitCacheManager.cs
- Modify: Cache/ReferenceTextCacheStore.cs
- Modify: Cache/ReferenceTextCacheRegistry.cs
- Modify: Cache/StringArrayDataCacheManager.cs
- Modify: Cache/GameWindowCacheManager.cs
- Modify: DBHelpers/ActionItemTooltipDbOperations.cs
- Modify: DBHelpers/ReferenceTextDbOperations.cs
- Modify: DBHelpers/ActionTooltipPersistenceHelper.cs
- Modify: DBHelpers/ItemTooltipPersistenceHelper.cs
- Modify: DBHelpers/TraitPersistenceHelper.cs
- Modify: DBHelpers/ReferenceTextPersistenceHelper.cs
- Modify: DBHelpers/StringArrayDataPersistenceHelper.cs
- Modify: DBHelpers/GameWindowPersistenceHelper.cs
- Test: existing cache and persistence tests.

**Interfaces:**
- Cache APIs select rows with TranslationReuseScope, not independent target/engine parameters.
- Canonical, historical, identity, forward text, and reverse text lookup all evaluate the same scope.

- [ ] **Step 1: Add failing cache tests**

~~~csharp
[Fact]
public void TryFindCanonicalMatch_DifferentSourceLanguage_ReturnsNull()
{
    ActionTooltipCacheManager.Update(new ActionTooltip
    {
        ActionId = 15998,
        OriginalLang = "en",
        TranslationLang = "iw",
        TranslationEngine = 4,
        SourceContentHash = "english-hash",
    });

    var scope = new TranslationReuseScope("ja", "iw", 4, false);

    Assert.Null(ActionTooltipCacheManager.TryFindCanonicalMatch(
        15998, scope, "7.3", "english-hash"));
}
~~~

Repeat for item, trait, reference text, StringArrayData, and GameWindow candidates. Add a reverse-map test proving a translated text cannot restore an original from another source language.

- [ ] **Step 2: Run the cache tests and observe RED**

Run: dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~ActionTooltipCacheManagerTests|FullyQualifiedName~ActionItemTooltipPersistenceTests|FullyQualifiedName~StringArrayDataPersistenceTests|FullyQualifiedName~GameWindowCacheManagerTests"

Expected: the current identity or text-map lookup returns the mismatched row.

- [ ] **Step 3: Scope cache and persistence predicates**

~~~csharp
return rows.Where(row =>
        scope.Matches(
            row.OriginalLang,
            row.TranslationLang,
            row.TranslationEngine) &&
        row.SourceContentHash == sourceContentHash &&
        GameVersionLookupHelper.MatchesStoredVersion(
            row.GameVersion,
            gameVersion))
    .OrderByDescending(row => ComputeCanonicalMatchScore(
        row,
        gameVersion))
    .FirstOrDefault();
~~~

For persistence upserts, include the stored source language in existing-row selection so Japanese and English rows are separate records even when id, target, engine, version, and hash coincide.

- [ ] **Step 4: Update callers in one compiler-driven pass**

~~~csharp
if (!TranslationReuseScope.TryCreate(this.configuration, out var scope))
{
    return null;
}
var row = ActionTooltipCacheManager.TryFindCanonicalMatch(
    actionId,
    scope,
    gameVersion,
    sourceContentHash);
~~~

Thread scope through ActionItemTooltipDbOperations, ReferenceTextDbOperations, ReferenceTextCacheRegistry, and DbFirstGameWindowAddonHandler. Delete target-plus-engine-only overloads so a new caller cannot bypass source scope.

- [ ] **Step 5: Run focused tests and observe GREEN**

Run: dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~ActionTooltipCacheManagerTests|FullyQualifiedName~ActionItemTooltipPersistenceTests|FullyQualifiedName~StringArrayDataPersistenceTests|FullyQualifiedName~GameWindowCacheManagerTests|FullyQualifiedName~GameWindowPersistenceTests|FullyQualifiedName~MainCommandTextPersistenceTests"

Expected: all selected tests pass and no cross-source row is reusable.

- [ ] **Step 6: Commit**

~~~powershell
git add -- Cache DBHelpers/ActionItemTooltipDbOperations.cs DBHelpers/ReferenceTextDbOperations.cs DBHelpers/ActionTooltipPersistenceHelper.cs DBHelpers/ItemTooltipPersistenceHelper.cs DBHelpers/TraitPersistenceHelper.cs DBHelpers/ReferenceTextPersistenceHelper.cs DBHelpers/StringArrayDataPersistenceHelper.cs DBHelpers/GameWindowPersistenceHelper.cs Echoglossian.Tests
git commit -m "#139 Scope canonical caches by source language"
~~~

## Task 5: Remove Handler-Specific Language Fallbacks

**Files:**
- Modify: NativeUI/AddonHandlers/Character/CharacterTextNodeWindowHandlerBase.cs
- Modify: NativeUI/AddonHandlers/Character/CharacterWindowHandler.cs
- Modify: NativeUI/AddonHandlers/Character/CharacterStatusSubWindowHandler.cs
- Modify: NativeUI/AddonHandlers/ActionMenu/ActionMenuWindowHandler.cs
- Modify: NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs
- Test: CharacterWindowHandlerTests.cs, CharacterStatusSubWindowHandlerTests.cs, ActionMenuWindowHandlerTests.cs

**Interfaces:**
- Root Character labels are captured and persisted through the scoped canonical DB-first flow.
- ActionMenu reverse resolution consumes a scoped canonical original; it never synthesizes an English level label.

- [ ] **Step 1: Write failing handler tests**

~~~csharp
[Fact]
public void HasExpectedTranslatedSectionCoverage_GermanOriginals_ReturnsTrue()
{
    var original = Payload(
        "Attribute",
        "Offensivwerte",
        "Verteidigungswerte");
    var translated = Payload(
        "תכונות",
        "ערכי התקפה",
        "ערכי הגנה");

    Assert.True(CharacterStatusSubWindowHandler
        .HasExpectedTranslatedSectionCoverage(original, translated));
}

[Fact]
public void CharacterHeaders_HaveNoLiteralPortugueseFallback()
{
    Assert.False(CharacterWindowHandler.HasLocalHeaderFallbacks);
}
~~~

Add an ActionMenu test with a non-English source level label; when no scoped canonical original exists, resolution must not produce Lv.

- [ ] **Step 2: Run the handler tests and observe RED**

Run: dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~CharacterWindowHandlerTests|FullyQualifiedName~CharacterStatusSubWindowHandlerTests|FullyQualifiedName~ActionMenuWindowHandlerTests"

Expected: Character exposes the literal map and ActionMenu reconstructs the English token.

- [ ] **Step 3: Use canonical payloads instead of literals**

~~~csharp
// Character: no translated text is appended locally.
return this.TryBuildCharacterLookups(
    out originalLookup,
    out translatedLookup,
    out knownTexts);

// ActionMenu: do not reverse a partial level decomposition without
// a complete original from the current source-language scope.
if (!canonicalOriginalLookup.TryGetValue(visibleText, out originalText))
{
    return false;
}
~~~

Delete StableHeaderFallbackTranslations, AppendStableHeaderFallbackTranslations, and the Lv. normalization branch. Enable the existing DB-first queue/persistence path for root Character header payloads while keeping dynamic subwindow ownership isolated. Replace CharacterStatus English title membership with a minimum same-slot changed-content threshold.

- [ ] **Step 4: Run the handler tests and observe GREEN**

Run: dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~CharacterWindowHandlerTests|FullyQualifiedName~CharacterStatusSubWindowHandlerTests|FullyQualifiedName~ActionMenuWindowHandlerTests"

Expected: all selected tests pass without a literal source-to-target map.

- [ ] **Step 5: Commit**

~~~powershell
git add -- NativeUI/AddonHandlers/Character NativeUI/AddonHandlers/ActionMenu/ActionMenuWindowHandler.cs NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs Echoglossian.Tests/CharacterWindowHandlerTests.cs Echoglossian.Tests/CharacterStatusSubWindowHandlerTests.cs Echoglossian.Tests/ActionMenuWindowHandlerTests.cs
git commit -m "#139 Remove handler-specific language fallbacks"
~~~

## Task 6: Full Verification and In-Game Matrix

**Files:**
- Modify only if required: GeneralHelpers/RuntimeConfigurationRefresh.cs
- Test: full Echoglossian.Tests suite.

- [ ] **Step 1: Add source/target transition coverage**

~~~csharp
[Fact]
public void TranslationReuseScope_TargetOrSourceChange_RejectsPriorRow()
{
    var original = new TranslationReuseScope("en", "iw", 4, false);
    var changedSource = new TranslationReuseScope("de", "iw", 4, false);
    var changedTarget = new TranslationReuseScope("en", "fa", 4, false);

    Assert.True(original.Matches("en", "iw", 4));
    Assert.False(changedSource.Matches("en", "iw", 4));
    Assert.False(changedTarget.Matches("en", "iw", 4));
}

[Fact]
public void TranslationReuseScope_ChineseClientIdentities_DoNotCrossReuse()
{
    var simplifiedChina = new TranslationReuseScope("chs", "iw", 4, false);

    Assert.True(simplifiedChina.Matches("chs", "iw", 4));
    Assert.False(simplifiedChina.Matches("cht", "iw", 4));
    Assert.False(simplifiedChina.Matches("tc", "iw", 4));
}
~~~

- [ ] **Step 2: Run the build**

Run: dotnet build .\Echoglossian.sln -c Debug --no-restore

Expected: exit code 0. Report existing package advisories only if they remain.

- [ ] **Step 3: Run the full test suite**

Run: dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build

Expected: exit code 0 and all tests pass.

- [ ] **Step 4: Verify in game**

1. On the English FFXIV client, use target he and open Character, ActionMenu, ActionDetail, ItemDetail, a quest, and dialogue.
2. Change target language and verify he rows do not appear.
3. Change active engine with TranslateAlreadyTranslatedTexts enabled and verify the prior-engine row is not reused.
4. Change FFXIV client to German, Japanese, or French and verify the English-source row is not reused.
5. On an extended client, verify raw values 4, 5, 6, and 7 persist chs, cht, ko, and tc and submit zh-CN, zh-CN, ko, and zh-TW to the provider.
6. Verify a row created by raw client value 4 is not reused by raw client value 5, even though both provider calls use zh-CN.
7. Return to the original client language and verify only scope-compatible rows are reused.

- [ ] **Step 5: Commit verification adjustments**

~~~powershell
git add -- GeneralHelpers/RuntimeConfigurationRefresh.cs Echoglossian.Tests
git commit -m "#139 Verify canonical translation reuse transitions"
~~~

## Plan Self-Review

- Spec coverage: Task 1 resolves source identities from raw client values and constructs a fail-closed scope. Tasks 2-4 carry that identity through provider calls, persistence, and caches. Task 5 removes handler-local translation semantics. Task 6 validates source, target, engine, client-language, and Chinese-client transitions.
- Placeholder scan: each behavior has a named owner, test, command, and commit boundary.
- Type consistency: SourceClientLanguage supplies persistence and provider codes; TranslationReuseScope.TryCreate consumes the persistence code and is passed to cache and persistence lookups for uniform source, target, and engine evaluation.

## Execution Handoff

Plan saved to docs/superpowers/plans/2026-07-13-issue-139-canonical-translation-reuse-scope-plan.md.

1. **Subagent-Driven (recommended)** - dispatch a fresh subagent per task, review between tasks.
2. **Inline Execution** - execute the tasks in this session using superpowers:executing-plans, with checkpoints.

## As-Built Status (2026-07-13)

The planned contracts are implemented. Live work captures one
`SourceClientLanguage` from raw `IClientState.ClientLanguage`; persistence uses
its `PersistenceCode`, while `TranslationService` receives the contract and
selects `ProviderCode` internally. Reuse requires the full source, target,
game/version/content, and configured engine-policy predicates. Unknown source
fails closed. The manual matrix in
`docs/issue-139-canonical-language-validation.md` remains not run.
