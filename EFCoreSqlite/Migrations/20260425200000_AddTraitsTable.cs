// <copyright file="20260425200000_AddTraitsTable.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;

using Echoglossian.EFCoreSqlite;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Echoglossian.EFCoreSqlite.Migrations
{
    /// <summary>
    ///     Adds canonical persisted trait tooltip storage.
    /// </summary>
    [DbContext(typeof(EchoglossianDbContext))]
    [Migration("20260425200000_AddTraitsTable")]
    public partial class AddTraitsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Traits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TraitId = table.Column<uint>(type: "INTEGER", nullable: false),
                    IconId = table.Column<uint>(type: "INTEGER", nullable: false),
                    ClassJobId = table.Column<uint>(type: "INTEGER", nullable: false),
                    ClassJobCategoryId = table.Column<uint>(type: "INTEGER", nullable: false),
                    TraitName = table.Column<string>(type: "TEXT", nullable: true),
                    TraitDescription = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalTooltipText = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalLang = table.Column<string>(type: "TEXT", nullable: true),
                    TranslatedTraitName = table.Column<string>(type: "TEXT", nullable: true),
                    TranslatedTraitDescription = table.Column<string>(type: "TEXT", nullable: true),
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
                    table.PrimaryKey("PK_Traits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_traits_lookup",
                table: "Traits",
                columns: new[]
                {
                    "TraitId",
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
                name: "Traits");
        }
    }
}
