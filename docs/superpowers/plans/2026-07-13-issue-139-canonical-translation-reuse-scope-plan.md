# Canonical Translation Reuse Scope Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Make source client language, target language, engine policy, version, and source content a mandatory and uniform translation-reuse scope for every Echoglossian surface.

**Architecture:** Add one pure shared scope object that owns language and engine compatibility. Persist new source-language identities as normalized codes, preserve legacy named values through normalized comparison, and make every DB/cache retrieval path use the scope before returning a translation. Handlers retain only addon capture, native mutation, and layout responsibilities.

**Tech Stack:** C#/.NET 10, Dalamud IClientState, EF Core SQLite, xUnit, FFXIVClientStructs.

## Global Constraints

- Source language is always RuntimeLanguageHelper.GetCurrentGameLanguageCode() for persisted identity.
- A row is reusable only when source and target language match the requested scope.
- TranslateAlreadyTranslatedTexts true additionally requires the active engine; false permits another compatible engine.
- Empty or unknown stored source language is not reusable.
- Keep provider-facing ClientLanguage.Humanize() separate from persisted identity.
- Do not add a schema migration. Normalize legacy source names during comparison.
- Preserve existing game-version and source-content hash checks.
- Overlay-only mode must not mutate native addon state.
- Commit each completed task with #139.

---

## File Map

