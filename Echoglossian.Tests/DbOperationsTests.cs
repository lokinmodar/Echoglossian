// <copyright file="DbOperationsTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;
using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
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
