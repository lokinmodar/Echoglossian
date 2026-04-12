// <copyright file="20260412193000_AddQuestSheetFields.cs" company="lokinmodar">
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
    ///     Adds the quest text sheet name and structured translated-row columns to questplates.
    /// </summary>
    [DbContext(typeof(EchoglossianDbContext))]
    [Migration("20260412193000_AddQuestSheetFields")]
    public partial class AddQuestSheetFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QuestTextSheetName",
                table: "questplates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslatedObjectivesAsText",
                table: "questplates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslatedSummariesAsText",
                table: "questplates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemRowsAsText",
                table: "questplates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslatedSystemRowsAsText",
                table: "questplates",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuestTextSheetName",
                table: "questplates");

            migrationBuilder.DropColumn(
                name: "TranslatedObjectivesAsText",
                table: "questplates");

            migrationBuilder.DropColumn(
                name: "TranslatedSummariesAsText",
                table: "questplates");

            migrationBuilder.DropColumn(
                name: "SystemRowsAsText",
                table: "questplates");

            migrationBuilder.DropColumn(
                name: "TranslatedSystemRowsAsText",
                table: "questplates");
        }
    }
}
