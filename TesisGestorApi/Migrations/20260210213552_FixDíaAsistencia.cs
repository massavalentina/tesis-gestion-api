using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class FixDíaAsistencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateOnly>(
                name: "Fecha",
                table: "Asistencias",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "HoraEntradaManana",
                table: "Asistencias",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "HoraEntradaTarde",
                table: "Asistencias",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "HoraSalidaManana",
                table: "Asistencias",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "HoraSalidaTarde",
                table: "Asistencias",
                type: "interval",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HoraEntradaManana",
                table: "Asistencias");

            migrationBuilder.DropColumn(
                name: "HoraEntradaTarde",
                table: "Asistencias");

            migrationBuilder.DropColumn(
                name: "HoraSalidaManana",
                table: "Asistencias");

            migrationBuilder.DropColumn(
                name: "HoraSalidaTarde",
                table: "Asistencias");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Fecha",
                table: "Asistencias",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");
        }
    }
}
