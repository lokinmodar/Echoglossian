// <copyright file="20260416031500_AddCanonicalStringArrayPayloadFields.cs" company="lokinmodar">
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
    ///     Adds canonical structured payload fields to
    ///     <c>stringarraydatas</c> so future DB-first schema-driven runtimes
    ///     can persist semantic context without relying on the legacy blob-only
    ///     contract.
    /// </summary>
    [DbContext(typeof(EchoglossianDbContext))]
    [Migration("20260416031500_AddCanonicalStringArrayPayloadFields")]
    public partial class AddCanonicalStringArrayPayloadFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContextKey",
                table: "stringarraydatas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalStructuredPayload",
                table: "stringarraydatas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchemaVersion",
                table: "stringarraydatas",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceContentHash",
                table: "stringarraydatas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslatedStructuredPayload",
                table: "stringarraydatas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_stringarraydatas_context_lookup",
                table: "stringarraydatas",
                columns: new[]
                {
                    "Type",
                    "ContextKey",
                    "TranslationLang",
                    "TranslationEngine",
                    "GameVersion",
                    "SourceContentHash",
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stringarraydatas_context_lookup",
                table: "stringarraydatas");

            migrationBuilder.DropColumn(
                name: "ContextKey",
                table: "stringarraydatas");

            migrationBuilder.DropColumn(
                name: "OriginalStructuredPayload",
                table: "stringarraydatas");

            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "stringarraydatas");

            migrationBuilder.DropColumn(
                name: "SourceContentHash",
                table: "stringarraydatas");

            migrationBuilder.DropColumn(
                name: "TranslatedStructuredPayload",
                table: "stringarraydatas");
        }
    }
}
