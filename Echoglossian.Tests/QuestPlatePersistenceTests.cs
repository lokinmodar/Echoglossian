// <copyright file="QuestPlatePersistenceTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite.Models.Journal;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers persistence-sensitive <see cref="QuestPlate" /> behavior so
///     canonical quest rows are not lost between in-memory translation and DB
///     serialization.
/// </summary>
public class QuestPlatePersistenceTests
{
    /// <summary>
    ///     Ensures an unsaved canonical translation is preserved when the plate
    ///     refreshes from serialized fields that have not yet been materialized.
    /// </summary>
    [Fact]
    public void UpdateFieldsFromText_PreservesInMemoryCanonicalTranslations_WhenSerializedFieldsAreEmpty()
    {
        var questPlate = new QuestPlate(
            questName: "Mind over Manor",
            originalQuestMessage: "With the power of pictomancy now at your disposal...",
            originalLang: "English",
            translatedQuestName: string.Empty,
            translatedQuestMessage: string.Empty,
            questId: "70391",
            translationLang: "pt",
            translationEngine: 0,
            createdDate: DateTime.UtcNow,
            updatedDate: DateTime.UtcNow,
            gameVersion: "2026.03.17.0000.0000")
        {
            CanonicalRows =
            [
                new QuestPlateCanonicalRow(
                    QuestPlateCanonicalRowSection.Summary,
                    "TEXT_KINGBB202_04855_SEQ_01",
                    "With the power of pictomancy now at your disposal...",
                    order: 1,
                    isCurrentSequence: true,
                    translatedText: "Com o poder da pictomancia agora a sua disposicao..."),
            ],
        };

        questPlate.UpdateFieldsFromText();

        var translatedCanonicalRow = Assert.Single(questPlate.CanonicalRows);
        Assert.Equal(
            "Com o poder da pictomancia agora a sua disposicao...",
            translatedCanonicalRow.TranslatedText);
        Assert.Equal(
            "Com o poder da pictomancia agora a sua disposicao...",
            questPlate.TranslatedQuestMessage);
        Assert.True(
            questPlate.TranslatedSummaryRowsByKey.TryGetValue(
                "TEXT_KINGBB202_04855_SEQ_01",
                out var translatedSummaryText));
        Assert.Equal(
            "Com o poder da pictomancia agora a sua disposicao...",
            translatedSummaryText);
    }

    /// <summary>
    ///     Ensures duplicate row text still updates the correct canonical row
    ///     when a specific row key is supplied.
    /// </summary>
    [Fact]
    public void SetTranslatedObjectiveText_UsesRowKeyBeforeSourceText_WhenDuplicateObjectiveTextsExist()
    {
        var questPlate = new QuestPlate(
            questName: "Mind over Manor",
            originalQuestMessage: "With the power of pictomancy now at your disposal...",
            originalLang: "English",
            translatedQuestName: string.Empty,
            translatedQuestMessage: string.Empty,
            questId: "70391",
            translationLang: "pt",
            translationEngine: 0,
            createdDate: DateTime.UtcNow,
            updatedDate: DateTime.UtcNow,
            gameVersion: "2026.03.17.0000.0000")
        {
            CanonicalRows =
            [
                new QuestPlateCanonicalRow(
                    QuestPlateCanonicalRowSection.Objective,
                    "TEXT_KINGBB202_04855_TODO_01",
                    "Speak with Kupopo.",
                    order: 1,
                    isCurrentSequence: false),
                new QuestPlateCanonicalRow(
                    QuestPlateCanonicalRowSection.Objective,
                    "TEXT_KINGBB202_04855_TODO_02",
                    "Speak with Kupopo.",
                    order: 2,
                    isCurrentSequence: false),
            ],
        };

        questPlate.SetTranslatedObjectiveText(
            "TEXT_KINGBB202_04855_TODO_01",
            "Speak with Kupopo.",
            "Fale com Kupopo.");
        questPlate.SetTranslatedObjectiveText(
            "TEXT_KINGBB202_04855_TODO_02",
            "Speak with Kupopo.",
            "Fale com Kupopo novamente.");
        questPlate.UpdateFieldsAsText();
        questPlate.UpdateFieldsFromText();

        Assert.Equal(
            "Fale com Kupopo.",
            questPlate.CanonicalRows.Single(row =>
                row.RowKey == "TEXT_KINGBB202_04855_TODO_01").TranslatedText);
        Assert.Equal(
            "Fale com Kupopo novamente.",
            questPlate.CanonicalRows.Single(row =>
                row.RowKey == "TEXT_KINGBB202_04855_TODO_02").TranslatedText);
        Assert.Equal(
            "Fale com Kupopo.",
            questPlate.TranslatedObjectiveRowsByKey["TEXT_KINGBB202_04855_TODO_01"]);
        Assert.Equal(
            "Fale com Kupopo novamente.",
            questPlate.TranslatedObjectiveRowsByKey["TEXT_KINGBB202_04855_TODO_02"]);
    }
}
