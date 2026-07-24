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
