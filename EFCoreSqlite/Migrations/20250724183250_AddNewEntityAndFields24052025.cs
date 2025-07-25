using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Echoglossian.EFCoreSqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddNewEntityAndFields24052025 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RTLLangTranslationImageData",
                table: "talkmessages",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RTLLangTranslationImageData",
                table: "battletalkmessages",
                type: "BLOB",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RTLLangTranslationImageData",
                table: "talkmessages");

            migrationBuilder.DropColumn(
                name: "RTLLangTranslationImageData",
                table: "battletalkmessages");
        }
    }
}
