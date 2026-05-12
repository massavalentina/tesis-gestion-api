using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class CleanUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EsDelegado ya existe en la DB en algunos entornos (merge anterior)
            migrationBuilder.Sql(@"
                ALTER TABLE ""Preceptores""
                ADD COLUMN IF NOT EXISTS ""EsDelegado"" boolean NOT NULL DEFAULT false;");

            // Columnas huérfanas de auth anterior: RefreshToken vivía en Usuario,
            // ahora tiene su propia tabla RefreshTokens
            migrationBuilder.Sql(@"
                ALTER TABLE ""Usuarios""
                DROP COLUMN IF EXISTS ""RefreshToken"",
                DROP COLUMN IF EXISTS ""RefreshTokenVencimiento"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsDelegado",
                table: "Preceptores");

            migrationBuilder.AddColumn<string>(
                name: "RefreshToken",
                table: "Usuarios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshTokenVencimiento",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
