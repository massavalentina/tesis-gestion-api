using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class AddDomicilio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Domicilio",
                table: "Tutores",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Domicilio",
                table: "Estudiantes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Domicilio",
                table: "Tutores");

            migrationBuilder.DropColumn(
                name: "Domicilio",
                table: "Estudiantes");
        }
    }
}
