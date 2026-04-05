// <copyright file="20260405153000_AddSelectStringOptions.cs" company="lokinmodar">
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
    [Migration("20260405153000_AddSelectStringOptions")]
    public partial class AddSelectStringOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalOptionsAsText",
                table: "selectstrings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslatedOptionsAsText",
                table: "selectstrings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_selectstrings_lookup",
                table: "selectstrings",
                columns: new[]
                {
                    "OriginalSelectString",
                    "OriginalOptionsAsText",
                    "TranslationLang",
                    "TranslationEngine",
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_selectstrings_lookup",
                table: "selectstrings");

            migrationBuilder.DropColumn(
                name: "OriginalOptionsAsText",
                table: "selectstrings");

            migrationBuilder.DropColumn(
                name: "TranslatedOptionsAsText",
                table: "selectstrings");
        }
    }
}
