// <copyright file="DbOperationsTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;

using PluginEntry = Echoglossian.Echoglossian;
using Echoglossian.EFCoreSqlite.Models;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the DB-side guard that decides whether translated text is safe
///     to persist.
/// </summary>
public class DbOperationsTests
{
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
}
