using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class FixAsistenciaComputo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TipoLlegadaManianaId",
                table: "Asistencias",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Asistencias_TipoLlegadaManianaId",
                table: "Asistencias",
                column: "TipoLlegadaManianaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Asistencias_TiposAsistencia_TipoLlegadaManianaId",
                table: "Asistencias",
                column: "TipoLlegadaManianaId",
                principalTable: "TiposAsistencia",
                principalColumn: "IdTipo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Asistencias_TiposAsistencia_TipoLlegadaManianaId",
                table: "Asistencias");

            migrationBuilder.DropIndex(
                name: "IX_Asistencias_TipoLlegadaManianaId",
                table: "Asistencias");

            migrationBuilder.DropColumn(
                name: "TipoLlegadaManianaId",
                table: "Asistencias");
        }
    }
}
