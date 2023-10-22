using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Echoglossian.EFCoreSqlite.Migrations
{
  /// <inheritdoc />
  public partial class EchoglossianDBupd : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateTable(
          name: "locationnames",
          columns: table => new
          {
            Id = table.Column<int>(type: "INTEGER", nullable: false)
                  .Annotation("Sqlite:Autoincrement", true),
            OriginalLocationName = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
            OriginalLocationNameLang = table.Column<string>(type: "TEXT", nullable: false),
            TranslatedLocationName = table.Column<string>(type: "TEXT", nullable: true),
            TranslationLang = table.Column<string>(type: "TEXT", nullable: false),
            TranslationEngine = table.Column<int>(type: "INTEGER", nullable: false),
            CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
            UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
            RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_locationnames", x => x.Id);
          });

      migrationBuilder.CreateTable(
          name: "npcnames",
          columns: table => new
          {
            Id = table.Column<int>(type: "INTEGER", nullable: false)
                  .Annotation("Sqlite:Autoincrement", true),
            OriginalNpcName = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
            OriginalNpcNameLang = table.Column<string>(type: "TEXT", nullable: false),
            TranslatedNpcName = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
            TranslationLang = table.Column<string>(type: "TEXT", nullable: false),
            TranslationEngine = table.Column<int>(type: "INTEGER", nullable: false),
            CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
            UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
            RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_npcnames", x => x.Id);
          });

      migrationBuilder.CreateTable(
          name: "talksubtitlemessages",
          columns: table => new
          {
            Id = table.Column<int>(type: "INTEGER", nullable: false)
                  .Annotation("Sqlite:Autoincrement", true),
            OriginalTalkSubtitleMessage = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
            OriginalTalkSubtitleMessageLang = table.Column<string>(type: "TEXT", nullable: false),
            TranslatedTalkSubtitleMessage = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
            TranslationLang = table.Column<string>(type: "TEXT", nullable: false),
            TranslationEngine = table.Column<int>(type: "INTEGER", nullable: false),
            CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
            UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
            RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_talksubtitlemessages", x => x.Id);
          });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "locationnames");

      migrationBuilder.DropTable(
          name: "npcnames");

      migrationBuilder.DropTable(
          name: "talksubtitlemessages");
    }
  }
}
