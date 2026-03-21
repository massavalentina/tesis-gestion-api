using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class HorarioEfectivoClaseDictada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "HorarioEntradaEfectiva",
                table: "ClasesDictadas",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "HorarioSalidaEfectiva",
                table: "ClasesDictadas",
                type: "interval",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HorarioEntradaEfectiva",
                table: "ClasesDictadas");

            migrationBuilder.DropColumn(
                name: "HorarioSalidaEfectiva",
                table: "ClasesDictadas");
        }
    }
}
