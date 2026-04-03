// <copyright file="20260324193000_AddLookupIndexes.cs" company="lokinmodar">
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
    [Migration("20260324193000_AddLookupIndexes")]
    public partial class AddLookupIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_battletalkmessages_lookup",
                table: "battletalkmessages",
                columns: new[] { "SenderName", "TranslationLang", "TranslationEngine" });

            migrationBuilder.CreateIndex(
                name: "IX_gamewindows_lookup",
                table: "gamewindows",
                columns: new[] { "WindowAddonName", "TranslationLang", "TranslationEngine", "GameVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_questplates_lookup",
                table: "questplates",
                columns: new[] { "QuestName", "TranslationLang", "TranslationEngine" });

            migrationBuilder.CreateIndex(
                name: "IX_stringarraydatas_lookup",
                table: "stringarraydatas",
                columns: new[] { "Type", "TranslationLang", "TranslationEngine" });

            migrationBuilder.CreateIndex(
                name: "IX_talkmessages_lookup",
                table: "talkmessages",
                columns: new[] { "SenderName", "TranslationLang", "TranslationEngine" });

            migrationBuilder.CreateIndex(
                name: "IX_talksubtitlemessages_lookup",
                table: "talksubtitlemessages",
                columns: new[] { "TranslationLang", "TranslationEngine" });

            migrationBuilder.CreateIndex(
                name: "IX_toastmessages_lookup",
                table: "toastmessages",
                columns: new[] { "ToastType", "TranslationLang", "TranslationEngine" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_battletalkmessages_lookup",
                table: "battletalkmessages");

            migrationBuilder.DropIndex(
                name: "IX_gamewindows_lookup",
                table: "gamewindows");

            migrationBuilder.DropIndex(
                name: "IX_questplates_lookup",
                table: "questplates");

            migrationBuilder.DropIndex(
                name: "IX_stringarraydatas_lookup",
                table: "stringarraydatas");

            migrationBuilder.DropIndex(
                name: "IX_talkmessages_lookup",
                table: "talkmessages");

            migrationBuilder.DropIndex(
                name: "IX_talksubtitlemessages_lookup",
                table: "talksubtitlemessages");

            migrationBuilder.DropIndex(
                name: "IX_toastmessages_lookup",
                table: "toastmessages");
        }
    }
}