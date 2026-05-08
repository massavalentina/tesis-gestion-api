using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class FeatUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotente: usa IF EXISTS / IF NOT EXISTS para tolerar estados parciales previos.

            // ── Drops ────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"ALTER TABLE ""EspaciosCurriculares"" DROP CONSTRAINT IF EXISTS ""FK_EspaciosCurriculares_Docentes_IdDocente"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Preceptores""          DROP CONSTRAINT IF EXISTS ""FK_Preceptores_Cursos_CursoIdCurso"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Preceptores_CursoIdCurso"";");

            migrationBuilder.Sql(@"ALTER TABLE ""Usuarios""   DROP COLUMN IF EXISTS ""Verificado"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Preceptores"" DROP COLUMN IF EXISTS ""Apellido"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Preceptores"" DROP COLUMN IF EXISTS ""CursoIdCurso"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Preceptores"" DROP COLUMN IF EXISTS ""Documento"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Preceptores"" DROP COLUMN IF EXISTS ""IdCurso"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Preceptores"" DROP COLUMN IF EXISTS ""Nombre"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Docentes""    DROP COLUMN IF EXISTS ""Apellido"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Docentes""    DROP COLUMN IF EXISTS ""Documento"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Docentes""    DROP COLUMN IF EXISTS ""Email"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Docentes""    DROP COLUMN IF EXISTS ""Nombre"";");

            // ── Rename Mail → Nombre (solo si Mail todavía existe) ───────────────────────
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'Usuarios' AND column_name = 'Mail'
                    ) THEN
                        ALTER TABLE ""Usuarios"" RENAME COLUMN ""Mail"" TO ""Nombre"";
                    END IF;
                END $$;");

            // ── Limpiar datos incompatibles con el nuevo esquema ─────────────────────────
            // IdDocente debe ser nullable antes de nullear los EC, y los EC deben
            // quedar sin referencia antes de borrar Docentes para que el FK final sea válido.
            migrationBuilder.Sql(@"ALTER TABLE ""EspaciosCurriculares"" ALTER COLUMN ""IdDocente"" DROP NOT NULL;");
            migrationBuilder.Sql(@"UPDATE ""EspaciosCurriculares"" SET ""IdDocente"" = NULL;");

            migrationBuilder.Sql(@"DELETE FROM ""UsuariosRoles"";");
            migrationBuilder.Sql(@"DELETE FROM ""Docentes"";");
            migrationBuilder.Sql(@"DELETE FROM ""Preceptores"";");
            migrationBuilder.Sql(@"DELETE FROM ""Usuarios"";");

            // ── Nuevas columnas en Usuarios ──────────────────────────────────────────────
            migrationBuilder.Sql(@"ALTER TABLE ""Usuarios"" ADD COLUMN IF NOT EXISTS ""Apellido""          text                     NOT NULL DEFAULT '';");
            migrationBuilder.Sql(@"ALTER TABLE ""Usuarios"" ADD COLUMN IF NOT EXISTS ""BloqueadoHasta""    timestamp with time zone;");
            migrationBuilder.Sql(@"ALTER TABLE ""Usuarios"" ADD COLUMN IF NOT EXISTS ""Documento""         text                     NOT NULL DEFAULT '';");
            migrationBuilder.Sql(@"ALTER TABLE ""Usuarios"" ADD COLUMN IF NOT EXISTS ""Email""             text                     NOT NULL DEFAULT '';");
            migrationBuilder.Sql(@"ALTER TABLE ""Usuarios"" ADD COLUMN IF NOT EXISTS ""IntentosFailidos""  integer                  NOT NULL DEFAULT 0;");

            // ── IdPreceptor en Cursos ────────────────────────────────────────────────────
            migrationBuilder.Sql(@"ALTER TABLE ""Cursos"" ADD COLUMN IF NOT EXISTS ""IdPreceptor"" uuid;");

            // ── Tabla RefreshTokens ──────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""RefreshTokens"" (
                    ""Id""            uuid                     NOT NULL,
                    ""Token""         text                     NOT NULL,
                    ""FechaCreacion"" timestamp with time zone NOT NULL,
                    ""Expiracion""    timestamp with time zone NOT NULL,
                    ""Revocado""      boolean                  NOT NULL,
                    ""IdUsuario""     uuid                     NOT NULL,
                    CONSTRAINT ""PK_RefreshTokens"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_RefreshTokens_Usuarios_IdUsuario""
                        FOREIGN KEY (""IdUsuario"") REFERENCES ""Usuarios"" (""IdUsuario"") ON DELETE CASCADE
                );");

            // ── Índices ──────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Usuarios_Documento""       ON ""Usuarios""      (""Documento"");");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Usuarios_Email""           ON ""Usuarios""      (""Email"");");
            migrationBuilder.Sql(@"CREATE        INDEX IF NOT EXISTS ""IX_Cursos_IdPreceptor""       ON ""Cursos""        (""IdPreceptor"");");
            migrationBuilder.Sql(@"CREATE        INDEX IF NOT EXISTS ""IX_RefreshTokens_IdUsuario""  ON ""RefreshTokens"" (""IdUsuario"");");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_RefreshTokens_Token""      ON ""RefreshTokens"" (""Token"");");

            // ── Foreign keys (con guarda IF NOT EXISTS via DO block) ─────────────────────
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Cursos_Preceptores_IdPreceptor') THEN
                        ALTER TABLE ""Cursos"" ADD CONSTRAINT ""FK_Cursos_Preceptores_IdPreceptor""
                            FOREIGN KEY (""IdPreceptor"") REFERENCES ""Preceptores"" (""IdPreceptor"") ON DELETE SET NULL;
                    END IF;
                END $$;");

            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_EspaciosCurriculares_Docentes_IdDocente') THEN
                        ALTER TABLE ""EspaciosCurriculares"" ADD CONSTRAINT ""FK_EspaciosCurriculares_Docentes_IdDocente""
                            FOREIGN KEY (""IdDocente"") REFERENCES ""Docentes"" (""IdDocente"") ON DELETE SET NULL;
                    END IF;
                END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cursos_Preceptores_IdPreceptor",
                table: "Cursos");

            migrationBuilder.DropForeignKey(
                name: "FK_EspaciosCurriculares_Docentes_IdDocente",
                table: "EspaciosCurriculares");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_Documento",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Cursos_IdPreceptor",
                table: "Cursos");

            migrationBuilder.DropColumn(
                name: "Apellido",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "BloqueadoHasta",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Documento",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "IntentosFailidos",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "IdPreceptor",
                table: "Cursos");

            migrationBuilder.RenameColumn(
                name: "Nombre",
                table: "Usuarios",
                newName: "Mail");

            migrationBuilder.AddColumn<bool>(
                name: "Verificado",
                table: "Usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Apellido",
                table: "Preceptores",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CursoIdCurso",
                table: "Preceptores",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Documento",
                table: "Preceptores",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "IdCurso",
                table: "Preceptores",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Nombre",
                table: "Preceptores",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "IdDocente",
                table: "EspaciosCurriculares",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Apellido",
                table: "Docentes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Documento",
                table: "Docentes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Docentes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Nombre",
                table: "Docentes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Preceptores_CursoIdCurso",
                table: "Preceptores",
                column: "CursoIdCurso");

            migrationBuilder.AddForeignKey(
                name: "FK_EspaciosCurriculares_Docentes_IdDocente",
                table: "EspaciosCurriculares",
                column: "IdDocente",
                principalTable: "Docentes",
                principalColumn: "IdDocente",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Preceptores_Cursos_CursoIdCurso",
                table: "Preceptores",
                column: "CursoIdCurso",
                principalTable: "Cursos",
                principalColumn: "IdCurso",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
