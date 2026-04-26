using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Echoglossian.EFCoreSqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceTextTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "aozactiontexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
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
                    table.PrimaryKey("PK_aozactiontexts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bgcarmyactiontexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
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
                    table.PrimaryKey("PK_bgcarmyactiontexts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "buddyactiontexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
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
                    table.PrimaryKey("PK_buddyactiontexts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "companyactiontexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
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
                    table.PrimaryKey("PK_companyactiontexts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "craftactiontexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
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
                    table.PrimaryKey("PK_craftactiontexts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "eurekamagiaactiontexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
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
                    table.PrimaryKey("PK_eurekamagiaactiontexts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "eventactiontexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
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
                    table.PrimaryKey("PK_eventactiontexts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "generalactiontexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
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
                    table.PrimaryKey("PK_generalactiontexts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mountactiontexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
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
                    table.PrimaryKey("PK_mountactiontexts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "petactiontexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
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
                    table.PrimaryKey("PK_petactiontexts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pvpactiontexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
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
                    table.PrimaryKey("PK_pvpactiontexts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_aozactiontexts_lookup",
                table: "aozactiontexts",
                columns: new[] { "ReferenceId", "TranslationLang", "TranslationEngine", "GameVersion", "SourceContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_bgcarmyactiontexts_lookup",
                table: "bgcarmyactiontexts",
                columns: new[] { "ReferenceId", "TranslationLang", "TranslationEngine", "GameVersion", "SourceContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_buddyactiontexts_lookup",
                table: "buddyactiontexts",
                columns: new[] { "ReferenceId", "TranslationLang", "TranslationEngine", "GameVersion", "SourceContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_companyactiontexts_lookup",
                table: "companyactiontexts",
                columns: new[] { "ReferenceId", "TranslationLang", "TranslationEngine", "GameVersion", "SourceContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_craftactiontexts_lookup",
                table: "craftactiontexts",
                columns: new[] { "ReferenceId", "TranslationLang", "TranslationEngine", "GameVersion", "SourceContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_eurekamagiaactiontexts_lookup",
                table: "eurekamagiaactiontexts",
                columns: new[] { "ReferenceId", "TranslationLang", "TranslationEngine", "GameVersion", "SourceContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_eventactiontexts_lookup",
                table: "eventactiontexts",
                columns: new[] { "ReferenceId", "TranslationLang", "TranslationEngine", "GameVersion", "SourceContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_generalactiontexts_lookup",
                table: "generalactiontexts",
                columns: new[] { "ReferenceId", "TranslationLang", "TranslationEngine", "GameVersion", "SourceContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_mountactiontexts_lookup",
                table: "mountactiontexts",
                columns: new[] { "ReferenceId", "TranslationLang", "TranslationEngine", "GameVersion", "SourceContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_petactiontexts_lookup",
                table: "petactiontexts",
                columns: new[] { "ReferenceId", "TranslationLang", "TranslationEngine", "GameVersion", "SourceContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_pvpactiontexts_lookup",
                table: "pvpactiontexts",
                columns: new[] { "ReferenceId", "TranslationLang", "TranslationEngine", "GameVersion", "SourceContentHash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aozactiontexts");

            migrationBuilder.DropTable(
                name: "bgcarmyactiontexts");

            migrationBuilder.DropTable(
                name: "buddyactiontexts");

            migrationBuilder.DropTable(
                name: "companyactiontexts");

            migrationBuilder.DropTable(
                name: "craftactiontexts");

            migrationBuilder.DropTable(
                name: "eurekamagiaactiontexts");

            migrationBuilder.DropTable(
                name: "eventactiontexts");

            migrationBuilder.DropTable(
                name: "generalactiontexts");

            migrationBuilder.DropTable(
                name: "mountactiontexts");

            migrationBuilder.DropTable(
                name: "petactiontexts");

            migrationBuilder.DropTable(
                name: "pvpactiontexts");
        }
    }
}
