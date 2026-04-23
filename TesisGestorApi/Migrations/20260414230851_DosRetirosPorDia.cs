using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class DosRetirosPorDia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Permite dos retiros por día (uno por turno):
            // Reemplaza la restricción única sobre IdAsistencia por una
            // restricción única compuesta sobre (IdAsistencia, Turno).
            migrationBuilder.DropIndex(
                name: "IX_RetirosAnticipados_IdAsistencia",
                table: "RetirosAnticipados");

            migrationBuilder.CreateIndex(
                name: "IX_RetirosAnticipados_IdAsistencia_Turno",
                table: "RetirosAnticipados",
                columns: new[] { "IdAsistencia", "Turno" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RetirosAnticipados_IdAsistencia_Turno",
                table: "RetirosAnticipados");

            migrationBuilder.CreateIndex(
                name: "IX_RetirosAnticipados_IdAsistencia",
                table: "RetirosAnticipados",
                column: "IdAsistencia",
                unique: true);
        }
    }
}
