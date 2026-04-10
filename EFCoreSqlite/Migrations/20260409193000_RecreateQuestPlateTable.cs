// <copyright file="20260409193000_RecreateQuestPlateTable.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Echoglossian.EFCoreSqlite.Migrations
{
    [DbContext(typeof(EchoglossianDbContext))]
    [Migration("20260409193000_RecreateQuestPlateTable")]
    public partial class RecreateQuestPlateTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "questplates");

            migrationBuilder.CreateTable(
                name: "questplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuestId = table.Column<string>(type: "TEXT", nullable: true),
                    QuestName = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalQuestMessage = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalLang = table.Column<string>(type: "TEXT", nullable: true),
                    TranslatedQuestName = table.Column<string>(type: "TEXT", nullable: true),
                    TranslatedQuestMessage = table.Column<string>(type: "TEXT", nullable: true),
                    TranslationLang = table.Column<string>(type: "TEXT", nullable: true),
                    TranslationEngine = table.Column<int>(type: "INTEGER", nullable: true),
                    GameVersion = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ObjectivesAsText = table.Column<string>(type: "TEXT", nullable: true),
                    SummariesAsText = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_questplates_lookup",
                table: "questplates",
                columns: new[] { "QuestName", "TranslationLang", "TranslationEngine", "GameVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_questplates_questid_lookup",
                table: "questplates",
                columns: new[] { "QuestId", "TranslationLang", "TranslationEngine", "GameVersion" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "questplates");

            migrationBuilder.CreateTable(
                name: "questplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuestId = table.Column<string>(type: "TEXT", nullable: true),
                    QuestName = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalQuestMessage = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalLang = table.Column<string>(type: "TEXT", nullable: true),
                    TranslatedQuestName = table.Column<string>(type: "TEXT", nullable: true),
                    TranslatedQuestMessage = table.Column<string>(type: "TEXT", nullable: true),
                    TranslationLang = table.Column<string>(type: "TEXT", nullable: true),
                    TranslationEngine = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ObjectivesAsText = table.Column<string>(type: "TEXT", nullable: true),
                    SummariesAsText = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_questplates_lookup",
                table: "questplates",
                columns: new[] { "QuestName", "TranslationLang", "TranslationEngine" });
        }
    }
}
