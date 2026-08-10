// <copyright file="QuestDialogueMetadataPersistenceTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models.Journal;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using Xunit;

using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>
///     Covers exact-row persistence and reuse for quest dialogue metadata.
/// </summary>
public class QuestDialogueMetadataPersistenceTests
{
    /// <summary>
    ///     Ensures concurrent upserts for one exact row coalesce without a
    ///     unique-index failure or duplicate persisted rows.
    /// </summary>
    [Fact]
    public async Task UpsertQuestDialogueMetadataBatchAsync_ConcurrentSameKey_CoalescesIntoOneRow()
    {
        var configDir = CreateTempConfigDirectory();
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        PluginEntry.ConfigDirectory = configDir + Path.DirectorySeparatorChar;
        var plugin = (PluginEntry)RuntimeHelpers.GetUninitializedObject(
            typeof(PluginEntry));

        try
        {
            await using (var context = new EchoglossianDbContext(configDir))
            {
                await context.Database.MigrateAsync();
            }

            await Task.WhenAll(
                Enumerable.Range(0, 16)
                    .Select(index => Task.Run(() =>
                        plugin.UpsertQuestDialogueMetadataBatchAsync(
                            [CreateRow(speakerHint: $"Speaker {index}")],
                            CancellationToken.None))));

            await using var verification = new EchoglossianDbContext(configDir);
            var persisted = await verification.QuestDialogueMetadata
                .AsNoTracking()
                .Where(row => row.QuestId == 100 &&
                              row.QuestSequence == 1 &&
                              row.SourceLanguageCode == "en" &&
                              row.GameVersion == "2026.08.10.0000" &&
                              row.SourceRowKey == "TEXT_QUEST_001_SEQ_001" &&
                              row.SourceTextHash == "hash-001" &&
                              row.DerivationVersion == "v1")
                .ToListAsync();
            Assert.Single(persisted);
        }
        finally
        {
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures exact-row lookup rejects every logical-key mismatch and an
    ///     upsert replaces its existing logical row rather than duplicating it.
    /// </summary>
    [Fact]
    public async Task QuestDialogueMetadata_UsesExactLogicalKeyAndReplacesExistingRow()
    {
        var configDir = CreateTempConfigDirectory();
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        PluginEntry.ConfigDirectory = configDir + Path.DirectorySeparatorChar;
        var plugin = (PluginEntry)RuntimeHelpers.GetUninitializedObject(
            typeof(PluginEntry));

        try
        {
            await using (var context = new EchoglossianDbContext(configDir))
            {
                await context.Database.MigrateAsync();
                await context.QuestDialogueMetadata.AddRangeAsync(
                    CreateRow(),
                    CreateRow(questSequence: 2),
                    CreateRow(sourceLanguageCode: "ja"),
                    CreateRow(gameVersion: "2026.08.10.0001"),
                    CreateRow(sourceRowKey: "TEXT_QUEST_001_SEQ_002"),
                    CreateRow(sourceTextHash: "hash-002"),
                    CreateRow(derivationVersion: "v2", speakerHint: "Speaker v2"));
                await context.SaveChangesAsync();
            }

            var lookup = CreateLookup();
            var match = await plugin.FindQuestDialogueMetadataAsync(
                lookup,
                CancellationToken.None);

            Assert.NotNull(match);
            Assert.Equal("Speaker v1", match.SpeakerHint);
            var derivationVersionMatch = await plugin.FindQuestDialogueMetadataAsync(
                CreateLookup(derivationVersion: "v2"),
                CancellationToken.None);
            Assert.NotNull(derivationVersionMatch);
            Assert.Equal("Speaker v2", derivationVersionMatch.SpeakerHint);
            Assert.Null(await plugin.FindQuestDialogueMetadataAsync(
                CreateLookup(questSequence: 99),
                CancellationToken.None));
            Assert.Null(await plugin.FindQuestDialogueMetadataAsync(
                CreateLookup(sourceLanguageCode: "fr"),
                CancellationToken.None));
            Assert.Null(await plugin.FindQuestDialogueMetadataAsync(
                CreateLookup(gameVersion: "2026.08.10.9999"),
                CancellationToken.None));
            Assert.Null(await plugin.FindQuestDialogueMetadataAsync(
                CreateLookup(sourceRowKey: "TEXT_QUEST_001_SEQ_999"),
                CancellationToken.None));
            Assert.Null(await plugin.FindQuestDialogueMetadataAsync(
                CreateLookup(sourceTextHash: "hash-999"),
                CancellationToken.None));
            Assert.Null(await plugin.FindQuestDialogueMetadataAsync(
                CreateLookup(derivationVersion: "v999"),
                CancellationToken.None));
            Assert.Null(await plugin.FindQuestDialogueMetadataAsync(
                CreateLookup(sourceRowKey: string.Empty),
                CancellationToken.None));

            await plugin.UpsertQuestDialogueMetadataBatchAsync(
                [
                    CreateRow(speakerHint: "Speaker updated"),
                    CreateRow(derivationVersion: "v2", speakerHint: "Speaker v2 updated"),
                ],
                CancellationToken.None);

            await using var verification = new EchoglossianDbContext(configDir);
            var rows = await verification.QuestDialogueMetadata
                .AsNoTracking()
                .Where(row => row.QuestId == 100 &&
                              row.QuestSequence == 1 &&
                              row.SourceLanguageCode == "en" &&
                              row.GameVersion == "2026.08.10.0000" &&
                              row.SourceRowKey == "TEXT_QUEST_001_SEQ_001" &&
                              row.SourceTextHash == "hash-001" &&
                              row.DerivationVersion == "v1")
                .ToListAsync();
            var updated = Assert.Single(rows);
            Assert.Equal("Speaker updated", updated.SpeakerHint);
            Assert.Equal("v1", updated.DerivationVersion);
            var updatedDerivationVersionMatch =
                await plugin.FindQuestDialogueMetadataAsync(
                    CreateLookup(derivationVersion: "v2"),
                    CancellationToken.None);
            Assert.NotNull(updatedDerivationVersionMatch);
            Assert.Equal("Speaker v2 updated", updatedDerivationVersionMatch.SpeakerHint);
        }
        finally
        {
            PluginEntry.ConfigDirectory = previousConfigDirectory;
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Creates one metadata row with the canonical exact-match values.
    /// </summary>
    private static QuestDialogueMetadata CreateRow(
        ushort questSequence = 1,
        string sourceLanguageCode = "en",
        string gameVersion = "2026.08.10.0000",
        string sourceRowKey = "TEXT_QUEST_001_SEQ_001",
        string sourceTextHash = "hash-001",
        string derivationVersion = "v1",
        string speakerHint = "Speaker v1")
    {
        return new QuestDialogueMetadata
        {
            QuestId = 100,
            QuestSequence = questSequence,
            SourceLanguageCode = sourceLanguageCode,
            GameVersion = gameVersion,
            QuestSheetId = "001",
            QuestTextSheetName = "quest/001/Quest001",
            SourceRowKey = sourceRowKey,
            SourceTextHash = sourceTextHash,
            SourceTextPreview = "A precise quest dialogue line.",
            SpeakerHint = speakerHint,
            AddresseeHint = "Player",
            SpeakerRoleHint = "Quest giver",
            AddresseeRoleHint = "Adventurer",
            Provenance = "quest-text",
            ConfidenceTier = 2,
            DerivationVersion = derivationVersion,
            CreatedAtUtc = DateTime.UnixEpoch,
            UpdatedAtUtc = DateTime.UnixEpoch,
        };
    }

    /// <summary>
    ///     Creates one exact-row lookup with the canonical values.
    /// </summary>
    private static QuestDialogueMetadataLookup CreateLookup(
        ushort questSequence = 1,
        string sourceLanguageCode = "en",
        string gameVersion = "2026.08.10.0000",
        string sourceRowKey = "TEXT_QUEST_001_SEQ_001",
        string sourceTextHash = "hash-001",
        string derivationVersion = "v1")
    {
        return new QuestDialogueMetadataLookup(
            100,
            questSequence,
            sourceLanguageCode,
            gameVersion,
            sourceRowKey,
            sourceTextHash,
            derivationVersion);
    }

    /// <summary>
    ///     Creates an isolated temporary configuration directory.
    /// </summary>
    /// <returns>The created configuration directory path.</returns>
    private static string CreateTempConfigDirectory()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);
        return configDir;
    }

    /// <summary>
    ///     Deletes an isolated temporary directory when it is no longer used.
    /// </summary>
    /// <param name="path">The directory path to delete.</param>
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
}
