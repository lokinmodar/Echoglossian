using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Echoglossian.EFCoreSqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmCapabilityMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "llmmodelcapabilityobservations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Engine = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderScope = table.Column<string>(type: "TEXT", nullable: false),
                    EndpointScope = table.Column<string>(type: "TEXT", nullable: false),
                    ModelId = table.Column<string>(type: "TEXT", nullable: false),
                    ParameterName = table.Column<string>(type: "TEXT", nullable: false),
                    StatusCode = table.Column<int>(type: "INTEGER", nullable: false),
                    ProviderErrorCode = table.Column<string>(type: "TEXT", nullable: false),
                    MessageExcerpt = table.Column<string>(type: "TEXT", nullable: false),
                    ObservedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llmmodelcapabilityobservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "llmmodelcapabilityrules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Engine = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderScope = table.Column<string>(type: "TEXT", nullable: false),
                    EndpointScope = table.Column<string>(type: "TEXT", nullable: false),
                    MatchType = table.Column<string>(type: "TEXT", nullable: false),
                    MatchValue = table.Column<string>(type: "TEXT", nullable: false),
                    ParameterName = table.Column<string>(type: "TEXT", nullable: false),
                    SupportState = table.Column<string>(type: "TEXT", nullable: false),
                    MinValue = table.Column<float>(type: "REAL", nullable: true),
                    MaxValue = table.Column<float>(type: "REAL", nullable: true),
                    AllowedEnumValuesJson = table.Column<string>(type: "TEXT", nullable: false),
                    OmitWhenDefaultOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    Confidence = table.Column<string>(type: "TEXT", nullable: false),
                    ObservedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llmmodelcapabilityrules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_llmmodelcapabilityrules_lookup",
                table: "llmmodelcapabilityrules",
                columns: new[] { "Engine", "ProviderScope", "EndpointScope", "MatchType", "MatchValue", "ParameterName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "llmmodelcapabilityobservations");

            migrationBuilder.DropTable(
                name: "llmmodelcapabilityrules");
        }
    }
}
