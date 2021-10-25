// <copyright file="20210922032558_EchoglossianDB.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Echoglossian.EFCoreSqlite.Migrations
{
  public partial class EchoglossianDB : Migration
  {
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateTable(
          name: "battletalkmessages",
          columns: table => new
          {
            Id = table.Column<int>(type: "INTEGER", nullable: false)
                  .Annotation("Sqlite:Autoincrement", true),
            SenderName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
            OriginalBattleTalkMessage = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
            OriginalSenderNameLang = table.Column<string>(type: "TEXT", nullable: false),
            OriginalBattleTalkMessageLang = table.Column<string>(type: "TEXT", nullable: false),
            TranslatedSenderName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
            TranslatedBattleTalkMessage = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
            TranslationLang = table.Column<string>(type: "TEXT", nullable: false),
            TranslationEngine = table.Column<int>(type: "INTEGER", nullable: false),
            CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
            UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
            RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_battletalkmessages", x => x.Id);
          });

      migrationBuilder.CreateTable(
          name: "questplates",
          columns: table => new
          {
            Id = table.Column<int>(type: "INTEGER", nullable: false)
                  .Annotation("Sqlite:Autoincrement", true),
            QuestId = table.Column<string>(type: "TEXT", nullable: false),
            QuestName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
            OriginalQuestMessage = table.Column<string>(type: "TEXT", maxLength: 2500, nullable: false),
            OriginalLang = table.Column<string>(type: "TEXT", nullable: false),
            TranslatedQuestName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
            TranslatedQuestMessage = table.Column<string>(type: "TEXT", maxLength: 2500, nullable: false),
            TranslationLang = table.Column<string>(type: "TEXT", nullable: false),
            TranslationEngine = table.Column<int>(type: "INTEGER", nullable: false),
            CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
            UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
            RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_questplates", x => x.Id);
          });

      migrationBuilder.CreateTable(
          name: "talkmessages",
          columns: table => new
          {
            Id = table.Column<int>(type: "INTEGER", nullable: false)
                  .Annotation("Sqlite:Autoincrement", true),
            SenderName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
            OriginalTalkMessage = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
            OriginalSenderNameLang = table.Column<string>(type: "TEXT", nullable: false),
            OriginalTalkMessageLang = table.Column<string>(type: "TEXT", nullable: false),
            TranslatedSenderName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
            TranslatedTalkMessage = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
            TranslationLang = table.Column<string>(type: "TEXT", nullable: false),
            TranslationEngine = table.Column<int>(type: "INTEGER", nullable: false),
            CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
            UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
            RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_talkmessages", x => x.Id);
          });

      migrationBuilder.CreateTable(
          name: "toastmessages",
          columns: table => new
          {
            Id = table.Column<int>(type: "INTEGER", nullable: false)
                  .Annotation("Sqlite:Autoincrement", true),
            ToastType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
            OriginalToastMessage = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
            OriginalLang = table.Column<string>(type: "TEXT", nullable: false),
            TranslatedToastMessage = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
            TranslationLang = table.Column<string>(type: "TEXT", nullable: false),
            TranslationEngine = table.Column<int>(type: "INTEGER", nullable: false),
            CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
            UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
            RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_toastmessages", x => x.Id);
          });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "battletalkmessages");

      migrationBuilder.DropTable(
          name: "questplates");

      migrationBuilder.DropTable(
          name: "talkmessages");

      migrationBuilder.DropTable(
          name: "toastmessages");
    }
  }
}
