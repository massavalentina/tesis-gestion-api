using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class FeatUmbralNotificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =========================================================
            // Estudiantes.TeaGeneral
            // =========================================================
            migrationBuilder.Sql("""
                ALTER TABLE "Estudiantes"
                ADD COLUMN IF NOT EXISTS "TeaGeneral" boolean NOT NULL DEFAULT FALSE;
            """);

            // =========================================================
            // Tabla AsistenciaResumenAnual
            // =========================================================
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "AsistenciaResumenAnual" (
                    "IdResumen" uuid NOT NULL,
                    "IdEstudiante" uuid NOT NULL,
                    "AnioLectivo" integer NOT NULL,
                    "FaltasAcumuladas" numeric NOT NULL,
                    "UltimoRecalculoUtc" timestamp with time zone NOT NULL,
                    "TeaGeneral" boolean NOT NULL,
                    "FechaTeaGeneralUtc" timestamp with time zone NULL,
                    CONSTRAINT "PK_AsistenciaResumenAnual" PRIMARY KEY ("IdResumen")
                );
            """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'FK_AsistenciaResumenAnual_Estudiantes_IdEstudiante'
                    ) THEN
                        ALTER TABLE "AsistenciaResumenAnual"
                        ADD CONSTRAINT "FK_AsistenciaResumenAnual_Estudiantes_IdEstudiante"
                        FOREIGN KEY ("IdEstudiante")
                        REFERENCES "Estudiantes" ("IdEstudiante")
                        ON DELETE CASCADE;
                    END IF;
                END
                $$;
            """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_AsistenciaResumenAnual_IdEstudiante_AnioLectivo"
                ON "AsistenciaResumenAnual" ("IdEstudiante", "AnioLectivo");
            """);

            // =========================================================
            // Tabla AsistenciaUmbralNotificacion
            // =========================================================
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "AsistenciaUmbralNotificacion" (
                    "IdNotif" uuid NOT NULL,
                    "IdEstudiante" uuid NOT NULL,
                    "AnioLectivo" integer NOT NULL,
                    "Umbral" integer NOT NULL,
                    "CantidadEnviados" integer NOT NULL,
                    "ProximoEnvioUtc" timestamp with time zone NOT NULL,
                    "Estado" character varying(20) NOT NULL,
                    "CreadoUtc" timestamp with time zone NOT NULL,
                    "UltimoEnvioUtc" timestamp with time zone NULL,
                    "UltimoError" text NULL,
                    CONSTRAINT "PK_AsistenciaUmbralNotificacion" PRIMARY KEY ("IdNotif")
                );
            """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'FK_AsistenciaUmbralNotificacion_Estudiantes_IdEstudiante'
                    ) THEN
                        ALTER TABLE "AsistenciaUmbralNotificacion"
                        ADD CONSTRAINT "FK_AsistenciaUmbralNotificacion_Estudiantes_IdEstudiante"
                        FOREIGN KEY ("IdEstudiante")
                        REFERENCES "Estudiantes" ("IdEstudiante")
                        ON DELETE CASCADE;
                    END IF;
                END
                $$;
            """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_AsistenciaUmbralNotificacion_Estado_ProximoEnvioUtc"
                ON "AsistenciaUmbralNotificacion" ("Estado", "ProximoEnvioUtc");
            """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_AsistenciaUmbralNotificacion_IdEstudiante_AnioLectivo_Umbral"
                ON "AsistenciaUmbralNotificacion" ("IdEstudiante", "AnioLectivo", "Umbral");
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS "AsistenciaResumenAnual";
            """);

            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS "AsistenciaUmbralNotificacion";
            """);

            migrationBuilder.Sql("""
                ALTER TABLE "Estudiantes"
                DROP COLUMN IF EXISTS "TeaGeneral";
            """);
        }
    }
}
