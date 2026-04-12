// <copyright file="20260412200000_AddSourceContentHash.cs" company="lokinmodar">
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
    ///     Adds the SourceContentHash column to questplates for content-aware
    ///     retranslation detection: translations can be reused across game patches
    ///     when the quest text has not changed.
    /// </summary>
    [DbContext(typeof(EchoglossianDbContext))]
    [Migration("20260412200000_AddSourceContentHash")]
    public partial class AddSourceContentHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceContentHash",
                table: "questplates",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceContentHash",
                table: "questplates");
        }
    }
}
