// <copyright file="SourceScopedFallbackFlowTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

using Dalamud.Plugin.Services;

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.LanguagesHandling;
using Echoglossian.NativeUI.AddonHandlers.ActionMenu;
using Echoglossian.NativeUI.AddonHandlers.Character;
using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.Helpers;

using Microsoft.EntityFrameworkCore;

using Xunit;

using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>
///     Covers operation-scoped source identity in persisted fallback flows.
/// </summary>
public class SourceScopedFallbackFlowTests
{
    /// <summary>
    ///     Ensures an empty source-scoped Character cache cannot synthesize a
    ///     Portuguese root-header translation.
    /// </summary>
    [Fact]
    public void CharacterFallback_EmptyScopedCaches_DoesNotSynthesizeHeader()
    {
        using var runtimeScope = new TestRuntimeScope();
        StringArrayDataCacheManager.Clear();
        GameWindowCacheManager.Clear();

        try
        {
            var handler = new CharacterWindowHandler(
                new Config
                {
                    Lang = 28,
                    ChosenTransEngine = 0,
                },
                null!,
                null!);
            var originalPayload = new DbFirstGameWindowPayload(
                new SortedDictionary<int, string>
                {
                    [17] = "Character",
                },
                new SortedDictionary<int, string>(),
                new SortedDictionary<string, string>(StringComparer.Ordinal));

            var found = TryResolveCharacterTranslatedPayload(
                handler,
                new SourceClientLanguage("en", "en"),
                originalPayload,
                out var translatedPayload);

            Assert.False(found);
            Assert.Empty(translatedPayload.AtkValues);
            Assert.Empty(translatedPayload.StringArrayValues);
            Assert.Empty(translatedPayload.TextNodes);
        }
        finally
        {
            StringArrayDataCacheManager.Clear();
            GameWindowCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures Character fallback reuse requires the operation-captured
    ///     source while retaining matching-source persisted reuse.
    /// </summary>
    [Fact]
    public void CharacterPersistedFallback_RequiresMatchingSource()
    {
        using var runtimeScope = new TestRuntimeScope();
        StringArrayDataCacheManager.Clear();
        GameWindowCacheManager.Clear();

        try
        {
            var handler = new CharacterWindowHandler(
                new Config
                {
                    Lang = 28,
                    ChosenTransEngine = 0,
                },
                null!,
                null!);
            var originalPayload = new DbFirstGameWindowPayload(
                new SortedDictionary<int, string>
                {
                    [17] = "Personnage",
                },
                new SortedDictionary<int, string>(),
                new SortedDictionary<string, string>(StringComparer.Ordinal));
            var translatedPayload = new DbFirstGameWindowPayload(
                new SortedDictionary<int, string>
                {
                    [17] = "Personagem",
                },
                new SortedDictionary<int, string>(),
                new SortedDictionary<string, string>(StringComparer.Ordinal));

            GameWindowCacheManager.Update(new GameWindow(
                windowAddonName: "Character",
                originalWindowStrings: originalPayload.Serialize(),
                originalWindowStringsLang: "fr",
                translatedWindowStrings: translatedPayload.Serialize(),
                translationLang: "pt",
                translationEngine: 0,
                gameVersion: "test-version",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow));

            var matchingFound = TryResolveCharacterTranslatedPayload(
                handler,
                new SourceClientLanguage("fr", "fr"),
                originalPayload,
                out var matchingPayload);
            var mismatchedFound = TryResolveCharacterTranslatedPayload(
                handler,
                new SourceClientLanguage("de", "de"),
                originalPayload,
                out var mismatchedPayload);

            Assert.True(matchingFound);
            Assert.Equal("Personagem", matchingPayload.AtkValues[17]);
            Assert.False(mismatchedFound);
            Assert.Empty(mismatchedPayload.AtkValues);
        }
        finally
        {
            StringArrayDataCacheManager.Clear();
            GameWindowCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures the live structured-payload DB fallback passes its configured
    ///     engine reuse policy through to persistence.
    /// </summary>
    [Fact]
    public void StructuredPayloadPersistedFallback_CompatiblePolicyReusesDifferentEngineRow()
    {
        using var runtimeScope = new TestRuntimeScope();
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        PluginEntry.ConfigDirectory = configDir + Path.DirectorySeparatorChar;
        StringArrayDataCacheManager.Clear();

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var originalPayload = new StringArrayStructuredPayload
            {
                Type = "Character",
                ContextKey = "Character:Profile",
                SchemaVersion = 1,
            };
            originalPayload.Slots[0] = new StringArrayStructuredSlot
            {
                SemanticKey = "name",
                OriginalText = "Profile",
                IsVisible = true,
                IsTranslatable = true,
            };
            var translatedPayload = new StringArrayStructuredPayload
            {
                Type = "Character",
                ContextKey = "Character:Profile",
                SchemaVersion = 1,
            };
            translatedPayload.Slots[0] = new StringArrayStructuredSlot
            {
                SemanticKey = "name",
                OriginalText = "Profile",
                TranslatedText = "Perfil",
                IsVisible = true,
                IsTranslatable = true,
            };
            StringArrayDataPersistenceHelper.InsertStringArrayData(
                configDir,
                StringArrayDataPersistenceHelper.CreateCanonicalRow(
                    "Character",
                    "en",
                    "pt-BR",
                    7,
                    "test-version",
                    originalPayload,
                    translatedPayload));
            var handler = new CharacterWindowHandler(
                new Config
                {
                    Lang = 28,
                    ChosenTransEngine = 0,
                    TranslateAlreadyTranslatedTexts = false,
                },
                null!,
                null!);
            var method = typeof(DbFirstGameWindowAddonHandler).GetMethod(
                "TryFindStructuredPayload",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            var arguments = new object?[]
            {
                new SourceClientLanguage("en", "en"),
                new TranslationReuseScope("en", "pt-BR", 0, false),
                originalPayload,
                null,
            };

            var found = Assert.IsType<bool>(method.Invoke(handler, arguments));
            var resolvedPayload = Assert.IsType<StringArrayStructuredPayload>(
                arguments[3]);

            Assert.True(found);
            Assert.Equal("Perfil", resolvedPayload.Slots[0].TranslatedText);
        }
        finally
        {
            StringArrayDataCacheManager.Clear();
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures ActionMenu recovery and diagnostics ignore a persisted row
    ///     from a different operation-captured source language.
    /// </summary>
    [Fact]
    public void ActionMenuPersistedFallback_DifferentSource_DoesNotReuseRow()
    {
        using var runtimeScope = new TestRuntimeScope();
        GameWindowCacheManager.Clear();

        try
        {
            var config = new Config
            {
                Lang = 28,
                ChosenTransEngine = 0,
            };
            var handler = new ActionMenuWindowHandler(
                config,
                null!,
                null!);
            var originalPayload = new DbFirstGameWindowPayload(
                new SortedDictionary<int, string>
                {
                    [17] = "Sprint",
                },
                new SortedDictionary<int, string>(),
                new SortedDictionary<string, string>(StringComparer.Ordinal));
            var translatedPayload = new DbFirstGameWindowPayload(
                new SortedDictionary<int, string>
                {
                    [17] = "Corrida",
                },
                new SortedDictionary<int, string>(),
                new SortedDictionary<string, string>(StringComparer.Ordinal));

            GameWindowCacheManager.Update(new GameWindow(
                windowAddonName: "ActionMenu",
                originalWindowStrings: originalPayload.Serialize(),
                originalWindowStringsLang: "en",
                translatedWindowStrings: translatedPayload.Serialize(),
                translationLang: "pt",
                translationEngine: 0,
                gameVersion: "test-version",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow));

            var matchingSource = new SourceClientLanguage("en", "en");
            var mismatchedSource = new SourceClientLanguage("de", "de");
            var matchingLookups = GetActionMenuLookups(
                handler,
                matchingSource);
            var mismatchedLookups = GetActionMenuLookups(
                handler,
                mismatchedSource);
            var matchingResolvedPayload = NormalizeActionMenuPayload(
                handler,
                matchingSource,
                originalPayload);
            var mismatchedResolvedPayload = NormalizeActionMenuPayload(
                handler,
                mismatchedSource,
                originalPayload);
            var matchingDiagnostics = GetActionMenuCandidateDiagnostics(
                handler,
                matchingSource);
            var mismatchedDiagnostics = GetActionMenuCandidateDiagnostics(
                handler,
                mismatchedSource);

            Assert.Equal("Corrida", matchingLookups.TranslatedLookup["Sprint"]);
            Assert.Equal("Sprint", matchingLookups.OriginalLookup["Corrida"]);
            Assert.Equal("Corrida", matchingResolvedPayload.AtkValues[17]);
            Assert.Equal(1, matchingDiagnostics.CandidateCount);
            Assert.Empty(mismatchedLookups.TranslatedLookup);
            Assert.Empty(mismatchedLookups.OriginalLookup);
            Assert.Equal("Sprint", mismatchedResolvedPayload.AtkValues[17]);
            Assert.Equal(0, mismatchedDiagnostics.CandidateCount);
        }
        finally
        {
            GameWindowCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures normalized exact scoped full labels win before forward and
    ///     reverse decomposition when live labels contain line breaks.
    /// </summary>
    [Fact]
    public void ActionMenuFullLabelFallback_NormalizedSpacingPrecedesDecomposition()
    {
        using var runtimeScope = new TestRuntimeScope();
        GameWindowCacheManager.Clear();

        try
        {
            var handler = new ActionMenuWindowHandler(
                new Config
                {
                    Lang = 28,
                    ChosenTransEngine = 0,
                },
                null!,
                null!);
            var persistedOriginalPayload = new DbFirstGameWindowPayload(
                new SortedDictionary<int, string>
                {
                    [17] = "Sprint Niveau 20",
                    [18] = "Sprint",
                },
                new SortedDictionary<int, string>(),
                new SortedDictionary<string, string>(StringComparer.Ordinal));
            var persistedTranslatedPayload = new DbFirstGameWindowPayload(
                new SortedDictionary<int, string>
                {
                    [17] = "Corrida Nv. 20",
                    [18] = "Corrida",
                },
                new SortedDictionary<int, string>(),
                new SortedDictionary<string, string>(StringComparer.Ordinal));
            GameWindowCacheManager.Update(new GameWindow(
                windowAddonName: "ActionMenu",
                originalWindowStrings: persistedOriginalPayload.Serialize(),
                originalWindowStringsLang: "fr",
                translatedWindowStrings: persistedTranslatedPayload.Serialize(),
                translationLang: "pt",
                translationEngine: 0,
                gameVersion: "test-version",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow));

            var sourceLanguage = new SourceClientLanguage("fr", "fr");
            var lookups = GetActionMenuLookups(handler, sourceLanguage);
            var originalText = ResolveActionMenuOriginalText(
                "Corrida\r\nNv. 20",
                new TranslationReuseScope("fr", "pt-BR", 0, true),
                lookups.OriginalLookup);
            var translatedText = ResolveActionMenuTranslatedText(
                "Sprint\r\nNiveau 20",
                new TranslationReuseScope("fr", "pt-BR", 0, true),
                lookups.TranslatedLookup);

            Assert.Equal("Sprint\r\nNiveau 20", originalText);
            Assert.Equal("Corrida\r\nNv. 20", translatedText);
        }
        finally
        {
            GameWindowCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures legacy full-payload ActionMenu rows still contribute only
    ///     residual window chrome once canonical action storage already owns
    ///     the action label pair.
    /// </summary>
    [Fact]
    public void ActionMenuPersistedFallback_CanonicalActionLabelsStayOutOfLookupButWindowChromeRemains()
    {
        using var runtimeScope = new TestRuntimeScope();
        ActionTooltipCacheManager.Clear();
        GameWindowCacheManager.Clear();

        try
        {
            ActionTooltipCacheManager.Update(new ActionTooltip
            {
                Id = 9,
                ActionId = 16009,
                ActionName = "Sprint",
                OriginalLang = "en",
                TranslatedActionName = "Corrida",
                TranslationLang = "pt-BR",
                TranslationEngine = 0,
                GameVersion = "test-version",
                SourceContentHash = "hash-sprint-legacy-persisted-fallback",
            });

            var handler = new ActionMenuWindowHandler(
                new Config
                {
                    Lang = 28,
                    ChosenTransEngine = 0,
                },
                null!,
                null!);
            var persistedOriginalPayload = new DbFirstGameWindowPayload(
                new SortedDictionary<int, string>
                {
                    [17] = "Sprint",
                },
                new SortedDictionary<int, string>(),
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["3:100"] = "Support Desk",
                });
            var persistedTranslatedPayload = new DbFirstGameWindowPayload(
                new SortedDictionary<int, string>
                {
                    [17] = "Corrida",
                },
                new SortedDictionary<int, string>(),
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["3:100"] = "Central de Suporte",
                });

            GameWindowCacheManager.Update(new GameWindow(
                windowAddonName: "ActionMenu",
                originalWindowStrings: persistedOriginalPayload.Serialize(),
                originalWindowStringsLang: "en",
                translatedWindowStrings: persistedTranslatedPayload.Serialize(),
                translationLang: "pt-BR",
                translationEngine: 0,
                gameVersion: "test-version",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow));

            var lookups = GetActionMenuLookups(
                handler,
                new SourceClientLanguage("en", "en"));

            Assert.False(lookups.TranslatedLookup.ContainsKey("Sprint"));
            Assert.False(lookups.OriginalLookup.ContainsKey("Corrida"));
            Assert.Equal(
                "Central de Suporte",
                lookups.TranslatedLookup["Support Desk"]);
            Assert.Equal(
                "Support Desk",
                lookups.OriginalLookup["Central de Suporte"]);
        }
        finally
        {
            ActionTooltipCacheManager.Clear();
            GameWindowCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures the EventItem fallback flow does not publish a translated
    ///     payload from a different operation-captured source language.
    /// </summary>
    [Fact]
    public void EventItemFallback_DifferentSource_DoesNotResolvePayload()
    {
        using var runtimeScope = new TestRuntimeScope();
        ReferenceTextCacheRegistry.EventItemTexts.Clear();

        try
        {
            const uint itemId = 2000001;
            var referencePayload = new ReferenceTextCanonicalPayload
            {
                ReferenceId = itemId,
                Name = "Aether Compass",
                Description = null,
                TranslatedName = "Bussola Eterea",
                TranslatedDescription = null,
            };
            ReferenceTextCacheRegistry.EventItemTexts.Update(new EventItemText
            {
                Id = 1,
                ReferenceId = itemId,
                OriginalLang = "en",
                OriginalName = referencePayload.Name,
                TranslatedName = referencePayload.TranslatedName,
                TranslationLang = "pt",
                TranslationEngine = 0,
                GameVersion = "test-version",
                SourceContentHash = referencePayload.ComputeSourceContentHash(),
                CanonicalPayloadAsText = referencePayload.Serialize(),
            });
            var matchingScope = new TranslationReuseScope(
                "en",
                "pt",
                0,
                true);
            var mismatchedScope = new TranslationReuseScope(
                "de",
                "pt",
                0,
                true);

            Assert.NotNull(
                ReferenceTextCacheRegistry.EventItemTexts.TryFindIdentityMatch(
                    itemId,
                    matchingScope,
                    "test-version"));
            Assert.Null(
                ReferenceTextCacheRegistry.EventItemTexts.TryFindIdentityMatch(
                    itemId,
                    mismatchedScope,
                    "test-version"));

            var runtime = (PluginEntry)RuntimeHelpers.GetUninitializedObject(
                typeof(PluginEntry));
            SetPrivateField(
                runtime,
                "configuration",
                new Config
                {
                    Lang = 28,
                    ChosenTransEngine = 0,
                });
            var originalPayload = new ItemTooltipCanonicalPayload
            {
                ItemId = itemId,
                Name = referencePayload.Name,
                Description = string.Empty,
            };

            var matchingFound = TryResolveEventItemFallback(
                runtime,
                new SourceClientLanguage("en", "en"),
                originalPayload,
                out var matchingPayload);
            var mismatchedFound = TryResolveEventItemFallback(
                runtime,
                new SourceClientLanguage("de", "de"),
                originalPayload,
                out var mismatchedPayload);

            Assert.True(matchingFound);
            Assert.Equal("Bussola Eterea", matchingPayload.TranslatedName);
            Assert.False(mismatchedFound);
            Assert.Null(mismatchedPayload.TranslatedName);
        }
        finally
        {
            ReferenceTextCacheRegistry.EventItemTexts.Clear();
        }
    }

    /// <summary>
    ///     Invokes the persisted ActionMenu lookup flow.
    /// </summary>
    /// <param name="handler">The ActionMenu handler.</param>
    /// <param name="sourceLanguage">The operation-captured source.</param>
    /// <returns>The persisted forward and reverse lookups.</returns>
    private static (
        Dictionary<string, string> OriginalLookup,
        Dictionary<string, string> TranslatedLookup) GetActionMenuLookups(
        ActionMenuWindowHandler handler,
        SourceClientLanguage sourceLanguage)
    {
        var method = typeof(ActionMenuWindowHandler).GetMethod(
            "BuildPersistedActionMenuLookups",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var arguments = method.GetParameters()[0].ParameterType ==
                        typeof(SourceClientLanguage)
            ? new object?[] { sourceLanguage, null, null, null, null, null }
            : [null, null, null, null];
        method.Invoke(handler, arguments);
        var outputOffset = arguments.Length == 6 ? 1 : 0;
        return (
            Assert.IsType<Dictionary<string, string>>(arguments[outputOffset]),
            Assert.IsType<Dictionary<string, string>>(arguments[outputOffset + 1]));
    }

    /// <summary>
    ///     Invokes the Character supplemental translated-payload flow.
    /// </summary>
    /// <param name="handler">The Character handler.</param>
    /// <param name="sourceLanguage">The operation-captured source.</param>
    /// <param name="originalPayload">The original-facing payload.</param>
    /// <param name="translatedPayload">The resolved translated payload.</param>
    /// <returns>True when a source-scoped canonical row resolves the payload.</returns>
    private static bool TryResolveCharacterTranslatedPayload(
        CharacterWindowHandler handler,
        SourceClientLanguage sourceLanguage,
        DbFirstGameWindowPayload originalPayload,
        out DbFirstGameWindowPayload translatedPayload)
    {
        var method = typeof(CharacterTextNodeWindowHandlerBase).GetMethod(
            "TryResolveSupplementalTranslatedPayload",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var arguments = new object?[]
        {
            sourceLanguage,
            originalPayload,
            null,
        };
        var found = Assert.IsType<bool>(method.Invoke(handler, arguments));
        translatedPayload = Assert.IsType<DbFirstGameWindowPayload>(
            arguments[2]);
        return found;
    }

    /// <summary>
    ///     Invokes the ActionMenu original-text resolver with a source-scoped
    ///     persisted reverse lookup.
    /// </summary>
    /// <param name="visibleText">The translated visible text.</param>
    /// <param name="scope">The complete translation reuse scope.</param>
    /// <param name="originalLookup">The persisted translated-to-original lookup.</param>
    /// <returns>The resolved original text.</returns>
    private static string ResolveActionMenuOriginalText(
        string visibleText,
        TranslationReuseScope scope,
        IReadOnlyDictionary<string, string> originalLookup)
    {
        var method = typeof(ActionMenuWindowHandler).GetMethod(
            "ResolveOriginalText",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        return Assert.IsType<string>(method.Invoke(
            null,
            [visibleText, scope, "test-version", originalLookup]));
    }

    /// <summary>
    ///     Invokes the ActionMenu translated-text resolver with a source-scoped
    ///     persisted forward lookup.
    /// </summary>
    /// <param name="originalText">The original live text.</param>
    /// <param name="scope">The complete translation reuse scope.</param>
    /// <param name="translatedLookup">The persisted original-to-translated lookup.</param>
    /// <returns>The resolved translated text.</returns>
    private static string ResolveActionMenuTranslatedText(
        string originalText,
        TranslationReuseScope scope,
        IReadOnlyDictionary<string, string> translatedLookup)
    {
        var method = typeof(ActionMenuWindowHandler).GetMethod(
            "ResolveTranslatedText",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        return Assert.IsType<string>(method.Invoke(
            null,
            [originalText, scope, "test-version", translatedLookup]));
    }

    /// <summary>
    ///     Invokes ActionMenu normalization with an untranslated payload so
    ///     persisted fallback reuse is directly observable.
    /// </summary>
    /// <param name="handler">The ActionMenu handler.</param>
    /// <param name="sourceLanguage">The operation-captured source.</param>
    /// <param name="originalPayload">The original payload.</param>
    /// <returns>The normalized translated payload.</returns>
    private static DbFirstGameWindowPayload NormalizeActionMenuPayload(
        ActionMenuWindowHandler handler,
        SourceClientLanguage sourceLanguage,
        DbFirstGameWindowPayload originalPayload)
    {
        var method = typeof(ActionMenuWindowHandler)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate =>
                candidate.Name == "NormalizeResolvedTranslatedPayload" &&
                candidate.GetParameters().Length >= 4);
        var arguments = method.GetParameters()[0].ParameterType ==
                        typeof(SourceClientLanguage)
            ? new object?[]
            {
                sourceLanguage,
                originalPayload,
                originalPayload,
                null,
                null,
                null,
            }
            : [originalPayload, originalPayload, null, null];

        return Assert.IsType<DbFirstGameWindowPayload>(
            method.Invoke(handler, arguments));
    }

    /// <summary>
    ///     Invokes the ActionMenu persistence diagnostics with the captured
    ///     source when the production hook supports it.
    /// </summary>
    /// <param name="handler">The ActionMenu handler.</param>
    /// <param name="sourceLanguage">The operation-captured source.</param>
    /// <returns>The persisted candidate diagnostics.</returns>
    private static (int CandidateCount, int StableMatchCount)
        GetActionMenuCandidateDiagnostics(
            ActionMenuWindowHandler handler,
            SourceClientLanguage sourceLanguage)
    {
        var method = typeof(ActionMenuWindowHandler).GetMethod(
            "GetPersistedCandidateDiagnostics",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var arguments = method.GetParameters()[0].ParameterType ==
                        typeof(SourceClientLanguage)
            ? new object?[] { sourceLanguage, string.Empty, null, null }
            : [string.Empty, null];
        var result = method.Invoke(handler, arguments);
        Assert.NotNull(result);
        var resultType = result.GetType();
        var candidateCount = resultType.GetField("Item1")?.GetValue(result);
        var stableMatchCount = resultType.GetField("Item2")?.GetValue(result);

        return (
            Assert.IsType<int>(candidateCount),
            Assert.IsType<int>(stableMatchCount));
    }

    /// <summary>
    ///     Invokes the existing EventItem fallback flow.
    /// </summary>
    /// <param name="runtime">The native-free plugin runtime.</param>
    /// <param name="sourceLanguage">The operation-captured source.</param>
    /// <param name="originalPayload">The original item payload.</param>
    /// <param name="translatedPayload">The translated payload, if any.</param>
    /// <returns>True when the fallback resolved a complete payload.</returns>
    private static bool TryResolveEventItemFallback(
        PluginEntry runtime,
        SourceClientLanguage sourceLanguage,
        ItemTooltipCanonicalPayload originalPayload,
        out ItemTooltipCanonicalPayload translatedPayload)
    {
        var method = typeof(PluginEntry).GetMethod(
            "TryFindTranslatedItemTooltipPayload",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var sourceKindType = typeof(PluginEntry).GetNestedType(
            "StructuredTooltipItemSourceKind",
            BindingFlags.NonPublic);
        Assert.NotNull(method);
        Assert.NotNull(sourceKindType);

        var sourceKind = Enum.Parse(sourceKindType, "EventItem");
        var arguments = new object?[]
        {
            sourceLanguage,
            sourceKind,
            originalPayload,
            new ItemTooltipCanonicalPayload(),
        };
        var found = Assert.IsType<bool>(method.Invoke(runtime, arguments));
        translatedPayload = Assert.IsType<ItemTooltipCanonicalPayload>(
            arguments[3]);
        return found;
    }

    /// <summary>
    ///     Sets one private field on a native-free plugin instance.
    /// </summary>
    /// <param name="runtime">The plugin runtime.</param>
    /// <param name="fieldName">The private field name.</param>
    /// <param name="value">The field value.</param>
    private static void SetPrivateField(
        PluginEntry runtime,
        string fieldName,
        object value)
    {
        var field = typeof(PluginEntry).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(runtime, value);
    }

    /// <summary>
    ///     Deletes a temporary test directory when possible.
    /// </summary>
    /// <param name="path">The directory to delete.</param>
    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    ///     Provides deterministic target-language and game-version state for
    ///     native-free fallback tests.
    /// </summary>
    private sealed class TestRuntimeScope : IDisposable
    {
        private readonly IDataManager originalDataManager = PluginEntry.DManager;
        private readonly int originalLanguageInt = PluginEntry.LanguageInt;
        private readonly Dictionary<int, LanguageInfo> originalLanguages =
            PluginEntry.LangDict;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TestRuntimeScope" />
        ///     class.
        /// </summary>
        public TestRuntimeScope()
        {
            PluginEntry.DManager = CreateDataManager();
            PluginEntry.LanguageInt = 28;
            PluginEntry.LangDict = new Dictionary<int, LanguageInfo>
            {
                [28] = new LanguageInfo(
                    "pt-BR",
                    "Portuguese",
                    string.Empty,
                    string.Empty,
                    []),
            };
        }

        /// <inheritdoc />
        public void Dispose()
        {
            PluginEntry.DManager = this.originalDataManager;
            PluginEntry.LanguageInt = this.originalLanguageInt;
            PluginEntry.LangDict = this.originalLanguages;
        }

        /// <summary>
        ///     Creates the minimal data-manager proxy required by the shared
        ///     game-version helper.
        /// </summary>
        /// <returns>The configured data-manager proxy.</returns>
        private static IDataManager CreateDataManager()
        {
            var gameDataProperty = typeof(IDataManager).GetProperty("GameData") ??
                                   throw new MissingMemberException(
                                       typeof(IDataManager).FullName,
                                       "GameData");
            var gameData = RuntimeHelpers.GetUninitializedObject(
                gameDataProperty.PropertyType);
            var repositoriesProperty = gameDataProperty.PropertyType.GetProperty(
                "Repositories") ??
                throw new MissingMemberException(
                    gameDataProperty.PropertyType.FullName,
                    "Repositories");
            var repositories = (IDictionary)(Activator.CreateInstance(
                repositoriesProperty.PropertyType) ??
                throw new InvalidOperationException(
                    "Could not create a Lumina repository dictionary."));
            var repositoryType = repositoriesProperty.PropertyType
                .GetGenericArguments()[1];
            var repository = RuntimeHelpers.GetUninitializedObject(repositoryType);
            SetMember(repository, "Version", "test-version");
            repositories.Add("ffxiv", repository);
            SetMember(gameData, "Repositories", repositories);

            var dataManager = DispatchProxy.Create<IDataManager, DataManagerProxy>();
            ((DataManagerProxy)(object)dataManager).GameData = gameData;
            return dataManager;
        }

        /// <summary>
        ///     Sets a property or compiler-generated backing field on an
        ///     uninitialized test object.
        /// </summary>
        /// <param name="instance">The object to update.</param>
        /// <param name="memberName">The member name.</param>
        /// <param name="value">The value to assign.</param>
        private static void SetMember(
            object instance,
            string memberName,
            object value)
        {
            var property = instance.GetType().GetProperty(
                memberName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            if (property?.SetMethod != null)
            {
                property.SetValue(instance, value);
                return;
            }

            var backingField = instance.GetType().GetField(
                $"<{memberName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic) ??
                throw new MissingMemberException(
                    instance.GetType().FullName,
                    memberName);
            backingField.SetValue(instance, value);
        }
    }

    /// <summary>
    ///     Supplies the game-data member required by the game-version helper.
    /// </summary>
    private class DataManagerProxy : DispatchProxy
    {
        /// <summary>Gets or sets the fake Lumina game-data instance.</summary>
        public object? GameData { get; set; }

        /// <inheritdoc />
        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            if (targetMethod?.Name == "get_GameData")
            {
                return this.GameData;
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
