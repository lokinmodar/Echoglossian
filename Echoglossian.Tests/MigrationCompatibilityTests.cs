using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.EFCoreSqlite.Models.Journal;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Echoglossian.Tests;

public class MigrationCompatibilityTests
{
    private const string PreviousMigration = "20250724225932_fixNewEntity24052025";
    private const string LatestLookupMigration = "20260416031500_AddCanonicalStringArrayPayloadFields";

    private static readonly string[] TableNames =
    {
        "talkmessages",
        "battletalkmessages",
        "talksubtitlemessages",
        "toastmessages",
        "questplates",
        "gamewindows",
        "stringarraydatas",
    };

    private static readonly string[] ExpectedIndexes =
    {
        "IX_battletalkmessages_lookup",
        "IX_gamewindows_lookup",
        "IX_questplates_lookup",
        "IX_stringarraydatas_context_lookup",
        "IX_stringarraydatas_lookup",
        "IX_talkmessages_lookup",
        "IX_talksubtitlemessages_lookup",
        "IX_toastmessages_lookup",
    };

    [Fact]
    public void Migrate_FromPreviousVersion_PreservesRows_AndCreatesLookupIndexes()
    {
        var configDir = Path.Combine(Path.GetTempPath(), "Echoglossian.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                var migrator = context.GetService<IMigrator>();
                migrator.Migrate(PreviousMigration);
            }

            SeedSampleData(configDir);

            Dictionary<string, long> countsBefore;
            List<string> pendingBefore;
            using (var context = new EchoglossianDbContext(configDir))
            {
                countsBefore = GetCounts(context);
                pendingBefore = context.Database.GetPendingMigrations().ToList();
                context.Database.Migrate();
            }

            SeedTextGimmickHintData(configDir);

            Dictionary<string, long> countsAfter;
            List<string> pendingAfter;
            HashSet<string> indexes;
            using (var context = new EchoglossianDbContext(configDir))
            {
                countsAfter = GetCounts(context);
                pendingAfter = context.Database.GetPendingMigrations().ToList();
                indexes = GetExistingIndexes(context);
            }

            Assert.Contains(LatestLookupMigration, pendingBefore);
            Assert.Empty(pendingAfter);

            foreach (var tableName in TableNames)
            {
                if (string.Equals(tableName, "questplates", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Equal(1, countsBefore[tableName]);
                    Assert.Equal(0, countsAfter[tableName]);
                    continue;
                }

                Assert.True(
                    countsBefore[tableName] == countsAfter[tableName],
                    $"Row count changed for table {tableName}.");
            }

            foreach (var indexName in ExpectedIndexes)
            {
                Assert.True(indexes.Contains(indexName), $"Index {indexName} was not created.");
            }

            Assert.Equal(1, GetCount(configDir, "textgimmickhintmessages"));
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    private static void SeedSampleData(string configDir)
    {
        using var context = new EchoglossianDbContext(configDir);
        var now = DateTime.UtcNow;

        context.TalkMessage.Add(new TalkMessage(
            senderName: "Alisaie",
            originalTalkMessage: "Hello there",
            originalTalkMessageLang: "en",
            originalSenderNameLang: "en",
            translatedSenderName: "Alisaie",
            translatedTalkMessage: "Ola",
            translationLang: "pt",
            translationEngine: 1,
            rtlLangTranslationImageData: null,
            createdDate: now,
            updatedDate: now));

        context.BattleTalkMessage.Add(new BattleTalkMessage(
            senderName: "Thancred",
            originalBattleTalkMessage: "Move!",
            originalBattleTalkMessageLang: "en",
            originalSenderNameLang: "en",
            translatedSenderName: "Thancred",
            translatedBattleTalkMessage: "Mexa-se!",
            translationLang: "pt",
            translationEngine: 1,
            rtlLangTranslationImageData: null,
            createdDate: now,
            updatedDate: now));

        context.TalkSubtitleMessage.Add(new TalkSubtitleMessage(
            originalTalkSubtitleMessage: "A subtitle line",
            originalTalkSubtitleMessageLang: "en",
            translatedTalkSubtitleMessage: "Uma legenda",
            translationLang: "pt",
            translationEngine: 1,
            createdDate: now,
            updatedDate: now));

        context.ToastMessage.Add(new ToastMessage(
            toastType: "NonError",
            originalToastMessage: "Duty complete",
            originalLang: "en",
            translatedToastMessage: "Missao concluida",
            translationLang: "pt",
            translationEngine: 1,
            createdDate: now,
            updatedDate: now));

        var questPlate = new QuestPlate(
            questName: "Test Quest",
            originalQuestMessage: "Talk to the Scions.",
            originalLang: "en",
            translatedQuestName: "Missao Teste",
            translatedQuestMessage: "Fale com os Scions.",
            questId: "QST-001",
            translationLang: "pt",
            translationEngine: 1,
            createdDate: now,
            updatedDate: now,
            gameVersion: "7.2");
        questPlate.UpdateFieldsAsText();

        context.Database.ExecuteSqlInterpolated($"""
            INSERT INTO questplates (
                QuestId,
                QuestName,
                OriginalQuestMessage,
                OriginalLang,
                TranslatedQuestName,
                TranslatedQuestMessage,
                TranslationLang,
                TranslationEngine,
                CreatedDate,
                UpdatedDate,
                ObjectivesAsText,
                SummariesAsText
            ) VALUES (
                {questPlate.QuestId},
                {questPlate.QuestName},
                {questPlate.OriginalQuestMessage},
                {questPlate.OriginalLang},
                {questPlate.TranslatedQuestName},
                {questPlate.TranslatedQuestMessage},
                {questPlate.TranslationLang},
                {questPlate.TranslationEngine},
                {questPlate.CreatedDate},
                {questPlate.UpdatedDate},
                {questPlate.ObjectivesAsText},
                {questPlate.SummariesAsText}
            )
            """);

        context.Database.ExecuteSqlInterpolated($"""
            INSERT INTO gamewindows (
                WindowAddonName,
                OriginalWindowStrings,
                OriginalWindowStringsLang,
                TranslatedWindowStrings,
                TranslationLang,
                TranslationEngine,
                GameVersion,
                CreatedDate,
                UpdatedDate
            ) VALUES (
                {"JournalDetail"},
                {"{\"atkValues\":{\"1\":\"Quest\"}}"},
                {"en"},
                {"{\"atkValues\":{\"1\":\"Missao\"}}"},
                {"pt"},
                {1},
                {"7.2"},
                {now},
                {now}
            )
            """);

        context.Database.ExecuteSqlInterpolated($"""
            INSERT INTO stringarraydatas (
                Type,
                Size,
                RawData,
                FormattedRawData,
                OriginalLang,
                OriginalStrings,
                TranslationLang,
                TranslatedStrings,
                TranslatedStringsWithPayloads,
                TranslationEngine,
                GameVersion,
                CreatedAt,
                UpdatedAt
            ) VALUES (
                {"AddonJournalDetail"},
                {2},
                {new byte[] { 1, 2, 3, 4 }},
                {"01020304"},
                {"en"},
                {"[\"Quest\",\"Reward\"]"},
                {"pt"},
                {"[\"Missao\",\"Recompensa\"]"},
                {"[\"Missao\",\"Recompensa\"]"},
                {1},
                {"7.2"},
                {now},
                {now}
            )
            """);

        context.SaveChanges();
    }

    private static void SeedTextGimmickHintData(string configDir)
    {
        using var context = new EchoglossianDbContext(configDir);
        var now = DateTime.UtcNow;

        context.TextGimmickHintMessage.Add(new TextGimmickHintMessage(
            originalText: "Press the lever",
            originalLang: "en",
            translatedText: "Puxe a alavanca",
            translationLang: "pt",
            translationEngine: 1,
            createdDate: now,
            updatedDate: now));

        context.SaveChanges();
    }

    private static Dictionary<string, long> GetCounts(EchoglossianDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var tableName in TableNames)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM [{tableName}]";
            result[tableName] = Convert.ToInt64(command.ExecuteScalar());
        }

        connection.Close();
        return result;
    }

    private static HashSet<string> GetExistingIndexes(EchoglossianDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'index'";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            existing.Add(reader.GetString(0));
        }

        connection.Close();
        return existing;
    }

    private static long GetCount(string configDir, string tableName)
    {
        using var context = new EchoglossianDbContext(configDir);
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM [{tableName}]";
        var result = Convert.ToInt64(command.ExecuteScalar());
        connection.Close();
        return result;
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
        catch (IOException)
        {
            // Best effort cleanup for transient SQLite file locks during tests.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort cleanup for transient SQLite file locks during tests.
        }
    }
}
