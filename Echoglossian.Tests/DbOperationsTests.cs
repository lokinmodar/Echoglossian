// <copyright file="DbOperationsTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;
using System.Runtime.CompilerServices;

using Dalamud.Game;

using Xunit;
using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.EFCoreSqlite.Models.Journal;
using Echoglossian.LanguagesHandling;
using Echoglossian.Translators;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the DB-side guard that decides whether translated text is safe
///     to persist.
/// </summary>
public class DbOperationsTests
{
    /// <summary>
    ///     Ensures newly formatted rows persist the resolved source identity
    ///     rather than the client-language display name.
    /// </summary>
    [Fact]
    public void FormatToastMessage_PersistsResolvedSourceIdentity()
    {
        var originalClientState = PluginEntry.ClientStateInterface;

        try
        {
            PluginEntry.ClientStateInterface =
                TranslationReuseScopeTests.CreateClientState(ClientLanguage.English);
            var plugin = CreateFormattingPlugin();

            var row = plugin.FormatToastMessage("Area", "Test");

            Assert.NotNull(row);
            Assert.True(RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
                out var sourceLanguage));
            Assert.Equal(
                sourceLanguage.PersistenceCode,
                row.OriginalLang);
        }
        finally
        {
            PluginEntry.ClientStateInterface = originalClientState;
        }
    }

    /// <summary>
    ///     Ensures a hash-valid regional quest row is found when an older
    ///     provider-alias row exists for the same quest.
    /// </summary>
    [Fact]
    public void FindQuestPlate_RegionalTargetPrefersHashValidCurrentRow()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);
        var previousClientState = PluginEntry.ClientStateInterface;
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        var previousLanguages = PluginEntry.LangDict;

        try
        {
            PluginEntry.ClientStateInterface =
                TranslationReuseScopeTests.CreateClientState(ClientLanguage.English);
            PluginEntry.ConfigDirectory = configDir + Path.DirectorySeparatorChar;
            PluginEntry.LangDict = new Dictionary<int, LanguageInfo>
            {
                [81] = new LanguageInfo(
                    "pt",
                    "Portuguese",
                    string.Empty,
                    string.Empty,
                    []),
            };
            var plugin = CreateFormattingPlugin(
                new Config
                {
                    Lang = 81,
                    ChosenTransEngine = 0,
                    TranslateAlreadyTranslatedTexts = true,
                });
            var timestamp = new DateTime(
                2026,
                7,
                23,
                12,
                0,
                0,
                DateTimeKind.Utc);
            var legacyRow = CreateQuestPlate("en", 0, timestamp);
            legacyRow.QuestId = "69768";
            legacyRow.TranslationLang = "pt";
            legacyRow.SourceContentHash = null;
            var currentRow = CreateQuestPlate("en", 0, timestamp.AddMinutes(1));
            currentRow.QuestId = "69768";
            currentRow.TranslationLang = "pt-BR";
            currentRow.SourceContentHash = "known-content-hash";

            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
                context.QuestPlate.AddRange(legacyRow, currentRow);
                context.SaveChanges();
            }

            var lookup = new QuestPlate(
                "A Test of Resolve",
                "Speak with the attendant.",
                "en",
                string.Empty,
                string.Empty,
                "69768",
                "pt",
                0,
                timestamp,
                timestamp,
                "test-version")
            {
                SourceContentHash = "known-content-hash",
            };

            var result = plugin.FindQuestPlate(lookup);

            Assert.NotNull(result);
            Assert.Equal("pt-BR", result.TranslationLang);
            Assert.Equal("known-content-hash", result.SourceContentHash);
        }
        finally
        {
            PluginEntry.ClientStateInterface = previousClientState;
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            PluginEntry.LangDict = previousLanguages;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures duplicate canonical quest rows prefer the most complete
    ///     translated payload instead of an older partial row.
    /// </summary>
    [Fact]
    public void FindQuestPlate_DuplicateQuestIdRowsPreferMostCompleteCanonicalPayload()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);
        var previousClientState = PluginEntry.ClientStateInterface;
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        var previousLanguages = PluginEntry.LangDict;

        try
        {
            PluginEntry.ClientStateInterface =
                TranslationReuseScopeTests.CreateClientState(ClientLanguage.English);
            PluginEntry.ConfigDirectory = configDir + Path.DirectorySeparatorChar;
            PluginEntry.LangDict = CreateTargetLanguages();
            var plugin = CreateFormattingPlugin(new Config
            {
                Lang = 28,
                ChosenTransEngine = 0,
                TranslateAlreadyTranslatedTexts = true,
            });
            var timestamp = new DateTime(
                2026,
                7,
                25,
                12,
                0,
                0,
                DateTimeKind.Utc);
            const string questId = "69929";
            const string sourceContentHash = "e6746c90e9d04427";
            const string questTextSheetName = "quest/042/AktKzd008_04232";

            var staleRow = CreateQuestPlate("en", 0, timestamp);
            staleRow.QuestId = questId;
            staleRow.SourceContentHash = sourceContentHash;
            staleRow.QuestTextSheetName = null;
            staleRow.TranslatedQuestMessage = string.Empty;
            staleRow.TranslatedObjectives.Clear();
            staleRow.TranslatedObjectiveRowsByKey.Clear();
            staleRow.TranslatedSummaries.Clear();
            staleRow.TranslatedSummaryRowsByKey.Clear();
            staleRow.TranslatedSystemRows.Clear();
            staleRow.TranslatedSystemRowsByKey.Clear();
            staleRow.UpdateFieldsAsText();

            var canonicalRow = CreateQuestPlate("en", 0, timestamp.AddMinutes(1));
            canonicalRow.QuestId = questId;
            canonicalRow.SourceContentHash = sourceContentHash;
            canonicalRow.QuestTextSheetName = questTextSheetName;
            canonicalRow.TranslatedQuestMessage = "Lucia busca entender melhor a crise atual.";
            canonicalRow.ObjectiveRowsByKey["TODO#0"] =
                "Investigate the designated locations.";
            canonicalRow.SetTranslatedObjectiveText(
                "TODO#0",
                "Investigate the designated locations.",
                "Investigue os locais designados.");
            canonicalRow.SummaryRowsByKey["SEQ#0"] =
                "The immediate crisis has been contained.";
            canonicalRow.SetTranslatedSummaryText(
                "SEQ#0",
                "The immediate crisis has been contained.",
                "A crise imediata foi contida.");
            canonicalRow.SystemRowsByKey["SYSTEM#0"] =
                "Attention: proceed to the next area.";
            canonicalRow.SetTranslatedSystemText(
                "SYSTEM#0",
                "Attention: proceed to the next area.",
                "Atenção: siga para a próxima área.");
            canonicalRow.UpdateFieldsAsText();

            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
                context.QuestPlate.AddRange(staleRow, canonicalRow);
                context.SaveChanges();
            }

            var lookup = new QuestPlate(
                canonicalRow.QuestName,
                canonicalRow.OriginalQuestMessage,
                canonicalRow.OriginalLang,
                string.Empty,
                string.Empty,
                questId,
                canonicalRow.TranslationLang,
                canonicalRow.TranslationEngine,
                timestamp,
                timestamp,
                canonicalRow.GameVersion)
            {
                QuestTextSheetName = questTextSheetName,
                SourceContentHash = sourceContentHash,
            };

            var result = plugin.FindQuestPlate(lookup);

            Assert.NotNull(result);
            Assert.Equal(
                canonicalRow.TranslatedQuestMessage,
                result!.TranslatedQuestMessage);
            Assert.Equal(
                canonicalRow.TranslatedObjectiveRowsByKey["TODO#0"],
                result.TranslatedObjectiveRowsByKey["TODO#0"]);
            Assert.Equal(
                canonicalRow.TranslatedSummaryRowsByKey["SEQ#0"],
                result.TranslatedSummaryRowsByKey["SEQ#0"]);
            Assert.Equal(
                canonicalRow.TranslatedSystemRowsByKey["SYSTEM#0"],
                result.TranslatedSystemRowsByKey["SYSTEM#0"]);
            Assert.Equal(questTextSheetName, result.QuestTextSheetName);
        }
        finally
        {
            PluginEntry.ClientStateInterface = previousClientState;
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            PluginEntry.LangDict = previousLanguages;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures canonical quest writes promote legacy rows without
    ///     <c>QuestId</c> instead of creating a duplicate canonical row.
    /// </summary>
    [Fact]
    public void InsertQuestPlate_QuestIdPromotesLegacyNameRowInsteadOfCreatingDuplicate()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);
        var previousClientState = PluginEntry.ClientStateInterface;
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        var previousLanguages = PluginEntry.LangDict;

        try
        {
            PluginEntry.ClientStateInterface =
                TranslationReuseScopeTests.CreateClientState(ClientLanguage.English);
            PluginEntry.ConfigDirectory = configDir + Path.DirectorySeparatorChar;
            PluginEntry.LangDict = CreateTargetLanguages();
            var plugin = CreateFormattingPlugin(new Config
            {
                Lang = 28,
                ChosenTransEngine = 0,
                TranslateAlreadyTranslatedTexts = true,
            });
            var timestamp = new DateTime(
                2026,
                7,
                25,
                13,
                0,
                0,
                DateTimeKind.Utc);
            const string questId = "69768";
            const string sourceContentHash = "afa17a0939b31cd1";
            const string questTextSheetName = "quest/042/AktKzd008_04232";

            var legacyRow = CreateQuestPlate("en", 0, timestamp);
            legacyRow.QuestId = null;
            legacyRow.SourceContentHash = null;
            legacyRow.QuestTextSheetName = null;
            legacyRow.UpdateFieldsAsText();

            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
                context.QuestPlate.Add(legacyRow);
                context.SaveChanges();
            }

            var canonicalRow = CreateQuestPlate("en", 0, timestamp.AddMinutes(1));
            canonicalRow.QuestId = questId;
            canonicalRow.SourceContentHash = sourceContentHash;
            canonicalRow.QuestTextSheetName = questTextSheetName;
            canonicalRow.ObjectiveRowsByKey["TODO#0"] = "Speak with the designated target.";
            canonicalRow.SetTranslatedObjectiveText(
                "TODO#0",
                "Speak with the designated target.",
                "Fale com o alvo designado.");
            canonicalRow.SummaryRowsByKey["SEQ#0"] = "The search has begun.";
            canonicalRow.SetTranslatedSummaryText(
                "SEQ#0",
                "The search has begun.",
                "A busca começou.");
            canonicalRow.UpdateFieldsAsText();

            plugin.InsertQuestPlate(canonicalRow);

            using var verification = new EchoglossianDbContext(configDir);
            var questRows = verification.QuestPlate.ToList();
            var persistedRow = Assert.Single(questRows);
            persistedRow.UpdateFieldsFromText();

            Assert.Equal(questId, persistedRow.QuestId);
            Assert.Equal(sourceContentHash, persistedRow.SourceContentHash);
            Assert.Equal(questTextSheetName, persistedRow.QuestTextSheetName);
            Assert.Equal(
                canonicalRow.TranslatedQuestName,
                persistedRow.TranslatedQuestName);
            Assert.Equal(
                canonicalRow.TranslatedObjectiveRowsByKey["TODO#0"],
                persistedRow.TranslatedObjectiveRowsByKey["TODO#0"]);
            Assert.Equal(
                canonicalRow.TranslatedSummaryRowsByKey["SEQ#0"],
                persistedRow.TranslatedSummaryRowsByKey["SEQ#0"]);
        }
        finally
        {
            PluginEntry.ClientStateInterface = previousClientState;
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            PluginEntry.LangDict = previousLanguages;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures canonical quest saves merge into legacy regional-alias rows
    ///     instead of creating a duplicate <c>QuestPlate</c> entry.
    /// </summary>
    [Fact]
    public void InsertQuestPlate_RegionalTargetAliasPromotesLegacyQuestRow()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);
        var previousClientState = PluginEntry.ClientStateInterface;
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        var previousLanguages = PluginEntry.LangDict;

        try
        {
            PluginEntry.ClientStateInterface =
                TranslationReuseScopeTests.CreateClientState(ClientLanguage.English);
            PluginEntry.ConfigDirectory = configDir + Path.DirectorySeparatorChar;
            PluginEntry.LangDict = CreateTargetLanguages();
            var plugin = CreateFormattingPlugin(new Config
            {
                Lang = 28,
                ChosenTransEngine = 0,
                TranslateAlreadyTranslatedTexts = true,
            });
            var timestamp = new DateTime(
                2026,
                7,
                25,
                14,
                0,
                0,
                DateTimeKind.Utc);
            const string questId = "70391";
            const string sourceContentHash = "70391-content-hash";
            const string questTextSheetName = "quest/050/ManFst001_05010";

            var legacyAliasRow = CreateQuestPlate("en", 0, timestamp);
            legacyAliasRow.QuestId = questId;
            legacyAliasRow.TranslationLang = "pt";
            legacyAliasRow.SourceContentHash = sourceContentHash;
            legacyAliasRow.QuestTextSheetName = questTextSheetName;
            legacyAliasRow.TranslatedQuestName = "Mente sobre Mansão";
            legacyAliasRow.TranslatedQuestMessage = "A isca de Ogul foi roubada.";
            legacyAliasRow.UpdateFieldsAsText();

            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
                context.QuestPlate.Add(legacyAliasRow);
                context.SaveChanges();
            }

            var canonicalRow = CreateQuestPlate("en", 0, timestamp.AddMinutes(1));
            canonicalRow.QuestId = questId;
            canonicalRow.TranslationLang = "pt-BR";
            canonicalRow.SourceContentHash = sourceContentHash;
            canonicalRow.QuestTextSheetName = questTextSheetName;
            canonicalRow.TranslatedQuestName = "Mente Sobre Mansão";
            canonicalRow.TranslatedQuestMessage =
                "O batedor de carteiras aparece e toma a isca de Ogul.";
            canonicalRow.ObjectiveRowsByKey["TODO#0"] =
                "Wait at the designated location, then follow the boy thief without being seen.";
            canonicalRow.SetTranslatedObjectiveText(
                "TODO#0",
                canonicalRow.ObjectiveRowsByKey["TODO#0"],
                "Espere no local designado e siga o menino ladrão sem ser visto.");
            canonicalRow.UpdateFieldsAsText();

            var result = plugin.InsertQuestPlate(canonicalRow);

            Assert.Equal("Data merged into QuestPlate table.", result);

            using var verification = new EchoglossianDbContext(configDir);
            var questRows = verification.QuestPlate.ToList();
            var persistedRow = Assert.Single(questRows);
            persistedRow.UpdateFieldsFromText();

            Assert.Equal(questId, persistedRow.QuestId);
            Assert.Equal("pt-BR", persistedRow.TranslationLang);
            Assert.Equal(sourceContentHash, persistedRow.SourceContentHash);
            Assert.Equal(questTextSheetName, persistedRow.QuestTextSheetName);
            Assert.Equal(
                canonicalRow.TranslatedQuestName,
                persistedRow.TranslatedQuestName);
            Assert.Equal(
                canonicalRow.TranslatedQuestMessage,
                persistedRow.TranslatedQuestMessage);
            Assert.Equal(
                canonicalRow.TranslatedObjectiveRowsByKey["TODO#0"],
                persistedRow.TranslatedObjectiveRowsByKey["TODO#0"]);
        }
        finally
        {
            PluginEntry.ClientStateInterface = previousClientState;
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            PluginEntry.LangDict = previousLanguages;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures formatting fails closed when the current client language
    ///     has no resolved source identity.
    /// </summary>
    [Fact]
    public void FormatToastMessage_UnresolvedSourceLanguage_ReturnsNull()
    {
        var originalClientState = PluginEntry.ClientStateInterface;

        try
        {
            PluginEntry.ClientStateInterface =
                TranslationReuseScopeTests.CreateClientState((ClientLanguage)99);
            var plugin = CreateFormattingPlugin();

            var row = plugin.FormatToastMessage("Area", "Test");

            Assert.Null(row);
        }
        finally
        {
            PluginEntry.ClientStateInterface = originalClientState;
        }
    }

    /// <summary>
    ///     Ensures popup-table reuse works for quest popups that do not have a
    ///     safe canonical quest identity yet.
    /// </summary>
    [Fact]
    public void FindQuestPopupText_AllowsPopupReuseWithoutQuestPlateIdentity()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);
        var previousClientState = PluginEntry.ClientStateInterface;
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        var previousLanguages = PluginEntry.LangDict;

        try
        {
            PluginEntry.ClientStateInterface =
                TranslationReuseScopeTests.CreateClientState(ClientLanguage.English);
            PluginEntry.ConfigDirectory = configDir + Path.DirectorySeparatorChar;
            PluginEntry.LangDict = CreateTargetLanguages();
            var plugin = CreateFormattingPlugin(
                new Config
                {
                    Lang = 28,
                    ChosenTransEngine = 0,
                    TranslateAlreadyTranslatedTexts = true,
                });
            var questPopupType = ResolveQuestPopupTextType();
            var timestamp = new DateTime(
                2026,
                7,
                29,
                12,
                0,
                0,
                DateTimeKind.Utc);

            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
                context.Add(CreateQuestPopupText(
                    questPopupType,
                    "JournalAccept",
                    null,
                    "The Yedlihmad Hunt",
                    "Accept the hunt.",
                    "en",
                    "A Caçada de Yedlihmad",
                    "Aceite a caçada.",
                    "pt-BR",
                    0,
                    "test-version",
                    "popup-hash",
                    timestamp));
                context.SaveChanges();
            }

            var lookup = CreateQuestPopupText(
                questPopupType,
                "JournalAccept",
                null,
                "The Yedlihmad Hunt",
                "Accept the hunt.",
                "en",
                string.Empty,
                string.Empty,
                "pt-BR",
                0,
                "test-version",
                "popup-hash",
                timestamp);
            var findMethod = typeof(PluginEntry).GetMethod(
                "FindQuestPopupText",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                [questPopupType],
                modifiers: null);

            Assert.NotNull(findMethod);
            var result = findMethod!.Invoke(plugin, [lookup]);

            Assert.NotNull(result);
            Assert.Equal(
                "A Caçada de Yedlihmad",
                GetObjectProperty<string>(result!, "TranslatedTitle"));
            Assert.Equal(
                "Aceite a caçada.",
                GetObjectProperty<string>(result!, "TranslatedBody"));
        }
        finally
        {
            PluginEntry.ClientStateInterface = previousClientState;
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            PluginEntry.LangDict = previousLanguages;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures generic selection-dialog reuse works for `SelectOk`
    ///     payloads that should not route through the cutscene-specific
    ///     select-string table.
    /// </summary>
    [Fact]
    public void FindSelectionDialogText_ReusesGenericSelectOkRow()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);
        var previousClientState = PluginEntry.ClientStateInterface;
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        var previousLanguages = PluginEntry.LangDict;

        try
        {
            PluginEntry.ClientStateInterface =
                TranslationReuseScopeTests.CreateClientState(ClientLanguage.English);
            PluginEntry.ConfigDirectory = configDir + Path.DirectorySeparatorChar;
            PluginEntry.LangDict = CreateTargetLanguages();
            var plugin = CreateFormattingPlugin(
                new Config
                {
                    Lang = 28,
                    ChosenTransEngine = 0,
                    TranslateAlreadyTranslatedTexts = true,
                });
            var selectionDialogType = ResolveSelectionDialogTextType();
            var timestamp = new DateTime(
                2026,
                7,
                29,
                14,
                30,
                0,
                DateTimeKind.Utc);
            var originalTexts = JsonConvert.SerializeObject(
                new[] { "Duty registration complete.", "OK" });
            var translatedTexts = JsonConvert.SerializeObject(
                new[] { "Registro de conteudo concluido.", "OK" });

            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
                context.Add(CreateSelectionDialogText(
                    selectionDialogType,
                    "SelectOk",
                    originalTexts,
                    "en",
                    translatedTexts,
                    "pt-BR",
                    0,
                    "test-version",
                    timestamp));
                context.SaveChanges();
            }

            var lookup = CreateSelectionDialogText(
                selectionDialogType,
                "SelectOk",
                originalTexts,
                "en",
                string.Empty,
                "pt-BR",
                0,
                "test-version",
                timestamp);
            var findMethod = typeof(PluginEntry).GetMethod(
                "FindSelectionDialogText",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                [selectionDialogType],
                modifiers: null);

            Assert.NotNull(findMethod);
            var result = findMethod!.Invoke(plugin, [lookup]);

            Assert.NotNull(result);
            Assert.Equal(
                translatedTexts,
                GetObjectProperty<string>(result!, "TranslatedTextsAsText"));
        }
        finally
        {
            PluginEntry.ClientStateInterface = previousClientState;
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            PluginEntry.LangDict = previousLanguages;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures translation-failure cache preload keeps EF filtering SQL-safe
    ///     and only loads failure reasons that are meant to persist.
    /// </summary>
    [Fact]
    public void TranslationFailureCachePreload_LoadsOnlyPersistentFailures()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);

        try
        {
            TranslationFailureCacheManager.Clear();

            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
                context.Set<TranslationFailure>().AddRange(
                    new TranslationFailure
                    {
                        SourceText = "Persistent failure sample",
                        SourceTextHash =
                            TranslationFailureKey.ComputeSourceTextHash(
                                "Persistent failure sample"),
                        SourceLanguage = "en",
                        TargetLanguage = "pt-BR",
                        TranslationEngine = 3,
                        FailureReason = "request-failed",
                    },
                    new TranslationFailure
                    {
                        SourceText = "Transient failure sample",
                        SourceTextHash =
                            TranslationFailureKey.ComputeSourceTextHash(
                                "Transient failure sample"),
                        SourceLanguage = "en",
                        TargetLanguage = "pt-BR",
                        TranslationEngine = 3,
                        FailureReason = "empty-result",
                    });
                context.SaveChanges();
            }

            TranslationFailureCacheManager.Preload(configDir);

            Assert.True(
                TranslationFailureCacheManager.Contains(
                    "Persistent failure sample",
                    "en",
                    "pt-BR",
                    3));
            Assert.False(
                TranslationFailureCacheManager.Contains(
                    "Transient failure sample",
                    "en",
                    "pt-BR",
                    3));
        }
        finally
        {
            TranslationFailureCacheManager.Clear();
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures synthetic translation-error placeholders are never treated
    ///     as valid rows to persist.
    /// </summary>
    [Fact]
    public void ShouldSaveToDB_RejectsSyntheticTranslationError()
    {
        var shouldSave = PluginEntry.ShouldSaveToDB(
            "[Translation Error: LmStudio: No connection could be made]");

        Assert.False(shouldSave);
    }

    /// <summary>
    ///     Ensures ordinary translated content still remains persistable.
    /// </summary>
    [Fact]
    public void ShouldSaveToDB_AcceptsNormalTranslatedText()
    {
        var shouldSave = PluginEntry.ShouldSaveToDB("O trabalho me deixa exausto.");

        Assert.True(shouldSave);
    }

    /// <summary>
    ///     Ensures dialogue rows that merely echo the original source text
    ///     across different languages are never treated as reusable
    ///     translations.
    /// </summary>
    [Fact]
    public void IsUsableDialogueTranslation_RejectsOriginalEchoAcrossLanguages()
    {
        var isUsable = TranslationPersistenceGuard.IsUsableDialogueTranslation(
            "If you wish to assign a level 50 retainer a job...",
            "If you wish to assign a level 50 retainer a job...",
            "en",
            "pt-BR");

        Assert.False(isUsable);
    }

    /// <summary>
    ///     Ensures transient translation failures are not considered safe to
    ///     persist as exact known-failure rows.
    /// </summary>
    [Theory]
    [InlineData("empty-result")]
    [InlineData("synthetic-error-result")]
    public void IsPersistentFailureReason_RejectsTransientReasons(string reason)
    {
        var isPersistent = TranslationPersistenceGuard.IsPersistentFailureReason(reason);

        Assert.False(isPersistent);
    }

    /// <summary>
    ///     Ensures every legacy lookup skips a preferred semantic match stored
    ///     for a different client language before selecting a row.
    /// </summary>
    /// <param name="retrieval">The legacy retrieval path under test.</param>
    [Theory]
    [InlineData("Talk")]
    [InlineData("Toast")]
    [InlineData("ToastReturn")]
    [InlineData("ErrorToast")]
    [InlineData("BattleTalk")]
    [InlineData("QuestPlate")]
    [InlineData("QuestPlateByName")]
    [InlineData("TalkSubtitle")]
    [InlineData("MiniTalk")]
    [InlineData("TextGimmickHint")]
    [InlineData("SelectString")]
    public void LegacyRetrieval_DifferentStoredSourceLanguage_SkipsWrongRow(
        string retrieval)
    {
        var result = RunLegacyRetrieval(
            retrieval,
            translateAlreadyTranslatedTexts: false,
            preferredSourceLanguage: "en",
            preferredEngine: 4,
            eligibleSourceLanguage: "ja",
            eligibleEngine: 4);

        Assert.Equal("ja", result.SourceLanguage);
        Assert.Equal(4, result.TranslationEngine);
    }

    /// <summary>
    ///     Ensures every legacy lookup uses the effective engine carried by the
    ///     request before selecting a semantic match during explicit
    ///     retranslation.
    /// </summary>
    /// <param name="retrieval">The legacy retrieval path under test.</param>
    [Theory]
    [InlineData("Talk")]
    [InlineData("Toast")]
    [InlineData("ToastReturn")]
    [InlineData("ErrorToast")]
    [InlineData("BattleTalk")]
    [InlineData("QuestPlate")]
    [InlineData("QuestPlateByName")]
    [InlineData("TalkSubtitle")]
    [InlineData("MiniTalk")]
    [InlineData("TextGimmickHint")]
    [InlineData("SelectString")]
    public void LegacyRetrieval_RetranslationEnabled_UsesRequestedEffectiveEngine(
        string retrieval)
    {
        var result = RunLegacyRetrieval(
            retrieval,
            translateAlreadyTranslatedTexts: true,
            preferredSourceLanguage: "ja",
            preferredEngine: 4,
            eligibleSourceLanguage: "ja",
            eligibleEngine: 7);

        Assert.Equal("ja", result.SourceLanguage);
        Assert.Equal(7, result.TranslationEngine);
    }

    /// <summary>
    ///     Ensures asynchronous dialogue lookup selects the same persisted row
    ///     as the existing synchronous lookup.
    /// </summary>
    /// <param name="retrieval">The dialogue retrieval path to invoke.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("Talk")]
    [InlineData("BattleTalk")]
    public async Task DialogueLookupAsync_MatchesSynchronousLookupSelection(
        string retrieval)
    {
        var result = await this.RunDialogueLookupParityAsync(retrieval);

        Assert.Equal(("Japanese", 7, result.ExpectedUpdatedDate), result.Sync);
        Assert.Equal(result.Sync, result.Async);
    }

    /// <summary>
    ///     Ensures asynchronous dialogue lookup observes an already-cancelled
    ///     token before materializing database candidates.
    /// </summary>
    /// <param name="retrieval">The dialogue retrieval path to invoke.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("Talk")]
    [InlineData("BattleTalk")]
    public async Task DialogueLookupAsync_AlreadyCancelledToken_Cancels(
        string retrieval)
    {
        await this.AssertDialogueLookupCancellationAsync(retrieval);
    }

    /// <summary>
    ///     Runs synchronous and asynchronous dialogue lookups against the same
    ///     competing persisted candidates.
    /// </summary>
    /// <param name="retrieval">The dialogue retrieval path to invoke.</param>
    /// <returns>The synchronous and asynchronous lookup scopes.</returns>
    private async Task<(
        (string? SourceLanguage, int? TranslationEngine, DateTime? UpdatedDate) Sync,
        (string? SourceLanguage, int? TranslationEngine, DateTime? UpdatedDate) Async,
        DateTime ExpectedUpdatedDate)> RunDialogueLookupParityAsync(
        string retrieval)
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);
        var previousClientState = PluginEntry.ClientStateInterface;
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        var previousLanguages = PluginEntry.LangDict;
        var plugin = CreateFormattingPlugin(new Config
        {
            Lang = 28,
            ChosenTransEngine = 4,
            TranslateAlreadyTranslatedTexts = true,
        });
        var expectedUpdatedDate = new DateTime(
            2026,
            8,
            6,
            12,
            0,
            0,
            DateTimeKind.Utc);

        try
        {
            PluginEntry.ClientStateInterface =
                TranslationReuseScopeTests.CreateClientState(
                    ClientLanguage.Japanese);
            PluginEntry.ConfigDirectory =
                configDir + Path.DirectorySeparatorChar;
            PluginEntry.LangDict = CreateTargetLanguages();

            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
                SeedDialogueLookupRows(context, retrieval, expectedUpdatedDate);
                context.SaveChanges();
            }

            return retrieval switch
            {
                "Talk" => (
                    GetLookupScope(plugin.FindAndReturnTalkMessage(
                        CreateTalkMessage("ja", 7, expectedUpdatedDate))),
                    GetLookupScope(await plugin.FindAndReturnTalkMessageAsync(
                        CreateTalkMessage("ja", 7, expectedUpdatedDate),
                        CancellationToken.None).ConfigureAwait(false)),
                    expectedUpdatedDate),
                "BattleTalk" => (
                    GetLookupScope(plugin.FindAndReturnBattleTalkMessage(
                        CreateBattleTalkMessage("ja", 7, expectedUpdatedDate))),
                    GetLookupScope(await plugin.FindAndReturnBattleTalkMessageAsync(
                        CreateBattleTalkMessage("ja", 7, expectedUpdatedDate),
                        CancellationToken.None).ConfigureAwait(false)),
                    expectedUpdatedDate),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(retrieval),
                    retrieval,
                    "Unknown dialogue retrieval."),
            };
        }
        finally
        {
            PluginEntry.LangDict = previousLanguages;
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            PluginEntry.ClientStateInterface = previousClientState;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Verifies that one dialogue lookup honors a pre-cancelled token.
    /// </summary>
    /// <param name="retrieval">The dialogue retrieval path to invoke.</param>
    /// <returns>A task representing the cancellation assertion.</returns>
    private async Task AssertDialogueLookupCancellationAsync(string retrieval)
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);
        var previousClientState = PluginEntry.ClientStateInterface;
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        var previousLanguages = PluginEntry.LangDict;

        try
        {
            PluginEntry.ClientStateInterface =
                TranslationReuseScopeTests.CreateClientState(
                    ClientLanguage.Japanese);
            PluginEntry.ConfigDirectory =
                configDir + Path.DirectorySeparatorChar;
            PluginEntry.LangDict = CreateTargetLanguages();
            var plugin = CreateFormattingPlugin(new Config
            {
                Lang = 28,
                ChosenTransEngine = 7,
                TranslateAlreadyTranslatedTexts = true,
            });
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();
            switch (retrieval)
            {
                case "Talk":
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(
                        () => plugin.FindAndReturnTalkMessageAsync(
                            CreateTalkMessage("ja", 7, DateTime.UtcNow),
                            cancellationTokenSource.Token)).ConfigureAwait(false);
                    break;
                case "BattleTalk":
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(
                        () => plugin.FindAndReturnBattleTalkMessageAsync(
                            CreateBattleTalkMessage("ja", 7, DateTime.UtcNow),
                            cancellationTokenSource.Token)).ConfigureAwait(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(retrieval),
                        retrieval,
                        "Unknown dialogue retrieval.");
            }
        }
        finally
        {
            PluginEntry.LangDict = previousLanguages;
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            PluginEntry.ClientStateInterface = previousClientState;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Seeds dialogue rows that exercise source, engine, target, and
    ///     recency selection rules.
    /// </summary>
    /// <param name="context">The temporary database context.</param>
    /// <param name="retrieval">The dialogue retrieval path under test.</param>
    /// <param name="expectedUpdatedDate">The timestamp of the expected row.</param>
    private static void SeedDialogueLookupRows(
        EchoglossianDbContext context,
        string retrieval,
        DateTime expectedUpdatedDate)
    {
        switch (retrieval)
        {
            case "Talk":
                context.TalkMessage.AddRange(CreateDialogueLookupRows(
                    CreateTalkMessage,
                    expectedUpdatedDate));
                break;
            case "BattleTalk":
                context.BattleTalkMessage.AddRange(CreateDialogueLookupRows(
                    CreateBattleTalkMessage,
                    expectedUpdatedDate));
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(retrieval),
                    retrieval,
                    "Unknown dialogue retrieval.");
        }
    }

    /// <summary>
    ///     Creates competing dialogue rows with one newest compatible row.
    /// </summary>
    /// <typeparam name="T">The dialogue row type.</typeparam>
    /// <param name="create">The row factory.</param>
    /// <param name="expectedUpdatedDate">The timestamp of the expected row.</param>
    /// <returns>The competing persisted rows.</returns>
    private static T[] CreateDialogueLookupRows<T>(
        Func<string, int, DateTime, T> create,
        DateTime expectedUpdatedDate)
        where T : class
    {
        var wrongTarget = create("Japanese", 7, expectedUpdatedDate.AddMinutes(3));
        SetObjectProperty(wrongTarget, "TranslationLang", "Portuguese");

        return
        [
            create("en", 7, expectedUpdatedDate.AddMinutes(4)),
            create("Japanese", 4, expectedUpdatedDate.AddMinutes(2)),
            wrongTarget,
            create("Japanese", 7, expectedUpdatedDate.AddMinutes(-1)),
            create("Japanese", 7, expectedUpdatedDate),
        ];
    }

    /// <summary>
    ///     Ensures explicit Talk retranslation persistence updates the existing
    ///     row for the same source line and engine instead of growing duplicate
    ///     exact-engine history.
    /// </summary>
    [Fact]
    public async Task UpsertTalkDataAsync_RefreshesExistingExactEngineRow()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        PluginEntry.ConfigDirectory = configDir + Path.DirectorySeparatorChar;

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
                context.TalkMessage.Add(new TalkMessage(
                    senderName: "Krile",
                    originalTalkMessage: "The plan remains unchanged.",
                    originalTalkMessageLang: "en",
                    originalSenderNameLang: "en",
                    translatedSenderName: "Krile",
                    translatedTalkMessage: "O plano permanece inalterado.",
                    translationLang: "pt-BR",
                    translationEngine: 14,
                    rtlLangTranslationImageData: null,
                    createdDate: new DateTime(2026, 5, 12, 8, 0, 0, DateTimeKind.Local),
                    updatedDate: new DateTime(2026, 5, 12, 8, 0, 0, DateTimeKind.Local)));
                context.SaveChanges();
            }

            await PluginEntry.UpsertTalkDataAsync(new TalkMessage(
                senderName: "Krile",
                originalTalkMessage: "The plan remains unchanged.",
                originalTalkMessageLang: "en",
                originalSenderNameLang: "en",
                translatedSenderName: "Krile",
                translatedTalkMessage: "O plano continua o mesmo.",
                translationLang: "pt-BR",
                translationEngine: 14,
                rtlLangTranslationImageData: null,
                createdDate: new DateTime(2026, 5, 12, 9, 0, 0, DateTimeKind.Local),
                updatedDate: new DateTime(2026, 5, 12, 9, 0, 0, DateTimeKind.Local)));

            using (var context = new EchoglossianDbContext(configDir))
            {
                var rows = context.TalkMessage
                    .Where(t =>
                        t.SenderName == "Krile" &&
                        t.OriginalTalkMessage == "The plan remains unchanged." &&
                        t.TranslationLang == "pt-BR" &&
                        t.TranslationEngine == 14)
                    .ToList();

                Assert.Single(rows);
                Assert.Equal("O plano continua o mesmo.", rows[0].TranslatedTalkMessage);
            }
        }
        finally
        {
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures the dialogue lookup ordering prefers the most recently
    ///     refreshed Talk row when multiple historical rows exist for the same
    ///     source line.
    /// </summary>
    [Fact]
    public void OrderTalkMessageLookupQuery_PrefersNewestUpdatedRow()
    {
        var olderRow = new TalkMessage(
            senderName: "Zero",
            originalTalkMessage: "We move at dawn.",
            originalTalkMessageLang: "en",
            originalSenderNameLang: "en",
            translatedSenderName: "Zero",
            translatedTalkMessage: "Partimos ao amanhecer.",
            translationLang: "pt-BR",
            translationEngine: 14,
            rtlLangTranslationImageData: null,
            createdDate: new DateTime(2026, 5, 12, 8, 0, 0),
            updatedDate: new DateTime(2026, 5, 12, 8, 0, 0))
        {
            Id = 1,
        };
        var newerRow = new TalkMessage(
            senderName: "Zero",
            originalTalkMessage: "We move at dawn.",
            originalTalkMessageLang: "en",
            originalSenderNameLang: "en",
            translatedSenderName: "Zero",
            translatedTalkMessage: "Seguimos ao amanhecer.",
            translationLang: "pt-BR",
            translationEngine: 8,
            rtlLangTranslationImageData: null,
            createdDate: new DateTime(2026, 5, 12, 9, 0, 0),
            updatedDate: new DateTime(2026, 5, 12, 9, 0, 0))
        {
            Id = 2,
        };

        var preferredRow = PluginEntry.OrderTalkMessageLookupQuery(
                new[] { olderRow, newerRow }.AsQueryable())
            .First();

        Assert.Equal(2, preferredRow.Id);
        Assert.Equal("Seguimos ao amanhecer.", preferredRow.TranslatedTalkMessage);
    }

    /// <summary>
    ///     Ensures explicit BattleTalk retranslation persistence updates the
    ///     existing row for the same source line and engine instead of growing
    ///     duplicate exact-engine history.
    /// </summary>
    [Fact]
    public async Task UpsertBattleTalkDataAsync_RefreshesExistingExactEngineRow()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        PluginEntry.ConfigDirectory = configDir + Path.DirectorySeparatorChar;

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
                context.BattleTalkMessage.Add(new BattleTalkMessage(
                    senderName: "Alphinaud",
                    originalBattleTalkMessage: "Hold the line!",
                    originalBattleTalkMessageLang: "en",
                    originalSenderNameLang: "en",
                    translatedSenderName: "Alphinaud",
                    translatedBattleTalkMessage: "Segurem a linha!",
                    translationLang: "pt-BR",
                    translationEngine: 14,
                    rtlLangTranslationImageData: null,
                    createdDate: new DateTime(2026, 5, 12, 8, 0, 0, DateTimeKind.Local),
                    updatedDate: new DateTime(2026, 5, 12, 8, 0, 0, DateTimeKind.Local)));
                context.SaveChanges();
            }

            await PluginEntry.UpsertBattleTalkDataAsync(new BattleTalkMessage(
                senderName: "Alphinaud",
                originalBattleTalkMessage: "Hold the line!",
                originalBattleTalkMessageLang: "en",
                originalSenderNameLang: "en",
                translatedSenderName: "Alphinaud",
                translatedBattleTalkMessage: "Mantenham a formação!",
                translationLang: "pt-BR",
                translationEngine: 14,
                rtlLangTranslationImageData: null,
                createdDate: new DateTime(2026, 5, 12, 9, 0, 0, DateTimeKind.Local),
                updatedDate: new DateTime(2026, 5, 12, 9, 0, 0, DateTimeKind.Local)));

            using (var context = new EchoglossianDbContext(configDir))
            {
                var rows = context.BattleTalkMessage
                    .Where(t =>
                        t.SenderName == "Alphinaud" &&
                        t.OriginalBattleTalkMessage == "Hold the line!" &&
                        t.TranslationLang == "pt-BR" &&
                        t.TranslationEngine == 14)
                    .ToList();

                Assert.Single(rows);
                Assert.Equal("Mantenham a formação!", rows[0].TranslatedBattleTalkMessage);
            }
        }
        finally
        {
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures dialogue upserts do not overwrite an otherwise identical
    ///     row persisted for a different source client language.
    /// </summary>
    [Fact]
    public async Task DialogueUpserts_DifferentSource_PreserveSeparateRows()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        PluginEntry.ConfigDirectory = configDir + Path.DirectorySeparatorChar;

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
                context.TalkMessage.Add(CreateTalkMessage("en", 14, DateTime.Now));
                context.BattleTalkMessage.Add(
                    CreateBattleTalkMessage("en", 14, DateTime.Now));
                context.SaveChanges();
            }

            await PluginEntry.UpsertTalkDataAsync(
                CreateTalkMessage("de", 14, DateTime.Now));
            await PluginEntry.UpsertBattleTalkDataAsync(
                CreateBattleTalkMessage("de", 14, DateTime.Now));

            using var verification = new EchoglossianDbContext(configDir);
            Assert.Equal(
                new[] { "de", "en" },
                verification.TalkMessage
                    .Select(row => row.OriginalTalkMessageLang)
                    .OrderBy(source => source)
                    .ToArray());
            Assert.Equal(
                new[] { "de", "en" },
                verification.BattleTalkMessage
                    .Select(row => row.OriginalBattleTalkMessageLang)
                    .OrderBy(source => source)
                    .ToArray());
        }
        finally
        {
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures legacy toast and quest writes do not suppress or merge an
    ///     otherwise identical row from a different source client language.
    /// </summary>
    [Fact]
    public void ToastAndQuestWrites_DifferentSource_PreserveSeparateRows()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        PluginEntry.ConfigDirectory = configDir + Path.DirectorySeparatorChar;

        try
        {
            var plugin = CreateFormattingPlugin(new Config
            {
                Lang = 28,
                ChosenTransEngine = 14,
                TranslateAlreadyTranslatedTexts = true,
            });
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
                context.ToastMessage.Add(
                    CreateToastMessage("Error", "en", 14, DateTime.Now));
                context.ToastMessage.Add(
                    CreateToastMessage("NonError", "en", 14, DateTime.Now));
                context.QuestPlate.Add(CreateQuestPlate("en", 14, DateTime.Now));
                context.SaveChanges();
            }

            plugin.LoadAllErrorToasts();
            plugin.LoadAllOtherToasts();
            plugin.InsertErrorToastMessageData(
                CreateToastMessage("Error", "de", 14, DateTime.Now));
            plugin.InsertOtherToastMessageData(
                CreateToastMessage("NonError", "de", 14, DateTime.Now));
            plugin.InsertQuestPlate(CreateQuestPlate("de", 14, DateTime.Now));

            using var verification = new EchoglossianDbContext(configDir);
            foreach (var toastType in new[] { "Error", "NonError" })
            {
                Assert.Equal(
                    new[] { "de", "en" },
                    verification.ToastMessage
                        .Where(row => row.ToastType == toastType)
                        .Select(row => row.OriginalLang)
                        .OrderBy(source => source)
                        .ToArray());
            }

            Assert.Equal(
                new[] { "de", "en" },
                verification.QuestPlate
                    .Select(row => row.OriginalLang)
                    .OrderBy(source => source)
                    .ToArray());
        }
        finally
        {
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures legacy display-name source rows are recognized by canonical
    ///     Talk, BattleTalk, and Quest writes without creating duplicates.
    /// </summary>
    /// <param name="legacySource">The legacy display-name source identity.</param>
    /// <param name="canonicalSource">The matching canonical source identity.</param>
    [Theory]
    [InlineData("English", "en")]
    [InlineData("Deutsch", "de")]
    [InlineData("French", "fr")]
    [InlineData("Japanese", "ja")]
    public async Task LegacySourceWrites_MatchingCanonicalSourceUpdatesExistingRows(
        string legacySource,
        string canonicalSource)
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        PluginEntry.ConfigDirectory = configDir + Path.DirectorySeparatorChar;

        try
        {
            var plugin = CreateFormattingPlugin(new Config
            {
                Lang = 28,
                ChosenTransEngine = 14,
                TranslateAlreadyTranslatedTexts = true,
            });
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
                context.TalkMessage.Add(
                    CreateTalkMessage(legacySource, 14, DateTime.Now));
                context.BattleTalkMessage.Add(
                    CreateBattleTalkMessage(legacySource, 14, DateTime.Now));
                context.QuestPlate.Add(
                    CreateQuestPlate(legacySource, 14, DateTime.Now));
                context.SaveChanges();
            }

            await PluginEntry.UpsertTalkDataAsync(
                CreateTalkMessage(canonicalSource, 14, DateTime.Now));
            await PluginEntry.UpsertBattleTalkDataAsync(
                CreateBattleTalkMessage(canonicalSource, 14, DateTime.Now));
            plugin.InsertQuestPlate(
                CreateQuestPlate(canonicalSource, 14, DateTime.Now));

            using var verification = new EchoglossianDbContext(configDir);
            Assert.Single(verification.TalkMessage);
            Assert.Single(verification.BattleTalkMessage);
            Assert.Single(verification.QuestPlate);
        }
        finally
        {
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures write-side legacy normalization does not collapse distinct
    ///     extended or unknown source identities.
    /// </summary>
    [Fact]
    public async Task LegacySourceWrites_ExtendedSourcesRemainDistinct()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        PluginEntry.ConfigDirectory = configDir + Path.DirectorySeparatorChar;

        try
        {
            var plugin = CreateFormattingPlugin(new Config
            {
                Lang = 28,
                ChosenTransEngine = 14,
                TranslateAlreadyTranslatedTexts = true,
            });
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var sources = new[]
            {
                "chs",
                "cht",
                "tc",
                "pt",
                "pt-BR",
                "he",
                "iw",
                "unknown-source",
            };
            foreach (var source in sources)
            {
                await PluginEntry.UpsertTalkDataAsync(
                    CreateTalkMessage(source, 14, DateTime.Now));
                await PluginEntry.UpsertBattleTalkDataAsync(
                    CreateBattleTalkMessage(source, 14, DateTime.Now));
                plugin.InsertQuestPlate(CreateQuestPlate(source, 14, DateTime.Now));
            }

            using var verification = new EchoglossianDbContext(configDir);
            Assert.Equal(
                sources.OrderBy(source => source),
                verification.TalkMessage
                    .Select(row => row.OriginalTalkMessageLang)
                    .OrderBy(source => source));
            Assert.Equal(
                sources.OrderBy(source => source),
                verification.BattleTalkMessage
                    .Select(row => row.OriginalBattleTalkMessageLang)
                    .OrderBy(source => source));
            Assert.Equal(
                sources.OrderBy(source => source),
                verification.QuestPlate
                    .Select(row => row.OriginalLang)
                    .OrderBy(source => source));
        }
        finally
        {
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures quest writes retain separate exact-engine history even when
    ///     reads are configured to reuse translations from another engine.
    /// </summary>
    [Fact]
    public void QuestWrites_CompatibleReadPolicyPreservesExactEngineRows()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        PluginEntry.ConfigDirectory = configDir + Path.DirectorySeparatorChar;

        try
        {
            var plugin = CreateFormattingPlugin(new Config
            {
                Lang = 28,
                ChosenTransEngine = 0,
                TranslateAlreadyTranslatedTexts = false,
            });
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
                context.QuestPlate.Add(CreateQuestPlate("en", 7, DateTime.Now));
                context.SaveChanges();
            }

            plugin.InsertQuestPlate(CreateQuestPlate("en", 0, DateTime.Now));

            using var verification = new EchoglossianDbContext(configDir);
            Assert.Equal(
                new int?[] { 0, 7 },
                verification.QuestPlate
                    .OrderBy(row => row.TranslationEngine)
                    .Select(row => row.TranslationEngine)
                    .ToArray());
        }
        finally
        {
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures the payload-insensitive, active-configuration GameWindow
    ///     lookup bypass is not exposed as a persistence API.
    /// </summary>
    [Fact]
    public void LegacyGameWindowLookup_IsRemoved()
    {
        var method = typeof(PluginEntry).GetMethod(
            "FindAndReturnGameWindow",
            BindingFlags.Public | BindingFlags.Static);

        Assert.Null(method);
    }

    /// <summary>
    ///     Runs one legacy retrieval against a deterministically staged pair of
    ///     competing persisted rows.
    /// </summary>
    /// <param name="retrieval">The retrieval path to invoke.</param>
    /// <param name="translateAlreadyTranslatedTexts">
    ///     Whether the active scope requires the configured engine.
    /// </param>
    /// <param name="preferredSourceLanguage">
    ///     The source identity on the row preferred by legacy selection.
    /// </param>
    /// <param name="preferredEngine">
    ///     The engine on the row preferred by legacy selection.
    /// </param>
    /// <param name="eligibleSourceLanguage">
    ///     The source identity on the row eligible for the active scope.
    /// </param>
    /// <param name="eligibleEngine">
    ///     The engine on the row eligible for the active scope.
    /// </param>
    /// <returns>The source identity and engine on the selected row.</returns>
    private static (string? SourceLanguage, int? TranslationEngine)
        RunLegacyRetrieval(
            string retrieval,
            bool translateAlreadyTranslatedTexts,
            string preferredSourceLanguage,
            int preferredEngine,
            string eligibleSourceLanguage,
            int eligibleEngine)
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);

        var previousClientState = PluginEntry.ClientStateInterface;
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        var previousDataManager = PluginEntry.DManager;
        var previousLanguages = PluginEntry.LangDict;
        var activeInstanceField = typeof(PluginEntry).GetField(
            "activeInstance",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(activeInstanceField);
        var previousActiveInstance = activeInstanceField.GetValue(null);

        try
        {
            var config = new Config
            {
                Lang = 28,
                ChosenTransEngine = 4,
                TranslateAlreadyTranslatedTexts =
                    translateAlreadyTranslatedTexts,
            };
            var plugin = CreateFormattingPlugin(config);

            PluginEntry.ClientStateInterface =
                TranslationReuseScopeTests.CreateClientState(
                    ClientLanguage.Japanese);
            PluginEntry.ConfigDirectory =
                configDir + Path.DirectorySeparatorChar;
            PluginEntry.DManager = null!;
            PluginEntry.LangDict = CreateTargetLanguages();
            activeInstanceField.SetValue(null, plugin);

            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
                var newer = new DateTime(
                    2026,
                    7,
                    13,
                    12,
                    0,
                    0,
                    DateTimeKind.Local);
                var older = newer.AddMinutes(-1);

                SeedLegacyRetrievalRow(
                    context,
                    retrieval,
                    preferredSourceLanguage,
                    preferredEngine,
                    newer);
                context.SaveChanges();

                var rejectedResult = InvokeLegacyRetrieval(plugin, retrieval);
                Assert.Null(rejectedResult.SourceLanguage);
                Assert.Null(rejectedResult.TranslationEngine);

                SeedLegacyRetrievalRow(
                    context,
                    retrieval,
                    eligibleSourceLanguage,
                    eligibleEngine,
                    older);
                context.SaveChanges();
            }

            ReloadLegacyToastCache(plugin, retrieval);
            return InvokeLegacyRetrieval(plugin, retrieval);
        }
        finally
        {
            activeInstanceField.SetValue(null, previousActiveInstance);
            PluginEntry.DManager = previousDataManager;
            PluginEntry.LangDict = previousLanguages;
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            PluginEntry.ClientStateInterface = previousClientState;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Seeds one row with the supplied legacy semantic identity.
    /// </summary>
    /// <param name="context">The temporary database context.</param>
    /// <param name="retrieval">The retrieval path under test.</param>
    /// <param name="sourceLanguage">The row's source identity.</param>
    /// <param name="engine">The row's engine.</param>
    /// <param name="updatedDate">The row update timestamp.</param>
    private static void SeedLegacyRetrievalRow(
        EchoglossianDbContext context,
        string retrieval,
        string sourceLanguage,
        int engine,
        DateTime updatedDate)
    {
        switch (retrieval)
        {
            case "Talk":
                context.TalkMessage.Add(
                    CreateTalkMessage(sourceLanguage, engine, updatedDate));
                break;
            case "Toast":
            case "ToastReturn":
                context.ToastMessage.Add(
                    CreateToastMessage(
                        "NonError",
                        sourceLanguage,
                        engine,
                        updatedDate));
                break;
            case "ErrorToast":
                context.ToastMessage.Add(
                    CreateToastMessage(
                        "Error",
                        sourceLanguage,
                        engine,
                        updatedDate));
                break;
            case "BattleTalk":
                context.BattleTalkMessage.Add(
                    CreateBattleTalkMessage(sourceLanguage, engine, updatedDate));
                break;
            case "QuestPlate":
            case "QuestPlateByName":
                context.QuestPlate.Add(
                    CreateQuestPlate(sourceLanguage, engine, updatedDate));
                break;
            case "TalkSubtitle":
                context.TalkSubtitleMessage.Add(
                    CreateTalkSubtitleMessage(sourceLanguage, engine, updatedDate));
                break;
            case "MiniTalk":
                context.MiniTalkMessage.Add(
                    CreateMiniTalkMessage(sourceLanguage, engine, updatedDate));
                break;
            case "TextGimmickHint":
                context.TextGimmickHintMessage.Add(
                    CreateTextGimmickHintMessage(sourceLanguage, engine, updatedDate));
                break;
            case "SelectString":
                context.SelectString.Add(
                    CreateSelectString(sourceLanguage, engine, updatedDate));
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(retrieval),
                    retrieval,
                    "Unknown legacy retrieval.");
        }
    }

    /// <summary>
    ///     Reloads a toast cache after the second staged row is persisted.
    /// </summary>
    /// <param name="plugin">The plugin instance owning the cache.</param>
    /// <param name="retrieval">The legacy retrieval path under test.</param>
    private static void ReloadLegacyToastCache(
        PluginEntry plugin,
        string retrieval)
    {
        switch (retrieval)
        {
            case "Toast":
            case "ToastReturn":
                plugin.LoadAllOtherToasts();
                break;
            case "ErrorToast":
                plugin.LoadAllErrorToasts();
                break;
        }
    }

    /// <summary>
    ///     Invokes one legacy retrieval and returns the selected row scope.
    /// </summary>
    /// <param name="plugin">The configured plugin instance.</param>
    /// <param name="retrieval">The retrieval path to invoke.</param>
    /// <returns>The selected row's source identity and engine.</returns>
    private static (string? SourceLanguage, int? TranslationEngine)
        InvokeLegacyRetrieval(PluginEntry plugin, string retrieval)
    {
        return retrieval switch
        {
            "Talk" => GetScope(plugin.FindAndReturnTalkMessage(
                CreateTalkMessage("ja", 7, DateTime.Now))),
            "Toast" => GetToastScopeAfterFind(
                plugin,
                CreateToastMessage("NonError", "ja", 7, DateTime.Now),
                findError: false),
            "ToastReturn" => GetScope(plugin.FindAndReturnToastMessage(
                CreateToastMessage("NonError", "ja", 7, DateTime.Now))),
            "ErrorToast" => GetToastScopeAfterFind(
                plugin,
                CreateToastMessage("Error", "ja", 7, DateTime.Now),
                findError: true),
            "BattleTalk" => GetScope(plugin.FindAndReturnBattleTalkMessage(
                CreateBattleTalkMessage("ja", 7, DateTime.Now))),
            "QuestPlate" => GetScope(plugin.FindQuestPlate(
                CreateQuestPlate("ja", 7, DateTime.Now))),
            "QuestPlateByName" => GetScope(plugin.FindQuestPlateByName(
                CreateQuestPlate("ja", 7, DateTime.Now))),
            "TalkSubtitle" => GetScope(
                plugin.FindAndReturnTalkSubtitleMessage(
                    CreateTalkSubtitleMessage("ja", 7, DateTime.Now))),
            "MiniTalk" => GetScope(plugin.FindAndReturnMiniTalkMessage(
                CreateMiniTalkMessage("ja", 7, DateTime.Now))),
            "TextGimmickHint" => GetScope(
                plugin.FindAndReturnTextGimmickHintMessage(
                    CreateTextGimmickHintMessage("ja", 7, DateTime.Now))),
            "SelectString" => GetScope(
                plugin.FindAndReturnCutSceneSelectStringMessage(
                    CreateSelectString("ja", 7, DateTime.Now))),
            _ => throw new ArgumentOutOfRangeException(
                nameof(retrieval),
                retrieval,
                "Unknown legacy retrieval."),
        };
    }

    /// <summary>
    ///     Invokes one boolean toast lookup and returns its selected row scope.
    /// </summary>
    /// <param name="plugin">The configured plugin instance.</param>
    /// <param name="request">The toast lookup request.</param>
    /// <param name="findError">Whether to invoke the error-toast lookup.</param>
    /// <returns>The selected toast row's source identity and engine.</returns>
    private static (string? SourceLanguage, int? TranslationEngine)
        GetToastScopeAfterFind(
            PluginEntry plugin,
            ToastMessage request,
            bool findError)
    {
        plugin.FoundToastMessage = null;
        _ = findError
            ? plugin.FindErrorToastMessage(request)
            : plugin.FindToastMessage(request);
        return GetScope(plugin.FoundToastMessage);
    }

    /// <summary>
    ///     Gets the stored scope fields from a Talk row.
    /// </summary>
    /// <param name="row">The selected row.</param>
    /// <returns>The row's source identity and engine.</returns>
    private static (string?, int?) GetScope(TalkMessage? row)
    {
        return (row?.OriginalTalkMessageLang, row?.TranslationEngine);
    }

    /// <summary>
    ///     Gets the persisted lookup fields from a Talk row.
    /// </summary>
    /// <param name="row">The selected row.</param>
    /// <returns>The row's source identity, engine, and update timestamp.</returns>
    private static (string?, int?, DateTime?) GetLookupScope(TalkMessage? row)
    {
        return (
            row?.OriginalTalkMessageLang,
            row?.TranslationEngine,
            row?.UpdatedDate);
    }

    /// <summary>
    ///     Gets the stored scope fields from a toast row.
    /// </summary>
    /// <param name="row">The selected row.</param>
    /// <returns>The row's source identity and engine.</returns>
    private static (string?, int?) GetScope(ToastMessage? row)
    {
        return (row?.OriginalLang, row?.TranslationEngine);
    }

    /// <summary>
    ///     Gets the stored scope fields from a BattleTalk row.
    /// </summary>
    /// <param name="row">The selected row.</param>
    /// <returns>The row's source identity and engine.</returns>
    private static (string?, int?) GetScope(BattleTalkMessage? row)
    {
        return (row?.OriginalBattleTalkMessageLang, row?.TranslationEngine);
    }

    /// <summary>
    ///     Gets the persisted lookup fields from a BattleTalk row.
    /// </summary>
    /// <param name="row">The selected row.</param>
    /// <returns>The row's source identity, engine, and update timestamp.</returns>
    private static (string?, int?, DateTime?) GetLookupScope(
        BattleTalkMessage? row)
    {
        return (
            row?.OriginalBattleTalkMessageLang,
            row?.TranslationEngine,
            row?.UpdatedDate);
    }

    /// <summary>
    ///     Gets the stored scope fields from a quest row.
    /// </summary>
    /// <param name="row">The selected row.</param>
    /// <returns>The row's source identity and engine.</returns>
    private static (string?, int?) GetScope(QuestPlate? row)
    {
        return (row?.OriginalLang, row?.TranslationEngine);
    }

    /// <summary>
    ///     Gets the stored scope fields from a TalkSubtitle row.
    /// </summary>
    /// <param name="row">The selected row.</param>
    /// <returns>The row's source identity and engine.</returns>
    private static (string?, int?) GetScope(TalkSubtitleMessage? row)
    {
        return (row?.OriginalTalkSubtitleMessageLang, row?.TranslationEngine);
    }

    /// <summary>
    ///     Gets the stored scope fields from a MiniTalk row.
    /// </summary>
    /// <param name="row">The selected row.</param>
    /// <returns>The row's source identity and engine.</returns>
    private static (string?, int?) GetScope(MiniTalkMessage? row)
    {
        return (row?.OriginalMiniTalkMessageLang, row?.TranslationEngine);
    }

    /// <summary>
    ///     Gets the stored scope fields from a text-gimmick row.
    /// </summary>
    /// <param name="row">The selected row.</param>
    /// <returns>The row's source identity and engine.</returns>
    private static (string?, int?) GetScope(TextGimmickHintMessage? row)
    {
        return (row?.OriginalLang, row?.TranslationEngine);
    }

    /// <summary>
    ///     Gets the stored scope fields from a select-string row.
    /// </summary>
    /// <param name="row">The selected row.</param>
    /// <returns>The row's source identity and engine.</returns>
    private static (string?, int?) GetScope(SelectString? row)
    {
        return (row?.OriginalSelectStringLang, row?.TranslationEngine);
    }

    /// <summary>
    ///     Creates the semantic Talk row used by legacy lookup tests.
    /// </summary>
    /// <param name="sourceLanguage">The stored source identity.</param>
    /// <param name="engine">The stored translation engine.</param>
    /// <param name="updatedDate">The row update timestamp.</param>
    /// <returns>The configured Talk row.</returns>
    private static TalkMessage CreateTalkMessage(
        string sourceLanguage,
        int engine,
        DateTime updatedDate)
    {
        return new TalkMessage(
            "Krile",
            "The plan remains unchanged.",
            sourceLanguage,
            sourceLanguage,
            "Krile",
            "O plano permanece inalterado.",
            "pt-BR",
            engine,
            null,
            updatedDate,
            updatedDate);
    }

    /// <summary>
    ///     Creates the semantic toast row used by legacy lookup tests.
    /// </summary>
    /// <param name="toastType">The stored toast type.</param>
    /// <param name="sourceLanguage">The stored source identity.</param>
    /// <param name="engine">The stored translation engine.</param>
    /// <param name="updatedDate">The row update timestamp.</param>
    /// <returns>The configured toast row.</returns>
    private static ToastMessage CreateToastMessage(
        string toastType,
        string sourceLanguage,
        int engine,
        DateTime updatedDate)
    {
        return new ToastMessage(
            toastType,
            "The duty is ready.",
            sourceLanguage,
            "A missão está pronta.",
            "pt-BR",
            engine,
            updatedDate,
            updatedDate);
    }

    /// <summary>
    ///     Creates the semantic BattleTalk row used by legacy lookup tests.
    /// </summary>
    /// <param name="sourceLanguage">The stored source identity.</param>
    /// <param name="engine">The stored translation engine.</param>
    /// <param name="updatedDate">The row update timestamp.</param>
    /// <returns>The configured BattleTalk row.</returns>
    private static BattleTalkMessage CreateBattleTalkMessage(
        string sourceLanguage,
        int engine,
        DateTime updatedDate)
    {
        return new BattleTalkMessage(
            "Alphinaud",
            "Hold the line!",
            sourceLanguage,
            sourceLanguage,
            "Alphinaud",
            "Segurem a linha!",
            "pt-BR",
            engine,
            null,
            updatedDate,
            updatedDate);
    }

    /// <summary>
    ///     Creates the semantic quest row used by legacy lookup tests.
    /// </summary>
    /// <param name="sourceLanguage">The stored source identity.</param>
    /// <param name="engine">The stored translation engine.</param>
    /// <param name="updatedDate">The row update timestamp.</param>
    /// <returns>The configured quest row.</returns>
    private static QuestPlate CreateQuestPlate(
        string sourceLanguage,
        int engine,
        DateTime updatedDate)
    {
        return new QuestPlate(
            "A Test of Resolve",
            "Speak with the attendant.",
            sourceLanguage,
            "Um Teste de Determinação",
            "Fale com o atendente.",
            null,
            "pt-BR",
            engine,
            updatedDate,
            updatedDate,
            "test-version");
    }

    /// <summary>
    ///     Creates the semantic TalkSubtitle row used by legacy lookup tests.
    /// </summary>
    /// <param name="sourceLanguage">The stored source identity.</param>
    /// <param name="engine">The stored translation engine.</param>
    /// <param name="updatedDate">The row update timestamp.</param>
    /// <returns>The configured TalkSubtitle row.</returns>
    private static TalkSubtitleMessage CreateTalkSubtitleMessage(
        string sourceLanguage,
        int engine,
        DateTime updatedDate)
    {
        return new TalkSubtitleMessage(
            "The way is clear.",
            sourceLanguage,
            "O caminho está livre.",
            "pt-BR",
            engine,
            updatedDate,
            updatedDate);
    }

    /// <summary>
    ///     Creates the semantic MiniTalk row used by legacy lookup tests.
    /// </summary>
    /// <param name="sourceLanguage">The stored source identity.</param>
    /// <param name="engine">The stored translation engine.</param>
    /// <param name="updatedDate">The row update timestamp.</param>
    /// <returns>The configured MiniTalk row.</returns>
    private static MiniTalkMessage CreateMiniTalkMessage(
        string sourceLanguage,
        int engine,
        DateTime updatedDate)
    {
        return new MiniTalkMessage(
            "Over here!",
            sourceLanguage,
            "Por aqui!",
            "pt-BR",
            engine,
            updatedDate,
            updatedDate);
    }

    /// <summary>
    ///     Creates the semantic text-gimmick row used by legacy lookup tests.
    /// </summary>
    /// <param name="sourceLanguage">The stored source identity.</param>
    /// <param name="engine">The stored translation engine.</param>
    /// <param name="updatedDate">The row update timestamp.</param>
    /// <returns>The configured text-gimmick row.</returns>
    private static TextGimmickHintMessage CreateTextGimmickHintMessage(
        string sourceLanguage,
        int engine,
        DateTime updatedDate)
    {
        return new TextGimmickHintMessage(
            "Examine the device.",
            sourceLanguage,
            "Examine o dispositivo.",
            "pt-BR",
            engine,
            updatedDate,
            updatedDate);
    }

    /// <summary>
    ///     Creates the semantic select-string row used by legacy lookup tests.
    /// </summary>
    /// <param name="sourceLanguage">The stored source identity.</param>
    /// <param name="engine">The stored translation engine.</param>
    /// <param name="updatedDate">The row update timestamp.</param>
    /// <returns>The configured select-string row.</returns>
    private static SelectString CreateSelectString(
        string sourceLanguage,
        int engine,
        DateTime updatedDate)
    {
        return new SelectString(
            "Proceed?",
            sourceLanguage,
            "[\"Proceed.\"]",
            "Prosseguir?",
            "[\"Prosseguir.\"]",
            "pt-BR",
            engine,
            updatedDate,
            updatedDate);
    }

    /// <summary>
    ///     Resolves the dedicated quest-popup type from the compiled plugin
    ///     assembly.
    /// </summary>
    /// <returns>The resolved quest-popup type.</returns>
    private static Type ResolveQuestPopupTextType()
    {
        var questPopupType = typeof(PluginEntry).Assembly.GetType(
            "Echoglossian.EFCoreSqlite.Models.Journal.QuestPopupText");
        Assert.NotNull(questPopupType);
        return questPopupType!;
    }

    /// <summary>
    ///     Resolves the dedicated generic selection-dialog type from the
    ///     compiled plugin assembly.
    /// </summary>
    /// <returns>The resolved generic selection-dialog type.</returns>
    private static Type ResolveSelectionDialogTextType()
    {
        var selectionDialogType = typeof(PluginEntry).Assembly.GetType(
            "Echoglossian.EFCoreSqlite.Models.SelectionDialogText");
        Assert.NotNull(selectionDialogType);
        return selectionDialogType!;
    }

    /// <summary>
    ///     Creates one reflective quest-popup instance for DB tests.
    /// </summary>
    /// <param name="questPopupType">The quest-popup runtime type.</param>
    /// <param name="surfaceName">The popup surface name.</param>
    /// <param name="questId">The optional quest id.</param>
    /// <param name="originalTitle">The original popup title.</param>
    /// <param name="originalBody">The original popup body.</param>
    /// <param name="originalLang">The original source language.</param>
    /// <param name="translatedTitle">The translated popup title.</param>
    /// <param name="translatedBody">The translated popup body.</param>
    /// <param name="translationLang">The target translation language.</param>
    /// <param name="translationEngine">The translation engine.</param>
    /// <param name="gameVersion">The stored game version.</param>
    /// <param name="sourceContentHash">The stored source content hash.</param>
    /// <param name="timestamp">The created/updated timestamp.</param>
    /// <returns>The configured popup row instance.</returns>
    private static object CreateQuestPopupText(
        Type questPopupType,
        string surfaceName,
        string? questId,
        string originalTitle,
        string originalBody,
        string originalLang,
        string translatedTitle,
        string translatedBody,
        string translationLang,
        int translationEngine,
        string gameVersion,
        string sourceContentHash,
        DateTime timestamp)
    {
        var instance = Activator.CreateInstance(questPopupType);
        Assert.NotNull(instance);
        SetObjectProperty(instance!, "SurfaceName", surfaceName);
        SetObjectProperty(instance!, "QuestId", questId);
        SetObjectProperty(instance!, "OriginalTitle", originalTitle);
        SetObjectProperty(instance!, "OriginalBody", originalBody);
        SetObjectProperty(instance!, "OriginalLang", originalLang);
        SetObjectProperty(instance!, "TranslatedTitle", translatedTitle);
        SetObjectProperty(instance!, "TranslatedBody", translatedBody);
        SetObjectProperty(instance!, "TranslationLang", translationLang);
        SetObjectProperty(instance!, "TranslationEngine", translationEngine);
        SetObjectProperty(instance!, "GameVersion", gameVersion);
        SetObjectProperty(instance!, "SourceContentHash", sourceContentHash);
        SetObjectProperty(instance!, "CreatedDate", timestamp);
        SetObjectProperty(instance!, "UpdatedDate", timestamp);
        return instance!;
    }

    /// <summary>
    ///     Creates one reflective generic selection-dialog instance for DB
    ///     tests.
    /// </summary>
    /// <param name="selectionDialogType">The runtime selection-dialog type.</param>
    /// <param name="addonName">The addon name.</param>
    /// <param name="originalTextsAsText">The serialized original payload.</param>
    /// <param name="originalLang">The original source language.</param>
    /// <param name="translatedTextsAsText">The serialized translated payload.</param>
    /// <param name="translationLang">The target translation language.</param>
    /// <param name="translationEngine">The translation engine.</param>
    /// <param name="gameVersion">The stored game version.</param>
    /// <param name="timestamp">The created/updated timestamp.</param>
    /// <returns>The configured selection-dialog row instance.</returns>
    private static object CreateSelectionDialogText(
        Type selectionDialogType,
        string addonName,
        string originalTextsAsText,
        string originalLang,
        string translatedTextsAsText,
        string translationLang,
        int translationEngine,
        string gameVersion,
        DateTime timestamp)
    {
        var instance = Activator.CreateInstance(selectionDialogType);
        Assert.NotNull(instance);
        SetObjectProperty(instance!, "AddonName", addonName);
        SetObjectProperty(instance!, "OriginalTextsAsText", originalTextsAsText);
        SetObjectProperty(instance!, "OriginalLang", originalLang);
        SetObjectProperty(instance!, "TranslatedTextsAsText", translatedTextsAsText);
        SetObjectProperty(instance!, "TranslationLang", translationLang);
        SetObjectProperty(instance!, "TranslationEngine", translationEngine);
        SetObjectProperty(instance!, "GameVersion", gameVersion);
        SetObjectProperty(instance!, "CreatedDate", timestamp);
        SetObjectProperty(instance!, "UpdatedDate", timestamp);
        return instance!;
    }

    /// <summary>
    ///     Reads one reflected property value from a popup-row instance.
    /// </summary>
    /// <typeparam name="T">The expected property type.</typeparam>
    /// <param name="instance">The source object.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The resolved property value.</returns>
    private static T? GetObjectProperty<T>(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        return (T?)property!.GetValue(instance);
    }

    /// <summary>
    ///     Writes one reflected property value onto a popup-row instance.
    /// </summary>
    /// <param name="instance">The target object.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The value to assign.</param>
    private static void SetObjectProperty(
        object instance,
        string propertyName,
        object? value)
    {
        var property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        property!.SetValue(instance, value);
    }

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
        catch
        {
            // Best-effort cleanup for temp test DB folders.
        }
    }

    /// <summary>
    ///     Creates the minimal plugin instance required by entity formatters.
    /// </summary>
    /// <returns>The formatting-only plugin instance.</returns>
    private static PluginEntry CreateFormattingPlugin(Config? config = null)
    {
        var plugin = (PluginEntry)RuntimeHelpers.GetUninitializedObject(
            typeof(PluginEntry));
        config ??= new Config
        {
            Lang = 28,
            ChosenTransEngine = 0,
        };
        var languages = new Dictionary<int, LanguageInfo>
        {
            [28] = new LanguageInfo(
                "pt-BR",
                "Portuguese",
                string.Empty,
                string.Empty,
                []),
        };

        SetPrivateField(plugin, "configuration", config);
        SetPrivateField(plugin, "languagesDictionary", languages);
        return plugin;
    }

    /// <summary>
    ///     Creates the target-language table required by reuse-scope tests.
    /// </summary>
    /// <returns>A language table containing the configured Portuguese target.</returns>
    private static Dictionary<int, LanguageInfo> CreateTargetLanguages()
    {
        return new Dictionary<int, LanguageInfo>
        {
            [28] = new LanguageInfo(
                "pt-BR",
                "Portuguese",
                string.Empty,
                string.Empty,
                []),
        };
    }

    /// <summary>
    ///     Sets one private plugin field on a formatting-only instance.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    /// <param name="fieldName">The private field name.</param>
    /// <param name="value">The value to assign.</param>
    private static void SetPrivateField(
        PluginEntry plugin,
        string fieldName,
        object value)
    {
        var field = typeof(PluginEntry).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(plugin, value);
    }

}
