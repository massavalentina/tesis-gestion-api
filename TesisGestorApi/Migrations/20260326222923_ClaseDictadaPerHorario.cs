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
            // Limpieza de datos: los registros existentes no son compatibles con el nuevo esquema por-slot.
            migrationBuilder.Sql("DELETE FROM \"AsistenciasPorEspacio\"");
            migrationBuilder.Sql("DELETE FROM \"ClasesDictadas\"");

            // ADD COLUMN IF NOT EXISTS — idempotente por si la columna ya fue creada en un apply anterior.
            migrationBuilder.Sql(@"ALTER TABLE ""ClasesDictadas"" ADD COLUMN IF NOT EXISTS ""IdHorario"" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            // CREATE INDEX IF NOT EXISTS
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ClasesDictadas_IdHorario_Fecha"" ON ""ClasesDictadas"" (""IdHorario"", ""Fecha"");");

            // ADD FK solo si no existe
            migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_ClasesDictadas_Horarios_IdHorario'
    ) THEN
        ALTER TABLE ""ClasesDictadas""
        ADD CONSTRAINT ""FK_ClasesDictadas_Horarios_IdHorario""
        FOREIGN KEY (""IdHorario"") REFERENCES ""Horarios""(""IdHorario"") ON DELETE RESTRICT;
    END IF;
END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClasesDictadas_Horarios_IdHorario",
                table: "ClasesDictadas");

            migrationBuilder.DropIndex(
                name: "IX_ClasesDictadas_IdHorario_Fecha",
                table: "ClasesDictadas");

            migrationBuilder.DropColumn(
                name: "IdHorario",
                table: "ClasesDictadas");
        }
    }
}
