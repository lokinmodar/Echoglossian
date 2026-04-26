using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Echoglossian.EFCoreSqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMainCommandTextTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "maincommandtexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IconId = table.Column<uint>(type: "INTEGER", nullable: true),
                    CategoryId = table.Column<uint>(type: "INTEGER", nullable: true),
                    MainCommandCategoryId = table.Column<uint>(type: "INTEGER", nullable: true),
                    Unknown0 = table.Column<uint>(type: "INTEGER", nullable: true),
                    SortId = table.Column<uint>(type: "INTEGER", nullable: true),
                    ReferenceId = table.Column<uint>(type: "INTEGER", nullable: false),
                    OriginalName = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalDescription = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalLang = table.Column<string>(type: "TEXT", nullable: true),
                    TranslatedName = table.Column<string>(type: "TEXT", nullable: true),
                    TranslatedDescription = table.Column<string>(type: "TEXT", nullable: true),
                    TranslationLang = table.Column<string>(type: "TEXT", nullable: true),
                    TranslationEngine = table.Column<int>(type: "INTEGER", nullable: true),
                    GameVersion = table.Column<string>(type: "TEXT", nullable: true),
                    SourceContentHash = table.Column<string>(type: "TEXT", nullable: true),
                    CanonicalPayloadAsText = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maincommandtexts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_maincommandtexts_lookup",
                table: "maincommandtexts",
                columns: new[] { "ReferenceId", "TranslationLang", "TranslationEngine", "GameVersion", "SourceContentHash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "maincommandtexts");
        }
    }
}
