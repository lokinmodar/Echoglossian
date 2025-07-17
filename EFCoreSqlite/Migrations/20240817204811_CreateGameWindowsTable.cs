// <copyright file="20240817204811_CreateGameWindowsTable.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Migrations
{
  using Microsoft.EntityFrameworkCore.Migrations;

  /// <inheritdoc />
  public partial class CreateGameWindowsTable : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateTable(
          name: "gamewindows",
          columns: table => new
          {
            Id = table.Column<int>(type: "INTEGER", nullable: false)
                  .Annotation("Sqlite:Autoincrement", true),
            WindowAddonName = table.Column<string>(type: "TEXT", nullable: false),
            OriginalWindowStrings = table.Column<string>(type: "TEXT", nullable: false),
            OriginalWindowStringsLang = table.Column<string>(type: "TEXT", nullable: false),
            TranslatedWindowStrings = table.Column<string>(type: "TEXT", nullable: true),
            TranslationLang = table.Column<string>(type: "TEXT", nullable: true),
            TranslationEngine = table.Column<int>(type: "INTEGER", nullable: false),
            GameVersion = table.Column<string>(type: "TEXT", nullable: false),
            CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
            UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
            RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_gamewindows", x => x.Id);
          });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "gamewindows");
    }
  }
}
