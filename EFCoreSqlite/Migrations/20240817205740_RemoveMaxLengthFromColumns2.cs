// <copyright file="20240817205740_RemoveMaxLengthFromColumns2.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

#nullable disable

namespace Echoglossian.EFCoreSqlite.Migrations
{
  using Microsoft.EntityFrameworkCore.Migrations;

  public partial class RemoveMaxLengthFromColumns2 : Migration
  {
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      // Modifications for talkmessages table
      migrationBuilder.AlterColumn<string>(
          name: "SenderName",
          table: "talkmessages",
          type: "TEXT",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldMaxLength: 100);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalTalkMessage",
          table: "talkmessages",
          type: "TEXT",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldMaxLength: 400);

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedSenderName",
          table: "talkmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldMaxLength: 100,
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedTalkMessage",
          table: "talkmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldMaxLength: 400,
          oldNullable: true);

      // Modifications for npcnames table
      migrationBuilder.AlterColumn<string>(
          name: "OriginalNpcName",
          table: "npcnames",
          type: "TEXT",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldMaxLength: 400);

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedNpcName",
          table: "npcnames",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldMaxLength: 400,
          oldNullable: true);

      // Modifications for locationnames table
      migrationBuilder.AlterColumn<string>(
          name: "OriginalLocationName",
          table: "locationnames",
          type: "TEXT",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldMaxLength: 400);

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedLocationName",
          table: "locationnames",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      // Modifications for talksubtitlemessages table
      migrationBuilder.AlterColumn<string>(
          name: "OriginalTalkSubtitleMessage",
          table: "talksubtitlemessages",
          type: "TEXT",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldMaxLength: 400);

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedTalkSubtitleMessage",
          table: "talksubtitlemessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldMaxLength: 400,
          oldNullable: true);

      // Modifications for toastmessages table
      migrationBuilder.AlterColumn<string>(
          name: "ToastType",
          table: "toastmessages",
          type: "TEXT",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldMaxLength: 40);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalToastMessage",
          table: "toastmessages",
          type: "TEXT",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldMaxLength: 200);

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedToastMessage",
          table: "toastmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldMaxLength: 200,
          oldNullable: true);

      // Modifications for battletalkmessages table
      migrationBuilder.AlterColumn<string>(
          name: "SenderName",
          table: "battletalkmessages",
          type: "TEXT",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldMaxLength: 100);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalBattleTalkMessage",
          table: "battletalkmessages",
          type: "TEXT",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldMaxLength: 400);

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedSenderName",
          table: "battletalkmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldMaxLength: 100,
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedBattleTalkMessage",
          table: "battletalkmessages",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldMaxLength: 400,
          oldNullable: true);

      // Modifications for questplates table
      migrationBuilder.AlterColumn<string>(
          name: "QuestName",
          table: "questplates",
          type: "TEXT",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldMaxLength: 200);

      migrationBuilder.AlterColumn<string>(
          name: "OriginalQuestMessage",
          table: "questplates",
          type: "TEXT",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldMaxLength: 2500);

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedQuestName",
          table: "questplates",
          type: "TEXT",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldMaxLength: 200);

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedQuestMessage",
          table: "questplates",
          type: "TEXT",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldMaxLength: 2500);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
      // Revert changes for talkmessages table
      migrationBuilder.AlterColumn<string>(
          name: "SenderName",
          table: "talkmessages",
          type: "TEXT",
          maxLength: 100,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalTalkMessage",
          table: "talkmessages",
          type: "TEXT",
          maxLength: 400,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedSenderName",
          table: "talkmessages",
          type: "TEXT",
          maxLength: 100,
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedTalkMessage",
          table: "talkmessages",
          type: "TEXT",
          maxLength: 400,
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      // Revert changes for npcnames table
      migrationBuilder.AlterColumn<string>(
          name: "OriginalNpcName",
          table: "npcnames",
          type: "TEXT",
          maxLength: 400,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedNpcName",
          table: "npcnames",
          type: "TEXT",
          maxLength: 400,
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      // Revert changes for locationnames table
      migrationBuilder.AlterColumn<string>(
          name: "OriginalLocationName",
          table: "locationnames",
          type: "TEXT",
          maxLength: 400,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedLocationName",
          table: "locationnames",
          type: "TEXT",
          maxLength: 400,
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      // Revert changes for talksubtitlemessages table
      migrationBuilder.AlterColumn<string>(
          name: "OriginalTalkSubtitleMessage",
          table: "talksubtitlemessages",
          type: "TEXT",
          maxLength: 400,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedTalkSubtitleMessage",
          table: "talksubtitlemessages",
          type: "TEXT",
          maxLength: 400,
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      // Revert changes for toastmessages table
      migrationBuilder.AlterColumn<string>(
          name: "ToastType",
          table: "toastmessages",
          type: "TEXT",
          maxLength: 40,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalToastMessage",
          table: "toastmessages",
          type: "TEXT",
          maxLength: 200,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedToastMessage",
          table: "toastmessages",
          type: "TEXT",
          maxLength: 200,
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      // Revert changes for battletalkmessages table
      migrationBuilder.AlterColumn<string>(
          name: "SenderName",
          table: "battletalkmessages",
          type: "TEXT",
          maxLength: 100,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalBattleTalkMessage",
          table: "battletalkmessages",
          type: "TEXT",
          maxLength: 400,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedSenderName",
          table: "battletalkmessages",
          type: "TEXT",
          maxLength: 100,
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedBattleTalkMessage",
          table: "battletalkmessages",
          type: "TEXT",
          maxLength: 400,
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);

      // Revert changes for questplates table
      migrationBuilder.AlterColumn<string>(
          name: "QuestName",
          table: "questplates",
          type: "TEXT",
          maxLength: 200,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "OriginalQuestMessage",
          table: "questplates",
          type: "TEXT",
          maxLength: 2500,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedQuestName",
          table: "questplates",
          type: "TEXT",
          maxLength: 200,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT");

      migrationBuilder.AlterColumn<string>(
          name: "TranslatedQuestMessage",
          table: "questplates",
          type: "TEXT",
          maxLength: 2500,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "TEXT");
    }
  }
}
