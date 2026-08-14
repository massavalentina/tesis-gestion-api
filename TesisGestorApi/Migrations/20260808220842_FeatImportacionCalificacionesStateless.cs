using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class FeatImportacionCalificacionesStateless : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "IdCalificacionNueva",
                table: "AuditoriaCalificacionDetalle",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "ResultadoOperacion",
                table: "AuditoriaCalificacionDetalle",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ValorFuenteOficialRaw",
                table: "AuditoriaCalificacionDetalle",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResultadoOperacion",
                table: "AuditoriaCalificacionDetalle");

            migrationBuilder.DropColumn(
                name: "ValorFuenteOficialRaw",
                table: "AuditoriaCalificacionDetalle");

            migrationBuilder.AlterColumn<Guid>(
                name: "IdCalificacionNueva",
                table: "AuditoriaCalificacionDetalle",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
