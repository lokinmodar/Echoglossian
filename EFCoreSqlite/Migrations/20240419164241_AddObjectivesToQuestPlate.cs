// <copyright file="20240419164241_AddObjectivesToQuestPlate.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

#nullable disable

namespace Echoglossian.EFCoreSqlite.Migrations
{
  using Microsoft.EntityFrameworkCore.Migrations;

  /// <inheritdoc />
  public partial class AddObjectivesToQuestPlate : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AddColumn<string>(
          name: "ObjectivesAsText",
          table: "questplates",
          type: "TEXT",
          nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(
          name: "ObjectivesAsText",
          table: "questplates");
    }
  }
}
