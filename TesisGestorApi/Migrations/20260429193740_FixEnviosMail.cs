using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class FixEnviosMail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AsistenciaUmbralNotificacion_Estado_ProximoEnvioUtc",
                table: "AsistenciaUmbralNotificacion");

            migrationBuilder.DropColumn(
                name: "CantidadEnviados",
                table: "AsistenciaUmbralNotificacion");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "AsistenciaUmbralNotificacion");

            migrationBuilder.DropColumn(
                name: "ProximoEnvioUtc",
                table: "AsistenciaUmbralNotificacion");

            migrationBuilder.DropColumn(
                name: "UltimoEnvioUtc",
                table: "AsistenciaUmbralNotificacion");

            migrationBuilder.DropColumn(
                name: "UltimoError",
                table: "AsistenciaUmbralNotificacion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CantidadEnviados",
                table: "AsistenciaUmbralNotificacion",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "AsistenciaUmbralNotificacion",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProximoEnvioUtc",
                table: "AsistenciaUmbralNotificacion",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimoEnvioUtc",
                table: "AsistenciaUmbralNotificacion",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UltimoError",
                table: "AsistenciaUmbralNotificacion",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AsistenciaUmbralNotificacion_Estado_ProximoEnvioUtc",
                table: "AsistenciaUmbralNotificacion",
                columns: new[] { "Estado", "ProximoEnvioUtc" });
        }
    }
}
