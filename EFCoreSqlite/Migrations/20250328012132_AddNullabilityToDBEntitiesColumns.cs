// <copyright file="20250328012132_AddNullabilityToDBEntitiesColumns.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

#nullable disable

namespace Echoglossian.EFCoreSqlite.Migrations
{
  using Microsoft.EntityFrameworkCore.Migrations;

  /// <inheritdoc />
  public partial class AddNullabilityToDBEntitiesColumns : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AlterColumn<string>(
          name: "TranslationLang",
          table: "toastmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "toastmessages",
          type: "INTEGER",
          nullable: true,
          oldClrType: typeof(int),
          oldType: "INTEGER");

      migrationBuilder.AlterColumn<string>(
          name: "ToastType",
          table: "toastmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalToastMessage",
          table: "toastmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalLang",
          table: "toastmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "toastmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(DateTime),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "TranslationLang",
          table: "talksubtitlemessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "talksubtitlemessages",
          type: "INTEGER",
          nullable: true,
          oldClrType: typeof(int),
          oldType: "INTEGER");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalTalkSubtitleMessageLang",
          table: "talksubtitlemessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalTalkSubtitleMessage",
          table: "talksubtitlemessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "talksubtitlemessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(DateTime),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "TranslationLang",
          table: "talkmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "talkmessages",
          type: "INTEGER",
          nullable: true,
          oldClrType: typeof(int),
          oldType: "INTEGER");

      migrationBuilder.AlterColumn<string>(
          name: "SenderName",
          table: "talkmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalTalkMessageLang",
          table: "talkmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalTalkMessage",
          table: "talkmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalSenderNameLang",
          table: "talkmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "talkmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(DateTime),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "selectstrings",
          type: "INTEGER",
          nullable: true,
          oldClrType: typeof(int),
          oldType: "INTEGER");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalSelectStringLang",
          table: "selectstrings",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalSelectString",
          table: "selectstrings",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "selectstrings",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(DateTime),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "TranslationLang",
          table: "questplates",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "questplates",
          type: "INTEGER",
          nullable: true,
          oldClrType: typeof(int),
          oldType: "INTEGER");

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedQuestName",
          table: "questplates",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedQuestMessage",
          table: "questplates",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "QuestName",
          table: "questplates",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "QuestId",
          table: "questplates",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalQuestMessage",
          table: "questplates",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalLang",
          table: "questplates",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "questplates",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(DateTime),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "TranslationLang",
          table: "npcnames",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "npcnames",
          type: "INTEGER",
          nullable: true,
          oldClrType: typeof(int),
          oldType: "INTEGER");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalNpcNameLang",
          table: "npcnames",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalNpcName",
          table: "npcnames",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "npcnames",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(DateTime),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "TranslationLang",
          table: "locationnames",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "locationnames",
          type: "INTEGER",
          nullable: true,
          oldClrType: typeof(int),
          oldType: "INTEGER");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalLocationNameLang",
          table: "locationnames",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalLocationName",
          table: "locationnames",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "locationnames",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(DateTime),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "itemtooltips",
          type: "INTEGER",
          nullable: true,
          oldClrType: typeof(int),
          oldType: "INTEGER");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalItemTooltipLang",
          table: "itemtooltips",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalItemTooltip",
          table: "itemtooltips",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "GameVersion",
          table: "itemtooltips",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "itemtooltips",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(DateTime),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "WindowAddonName",
          table: "gamewindows",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "gamewindows",
          type: "INTEGER",
          nullable: true,
          oldClrType: typeof(int),
          oldType: "INTEGER");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalWindowStringsLang",
          table: "gamewindows",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalWindowStrings",
          table: "gamewindows",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "GameVersion",
          table: "gamewindows",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "gamewindows",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(DateTime),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "TranslationLang",
          table: "battletalkmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "battletalkmessages",
          type: "INTEGER",
          nullable: true,
          oldClrType: typeof(int),
          oldType: "INTEGER");

      migrationBuilder.AlterColumn<string>(
          name: "SenderName",
          table: "battletalkmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalSenderNameLang",
          table: "battletalkmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalBattleTalkMessageLang",
          table: "battletalkmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalBattleTalkMessage",
          table: "battletalkmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "battletalkmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(DateTime),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "actiontooltips",
          type: "INTEGER",
          nullable: true,
          oldClrType: typeof(int),
          oldType: "INTEGER");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalActionTooltipLang",
          table: "actiontooltips",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalActionTooltip",
          table: "actiontooltips",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "GameVersion",
          table: "actiontooltips",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "actiontooltips",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(DateTime),
          oldType: "TEXT");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AlterColumn<string>(
          name: "TranslationLang",
          table: "toastmessages",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "toastmessages",
          type: "INTEGER",
          nullable: false,
          defaultValue: 0,
          oldClrType: typeof(int),
          oldType: "INTEGER",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "ToastType",
          table: "toastmessages",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalToastMessage",
          table: "toastmessages",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalLang",
          table: "toastmessages",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "toastmessages",
          type: "TEXT",
          nullable: false,
          defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
          oldClrType: typeof(DateTime),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "TranslationLang",
          table: "talksubtitlemessages",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "talksubtitlemessages",
          type: "INTEGER",
          nullable: false,
          defaultValue: 0,
          oldClrType: typeof(int),
          oldType: "INTEGER",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalTalkSubtitleMessageLang",
          table: "talksubtitlemessages",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalTalkSubtitleMessage",
          table: "talksubtitlemessages",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "talksubtitlemessages",
          type: "TEXT",
          nullable: false,
          defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
          oldClrType: typeof(DateTime),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "TranslationLang",
          table: "talkmessages",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "talkmessages",
          type: "INTEGER",
          nullable: false,
          defaultValue: 0,
          oldClrType: typeof(int),
          oldType: "INTEGER",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "SenderName",
          table: "talkmessages",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalTalkMessageLang",
          table: "talkmessages",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalTalkMessage",
          table: "talkmessages",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalSenderNameLang",
          table: "talkmessages",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "talkmessages",
          type: "TEXT",
          nullable: false,
          defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
          oldClrType: typeof(DateTime),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "selectstrings",
          type: "INTEGER",
          nullable: false,
          defaultValue: 0,
          oldClrType: typeof(int),
          oldType: "INTEGER",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalSelectStringLang",
          table: "selectstrings",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalSelectString",
          table: "selectstrings",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "selectstrings",
          type: "TEXT",
          nullable: false,
          defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
          oldClrType: typeof(DateTime),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "TranslationLang",
          table: "questplates",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "questplates",
          type: "INTEGER",
          nullable: false,
          defaultValue: 0,
          oldClrType: typeof(int),
          oldType: "INTEGER",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedQuestName",
          table: "questplates",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedQuestMessage",
          table: "questplates",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "QuestName",
          table: "questplates",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "QuestId",
          table: "questplates",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalQuestMessage",
          table: "questplates",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalLang",
          table: "questplates",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "questplates",
          type: "TEXT",
          nullable: false,
          defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
          oldClrType: typeof(DateTime),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "TranslationLang",
          table: "npcnames",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "npcnames",
          type: "INTEGER",
          nullable: false,
          defaultValue: 0,
          oldClrType: typeof(int),
          oldType: "INTEGER",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalNpcNameLang",
          table: "npcnames",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalNpcName",
          table: "npcnames",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "npcnames",
          type: "TEXT",
          nullable: false,
          defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
          oldClrType: typeof(DateTime),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "TranslationLang",
          table: "locationnames",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "locationnames",
          type: "INTEGER",
          nullable: false,
          defaultValue: 0,
          oldClrType: typeof(int),
          oldType: "INTEGER",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalLocationNameLang",
          table: "locationnames",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalLocationName",
          table: "locationnames",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "locationnames",
          type: "TEXT",
          nullable: false,
          defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
          oldClrType: typeof(DateTime),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "itemtooltips",
          type: "INTEGER",
          nullable: false,
          defaultValue: 0,
          oldClrType: typeof(int),
          oldType: "INTEGER",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalItemTooltipLang",
          table: "itemtooltips",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalItemTooltip",
          table: "itemtooltips",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "GameVersion",
          table: "itemtooltips",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "itemtooltips",
          type: "TEXT",
          nullable: false,
          defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
          oldClrType: typeof(DateTime),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "WindowAddonName",
          table: "gamewindows",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "gamewindows",
          type: "INTEGER",
          nullable: false,
          defaultValue: 0,
          oldClrType: typeof(int),
          oldType: "INTEGER",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalWindowStringsLang",
          table: "gamewindows",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalWindowStrings",
          table: "gamewindows",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "GameVersion",
          table: "gamewindows",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "gamewindows",
          type: "TEXT",
          nullable: false,
          defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
          oldClrType: typeof(DateTime),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "TranslationLang",
          table: "battletalkmessages",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "battletalkmessages",
          type: "INTEGER",
          nullable: false,
          defaultValue: 0,
          oldClrType: typeof(int),
          oldType: "INTEGER",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "SenderName",
          table: "battletalkmessages",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalSenderNameLang",
          table: "battletalkmessages",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalBattleTalkMessageLang",
          table: "battletalkmessages",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalBattleTalkMessage",
          table: "battletalkmessages",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "battletalkmessages",
          type: "TEXT",
          nullable: false,
          defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
          oldClrType: typeof(DateTime),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<int>(
          name: "TranslationEngine",
          table: "actiontooltips",
          type: "INTEGER",
          nullable: false,
          defaultValue: 0,
          oldClrType: typeof(int),
          oldType: "INTEGER",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalActionTooltipLang",
          table: "actiontooltips",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalActionTooltip",
          table: "actiontooltips",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "GameVersion",
          table: "actiontooltips",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<DateTime>(
          name: "CreatedDate",
          table: "actiontooltips",
          type: "TEXT",
          nullable: false,
          defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
          oldClrType: typeof(DateTime),
          oldType: "TEXT",
          oldNullable: true);
    }
  }
}
