using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class ClaseDictadaPerHorario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Limpieza de datos existentes: los registros de ClaseDictadas/AsistenciasPorEspacio
            // no pueden migrarse automáticamente al nuevo esquema por-slot (IdHorario).
            migrationBuilder.Sql("DELETE FROM \"AsistenciasPorEspacio\"");
            migrationBuilder.Sql("DELETE FROM \"ClasesDictadas\"");

            migrationBuilder.DropIndex(
                name: "IX_ClasesDictadas_IdEC_Fecha",
                table: "ClasesDictadas");

            migrationBuilder.AddColumn<Guid>(
                name: "IdHorario",
                table: "ClasesDictadas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_ClasesDictadas_IdEC",
                table: "ClasesDictadas",
                column: "IdEC");

            migrationBuilder.CreateIndex(
                name: "IX_ClasesDictadas_IdHorario_Fecha",
                table: "ClasesDictadas",
                columns: new[] { "IdHorario", "Fecha" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ClasesDictadas_Horarios_IdHorario",
                table: "ClasesDictadas",
                column: "IdHorario",
                principalTable: "Horarios",
                principalColumn: "IdHorario",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClasesDictadas_Horarios_IdHorario",
                table: "ClasesDictadas");

            migrationBuilder.DropIndex(
                name: "IX_ClasesDictadas_IdEC",
                table: "ClasesDictadas");

            migrationBuilder.DropIndex(
                name: "IX_ClasesDictadas_IdHorario_Fecha",
                table: "ClasesDictadas");

            migrationBuilder.DropColumn(
                name: "IdHorario",
                table: "ClasesDictadas");

            migrationBuilder.CreateIndex(
                name: "IX_ClasesDictadas_IdEC_Fecha",
                table: "ClasesDictadas",
                columns: new[] { "IdEC", "Fecha" });
        }
    }
}
