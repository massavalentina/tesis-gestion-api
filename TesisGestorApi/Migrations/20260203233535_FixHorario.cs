using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class FixHorario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE ""Horarios"" 
          ALTER COLUMN ""HorarioEntrada"" TYPE interval 
          USING ""HorarioEntrada""::time::interval;");

            migrationBuilder.Sql(
                @"ALTER TABLE ""Horarios"" 
          ALTER COLUMN ""HorarioSalida"" TYPE interval 
          USING ""HorarioSalida""::time::interval;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "HorarioSalida",
                table: "Horarios",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(TimeSpan),
                oldType: "interval");

            migrationBuilder.AlterColumn<DateTime>(
                name: "HorarioEntrada",
                table: "Horarios",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(TimeSpan),
                oldType: "interval");
        }
    }
}
