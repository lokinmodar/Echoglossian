// <copyright file="20260404142000_AddMiniTalkMessage.cs" company="lokinmodar">
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
    [Migration("20260404142000_AddMiniTalkMessage")]
    public partial class AddMiniTalkMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "minitalkmessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OriginalMiniTalkMessage = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalMiniTalkMessageLang = table.Column<string>(type: "TEXT", nullable: true),
                    TranslatedMiniTalkMessage = table.Column<string>(type: "TEXT", nullable: true),
                    TranslationLang = table.Column<string>(type: "TEXT", nullable: true),
                    TranslationEngine = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_minitalkmessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_minitalkmessages_lookup",
                table: "minitalkmessages",
                columns: new[] { "TranslationLang", "TranslationEngine" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_minitalkmessages_lookup",
                table: "minitalkmessages");

            migrationBuilder.DropTable(
                name: "minitalkmessages");
        }
    }
}
