// <copyright file="20260421133000_AddTranslationFailureCache.cs" company="lokinmodar">
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
    ///     Adds a persistent exact-translation-failure cache so identical
    ///     source/target/engine requests that already returned no result can be
    ///     skipped in later sessions.
    /// </summary>
    [DbContext(typeof(EchoglossianDbContext))]
    [Migration("20260421133000_AddTranslationFailureCache")]
    public partial class AddTranslationFailureCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "translationfailures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceText = table.Column<string>(type: "TEXT", nullable: true),
                    SourceTextHash = table.Column<string>(type: "TEXT", nullable: true),
                    SourceLanguage = table.Column<string>(type: "TEXT", nullable: true),
                    TargetLanguage = table.Column<string>(type: "TEXT", nullable: true),
                    TranslationEngine = table.Column<int>(type: "INTEGER", nullable: false),
                    FailureReason = table.Column<string>(type: "TEXT", nullable: true),
                    FailureCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translationfailures", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_translationfailures_lookup",
                table: "translationfailures",
                columns: new[]
                {
                    "SourceTextHash",
                    "SourceLanguage",
                    "TargetLanguage",
                    "TranslationEngine",
                    "SourceText",
                },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "translationfailures");
        }
    }
}
