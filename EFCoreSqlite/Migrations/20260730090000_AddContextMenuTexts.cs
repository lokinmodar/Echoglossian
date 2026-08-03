using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Echoglossian.EFCoreSqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddContextMenuTexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contextmenutexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AddonName = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalTextsAsText = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalLang = table.Column<string>(type: "TEXT", nullable: true),
                    TranslatedTextsAsText = table.Column<string>(type: "TEXT", nullable: true),
                    TranslationLang = table.Column<string>(type: "TEXT", nullable: true),
                    TranslationEngine = table.Column<int>(type: "INTEGER", nullable: true),
                    GameVersion = table.Column<string>(type: "TEXT", nullable: true),
                    SourceContentHash = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contextmenutexts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contextmenutexts_lookup",
                table: "contextmenutexts",
                columns: new[]
                {
                    "AddonName",
                    "TranslationLang",
                    "TranslationEngine",
                    "GameVersion",
                    "SourceContentHash"
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contextmenutexts");
        }
    }
}
