using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class FixTutores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsPrincipal",
                table: "Tutores");

            migrationBuilder.AddColumn<bool>(
                name: "EsPrincipal",
                table: "TutorEstudiante",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsPrincipal",
                table: "TutorEstudiante");

            migrationBuilder.AddColumn<bool>(
                name: "EsPrincipal",
                table: "Tutores",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
