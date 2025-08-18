// <copyright file="20250724183250_AddNewEntityAndFields24052025.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Echoglossian.EFCoreSqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddNewEntityAndFields24052025 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RTLLangTranslationImageData",
                table: "talkmessages",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RTLLangTranslationImageData",
                table: "battletalkmessages",
                type: "BLOB",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RTLLangTranslationImageData",
                table: "talkmessages");

            migrationBuilder.DropColumn(
                name: "RTLLangTranslationImageData",
                table: "battletalkmessages");
        }
    }
}
