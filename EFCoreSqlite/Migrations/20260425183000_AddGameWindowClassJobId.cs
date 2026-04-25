// <copyright file="20260425183000_AddGameWindowClassJobId.cs" company="lokinmodar">
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
    ///     Adds an optional class/job discriminator to persisted GameWindow
    ///     rows so job-sensitive addons can scope their canonical payloads.
    /// </summary>
    [DbContext(typeof(EchoglossianDbContext))]
    [Migration("20260425183000_AddGameWindowClassJobId")]
    public partial class AddGameWindowClassJobId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "ClassJobId",
                table: "gamewindows",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_gamewindows_classjob_lookup",
                table: "gamewindows",
                columns: new[]
                {
                    "WindowAddonName",
                    "ClassJobId",
                    "TranslationLang",
                    "TranslationEngine",
                    "GameVersion",
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_gamewindows_classjob_lookup",
                table: "gamewindows");

            migrationBuilder.DropColumn(
                name: "ClassJobId",
                table: "gamewindows");
        }
    }
}
