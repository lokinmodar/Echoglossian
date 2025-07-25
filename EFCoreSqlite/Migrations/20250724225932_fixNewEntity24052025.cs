using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Echoglossian.EFCoreSqlite.Migrations
{
    /// <inheritdoc />
    public partial class fixNewEntity24052025 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TranslationEngine",
                table: "stringarraydatas",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TranslationEngine",
                table: "stringarraydatas",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
