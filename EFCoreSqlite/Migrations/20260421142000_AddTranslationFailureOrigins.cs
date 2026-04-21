// <copyright file="20260421142000_AddTranslationFailureOrigins.cs" company="lokinmodar">
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
    ///     Adds origin metadata to persisted translation failures without
    ///     changing the exact-lookup key semantics.
    /// </summary>
    [DbContext(typeof(EchoglossianDbContext))]
    [Migration("20260421142000_AddTranslationFailureOrigins")]
    public partial class AddTranslationFailureOrigins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirstSeenOrigin",
                table: "translationfailures",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSeenOrigin",
                table: "translationfailures",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstSeenOrigin",
                table: "translationfailures");

            migrationBuilder.DropColumn(
                name: "LastSeenOrigin",
                table: "translationfailures");
        }
    }
}