- Create: GeneralHelpers/TranslationReuseScope.cs - shared source/target/engine predicate.
- Create: Echoglossian.Tests/TranslationReuseScopeTests.cs - pure scope regression tests.
- Modify: GeneralHelpers/RuntimeLanguageHelper.cs - central current-client source identity.
- Modify: DBHelpers/DbOperations.cs - all dialogue, toast, quest, select-string, hint, and legacy GameWindow retrieval paths.
- Modify: DBHelpers/EntitiesHelper.cs and NativeUI/Helpers/GenericAddonHandlerHelper.cs - normalized source writes.
- Modify: Cache/ActionTooltipCacheManager.cs, Cache/ItemTooltipCacheManager.cs, Cache/TraitCacheManager.cs, Cache/ReferenceTextCacheStore.cs, Cache/ReferenceTextCacheRegistry.cs, Cache/StringArrayDataCacheManager.cs, Cache/GameWindowCacheManager.cs - scoped lookup APIs.
- Modify: DBHelpers/ActionItemTooltipDbOperations.cs, DBHelpers/ReferenceTextDbOperations.cs, and the canonical persistence helpers - scope propagation.
- Modify: NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs - scoped GameWindow/StringArrayData recovery.
- Modify: NativeUI/AddonHandlers/Character/*.cs and NativeUI/AddonHandlers/ActionMenu/ActionMenuWindowHandler.cs - remove literal source/target fallbacks.
- Test: existing DbOperations, cache, persistence, Character, and ActionMenu test files.

## Task 1: Shared Scope Contract

**Files:**
- Create: GeneralHelpers/TranslationReuseScope.cs
- Modify: GeneralHelpers/RuntimeLanguageHelper.cs
- Create: Echoglossian.Tests/TranslationReuseScopeTests.cs

**Interfaces:**
- Produces TranslationReuseScope.Create(Config config).
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
~~~

- [ ] **Step 2: Run the test and observe RED**

Run: dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~TranslationReuseScopeTests

Expected: compile failure because TranslationReuseScope is missing.

- [ ] **Step 3: Implement the pure scope value object**

~~~csharp
internal readonly record struct TranslationReuseScope(
    string SourceLanguageCode,
    string TargetLanguageCode,
    int? TranslationEngine,
    bool RequireMatchingEngine)
{
    public static TranslationReuseScope Create(Config config)
    {
        return new TranslationReuseScope(
            RuntimeLanguageHelper.GetCurrentGameLanguageCode(),
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(config.Lang),
            config.ChosenTransEngine,
            config.TranslateAlreadyTranslatedTexts);
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

Do not put provider aliases in this type. RuntimeLanguageHelper remains the only converter from ClientState.ClientLanguage to a source-language code.

- [ ] **Step 4: Run the test and observe GREEN**

Run: dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~TranslationReuseScopeTests

Expected: all scope tests pass.

- [ ] **Step 5: Commit**

~~~powershell
git add -- GeneralHelpers/TranslationReuseScope.cs GeneralHelpers/RuntimeLanguageHelper.cs Echoglossian.Tests/TranslationReuseScopeTests.cs
git commit -m "#139 Add canonical translation reuse scope"
~~~

## Task 2: Normalize Every Persisted Source-Language Write

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
- Test: Echoglossian.Tests/DbOperationsTests.cs and Echoglossian.Tests/RuntimeLanguageHelperTests.cs

**Interfaces:**
- All OriginalLang and equivalent fields receive the normalized current game language code.
- TranslationService calls retain the provider-facing source language separately.

- [ ] **Step 1: Write a failing persisted identity test**

~~~csharp
[Fact]
public void FormatToastMessage_PersistsNormalizedCurrentSourceLanguage()
{
    var row = plugin.FormatToastMessage("Area", "Test");

    Assert.Equal(
        RuntimeLanguageHelper.GetCurrentGameLanguageCode(),
        row.OriginalToastMessageLang);
}
~~~

Use existing plugin-service doubles. Do not create another client-state abstraction.

- [ ] **Step 2: Run the test and observe RED**

Run: dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~DbOperationsTests

Expected: the stored field contains the old Humanize value, such as English.

- [ ] **Step 3: Split provider input from persisted identity**

~~~csharp
var providerSourceLanguage = ClientStateInterface.ClientLanguage.Humanize();
var persistedSourceLanguage =
    RuntimeLanguageHelper.GetCurrentGameLanguageCode();

var translatedText = TranslationService.Translate(
    originalText,
    providerSourceLanguage,
    targetLanguage);

row.OriginalLang = persistedSourceLanguage;
~~~

Apply this only to row constructors and SetOriginalLang calls. Leave logging and provider calls unchanged.

- [ ] **Step 4: Run focused persistence tests**

Run: dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~DbOperationsTests|FullyQualifiedName~RuntimeLanguageHelperTests"

Expected: all selected tests pass.

- [ ] **Step 5: Commit**

~~~powershell
git add -- DBHelpers/EntitiesHelper.cs NativeUI/Helpers NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs NativeUI/AddonHandlers/CutSceneSelectString NativeUI/AddonHandlers/Quest/QuestAddonHandlerBase.cs NativeUI/AddonHandlers/Toasts/TextGimmickHintHandler.cs Echoglossian.Tests/DbOperationsTests.cs Echoglossian.Tests/RuntimeLanguageHelperTests.cs
git commit -m "#139 Persist canonical client source language"
~~~

## Task 3: Apply the Scope to Legacy Database Retrieval

**Files:**
- Modify: DBHelpers/DbOperations.cs
- Modify: GeneralHelpers/TranslationPersistenceGuard.cs
- Test: Echoglossian.Tests/DbOperationsTests.cs

**Interfaces:**
- Each stored entity retrieval uses TranslationReuseScope.Create(this.configuration).
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
var scope = TranslationReuseScope.Create(this.configuration);
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
var scope = TranslationReuseScope.Create(this.configuration);
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
5. Return to the original client language and verify only scope-compatible rows are reused.

- [ ] **Step 5: Commit verification adjustments**

~~~powershell
git add -- GeneralHelpers/RuntimeConfigurationRefresh.cs Echoglossian.Tests
git commit -m "#139 Verify canonical translation reuse transitions"
~~~

## Plan Self-Review

- Spec coverage: Tasks 1-4 establish and apply one source/target/engine scope to persistence and caches. Task 5 removes handler-local translation semantics. Task 6 validates source, target, engine, and client-language transitions.
- Placeholder scan: each behavior has a named owner, test, command, and commit boundary.
- Type consistency: TranslationReuseScope is created from Config, passed to cache and persistence lookups, and evaluates stored source, target, and engine uniformly.

## Execution Handoff

Plan saved to docs/superpowers/plans/2026-07-13-issue-139-canonical-translation-reuse-scope-plan.md.

1. **Subagent-Driven (recommended)** - dispatch a fresh subagent per task, review between tasks.
2. **Inline Execution** - execute the tasks in this session using superpowers:executing-plans, with checkpoints.
