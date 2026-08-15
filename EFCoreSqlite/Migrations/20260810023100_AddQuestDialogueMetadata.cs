using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Echoglossian.EFCoreSqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestDialogueMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "questdialoguemetadata",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuestId = table.Column<uint>(type: "INTEGER", nullable: false),
                    QuestSequence = table.Column<ushort>(type: "INTEGER", nullable: false),
                    SourceLanguageCode = table.Column<string>(type: "TEXT", nullable: false),
                    GameVersion = table.Column<string>(type: "TEXT", nullable: false),
                    QuestSheetId = table.Column<string>(type: "TEXT", nullable: false),
                    QuestTextSheetName = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRowKey = table.Column<string>(type: "TEXT", nullable: false),
                    SourceTextHash = table.Column<string>(type: "TEXT", nullable: false),
                    SourceTextPreview = table.Column<string>(type: "TEXT", nullable: false),
                    SpeakerHint = table.Column<string>(type: "TEXT", nullable: false),
                    AddresseeHint = table.Column<string>(type: "TEXT", nullable: false),
                    SpeakerRoleHint = table.Column<string>(type: "TEXT", nullable: false),
                    AddresseeRoleHint = table.Column<string>(type: "TEXT", nullable: false),
                    Provenance = table.Column<string>(type: "TEXT", nullable: false),
                    ConfidenceTier = table.Column<int>(type: "INTEGER", nullable: false),
                    DerivationVersion = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questdialoguemetadata", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_questdialoguemetadata_lookup",
                table: "questdialoguemetadata",
                columns: new[] { "QuestId", "QuestSequence", "SourceLanguageCode", "GameVersion", "SourceRowKey", "SourceTextHash", "DerivationVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "questdialoguemetadata");
        }
    }
}
