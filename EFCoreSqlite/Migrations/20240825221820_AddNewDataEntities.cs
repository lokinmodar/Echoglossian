// <copyright file="20240825221820_AddNewDataEntities.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

#nullable disable

namespace Echoglossian.EFCoreSqlite.Migrations
{
  using Microsoft.EntityFrameworkCore.Migrations;

  /// <inheritdoc />
  public partial class AddNewDataEntities : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateTable(
          name: "actiontooltips",
          columns: table => new
          {
            Id = table.Column<int>(type: "INTEGER", nullable: false)
                  .Annotation("Sqlite:Autoincrement", true),
            OriginalActionTooltip = table.Column<string>(type: "TEXT", nullable: false),
            OriginalActionTooltipLang = table.Column<string>(type: "TEXT", nullable: false),
            TranslatedActionTooltip = table.Column<string>(type: "TEXT", nullable: true),
            TranslationLang = table.Column<string>(type: "TEXT", nullable: true),
            TranslationEngine = table.Column<int>(type: "INTEGER", nullable: false),
            GameVersion = table.Column<string>(type: "TEXT", nullable: false),
            CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
            UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
            RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_actiontooltips", x => x.Id);
          });

      migrationBuilder.CreateTable(
          name: "itemtooltips",
          columns: table => new
          {
            Id = table.Column<int>(type: "INTEGER", nullable: false)
                  .Annotation("Sqlite:Autoincrement", true),
            OriginalItemTooltip = table.Column<string>(type: "TEXT", nullable: false),
            OriginalItemTooltipLang = table.Column<string>(type: "TEXT", nullable: false),
            TranslatedItemTooltip = table.Column<string>(type: "TEXT", nullable: true),
            TranslationLang = table.Column<string>(type: "TEXT", nullable: true),
            TranslationEngine = table.Column<int>(type: "INTEGER", nullable: false),
            GameVersion = table.Column<string>(type: "TEXT", nullable: false),
            CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
            UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
            RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_itemtooltips", x => x.Id);
          });

      migrationBuilder.CreateTable(
          name: "selectstrings",
          columns: table => new
          {
            Id = table.Column<int>(type: "INTEGER", nullable: false)
                  .Annotation("Sqlite:Autoincrement", true),
            OriginalSelectString = table.Column<string>(type: "TEXT", nullable: false),
            OriginalSelectStringLang = table.Column<string>(type: "TEXT", nullable: false),
            TranslatedSelectString = table.Column<string>(type: "TEXT", nullable: true),
            TranslationLang = table.Column<string>(type: "TEXT", nullable: true),
            TranslationEngine = table.Column<int>(type: "INTEGER", nullable: false),
            CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
            UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
            RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_selectstrings", x => x.Id);
          });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "actiontooltips");

      migrationBuilder.DropTable(
          name: "itemtooltips");

      migrationBuilder.DropTable(
          name: "selectstrings");
    }
  }
}
