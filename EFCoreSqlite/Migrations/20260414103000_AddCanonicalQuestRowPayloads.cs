// <copyright file="20260414103000_AddCanonicalQuestRowPayloads.cs" company="lokinmodar">
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
    ///     Adds canonical row-keyed quest payload columns so questplates can persist
    ///     the full resolved quest payload without collapsing repeated source text.
    /// </summary>
    [DbContext(typeof(EchoglossianDbContext))]
    [Migration("20260414103000_AddCanonicalQuestRowPayloads")]
    public partial class AddCanonicalQuestRowPayloads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ObjectiveRowsByKeyAsText",
                table: "questplates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SummaryRowsByKeyAsText",
                table: "questplates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemRowsByKeyAsText",
                table: "questplates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslatedObjectiveRowsByKeyAsText",
                table: "questplates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslatedSummaryRowsByKeyAsText",
                table: "questplates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslatedSystemRowsByKeyAsText",
                table: "questplates",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ObjectiveRowsByKeyAsText",
                table: "questplates");

            migrationBuilder.DropColumn(
                name: "SummaryRowsByKeyAsText",
                table: "questplates");

            migrationBuilder.DropColumn(
                name: "SystemRowsByKeyAsText",
                table: "questplates");

            migrationBuilder.DropColumn(
                name: "TranslatedObjectiveRowsByKeyAsText",
                table: "questplates");

            migrationBuilder.DropColumn(
                name: "TranslatedSummaryRowsByKeyAsText",
                table: "questplates");

            migrationBuilder.DropColumn(
                name: "TranslatedSystemRowsByKeyAsText",
                table: "questplates");
        }
    }
}
