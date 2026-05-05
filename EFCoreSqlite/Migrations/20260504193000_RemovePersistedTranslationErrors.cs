// <copyright file="20260504193000_RemovePersistedTranslationErrors.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian;
using Echoglossian.EFCoreSqlite;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Echoglossian.EFCoreSqlite.Migrations
{
    /// <summary>
    ///     Removes legacy rows whose translated payloads contain synthetic
    ///     translation-error placeholders emitted by failed engines.
    /// </summary>
    [DbContext(typeof(EchoglossianDbContext))]
    [Migration("20260504193000_RemovePersistedTranslationErrors")]
    public partial class RemovePersistedTranslationErrors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            DeleteRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "actiontooltips",
                "TranslatedActionName",
                "TranslatedActionDescription",
                "TranslatedTooltipText",
                "CanonicalPayloadAsText");
            DeleteRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "itemtooltips",
                "TranslatedItemName",
                "TranslatedItemDescription",
                "TranslatedTooltipText",
                "CanonicalPayloadAsText");
            DeleteRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "Traits",
                "TranslatedTraitName",
                "TranslatedTraitDescription",
                "TranslatedTooltipText",
                "CanonicalPayloadAsText");
            DeleteRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "talkmessages",
                "TranslatedSenderName",
                "TranslatedTalkMessage");
            DeleteRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "battletalkmessages",
                "TranslatedSenderName",
                "TranslatedBattleTalkMessage");
            DeleteRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "talksubtitlemessages",
                "TranslatedTalkSubtitleMessage");
            DeleteRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "minitalkmessages",
                "TranslatedMiniTalkMessage");
            DeleteRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "textgimmickhintmessages",
                "TranslatedText");
            DeleteRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "selectstrings",
                "TranslatedSelectString",
                "TranslatedOptionsAsText");
            DeleteRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "toastmessages",
                "TranslatedToastMessage");
            DeleteRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "gamewindows",
                "TranslatedWindowStrings");
            DeleteRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "questplates",
                "TranslatedQuestName",
                "TranslatedQuestMessage",
                "CanonicalRowsAsText",
                "TranslatedObjectivesAsText",
                "TranslatedObjectiveRowsByKeyAsText",
                "TranslatedSummariesAsText",
                "TranslatedSummaryRowsByKeyAsText",
                "TranslatedSystemRowsAsText",
                "TranslatedSystemRowsByKeyAsText");
            DeleteRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "stringarraydatas",
                "TranslatedStrings",
                "TranslatedStringsWithPayloads",
                "TranslatedStructuredPayload");
            DeleteRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "npcnames",
                "TranslatedNpcName");
            DeleteRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "locationnames",
                "TranslatedLocationName");

            DeleteReferenceTextRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "generalactiontexts");
            DeleteReferenceTextRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "buddyactiontexts");
            DeleteReferenceTextRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "companyactiontexts");
            DeleteReferenceTextRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "craftactiontexts");
            DeleteReferenceTextRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "petactiontexts");
            DeleteReferenceTextRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "eventactiontexts");
            DeleteReferenceTextRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "eventitemtexts");
            DeleteReferenceTextRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "bgcarmyactiontexts");
            DeleteReferenceTextRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "aozactiontexts");
            DeleteReferenceTextRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "pvpactiontexts");
            DeleteReferenceTextRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "mountactiontexts");
            DeleteReferenceTextRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "maincommandtexts");
            DeleteReferenceTextRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "eurekamagiaactiontexts");
            DeleteReferenceTextRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                "deepdungeonitemtexts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }

        private static void DeleteReferenceTextRowsContainingSyntheticTranslationErrors(
            MigrationBuilder migrationBuilder,
            string tableName)
        {
            DeleteRowsContainingSyntheticTranslationErrors(
                migrationBuilder,
                tableName,
                "TranslatedName",
                "TranslatedDescription",
                "CanonicalPayloadAsText");
        }

        private static void DeleteRowsContainingSyntheticTranslationErrors(
            MigrationBuilder migrationBuilder,
            string tableName,
            params string[] columnNames)
        {
            foreach (var syntheticErrorPrefix in TranslationResultGuard.GetSyntheticErrorPrefixes())
            {
                var escapedSyntheticErrorPrefix = syntheticErrorPrefix.Replace("'", "''");
                var whereClause = string.Join(
                    " OR ",
                    columnNames.Select(
                        columnName =>
                            $@"instr(COALESCE(""{columnName}"", ''), '{escapedSyntheticErrorPrefix}') > 0"));

                migrationBuilder.Sql(
                    $@"DELETE FROM ""{tableName}"" WHERE {whereClause};");
            }
        }
    }
}
