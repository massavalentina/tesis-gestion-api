using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaVencimientoContrasena",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiereCambioContrasena",
                table: "Usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimoLogin",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);

            // Seed de roles (idempotente — no falla si ya existen)
            migrationBuilder.Sql(@"
                INSERT INTO ""Roles"" (""IdRol"", ""Nombre"")
                SELECT '11111111-1111-1111-1111-111111111111'::uuid, 'Admin'
                WHERE NOT EXISTS (SELECT 1 FROM ""Roles"" WHERE ""Nombre"" = 'Admin');

                INSERT INTO ""Roles"" (""IdRol"", ""Nombre"")
                SELECT '22222222-2222-2222-2222-222222222222'::uuid, 'Docente'
                WHERE NOT EXISTS (SELECT 1 FROM ""Roles"" WHERE ""Nombre"" = 'Docente');

                INSERT INTO ""Roles"" (""IdRol"", ""Nombre"")
                SELECT '33333333-3333-3333-3333-333333333333'::uuid, 'Preceptor'
                WHERE NOT EXISTS (SELECT 1 FROM ""Roles"" WHERE ""Nombre"" = 'Preceptor');

                INSERT INTO ""Roles"" (""IdRol"", ""Nombre"")
                SELECT '44444444-4444-4444-4444-444444444444'::uuid, 'Equipo Directivo'
                WHERE NOT EXISTS (SELECT 1 FROM ""Roles"" WHERE ""Nombre"" = 'Equipo Directivo');

                INSERT INTO ""Roles"" (""IdRol"", ""Nombre"")
                SELECT '55555555-5555-5555-5555-555555555555'::uuid, 'Secretario'
                WHERE NOT EXISTS (SELECT 1 FROM ""Roles"" WHERE ""Nombre"" = 'Secretario');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaVencimientoContrasena",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "RequiereCambioContrasena",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "UltimoLogin",
                table: "Usuarios");
        }
    }
}
