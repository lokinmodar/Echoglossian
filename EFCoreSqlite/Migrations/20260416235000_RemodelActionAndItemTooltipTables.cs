// <copyright file="20260416235000_RemodelActionAndItemTooltipTables.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Echoglossian.EFCoreSqlite.Migrations
{
    /// <summary>
    ///     Rebuilds action and item tooltip tables around canonical DB-first payloads.
    /// </summary>
    [DbContext(typeof(EchoglossianDbContext))]
    [Migration("20260416235000_RemodelActionAndItemTooltipTables")]
    public partial class RemodelActionAndItemTooltipTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "actiontooltips");

            migrationBuilder.DropTable(
                name: "itemtooltips");

            migrationBuilder.CreateTable(
                name: "actiontooltips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActionId = table.Column<uint>(type: "INTEGER", nullable: false),
                    IconId = table.Column<uint>(type: "INTEGER", nullable: false),
                    ActionCategoryId = table.Column<uint>(type: "INTEGER", nullable: false),
                    ClassJobId = table.Column<uint>(type: "INTEGER", nullable: false),
                    ClassJobCategoryId = table.Column<uint>(type: "INTEGER", nullable: false),
                    ActionName = table.Column<string>(type: "TEXT", nullable: true),
                    ActionDescription = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalTooltipText = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalLang = table.Column<string>(type: "TEXT", nullable: true),
                    TranslatedActionName = table.Column<string>(type: "TEXT", nullable: true),
                    TranslatedActionDescription = table.Column<string>(type: "TEXT", nullable: true),
                    TranslatedTooltipText = table.Column<string>(type: "TEXT", nullable: true),
                    TranslationLang = table.Column<string>(type: "TEXT", nullable: true),
                    TranslationEngine = table.Column<int>(type: "INTEGER", nullable: true),
                    GameVersion = table.Column<string>(type: "TEXT", nullable: true),
                    SourceContentHash = table.Column<string>(type: "TEXT", nullable: true),
                    CanonicalPayloadAsText = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_actiontooltips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "itemtooltips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemId = table.Column<uint>(type: "INTEGER", nullable: false),
                    IconId = table.Column<uint>(type: "INTEGER", nullable: false),
                    ItemActionId = table.Column<uint>(type: "INTEGER", nullable: false),
                    ItemUiCategoryId = table.Column<uint>(type: "INTEGER", nullable: false),
                    ClassJobCategoryId = table.Column<uint>(type: "INTEGER", nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", nullable: true),
                    ItemDescription = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalTooltipText = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalLang = table.Column<string>(type: "TEXT", nullable: true),
                    TranslatedItemName = table.Column<string>(type: "TEXT", nullable: true),
                    TranslatedItemDescription = table.Column<string>(type: "TEXT", nullable: true),
                    TranslatedTooltipText = table.Column<string>(type: "TEXT", nullable: true),
                    TranslationLang = table.Column<string>(type: "TEXT", nullable: true),
                    TranslationEngine = table.Column<int>(type: "INTEGER", nullable: true),
                    GameVersion = table.Column<string>(type: "TEXT", nullable: true),
                    SourceContentHash = table.Column<string>(type: "TEXT", nullable: true),
                    CanonicalPayloadAsText = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itemtooltips", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_actiontooltips_lookup",
                table: "actiontooltips",
                columns: new[]
                {
                    "ActionId",
                    "TranslationLang",
                    "TranslationEngine",
                    "GameVersion",
                    "SourceContentHash",
                });

            migrationBuilder.CreateIndex(
                name: "IX_itemtooltips_lookup",
                table: "itemtooltips",
                columns: new[]
                {
                    "ItemId",
                    "TranslationLang",
                    "TranslationEngine",
                    "GameVersion",
                    "SourceContentHash",
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "actiontooltips");

            migrationBuilder.DropTable(
                name: "itemtooltips");

            migrationBuilder.CreateTable(
                name: "actiontooltips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    GameVersion = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalActionTooltip = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalActionTooltipLang = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
                    TranslatedActionTooltip = table.Column<string>(type: "TEXT", nullable: true),
                    TranslationEngine = table.Column<int>(type: "INTEGER", nullable: true),
                    TranslationLang = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_actiontooltips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "itemtooltips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    GameVersion = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalItemTooltip = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalItemTooltipLang = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
                    TranslatedItemTooltip = table.Column<string>(type: "TEXT", nullable: true),
                    TranslationEngine = table.Column<int>(type: "INTEGER", nullable: true),
                    TranslationLang = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itemtooltips", x => x.Id);
                });
        }
    }
}
